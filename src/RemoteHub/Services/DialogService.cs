using RemoteHub.ViewModels;

namespace RemoteHub.Services;

/// <summary>
/// Default WPF implementation of <see cref="IDialogService"/> using windows and common dialogs.
/// </summary>
public sealed class DialogService : IDialogService
{
    // TODO(Implement): show ConnectionEditorDialog / SettingsDialog modally; wire file dialogs.
    public bool? EditConnection(ConnectionEditorViewModel vm) => throw new NotImplementedException();

    public bool? ShowSettings(SettingsViewModel vm) => throw new NotImplementedException();

    public string? PickImportFile() => throw new NotImplementedException();

    public string? PickConnectionsFile(bool save) => throw new NotImplementedException();

    public bool Confirm(string title, string message) => throw new NotImplementedException();
}
