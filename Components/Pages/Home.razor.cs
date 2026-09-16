using DiscordUsernameChecker.Helpers;
using DiscordUsernameChecker.Models;
using DiscordUsernameChecker.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace DiscordUsernameChecker.Components.Pages;

public sealed partial class Home : IDisposable
{
    [Inject]
    private IUsernameCheckService UsernameCheckService { get; set; } = default!;

    [Inject]
    private IJSRuntime JavaScriptRuntime { get; set; } = default!;

    private string RunToggleLabel => UsernameCheckService.IsRunning ? "Stop" : "Start";

    private string RunToggleIcon =>
        UsernameCheckService.IsRunning
            ? Icons.Material.Rounded.Stop
            : Icons.Material.Rounded.PlayArrow;

    private Color RunToggleColor => UsernameCheckService.IsRunning ? Color.Error : Color.Primary;

    private Color RunStatusColor => UsernameCheckService.IsRunning ? Color.Success : Color.Default;

    private string RunStatusText =>
        UsernameCheckService.NextCheckAt is { } nextCheckAt
            ? $"Next Check {BrowserTimeZone.FormatShortTime(nextCheckAt)}"
            : "Stopped";

    protected override void OnInitialized() => UsernameCheckService.StateChanged += OnStateChanged;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        // Read The Browser Zone Once
        var browserTimeZoneId = await JavaScriptRuntime.InvokeAsync<string>("getBrowserTimeZone");
        BrowserTimeZone.Apply(browserTimeZoneId);
        StateHasChanged();
    }

    public void Dispose()
    {
        UsernameCheckService.StateChanged -= OnStateChanged;
        GC.SuppressFinalize(this);
    }

    private void OnStateChanged() => _ = InvokeAsync(StateHasChanged);

    private void ToggleRunning()
    {
        if (UsernameCheckService.IsRunning)
        {
            UsernameCheckService.Stop();
            return;
        }

        UsernameCheckService.Start();
    }

    private static Color GetStatusColor(UsernameStatus status) =>
        status switch
        {
            UsernameStatus.Free => Color.Success,
            UsernameStatus.Taken => Color.Error,
            UsernameStatus.Error => Color.Warning,
            _ => Color.Default,
        };
}
