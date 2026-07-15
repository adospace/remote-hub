using System.Security.Cryptography;
using System.Text;
using RemoteHub.Core.Models;

namespace RemoteHub.Core.Security;

/// <summary>
/// Default <see cref="ICredentialProtector"/>: PBKDF2-SHA256 key derivation + AES-256-GCM
/// authenticated encryption. Token layout is <c>nonce(12) | tag(16) | ciphertext</c>, base64-encoded.
/// The master password and derived key are never written to disk; the key lives only in memory
/// while unlocked and is zeroed on <see cref="Lock"/>.
/// </summary>
public sealed class MasterKeyService : ICredentialProtector
{
    private const int SaltSize = 16;
    private const int KeySize = 32;   // AES-256
    private const int NonceSize = 12; // AES-GCM standard nonce
    private const int TagSize = 16;   // AES-GCM tag
    private const int DefaultIterations = 600_000;
    private const string KdfName = "PBKDF2-SHA256";

    // A fixed, non-secret constant. Encrypting it under the derived key yields the vault verifier;
    // being able to decrypt it back proves the master password is correct.
    private static readonly byte[] VerifierToken = Encoding.UTF8.GetBytes("RemoteHub.Vault.Verifier.v1");

    private byte[]? _key;
    private VaultHeader? _header;

    public bool IsUnlocked => _key is not null;

    public bool HasVault => _header is not null;

    public VaultHeader? CurrentHeader => _header;

    public VaultHeader CreateVault(string masterPassword)
    {
        ArgumentException.ThrowIfNullOrEmpty(masterPassword);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = DeriveKey(masterPassword, salt, DefaultIterations);
        var verifier = EncryptRaw(key, VerifierToken);

        var header = new VaultHeader
        {
            Kdf = KdfName,
            Iterations = DefaultIterations,
            Salt = Convert.ToBase64String(salt),
            Verifier = Convert.ToBase64String(verifier),
        };

        SwapKey(key, header);
        return header;
    }

    public bool TryUnlock(VaultHeader header, string masterPassword)
    {
        ArgumentNullException.ThrowIfNull(header);
        if (string.IsNullOrEmpty(masterPassword))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(header.Salt);
            var key = DeriveKey(masterPassword, salt, header.Iterations);
            var verifier = Convert.FromBase64String(header.Verifier);

            if (!TryDecryptRaw(key, verifier, out var plain) || !plain.AsSpan().SequenceEqual(VerifierToken))
            {
                CryptographicOperations.ZeroMemory(key);
                return false;
            }

            SwapKey(key, header);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public bool IsUnlockedFor(VaultHeader header) =>
        IsUnlocked && _header is not null && header is not null &&
        string.Equals(_header.Salt, header.Salt, StringComparison.Ordinal);

    public void Lock()
    {
        if (_key is not null)
        {
            CryptographicOperations.ZeroMemory(_key);
            _key = null;
        }

        _header = null;
    }

    public string Encrypt(string plaintext)
    {
        if (_key is null)
        {
            throw new InvalidOperationException("The vault is locked; cannot encrypt.");
        }

        ArgumentNullException.ThrowIfNull(plaintext);
        var data = EncryptRaw(_key, Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToBase64String(data);
    }

    public bool TryDecrypt(string token, out string plaintext)
    {
        plaintext = string.Empty;
        if (_key is null || string.IsNullOrEmpty(token))
        {
            return false;
        }

        try
        {
            var data = Convert.FromBase64String(token);
            if (!TryDecryptRaw(_key, data, out var bytes))
            {
                return false;
            }

            plaintext = Encoding.UTF8.GetString(bytes);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private void SwapKey(byte[] key, VaultHeader header)
    {
        if (_key is not null)
        {
            CryptographicOperations.ZeroMemory(_key);
        }

        _key = key;
        _header = header;
    }

    private static byte[] DeriveKey(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, KeySize);

    // Output layout: nonce(12) | tag(16) | ciphertext
    private static byte[] EncryptRaw(byte[] key, byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var gcm = new AesGcm(key, TagSize);
        gcm.Encrypt(nonce, plaintext, cipher, tag);

        var output = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, output, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, output, NonceSize + TagSize, cipher.Length);
        return output;
    }

    private static bool TryDecryptRaw(byte[] key, byte[] data, out byte[] plaintext)
    {
        plaintext = Array.Empty<byte>();
        if (data.Length < NonceSize + TagSize)
        {
            return false;
        }

        try
        {
            var nonce = data.AsSpan(0, NonceSize);
            var tag = data.AsSpan(NonceSize, TagSize);
            var cipher = data.AsSpan(NonceSize + TagSize);
            var result = new byte[cipher.Length];

            using var gcm = new AesGcm(key, TagSize);
            gcm.Decrypt(nonce, cipher, tag, result);

            plaintext = result;
            return true;
        }
        catch (CryptographicException)
        {
            // Wrong key or tampered data — authentication tag mismatch.
            return false;
        }
    }
}
