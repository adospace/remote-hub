namespace RemoteHub.Core.Models;

/// <summary>
/// Public, non-secret metadata that lets the app re-derive the master key and verify a supplied
/// master password. Stored alongside the connections in the JSON document. Contains no plaintext
/// secret and no material from which the master password can be recovered — only a KDF salt and an
/// authenticated "verifier" token (a known constant encrypted under the derived key).
/// </summary>
public sealed class VaultHeader
{
    /// <summary>Key-derivation function identifier (informational / forward-compatibility).</summary>
    public string Kdf { get; set; } = "PBKDF2-SHA256";

    /// <summary>PBKDF2 iteration count used to derive the key.</summary>
    public int Iterations { get; set; }

    /// <summary>Base64 KDF salt.</summary>
    public string Salt { get; set; } = string.Empty;

    /// <summary>
    /// Base64 AES-GCM encryption of a fixed known token under the derived key. Decrypting it
    /// successfully proves the supplied master password is correct; it reveals nothing otherwise.
    /// </summary>
    public string Verifier { get; set; } = string.Empty;
}
