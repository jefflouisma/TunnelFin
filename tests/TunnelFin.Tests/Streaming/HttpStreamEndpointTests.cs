using FluentAssertions;
using TunnelFin.Streaming;
using Xunit;

namespace TunnelFin.Tests.Streaming;

/// <summary>
/// Unit tests for HttpStreamEndpoint (FR-009).
/// Tests HTTP range request handling per streaming-api.yaml contract.
/// </summary>
public class HttpStreamEndpointTests
{
    [Fact]
    public void ParseRangeHeader_Should_Extract_Start_And_End()
    {
        // Arrange
        var endpoint = new HttpStreamEndpoint(Guid.NewGuid(), contentLength: 1000);
        var rangeHeader = "bytes=0-499";

        // Act
        var result = endpoint.ParseRangeHeader(rangeHeader, contentLength: 1000);

        // Assert
        result.Should().NotBeNull("valid range should be parsed");
        result!.Value.start.Should().Be(0, "start should be 0");
        result!.Value.end.Should().Be(499, "end should be 499");
    }

    [Fact]
    public void ParseRangeHeader_Should_Handle_Open_Ended_Range()
    {
        // Arrange
        var endpoint = new HttpStreamEndpoint(Guid.NewGuid(), contentLength: 1000);
        var rangeHeader = "bytes=500-";

        // Act
        var result = endpoint.ParseRangeHeader(rangeHeader, contentLength: 1000);

        // Assert
        result.Should().NotBeNull("valid range should be parsed");
        result!.Value.start.Should().Be(500, "start should be 500");
        result!.Value.end.Should().Be(999, "end should be content length - 1");
    }

    [Fact]
    public void ParseRangeHeader_Should_Handle_Suffix_Range()
    {
        // Arrange
        var endpoint = new HttpStreamEndpoint(Guid.NewGuid(), contentLength: 1000);
        var rangeHeader = "bytes=-500";

        // Act
        var result = endpoint.ParseRangeHeader(rangeHeader, contentLength: 1000);

        // Assert
        result.Should().NotBeNull("valid range should be parsed");
        result!.Value.start.Should().Be(500, "start should be content length - 500");
        result!.Value.end.Should().Be(999, "end should be content length - 1");
    }

    [Fact]
    public void ParseRangeHeader_Should_Return_Null_For_Invalid_Format()
    {
        // Arrange
        var endpoint = new HttpStreamEndpoint(Guid.NewGuid(), contentLength: 1000);
        var rangeHeader = "invalid-range";

        // Act
        var result = endpoint.ParseRangeHeader(rangeHeader, contentLength: 1000);

        // Assert
        result.Should().BeNull("invalid format should return null");
    }

    [Fact]
    public void BuildContentRangeHeader_Should_Format_Correctly()
    {
        // Arrange
        var endpoint = new HttpStreamEndpoint(Guid.NewGuid(), contentLength: 1000);

        // Act
        var header = endpoint.BuildContentRangeHeader(start: 0, end: 499, totalLength: 1000);

        // Assert
        header.Should().Be("bytes 0-499/1000", "should match RFC 7233 format");
    }

    [Fact]
    public void GetStatusCode_Should_Return_206_For_Range_Request()
    {
        // Arrange
        var endpoint = new HttpStreamEndpoint(Guid.NewGuid(), contentLength: 1000);

        // Act
        var statusCode = endpoint.GetStatusCode(hasRangeHeader: true);

        // Assert
        statusCode.Should().Be(206, "range requests should return 206 Partial Content");
    }

    [Fact]
    public void GetStatusCode_Should_Return_200_For_Full_Request()
    {
        // Arrange
        var endpoint = new HttpStreamEndpoint(Guid.NewGuid(), contentLength: 1000);

        // Act
        var statusCode = endpoint.GetStatusCode(hasRangeHeader: false);

        // Assert
        statusCode.Should().Be(200, "full requests should return 200 OK");
    }

    [Fact]
    public void Constructor_Should_Set_Properties()
    {
        // Arrange
        var streamId = Guid.NewGuid();
        var contentLength = 1_000_000_000L;

        // Act
        var endpoint = new HttpStreamEndpoint(streamId, contentLength);

        // Assert
        endpoint.StreamId.Should().Be(streamId);
        endpoint.ContentLength.Should().Be(contentLength);
    }

    [Fact]
    public void ValidateRange_Should_Accept_Valid_Range()
    {
        // Arrange
        var endpoint = new HttpStreamEndpoint(Guid.NewGuid(), contentLength: 1000);

        // Act
        var isValid = endpoint.ValidateRange(start: 0, end: 499, contentLength: 1000);

        // Assert
        isValid.Should().BeTrue("valid range should be accepted");
    }

    [Fact]
    public void ValidateRange_Should_Reject_Start_Greater_Than_End()
    {
        // Arrange
        var endpoint = new HttpStreamEndpoint(Guid.NewGuid(), contentLength: 1000);

        // Act
        var isValid = endpoint.ValidateRange(start: 500, end: 100, contentLength: 1000);

        // Assert
        isValid.Should().BeFalse("start > end should be rejected");
    }

    [Fact]
    public void ValidateRange_Should_Reject_End_Beyond_Content_Length()
    {
        // Arrange
        var endpoint = new HttpStreamEndpoint(Guid.NewGuid(), contentLength: 1000);

        // Act
        var isValid = endpoint.ValidateRange(start: 0, end: 1500, contentLength: 1000);

        // Assert
        isValid.Should().BeFalse("end beyond content length should be rejected");
    }

    [Fact]
    public void ValidateRange_Should_Reject_Negative_Start()
    {
        // Arrange
        var endpoint = new HttpStreamEndpoint(Guid.NewGuid(), contentLength: 1000);

        // Act
        var isValid = endpoint.ValidateRange(start: -1, end: 499, contentLength: 1000);

        // Assert
        isValid.Should().BeFalse("negative start should be rejected");
    }
}

