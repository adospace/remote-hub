using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using RemoteHub.ViewModels;
// System.Windows.Forms (globally imported) also defines KeyEventArgs; pin to the WPF one.
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using ItemsControl = System.Windows.Controls.ItemsControl;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using Popup = System.Windows.Controls.Primitives.Popup;

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
        StateChanged += OnStateChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    // --- Custom window chrome ------------------------------------------------

    private void OnMinimizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnStateChanged(object? sender, EventArgs e)
    {
        var maximized = WindowState == WindowState.Maximized;
        // Toggle the glyph (Maximize / Restore) and tooltip to match the current state.
        MaxRestoreButton.Content = maximized ? "" : "";
        MaxRestoreButton.ToolTip = maximized ? "Restore" : "Maximize";
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // With WindowStyle="None", a maximized window would otherwise cover the taskbar and spill
        // a few pixels off-screen. Constrain the maximized bounds to the monitor's work area.
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
    }

    private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024;
        if (msg == WM_GETMINMAXINFO)
        {
            AdjustMaximizedBounds(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static void AdjustMaximizedBounds(IntPtr hwnd, IntPtr lParam)
    {
        const int MONITOR_DEFAULTTONEAREST = 0x00000002;
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            return;
        }

        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        var work = info.rcWork;
        var bounds = info.rcMonitor;
        mmi.ptMaxPosition.X = work.left - bounds.left;
        mmi.ptMaxPosition.Y = work.top - bounds.top;
        mmi.ptMaxSize.X = work.right - work.left;
        mmi.ptMaxSize.Y = work.bottom - work.top;
        Marshal.StructureToPtr(mmi, lParam, true);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    // --- Tree interaction ----------------------------------------------------

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

    // --- Tab overflow menu ---------------------------------------------------

    // Dismiss the "show all tabs" flyout once a session row is clicked. The tab switch itself is
    // handled by the overflow ListBox's SelectedItem <-> TabControlEx.SelectedItem two-way binding.
    // Closing on mouse-up (not SelectionChanged) avoids a false close from the initial selection
    // sync when the popup first opens.
    private void OnOverflowItemClicked(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source ||
            ItemsControl.ContainerFromElement((ItemsControl)sender, source) is not ListBoxItem)
        {
            return; // click landed on empty flyout space, not a row
        }

        for (DependencyObject? node = (DependencyObject)sender; node is not null; node = LogicalTreeHelper.GetParent(node))
        {
            if (node is Popup popup)
            {
                popup.SetCurrentValue(Popup.IsOpenProperty, false);
                break;
            }
        }
    }
}
