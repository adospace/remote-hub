using System.Windows;
using RemoteHub.Core.Services;
using Application = System.Windows.Application;

namespace RemoteHub.Services;

/// <summary>
/// Maps a <see cref="ThemePreference"/> to WPF's built-in Fluent <c>Application.ThemeMode</c>.
/// </summary>
public sealed class ThemeManager
{
    /// <summary>
    /// Applies the given preference to the running application's theme.
    /// </summary>
    public void Apply(ThemePreference pref)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        app.ThemeMode = pref switch
        {
            ThemePreference.Light => ThemeMode.Light,
            ThemePreference.Dark => ThemeMode.Dark,
            _ => ThemeMode.System,
        };
    }
}
