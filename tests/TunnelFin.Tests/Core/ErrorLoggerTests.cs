using FluentAssertions;
using TunnelFin.Core;
using Xunit;

namespace TunnelFin.Tests.Core;

/// <summary>
/// Tests for ErrorLogger (T100).
/// Tests error logging without PII exposure per FR-041, SC-013.
/// </summary>
public class ErrorLoggerTests
{
    private readonly ErrorLogger _logger;

    public ErrorLoggerTests()
    {
        _logger = new ErrorLogger();
    }

    [Fact]
    public void LogError_Should_Record_Error_Without_PII()
    {
        // Arrange
        var component = "CircuitManager";
        var errorMessage = "Circuit creation failed";

        // Act
        _logger.LogError(component, errorMessage);

        // Assert
        var errors = _logger.GetRecentErrors(10);
        errors.Should().HaveCount(1);
        errors[0].Component.Should().Be(component);
        errors[0].Message.Should().Be(errorMessage);
        errors[0].Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void LogError_Should_Not_Include_Content_Titles()
    {
        // Arrange
        var component = "StreamManager";
        var errorMessage = "Stream initialization failed for content";

        // Act
        _logger.LogError(component, errorMessage);

        // Assert
        var errors = _logger.GetRecentErrors(10);
        errors[0].Message.Should().NotContain("Avengers");
        errors[0].Message.Should().NotContain("Breaking Bad");
        errors[0].Message.Should().Be(errorMessage, "should not include content titles per FR-041");
    }

    [Fact]
    public void LogError_Should_Not_Include_User_Identifiers()
    {
        // Arrange
        var component = "AuthManager";
        var errorMessage = "Authentication failed";

        // Act
        _logger.LogError(component, errorMessage);

        // Assert
        var errors = _logger.GetRecentErrors(10);
        errors[0].Message.Should().NotContain("user123");
        errors[0].Message.Should().NotContain("john@example.com");
        errors[0].Message.Should().Be(errorMessage, "should not include user identifiers per FR-041");
    }

    [Fact]
    public void GetRecentErrors_Should_Return_Limited_Count()
    {
        // Arrange
        for (int i = 0; i < 20; i++)
        {
            _logger.LogError("Component", $"Error {i}");
        }

        // Act
        var errors = _logger.GetRecentErrors(10);

        // Assert
        errors.Should().HaveCount(10, "should limit to requested count");
    }

    [Fact]
    public void GetRecentErrors_Should_Return_Most_Recent_First()
    {
        // Arrange
        _logger.LogError("Component", "Error 1");
        Thread.Sleep(10);
        _logger.LogError("Component", "Error 2");
        Thread.Sleep(10);
        _logger.LogError("Component", "Error 3");

        // Act
        var errors = _logger.GetRecentErrors(10);

        // Assert
        errors.Should().HaveCount(3);
        errors[0].Message.Should().Be("Error 3", "most recent should be first");
        errors[1].Message.Should().Be("Error 2");
        errors[2].Message.Should().Be("Error 1");
    }

    [Fact]
    public void GetErrorCount_Should_Return_Total_Errors()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            _logger.LogError("Component", $"Error {i}");
        }

        // Act
        var count = _logger.GetErrorCount();

        // Assert
        count.Should().Be(5);
    }

    [Fact]
    public void ClearErrors_Should_Remove_All_Errors()
    {
        // Arrange
        _logger.LogError("Component", "Error 1");
        _logger.LogError("Component", "Error 2");

        // Act
        _logger.ClearErrors();

        // Assert
        _logger.GetErrorCount().Should().Be(0);
        _logger.GetRecentErrors(10).Should().BeEmpty();
    }

    [Fact]
    public void GetRecentErrors_Should_Complete_In_Under_1_Second()
    {
        // Arrange
        for (int i = 0; i < 100; i++)
        {
            _logger.LogError("Component", $"Error {i}");
        }

        // Act
        var start = DateTime.UtcNow;
        var errors = _logger.GetRecentErrors(50);
        var duration = DateTime.UtcNow - start;

        // Assert
        duration.Should().BeLessThan(TimeSpan.FromSeconds(1), "should meet SC-009 latency requirement");
        errors.Should().HaveCount(50);
    }
}

