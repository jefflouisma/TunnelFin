using FluentAssertions;
using TunnelFin.BitTorrent;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.BitTorrent;

/// <summary>
/// Unit tests for TorrentStreamWrapper (FR-007, FR-008, FR-009).
/// Tests torrent stream wrapper with MonoTorrent integration.
/// </summary>
public class TorrentStreamTests
{
    [Fact]
    public void Constructor_Should_Initialize_Properties()
    {
        // Arrange
        var torrentId = Guid.NewGuid();
        var magnetLink = "magnet:?xt=urn:btih:1234567890abcdef1234567890abcdef12345678";

        // Act
        var stream = new TorrentStreamWrapper(torrentId, magnetLink);

        // Assert
        stream.TorrentId.Should().Be(torrentId);
        stream.MagnetLink.Should().Be(magnetLink);
        stream.State.Should().Be(TorrentState.Initializing, "should start in initializing state");
    }

    [Fact]
    public void GetStatus_Should_Return_Current_State()
    {
        // Arrange
        var torrentId = Guid.NewGuid();
        var magnetLink = "magnet:?xt=urn:btih:1234567890abcdef1234567890abcdef12345678";
        var stream = new TorrentStreamWrapper(torrentId, magnetLink);

        // Act
        var status = stream.GetStatus();

        // Assert
        status.Should().NotBeNull();
        status.TorrentId.Should().Be(torrentId);
        status.State.Should().Be(TorrentState.Initializing);
        status.DownloadProgress.Should().Be(0, "no progress initially");
    }

    [Fact]
    public void UpdateProgress_Should_Update_Download_Metrics()
    {
        // Arrange
        var stream = new TorrentStreamWrapper(Guid.NewGuid(), "magnet:?xt=urn:btih:test");

        // Act
        stream.UpdateProgress(
            downloadedBytes: 1_000_000,
            totalBytes: 10_000_000,
            downloadSpeed: 100_000,
            uploadSpeed: 50_000,
            peerCount: 10
        );

        var status = stream.GetStatus();

        // Assert
        status.DownloadedBytes.Should().Be(1_000_000);
        status.TotalSizeBytes.Should().Be(10_000_000);
        status.DownloadProgress.Should().BeApproximately(10.0, 0.1, "10% downloaded");
        status.DownloadSpeedBytesPerSecond.Should().Be(100_000);
        status.UploadSpeedBytesPerSecond.Should().Be(50_000);
        status.PeerCount.Should().Be(10);
    }

    [Fact]
    public void SetState_Should_Update_Torrent_State()
    {
        // Arrange
        var stream = new TorrentStreamWrapper(Guid.NewGuid(), "magnet:?xt=urn:btih:test");

        // Act
        stream.SetState(TorrentState.Downloading);
        var status1 = stream.GetStatus();

        stream.SetState(TorrentState.Seeding);
        var status2 = stream.GetStatus();

        // Assert
        status1.State.Should().Be(TorrentState.Downloading);
        status2.State.Should().Be(TorrentState.Seeding);
    }

    [Fact]
    public void GetFileCount_Should_Return_Number_Of_Files()
    {
        // Arrange
        var stream = new TorrentStreamWrapper(Guid.NewGuid(), "magnet:?xt=urn:btih:test");

        // Act - Initially no files until metadata is loaded
        var count = stream.GetFileCount();

        // Assert
        count.Should().Be(0, "no files until metadata is loaded");
    }

    [Fact]
    public void GetFileCount_Should_Reflect_Added_Files()
    {
        // Arrange
        var stream = new TorrentStreamWrapper(Guid.NewGuid(), "magnet:?xt=urn:btih:test");

        // Act - Add files using SetFileInfo
        stream.SetFileInfo(0, "file1.mp4", 1000);
        stream.SetFileInfo(1, "file2.mp4", 2000);
        stream.SetFileInfo(2, "file3.mp4", 3000);
        var count = stream.GetFileCount();

        // Assert
        count.Should().Be(3, "three files were added");
    }

    [Fact]
    public void GetFileName_Should_Return_File_Name_By_Index()
    {
        // Arrange
        var stream = new TorrentStreamWrapper(Guid.NewGuid(), "magnet:?xt=urn:btih:test");
        stream.SetFileInfo(0, "video.mp4", 1_000_000_000);
        stream.SetFileInfo(1, "subtitle.srt", 50_000);

        // Act
        var name0 = stream.GetFileName(0);
        var name1 = stream.GetFileName(1);

        // Assert
        name0.Should().Be("video.mp4");
        name1.Should().Be("subtitle.srt");
    }

    [Fact]
    public void GetFileSize_Should_Return_File_Size_By_Index()
    {
        // Arrange
        var stream = new TorrentStreamWrapper(Guid.NewGuid(), "magnet:?xt=urn:btih:test");
        stream.SetFileInfo(0, "video.mp4", 1_000_000_000);

        // Act
        var size = stream.GetFileSize(0);

        // Assert
        size.Should().Be(1_000_000_000);
    }

    [Fact]
    public void GetFileName_Should_Throw_For_Invalid_Index()
    {
        // Arrange
        var stream = new TorrentStreamWrapper(Guid.NewGuid(), "magnet:?xt=urn:btih:test");

        // Act
        var act = () => stream.GetFileName(99);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>("invalid file index should throw");
    }

    [Fact]
    public void IsMetadataLoaded_Should_Return_False_Initially()
    {
        // Arrange
        var stream = new TorrentStreamWrapper(Guid.NewGuid(), "magnet:?xt=urn:btih:test");

        // Act
        var isLoaded = stream.IsMetadataLoaded();

        // Assert
        isLoaded.Should().BeFalse("metadata not loaded initially");
    }

    [Fact]
    public void SetMetadataLoaded_Should_Update_Metadata_Status()
    {
        // Arrange
        var stream = new TorrentStreamWrapper(Guid.NewGuid(), "magnet:?xt=urn:btih:test");

        // Act
        stream.SetMetadataLoaded(true);
        var isLoaded = stream.IsMetadataLoaded();

        // Assert
        isLoaded.Should().BeTrue("metadata should be marked as loaded");
    }
}

