namespace RemoteHub.Core.Models;

/// <summary>
/// Root persisted document holding the top-level nodes of the connection tree.
/// </summary>
public sealed class ConnectionDocument
{
    public int Version { get; set; } = 2;

    /// <summary>
    /// Vault metadata (KDF salt + verifier) when a master password protects stored passwords.
    /// Null when no master password has been set — in that case no connection stores a password.
    /// </summary>
    public VaultHeader? Security { get; set; }

    public List<ConnectionNode> Roots { get; set; } = new();
}
