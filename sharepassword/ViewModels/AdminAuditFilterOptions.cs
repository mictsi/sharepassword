namespace SharePassword.ViewModels;

public static class AdminAuditOperationOption
{
    public const string All = "all";
}

public static class AdminAuditSuccessOption
{
    public const string All = "all";
    public const string Success = "success";
    public const string Failure = "failure";

    public static readonly IReadOnlyList<string> AllValues =
    [
        All,
        Success,
        Failure
    ];

    public static string Normalize(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? All : value.Trim().ToLowerInvariant();
        return AllValues.Contains(normalized, StringComparer.Ordinal) ? normalized : All;
    }

    public static string GetLabel(string value)
    {
        return Normalize(value) switch
        {
            Success => "Successful only",
            Failure => "Failed only",
            _ => "All results"
        };
    }
}