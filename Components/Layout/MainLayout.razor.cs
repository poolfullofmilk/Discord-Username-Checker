using MudBlazor;

namespace DiscordUsernameChecker.Components.Layout;

public sealed partial class MainLayout
{
    private static MudTheme ApplicationTheme { get; } =
        new()
        {
            PaletteDark = new PaletteDark
            {
                Primary = "#5865F2",
                Background = "#131417",
                Surface = "#1B1C20",
                TableLines = "#2A2C31",
            },
            LayoutProperties = new LayoutProperties { DefaultBorderRadius = "16px" },
        };
}
