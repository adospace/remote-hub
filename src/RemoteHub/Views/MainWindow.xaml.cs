using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using RemoteHub.Diagnostics;
using RemoteHub.ViewModels;
// System.Windows.Forms (globally imported) also defines KeyEventArgs; pin to the WPF one.
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using ItemsControl = System.Windows.Controls.ItemsControl;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using Point = System.Windows.Point;
using Popup = System.Windows.Controls.Primitives.Popup;
using ScrollViewer = System.Windows.Controls.ScrollViewer;
using TreeView = System.Windows.Controls.TreeView;
using TreeViewItem = System.Windows.Controls.TreeViewItem;
using VisualTreeHelper = System.Windows.Media.VisualTreeHelper;

namespace RemoteHub.Views;

public partial class MainWindow : Window
{
    // Hovering a collapsed folder this long mid-drag opens it, so nested folders are reachable.
    private static readonly TimeSpan HoverExpandDelay = TimeSpan.FromMilliseconds(700);

    // Dragging within this distance of the tree's top/bottom edge scrolls it.
    private const double AutoScrollMargin = 24;

    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _hoverExpandTimer = new() { Interval = HoverExpandDelay };

    private Point _dragStart;
    private TreeNodeViewModel? _dragCandidate;
    private TreeNodeViewModel? _dropHighlight;
    private TreeNodeViewModel? _hoverFolder;
    private ScrollViewer? _treeScroller;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
        StateChanged += OnStateChanged;
        _hoverExpandTimer.Tick += OnHoverExpandTick;
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

    // --- Tree drag and drop -------------------------------------------------
    //
    // Any real node can be dragged; the Pinned group cannot. A drop lands in a folder: the folder
    // under the pointer, the folder of the connection under the pointer, or the top level over empty
    // space. MainViewModel decides whether that is a legal, non-trivial move and performs it.

    private void OnTreePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(ConnectionsTree);
        _dragCandidate = ContainerAt(e.OriginalSource)?.DataContext as TreeNodeViewModel;
        if (_dragCandidate is { IsPinnedContainer: true })
        {
            _dragCandidate = null;
        }
    }

    private void OnTreePreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragCandidate is null)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _dragCandidate = null;
            return;
        }

        var moved = e.GetPosition(ConnectionsTree) - _dragStart;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var source = _dragCandidate;
        _dragCandidate = null;
        try
        {
            // Blocks until the drop (or Escape); DragOver/Drop below run meanwhile.
            DragDrop.DoDragDrop(ConnectionsTree, new DataObject(typeof(TreeNodeViewModel), source), DragDropEffects.Move);
        }
        finally
        {
            EndDragFeedback();
        }
    }

    private void OnTreeDragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
        e.Effects = DragDropEffects.None;
        if (e.Data.GetData(typeof(TreeNodeViewModel)) is not TreeNodeViewModel source)
        {
            ShowDropTarget(null, allowed: false);
            return;
        }

        AutoScroll(e.GetPosition(ConnectionsTree).Y);

        var item = ContainerAt(e.OriginalSource);
        ScheduleHoverExpand(item?.DataContext as TreeNodeViewModel);

        var target = DropTargetFor(item);
        var allowed = _viewModel.CanDrop(source, target);
        e.Effects = allowed ? DragDropEffects.Move : DragDropEffects.None;
        ShowDropTarget(target, allowed);
    }

    private void OnTreeDragLeave(object sender, DragEventArgs e)
    {
        // DragLeave also bubbles up from every row the pointer crosses; only a real exit counts.
        var p = e.GetPosition(ConnectionsTree);
        if (p.X < 0 || p.Y < 0 || p.X >= ConnectionsTree.ActualWidth || p.Y >= ConnectionsTree.ActualHeight)
        {
            EndDragFeedback();
        }
    }

    // async void: an event handler. Failures are logged rather than surfaced as a crash dialog.
    private async void OnTreeDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        var source = e.Data.GetData(typeof(TreeNodeViewModel)) as TreeNodeViewModel;
        var target = DropTargetFor(ContainerAt(e.OriginalSource));
        EndDragFeedback();
        if (source is null)
        {
            return;
        }

        try
        {
            await _viewModel.DropAsync(source, target);
        }
        catch (Exception ex)
        {
            Log.Error("Moving a tree node by drag and drop failed.", ex);
        }
    }

    /// <summary>
    /// Where a drop over <paramref name="item"/> lands: a folder (or the Pinned group) receives it
    /// directly, a connection passes it to its own folder, and null — empty space, or a top-level
    /// connection — means the top level.
    /// </summary>
    private static TreeNodeViewModel? DropTargetFor(TreeViewItem? item)
    {
        if (item?.DataContext is not TreeNodeViewModel node)
        {
            return null;
        }

        if (node.IsFolder)
        {
            return node;
        }

        return ItemsControl.ItemsControlFromItemContainer(item) is TreeViewItem parent
            ? parent.DataContext as TreeNodeViewModel
            : null;
    }

    /// <summary>The tree row that contains <paramref name="source"/>, or null when it is not in a row.</summary>
    private static TreeViewItem? ContainerAt(object source)
    {
        for (var current = source as DependencyObject; current is not null; current = ParentOf(current))
        {
            switch (current)
            {
                case TreeViewItem item:
                    return item;
                case TreeView:
                    return null;
            }
        }

        return null;
    }

    // Hit-test sources can be text Runs, which live only in the logical tree.
    private static DependencyObject? ParentOf(DependencyObject node) =>
        node is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(node)
            : LogicalTreeHelper.GetParent(node);

    private void ShowDropTarget(TreeNodeViewModel? target, bool allowed)
    {
        var highlight = allowed ? target : null;
        if (!ReferenceEquals(highlight, _dropHighlight))
        {
            if (_dropHighlight is not null)
            {
                _dropHighlight.IsDropTarget = false;
            }

            _dropHighlight = highlight;
            if (highlight is not null)
            {
                highlight.IsDropTarget = true;
            }
        }

        RootDropHighlight.Visibility = allowed && target is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void EndDragFeedback()
    {
        ShowDropTarget(null, allowed: false);
        _hoverExpandTimer.Stop();
        _hoverFolder = null;
    }

    private void ScheduleHoverExpand(TreeNodeViewModel? node)
    {
        var folder = node is { IsFolder: true, IsExpanded: false } ? node : null;
        if (ReferenceEquals(folder, _hoverFolder))
        {
            return;
        }

        _hoverFolder = folder;
        _hoverExpandTimer.Stop();
        if (folder is not null)
        {
            _hoverExpandTimer.Start();
        }
    }

    private void OnHoverExpandTick(object? sender, EventArgs e)
    {
        _hoverExpandTimer.Stop();
        if (_hoverFolder is not null)
        {
            _hoverFolder.IsExpanded = true;
            _hoverFolder = null;
        }
    }

    // OLE keeps raising DragOver while the pointer rests, so this scrolls steadily at the edges.
    private void AutoScroll(double y)
    {
        _treeScroller ??= FindDescendant<ScrollViewer>(ConnectionsTree);
        if (_treeScroller is null)
        {
            return;
        }

        if (y < AutoScrollMargin)
        {
            _treeScroller.LineUp();
        }
        else if (y > ConnectionsTree.ActualHeight - AutoScrollMargin)
        {
            _treeScroller.LineDown();
        }
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
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
