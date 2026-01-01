using Xunit;
using FluentAssertions;

namespace TunnelFin.Tests.Core;

/// <summary>
/// Sample test to verify test infrastructure is working
/// </summary>
public class SampleTest
{
    [Fact]
    public void SampleTest_ShouldPass()
    {
        // Arrange
        var expected = 42;

        // Act
        var actual = 42;

        // Assert
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(5, 5, 10)]
    [InlineData(-1, 1, 0)]
    public void Add_ShouldReturnCorrectSum(int a, int b, int expected)
    {
        // Act
        var result = a + b;

        // Assert
        result.Should().Be(expected);
    }
}

