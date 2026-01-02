using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TunnelFin.Core;
using Xunit;

namespace TunnelFin.Tests.Core;

public class LoggingTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly PrivacyAwareLogger _privacyLogger;

    public LoggingTests()
    {
        _mockLogger = new Mock<ILogger>();
        _privacyLogger = new PrivacyAwareLogger(_mockLogger.Object, enableVerboseLogging: true);
    }

    [Fact]
    public void PrivacyAwareLogger_Should_Redact_IPv4_Addresses()
    {
        // Arrange
        var messageWithIp = "Connection from 192.168.1.100 established";

        // Act
        _privacyLogger.LogInformation(messageWithIp);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[IP_REDACTED]") && !v.ToString()!.Contains("192.168.1.100")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void PrivacyAwareLogger_Should_Redact_IPv6_Addresses()
    {
        // Arrange
        var messageWithIpv6 = "Connection from 2001:0db8:85a3:0000:0000:8a2e:0370:7334 established";

        // Act
        _privacyLogger.LogInformation(messageWithIpv6);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[IP_REDACTED]") && !v.ToString()!.Contains("2001:0db8")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void PrivacyAwareLogger_Should_Redact_InfoHash_But_Keep_Prefix()
    {
        // Arrange
        var infoHash = "1234567890abcdef1234567890abcdef12345678";
        var messageWithHash = $"Downloading torrent {infoHash}";

        // Act
        _privacyLogger.LogInformation(messageWithHash);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("12345678...") && 
                    v.ToString()!.Contains("[HASH_REDACTED]") &&
                    !v.ToString()!.Contains(infoHash)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void PrivacyAwareLogger_Should_Redact_Magnet_URIs()
    {
        // Arrange
        var magnetUri = "magnet:?xt=urn:btih:1234567890abcdef1234567890abcdef12345678&dn=example";
        var messageWithMagnet = $"Starting stream from {magnetUri}";

        // Act
        _privacyLogger.LogInformation(messageWithMagnet);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains("magnet:?[URI_REDACTED]") &&
                    !v.ToString()!.Contains("xt=urn:btih")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void PrivacyAwareLogger_Should_Not_Log_Debug_When_Verbose_Disabled()
    {
        // Arrange
        var nonVerboseLogger = new PrivacyAwareLogger(_mockLogger.Object, enableVerboseLogging: false);

        // Act
        nonVerboseLogger.LogDebug("Debug message");

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public void PrivacyAwareLogger_Should_Log_Debug_When_Verbose_Enabled()
    {
        // Arrange & Act
        _privacyLogger.LogDebug("Debug message");

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void PrivacyAwareLogger_Should_Log_Circuit_Created_Without_PII()
    {
        // Arrange
        var circuitId = Guid.NewGuid();
        var hopCount = 3;
        var rttMs = 250.5;

        // Act
        _privacyLogger.LogCircuitCreated(circuitId, hopCount, rttMs);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains(circuitId.ToString()) &&
                    v.ToString()!.Contains("3") &&
                    v.ToString()!.Contains("250.5")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void PrivacyAwareLogger_Should_Log_Stream_Initialized_Without_Content_Title()
    {
        // Arrange
        var streamId = Guid.NewGuid();
        var sizeBytes = 1073741824L; // 1GB
        var isAnonymous = true;

        // Act
        _privacyLogger.LogStreamInitialized(streamId, sizeBytes, isAnonymous);

        // Assert - Verify no content title is logged, only stream metadata
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString()!.Contains(streamId.ToString()) &&
                    v.ToString()!.Contains(sizeBytes.ToString()) &&
                    v.ToString()!.Contains("True")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void PrivacyAwareLogger_Should_Log_Errors_With_Exception()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        var message = "An error occurred";

        // Act
        _privacyLogger.LogError(message, exception);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_Should_Throw_When_Logger_Is_Null()
    {
        // Arrange & Act
        var act = () => new PrivacyAwareLogger(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void LogWarning_Should_Redact_Sensitive_Info()
    {
        // Arrange
        var messageWithIp = "Warning: Connection from 10.0.0.1 failed";

        // Act
        _privacyLogger.LogWarning(messageWithIp);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[IP_REDACTED]")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogCritical_Should_Redact_Sensitive_Info()
    {
        // Arrange
        var messageWithHash = "Critical: Failed to download 1234567890abcdef1234567890abcdef12345678";

        // Act
        _privacyLogger.LogCritical(messageWithHash);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[HASH_REDACTED]")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogCritical_Should_Log_With_Exception()
    {
        // Arrange
        var exception = new Exception("Critical error");
        var message = "Critical failure occurred";

        // Act
        _privacyLogger.LogCritical(message, exception);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogCircuitFailed_Should_Log_Without_PII()
    {
        // Arrange
        var circuitId = Guid.NewGuid();
        var hopCount = 2;
        var reason = "Timeout connecting to 192.168.1.1";

        // Act
        _privacyLogger.LogCircuitFailed(circuitId, hopCount, reason);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains(circuitId.ToString()) &&
                    v.ToString()!.Contains("2") &&
                    v.ToString()!.Contains("[IP_REDACTED]") &&
                    !v.ToString()!.Contains("192.168.1.1")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogStreamProgress_Should_Log_When_Verbose_Enabled()
    {
        // Arrange
        var streamId = Guid.NewGuid();
        var progressPercent = 45.5;
        var speedBps = 1024000L;

        // Act
        _privacyLogger.LogStreamProgress(streamId, progressPercent, speedBps);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains(streamId.ToString()) &&
                    v.ToString()!.Contains("45.5") &&
                    v.ToString()!.Contains("1024000")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void LogStreamFailed_Should_Redact_Sensitive_Info()
    {
        // Arrange
        var streamId = Guid.NewGuid();
        var reason = "Failed to connect to peer at 172.16.0.1";

        // Act
        _privacyLogger.LogStreamFailed(streamId, reason);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains(streamId.ToString()) &&
                    v.ToString()!.Contains("[IP_REDACTED]") &&
                    !v.ToString()!.Contains("172.16.0.1")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void RedactSensitiveInfo_Should_Handle_Empty_String()
    {
        // Arrange & Act
        _privacyLogger.LogInformation("");

        // Assert - Should not throw
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void RedactSensitiveInfo_Should_Handle_Multiple_IPs()
    {
        // Arrange
        var message = "Connecting from 192.168.1.1 to 10.0.0.1";

        // Act
        _privacyLogger.LogInformation(message);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    !v.ToString()!.Contains("192.168.1.1") &&
                    !v.ToString()!.Contains("10.0.0.1")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

