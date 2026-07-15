using System.Windows;
using System.Windows.Input;
using RemoteHub.ViewModels;
// System.Windows.Forms (globally imported) also defines KeyEventArgs; pin to the WPF one.
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace RemoteHub.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        _viewModel.SelectedNode = e.NewValue as TreeNodeViewModel;
    }

    private void OnTreeMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ConnectSelected();
    }

    private void OnTreeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ConnectSelected();
            e.Handled = true;
        }
    }

    private void ConnectSelected()
    {
        var node = _viewModel.SelectedNode;
        if (node is { IsConnection: true } && _viewModel.ConnectCommand.CanExecute(node))
        {
            _viewModel.ConnectCommand.Execute(node);
        }
    }
}
