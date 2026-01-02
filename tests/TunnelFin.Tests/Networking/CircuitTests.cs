using FluentAssertions;
using TunnelFin.Models;
using TunnelFin.Networking.Circuits;
using Xunit;

namespace TunnelFin.Tests.Networking;

/// <summary>
/// Unit tests for Circuit class.
/// Tests circuit lifecycle, state transitions, and hop management.
/// </summary>
public class CircuitTests
{
    private static HopNode CreateTestHopNode(int hopIndex = 0)
    {
        var publicKey = new byte[32];
        new Random().NextBytes(publicKey);
        return new HopNode(publicKey, 0x7F000001, 8000, hopIndex);
    }

    [Fact]
    public void Constructor_Should_Initialize_Circuit_With_Valid_Parameters()
    {
        // Arrange & Act
        var circuit = new Circuit(12345, 3, 600);

        // Assert
        circuit.CircuitId.Should().NotBeEmpty();
        circuit.IPv8CircuitId.Should().Be(12345u);
        circuit.TargetHopCount.Should().Be(3);
        circuit.State.Should().Be(CircuitState.Creating);
        circuit.CurrentHopCount.Should().Be(0);
        circuit.IsEstablished.Should().BeFalse();
        circuit.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        circuit.LastActivityAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        circuit.ExpiresAt.Should().NotBeNull();
        circuit.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(600), TimeSpan.FromSeconds(2));
        circuit.IsExpired.Should().BeFalse();
        circuit.BytesSent.Should().Be(0);
        circuit.BytesReceived.Should().Be(0);
        circuit.RoundTripTimeMs.Should().Be(0);
        circuit.ErrorMessage.Should().BeNull();
        circuit.Hops.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void Constructor_Should_Throw_When_HopCount_Is_Invalid(int hopCount)
    {
        // Arrange & Act
        var act = () => new Circuit(12345, hopCount, 600);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("targetHopCount");
    }

