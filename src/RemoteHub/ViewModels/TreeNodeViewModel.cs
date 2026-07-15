using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RemoteHub.Core.Models;

namespace RemoteHub.ViewModels;

/// <summary>
/// Wraps a <see cref="ConnectionNode"/> (folder or connection) for display in the tree.
/// </summary>
public sealed partial class TreeNodeViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isExpanded = true;

    public TreeNodeViewModel(ConnectionNode node)
    {
        Node = node;
        Name = node.Name;
        IsFolder = node is FolderNode;
        if (node is FolderNode folder)
        {
            IsExpanded = folder.IsExpanded;
        }
    }

    /// <summary>The underlying model node.</summary>
    public ConnectionNode Node { get; }

    public bool IsFolder { get; }

    public ObservableCollection<TreeNodeViewModel> Children { get; } = new();
}
