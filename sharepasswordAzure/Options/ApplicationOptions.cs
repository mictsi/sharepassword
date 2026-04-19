namespace SharePassword.Options;

public class ApplicationOptions
{
    public const string SectionName = "Application";

    public string Name { get; set; } = "sharepasswordAzure";
    public bool EnableHttpsRedirection { get; set; }
    public string PathBase { get; set; } = "/";
    public int AuthenticationSessionTimeoutMinutes { get; set; } = 60;
    public bool AuthenticationSlidingExpiration { get; set; } = true;

    public static bool IsValidPathBase(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), "/", StringComparison.Ordinal))
        {
            return true;
        }

        var normalized = value.Trim();
        if (normalized.Contains("://", StringComparison.Ordinal) || normalized.Contains('?') || normalized.Contains('#'))
        {
            return false;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length > 0 && segments.All(segment => !string.IsNullOrWhiteSpace(segment));
    }

    public static string NormalizePathBase(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), "/", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var segments = value
            .Trim()
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return segments.Length == 0
            ? string.Empty
            : "/" + string.Join('/', segments);
    }
}