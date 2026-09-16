namespace DiscordUsernameChecker.Services;

public sealed class UsernameCheckBackgroundService(IUsernameCheckService usernameCheckService)
    : BackgroundService
{
    private static readonly TimeSpan s_heartbeatInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var heartbeatTimer = new PeriodicTimer(s_heartbeatInterval);

        try
        {
            while (await heartbeatTimer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    if (usernameCheckService.IsCheckDue)
                    {
                        await usernameCheckService.CheckNextUsernameAsync(stoppingToken);
                    }
                }
                catch (Exception) when (!stoppingToken.IsCancellationRequested)
                {
                    // Keep The Loop Alive
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore On Shutdown
        }
    }
}
