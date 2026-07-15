using System.Windows;
using RemoteHub.ViewModels;

namespace RemoteHub.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
