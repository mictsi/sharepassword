using System.Net;

namespace SharePassword.Options;

public class ForwardedHeadersProxyOptions
{
    public const string SectionName = "ForwardedHeaders";

    public bool Enabled { get; set; } = false;

    /// <summary>Proxy IP addresses trusted to supply X-Forwarded-* headers, e.g. "127.0.0.1" or "::1".</summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>Trusted proxy networks in CIDR notation, e.g. "10.0.0.0/8".</summary>
    public string[] KnownNetworks { get; set; } = [];

    public static bool TryParseProxy(string value, out IPAddress address)
    {
        return IPAddress.TryParse((value ?? string.Empty).Trim(), out address!);
    }

    public static bool TryParseNetwork(string value, out IPNetwork network)
    {
        return IPNetwork.TryParse((value ?? string.Empty).Trim(), out network);
    }

    public bool AreEntriesValid()
    {
        return KnownProxies.All(entry => TryParseProxy(entry, out _))
            && KnownNetworks.All(entry => TryParseNetwork(entry, out _));
    }
}
