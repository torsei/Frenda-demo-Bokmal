using Bokmal.Database.Seeding;

namespace Bokmal.Tests;

public class DemoDataPolicyTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Local")]
    public void Seeds_when_enabled_outside_production(string environment)
        => Assert.True(DemoDataPolicy.Decide(enabledInConfiguration: true, environment).ShouldSeed);

    [Fact]
    public void Does_not_seed_when_configuration_says_no()
        => Assert.False(DemoDataPolicy.Decide(enabledInConfiguration: false, "Development").ShouldSeed);

    [Theory]
    [InlineData("Production")]
    [InlineData("production")]
    [InlineData("PRODUCTION")]
    public void Never_seeds_production_even_when_configuration_says_yes(string environment)
    {
        // The case that matters: a development settings file reaching a production host.
        // Configuration alone must not be able to fill a real library with invented data.
        var decision = DemoDataPolicy.Decide(enabledInConfiguration: true, environment);

        Assert.False(decision.ShouldSeed);
        Assert.Contains("Production", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
