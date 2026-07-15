using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RemoteHub.Core.Import;
using RemoteHub.Core.Models;
using RemoteHub.Core.Services;
using RemoteHub.Services;

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
    private readonly ISettingsService _settings;
    private readonly SessionsViewModel _sessions;

    private ConnectionDocument _document = new();

    public MainViewModel(
        IConnectionStore store,
        IConnectionImporter importer,
        IDialogService dialogs,
        ISettingsService settings,
        SessionsViewModel sessions)
    {
        _store = store;
        _importer = importer;
        _dialogs = dialogs;
        _settings = settings;
        _sessions = sessions;
    }

    /// <summary>Open RDP session tabs, surfaced for the main window's TabControl.</summary>
    public SessionsViewModel Sessions => _sessions;

    /// <summary>Top-level nodes shown in the tree.</summary>
    public ObservableCollection<TreeNodeViewModel> RootNodes { get; } = new();

    [ObservableProperty]
    private string? _connectionsFilePath;

    /// <summary>Loads the connection document and populates the tree. Called at startup.</summary>
    public Task InitializeAsync()
    {
        // TODO(Implement): load settings + document, build RootNodes.
        throw new NotImplementedException();
    }

    [RelayCommand]
    private void ImportRdm() => throw new NotImplementedException();

    [RelayCommand]
    private void NewFolder() => throw new NotImplementedException();

    [RelayCommand]
    private void NewConnection() => throw new NotImplementedException();

    [RelayCommand]
    private void Save() => throw new NotImplementedException();

    [RelayCommand]
    private void OpenSettings() => throw new NotImplementedException();

    [RelayCommand]
    private void Connect(TreeNodeViewModel? node) => throw new NotImplementedException();

    [RelayCommand]
    private void EditNode(TreeNodeViewModel? node) => throw new NotImplementedException();

    [RelayCommand]
    private void DeleteNode(TreeNodeViewModel? node) => throw new NotImplementedException();

    [RelayCommand]
    private void RenameNode(TreeNodeViewModel? node) => throw new NotImplementedException();
}
