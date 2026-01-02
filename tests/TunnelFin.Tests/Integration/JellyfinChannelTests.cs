using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;
using Moq;
using TunnelFin.Indexers;
using TunnelFin.BitTorrent;
using TunnelFin.Jellyfin;
using TunnelFin.Models;
using TunnelFin.Streaming;
using Xunit;

namespace TunnelFin.Tests.Integration;

/// <summary>
/// Integration tests for Jellyfin channel functionality.
/// These tests verify the end-to-end flow from channel registration to item browsing.
/// </summary>
public class JellyfinChannelTests
{
    private readonly Mock<ILogger<TunnelFinChannel>> _mockChannelLogger;
    private readonly Mock<ILogger<IndexerManager>> _mockIndexerLogger;
    private readonly Mock<ILogger<StreamManager>> _mockStreamLogger;
    private readonly Mock<ITorrentEngine> _mockTorrentEngine;

    public JellyfinChannelTests()
    {
        _mockChannelLogger = new Mock<ILogger<TunnelFinChannel>>();
        _mockIndexerLogger = new Mock<ILogger<IndexerManager>>();
        _mockStreamLogger = new Mock<ILogger<StreamManager>>();
        _mockTorrentEngine = new Mock<ITorrentEngine>();
    }

    [Fact]
    public async Task TunnelFinChannel_GetChannelItems_IntegrationTest()
    {
        // Arrange
        var httpClient = new HttpClient();
        var indexerManager = new IndexerManager(httpClient, _mockIndexerLogger.Object);
        
        var streamConfig = new TunnelFin.Configuration.StreamingConfig
        {
            MaxConcurrentStreams = 5,
            PrebufferSize = 10 * 1024 * 1024
        };
        var streamManager = new StreamManager(_mockTorrentEngine.Object, streamConfig, _mockStreamLogger.Object);

        var channel = new TunnelFinChannel(indexerManager, streamManager, _mockTorrentEngine.Object, _mockChannelLogger.Object);

        var query = new InternalChannelItemQuery
        {
            FolderId = "Big Buck Bunny"
        };

        // Act
        var result = await channel.GetChannelItems(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeNull();
        // Note: Results may be empty if no indexers are configured
    }

    [Fact]
    public void TunnelFinChannel_GetChannelFeatures_ReturnsExpectedFeatures()
    {
        // Arrange
        var httpClient = new HttpClient();
        var indexerManager = new IndexerManager(httpClient, _mockIndexerLogger.Object);
        
        var streamConfig = new TunnelFin.Configuration.StreamingConfig
        {
            MaxConcurrentStreams = 5,
            PrebufferSize = 10 * 1024 * 1024
        };
        var streamManager = new StreamManager(_mockTorrentEngine.Object, streamConfig, _mockStreamLogger.Object);

        var channel = new TunnelFinChannel(indexerManager, streamManager, _mockTorrentEngine.Object, _mockChannelLogger.Object);

        // Act
        var features = channel.GetChannelFeatures();

        // Assert
        features.Should().NotBeNull();
        features.MediaTypes.Should().Contain(ChannelMediaType.Video);
        features.SupportsContentDownloading.Should().BeFalse();
    }

    [Fact]
    public async Task TunnelFinChannel_GetChannelItemMediaInfo_ReturnsValidMediaSource()
    {
        // Arrange
        var httpClient = new HttpClient();
        var indexerManager = new IndexerManager(httpClient, _mockIndexerLogger.Object);
        
        var streamConfig = new TunnelFin.Configuration.StreamingConfig
        {
            MaxConcurrentStreams = 5,
            PrebufferSize = 10 * 1024 * 1024
        };
        var streamManager = new StreamManager(_mockTorrentEngine.Object, streamConfig, _mockStreamLogger.Object);

        var channel = new TunnelFinChannel(indexerManager, streamManager, _mockTorrentEngine.Object, _mockChannelLogger.Object);

        var infoHash = "dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c";
        var filePath = "video.mkv";

        // Mock the torrent engine to return a stream
        var mockStream = new MemoryStream(new byte[1024]);
        _mockTorrentEngine
            .Setup(e => e.CreateStreamAsync(infoHash, filePath, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockStream);

        // Mock GetBufferStatus
        _mockTorrentEngine
            .Setup(e => e.GetBufferStatus(infoHash, filePath))
            .Returns(new BufferStatus
            {
                BufferedRanges = new List<(long Start, long End)>(),
                PrebufferComplete = true,
                CurrentBufferedBytes = 1024,
                DownloadRate = 0,
                LastUpdated = DateTime.UtcNow
            });

        // Act
        var mediaSources = await channel.GetChannelItemMediaInfo(infoHash, filePath, CancellationToken.None);

        // Assert
        mediaSources.Should().NotBeNull();
        mediaSources.Should().HaveCount(1);
        var mediaSource = mediaSources.First();
        mediaSource.Protocol.Should().Be(MediaProtocol.Http);
        mediaSource.Id.Should().Be(infoHash);
    }

    [Fact]
    public void TunnelFinChannel_Name_ReturnsCorrectValue()
    {
        // Arrange
        var httpClient = new HttpClient();
        var indexerManager = new IndexerManager(httpClient, _mockIndexerLogger.Object);
        
        var streamConfig = new TunnelFin.Configuration.StreamingConfig
        {
            MaxConcurrentStreams = 5,
            PrebufferSize = 10 * 1024 * 1024
        };
        var streamManager = new StreamManager(_mockTorrentEngine.Object, streamConfig, _mockStreamLogger.Object);

        var channel = new TunnelFinChannel(indexerManager, streamManager, _mockTorrentEngine.Object, _mockChannelLogger.Object);

        // Act & Assert
        channel.Name.Should().Be("TunnelFin");
    }
}

