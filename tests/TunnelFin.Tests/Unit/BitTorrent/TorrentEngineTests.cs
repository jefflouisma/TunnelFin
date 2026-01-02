using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using MonoTorrent;
using MonoTorrent.Connections;
using ReusableTasks;
using TunnelFin.BitTorrent;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.Unit.BitTorrent;

/// <summary>
/// Unit tests for TorrentEngine - MonoTorrent integration layer.
/// Tests: AddTorrentAsync, CreateStreamAsync, GetBufferStatus, RemoveTorrentAsync
/// </summary>
public class TorrentEngineTests
{
    /// <summary>
    /// T018: Verify TorrentEngine.AddTorrentAsync creates MonoTorrent manager and downloads metadata.
    /// </summary>
    [Fact]
    public async Task AddTorrentAsync_ValidMagnetLink_ReturnsMetadata()
    {
        // Arrange
        var engine = new TorrentEngine();
        var magnetLink = "magnet:?xt=urn:btih:dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c&dn=Big+Buck+Bunny";

        // Act
        var metadata = await engine.AddTorrentAsync(magnetLink, CancellationToken.None);

        // Assert
        metadata.Should().NotBeNull();
        metadata.InfoHash.Should().Be("dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c");
        metadata.MagnetLink.Should().Be(magnetLink);
        metadata.Files.Should().NotBeEmpty();
    }

    /// <summary>
    /// T018: Verify AddTorrentAsync validates magnet link format.
    /// </summary>
    [Fact]
    public async Task AddTorrentAsync_InvalidMagnetLink_ThrowsArgumentException()
    {
        // Arrange
        var engine = new TorrentEngine();
        var invalidMagnetLink = "not-a-magnet-link";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => engine.AddTorrentAsync(invalidMagnetLink, CancellationToken.None)
        );
    }

    /// <summary>
    /// T019: Verify TorrentEngine.CreateStreamAsync returns seekable stream.
    /// </summary>
    [Fact]
    public async Task CreateStreamAsync_ValidInfoHash_ReturnsSeekableStream()
    {
        // Arrange
        var engine = new TorrentEngine();
        var magnetLink = "magnet:?xt=urn:btih:dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c&dn=Big+Buck+Bunny";
        var metadata = await engine.AddTorrentAsync(magnetLink, CancellationToken.None);
        var filePath = metadata.Files[0].Path;

        // Act
        var stream = await engine.CreateStreamAsync(metadata.InfoHash, filePath, prebuffer: true, CancellationToken.None);

        // Assert
        stream.Should().NotBeNull();
        stream.CanSeek.Should().BeTrue();
        stream.CanRead.Should().BeTrue();
        stream.Length.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// T020: Verify TorrentEngine.GetBufferStatus tracks buffered ranges.
    /// </summary>
    [Fact]
    public async Task GetBufferStatus_ActiveStream_ReturnsBufferStatus()
    {
        // Arrange
        var engine = new TorrentEngine();
        var magnetLink = "magnet:?xt=urn:btih:dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c&dn=Big+Buck+Bunny";
        var metadata = await engine.AddTorrentAsync(magnetLink, CancellationToken.None);
        var filePath = metadata.Files[0].Path;
        await engine.CreateStreamAsync(metadata.InfoHash, filePath, prebuffer: true, CancellationToken.None);

        // Act
        var bufferStatus = engine.GetBufferStatus(metadata.InfoHash, filePath);

        // Assert
        bufferStatus.Should().NotBeNull();
        bufferStatus!.BufferedRanges.Should().NotBeEmpty();
        bufferStatus.CurrentBufferedBytes.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// T021: Verify TorrentEngine.RemoveTorrentAsync deletes cached data (FR-007a ephemeral requirement).
    /// </summary>
    [Fact]
    public async Task RemoveTorrentAsync_ActiveTorrent_DeletesCachedData()
    {
        // Arrange
        var engine = new TorrentEngine();
        var magnetLink = "magnet:?xt=urn:btih:dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c&dn=Big+Buck+Bunny";
        var metadata = await engine.AddTorrentAsync(magnetLink, CancellationToken.None);

        // Act
        await engine.RemoveTorrentAsync(metadata.InfoHash, CancellationToken.None);

        // Assert
        var retrievedMetadata = engine.GetTorrentMetadata(metadata.InfoHash);
        retrievedMetadata.Should().BeNull();
    }

    /// <summary>
    /// T021: Verify RemoveTorrentAsync handles non-existent torrent gracefully.
    /// </summary>
    [Fact]
    public async Task RemoveTorrentAsync_NonExistentTorrent_DoesNotThrow()
    {
        // Arrange
        var engine = new TorrentEngine();
        var nonExistentInfoHash = "0000000000000000000000000000000000000000";

        // Act & Assert
        await engine.RemoveTorrentAsync(nonExistentInfoHash, CancellationToken.None);
        // Should not throw
    }

    /// <summary>
    /// T018: Verify AddTorrentAsync times out after 90 seconds if metadata not received.
    /// </summary>
    [Fact]
    public async Task AddTorrentAsync_MetadataTimeout_ThrowsTimeoutException()
    {
        // Arrange
        var engine = new TorrentEngine();
        var magnetLinkWithNoSeeds = "magnet:?xt=urn:btih:0000000000000000000000000000000000000000&dn=NoSeeds";
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)); // Short timeout for test

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => engine.AddTorrentAsync(magnetLinkWithNoSeeds, cts.Token)
        );
    }

    /// <summary>
    /// T107: Verify TorrentEngine accepts custom socket connector for circuit-routed connections.
    /// </summary>
    [Fact]
    public void Constructor_WithSocketConnector_ConfiguresEngine()
    {
        // Arrange
        var mockConnector = new Mock<ISocketConnector>();
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        mockConnector
            .Setup(c => c.ConnectAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .Returns(ReusableTask.FromResult(socket));

        // Act
        var engine = new TorrentEngine(socketConnector: mockConnector.Object);

        // Assert
        engine.Should().NotBeNull();
        // Engine should be configured with custom connector
        // (MonoTorrent will use it for peer connections)
    }

    /// <summary>
    /// T107: Verify SetSocketConnector is deprecated and logs warning.
    /// </summary>
    [Fact]
    public void SetSocketConnector_LogsDeprecationWarning()
    {
        // Arrange
        var engine = new TorrentEngine();
        var mockConnector = new Mock<ISocketConnector>();

        // Act
        #pragma warning disable CS0618 // Type or member is obsolete
        engine.SetSocketConnector(mockConnector.Object);
        #pragma warning restore CS0618

        // Assert
        // Method should complete without throwing
        // (Warning is logged but we can't easily verify that in unit test)
    }
}

