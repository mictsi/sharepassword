namespace SharePassword.Options;

public class LoginThrottleOptions
{
    public const string SectionName = "LoginThrottle";

    /// <summary>Consecutive failed sign-in attempts per account before the pause starts.</summary>
    public int FailedAttemptLimit { get; set; } = 5;

    /// <summary>How long sign-in stays paused for the account once the limit is reached.</summary>
    public int PauseMinutes { get; set; } = 15;

    /// <summary>Failed attempts older than this are forgotten.</summary>
    public int FailureWindowMinutes { get; set; } = 60;
}
