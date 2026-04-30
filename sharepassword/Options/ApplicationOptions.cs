namespace SharePassword.Options;

public class ApplicationOptions
{
    public const string SectionName = "Application";

    public string Name { get; set; } = "sharepassword";
    public bool EnableHttpsRedirection { get; set; }
    public string PathBase { get; set; } = "/";
    public string TimeZoneId { get; set; } = "UTC";
    public int AuthenticationSessionTimeoutMinutes { get; set; } = 60;
    public bool AuthenticationSlidingExpiration { get; set; } = true;

    public static bool IsValidTimeZoneId(string? value)
    {
        return TryResolveTimeZone(value, out _);
    }

    public static TimeZoneInfo ResolveTimeZone(string? value)
    {
        if (TryResolveTimeZone(value, out var timeZone))
        {
            return timeZone;
        }

        throw new TimeZoneNotFoundException($"Application:TimeZoneId '{value}' was not found on this host.");
    }

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

    private static bool TryResolveTimeZone(string? value, out TimeZoneInfo timeZone)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "UTC" : value.Trim();

        if (TryFindSystemTimeZone(normalized, out timeZone))
        {
            return true;
        }

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(normalized, out var windowsId)
            && TryFindSystemTimeZone(windowsId, out timeZone))
        {
            return true;
        }

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(normalized, out var ianaId)
            && TryFindSystemTimeZone(ianaId, out timeZone))
        {
            return true;
        }

        timeZone = TimeZoneInfo.Utc;
        return false;
    }

    private static bool TryFindSystemTimeZone(string id, out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        timeZone = TimeZoneInfo.Utc;
        return false;
    }
}