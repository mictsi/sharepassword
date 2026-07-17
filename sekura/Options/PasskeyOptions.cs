namespace Sekura.Options;

public class PasskeyOptions
{
    public const string SectionName = "Passkey";

    /// <summary>Enables passkey (WebAuthn) support as a second factor for local users.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// WebAuthn relying-party ID: the site's registrable domain (e.g. "sekura.example.com").
    /// Credentials are bound to this value; changing it invalidates all registered passkeys.
    /// </summary>
    public string ServerDomain { get; set; } = string.Empty;

    /// <summary>Display name shown by authenticators during ceremonies.</summary>
    public string ServerName { get; set; } = "Sekura";

    /// <summary>Allowed web origins, e.g. "https://sekura.example.com".</summary>
    public string[] Origins { get; set; } = [];

    public bool IsConfigurationValid()
    {
        if (!Enabled)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(ServerDomain)
            && Origins.Length > 0
            && Origins.All(IsValidOrigin);
    }

    private static bool IsValidOrigin(string origin)
    {
        return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }
}
