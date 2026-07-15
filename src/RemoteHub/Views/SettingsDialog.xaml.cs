using System.Windows;
using RemoteHub.ViewModels;

namespace RemoteHub.Views;

public partial class SettingsDialog : Window
{
    public SettingsDialog(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        // TODO(Implement): validate + persist via the view-model before closing.
        DialogResult = true;
    }
}
