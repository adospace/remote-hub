namespace RemoteHub.Core.Models;

/// <summary>
/// A single RDP connection. Any saved password is stored only as <see cref="EncryptedPassword"/>,
/// an opaque AES-GCM token protected by the master password — never in plaintext. When no password
/// is stored, the RDP control prompts for credentials (CredSSP) at connect time.
/// </summary>
public sealed class RdpConnection : ConnectionNode
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 3389;

    public string? Username { get; set; }

    public string? Domain { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// When true, the connection is surfaced in a "Pinned" group at the top of the tree instead of
    /// its normal folder position. Purely a presentation flag; the node keeps its place in the document.
    /// </summary>
    public bool IsPinned { get; set; }

    /// <summary>
    /// The saved password encrypted under the master key (opaque base64 token), or null if no
    /// password is stored. Never contains plaintext. Produced/read via <c>ICredentialProtector</c>.
    /// </summary>
    public string? EncryptedPassword { get; set; }

    public RdpDisplaySettings Display { get; set; } = new();
}
