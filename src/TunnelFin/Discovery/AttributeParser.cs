using System.Text.RegularExpressions;

namespace TunnelFin.Discovery;

/// <summary>
/// AttributeParser extracts attributes from torrent filenames (T078).
/// Supports resolution, quality, codecs, audio, HDR, language, release group per FR-020.
/// </summary>
public class AttributeParser
{
    private static readonly Regex ResolutionRegex = new(@"\b(2160p|1080p|720p|480p|4K|UHD)\b", RegexOptions.IgnoreCase);
    private static readonly Regex QualityRegex = new(@"\b(BluRay|WEBRip|WEB-DL|HDTV|DVDRip|BDRip|BRRip)\b", RegexOptions.IgnoreCase);
    private static readonly Regex CodecRegex = new(@"\b(x264|x265|H\.?264|H\.?265|HEVC|AVC)\b", RegexOptions.IgnoreCase);
    private static readonly Regex AudioRegex = new(@"\b(AAC|DTS|AC3|EAC3|TrueHD|FLAC|MP3|Atmos)\b", RegexOptions.IgnoreCase);
    private static readonly Regex HDRRegex = new(@"\b(HDR|HDR10|HDR10\+|Dolby Vision)\b", RegexOptions.IgnoreCase);
    private static readonly Regex LanguageRegex = new(@"\b(MULTI|FRENCH|ENGLISH|SPANISH|GERMAN|ITALIAN|JAPANESE|KOREAN)\b", RegexOptions.IgnoreCase);
    // Matches release groups in brackets at start (e.g., [SubsPlease]) or after dash at end (e.g., -RARBG)
    private static readonly Regex ReleaseGroupRegex = new(@"^\[([^\]]+)\]|-([A-Z0-9]+)(?:\[.*\])?$", RegexOptions.IgnoreCase);

    /// <summary>
    /// Parses resolution from title (e.g., "1080p", "720p", "2160p").
    /// </summary>
    public string? ParseResolution(string title)
    {
        var match = ResolutionRegex.Match(title);
        if (match.Success)
        {
            var resolution = match.Value.ToUpper();
            // Normalize 4K/UHD to 2160p
            if (resolution == "4K" || resolution == "UHD")
            {
                return "2160p";
            }
            return resolution.ToLower();
        }
        return null;
    }

    /// <summary>
    /// Parses quality from title (e.g., "BluRay", "WEBRip", "WEB-DL").
    /// </summary>
    public string? ParseQuality(string title)
    {
        var match = QualityRegex.Match(title);
        return match.Success ? match.Value : null;
    }

    /// <summary>
    /// Parses codec from title (e.g., "x264", "x265", "HEVC").
    /// </summary>
    public string? ParseCodec(string title)
    {
        var match = CodecRegex.Match(title);
        if (match.Success)
        {
            var codec = match.Value.ToUpper();
            // Normalize codec names
            if (codec.Contains("265") || codec == "HEVC")
            {
                return "x265";
            }
            if (codec.Contains("264") || codec == "AVC")
            {
                return "x264";
            }
            return codec;
        }
        return null;
    }

    /// <summary>
    /// Parses audio format from title (e.g., "AAC", "DTS", "AC3").
    /// </summary>
    public string? ParseAudio(string title)
    {
        var match = AudioRegex.Match(title);
        return match.Success ? match.Value.ToUpper() : null;
    }

    /// <summary>
    /// Detects HDR from title.
    /// </summary>
    public bool ParseHDR(string title)
    {
        return HDRRegex.IsMatch(title);
    }

    /// <summary>
    /// Parses language from title (e.g., "MULTI", "FRENCH", "ENGLISH").
    /// </summary>
    public string? ParseLanguage(string title)
    {
        var match = LanguageRegex.Match(title);
        return match.Success ? match.Value.ToUpper() : null;
    }

    /// <summary>
    /// Parses release group from title (e.g., "RARBG", "YTS", "ETRG", "SubsPlease").
    /// Supports both bracket format [Group] and dash format -Group.
    /// </summary>
    public string? ParseReleaseGroup(string title)
    {
        var match = ReleaseGroupRegex.Match(title);
        if (!match.Success)
            return null;

        // Group 1 is for bracket format [Group], Group 2 is for dash format -Group
        return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
    }

    /// <summary>
    /// Parses all attributes from title at once.
    /// </summary>
    public Dictionary<string, string> ParseAttributes(string title)
    {
        var attributes = new Dictionary<string, string>();

        var resolution = ParseResolution(title);
        if (resolution != null)
        {
            attributes["resolution"] = resolution;
        }

        var quality = ParseQuality(title);
        if (quality != null)
        {
            attributes["quality"] = quality;
        }

        var codec = ParseCodec(title);
        if (codec != null)
        {
            attributes["codec"] = codec;
        }

        var audio = ParseAudio(title);
        if (audio != null)
        {
            attributes["audio"] = audio;
        }

        var hdr = ParseHDR(title);
        attributes["hdr"] = hdr.ToString().ToLower();

        var language = ParseLanguage(title);
        if (language != null)
        {
            attributes["language"] = language;
        }

        var releaseGroup = ParseReleaseGroup(title);
        if (releaseGroup != null)
        {
            attributes["releaseGroup"] = releaseGroup;
        }

        return attributes;
    }
}

