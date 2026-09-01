using DeviceRental.Application.Identity;
using Xunit;

namespace DeviceRental.UnitTests.Identity;

public sealed class CorporateEmailPolicyTests
{
    [Theory]
    [InlineData(" Alice@Example.COM ", "alice@example.com")]
    [InlineData("USER@xn--fsqu00a.xn--55qx5d", "user@xn--fsqu00a.xn--55qx5d")]
    [InlineData("User@例子.公司", "user@xn--fsqu00a.xn--55qx5d")]
    [Trait("Requirement", "REQ-AUTH-002")]
    public void Evaluate_NormalizesTheWholeEmailAndIdnaDomain(string input, string expected)
    {
        var policy = new CorporateEmailPolicy(["example.com", "例子.公司"]);

        var decision = policy.Evaluate(input);

        Assert.True(decision.IsAllowed);
        Assert.Equal(expected, decision.NormalizedEmail);
    }

    [Theory]
    [InlineData("user@sub.example.com")]
    [InlineData("user@example.com.evil.test")]
    [InlineData("user@other.test")]
    public void Evaluate_RequiresAnExactConfiguredDomain(string input)
    {
        var decision = new CorporateEmailPolicy(["example.com"]).Evaluate(input);

        Assert.False(decision.IsAllowed);
        Assert.NotNull(decision.NormalizedEmail);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("a@@example.com")]
    [InlineData("display <a@example.com>")]
    [InlineData("a @example.com")]
    [InlineData("a@example.com.")]
    public void Evaluate_RejectsMalformedAddresses(string input)
    {
        var decision = new CorporateEmailPolicy(["example.com"]).Evaluate(input);

        Assert.False(decision.IsAllowed);
        Assert.Null(decision.NormalizedEmail);
    }

    [Fact]
    public void Constructor_RejectsEmptyOrMalformedAllowedDomains()
    {
        Assert.Throws<ArgumentException>(() => new CorporateEmailPolicy([]));
        Assert.Throws<ArgumentException>(() => new CorporateEmailPolicy(["sub..example.com"]));
    }
}
