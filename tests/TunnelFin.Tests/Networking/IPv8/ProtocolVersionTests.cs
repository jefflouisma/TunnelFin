using FluentAssertions;
using TunnelFin.Networking.IPv8;

namespace TunnelFin.Tests.Networking.IPv8;

public class ProtocolVersionTests
{
    [Fact]
    public void ProtocolVersion_Should_Initialize_With_Valid_Values()
    {
        var version = new ProtocolVersion(3, 1, 0);

        version.Major.Should().Be(3);
        version.Minor.Should().Be(1);
        version.Patch.Should().Be(0);
    }

    [Fact]
    public void Constructor_Should_Throw_On_Negative_Major()
    {
        var act = () => new ProtocolVersion(-1, 0, 0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Should_Throw_On_Negative_Minor()
    {
        var act = () => new ProtocolVersion(3, -1, 0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Should_Throw_On_Negative_Patch()
    {
        var act = () => new ProtocolVersion(3, 1, -1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Current_Should_Be_Version_3_1_0()
    {
        ProtocolVersion.Current.Major.Should().Be(3);
        ProtocolVersion.Current.Minor.Should().Be(1);
        ProtocolVersion.Current.Patch.Should().Be(0);
    }

    [Fact]
    public void MinimumCompatible_Should_Be_Version_3_0_0()
    {
        ProtocolVersion.MinimumCompatible.Major.Should().Be(3);
        ProtocolVersion.MinimumCompatible.Minor.Should().Be(0);
        ProtocolVersion.MinimumCompatible.Patch.Should().Be(0);
    }

    [Fact]
    public void IsCompatibleWith_Should_Accept_Same_Version()
    {
        var version = new ProtocolVersion(3, 1, 0);
        var other = new ProtocolVersion(3, 1, 0);

        version.IsCompatibleWith(other).Should().BeTrue();
    }

    [Fact]
    public void IsCompatibleWith_Should_Accept_Lower_Minor_Version()
    {
        var version = new ProtocolVersion(3, 1, 0);
        var other = new ProtocolVersion(3, 0, 0);

        version.IsCompatibleWith(other).Should().BeTrue();
    }

    [Fact]
    public void IsCompatibleWith_Should_Reject_Higher_Minor_Version()
    {
        var version = new ProtocolVersion(3, 0, 0);
        var other = new ProtocolVersion(3, 1, 0);

        version.IsCompatibleWith(other).Should().BeFalse();
    }

    [Fact]
    public void IsCompatibleWith_Should_Reject_Different_Major_Version()
    {
        var version = new ProtocolVersion(3, 1, 0);
        var other = new ProtocolVersion(2, 1, 0);

        version.IsCompatibleWith(other).Should().BeFalse();
    }

    [Fact]
    public void IsCompatibleWith_Should_Accept_Lower_Patch_Version()
    {
        var version = new ProtocolVersion(3, 1, 5);
        var other = new ProtocolVersion(3, 1, 0);

        version.IsCompatibleWith(other).Should().BeTrue();
    }

    [Fact]
    public void IsCompatibleWith_Should_Reject_Higher_Patch_Version()
    {
        var version = new ProtocolVersion(3, 1, 0);
        var other = new ProtocolVersion(3, 1, 5);

        version.IsCompatibleWith(other).Should().BeFalse();
    }

    [Fact]
    public void Parse_Should_Parse_Valid_Version_String()
    {
        var version = ProtocolVersion.Parse("3.1.0");

        version.Major.Should().Be(3);
        version.Minor.Should().Be(1);
        version.Patch.Should().Be(0);
    }

    [Fact]
    public void Parse_Should_Throw_On_Empty_String()
    {
        var act = () => ProtocolVersion.Parse("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_Should_Throw_On_Invalid_Format()
    {
        var act = () => ProtocolVersion.Parse("3.1");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_Should_Throw_On_Invalid_Major()
    {
        var act = () => ProtocolVersion.Parse("abc.1.0");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToString_Should_Return_Version_String()
    {
        var version = new ProtocolVersion(3, 1, 0);
        version.ToString().Should().Be("3.1.0");
    }

    [Fact]
    public void Equals_Should_Return_True_For_Same_Version()
    {
        var version1 = new ProtocolVersion(3, 1, 0);
        var version2 = new ProtocolVersion(3, 1, 0);

        version1.Equals(version2).Should().BeTrue();
    }

    [Fact]
    public void Equals_Should_Return_False_For_Different_Version()
    {
        var version1 = new ProtocolVersion(3, 1, 0);
        var version2 = new ProtocolVersion(3, 0, 0);

        version1.Equals(version2).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_Should_Be_Same_For_Equal_Versions()
    {
        var version1 = new ProtocolVersion(3, 1, 0);
        var version2 = new ProtocolVersion(3, 1, 0);

        version1.GetHashCode().Should().Be(version2.GetHashCode());
    }
}

