using DeviceRental.Application.Policy;
using Xunit;

namespace DeviceRental.UnitTests.Policy;

public sealed class AccessWindowPolicyTests
{
    private readonly AccessWindowPolicy _policy = new();

    [Theory]
    [InlineData("2026-09-01T00:59:59.999Z", false, "2026-09-01T01:00:00.000Z")]
    [InlineData("2026-09-01T01:00:00.000Z", true, null)]
    [InlineData("2026-09-01T10:59:59.999Z", true, null)]
    [InlineData("2026-09-01T11:00:00.000Z", false, "2026-09-02T01:00:00.000Z")]
    public void Evaluate_UsesHalfOpenShanghaiWindow(
        string utcText,
        bool expectedOpen,
        string? expectedNextOpenUtcText)
    {
        var decision = _policy.Evaluate(DateTimeOffset.Parse(utcText));

        Assert.Equal(expectedOpen, decision.IsOpen);
        Assert.Equal(
            expectedNextOpenUtcText is null ? null : DateTimeOffset.Parse(expectedNextOpenUtcText),
            decision.NextOpenUtc);
    }

    [Fact]
    [Trait("Requirement", "REQ-TIME-001")]
    [Trait("Requirement", "REQ-TIME-002")]
    public void Evaluate_NormalizesNonUtcInputAndNeverReadsSystemTime()
    {
        var decision = _policy.Evaluate(new DateTimeOffset(2026, 9, 1, 8, 59, 59, 999, TimeSpan.FromHours(8)));

        Assert.False(decision.IsOpen);
        Assert.Equal(DateTimeOffset.Parse("2026-09-01T01:00:00Z"), decision.NextOpenUtc);
        Assert.Equal(TimeSpan.Zero, decision.NextOpenUtc!.Value.Offset);
    }
}
