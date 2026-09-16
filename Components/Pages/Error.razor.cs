using System.Diagnostics;

namespace DiscordUsernameChecker.Components.Pages;

public sealed partial class Error
{
    private string? RequestId { get; set; }
    private bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    protected override void OnInitialized() => RequestId = Activity.Current?.Id;
}
