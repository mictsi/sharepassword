using Sekura.Options;

namespace Sekura.Tests;

public class PasskeyOptionsTests
{
    [Fact]
    public void Disabled_IsAlwaysValid()
    {
        var options = new PasskeyOptions { Enabled = false };

        Assert.True(options.IsConfigurationValid());
    }

    [Fact]
    public void Enabled_RequiresServerDomainAndOrigins()
    {
        Assert.False(new PasskeyOptions { Enabled = true }.IsConfigurationValid());
        Assert.False(new PasskeyOptions { Enabled = true, ServerDomain = "example.com" }.IsConfigurationValid());
        Assert.False(new PasskeyOptions { Enabled = true, Origins = ["https://example.com"] }.IsConfigurationValid());
    }

    [Fact]
    public void Enabled_RejectsRelativeOrigins()
    {
        var options = new PasskeyOptions
        {
            Enabled = true,
            ServerDomain = "example.com",
            Origins = ["/relative"]
        };

        Assert.False(options.IsConfigurationValid());
    }

    [Fact]
    public void Enabled_AcceptsDomainAndAbsoluteOrigins()
    {
        var options = new PasskeyOptions
        {
            Enabled = true,
            ServerDomain = "sekura.example.com",
            Origins = ["https://sekura.example.com"]
        };

        Assert.True(options.IsConfigurationValid());
    }
}
