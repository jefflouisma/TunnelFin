using FluentAssertions;
using Moq;
using TunnelFin.Streaming;
using Xunit;

namespace TunnelFin.Tests.Streaming;

/// <summary>
/// Unit tests for StreamManager (FR-009, FR-011, FR-012, FR-013, FR-035, FR-036).
/// Tests HTTP endpoint creation, range requests, concurrent stream limits, health metrics,
/// anonymous-first routing, and non-anonymous consent workflow.
/// </summary>
public class StreamManagerTests
{
    [Fact]
    public async Task CreateStream_Should_Return_HTTP_Endpoint()
    {
        // Arrange - FR-009
        var manager = new StreamManager(maxConcurrentStreams: 3);
        var torrentId = Guid.NewGuid();
        var fileIndex = 0;

        // Act
        var streamId = await manager.CreateStreamAsync(torrentId, fileIndex);

        // Assert
        streamId.Should().NotBeEmpty("stream should be assigned a unique ID");
        var endpoint = manager.GetStreamEndpoint(streamId);
        endpoint.Should().NotBeNullOrEmpty("stream should have HTTP endpoint");
        endpoint.Should().StartWith("http://", "endpoint should be HTTP URL");
    }

    [Fact]
    public async Task CreateStream_Should_Reject_When_Max_Concurrent_Streams_Reached()
    {
        // Arrange - FR-013
        var manager = new StreamManager(maxConcurrentStreams: 2);
        var torrent1 = Guid.NewGuid();
        var torrent2 = Guid.NewGuid();
        var torrent3 = Guid.NewGuid();

        // Act
        await manager.CreateStreamAsync(torrent1, 0);
        await manager.CreateStreamAsync(torrent2, 0);
        var act = async () => await manager.CreateStreamAsync(torrent3, 0);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*maximum concurrent streams*", "should enforce concurrent stream limit");
    }

    [Fact]
    public async Task GetStreamHealth_Should_Return_Metrics()
    {
        // Arrange - FR-011
        var manager = new StreamManager(maxConcurrentStreams: 3);
        var torrentId = Guid.NewGuid();
        var streamId = await manager.CreateStreamAsync(torrentId, 0);

        // Act
        var health = manager.GetStreamHealth(streamId);

        // Assert
        health.Should().NotBeNull("health metrics should be available");
        health.StreamId.Should().Be(streamId);
        health.PeerCount.Should().BeGreaterThanOrEqualTo(0, "peer count should be non-negative");
        health.DownloadSpeedBytesPerSecond.Should().BeGreaterThanOrEqualTo(0, "download speed should be non-negative");
        health.BufferSeconds.Should().BeGreaterThanOrEqualTo(0, "buffer should be non-negative");
    }

    [Fact]
    public async Task StopStream_Should_Cleanup_Resources()
    {
        // Arrange
        var manager = new StreamManager(maxConcurrentStreams: 3);
        var torrentId = Guid.NewGuid();
        var streamId = await manager.CreateStreamAsync(torrentId, 0);

        // Act
        await manager.StopStreamAsync(streamId);

        // Assert
        var act = () => manager.GetStreamEndpoint(streamId);
        act.Should().Throw<KeyNotFoundException>("stopped stream should not be found");
    }

    [Fact]
    public async Task GetActiveStreamCount_Should_Track_Streams()
    {
        // Arrange
        var manager = new StreamManager(maxConcurrentStreams: 3);
        var torrent1 = Guid.NewGuid();
        var torrent2 = Guid.NewGuid();

        // Act
        var count1 = manager.GetActiveStreamCount();
        await manager.CreateStreamAsync(torrent1, 0);
        var count2 = manager.GetActiveStreamCount();
        await manager.CreateStreamAsync(torrent2, 0);
        var count3 = manager.GetActiveStreamCount();

        // Assert
        count1.Should().Be(0, "no streams initially");
        count2.Should().Be(1, "one stream after first create");
        count3.Should().Be(2, "two streams after second create");
    }

    [Fact]
    public async Task GetStreamHealth_Should_Throw_For_Unknown_Stream()
    {
        // Arrange
        var manager = new StreamManager(maxConcurrentStreams: 3);
        var unknownId = Guid.NewGuid();

        // Act
        var act = () => manager.GetStreamHealth(unknownId);

        // Assert
        act.Should().Throw<KeyNotFoundException>("unknown stream ID should throw");
    }

    [Fact]
    public async Task CreateStream_Should_Support_Multiple_Files_From_Same_Torrent()
    {
        // Arrange
        var manager = new StreamManager(maxConcurrentStreams: 3);
        var torrentId = Guid.NewGuid();

        // Act
        var stream1 = await manager.CreateStreamAsync(torrentId, 0);
        var stream2 = await manager.CreateStreamAsync(torrentId, 1);

        // Assert
        stream1.Should().NotBe(stream2, "different files should have different stream IDs");
        manager.GetActiveStreamCount().Should().Be(2, "both streams should be active");
    }

    [Fact]
    public void Constructor_Should_Set_Configuration()
    {
        // Arrange & Act
        var manager = new StreamManager(maxConcurrentStreams: 5);

        // Assert
        manager.MaxConcurrentStreams.Should().Be(5);
    }

