namespace RemoteHub.Core.Models;

/// <summary>
/// A single RDP connection. Contains no password or credential material by design —
/// the RDP control prompts for credentials (CredSSP) at connect time.
/// </summary>
public sealed class RdpConnection : ConnectionNode
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 3389;

    public string? Username { get; set; }

    public string? Domain { get; set; }

    public string? Description { get; set; }

    public RdpDisplaySettings Display { get; set; } = new();
}
