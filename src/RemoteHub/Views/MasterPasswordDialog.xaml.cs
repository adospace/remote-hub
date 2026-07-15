using System.Windows;

namespace RemoteHub.Views;

/// <summary>
/// Prompts for a master password. In "confirm" mode it shows a second field and validates a
/// minimum length and that both entries match (used to set or change the master password). In
/// unlock mode it shows a single field. The entered value is exposed via <see cref="EnteredPassword"/>.
/// PasswordBox is used (never a bindable TextBox) so the secret is not exposed through data binding.
/// </summary>
public partial class MasterPasswordDialog : Window
{
    private const int MinLength = 4;
    private readonly bool _confirmMode;

    public MasterPasswordDialog(bool confirmMode, string title, string message)
    {
        InitializeComponent();
        _confirmMode = confirmMode;
        Title = title;
        MessageText.Text = message;
        MessageText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;

        if (!confirmMode)
        {
            Field2Label.Visibility = Visibility.Collapsed;
            Password2.Visibility = Visibility.Collapsed;
        }

        Loaded += (_, _) => Password1.Focus();
    }

    /// <summary>The password the user entered (valid only when the dialog returned true).</summary>
    public string EnteredPassword { get; private set; } = string.Empty;

    private void OnOk(object sender, RoutedEventArgs e)
    {
        var pw = Password1.Password;

        if (string.IsNullOrEmpty(pw))
        {
            ShowError("Please enter a password.");
            return;
        }

        if (_confirmMode)
        {
            if (pw.Length < MinLength)
            {
                ShowError($"Use at least {MinLength} characters.");
                return;
            }

            if (pw != Password2.Password)
            {
                ShowError("The passwords do not match.");
                return;
            }
        }

        EnteredPassword = pw;
        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
