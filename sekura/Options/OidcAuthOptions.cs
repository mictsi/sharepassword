namespace Sekura.Options;

public class OidcAuthOptions
{
    public const string SectionName = "OidcAuth";

    public bool Enabled { get; set; } = false;
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string CallbackPath { get; set; } = "/signin-oidc";
    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";
    public bool RequireHttpsMetadata { get; set; } = true;
    public string[] Scopes { get; set; } = ["openid", "profile", "email"];
    public string GroupClaimType { get; set; } = "groups";
    public string AdminRoleName { get; set; } = "Admin";
    public string UserRoleName { get; set; } = "User";
    public string[] AdminGroups { get; set; } = [];
    public string[] UserGroups { get; set; } = [];
    public bool LogTokensForTroubleshooting { get; set; } = false;

    /// <summary>
    /// Controls whether the username/password login form stays reachable while OIDC is enabled.
    /// "LoopbackOnly" (default) allows it only for requests from the local machine,
    /// "Always" allows it for everyone, and "Never" forces every sign-in through OIDC.
    /// </summary>
    public string LocalLoginFallback { get; set; } = LocalLoginFallbackModes.LoopbackOnly;
}

public static class LocalLoginFallbackModes
{
    public const string LoopbackOnly = "LoopbackOnly";
    public const string Always = "Always";
    public const string Never = "Never";

    public static bool IsValid(string? value)
    {
        return string.Equals(value, LoopbackOnly, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, Always, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, Never, StringComparison.OrdinalIgnoreCase);
    }
}
