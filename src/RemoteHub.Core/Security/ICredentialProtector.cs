using RemoteHub.Core.Models;

namespace RemoteHub.Core.Security;

/// <summary>
/// Protects connection passwords with a master-password-derived key. Holds the derived key in
/// memory only while unlocked; never persists or exposes the master password or the key. Passwords
/// are encrypted with authenticated encryption (AES-GCM) and stored as opaque base64 tokens.
/// </summary>
public interface ICredentialProtector
{
    /// <summary>True when a master key is currently held in memory (passwords can be read/written).</summary>
    bool IsUnlocked { get; }

    /// <summary>True when a vault (master password) has been established this session.</summary>
    bool HasVault { get; }

    /// <summary>The header of the currently held vault, if any.</summary>
    VaultHeader? CurrentHeader { get; }

    /// <summary>
    /// Creates a brand-new vault for the given master password: fresh salt, derived key held in
    /// memory (leaving the protector unlocked), and a verifier. Returns the header to persist.
    /// Also used to re-key during a master-password change (swaps the in-memory key).
    /// </summary>
    VaultHeader CreateVault(string masterPassword);

    /// <summary>
    /// Attempts to unlock the given vault with the master password. On success the derived key is
    /// held in memory and the method returns true; on failure nothing changes and it returns false.
    /// </summary>
    bool TryUnlock(VaultHeader header, string masterPassword);

    /// <summary>True when unlocked AND the held key belongs to the supplied header (same salt).</summary>
    bool IsUnlockedFor(VaultHeader header);

    /// <summary>Clears the in-memory key (and vault state).</summary>
    void Lock();

    /// <summary>Encrypts a plaintext secret into an opaque base64 token. Requires <see cref="IsUnlocked"/>.</summary>
    string Encrypt(string plaintext);

    /// <summary>Decrypts a token produced by <see cref="Encrypt"/>. Returns false if locked, tampered, or wrong key.</summary>
    bool TryDecrypt(string token, out string plaintext);
}
