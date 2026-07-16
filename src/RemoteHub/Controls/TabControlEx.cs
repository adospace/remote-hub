using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
// UseWindowsForms pulls WinForms into scope; pin the ambiguous types to their WPF versions.
using TabControl = System.Windows.Controls.TabControl;
using Panel = System.Windows.Controls.Panel;

namespace RemoteHub.Controls;

/// <summary>
/// A <see cref="TabControl"/> that keeps every tab's content alive — one <see cref="ContentPresenter"/>
/// per item, with only the selected one visible — instead of recreating the selected tab's content
/// on every switch (the default WPF behaviour, which reuses a single content host and merely
/// re-points its DataContext). This is required for hosting live content such as embedded RDP
/// sessions: each session gets its own persistent view/control, so switching tabs shows the correct
/// remote desktop and never tears an active session down.
/// </summary>
public sealed class TabControlEx : TabControl
{
    private Panel? _itemsHolder;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _itemsHolder = GetTemplateChild("PART_ItemsHolder") as Panel;
        UpdateSelectedItem();
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        UpdateSelectedItem();
        BringSelectedTabIntoView();
    }

    // Scroll the selected tab's header into view — e.g. when it was chosen from the overflow menu
    // and currently sits off-screen in the clipped, non-wrapping header strip. BringIntoView bubbles
    // a RequestBringIntoView that the header ScrollViewer honours.
    private void BringSelectedTabIntoView()
    {
        var selected = SelectedItem;
        if (selected is null)
        {
            return;
        }

        // A freshly added tab's container may not be realized yet; defer until after layout.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (ItemContainerGenerator.ContainerFromItem(selected) is FrameworkElement container)
            {
                container.BringIntoView();
            }
        }));
    }

    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        UpdateSelectedItem();
    }

    private void UpdateSelectedItem()
    {
        if (_itemsHolder is null)
        {
            return;
        }

        if (SelectedItem is not null)
        {
            EnsurePresenter(SelectedItem);
        }

        PrunePresentersForRemovedItems();

        foreach (ContentPresenter presenter in _itemsHolder.Children)
        {
            presenter.Visibility = Equals(presenter.Content, SelectedItem)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private ContentPresenter EnsurePresenter(object item)
    {
        foreach (ContentPresenter existing in _itemsHolder!.Children)
        {
            if (Equals(existing.Content, item))
            {
                return existing;
            }
        }

        var presenter = new ContentPresenter
        {
            Content = item,
            ContentTemplate = ContentTemplate,
            ContentTemplateSelector = ContentTemplateSelector,
            ContentStringFormat = ContentStringFormat,
            Visibility = Visibility.Collapsed,
        };

        _itemsHolder.Children.Add(presenter);
        return presenter;
    }

    private void PrunePresentersForRemovedItems()
    {
        var live = new HashSet<object>(Items.Cast<object>());
        for (var i = _itemsHolder!.Children.Count - 1; i >= 0; i--)
        {
            if (_itemsHolder.Children[i] is ContentPresenter presenter && !live.Contains(presenter.Content))
            {
                _itemsHolder.Children.RemoveAt(i);
            }
        }
    }
}
