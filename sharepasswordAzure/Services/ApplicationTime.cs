using System.Globalization;
using Microsoft.Extensions.Options;
using SharePassword.Options;

namespace SharePassword.Services;

public sealed class ApplicationTime : IApplicationTime
{
    private readonly TimeZoneInfo _timeZone;

    public ApplicationTime(IOptions<ApplicationOptions> applicationOptions)
    {
        _timeZone = ApplicationOptions.ResolveTimeZone(applicationOptions.Value.TimeZoneId);
    }

    public DateTime UtcNow => DateTime.UtcNow;

    public DateTimeOffset Now => ConvertUtcToApplicationTime(UtcNow);

    public TimeZoneInfo TimeZone => _timeZone;

    public string TimeZoneId => _timeZone.Id;

    public DateTimeOffset ConvertUtcToApplicationTime(DateTime utcDateTime)
    {
        var normalizedUtc = utcDateTime.Kind switch
        {
            DateTimeKind.Utc => utcDateTime,
            DateTimeKind.Local => utcDateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTime(new DateTimeOffset(normalizedUtc, TimeSpan.Zero), _timeZone);
    }

    public string FormatUtcForDisplay(DateTime utcDateTime)
    {
        return ConvertUtcToApplicationTime(utcDateTime)
            .ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
    }
}