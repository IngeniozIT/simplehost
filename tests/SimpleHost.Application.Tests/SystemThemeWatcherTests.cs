using Shouldly;
using SimpleHost.Application.Appearance;
using SimpleHost.Application.Ports;

namespace SimpleHost.Application.Tests;

public class SystemThemeWatcherTests
{
    [Fact]
    public void Shows_the_light_theme_when_the_system_is_set_to_light()
    {
        using var watcher = new SystemThemeWatcher(new FakeSystemThemeProvider { IsLightTheme = true });

        watcher.CurrentTheme.ShouldBe(AppTheme.Light);
    }

    [Fact]
    public void Shows_the_dark_theme_when_the_system_is_set_to_dark()
    {
        using var watcher = new SystemThemeWatcher(new FakeSystemThemeProvider { IsLightTheme = false });

        watcher.CurrentTheme.ShouldBe(AppTheme.Dark);
    }

    [Fact]
    public void Switches_to_light_when_the_system_changes_to_light()
    {
        var provider = new FakeSystemThemeProvider { IsLightTheme = false };
        using var watcher = new SystemThemeWatcher(provider);
        var announced = new List<AppTheme>();
        watcher.ThemeChanged += theme => announced.Add(theme);

        provider.ChangeTo(isLightTheme: true);

        announced.ShouldBe(new[] { AppTheme.Light });
        watcher.CurrentTheme.ShouldBe(AppTheme.Light);
    }

    [Fact]
    public void Switches_to_dark_when_the_system_changes_to_dark()
    {
        var provider = new FakeSystemThemeProvider { IsLightTheme = true };
        using var watcher = new SystemThemeWatcher(provider);
        var announced = new List<AppTheme>();
        watcher.ThemeChanged += theme => announced.Add(theme);

        provider.ChangeTo(isLightTheme: false);

        announced.ShouldBe(new[] { AppTheme.Dark });
        watcher.CurrentTheme.ShouldBe(AppTheme.Dark);
    }

    [Fact]
    public void Follows_every_switch_back_and_forth()
    {
        var provider = new FakeSystemThemeProvider { IsLightTheme = true };
        using var watcher = new SystemThemeWatcher(provider);
        var announced = new List<AppTheme>();
        watcher.ThemeChanged += theme => announced.Add(theme);

        provider.ChangeTo(isLightTheme: false);
        provider.ChangeTo(isLightTheme: true);
        provider.ChangeTo(isLightTheme: false);

        announced.ShouldBe(new[] { AppTheme.Dark, AppTheme.Light, AppTheme.Dark });
        watcher.CurrentTheme.ShouldBe(AppTheme.Dark);
    }

    [Fact]
    public void Ignores_a_system_notification_that_leaves_the_theme_unchanged()
    {
        var provider = new FakeSystemThemeProvider { IsLightTheme = true };
        using var watcher = new SystemThemeWatcher(provider);
        var announced = new List<AppTheme>();
        watcher.ThemeChanged += theme => announced.Add(theme);

        provider.RaiseThemeChanged();

        announced.ShouldBeEmpty();
        watcher.CurrentTheme.ShouldBe(AppTheme.Light);
    }

    [Fact]
    public void Stops_following_the_system_after_it_is_disposed()
    {
        var provider = new FakeSystemThemeProvider { IsLightTheme = true };
        var watcher = new SystemThemeWatcher(provider);
        var announced = new List<AppTheme>();
        watcher.ThemeChanged += theme => announced.Add(theme);

        watcher.Dispose();
        provider.ChangeTo(isLightTheme: false);

        announced.ShouldBeEmpty();
        watcher.CurrentTheme.ShouldBe(AppTheme.Light);
    }

    [Fact]
    public void Does_not_fail_when_the_theme_changes_with_no_listener_attached()
    {
        var provider = new FakeSystemThemeProvider { IsLightTheme = true };
        using var watcher = new SystemThemeWatcher(provider);

        Should.NotThrow(() => provider.ChangeTo(isLightTheme: false));

        watcher.CurrentTheme.ShouldBe(AppTheme.Dark);
    }

    [Fact]
    public void Cannot_be_created_without_a_system_theme_source()
    {
        Should.Throw<ArgumentNullException>(() => new SystemThemeWatcher(null!));
    }

    private sealed class FakeSystemThemeProvider : ISystemThemeProvider
    {
        public bool IsLightTheme { get; set; }

        public event EventHandler? ThemeChanged;

        public void ChangeTo(bool isLightTheme)
        {
            IsLightTheme = isLightTheme;
            RaiseThemeChanged();
        }

        public void RaiseThemeChanged() => ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
}
