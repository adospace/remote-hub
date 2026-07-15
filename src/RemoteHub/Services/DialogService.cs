using System.Windows;
using System.Windows.Controls;
using RemoteHub.ViewModels;
using RemoteHub.Views;
// Aliases disambiguate WPF types from the globally-imported System.Windows.Forms namespace.
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Orientation = System.Windows.Controls.Orientation;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using TextBox = System.Windows.Controls.TextBox;

namespace RemoteHub.Services;

/// <summary>
/// Default WPF implementation of <see cref="IDialogService"/> using windows and common dialogs.
/// </summary>
public sealed class DialogService : IDialogService
{
    public bool? EditConnection(ConnectionEditorViewModel vm)
    {
        var dialog = new ConnectionEditorDialog(vm) { Owner = ActiveWindow };
        return dialog.ShowDialog();
    }

    public bool? ShowSettings(SettingsViewModel vm)
    {
        var dialog = new SettingsDialog(vm) { Owner = ActiveWindow };
        return dialog.ShowDialog();
    }

    public string? PickImportFile()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Import from RDM",
            Filter = "RDM export (*.xml)|*.xml|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? PickConnectionsFile(bool save)
    {
        const string filter = "Connections (*.json)|*.json|All files (*.*)|*.*";
        if (save)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Connections file",
                Filter = filter,
                DefaultExt = ".json",
                AddExtension = true,
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        var open = new OpenFileDialog { Title = "Connections file", Filter = filter };
        return open.ShowDialog() == true ? open.FileName : null;
    }

    public bool Confirm(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public string? PromptMasterPassword(string title, string message)
    {
        var dialog = new MasterPasswordDialog(confirmMode: false, title, message) { Owner = ActiveWindow };
        return dialog.ShowDialog() == true ? dialog.EnteredPassword : null;
    }

    public string? CreateMasterPassword(string title, string message)
    {
        var dialog = new MasterPasswordDialog(confirmMode: true, title, message) { Owner = ActiveWindow };
        return dialog.ShowDialog() == true ? dialog.EnteredPassword : null;
    }

    public string? Prompt(string title, string label, string initialValue)
    {
        // A minimal, self-contained text-input dialog built in code (WPF has no InputBox).
        var input = new TextBox { Text = initialValue, Margin = new Thickness(0, 0, 0, 12) };
        input.SelectAll();

        var ok = new Button { Content = "OK", Width = 80, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 4) });
        panel.Children.Add(input);
        panel.Children.Add(buttons);

        var window = new Window
        {
            Title = title,
            Content = panel,
            Width = 380,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            Owner = ActiveWindow,
        };

        ok.Click += (_, _) => window.DialogResult = true;
        window.Loaded += (_, _) => input.Focus();

        return window.ShowDialog() == true ? input.Text : null;
    }

    private static Window? ActiveWindow => Application.Current?.MainWindow;
}
