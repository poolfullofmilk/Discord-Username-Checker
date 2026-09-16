namespace DiscordUsernameChecker.Helpers;

public static class BrowserTimeZone
{
    private static TimeZoneInfo s_timeZone = TimeZoneInfo.Utc;

    public static void Apply(string? timeZoneId)
    {
        // Keep Utc When The Zone Is Unknown
        if (
            !string.IsNullOrWhiteSpace(timeZoneId)
            && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var timeZone)
        )
        {
            s_timeZone = timeZone;
        }
    }

    public static string FormatTime(DateTimeOffset? value) =>
        value is null
            ? "-"
            : TimeZoneInfo.ConvertTime(value.Value, s_timeZone).ToString("HH:mm:ss");

    public static string FormatShortTime(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, s_timeZone).ToString("HH:mm");
}
