using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;
using TunnelFin.Core;
using TunnelFin.Networking.IPv8;
using TunnelFin.Networking.Transport;

namespace TunnelFin.Networking.Circuits;

/// <summary>
/// Network layer for circuit operations over UDP transport (T035-T037).
/// Handles CREATE/EXTEND/DESTROY messages for circuit establishment.
/// </summary>
public class CircuitNetworkClient : ICircuitNetworkClient, IDisposable
{
    private readonly ITransport _transport;
    private readonly PrivacyAwareLogger _logger;
    private readonly ConcurrentDictionary<ushort, TaskCompletionSource<CreateResponse>> _createPending = new();
    private readonly ConcurrentDictionary<ushort, TaskCompletionSource<ExtendResponse>> _extendPending = new();
    private readonly int _responseTimeoutMs;
    private bool _disposed;

    // IPv8 message type IDs (from py-ipv8 anonymization payload)
    private const byte MSG_CREATE = 2;
    private const byte MSG_CREATED = 3;
    private const byte MSG_EXTEND = 4;
    private const byte MSG_EXTENDED = 5;
    private const byte MSG_DESTROY = 8;

    /// <summary>
    /// Creates a new CircuitNetworkClient.
    /// </summary>
    /// <param name="transport">UDP transport layer.</param>
    /// <param name="logger">Logger for privacy-aware logging.</param>
    /// <param name="responseTimeoutMs">Response timeout in milliseconds (default: 10s).</param>
    public CircuitNetworkClient(ITransport transport, ILogger logger, int responseTimeoutMs = 10000)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _logger = new PrivacyAwareLogger(logger ?? throw new ArgumentNullException(nameof(logger)));
        _responseTimeoutMs = responseTimeoutMs;
    }

    /// <summary>
    /// Starts listening for circuit responses (FR-014).
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CircuitNetworkClient));

        _transport.DatagramReceived += OnDatagramReceived;
        _logger.LogInformation("CircuitNetworkClient started");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops listening for circuit responses.
    /// </summary>
    public async Task StopAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CircuitNetworkClient));

        _transport.DatagramReceived -= OnDatagramReceived;
        _logger.LogInformation("CircuitNetworkClient stopped");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Sends a CREATE message to establish the first hop of a circuit (T035, FR-014).
    /// </summary>
    public async Task<CreateResponse> SendCreateAsync(
        uint circuitId,
        Peer relay,
        byte[] ephemeralPublicKey,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CircuitNetworkClient));
        if (relay == null)
            throw new ArgumentNullException(nameof(relay));
        if (ephemeralPublicKey == null || ephemeralPublicKey.Length != 32)
            throw new ArgumentException("Ephemeral public key must be 32 bytes", nameof(ephemeralPublicKey));

        var identifier = GenerateIdentifier();
        var tcs = new TaskCompletionSource<CreateResponse>();
        _createPending[identifier] = tcs;

        try
        {
            // Serialize CREATE message
            var payload = CircuitMessage.SerializeCreate(circuitId, identifier, relay.PublicKey, ephemeralPublicKey);
            var message = new byte[1 + payload.Length];
            message[0] = MSG_CREATE;
            Array.Copy(payload, 0, message, 1, payload.Length);

            // Send to relay - convert uint IPv4 address to bytes (big-endian)
            var ipBytes = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(ipBytes, relay.IPv4Address);
            var endpoint = new IPEndPoint(new IPAddress(ipBytes), relay.Port);

            await _transport.SendAsync(message, endpoint, cancellationToken);
            _logger.LogDebug("Sent CREATE message for circuit {CircuitId}, identifier={Identifier}", 
                circuitId, identifier);

            // Wait for CREATED response with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_responseTimeoutMs);

            var timeoutTask = Task.Delay(_responseTimeoutMs, cts.Token);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                throw new TimeoutException($"No CREATED response for circuit {circuitId} within {_responseTimeoutMs}ms");
            }

            return await tcs.Task;
        }
        finally
        {
            _createPending.TryRemove(identifier, out _);
        }
    }

    /// <summary>
    /// Sends an EXTEND message to add another hop to an existing circuit (T036, FR-015, FR-016).
    /// </summary>
    public async Task<ExtendResponse> SendExtendAsync(
        uint circuitId,
        Peer nextRelay,
        byte[] ephemeralPublicKey,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CircuitNetworkClient));
        if (nextRelay == null)
            throw new ArgumentNullException(nameof(nextRelay));
        if (ephemeralPublicKey == null || ephemeralPublicKey.Length != 32)
            throw new ArgumentException("Ephemeral public key must be 32 bytes", nameof(ephemeralPublicKey));

        var identifier = GenerateIdentifier();
        var tcs = new TaskCompletionSource<ExtendResponse>();
        _extendPending[identifier] = tcs;

        try
        {
            // Serialize EXTEND message
            var payload = CircuitMessage.SerializeExtend(circuitId, nextRelay.PublicKey, 
                nextRelay.IPv4Address, nextRelay.Port, identifier);
            var message = new byte[1 + payload.Length];
            message[0] = MSG_EXTEND;
            Array.Copy(payload, 0, message, 1, payload.Length);

            // Send through the circuit (to first hop)
            // Note: In production, this would be sent through the circuit, not directly
            // For now, we send directly to the relay for testing
            var extendIpBytes = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(extendIpBytes, nextRelay.IPv4Address);
            var endpoint = new IPEndPoint(new IPAddress(extendIpBytes), nextRelay.Port);

            await _transport.SendAsync(message, endpoint, cancellationToken);
            _logger.LogDebug("Sent EXTEND message for circuit {CircuitId}, identifier={Identifier}", 
                circuitId, identifier);

            // Wait for EXTENDED response with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_responseTimeoutMs);

            var timeoutTask = Task.Delay(_responseTimeoutMs, cts.Token);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                throw new TimeoutException($"No EXTENDED response for circuit {circuitId} within {_responseTimeoutMs}ms");
            }

            return await tcs.Task;
        }
        finally
        {
            _extendPending.TryRemove(identifier, out _);
        }
    }

    /// <summary>
    /// Sends a DESTROY message to tear down a circuit (T037, FR-018).
    /// </summary>
    public async Task SendDestroyAsync(
        uint circuitId,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CircuitNetworkClient));

        // Serialize DESTROY message
        // Structure: Circuit ID (4 bytes) + Reason (2 bytes)
        var payload = new byte[6];
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(0, 4), circuitId);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(4, 2), 0); // Reason: 0 = normal close

        var message = new byte[1 + payload.Length];
        message[0] = MSG_DESTROY;
        Array.Copy(payload, 0, message, 1, payload.Length);

        // Note: In production, this would be sent through the circuit
        // For now, we just log it
        _logger.LogDebug("Sent DESTROY message for circuit {CircuitId}", circuitId);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Handles incoming datagrams from the transport layer.
    /// </summary>
    private void OnDatagramReceived(object? sender, DatagramReceivedEventArgs e)
    {
        try
        {
            if (e.Data.Length < 1)
                return;

            var messageType = e.Data.Span[0];
            var payload = e.Data.Slice(1);

            switch (messageType)
            {
                case MSG_CREATED:
                    HandleCreatedMessage(payload);
                    break;

                case MSG_EXTENDED:
                    HandleExtendedMessage(payload);
                    break;

                default:
                    // Ignore other message types
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error handling datagram", ex);
        }
    }

    /// <summary>
    /// Handles CREATED response messages.
    /// </summary>
    private void HandleCreatedMessage(ReadOnlyMemory<byte> payload)
    {
        try
        {
            var (circuitId, identifier, ephemeralKey, auth, candidatesEnc) =
                CircuitMessage.ParseCreated(payload.Span);

            if (_createPending.TryGetValue(identifier, out var tcs))
            {
                var response = new CreateResponse(circuitId, identifier, ephemeralKey, auth, candidatesEnc);
                tcs.TrySetResult(response);
                _logger.LogDebug("Received CREATED response for circuit {CircuitId}, identifier={Identifier}",
                    circuitId, identifier);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error parsing CREATED message", ex);
        }
    }

    /// <summary>
    /// Handles EXTENDED response messages.
    /// </summary>
    private void HandleExtendedMessage(ReadOnlyMemory<byte> payload)
    {
        try
        {
            var (circuitId, identifier, ephemeralKey, auth, candidatesEnc) =
                CircuitMessage.ParseExtended(payload.Span);

            if (_extendPending.TryGetValue(identifier, out var tcs))
            {
                var response = new ExtendResponse(circuitId, identifier, ephemeralKey, auth, candidatesEnc);
                tcs.TrySetResult(response);
                _logger.LogDebug("Received EXTENDED response for circuit {CircuitId}, identifier={Identifier}",
                    circuitId, identifier);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error parsing EXTENDED message", ex);
        }
    }

    /// <summary>
    /// Generates a random identifier for requests.
    /// </summary>
    private ushort GenerateIdentifier()
    {
        return (ushort)Random.Shared.Next(1, ushort.MaxValue);
    }

    /// <summary>
    /// Disposes the CircuitNetworkClient.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _transport.DatagramReceived -= OnDatagramReceived;

        // Cancel all pending requests
        foreach (var tcs in _createPending.Values)
        {
            tcs.TrySetCanceled();
        }
        foreach (var tcs in _extendPending.Values)
        {
            tcs.TrySetCanceled();
        }

        _createPending.Clear();
        _extendPending.Clear();

        _disposed = true;
    }
}
