using FluentAssertions;
using TunnelFin.Discovery;
using Xunit;

namespace TunnelFin.Tests.Discovery;

/// <summary>
/// Tests for AttributeParser (T075).
/// Tests resolution, quality, codecs extraction from filenames per FR-020.
/// </summary>
public class AttributeParserTests
{
    private readonly AttributeParser _parser;

    public AttributeParserTests()
    {
        _parser = new AttributeParser();
    }

    [Theory]
    [InlineData("Movie.1080p.BluRay.x264", "1080p")]
    [InlineData("Movie.720p.WEBRip.x265", "720p")]
    [InlineData("Movie.2160p.UHD.BluRay", "2160p")]
    [InlineData("Movie.4K.HDR.BluRay", "2160p")]
    [InlineData("Movie.480p.DVDRip", "480p")]
    public void ParseResolution_Should_Extract_Resolution_From_Title(string title, string expectedResolution)
    {
        // Act
        var resolution = _parser.ParseResolution(title);

        // Assert
        resolution.Should().Be(expectedResolution);
    }

    [Theory]
    [InlineData("Movie.1080p.BluRay.x264", "BluRay")]
    [InlineData("Movie.720p.WEBRip.x265", "WEBRip")]
    [InlineData("Movie.1080p.WEB-DL.AAC", "WEB-DL")]
    [InlineData("Movie.720p.HDTV.x264", "HDTV")]
    [InlineData("Movie.1080p.DVDRip.x264", "DVDRip")]
    public void ParseQuality_Should_Extract_Quality_From_Title(string title, string expectedQuality)
    {
        // Act
        var quality = _parser.ParseQuality(title);

        // Assert
        quality.Should().Be(expectedQuality);
    }

    [Theory]
    [InlineData("Movie.1080p.BluRay.x264", "x264")]
    [InlineData("Movie.720p.WEBRip.x265", "x265")]
    [InlineData("Movie.1080p.BluRay.HEVC", "x265")] // HEVC is normalized to x265
    [InlineData("Movie.720p.WEBRip.H264", "x264")] // H264 is normalized to x264
    [InlineData("Movie.1080p.BluRay.H265", "x265")] // H265 is normalized to x265
    public void ParseCodec_Should_Extract_Codec_From_Title(string title, string expectedCodec)
    {
        // Act
        var codec = _parser.ParseCodec(title);

        // Assert
        codec.Should().Be(expectedCodec);
    }

    [Theory]
    [InlineData("Movie.1080p.BluRay.AAC.x264", "AAC")]
    [InlineData("Movie.720p.WEBRip.DTS.x265", "DTS")]
    [InlineData("Movie.1080p.BluRay.AC3.x264", "AC3")]
    [InlineData("Movie.720p.WEBRip.EAC3.x265", "EAC3")]
    [InlineData("Movie.1080p.BluRay.TrueHD.x264", "TRUEHD")] // Audio is normalized to uppercase
    public void ParseAudio_Should_Extract_Audio_Format_From_Title(string title, string expectedAudio)
    {
        // Act
        var audio = _parser.ParseAudio(title);

        // Assert
        audio.Should().Be(expectedAudio);
    }

    [Theory]
    [InlineData("Movie.1080p.BluRay.HDR.x265", true)]
    [InlineData("Movie.720p.WEBRip.SDR.x264", false)]
    [InlineData("Movie.2160p.UHD.HDR10.x265", true)]
    [InlineData("Movie.1080p.BluRay.x264", false)]
    public void ParseHDR_Should_Detect_HDR_From_Title(string title, bool expectedHDR)
    {
        // Act
        var hasHDR = _parser.ParseHDR(title);

        // Assert
        hasHDR.Should().Be(expectedHDR);
    }

    [Theory]
    [InlineData("Movie.1080p.BluRay.MULTI.x264", "MULTI")]
    [InlineData("Movie.720p.WEBRip.FRENCH.x265", "FRENCH")]
    [InlineData("Movie.1080p.BluRay.ENGLISH.x264", "ENGLISH")]
    [InlineData("Movie.720p.WEBRip.SPANISH.x265", "SPANISH")]
    public void ParseLanguage_Should_Extract_Language_From_Title(string title, string expectedLanguage)
    {
        // Act
        var language = _parser.ParseLanguage(title);

        // Assert
        language.Should().Be(expectedLanguage);
    }

    [Theory]
    [InlineData("Movie.1080p.BluRay.x264-GROUP1", "GROUP1")]
    [InlineData("Movie.720p.WEBRip.x265-RARBG", "RARBG")]
    [InlineData("Movie.1080p.BluRay.x264-YTS", "YTS")]
    [InlineData("Movie.720p.WEBRip.x265-ETRG", "ETRG")]
    public void ParseReleaseGroup_Should_Extract_Release_Group_From_Title(string title, string expectedGroup)
    {
        // Act
        var group = _parser.ParseReleaseGroup(title);

        // Assert
        group.Should().Be(expectedGroup);
    }

    [Fact]
    public void ParseAttributes_Should_Extract_All_Attributes_At_Once()
    {
        // Arrange
        var title = "Movie.2023.1080p.BluRay.x265.AAC.HDR.MULTI-GROUP1";

        // Act
        var attributes = _parser.ParseAttributes(title);

        // Assert
        attributes.Should().ContainKey("resolution");
        attributes["resolution"].Should().Be("1080p");
        attributes.Should().ContainKey("quality");
        attributes["quality"].Should().Be("BluRay");
        attributes.Should().ContainKey("codec");
        attributes["codec"].Should().Be("x265");
        attributes.Should().ContainKey("audio");
        attributes["audio"].Should().Be("AAC");
        attributes.Should().ContainKey("hdr");
        attributes["hdr"].Should().Be("true");
        attributes.Should().ContainKey("language");
        attributes["language"].Should().Be("MULTI");
        attributes.Should().ContainKey("releaseGroup");
        attributes["releaseGroup"].Should().Be("GROUP1");
    }
}

