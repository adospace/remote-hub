namespace RemoteHub.Core.Models;

/// <summary>
/// Structural queries and edits over the folder tree of a <see cref="ConnectionDocument"/>. Nodes are
/// matched by reference. Wherever a folder is expected, <c>null</c> stands for the document root.
/// </summary>
public static class ConnectionTree
{
    /// <summary>
    /// Finds the folder that directly contains <paramref name="node"/>. Returns false when the node is
    /// not in the document; a top-level node yields true with a null <paramref name="parent"/>.
    /// </summary>
    public static bool TryFindParent(ConnectionDocument document, ConnectionNode node, out FolderNode? parent)
    {
        parent = null;
        return TryFindParent(document.Roots, owner: null, node, ref parent);
    }

    /// <summary>
    /// The folders enclosing <paramref name="node"/>, outermost first. Empty for a top-level node or
    /// one that is not in the document.
    /// </summary>
    public static IReadOnlyList<FolderNode> GetAncestors(ConnectionDocument document, ConnectionNode node)
    {
        var chain = new List<FolderNode>();
        for (var current = node; TryFindParent(document, current, out var parent) && parent is not null; current = parent)
        {
            chain.Add(parent);
        }

        chain.Reverse();
        return chain;
    }

    /// <summary>True when <paramref name="node"/> sits anywhere below <paramref name="folder"/>.</summary>
    public static bool IsDescendantOf(ConnectionNode node, FolderNode folder) =>
        folder.Children.Any(child =>
            ReferenceEquals(child, node) || (child is FolderNode sub && IsDescendantOf(node, sub)));

    /// <summary>
    /// Whether <paramref name="node"/> may be moved into <paramref name="destination"/>. Both must be in
    /// the document, and a folder can never be moved into itself or into its own subtree. Moving a node
    /// to the folder it is already in is allowed (and changes nothing).
    /// </summary>
    public static bool CanMove(ConnectionDocument document, ConnectionNode node, FolderNode? destination)
    {
        if (!TryFindParent(document, node, out _))
        {
            return false;
        }

        if (destination is null)
        {
            return true;
        }

        if (ReferenceEquals(node, destination) ||
            (node is FolderNode folder && IsDescendantOf(destination, folder)))
        {
            return false;
        }

        return TryFindParent(document, destination, out _);
    }

    /// <summary>
    /// Moves <paramref name="node"/> into <paramref name="destination"/>. Returns false, leaving the
    /// document untouched, when <see cref="CanMove"/> does not allow it.
    /// </summary>
    public static bool Move(ConnectionDocument document, ConnectionNode node, FolderNode? destination)
    {
        if (!CanMove(document, node, destination))
        {
            return false;
        }

        TryFindParent(document, node, out var parent);
        if (!ReferenceEquals(parent, destination))
        {
            ChildrenOf(document, parent).Remove(node);
            ChildrenOf(document, destination).Add(node);
        }

        return true;
    }

    /// <summary>Removes <paramref name="node"/> (and, for a folder, its subtree) from the document.</summary>
    public static bool Remove(ConnectionDocument document, ConnectionNode node) =>
        TryFindParent(document, node, out var parent) && ChildrenOf(document, parent).Remove(node);

    private static List<ConnectionNode> ChildrenOf(ConnectionDocument document, FolderNode? folder) =>
        folder?.Children ?? document.Roots;

    private static bool TryFindParent(
        List<ConnectionNode> nodes, FolderNode? owner, ConnectionNode target, ref FolderNode? parent)
    {
        foreach (var node in nodes)
        {
            if (ReferenceEquals(node, target))
            {
                parent = owner;
                return true;
            }
        }

        foreach (var node in nodes)
        {
            if (node is FolderNode folder && TryFindParent(folder.Children, folder, target, ref parent))
            {
                return true;
            }
        }

        return false;
    }
}
