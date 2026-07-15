using RemoteHub.ViewModels;

namespace RemoteHub.Services;

/// <summary>
/// Abstracts modal dialogs and file pickers so view-models stay testable and view-agnostic.
/// </summary>
public interface IDialogService
{
    /// <summary>Shows the connection editor modally. Returns true if the user accepted.</summary>
    bool? EditConnection(ConnectionEditorViewModel vm);

    /// <summary>Shows the settings dialog modally. Returns true if the user accepted.</summary>
    bool? ShowSettings(SettingsViewModel vm);

    /// <summary>Prompts for a file to import (RDM XML). Returns null if cancelled.</summary>
    string? PickImportFile();

    /// <summary>Prompts for a connections file path. Returns null if cancelled.</summary>
    string? PickConnectionsFile(bool save);

    /// <summary>Shows a yes/no confirmation dialog.</summary>
    bool Confirm(string title, string message);

    /// <summary>Prompts for a single line of text. Returns null if cancelled.</summary>
    string? Prompt(string title, string label, string initialValue);
}
