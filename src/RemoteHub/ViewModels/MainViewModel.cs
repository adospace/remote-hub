using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using RemoteHub.Core.Import;
using RemoteHub.Core.Models;
using RemoteHub.Core.Security;
using RemoteHub.Core.Services;
using RemoteHub.Services;
using Application = System.Windows.Application;

namespace RemoteHub.ViewModels;

/// <summary>
/// Root view-model for the main window. Owns the loaded document, the tree, and the top-level
/// commands (import, add/edit/delete nodes, save, settings, connect).
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IConnectionStore _store;
    private readonly IConnectionImporter _importer;
    private readonly IDialogService _dialogs;
    private readonly ISettingsService _settingsService;
    private readonly ICredentialProtector _protector;
    private readonly SessionsViewModel _sessions;
    private readonly IServiceProvider _services;

    private ConnectionDocument _document = new();
    private string _connectionsPath = string.Empty;

    public MainViewModel(
        IConnectionStore store,
        IConnectionImporter importer,
        IDialogService dialogs,
        ISettingsService settings,
        ICredentialProtector protector,
        SessionsViewModel sessions,
        IServiceProvider services)
    {
        _store = store;
        _importer = importer;
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

    private void RebuildTree()
    {
        RootNodes.Clear();
        foreach (var node in _document.Roots)
        {
            RootNodes.Add(BuildNode(node));
        }
    }

    private static TreeNodeViewModel BuildNode(ConnectionNode model)
    {
        var vm = new TreeNodeViewModel(model);
        if (model is FolderNode folder)
        {
            foreach (var child in folder.Children)
            {
                vm.Children.Add(BuildNode(child));
            }
        }

        return vm;
    }

    private Task SaveDocumentAsync() => _store.SaveAsync(_connectionsPath, _document);

    // --- Commands ----------------------------------------------------------

    [RelayCommand]
    private async Task ImportRdmAsync()
    {
        var path = _dialogs.PickImportFile();
        if (path is null)
        {
            return;
        }

        ConnectionDocument imported;
        try
        {
            imported = _importer.ImportFile(path);
        }
        catch (Exception ex)
        {
            _dialogs.Confirm("Import failed", $"Could not import the selected file.\n\n{ex.Message}");
            return;
        }

        // Merge imported roots into the current document (append, keep existing).
        foreach (var root in imported.Roots)
        {
            _document.Roots.Add(root);
            RootNodes.Add(BuildNode(root));
        }

        await SaveDocumentAsync();
    }

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
    private Task SaveAsync() => SaveDocumentAsync();

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
                target.Name = connection.Name;
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
        if (target is null)
        {
            return;
        }

        var name = _dialogs.Prompt("Rename", "Name:", target.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        target.Name = name;      // OnNameChanged keeps the model in sync
        await SaveDocumentAsync();
    }

    [RelayCommand]
    private async Task DeleteNodeAsync(TreeNodeViewModel? node)
    {
        var target = node ?? SelectedNode;
        if (target is null)
        {
            return;
        }

        if (!_dialogs.Confirm("Delete", $"Delete '{target.Name}'?"))
        {
            return;
        }

        if (TryRemove(RootNodes, _document.Roots, target))
        {
            if (ReferenceEquals(SelectedNode, target))
            {
                SelectedNode = null;
            }

            await SaveDocumentAsync();
        }
    }

    // --- Tree mutation helpers --------------------------------------------

    private async Task AddChildAsync(ConnectionNode model, TreeNodeViewModel? target)
    {
        var childVm = BuildNode(model);
        if (target?.Node is FolderNode folder)
        {
            folder.Children.Add(model);
            target.Children.Add(childVm);
            target.IsExpanded = true;
        }
        else
        {
            _document.Roots.Add(model);
            RootNodes.Add(childVm);
        }

        await SaveDocumentAsync();
    }

    /// <summary>Removes <paramref name="target"/> from the matching VM and model collections.</summary>
    private static bool TryRemove(
        ObservableCollection<TreeNodeViewModel> vmList,
        List<ConnectionNode> modelList,
        TreeNodeViewModel target)
    {
        if (vmList.Contains(target))
        {
            vmList.Remove(target);
            modelList.Remove(target.Node);
            return true;
        }

        foreach (var vm in vmList)
        {
            if (vm.Node is FolderNode folder && TryRemove(vm.Children, folder.Children, target))
            {
                return true;
            }
        }

        return false;
    }
}
