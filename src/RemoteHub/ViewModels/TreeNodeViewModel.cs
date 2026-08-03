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

    /// <summary>
    /// True for the synthetic "Pinned" group at the top of the tree. It is not part of the document,
    /// so structural/edit commands do not apply to it.
    /// </summary>
    public bool IsPinnedContainer { get; init; }

    /// <summary>True when this leaf connection is currently pinned.</summary>
    public bool IsPinned => Node is RdpConnection { IsPinned: true };

    /// <summary>
    /// The connection's host, shown dimmed beside the name so a search that matched on host (rather
    /// than name) is self-explanatory. Null for folders. Read-only: an edit rebuilds the tree, so
    /// there is nothing to keep in sync here.
    /// </summary>
    public string? Host => (Node as RdpConnection)?.Host;

    public bool HasHost => !string.IsNullOrWhiteSpace(Host);

    /// <summary>Context-menu gates: pin a normal connection, unpin a pinned one.</summary>
    public bool CanPin => IsConnection && !IsPinnedContainer && !IsPinned;
    public bool CanUnpin => IsConnection && IsPinned;

    /// <summary>Real (document-backed) nodes support edit/rename/delete/new; the pinned group does not.</summary>
    public bool IsRealNode => !IsPinnedContainer;

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
