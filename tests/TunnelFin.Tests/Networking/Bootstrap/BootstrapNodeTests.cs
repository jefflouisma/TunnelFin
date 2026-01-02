using FluentAssertions;
using TunnelFin.Networking.Bootstrap;

namespace TunnelFin.Tests.Networking.Bootstrap;

public class BootstrapNodeTests
{
    [Fact]
    public void BootstrapNode_Should_Validate_Correctly()
    {
        var validNode = new BootstrapNode
        {
            Address = "130.161.119.206",
            Port = 6421
        };

        validNode.IsValid().Should().BeTrue();
    }

    [Fact]
    public void BootstrapNode_Should_Reject_Invalid_IP()
    {
        var invalidNode = new BootstrapNode
        {
            Address = "invalid.ip.address",
            Port = 6421
        };

        invalidNode.IsValid().Should().BeFalse();
    }

    [Fact]
    public void BootstrapNode_Should_Reject_Empty_Address()
    {
        var invalidNode = new BootstrapNode
        {
            Address = "",
            Port = 6421
        };

        invalidNode.IsValid().Should().BeFalse();
    }

    [Fact]
    public void BootstrapNode_Should_Reject_Port_Below_Range()
    {
        var invalidNode = new BootstrapNode
        {
            Address = "130.161.119.206",
            Port = 6420 // Below 6421
        };

        invalidNode.IsValid().Should().BeFalse();
    }

    [Fact]
    public void BootstrapNode_Should_Reject_Port_Above_Range()
    {
        var invalidNode = new BootstrapNode
        {
            Address = "130.161.119.206",
            Port = 6529 // Above 6528
        };

        invalidNode.IsValid().Should().BeFalse();
    }

    [Fact]
    public void GetEndPoint_Should_Return_Valid_IPEndPoint()
    {
        var node = new BootstrapNode
        {
            Address = "130.161.119.206",
            Port = 6421
        };

        var endpoint = node.GetEndPoint();
        endpoint.Address.ToString().Should().Be("130.161.119.206");
        endpoint.Port.Should().Be(6421);
    }

    [Fact]
    public void GetEndPoint_Should_Throw_On_Invalid_Address()
    {
        var node = new BootstrapNode
        {
            Address = "invalid",
            Port = 6421
        };

        var act = () => node.GetEndPoint();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetDefaultNodes_Should_Return_14_Nodes()
    {
        var nodes = BootstrapNode.GetDefaultNodes();
        nodes.Should().HaveCount(14);
    }

    [Fact]
    public void GetDefaultNodes_Should_Return_Valid_Nodes()
    {
        var nodes = BootstrapNode.GetDefaultNodes();
        nodes.Should().AllSatisfy(node => node.IsValid().Should().BeTrue());
    }

    [Fact]
    public void GetDefaultNodes_Should_Use_TU_Delft_Addresses()
    {
        var nodes = BootstrapNode.GetDefaultNodes();
        var addresses = nodes.Select(n => n.Address).Distinct().ToList();

        addresses.Should().Contain("130.161.119.206");
        addresses.Should().Contain("130.161.119.215");
        addresses.Should().Contain("130.161.119.201");
    }

    [Fact]
    public void GetDefaultNodes_Should_Use_Port_Range_6421_6528()
    {
        var nodes = BootstrapNode.GetDefaultNodes();
        nodes.Should().AllSatisfy(node =>
        {
            node.Port.Should().BeGreaterThanOrEqualTo((ushort)6421);
            node.Port.Should().BeLessThanOrEqualTo((ushort)6528);
        });
    }

    [Fact]
    public void BootstrapNode_Should_Track_Contact_Attempts()
    {
        var node = new BootstrapNode
        {
            Address = "130.161.119.206",
            Port = 6421
        };

        node.LastContactAttempt.Should().BeNull();
        node.LastSuccessfulContact.Should().BeNull();
        node.IsReachable.Should().BeFalse();

        node.LastContactAttempt = DateTime.UtcNow;
        node.LastContactAttempt.Should().NotBeNull();
    }
}

