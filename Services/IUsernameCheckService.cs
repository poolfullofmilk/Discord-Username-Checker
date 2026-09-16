using DiscordUsernameChecker.Models;
using MudBlazor;

namespace DiscordUsernameChecker.Services;

public interface IUsernameCheckService
{
    string UsernamesInput { get; set; }
    bool IsRunning { get; }
    DateTimeOffset? NextCheckAt { get; }
    IReadOnlyList<UsernameCheckResult> CheckResults { get; }
    string AlertMessage { get; }
    Severity AlertSeverity { get; }
    bool IsCheckDue { get; }

    event Action? StateChanged;

    void Start();
    void Stop();
    Task CheckNextUsernameAsync(CancellationToken cancellationToken = default);
    void ClearAlert();
}
