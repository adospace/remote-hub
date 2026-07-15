using CommunityToolkit.Mvvm.ComponentModel;
using RemoteHub.Core.Models;

namespace RemoteHub.ViewModels;

/// <summary>
/// Edits an <see cref="RdpConnection"/>'s fields and display settings. No password field.
/// </summary>
public sealed partial class ConnectionEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _host = string.Empty;

    [ObservableProperty]
    private int _port = 3389;

    [ObservableProperty]
    private string? _username;

    [ObservableProperty]
    private string? _domain;

    [ObservableProperty]
    private string? _description;

    /// <summary>
    /// Loads the editor from an existing connection. Called before showing the dialog.
    /// </summary>
    public void Load(RdpConnection connection)
    {
        // TODO(Implement): copy all fields + display settings into editable properties.
        throw new NotImplementedException();
    }

    /// <summary>
    /// Writes edited values back into the connection model.
    /// </summary>
    public void ApplyTo(RdpConnection connection)
    {
        // TODO(Implement): copy editable properties back into the model.
        throw new NotImplementedException();
    }
}
