using DiscordUsernameChecker.Models;

namespace DiscordUsernameChecker.Services;

public interface IUsernameCheckLogStore
{
    void AppendCheck(UsernameCheckResult checkResult);
    UsernameCheckResult? FindLatestCheck(string username);
    string? ReadState(string stateKey);
    void WriteState(string stateKey, string stateValue);
}
