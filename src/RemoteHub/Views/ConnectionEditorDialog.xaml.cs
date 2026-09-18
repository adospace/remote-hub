using System.Windows;
using RemoteHub.ViewModels;
using MessageBox = System.Windows.MessageBox;
using TabItem = System.Windows.Controls.TabItem;
using TextBox = System.Windows.Controls.TextBox;

namespace RemoteHub.Views;

public partial class ConnectionEditorDialog : Window
{
    public ConnectionEditorDialog(ConnectionEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (DataContext is ConnectionEditorViewModel vm)
        {
            // A host is the minimum required to make a usable connection.
            if (string.IsNullOrWhiteSpace(vm.Host))
            {
                Reject("Please enter a host.", GeneralTab, HostInput);
                return;
            }

            if (vm.Port is < 1 or > 65535)
            {
                Reject("The port must be between 1 and 65535.", GeneralTab, PortInput);
                return;
            }

            if (vm.UsesGateway && string.IsNullOrWhiteSpace(vm.GatewayHost))
            {
                Reject("Please enter the RD Gateway server, or choose not to use one.", AdvancedTab, GatewayHostInput);
                return;
            }

            // PasswordBox.Password is not bindable (by design); hand it to the VM here. A non-empty
            // entry replaces the stored password; blank leaves the existing token untouched.
            if (vm.CanStorePassword)
            {
                vm.NewPassword = PasswordInput.Password;
            }
        }

        DialogResult = true;
    }

    /// <summary>Explains what is wrong, then takes the user to the field that needs fixing.</summary>
    private void Reject(string message, TabItem tab, TextBox field)
    {
        MessageBox.Show(this, message, "Connection", MessageBoxButton.OK, MessageBoxImage.Warning);
        tab.IsSelected = true;

        // The field is only focusable once its tab's content is back in the visual tree.
        Dispatcher.BeginInvoke(() =>
        {
            field.Focus();
            field.SelectAll();
        }, System.Windows.Threading.DispatcherPriority.Input);
    }
}