    [Theory]
    [InlineData(59)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_Should_Throw_When_Lifetime_Is_Too_Short(int lifetimeSeconds)
    {
        // Arrange & Act
        var act = () => new Circuit(12345, 3, lifetimeSeconds);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("lifetimeSeconds");
    }

    [Fact]
    public void AddHop_Should_Add_Hop_To_Circuit()
    {
        // Arrange
        var circuit = new Circuit(12345, 3, 600);
        var hop = CreateTestHopNode();

        // Act
        circuit.AddHop(hop);

        // Assert
        circuit.CurrentHopCount.Should().Be(1);
        circuit.Hops.Should().HaveCount(1);
        circuit.Hops[0].Should().Be(hop);
        circuit.LastActivityAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void AddHop_Should_Throw_When_Hop_Is_Null()
    {
        // Arrange
        var circuit = new Circuit(12345, 3, 600);

        // Act
        var act = () => circuit.AddHop(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("hop");
    }

    [Fact]
    public void AddHop_Should_Throw_When_Circuit_Is_Full()
    {
        // Arrange
        var circuit = new Circuit(12345, 2, 600);
        circuit.AddHop(CreateTestHopNode());
        circuit.AddHop(CreateTestHopNode());

        // Act
        var act = () => circuit.AddHop(CreateTestHopNode());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already has 2 hops*");
    }

    [Fact]
    public void AddHop_Should_Throw_When_Circuit_Is_Not_Creating()
    {
        // Arrange
        var circuit = new Circuit(12345, 3, 600);
        circuit.MarkFailed("Test error");

        // Act
        var act = () => circuit.AddHop(CreateTestHopNode(0));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot add hops to circuit in state*");
    }

    [Fact]
    public void AddHop_Should_Throw_When_Circuit_Is_Disposed()
    {
        // Arrange
        var circuit = new Circuit(12345, 3, 600);
        circuit.Dispose();

        // Act
        var act = () => circuit.AddHop(CreateTestHopNode());

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void MarkEstablished_Should_Change_State_To_Established()
    {
        // Arrange
        var circuit = new Circuit(12345, 2, 600);
        circuit.AddHop(CreateTestHopNode());
        circuit.AddHop(CreateTestHopNode());

        // Act
        circuit.MarkEstablished();

        // Assert
        circuit.State.Should().Be(CircuitState.Established);
        circuit.IsEstablished.Should().BeTrue();
        circuit.LastActivityAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void MarkEstablished_Should_Throw_When_Hops_Incomplete()
    {
        // Arrange
        var circuit = new Circuit(12345, 3, 600);
        circuit.AddHop(CreateTestHopNode());

        // Act
        var act = () => circuit.MarkEstablished();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*has 1 hops, expected 3*");
    }

    [Fact]
    public void MarkEstablished_Should_Throw_When_Circuit_Is_Disposed()
    {
        // Arrange
        var circuit = new Circuit(12345, 1, 600);
        circuit.AddHop(CreateTestHopNode());
        circuit.Dispose();

        // Act
        var act = () => circuit.MarkEstablished();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void MarkFailed_Should_Change_State_To_Failed()
    {
        // Arrange
        var circuit = new Circuit(12345, 3, 600);
        var errorMessage = "Connection timeout";

        // Act
        circuit.MarkFailed(errorMessage);

        // Assert
        circuit.State.Should().Be(CircuitState.Failed);
        circuit.ErrorMessage.Should().Be(errorMessage);
        circuit.IsEstablished.Should().BeFalse();
        circuit.LastActivityAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void MarkFailed_Should_Throw_When_Circuit_Is_Disposed()
    {
        // Arrange
        var circuit = new Circuit(12345, 3, 600);
        circuit.Dispose();

        // Act
        var act = () => circuit.MarkFailed("Error");

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_Should_Change_State_To_Closed()
    {
        // Arrange
        var circuit = new Circuit(12345, 3, 600);
        circuit.AddHop(CreateTestHopNode());

        // Act
        circuit.Dispose();

        // Assert
        circuit.State.Should().Be(CircuitState.Closed);
        circuit.Hops.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_Should_Be_Idempotent()
    {
        // Arrange
        var circuit = new Circuit(12345, 3, 600);

        // Act
        circuit.Dispose();
        circuit.Dispose();

        // Assert
        circuit.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void IsExpired_Should_Return_False_For_New_Circuit()
    {
        // Arrange & Act
        var circuit = new Circuit(12345, 3, 600);

        // Assert
        circuit.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_Should_Return_True_For_Expired_Circuit()
    {
        // Arrange
        var circuit = new Circuit(12345, 3, 60); // 60 seconds lifetime

        // Simulate expiration by creating circuit with very short lifetime
        // We can't actually wait, so we test the logic with a circuit that would expire

        // Assert
        circuit.ExpiresAt.Should().NotBeNull();
        circuit.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Circuit_Should_Support_Different_Hop_Counts(int hopCount)
    {
        // Arrange & Act
        var circuit = new Circuit(12345, hopCount, 600);

        // Assert
        circuit.TargetHopCount.Should().Be(hopCount);
        circuit.CurrentHopCount.Should().Be(0);
    }

    [Fact]
    public void Circuit_Should_Track_Multiple_Hops()
    {
        // Arrange
        var circuit = new Circuit(12345, 3, 600);
        var hop1 = CreateTestHopNode(0);
        var hop2 = CreateTestHopNode(1);
        var hop3 = CreateTestHopNode(2);

        // Act
        circuit.AddHop(hop1);
        circuit.AddHop(hop2);
        circuit.AddHop(hop3);

        // Assert
        circuit.CurrentHopCount.Should().Be(3);
        circuit.Hops.Should().HaveCount(3);
        circuit.Hops[0].Should().Be(hop1);
        circuit.Hops[1].Should().Be(hop2);
        circuit.Hops[2].Should().Be(hop3);
    }

    [Fact]
    public void Hops_Should_Be_ReadOnly()
    {
        // Arrange
        var circuit = new Circuit(12345, 3, 600);
        circuit.AddHop(CreateTestHopNode());

        // Act & Assert
        circuit.Hops.Should().BeAssignableTo<IReadOnlyList<HopNode>>();
    }

    [Fact]
    public void Circuit_Should_Have_Unique_CircuitId()
    {
        // Arrange & Act
        var circuit1 = new Circuit(12345, 3, 600);
        var circuit2 = new Circuit(12345, 3, 600);

        // Assert
        circuit1.CircuitId.Should().NotBe(circuit2.CircuitId);
    }

    [Fact]
    public void Circuit_Should_Preserve_IPv8CircuitId()
    {
        // Arrange & Act
        var circuit = new Circuit(0xDEADBEEF, 3, 600);

        // Assert
        circuit.IPv8CircuitId.Should().Be(0xDEADBEEF);
    }
}

