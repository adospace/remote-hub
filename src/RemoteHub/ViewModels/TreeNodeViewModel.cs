using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RemoteHub.Core.Models;

namespace RemoteHub.ViewModels;

/// <summary>
/// Wraps a <see cref="ConnectionNode"/> (folder or connection) for display in the tree.
/// Keeps the underlying model in sync when the name or expansion state changes.
/// </summary>
public sealed partial class TreeNodeViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    // Folders start collapsed on every launch (the tree is expanded on demand by the user).
    [ObservableProperty]
    private bool _isExpanded;

    public TreeNodeViewModel(ConnectionNode node)
    {
        Node = node;
        IsFolder = node is FolderNode;
        Name = node.Name;
    }

    /// <summary>The underlying model node.</summary>
    public ConnectionNode Node { get; }

    public bool IsFolder { get; }

    /// <summary>True for leaf connection nodes (used to gate the Connect action).</summary>
    public bool IsConnection => !IsFolder;

    public ObservableCollection<TreeNodeViewModel> Children { get; } = new();

    partial void OnNameChanged(string value) => Node.Name = value;

    partial void OnIsExpandedChanged(bool value)
    {
        if (Node is FolderNode folder)
        {
            folder.IsExpanded = value;
        }
    }
}
