using System.Windows;
using RemoteHub.ViewModels;
using MessageBox = System.Windows.MessageBox;

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
                MessageBox.Show(this, "Please enter a host.", "Connection", MessageBoxButton.OK, MessageBoxImage.Warning);
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
}
