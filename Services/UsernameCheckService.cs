using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DiscordUsernameChecker.Models;
using MudBlazor;

namespace DiscordUsernameChecker.Services;

public sealed class UsernameCheckService : IUsernameCheckService
{
    private const string UsernameAttemptUrl =
        "https://discord.com/api/v9/unique-username/username-attempt-unauthed";
    private const int MaximumUsernameCount = 5;
    private const int MinutesBetweenChecks = 12;
    private const double DefaultRetryAfterSeconds = 300;

    // Persisted State Keys
    private const string UsernamesInputStateKey = "UsernamesInput";
    private const string IsRunningStateKey = "IsRunning";
    private const string NextCheckAtStateKey = "NextCheckAt";
    private const string NextUsernameIndexStateKey = "NextUsernameIndex";

    private static readonly HttpClient s_sharedHttpClient = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "DiscordUsernameChecker/1.0" } },
        Timeout = TimeSpan.FromSeconds(15),
    };

    private readonly IUsernameCheckLogStore _logStore;
    private string _usernamesInput = string.Empty;
    private int _nextUsernameIndex;

    public UsernameCheckService(IUsernameCheckLogStore logStore)
    {
        _logStore = logStore;
        RestoreState();
    }

    public string UsernamesInput
    {
        get => _usernamesInput;
        set
        {
            _usernamesInput = value;
            _logStore.WriteState(UsernamesInputStateKey, value);
        }
    }

    public bool IsRunning { get; private set; }
    public DateTimeOffset? NextCheckAt { get; private set; }
    public IReadOnlyList<UsernameCheckResult> CheckResults { get; private set; } = [];
    public string AlertMessage { get; private set; } = string.Empty;
    public Severity AlertSeverity { get; private set; } = Severity.Normal;

    public bool IsCheckDue => IsRunning && NextCheckAt <= DateTimeOffset.UtcNow;

    public event Action? StateChanged;

    public void Start()
    {
        var usernames = ParseUsernames();

        if (usernames.Count == 0)
        {
            SetAlert(Severity.Warning, "Enter At Least One Username");
            return;
        }

        CheckResults = BuildResults(usernames);
        _nextUsernameIndex = 0;
        NextCheckAt = DateTimeOffset.UtcNow;
        IsRunning = true;
        PersistRunState();
        ClearAlert();
    }

    public void Stop()
    {
        IsRunning = false;
        NextCheckAt = null;
        PersistRunState();
        NotifyStateChanged();
    }

    public async Task CheckNextUsernameAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning || CheckResults.Count == 0)
        {
            return;
        }

        var index = _nextUsernameIndex % CheckResults.Count;
        var previousResult = CheckResults[index];
        var (status, retryAfterSeconds) = await CheckUsernameAsync(
            previousResult.Username,
            cancellationToken
        );

        // Wait The Limit Out Instead Of Stopping
        if (retryAfterSeconds is not null)
        {
            NextCheckAt = DateTimeOffset.UtcNow.AddSeconds(retryAfterSeconds.Value);
            PersistRunState();
            SetAlert(
                Severity.Warning,
                $"Rate Limited Retrying In {FormatDuration(retryAfterSeconds.Value)}"
            );
            return;
        }

        var checkedResult = previousResult with
        {
            Status = status,
            LastCheckedAt = DateTimeOffset.UtcNow,
        };

        var workingResults = CheckResults.ToList();
        workingResults[index] = checkedResult;
        CheckResults = workingResults;
        _logStore.AppendCheck(checkedResult);

        if (status == UsernameStatus.Free && previousResult.Status != UsernameStatus.Free)
        {
            SetAlert(Severity.Success, $"Now Free {previousResult.Username}");
        }

        _nextUsernameIndex = (index + 1) % CheckResults.Count;
        NextCheckAt = DateTimeOffset.UtcNow.AddMinutes(MinutesBetweenChecks);
        PersistRunState();
        NotifyStateChanged();
    }

    public void ClearAlert() => SetAlert(Severity.Normal, string.Empty);

    private void RestoreState()
    {
        _usernamesInput = _logStore.ReadState(UsernamesInputStateKey) ?? string.Empty;
        IsRunning =
            bool.TryParse(_logStore.ReadState(IsRunningStateKey), out var isRunning) && isRunning;
        _nextUsernameIndex = int.TryParse(
            _logStore.ReadState(NextUsernameIndexStateKey),
            out var nextUsernameIndex
        )
            ? nextUsernameIndex
            : 0;
        NextCheckAt =
            IsRunning
            && DateTimeOffset.TryParse(
                _logStore.ReadState(NextCheckAtStateKey),
                out var nextCheckAt
            )
                ? nextCheckAt
                : null;

        CheckResults = BuildResults(ParseUsernames());
    }

    private void PersistRunState()
    {
        _logStore.WriteState(IsRunningStateKey, IsRunning.ToString());
        _logStore.WriteState(NextUsernameIndexStateKey, _nextUsernameIndex.ToString());
        _logStore.WriteState(NextCheckAtStateKey, NextCheckAt?.ToString("o") ?? string.Empty);
    }

    private List<string> ParseUsernames() =>
        [
            .. _usernamesInput
                .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaximumUsernameCount),
        ];

    private List<UsernameCheckResult> BuildResults(List<string> usernames) =>
        [
            .. usernames.Select(username =>
                _logStore.FindLatestCheck(username)
                ?? new UsernameCheckResult(username, UsernameStatus.Pending, null)
            ),
        ];

    private static string FormatDuration(double seconds) =>
        seconds >= 60 ? $"{seconds / 60:0} Minutes" : $"{seconds:0} Seconds";

    private static async Task<(
        UsernameStatus Status,
        double? RetryAfterSeconds
    )> CheckUsernameAsync(string username, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await s_sharedHttpClient.PostAsJsonAsync(
                UsernameAttemptUrl,
                new { username },
                cancellationToken
            );

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retryAfterSeconds = await ReadRetryAfterSecondsAsync(
                    response,
                    cancellationToken
                );
                return (UsernameStatus.Error, retryAfterSeconds);
            }

            response.EnsureSuccessStatusCode();
            var attempt = await response.Content.ReadFromJsonAsync<UsernameAttemptResponse>(
                cancellationToken
            );

            return (
                attempt?.Taken switch
                {
                    true => UsernameStatus.Taken,
                    false => UsernameStatus.Free,
                    null => UsernameStatus.Error,
                },
                null
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return (UsernameStatus.Error, null);
        }
    }

    private static async Task<double> ReadRetryAfterSecondsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var rateLimit = await response.Content.ReadFromJsonAsync<RateLimitResponse>(
                cancellationToken
            );
            if (rateLimit is { RetryAfter: > 0 })
            {
                return rateLimit.RetryAfter;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Fall Back To The Header
        }

        return response.Headers.RetryAfter?.Delta?.TotalSeconds ?? DefaultRetryAfterSeconds;
    }

    private void SetAlert(Severity severity, string message)
    {
        AlertSeverity = severity;
        AlertMessage = message;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();

    private sealed record UsernameAttemptResponse(bool Taken);

    private sealed record RateLimitResponse(
        [property: JsonPropertyName("retry_after")] double RetryAfter
    );
}
