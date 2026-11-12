using System.Security.Cryptography;
using System.Text;
using Forge.Guild;

namespace Forge.Tests;

public class BackupCryptoTests
{
    [Fact]
    public void Key_RoundTripsThroughItsWrittenForm()
    {
        var key = BackupCrypto.NewRecoveryKey();
        var text = BackupCrypto.FormatKey(key);
        Assert.Equal(13, text.Split('-').Length);
        Assert.Equal(key, BackupCrypto.ParseKey(text));
        Assert.Equal(key, BackupCrypto.ParseKey(" " + text.ToLowerInvariant().Replace("-", " ") + " "));
    }

    [Fact]
    public void Key_ReadsAmbiguousLettersAsDigits()
    {
        var key = new byte[32];
        var text = BackupCrypto.FormatKey(key); // all zeros
        Assert.Equal(key, BackupCrypto.ParseKey(text.Replace('0', 'O')));
    }

    [Fact]
    public void Key_RejectsGarbage()
    {
        Assert.Null(BackupCrypto.ParseKey("hello"));
        Assert.Null(BackupCrypto.ParseKey(null));
        Assert.Null(BackupCrypto.ParseKey(new string('U', 52))); // U isn't in the alphabet
    }

    [Fact]
    public void Derive_IsStableAndSeparatesItsParts()
    {
        var key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var a = BackupCrypto.Derive(key);
        var b = BackupCrypto.Derive(key);
        Assert.Equal(a.LookupId, b.LookupId);
        Assert.Equal(a.EncryptionKey, b.EncryptionKey);
        Assert.Equal(64, a.LookupId.Length);
        Assert.NotEqual(a.LookupId, Convert.ToHexString(a.EncryptionKey).ToLowerInvariant());
        Assert.NotEqual(a.LookupId, BackupCrypto.Derive(BackupCrypto.NewRecoveryKey()).LookupId);
    }

    [Fact]
    public void Seal_ThenOpen_GivesBackTheBytes()
    {
        var keys = BackupCrypto.Derive(BackupCrypto.NewRecoveryKey());
        var plain = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("SQLite format 3 ", 1000)));
        var sealedBytes = BackupCrypto.Seal(plain, keys.EncryptionKey);
        Assert.True(sealedBytes.Length < plain.Length); // compressed first
        Assert.Equal(plain, BackupCrypto.Open(sealedBytes, keys.EncryptionKey));
    }

    [Fact]
    public void Open_FailsWithTheWrongKey_OrTamperedData()
    {
        var keys = BackupCrypto.Derive(BackupCrypto.NewRecoveryKey());
        var sealedBytes = BackupCrypto.Seal(new byte[] { 1, 2, 3 }, keys.EncryptionKey);
        var other = BackupCrypto.Derive(BackupCrypto.NewRecoveryKey());
        Assert.ThrowsAny<CryptographicException>(() => BackupCrypto.Open(sealedBytes, other.EncryptionKey));
        sealedBytes[^1] ^= 0xFF;
        Assert.ThrowsAny<CryptographicException>(() => BackupCrypto.Open(sealedBytes, keys.EncryptionKey));
    }

    [Fact]
    public void TokenHash_IsHexSha256() => Assert.Equal(64, BackupCrypto.HashToken("abc").Length);
}
