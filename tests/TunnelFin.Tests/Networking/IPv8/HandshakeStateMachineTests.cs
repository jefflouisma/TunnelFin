using FluentAssertions;
using TunnelFin.Networking.IPv8;

namespace TunnelFin.Tests.Networking.IPv8;

public class HandshakeStateMachineTests
{
    [Fact]
    public void HandshakeStateMachine_Should_Initialize_With_Default_Timeout()
    {
        var machine = new HandshakeStateMachine();
        machine.Count.Should().Be(0);
    }

    [Fact]
    public void HandshakeStateMachine_Should_Initialize_With_Custom_Timeout()
    {
        var machine = new HandshakeStateMachine(timeoutSeconds: 5);
        machine.Count.Should().Be(0);
    }

    [Fact]
    public void Constructor_Should_Throw_On_Invalid_Timeout()
    {
        var act = () => new HandshakeStateMachine(timeoutSeconds: 0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetState_Should_Return_None_For_Unknown_Peer()
    {
        var machine = new HandshakeStateMachine();
        var state = machine.GetState("unknown");

        state.Should().Be(HandshakeState.None);
    }

    [Fact]
    public void UpdateState_Should_Add_New_Peer()
    {
        var machine = new HandshakeStateMachine();
        var publicKeyHex = "abcd1234";

        machine.UpdateState(publicKeyHex, HandshakeState.IntroRequestSent);

        machine.Count.Should().Be(1);
        machine.GetState(publicKeyHex).Should().Be(HandshakeState.IntroRequestSent);
    }

    [Fact]
    public void UpdateState_Should_Update_Existing_Peer()
    {
        var machine = new HandshakeStateMachine();
        var publicKeyHex = "abcd1234";

        machine.UpdateState(publicKeyHex, HandshakeState.IntroRequestSent);
        machine.UpdateState(publicKeyHex, HandshakeState.IntroResponseReceived);

        machine.Count.Should().Be(1);
        machine.GetState(publicKeyHex).Should().Be(HandshakeState.IntroResponseReceived);
    }

    [Fact]
    public void GetState_Should_Detect_Timeout()
    {
        var machine = new HandshakeStateMachine(timeoutSeconds: 1);
        var publicKeyHex = "abcd1234";

        machine.UpdateState(publicKeyHex, HandshakeState.IntroRequestSent);

        // Wait for timeout
        Thread.Sleep(1100);

        var state = machine.GetState(publicKeyHex);
        state.Should().Be(HandshakeState.TimedOut);
    }

    [Fact]
    public void GetState_Should_Not_Timeout_Completed_Handshake()
    {
        var machine = new HandshakeStateMachine(timeoutSeconds: 1);
        var publicKeyHex = "abcd1234";

        machine.UpdateState(publicKeyHex, HandshakeState.IntroResponseReceived);

        // Wait beyond timeout
        Thread.Sleep(1100);

        var state = machine.GetState(publicKeyHex);
        state.Should().Be(HandshakeState.IntroResponseReceived);
    }

    [Fact]
    public void RemovePeer_Should_Remove_Existing_Peer()
    {
        var machine = new HandshakeStateMachine();
        var publicKeyHex = "abcd1234";

        machine.UpdateState(publicKeyHex, HandshakeState.IntroRequestSent);
        var removed = machine.RemovePeer(publicKeyHex);

        removed.Should().BeTrue();
        machine.Count.Should().Be(0);
    }

    [Fact]
    public void RemovePeer_Should_Return_False_For_Unknown_Peer()
    {
        var machine = new HandshakeStateMachine();
        var removed = machine.RemovePeer("unknown");

        removed.Should().BeFalse();
    }

    [Fact]
    public void GetPeersInState_Should_Return_Matching_Peers()
    {
        var machine = new HandshakeStateMachine();

        machine.UpdateState("peer1", HandshakeState.IntroRequestSent);
        machine.UpdateState("peer2", HandshakeState.IntroRequestSent);
        machine.UpdateState("peer3", HandshakeState.IntroResponseReceived);

        var peers = machine.GetPeersInState(HandshakeState.IntroRequestSent);

        peers.Should().HaveCount(2);
        peers.Should().Contain("peer1");
        peers.Should().Contain("peer2");
    }

    [Fact]
    public void GetPeersInState_Should_Return_Empty_For_No_Matches()
    {
        var machine = new HandshakeStateMachine();
        machine.UpdateState("peer1", HandshakeState.IntroRequestSent);

        var peers = machine.GetPeersInState(HandshakeState.PunctureReceived);

        peers.Should().BeEmpty();
    }

    [Fact]
    public void Clear_Should_Remove_All_Peers()
    {
        var machine = new HandshakeStateMachine();

        machine.UpdateState("peer1", HandshakeState.IntroRequestSent);
        machine.UpdateState("peer2", HandshakeState.IntroResponseReceived);

        machine.Clear();

        machine.Count.Should().Be(0);
    }

    [Fact]
    public void UpdateState_Should_Throw_On_Empty_PublicKey()
    {
        var machine = new HandshakeStateMachine();
        var act = () => machine.UpdateState("", HandshakeState.IntroRequestSent);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetState_Should_Throw_On_Empty_PublicKey()
    {
        var machine = new HandshakeStateMachine();
        var act = () => machine.GetState("");

        act.Should().Throw<ArgumentException>();
    }
}

