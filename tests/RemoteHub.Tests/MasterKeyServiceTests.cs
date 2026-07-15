using RemoteHub.Core.Models;
using RemoteHub.Core.Security;
using Xunit;

namespace RemoteHub.Tests;

public class MasterKeyServiceTests
{
    [Fact]
    public void CreateVault_LeavesProtectorUnlocked()
    {
        var sut = new MasterKeyService();

        var header = sut.CreateVault("hunter2");

        Assert.True(sut.IsUnlocked);
        Assert.True(sut.HasVault);
        Assert.Equal("PBKDF2-SHA256", header.Kdf);
        Assert.True(header.Iterations >= 100_000);
        Assert.False(string.IsNullOrEmpty(header.Salt));
        Assert.False(string.IsNullOrEmpty(header.Verifier));
    }

    [Fact]
    public void TryUnlock_OnFreshService_WithCorrectPassword_Succeeds()
    {
        // Simulate app restart: one service creates the vault + header; a new service unlocks it.
        var header = new MasterKeyService().CreateVault("correct horse");

        var restarted = new MasterKeyService();
        var ok = restarted.TryUnlock(header, "correct horse");

        Assert.True(ok);
        Assert.True(restarted.IsUnlocked);
        Assert.True(restarted.IsUnlockedFor(header));
    }

    [Fact]
    public void TryUnlock_WithWrongPassword_Fails_AndStaysLocked()
    {
        var header = new MasterKeyService().CreateVault("s3cret");

        var restarted = new MasterKeyService();
        var ok = restarted.TryUnlock(header, "wrong");

        Assert.False(ok);
        Assert.False(restarted.IsUnlocked);
    }

    [Fact]
    public void EncryptDecrypt_RoundTrips()
    {
        var sut = new MasterKeyService();
        sut.CreateVault("master");

        var token = sut.Encrypt("P@ssw0rd!");

        Assert.True(sut.TryDecrypt(token, out var plain));
        Assert.Equal("P@ssw0rd!", plain);
    }

    [Fact]
    public void Encrypt_SameInput_ProducesDifferentTokens()
    {
        var sut = new MasterKeyService();
        sut.CreateVault("master");

        Assert.NotEqual(sut.Encrypt("same"), sut.Encrypt("same")); // random nonce per encryption
    }

    [Fact]
    public void Token_FromOneVault_CannotBeDecrypted_ByAnother()
    {
        var a = new MasterKeyService();
        a.CreateVault("password-a");
        var token = a.Encrypt("top secret");

        var b = new MasterKeyService();
        b.CreateVault("password-b");

        Assert.False(b.TryDecrypt(token, out _));
    }

    [Fact]
    public void UnlockedService_CanDecrypt_TokenCreatedBefore_Restart()
    {
        var original = new MasterKeyService();
        var header = original.CreateVault("pw");
        var token = original.Encrypt("value");

        var restarted = new MasterKeyService();
        Assert.True(restarted.TryUnlock(header, "pw"));
        Assert.True(restarted.TryDecrypt(token, out var plain));
        Assert.Equal("value", plain);
    }

    [Fact]
    public void Encrypt_WhenLocked_Throws()
    {
        var sut = new MasterKeyService();

        Assert.Throws<InvalidOperationException>(() => sut.Encrypt("x"));
    }

    [Fact]
    public void TryDecrypt_WhenLocked_ReturnsFalse()
    {
        var sut = new MasterKeyService();

        Assert.False(sut.TryDecrypt("not-even-valid", out _));
    }

    [Fact]
    public void Lock_ClearsState_AndPreventsDecrypt()
    {
        var sut = new MasterKeyService();
        sut.CreateVault("pw");
        var token = sut.Encrypt("v");

        sut.Lock();

        Assert.False(sut.IsUnlocked);
        Assert.False(sut.HasVault);
        Assert.False(sut.TryDecrypt(token, out _));
    }

    [Fact]
    public void IsUnlockedFor_IsFalse_ForADifferentHeader()
    {
        var sut = new MasterKeyService();
        sut.CreateVault("pw");
        var otherHeader = new MasterKeyService().CreateVault("pw"); // different salt

        Assert.False(sut.IsUnlockedFor(otherHeader));
    }

    [Fact]
    public void TryUnlock_WithTamperedVerifier_Fails()
    {
        var header = new MasterKeyService().CreateVault("pw");
        // Flip a character in the verifier to simulate tampering/corruption.
        var chars = header.Verifier.ToCharArray();
        chars[0] = chars[0] == 'A' ? 'B' : 'A';
        header.Verifier = new string(chars);

        var restarted = new MasterKeyService();

        Assert.False(restarted.TryUnlock(header, "pw"));
    }

    [Fact]
    public void RdpConnection_EncryptedPassword_RoundTripsThroughSerializer()
    {
        // Ensure the new EncryptedPassword + Security header persist through JSON.
        var sut = new MasterKeyService();
        var header = sut.CreateVault("pw");
        var doc = new ConnectionDocument
        {
            Security = header,
            Roots =
            {
                new RdpConnection { Name = "Srv", Host = "srv01", EncryptedPassword = sut.Encrypt("secret") },
            },
        };

        var serializer = new Core.Serialization.ConnectionSerializer();
        var restored = serializer.Deserialize(serializer.Serialize(doc));

        Assert.NotNull(restored.Security);
        Assert.Equal(header.Salt, restored.Security!.Salt);
        var conn = Assert.IsType<RdpConnection>(restored.Roots[0]);
        Assert.False(string.IsNullOrEmpty(conn.EncryptedPassword));

        var reader = new MasterKeyService();
        Assert.True(reader.TryUnlock(restored.Security, "pw"));
        Assert.True(reader.TryDecrypt(conn.EncryptedPassword!, out var plain));
        Assert.Equal("secret", plain);
    }
}
