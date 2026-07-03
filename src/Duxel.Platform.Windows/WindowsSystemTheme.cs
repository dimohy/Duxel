using Duxel.Core;
using Microsoft.Win32;
using System.Runtime.Versioning;

namespace Duxel.Platform.Windows;

[SupportedOSPlatform("windows")]
public static class WindowsSystemTheme
{
    private const string PersonalizeRegistryKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";

    public static UiSystemColorScheme GetAppColorScheme()
    {
        var value = Registry.GetValue(PersonalizeRegistryKey, AppsUseLightThemeValue, 1);
        return value is int appsUseLightTheme && appsUseLightTheme == 0
            ? UiSystemColorScheme.Dark
            : UiSystemColorScheme.Light;
    }

    public static UiCompiledDesign GetAppDesign()
        => GetAppColorScheme() is UiSystemColorScheme.Dark
            ? UiCompiledDesign.Windows11Dark
            : UiCompiledDesign.Windows11;
}
