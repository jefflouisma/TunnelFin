using FluentAssertions;
using TunnelFin.Networking.IPv8;

namespace TunnelFin.Tests.Networking.IPv8;

public class NatTypeInferenceTests
{
    [Fact]
    public void NatTypeInference_Should_Initialize_With_Default_Threshold()
    {
        var inference = new NatTypeInference();
        // Should not throw
    }

    [Fact]
    public void NatTypeInference_Should_Initialize_With_Custom_Threshold()
    {
        var inference = new NatTypeInference(symmetricNatThreshold: 0.7);
        // Should not throw
    }

    [Fact]
    public void Constructor_Should_Throw_On_Invalid_Threshold()
    {
        var act = () => new NatTypeInference(symmetricNatThreshold: 1.5);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void InferNatType_Should_Return_Unknown_For_No_Data()
    {
        var inference = new NatTypeInference();
        var natType = inference.InferNatType("peer1");

        natType.Should().Be(NatType.Unknown);
    }

    [Fact]
    public void InferNatType_Should_Return_Unknown_For_Insufficient_Data()
    {
        var inference = new NatTypeInference();
        inference.RecordSuccess("peer1");
        inference.RecordSuccess("peer1");

        var natType = inference.InferNatType("peer1");

        natType.Should().Be(NatType.Unknown); // Less than 3 attempts
    }

    [Fact]
    public void InferNatType_Should_Detect_Symmetric_NAT_On_High_Failure_Rate()
    {
        var inference = new NatTypeInference(symmetricNatThreshold: 0.5);

        // 3 failures, 1 success = 75% failure rate
        inference.RecordFailure("peer1");
        inference.RecordFailure("peer1");
        inference.RecordFailure("peer1");
        inference.RecordSuccess("peer1");

        var natType = inference.InferNatType("peer1");

        natType.Should().Be(NatType.Symmetric);
    }

    [Fact]
    public void InferNatType_Should_Detect_PortRestrictedCone_On_Low_Failure_Rate()
    {
        var inference = new NatTypeInference();

        // 1 failure, 9 successes = 10% failure rate
        inference.RecordFailure("peer1");
        for (int i = 0; i < 9; i++)
            inference.RecordSuccess("peer1");

        var natType = inference.InferNatType("peer1");

        natType.Should().Be(NatType.PortRestrictedCone);
    }

    [Fact]
    public void InferNatType_Should_Detect_RestrictedCone_On_Moderate_Failure_Rate()
    {
        var inference = new NatTypeInference();

        // 3 failures, 7 successes = 30% failure rate
        for (int i = 0; i < 3; i++)
            inference.RecordFailure("peer1");
        for (int i = 0; i < 7; i++)
            inference.RecordSuccess("peer1");

        var natType = inference.InferNatType("peer1");

        natType.Should().Be(NatType.RestrictedCone);
    }

    [Fact]
    public void GetFailureRate_Should_Return_Null_For_No_Data()
    {
        var inference = new NatTypeInference();
        var failureRate = inference.GetFailureRate("peer1");

        failureRate.Should().BeNull();
    }

    [Fact]
    public void GetFailureRate_Should_Calculate_Correctly()
    {
        var inference = new NatTypeInference();

        inference.RecordFailure("peer1");
        inference.RecordFailure("peer1");
        inference.RecordSuccess("peer1");
        inference.RecordSuccess("peer1");

        var failureRate = inference.GetFailureRate("peer1");

        failureRate.Should().Be(0.5); // 2 failures / 4 total = 50%
    }

    [Fact]
    public void ShouldUseRelayOnly_Should_Return_True_For_Symmetric_NAT()
    {
        var inference = new NatTypeInference(symmetricNatThreshold: 0.5);

        // Create symmetric NAT scenario
        for (int i = 0; i < 8; i++)
            inference.RecordFailure("peer1");
        for (int i = 0; i < 2; i++)
            inference.RecordSuccess("peer1");

        var shouldUseRelay = inference.ShouldUseRelayOnly("peer1");

        shouldUseRelay.Should().BeTrue();
    }

    [Fact]
    public void ShouldUseRelayOnly_Should_Return_False_For_Cone_NAT()
    {
        var inference = new NatTypeInference();

        // Create cone NAT scenario
        for (int i = 0; i < 9; i++)
            inference.RecordSuccess("peer1");
        inference.RecordFailure("peer1");

        var shouldUseRelay = inference.ShouldUseRelayOnly("peer1");

        shouldUseRelay.Should().BeFalse();
    }

    [Fact]
    public void ClearPeerStats_Should_Remove_Peer_Data()
    {
        var inference = new NatTypeInference();

        inference.RecordSuccess("peer1");
        inference.RecordFailure("peer1");

        var removed = inference.ClearPeerStats("peer1");

        removed.Should().BeTrue();
        inference.GetFailureRate("peer1").Should().BeNull();
    }

    [Fact]
    public void ClearPeerStats_Should_Return_False_For_Unknown_Peer()
    {
        var inference = new NatTypeInference();
        var removed = inference.ClearPeerStats("unknown");

        removed.Should().BeFalse();
    }

    [Fact]
    public void RecordSuccess_Should_Throw_On_Empty_PublicKey()
    {
        var inference = new NatTypeInference();
        var act = () => inference.RecordSuccess("");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecordFailure_Should_Throw_On_Empty_PublicKey()
    {
        var inference = new NatTypeInference();
        var act = () => inference.RecordFailure("");

        act.Should().Throw<ArgumentException>();
    }
}

