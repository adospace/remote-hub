using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using RemoteHub.Core.Import;
using RemoteHub.Core.Security;
using RemoteHub.Core.Services;
using RemoteHub.Diagnostics;
using RemoteHub.Services;
using RemoteHub.ViewModels;
using RemoteHub.Views;

namespace RemoteHub;

/// <summary>
/// Application entry point. Builds the DI container, applies the theme, and shows the main window.
/// This file owns ALL service and view-model registrations for the app.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    /// <summary>The composed service provider. Available after <see cref="OnStartup"/>.</summary>
    public IServiceProvider Services =>
        _services ?? throw new InvalidOperationException("Service provider not initialized.");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global crash logging so failures are never silent.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        Log.Info($"RemoteHub starting (logs at {Log.Directory}).");

        _services = ConfigureServices();

        // Load settings (fall back to defaults so a stubbed/broken service never blocks launch).
        AppSettings settings;
        try
        {
            settings = _services.GetRequiredService<ISettingsService>().Load();
        }
        catch
        {
            settings = new AppSettings();
        }

        // Apply the Fluent theme before any window is shown.
        _services.GetRequiredService<ThemeManager>().Apply(settings.Theme);

        var window = _services.GetRequiredService<MainWindow>();
        window.Width = settings.WindowWidth;
        window.Height = settings.WindowHeight;
        if (settings.WindowMaximized)
        {
            window.WindowState = WindowState.Maximized;
        }

        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Persist window size + settings on exit. Guarded so shutdown never throws.
        try
        {
            if (_services is not null && MainWindow is not null)
            {
                var settingsService = _services.GetRequiredService<ISettingsService>();
                var settings = settingsService.Load();
                settings.WindowMaximized = MainWindow.WindowState == WindowState.Maximized;
                if (MainWindow.WindowState == WindowState.Normal)
                {
                    settings.WindowWidth = MainWindow.Width;
                    settings.WindowHeight = MainWindow.Height;
                }
                settingsService.Save(settings);
            }
        }
        catch
        {
            // best-effort persistence
        }

        _services?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Unhandled UI exception.", e.Exception);
        // Keep the app alive and tell the user, rather than crashing silently.
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nDetails were written to:\n{Log.Directory}",
            "RemoteHub", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Error("Fatal unhandled exception (terminating=" + e.IsTerminating + ").", e.ExceptionObject as Exception);
    }

    private static void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        Log.Error("Unobserved task exception.", e.Exception);
        e.SetObserved();
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Core + app services (singletons).
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IConnectionStore, ConnectionStore>();
        services.AddSingleton<IConnectionImporter, RdmXmlImporter>();
        services.AddSingleton<ICredentialProtector, MasterKeyService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ThemeManager>();

        // View-models.
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<SessionsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ConnectionEditorViewModel>();

        // Windows.
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
