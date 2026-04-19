namespace SharePassword.Services;

public interface IApplicationTime
{
    DateTime UtcNow { get; }
    DateTimeOffset Now { get; }
    TimeZoneInfo TimeZone { get; }
    string TimeZoneId { get; }
    DateTimeOffset ConvertUtcToApplicationTime(DateTime utcDateTime);
    string FormatUtcForDisplay(DateTime utcDateTime);
}