using System.Windows;
using RemoteHub.ViewModels;

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
        // TODO(Implement): validate before closing.
        DialogResult = true;
    }
}