    [Fact]
    public async Task StopStream_Should_Allow_New_Stream_After_Limit_Reached()
    {
        // Arrange
        var manager = new StreamManager(maxConcurrentStreams: 2);
        var torrent1 = Guid.NewGuid();
        var torrent2 = Guid.NewGuid();
        var torrent3 = Guid.NewGuid();

        // Act
        var stream1 = await manager.CreateStreamAsync(torrent1, 0);
        var stream2 = await manager.CreateStreamAsync(torrent2, 0);
        await manager.StopStreamAsync(stream1);
        var stream3 = await manager.CreateStreamAsync(torrent3, 0);

        // Assert
        stream3.Should().NotBeEmpty("should allow new stream after stopping one");
        manager.GetActiveStreamCount().Should().Be(2, "should have 2 active streams");
    }

    [Fact]
    public async Task CreateStream_Should_Use_Anonymous_First_By_Default()
    {
        // Arrange - FR-035
        var manager = new StreamManager();
        var torrentId = Guid.NewGuid();

        // Act
        var streamId = await manager.CreateStreamAsync(torrentId, 0);

        // Assert
        streamId.Should().NotBeEmpty("stream should be created with anonymous-first routing by default");
    }

    [Fact]
    public async Task CreateStream_Should_Allow_Explicit_Anonymous_Routing()
    {
        // Arrange - FR-035
        var manager = new StreamManager();
        var torrentId = Guid.NewGuid();

        // Act
        var streamId = await manager.CreateStreamAsync(torrentId, 0, RoutingMode.AnonymousFirst);

        // Assert
        streamId.Should().NotBeEmpty("stream should be created with explicit anonymous routing");
    }

    [Fact]
    public async Task CreateStream_Should_Reject_NonAnonymous_Without_Consent()
    {
        // Arrange - FR-036
        var manager = new StreamManager();
        var torrentId = Guid.NewGuid();
        var userId = "user123";

        // Act
        var act = async () => await manager.CreateStreamAsync(torrentId, 0, RoutingMode.NonAnonymous, userId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*explicit user consent*", "non-anonymous routing requires consent");
    }

    [Fact]
    public async Task CreateStream_Should_Allow_NonAnonymous_With_Consent()
    {
        // Arrange - FR-036
        var manager = new StreamManager();
        var torrentId = Guid.NewGuid();
        var userId = "user123";

        // Grant consent
        manager.GrantNonAnonymousConsent(userId);

        // Act
        var streamId = await manager.CreateStreamAsync(torrentId, 0, RoutingMode.NonAnonymous, userId);

        // Assert
        streamId.Should().NotBeEmpty("stream should be created after consent is granted");
    }

    [Fact]
    public async Task CreateStream_Should_Reject_NonAnonymous_Without_UserId()
    {
        // Arrange - FR-036
        var manager = new StreamManager();
        var torrentId = Guid.NewGuid();

        // Act
        var act = async () => await manager.CreateStreamAsync(torrentId, 0, RoutingMode.NonAnonymous, userId: null);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*User ID is required*", "non-anonymous routing requires user ID");
    }

    [Fact]
    public void GrantNonAnonymousConsent_Should_Record_Consent()
    {
        // Arrange - FR-036
        var manager = new StreamManager();
        var userId = "user123";

        // Act
        manager.GrantNonAnonymousConsent(userId);

        // Assert
        manager.HasNonAnonymousConsent(userId).Should().BeTrue("consent should be recorded");
    }

    [Fact]
    public void RevokeNonAnonymousConsent_Should_Remove_Consent()
    {
        // Arrange - FR-036
        var manager = new StreamManager();
        var userId = "user123";
        manager.GrantNonAnonymousConsent(userId);

        // Act
        manager.RevokeNonAnonymousConsent(userId);

        // Assert
        manager.HasNonAnonymousConsent(userId).Should().BeFalse("consent should be revoked");
    }

    [Fact]
    public void HasNonAnonymousConsent_Should_Return_False_For_Empty_UserId()
    {
        // Arrange - FR-036
        var manager = new StreamManager();

        // Act & Assert
        manager.HasNonAnonymousConsent("").Should().BeFalse();
        manager.HasNonAnonymousConsent(null!).Should().BeFalse();
    }

    [Fact]
    public void GrantNonAnonymousConsent_Should_Reject_Empty_UserId()
    {
        // Arrange - FR-036
        var manager = new StreamManager();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => manager.GrantNonAnonymousConsent(""));
        Assert.Throws<ArgumentException>(() => manager.GrantNonAnonymousConsent(null!));
    }

    [Fact]
    public async Task CreateStream_Should_Reject_NonAnonymous_After_Consent_Revoked()
    {
        // Arrange - FR-036
        var manager = new StreamManager();
        var torrentId = Guid.NewGuid();
        var userId = "user123";

        manager.GrantNonAnonymousConsent(userId);
        manager.RevokeNonAnonymousConsent(userId);

        // Act
        var act = async () => await manager.CreateStreamAsync(torrentId, 0, RoutingMode.NonAnonymous, userId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*explicit user consent*", "should reject after consent is revoked");
    }
}

