using RemoteHub.Core.Models;
using Xunit;

namespace RemoteHub.Tests;

public class ConnectionTreeTests
{
    // Production
    //   Web01
    //   Databases
    //     DB01
    // Staging
    // Jump
    private readonly RdpConnection _web01 = new() { Name = "Web01" };
    private readonly RdpConnection _db01 = new() { Name = "DB01" };
    private readonly RdpConnection _jump = new() { Name = "Jump" };
    private readonly FolderNode _databases;
    private readonly FolderNode _production;
    private readonly FolderNode _staging = new() { Name = "Staging" };
    private readonly ConnectionDocument _document;

    public ConnectionTreeTests()
    {
        _databases = new FolderNode { Name = "Databases", Children = { _db01 } };
        _production = new FolderNode { Name = "Production", Children = { _web01, _databases } };
        _document = new ConnectionDocument { Roots = { _production, _staging, _jump } };
    }

    [Fact]
    public void TryFindParent_ReportsTheEnclosingFolder_OrNullAtTheRoot()
    {
        Assert.True(ConnectionTree.TryFindParent(_document, _db01, out var dbParent));
        Assert.Same(_databases, dbParent);

        Assert.True(ConnectionTree.TryFindParent(_document, _jump, out var jumpParent));
        Assert.Null(jumpParent);
    }

    [Fact]
    public void TryFindParent_NodeNotInDocument_ReturnsFalse()
    {
        Assert.False(ConnectionTree.TryFindParent(_document, new RdpConnection(), out _));
    }

    [Fact]
    public void GetAncestors_ListsEnclosingFoldersOutermostFirst()
    {
        Assert.Equal(new[] { _production, _databases }, ConnectionTree.GetAncestors(_document, _db01));
        Assert.Empty(ConnectionTree.GetAncestors(_document, _jump));
    }

    [Fact]
    public void Move_ConnectionIntoAnotherFolder()
    {
        Assert.True(ConnectionTree.Move(_document, _web01, _staging));

        Assert.DoesNotContain(_web01, _production.Children);
        Assert.Contains(_web01, _staging.Children);
    }

    [Fact]
    public void Move_NestedConnectionToTheRoot()
    {
        Assert.True(ConnectionTree.Move(_document, _db01, destination: null));

        Assert.Empty(_databases.Children);
        Assert.Contains(_db01, _document.Roots);
    }

    [Fact]
    public void Move_RootConnectionIntoAFolder()
    {
        Assert.True(ConnectionTree.Move(_document, _jump, _databases));

        Assert.DoesNotContain(_jump, _document.Roots);
        Assert.Contains(_jump, _databases.Children);
    }

    [Fact]
    public void Move_FolderCarriesItsSubtree()
    {
        Assert.True(ConnectionTree.Move(_document, _databases, _staging));

        Assert.DoesNotContain(_databases, _production.Children);
        Assert.Same(_databases, Assert.Single(_staging.Children));
        Assert.Same(_db01, Assert.Single(_databases.Children));
    }

    [Fact]
    public void Move_ToCurrentFolder_IsAllowedAndChangesNothing()
    {
        Assert.True(ConnectionTree.Move(_document, _web01, _production));

        Assert.Equal(new ConnectionNode[] { _web01, _databases }, _production.Children);
    }

    [Fact]
    public void Move_FolderIntoItself_IsRejected()
    {
        Assert.False(ConnectionTree.CanMove(_document, _production, _production));
        Assert.False(ConnectionTree.Move(_document, _production, _production));
        Assert.Contains(_production, _document.Roots);
    }

    [Fact]
    public void Move_FolderIntoItsOwnSubtree_IsRejected()
    {
        Assert.False(ConnectionTree.Move(_document, _production, _databases));

        Assert.Contains(_production, _document.Roots);
        Assert.Contains(_databases, _production.Children);
    }

    [Fact]
    public void Move_ToFolderOutsideTheDocument_IsRejected()
    {
        var stray = new FolderNode { Name = "Stray" };

        Assert.False(ConnectionTree.Move(_document, _web01, stray));
        Assert.Contains(_web01, _production.Children);
        Assert.Empty(stray.Children);
    }

    [Fact]
    public void Move_NodeOutsideTheDocument_IsRejected()
    {
        var stray = new RdpConnection { Name = "Stray" };

        Assert.False(ConnectionTree.Move(_document, stray, _staging));
        Assert.Empty(_staging.Children);
    }

    [Fact]
    public void Remove_DeletesNestedNode()
    {
        Assert.True(ConnectionTree.Remove(_document, _db01));
        Assert.Empty(_databases.Children);

        Assert.False(ConnectionTree.Remove(_document, _db01));
    }
}
