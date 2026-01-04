using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;
using Moq;
using TunnelFin.BitTorrent;
using TunnelFin.Indexers;
using TunnelFin.Streaming;
using TunnelFin.Jellyfin;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.Unit.Jellyfin;

public class TunnelFinChannelTests
{
    private readonly Mock<IIndexerManager> _mockIndexerManager;
    private readonly Mock<IStreamManager> _mockStreamManager;
    private readonly Mock<ITorrentEngine> _mockTorrentEngine;
    private readonly Mock<ILogger<TunnelFinChannel>> _mockLogger;
    private readonly TunnelFinChannel _channel;

    public TunnelFinChannelTests()
    {
        _mockIndexerManager = new Mock<IIndexerManager>();
        _mockStreamManager = new Mock<IStreamManager>();
        _mockTorrentEngine = new Mock<ITorrentEngine>();
        _mockLogger = new Mock<ILogger<TunnelFinChannel>>();
        _channel = new TunnelFinChannel(_mockIndexerManager.Object, _mockStreamManager.Object, _mockTorrentEngine.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetChannelItems_WithSearchTerm_ReturnsConvertedResults()
    {
        // Arrange
        var torrentResults = new List<TorrentResult>
        {
            new TorrentResult
            {
                InfoHash = "dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c",
                Title = "Big Buck Bunny 1080p",
                Size = 734003200,
                Seeders = 42,
                Leechers = 15,
                Category = "Movies",
                MagnetLink = "magnet:?xt=urn:btih:dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c",
                IndexerName = "TestIndexer"
            }
        };

        _mockIndexerManager
            .Setup(m => m.SearchAsync("Big Buck Bunny", It.IsAny<CancellationToken>()))
            .ReturnsAsync(torrentResults);

        var query = new InternalChannelItemQuery
        {
            FolderId = "Big Buck Bunny"
        };

        // Act
        var result = await _channel.GetChannelItems(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().Name.Should().Be("Big Buck Bunny 1080p");
        result.Items.First().Id.Should().Be("magnet:?xt=urn:btih:dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c");
        result.Items.First().Type.Should().Be(ChannelItemType.Media);
    }

    [Fact]
    public async Task GetChannelItemMediaInfo_ValidInfoHash_ReturnsMediaSource()
    {
        // Arrange
        var infoHash = "dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c";
        var sessionId = Guid.NewGuid();
        var streamUrl = "http://localhost:8096/stream/" + sessionId;

        var session = new StreamSession
        {
            SessionId = sessionId,
            InfoHash = infoHash,
            FilePath = "",
            StreamUrl = streamUrl,
            BufferStatus = new BufferStatus { BufferedRanges = new(), PrebufferComplete = true, CurrentBufferedBytes = 0, DownloadRate = 0 }
        };

        _mockStreamManager
            .Setup(m => m.CreateSessionAsync(infoHash, "", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _mockStreamManager
            .Setup(m => m.GetStreamUrl(sessionId))
            .Returns(streamUrl);

        // Act
        var result = await _channel.GetChannelItemMediaInfo(infoHash, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        var mediaSource = result.First();
        mediaSource.Protocol.Should().Be(MediaProtocol.Http);
        mediaSource.Path.Should().Be(streamUrl);
        mediaSource.Id.Should().Be(infoHash);
    }

    [Fact]
    public void GetChannelFeatures_ReturnsCorrectFeatures()
    {
        // Act
        var features = _channel.GetChannelFeatures();

        // Assert
        features.Should().NotBeNull();
        features.SupportsContentDownloading.Should().BeFalse();
        features.MediaTypes.Should().Contain(ChannelMediaType.Video);
    }

    [Fact]
    public void ToChannelItem_ValidTorrentResult_ConvertsCorrectly()
    {
        // Arrange
        var torrentResult = new TorrentResult
        {
            InfoHash = "dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c",
            Title = "Big Buck Bunny 1080p",
            Size = 734003200,
            Seeders = 42,
            Leechers = 15,
            Category = "Movies",
            MagnetLink = "magnet:?xt=urn:btih:dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c",
            IndexerName = "TestIndexer"
        };

        // Act
        var channelItem = _channel.ToChannelItem(torrentResult);

        // Assert
        channelItem.Should().NotBeNull();
        channelItem.Name.Should().Be("Big Buck Bunny 1080p");
        channelItem.Id.Should().Be("magnet:?xt=urn:btih:dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c");
        channelItem.Type.Should().Be(ChannelItemType.Media);
        channelItem.MediaType.Should().Be(ChannelMediaType.Video);
        channelItem.ContentType.Should().Be(ChannelMediaContentType.Movie);
    }

    [Fact]
    public async Task GetChannelItems_EmptyFolderId_ReturnsSearchCategories()
    {
        // Arrange
        var query = new InternalChannelItemQuery
        {
            FolderId = ""
        };

        // Act
        var result = await _channel.GetChannelItems(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().NotBeEmpty();
        result.Items.Should().AllSatisfy(item =>
        {
            item.Type.Should().Be(ChannelItemType.Folder);
            item.FolderType.Should().Be(ChannelFolderType.Container);
        });
        // Should contain at least the test content categories
        result.Items.Should().Contain(item => item.Name.Contains("Big Buck Bunny"));
    }

    [Fact]
    public async Task GetChannelItems_CategoryFolderId_SearchesWithCategoryTerm()
    {
        // Arrange
        var torrentResults = new List<TorrentResult>
        {
            new TorrentResult
            {
                InfoHash = "dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c",
                Title = "Big Buck Bunny 1080p",
                Size = 734003200,
                Seeders = 42,
                Leechers = 15,
                Category = "Movies",
                MagnetLink = "magnet:?xt=urn:btih:dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c",
                IndexerName = "TestIndexer"
            }
        };

        _mockIndexerManager
            .Setup(m => m.SearchAsync("big buck bunny", It.IsAny<CancellationToken>()))
            .ReturnsAsync(torrentResults);

        var query = new InternalChannelItemQuery
        {
            FolderId = "cat_big_buck_bunny"
        };

        // Act
        var result = await _channel.GetChannelItems(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().Name.Should().Be("Big Buck Bunny 1080p");
    }

    [Fact]
    public async Task GetLatestMedia_ReturnsPopularContent()
    {
        // Arrange
        var torrentResults = new List<TorrentResult>
        {
            new TorrentResult
            {
                InfoHash = "dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c",
                Title = "Big Buck Bunny 1080p",
                Size = 734003200,
                Seeders = 42,
                Leechers = 15,
                Category = "Movies",
                MagnetLink = "magnet:?xt=urn:btih:dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c",
                IndexerName = "TestIndexer"
            }
        };

        _mockIndexerManager
            .Setup(m => m.SearchAsync("big buck bunny", It.IsAny<CancellationToken>()))
            .ReturnsAsync(torrentResults);

        var request = new ChannelLatestMediaSearch { UserId = "test-user" };

        // Act
        var result = await _channel.GetLatestMedia(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Big Buck Bunny 1080p");
    }
}

