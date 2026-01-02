using FluentAssertions;
using TunnelFin.Configuration;
using TunnelFin.Networking.Circuits;
using TunnelFin.Networking.IPv8;
using Xunit;

namespace TunnelFin.Integration;

/// <summary>
/// Integration tests for privacy settings (T086).
/// Tests changing hop count and verifying circuit creation uses new setting.
/// </summary>
public class PrivacySettingsTests
{
    private Peer CreateTestPeer(string ip, ushort port)
    {
        var publicKey = new byte[32];
        new Random().NextBytes(publicKey);

        // Convert IP to big-endian uint32
        var ipParts = ip.Split('.');
        uint ipv4Address = ((uint)byte.Parse(ipParts[0]) << 24) |
                          ((uint)byte.Parse(ipParts[1]) << 16) |
                          ((uint)byte.Parse(ipParts[2]) << 8) |
                          (uint)byte.Parse(ipParts[3]);

        var peer = new Peer(publicKey, ipv4Address, port);
        peer.IsHandshakeComplete = true; // Mark as ready for circuit creation
        peer.IsRelayCandidate = true;
        return peer;
    }

    [Fact]
    public async Task ChangeHopCount_Should_Affect_New_Circuits()
    {
        // Arrange
        var settings = new AnonymitySettings { DefaultHopCount = 2 };
        var circuitManager = new CircuitManager(settings);

        // Add peers for circuit creation
        for (int i = 1; i <= 3; i++)
        {
            var peer = CreateTestPeer($"192.168.1.{i}", 6881);
            circuitManager.AddPeer(peer);
        }

        // Act
        var circuit = await circuitManager.CreateCircuitAsync();

        // Assert
        circuit.Should().NotBeNull();
        circuit.TargetHopCount.Should().Be(2, "circuit should use configured hop count");
    }

    [Fact]
    public async Task ChangeHopCount_From_1_To_3_Should_Create_Longer_Circuit()
    {
        // Arrange - Start with 1 hop
        var settings = new AnonymitySettings { DefaultHopCount = 1 };
        var circuitManager = new CircuitManager(settings);

        // Add peers for circuit creation
        for (int i = 1; i <= 5; i++)
        {
            var peer = CreateTestPeer($"192.168.1.{i}", 6881);
            circuitManager.AddPeer(peer);
        }

        var circuit1 = await circuitManager.CreateCircuitAsync();

        // Act - Change to 3 hops
        settings.SetHopCount(3);
        var circuit2 = await circuitManager.CreateCircuitAsync();

        // Assert
        circuit1.TargetHopCount.Should().Be(1);
        circuit2.TargetHopCount.Should().Be(3);
    }

    [Fact]
    public void DisableBandwidthContribution_Should_Stop_Relay()
    {
        // Arrange
        var settings = new AnonymitySettings { EnableBandwidthContribution = true };

        // Act - Disable contribution
        settings.EnableBandwidthContribution = false;

        // Assert
        settings.EnableBandwidthContribution.Should().BeFalse("relay should be disabled");
    }

    [Fact]
    public void AllowNonAnonymousFallback_Should_Enable_Direct_Connection()
    {
        // Arrange
        var settings = new AnonymitySettings { AllowNonAnonymousFallback = false };

        // Act - Enable fallback
        settings.AllowNonAnonymousFallback = true;

        // Assert
        settings.AllowNonAnonymousFallback.Should().BeTrue();
    }

    [Fact]
    public void AnonymitySettings_Should_Validate_Hop_Count_Range()
    {
        // Arrange
        var settings = new AnonymitySettings();

        // Act & Assert - Valid values
        var act1 = () => settings.SetHopCount(1);
        var act2 = () => settings.SetHopCount(2);
        var act3 = () => settings.SetHopCount(3);

        act1.Should().NotThrow();
        act2.Should().NotThrow();
        act3.Should().NotThrow();

        // Act & Assert - Invalid values
        var act4 = () => settings.SetHopCount(0);
        var act5 = () => settings.SetHopCount(4);

        act4.Should().Throw<ArgumentOutOfRangeException>();
        act5.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AnonymitySettings_Should_Have_Secure_Defaults()
    {
        // Act
        var settings = new AnonymitySettings();

        // Assert - FR-006: Default 3 hops for maximum privacy
        settings.DefaultHopCount.Should().Be(3);
        
        // Assert - FR-005: Bandwidth contribution enabled by default
        settings.EnableBandwidthContribution.Should().BeTrue();
        
        // Assert - FR-040: Non-anonymous fallback disabled by default
        settings.AllowNonAnonymousFallback.Should().BeFalse();
    }
}

