namespace DiscordUsernameChecker.Models;

public sealed record UsernameCheckResult(
    string Username,
    UsernameStatus Status,
    DateTimeOffset? LastCheckedAt
);

public enum UsernameStatus
{
    Pending,
    Free,
    Taken,
    Error,
}
