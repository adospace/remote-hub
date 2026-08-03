using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using RemoteHub.Core.Models;
using RemoteHub.Core.Security;
using RemoteHub.Core.Services;
using RemoteHub.Diagnostics;
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
    private readonly UpdateViewModel _update;
    private readonly IServiceProvider _services;

    private ConnectionDocument _document = new();
    private string _connectionsPath = string.Empty;

    // Expansion the user had chosen before a search forced every surviving folder open, so clearing
    // the search box gives them back the tree they were looking at.
    private HashSet<Guid>? _expandedBeforeFilter;

    public MainViewModel(
        IConnectionStore store,
        IDialogService dialogs,
        ISettingsService settings,
        ICredentialProtector protector,
        SessionsViewModel sessions,
        UpdateViewModel update,
        IServiceProvider services)
    {
        _store = store;
        _dialogs = dialogs;
        _settingsService = settings;
        _protector = protector;
        _sessions = sessions;
        _update = update;
        _services = services;
        _sessions.EditSessionRequested += OnEditSessionRequested;
        _sessions.Sessions.CollectionChanged += OnSessionsCollectionChanged;
    }

    /// <summary>Open RDP session tabs, surfaced for the main window's TabControl.</summary>
    public SessionsViewModel Sessions => _sessions;

    /// <summary>Backs the update banner's DataContext.</summary>
    public UpdateViewModel Update => _update;

    /// <summary>Top-level nodes shown in the tree.</summary>
    public ObservableCollection<TreeNodeViewModel> RootNodes { get; } = new();

    [ObservableProperty]
    private string? _connectionsFilePath;

    /// <summary>The tree node currently selected in the TreeView.</summary>
    [ObservableProperty]
    private TreeNodeViewModel? _selectedNode;

    /// <summary>
    /// Text typed into the search box above the tree. Empty shows the whole tree; anything else
    /// filters it (see <see cref="RebuildTree"/>).
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>True when a search is active and nothing matched — drives the empty-state text.</summary>
    [ObservableProperty]
    private bool _noSearchResults;

    partial void OnSearchTextChanged(string value) => RebuildTree();

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

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

        // Fire-and-forget: the check self-delays 5s and swallows its own failures, so the banner
        // never blocks or breaks startup.
        _ = _update.CheckForUpdatesOnStartupAsync();
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
    /// the rebuild. When <see cref="SearchText"/> is non-empty the tree is filtered to matching nodes
    /// and every surviving folder is expanded so the matches are visible without any clicking.
    /// </summary>
    private void RebuildTree(Guid? alsoExpand = null)
    {
        var filter = SearchText.Trim();

        // A filtered tree force-expands its folders, so its expansion state says nothing about what
        // the user wanted: capture that once when the search starts and hand it back when it ends.
        HashSet<Guid> expanded;
        if (filter.Length > 0)
        {
            expanded = _expandedBeforeFilter ??= CollectExpandedFolderIds();
        }
        else
        {
            expanded = _expandedBeforeFilter ?? CollectExpandedFolderIds();
            _expandedBeforeFilter = null;
        }

        if (alsoExpand is { } id)
        {
            expanded.Add(id);
        }

        RootNodes.Clear();

        // Pinned group (synthetic — not part of the document).
        var pinned = EnumerateConnections(_document.Roots)
            .Where(c => c.IsPinned && Matches(c, filter))
            .ToList();
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

            if (BuildNode(node, filter) is { } vm)
            {
                RootNodes.Add(vm);
            }
        }

        RestoreExpandedFolders(expanded);
        RefreshConnectedStates();   // the nodes are new objects; re-apply the live-session highlight
        NoSearchResults = filter.Length > 0 && RootNodes.Count == 0;
    }

    // --- Live-session highlight -------------------------------------------

    // Tabs come and go, and each one's status is driven by the RDP control's polling timer, so the
    // tree listens for both rather than sampling at rebuild time only.
    private void OnSessionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var session in e.OldItems?.OfType<SessionViewModel>() ?? [])
        {
            session.PropertyChanged -= OnSessionPropertyChanged;
        }

        foreach (var session in e.NewItems?.OfType<SessionViewModel>() ?? [])
        {
            session.PropertyChanged += OnSessionPropertyChanged;
        }

        RefreshConnectedStates();
    }

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SessionViewModel.Status) or nameof(SessionViewModel.IsConnected))
        {
            RefreshConnectedStates();
        }
    }

    /// <summary>
    /// Flags the tree nodes whose connection currently has a connected session. Matched by
    /// <see cref="ConnectionNode.Id"/>, so a pinned connection lights up in the Pinned group too.
    /// </summary>
    private void RefreshConnectedStates()
    {
        var live = _sessions.Sessions.Where(s => s.IsConnected)
                                     .Select(s => s.Connection.Id)
                                     .ToHashSet();

        void Walk(IEnumerable<TreeNodeViewModel> vms)
        {
            foreach (var vm in vms)
            {
                vm.IsConnected = vm.Node is RdpConnection connection && live.Contains(connection.Id);
                Walk(vm.Children);
            }
        }

        Walk(RootNodes);
    }

    /// <summary>
    /// Builds the view-model subtree for <paramref name="model"/>, or null when nothing in it
    /// survives <paramref name="filter"/>. A folder whose own name matches brings its whole subtree
    /// along; otherwise it is kept only for the sake of matching descendants.
    /// </summary>
    private static TreeNodeViewModel? BuildNode(ConnectionNode model, string filter)
    {
        var selfMatches = Matches(model, filter);
        if (model is not FolderNode folder)
        {
            return selfMatches ? new TreeNodeViewModel(model) : null;
        }

        var vm = new TreeNodeViewModel(model);
        foreach (var child in Sort(folder.Children))
        {
            if (child is RdpConnection { IsPinned: true })
            {
                continue; // pinned connections live in the top-level Pinned group
            }

            if (BuildNode(child, selfMatches ? string.Empty : filter) is { } childVm)
            {
                vm.Children.Add(childVm);
            }
        }

        if (!selfMatches && vm.Children.Count == 0)
        {
            return null;
        }

        if (filter.Length > 0)
        {
            vm.IsExpanded = true; // a hit buried three folders deep is no use if it stays hidden
        }

        return vm;
    }

    /// <summary>
    /// Case-insensitive substring match on the node name, plus a connection's host — searching for
    /// the machine you are about to reach is as natural as searching for what it was named. An empty
    /// filter matches everything.
    /// </summary>
    private static bool Matches(ConnectionNode node, string filter) =>
        filter.Length == 0 ||
        node.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
        (node is RdpConnection connection &&
         connection.Host.Contains(filter, StringComparison.CurrentCultureIgnoreCase));

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
            await EditConnectionAsync(connection);
        }
        else
        {
            await RenameNodeAsync(target);
        }
    }

    /// <summary>
    /// Opens the connection editor and persists the result. Shared by the tree's edit command and
    /// the session toolbar's edit button, which reach the same connection from different places.
    /// </summary>
    private async Task EditConnectionAsync(RdpConnection connection)
    {
        var editor = _services.GetRequiredService<ConnectionEditorViewModel>();
        editor.Load(connection);
        if (_dialogs.EditConnection(editor) != true)
        {
            return;
        }

        editor.ApplyTo(connection);

        // Tabs cache the title at construction, so a rename would otherwise leave them stale.
        foreach (var session in _sessions.Sessions.Where(s => ReferenceEquals(s.Connection, connection)))
        {
            session.RefreshTitle();
        }

        RebuildTree();   // name may have changed -> re-sort
        await SaveDocumentAsync();
    }

    // Fired by the session toolbar's edit button. async void because it is an event handler; it must
    // not throw, so failures are logged rather than surfaced through the global crash handler.
    private async void OnEditSessionRequested(object? sender, SessionViewModel session)
    {
        try
        {
            await EditConnectionAsync(session.Connection);
        }
        catch (Exception ex)
        {
            Log.Error("Editing a connection from the session toolbar failed.", ex);
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
