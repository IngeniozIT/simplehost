using Shouldly;
using SimpleHost.Application.Tray;

namespace SimpleHost.Application.Tests;

public class TrayMenuFactoryTests
{
    [Fact]
    public void Names_the_tray_icon_after_the_app()
    {
        new TrayMenuFactory().Create().Tooltip.ShouldBe("SimpleHost");
    }

    [Fact]
    public void Offers_opening_the_app_then_exiting_as_the_quick_actions()
    {
        new TrayMenuFactory().Create().Items.ShouldBe(new[]
        {
            new TrayMenuItem("Open SimpleHost", TrayMenuItemKind.OpenMainWindow),
            new TrayMenuItem("Exit", TrayMenuItemKind.Exit),
        });
    }
}
