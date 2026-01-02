using FluentAssertions;
using TunnelFin.Networking.Transport;

namespace TunnelFin.Tests.Networking.Transport;

public class RetryPolicyTests
{
    [Fact]
    public void Constructor_Should_Validate_Parameters()
    {
        // Initial delay must be positive
        var act1 = () => new RetryPolicy(initialDelayMs: 0);
        act1.Should().Throw<ArgumentException>().WithMessage("*Initial delay*");

        // Max delay must be >= initial delay
        var act2 = () => new RetryPolicy(initialDelayMs: 1000, maxDelayMs: 500);
        act2.Should().Throw<ArgumentException>().WithMessage("*Max delay*");

        // Max retries must be non-negative
        var act3 = () => new RetryPolicy(maxRetries: -1);
        act3.Should().Throw<ArgumentException>().WithMessage("*Max retries*");

        // Jitter must be between 0 and 1
        var act4 = () => new RetryPolicy(jitterPercent: 1.5);
        act4.Should().Throw<ArgumentException>().WithMessage("*Jitter*");
    }

    [Fact]
    public void GetBackoffDelayMs_Should_Use_Exponential_Backoff()
    {
        var policy = new RetryPolicy(initialDelayMs: 100, maxDelayMs: 5000, jitterPercent: 0);

        // Attempt 0: 100 * 2^0 = 100ms
        policy.GetBackoffDelayMs(0).Should().Be(100);

        // Attempt 1: 100 * 2^1 = 200ms
        policy.GetBackoffDelayMs(1).Should().Be(200);

        // Attempt 2: 100 * 2^2 = 400ms
        policy.GetBackoffDelayMs(2).Should().Be(400);

        // Attempt 3: 100 * 2^3 = 800ms
        policy.GetBackoffDelayMs(3).Should().Be(800);
    }

    [Fact]
    public void GetBackoffDelayMs_Should_Cap_At_MaxDelay()
    {
        var policy = new RetryPolicy(initialDelayMs: 100, maxDelayMs: 500, jitterPercent: 0);

        // Attempt 5: 100 * 2^5 = 3200ms, but capped at 500ms
        policy.GetBackoffDelayMs(5).Should().Be(500);

        // Attempt 10: 100 * 2^10 = 102400ms, but capped at 500ms
        policy.GetBackoffDelayMs(10).Should().Be(500);
    }

    [Fact]
    public void GetBackoffDelayMs_Should_Apply_Jitter()
    {
        var policy = new RetryPolicy(initialDelayMs: 100, maxDelayMs: 5000, jitterPercent: 0.25);

        // Attempt 0: base = 100ms, jitter range = ±25ms
        // Result should be in [75, 125]
        var delays = Enumerable.Range(0, 100).Select(_ => policy.GetBackoffDelayMs(0)).ToList();
        delays.Should().AllSatisfy(d => d.Should().BeInRange(75, 125));

        // Verify jitter is actually random (not all the same value)
        delays.Distinct().Count().Should().BeGreaterThan(10, "jitter should produce varied delays");
    }

    [Fact]
    public void GetBackoffDelayMs_Should_Respect_Jitter_Bounds()
    {
        var policy = new RetryPolicy(initialDelayMs: 1000, maxDelayMs: 5000, jitterPercent: 0.25);

        // Attempt 2: base = 4000ms, jitter range = ±1000ms
        // Result should be in [3000, 5000] (capped at maxDelay)
        var delays = Enumerable.Range(0, 100).Select(_ => policy.GetBackoffDelayMs(2)).ToList();
        delays.Should().AllSatisfy(d => d.Should().BeInRange(3000, 5000));
    }

    [Fact]
    public async Task ExecuteAsync_Should_Succeed_On_First_Attempt()
    {
        var policy = new RetryPolicy(maxRetries: 3);
        var callCount = 0;

        var result = await policy.ExecuteAsync(async () =>
        {
            callCount++;
            await Task.Delay(1);
            return 42;
        });

        result.Should().Be(42);
        callCount.Should().Be(1, "should succeed on first attempt");
    }

    [Fact]
    public async Task ExecuteAsync_Should_Retry_On_Failure()
    {
        var policy = new RetryPolicy(initialDelayMs: 10, maxRetries: 3);
        var callCount = 0;

        var result = await policy.ExecuteAsync(async () =>
        {
            callCount++;
            await Task.Delay(1);
            if (callCount < 3)
                throw new InvalidOperationException("Simulated failure");
            return 42;
        });

        result.Should().Be(42);
        callCount.Should().Be(3, "should retry twice before succeeding");
    }

    [Fact]
    public async Task ExecuteAsync_Should_Throw_After_Max_Retries()
    {
        var policy = new RetryPolicy(initialDelayMs: 10, maxRetries: 2);
        var callCount = 0;

        var act = async () => await policy.ExecuteAsync<int>(async () =>
        {
            callCount++;
            await Task.Delay(1);
            throw new InvalidOperationException($"Failure {callCount}");
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failure 3");
        callCount.Should().Be(3, "should attempt 3 times (initial + 2 retries)");
    }

    [Fact]
    public async Task ExecuteAsync_Should_Respect_Cancellation()
    {
        var policy = new RetryPolicy(initialDelayMs: 1000, maxRetries: 5);
        var cts = new CancellationTokenSource();
        var callCount = 0;

        var act = async () => await policy.ExecuteAsync<int>(async () =>
        {
            callCount++;
            await Task.Delay(1);
            throw new InvalidOperationException("Simulated failure");
        }, cts.Token);

        // Cancel after first failure
        cts.CancelAfter(50);

        await act.Should().ThrowAsync<OperationCanceledException>();
        callCount.Should().Be(1, "should stop retrying after cancellation");
    }
}

