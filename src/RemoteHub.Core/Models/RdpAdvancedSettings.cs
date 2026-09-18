namespace RemoteHub.Core.Models;

/// <summary>
/// What to do when the server's identity cannot be verified. Values match the RDP control's
/// <c>AuthenticationLevel</c>.
/// </summary>
public enum ServerAuthenticationMode
{
    ConnectWithoutWarning = 0,
    DoNotConnect = 1,
    Warn = 2,
}

/// <summary>
/// Security and session-start preferences for an <see cref="RdpConnection"/>.
/// </summary>
public sealed class RdpAdvancedSettings
{
    public ServerAuthenticationMode ServerAuthentication { get; set; } = ServerAuthenticationMode.Warn;

    /// <summary>Authenticates with CredSSP before the session is created.</summary>
    public bool NetworkLevelAuthentication { get; set; } = true;

    /// <summary>Connects to the server's administrative session (mstsc <c>/admin</c>).</summary>
    public bool AdminSession { get; set; }

    /// <summary>A program to run in place of the desktop shell, or null for the normal desktop.</summary>
    public string? StartProgram { get; set; }

    /// <summary>The working directory for <see cref="StartProgram"/>.</summary>
    public string? WorkingDirectory { get; set; }
}
