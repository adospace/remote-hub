using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using RemoteHub.Core.Models;
using RemoteHub.Core.Security;
using RemoteHub.Core.Services;
using RemoteHub.Services;
using Application = System.Windows.Application;

namespace RemoteHub.ViewModels;

/// <summary>
/// Root view-model for the main window. Owns the loaded document, the tree, and the top-level
/// commands (add/edit/delete nodes, settings, connect). All edits persist automatically; there is
/// no explicit save, and importing lives in the settings dialog.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IConnectionStore _store;
    private readonly IDialogService _dialogs;
    private readonly ISettingsService _settingsService;
    private readonly ICredentialProtector _protector;
    private readonly SessionsViewModel _sessions;
    private readonly IServiceProvider _services;

    private ConnectionDocument _document = new();
    private string _connectionsPath = string.Empty;

    public MainViewModel(
        IConnectionStore store,
        IDialogService dialogs,
        ISettingsService settings,
        ICredentialProtector protector,
        SessionsViewModel sessions,
        IServiceProvider services)
    {
        _store = store;
        _dialogs = dialogs;
        _settingsService = settings;
        _protector = protector;
        _sessions = sessions;
        _services = services;
    }

    /// <summary>Open RDP session tabs, surfaced for the main window's TabControl.</summary>
    public SessionsViewModel Sessions => _sessions;

    /// <summary>Top-level nodes shown in the tree.</summary>
    public ObservableCollection<TreeNodeViewModel> RootNodes { get; } = new();

    [ObservableProperty]
    private string? _connectionsFilePath;

    /// <summary>The tree node currently selected in the TreeView.</summary>
    [ObservableProperty]
    private TreeNodeViewModel? _selectedNode;

    /// <summary>Loads settings and the connection document, then builds the tree. Called at startup.</summary>
    public async Task InitializeAsync()
    {
        var settings = _settingsService.Load();
        _connectionsPath = settings.ConnectionsFilePath ?? string.Empty;
        ConnectionsFilePath = _connectionsPath;
        _document = await _store.LoadAsync(_connectionsPath);

        // If the document is protected by a master password, unlock it before showing anything.
        // Cancelling the unlock closes the app (saved passwords must not be accessed unlocked).
        if (_document.Security is not null && !TryUnlock(_document.Security))
        {
            Application.Current.Shutdown();
            return;
        }

        RebuildTree();
    }

    /// <summary>Prompts for the master password until it unlocks the vault or the user cancels.</summary>
    private bool TryUnlock(VaultHeader header)
    {
        if (_protector.IsUnlockedFor(header))
        {
            return true;
        }

        var message = "Enter your master password to unlock saved connection passwords.";
        while (true)
        {
            var password = _dialogs.PromptMasterPassword("Unlock RemoteHub", message);
            if (password is null)
            {
                return false; // cancelled
            }

            if (_protector.TryUnlock(header, password))
            {
                return true;
            }

            message = "Incorrect master password. Please try again.";
        }
    }

    // --- Tree construction -------------------------------------------------

    /// <summary>
    /// Rebuilds the tree from the document: a "Pinned" group of pinned connections on top, then the
    /// normal roots. Every level is sorted (folders first, then connections, each alphabetical) and
    /// pinned connections are hidden from their normal position. Folder expansion is preserved across
    /// the rebuild.
    /// </summary>
    private void RebuildTree(Guid? alsoExpand = null)
    {
        var expanded = CollectExpandedFolderIds();
        if (alsoExpand is { } id)
        {
            expanded.Add(id);
        }

        RootNodes.Clear();

        // Pinned group (synthetic — not part of the document).
        var pinned = EnumerateConnections(_document.Roots).Where(c => c.IsPinned).ToList();
        if (pinned.Count > 0)
        {
            var container = new TreeNodeViewModel(new FolderNode { Name = "Pinned" })
            {
                IsPinnedContainer = true,
                IsExpanded = true,
            };
            foreach (var connection in pinned.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                container.Children.Add(new TreeNodeViewModel(connection));
            }

            RootNodes.Add(container);
        }

        foreach (var node in Sort(_document.Roots))
        {
            if (node is RdpConnection { IsPinned: true })
            {
                continue; // shown in the Pinned group instead
            }

            RootNodes.Add(BuildNode(node));
        }

        RestoreExpandedFolders(expanded);
    }

    private static TreeNodeViewModel BuildNode(ConnectionNode model)
    {
        var vm = new TreeNodeViewModel(model);
        if (model is FolderNode folder)
        {
            foreach (var child in Sort(folder.Children))
            {
                if (child is RdpConnection { IsPinned: true })
                {
                    continue; // pinned connections live in the top-level Pinned group
                }

                vm.Children.Add(BuildNode(child));
            }
        }

        return vm;
    }

    /// <summary>Folders first, then connections; each group alphabetical (case-insensitive).</summary>
    private static IEnumerable<ConnectionNode> Sort(IEnumerable<ConnectionNode> nodes) =>
        nodes.OrderBy(n => n is FolderNode ? 0 : 1)
             .ThenBy(n => n.Name, StringComparer.CurrentCultureIgnoreCase);

    private HashSet<Guid> CollectExpandedFolderIds()
    {
        var set = new HashSet<Guid>();
        void Walk(IEnumerable<TreeNodeViewModel> vms)
        {
            foreach (var vm in vms)
            {
                if (vm is { IsFolder: true, IsExpanded: true, IsPinnedContainer: false })
                {
                    set.Add(vm.Node.Id);
                }

                Walk(vm.Children);
            }
        }

        Walk(RootNodes);
        return set;
    }

    private void RestoreExpandedFolders(HashSet<Guid> ids)
    {
        void Walk(IEnumerable<TreeNodeViewModel> vms)
        {
            foreach (var vm in vms)
            {
                if (vm is { IsFolder: true, IsPinnedContainer: false } && ids.Contains(vm.Node.Id))
                {
                    vm.IsExpanded = true;
                }

                Walk(vm.Children);
            }
        }

        Walk(RootNodes);
    }

    private static IEnumerable<RdpConnection> EnumerateConnections(IEnumerable<ConnectionNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node is RdpConnection connection)
            {
                yield return connection;
            }
            else if (node is FolderNode folder)
            {
                foreach (var child in EnumerateConnections(folder.Children))
                {
                    yield return child;
                }
            }
        }
    }

    private Task SaveDocumentAsync() => _store.SaveAsync(_connectionsPath, _document);

    // --- Commands ----------------------------------------------------------

    [RelayCommand]
    private async Task NewFolderAsync(TreeNodeViewModel? node)
    {
        var name = _dialogs.Prompt("New Folder", "Folder name:", "New Folder");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        await AddChildAsync(new FolderNode { Name = name }, node ?? SelectedNode);
    }

    [RelayCommand]
    private async Task NewConnectionAsync(TreeNodeViewModel? node)
    {
        var connection = new RdpConnection { Name = "New Connection" };
        var editor = _services.GetRequiredService<ConnectionEditorViewModel>();
        editor.Load(connection);
        if (_dialogs.EditConnection(editor) != true)
        {
            return;
        }

        editor.ApplyTo(connection);
        await AddChildAsync(connection, node ?? SelectedNode);
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var vm = _services.GetRequiredService<SettingsViewModel>();
        vm.Load();
        _dialogs.ShowSettings(vm);

        // Master-password (re-encryption) and path changes persist immediately inside the dialog;
        // reload so the in-memory document and tree reflect them.
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var settings = _settingsService.Load();
        _connectionsPath = settings.ConnectionsFilePath ?? string.Empty;
        ConnectionsFilePath = _connectionsPath;
        _document = await _store.LoadAsync(_connectionsPath);
        RebuildTree();
    }

    [RelayCommand]
    private void Connect(TreeNodeViewModel? node)
    {
        var target = node ?? SelectedNode;
        if (target?.Node is RdpConnection connection)
        {
            _sessions.OpenSession(connection);
        }
    }

    [RelayCommand]
    private async Task EditNodeAsync(TreeNodeViewModel? node)
    {
        var target = node ?? SelectedNode;
        if (target is null)
        {
            return;
        }

        if (target.Node is RdpConnection connection)
        {
            var editor = _services.GetRequiredService<ConnectionEditorViewModel>();
            editor.Load(connection);
            if (_dialogs.EditConnection(editor) == true)
            {
                editor.ApplyTo(connection);
                RebuildTree();   // name may have changed -> re-sort
                await SaveDocumentAsync();
            }
        }
        else
        {
            await RenameNodeAsync(target);
        }
    }

    [RelayCommand]
    private async Task RenameNodeAsync(TreeNodeViewModel? node)
    {
        var target = node ?? SelectedNode;
        if (target is null || target.IsPinnedContainer)
        {
            return;
        }

        var name = _dialogs.Prompt("Rename", "Name:", target.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        target.Node.Name = name;
        RebuildTree();           // keep the level alphabetically sorted
        await SaveDocumentAsync();
    }

    [RelayCommand]
    private async Task PinNodeAsync(TreeNodeViewModel? node)
    {
        var target = node ?? SelectedNode;
        if (target?.Node is RdpConnection connection)
        {
            connection.IsPinned = true;
            RebuildTree();
            await SaveDocumentAsync();
        }
    }

    [RelayCommand]
    private async Task UnpinNodeAsync(TreeNodeViewModel? node)
    {
        var target = node ?? SelectedNode;
        if (target?.Node is RdpConnection connection)
        {
            connection.IsPinned = false;
            RebuildTree();
            await SaveDocumentAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteNodeAsync(TreeNodeViewModel? node)
    {
        var target = node ?? SelectedNode;
        if (target is null || target.IsPinnedContainer)
        {
            return;
        }

        if (!_dialogs.Confirm("Delete", $"Delete '{target.Name}'?"))
        {
            return;
        }

        if (RemoveFromModel(_document.Roots, target.Node))
        {
            if (ReferenceEquals(SelectedNode, target))
            {
                SelectedNode = null;
            }

            RebuildTree();
            await SaveDocumentAsync();
        }
    }

    // --- Tree mutation helpers --------------------------------------------

    private async Task AddChildAsync(ConnectionNode model, TreeNodeViewModel? target)
    {
        // The pinned group is not a real container: add into the pinned connection's real parent.
        var parent = target is { IsPinnedContainer: false, Node: FolderNode folder } ? folder : null;
        if (parent is not null)
        {
            parent.Children.Add(model);
            RebuildTree(alsoExpand: parent.Id);
        }
        else
        {
            _document.Roots.Add(model);
            RebuildTree();
        }

        await SaveDocumentAsync();
    }

    /// <summary>Removes <paramref name="target"/> from the document tree by reference.</summary>
    private static bool RemoveFromModel(List<ConnectionNode> nodes, ConnectionNode target)
    {
        if (nodes.Remove(target))
        {
            return true;
        }

        foreach (var node in nodes)
        {
            if (node is FolderNode folder && RemoveFromModel(folder.Children, target))
            {
                return true;
            }
        }

        return false;
    }
}
