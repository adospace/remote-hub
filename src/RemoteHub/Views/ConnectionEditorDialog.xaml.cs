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
        // A host is the minimum required to make a usable connection.
        if (DataContext is ConnectionEditorViewModel vm && string.IsNullOrWhiteSpace(vm.Host))
        {
            MessageBox.Show(this, "Please enter a host.", "Connection", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}
