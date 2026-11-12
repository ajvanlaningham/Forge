using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Forge.Guild
{
    /// <summary>What a recovery key unlocks. The server only ever sees <see cref="LookupId"/> and <see cref="WriteToken"/>.</summary>
    public sealed record BackupKeys(string LookupId, string WriteToken, byte[] EncryptionKey);

    /// <summary>
    /// End-to-end encrypted off-phone backups (Decision 24). A random 256-bit recovery key, shown
    /// once and kept in a password manager, derives three independent values with HKDF: where
    /// the backup lives on the server, the token that lets you replace or fetch it, and the AES
    /// key that encrypts it. The server stores an opaque blob and can't read it; losing the key
    /// means losing the backup, by design.
    /// </summary>
    public static class BackupCrypto
    {
        private const byte Version = 1;
        private const int NonceSize = 12, TagSize = 16, KeySize = 32;
        private static readonly byte[] Aad = Encoding.ASCII.GetBytes("forge-backup-v1");
        private static readonly byte[] Salt = Encoding.ASCII.GetBytes("forge.backup.salt.v1");

        // Crockford base32: no I, L, O, U, so a key read aloud or copied by hand survives.
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        public static byte[] NewRecoveryKey() => RandomNumberGenerator.GetBytes(KeySize);

        /// <summary>"7K3M-…" in thirteen groups of four.</summary>
        public static string FormatKey(byte[] key)
        {
            var chars = new StringBuilder();
            int buffer = 0, bits = 0;
            foreach (var b in key)
            {
                buffer = (buffer << 8) | b;
                bits += 8;
                while (bits >= 5)
                {
                    chars.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                    bits -= 5;
                }
            }
            if (bits > 0) chars.Append(Alphabet[(buffer << (5 - bits)) & 31]);
            var s = chars.ToString();
            return string.Join("-", Enumerable.Range(0, (s.Length + 3) / 4).Select(i => s.Substring(i * 4, Math.Min(4, s.Length - i * 4))));
        }

        /// <summary>Forgiving: ignores case, spaces and dashes, and reads O as 0 and I/L as 1. Null if it isn't a key.</summary>
        public static byte[]? ParseKey(string? text)
        {
            var s = new string((text ?? "").ToUpperInvariant().Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray())
                .Replace('O', '0').Replace('I', '1').Replace('L', '1');
            if (s.Length != 52) return null;
            var bytes = new List<byte>(KeySize);
            int buffer = 0, bits = 0;
            foreach (var c in s)
            {
                var v = Alphabet.IndexOf(c);
                if (v < 0) return null;
                buffer = (buffer << 5) | v;
                bits += 5;
                if (bits >= 8)
                {
                    bytes.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                    bits -= 8;
                }
            }
            // 52 symbols carry 260 bits; the 4 left over must be the zero padding FormatKey wrote.
            if ((buffer & ((1 << bits) - 1)) != 0 || bytes.Count != KeySize) return null;
            return bytes.ToArray();
        }

        public static BackupKeys Derive(byte[] recoveryKey)
        {
            byte[] Part(string info, int length) => HKDF.DeriveKey(HashAlgorithmName.SHA256, recoveryKey, length, Salt, Encoding.ASCII.GetBytes(info));
            return new BackupKeys(
                Convert.ToHexString(Part("lookup", 32)).ToLowerInvariant(),
                Convert.ToBase64String(Part("write", 32)),
                Part("encrypt", KeySize));
        }

        /// <summary>Compresses, then encrypts with AES-256-GCM: version, nonce, tag, ciphertext.</summary>
        public static byte[] Seal(byte[] plain, byte[] encryptionKey)
        {
            byte[] compressed;
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true)) gz.Write(plain);
                compressed = ms.ToArray();
            }
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var tag = new byte[TagSize];
            var cipher = new byte[compressed.Length];
            using (var aes = new AesGcm(encryptionKey, TagSize))
                aes.Encrypt(nonce, compressed, cipher, tag, Aad);
            var sealedBytes = new byte[1 + NonceSize + TagSize + cipher.Length];
            sealedBytes[0] = Version;
            nonce.CopyTo(sealedBytes, 1);
            tag.CopyTo(sealedBytes, 1 + NonceSize);
            cipher.CopyTo(sealedBytes, 1 + NonceSize + TagSize);
            return sealedBytes;
        }

        /// <summary>Throws <see cref="CryptographicException"/> when the key is wrong or the data was altered.</summary>
        public static byte[] Open(byte[] sealedBytes, byte[] encryptionKey)
        {
            if (sealedBytes.Length < 1 + NonceSize + TagSize || sealedBytes[0] != Version)
                throw new CryptographicException("Not a Forge backup.");
            var nonce = sealedBytes.AsSpan(1, NonceSize);
            var tag = sealedBytes.AsSpan(1 + NonceSize, TagSize);
            var cipher = sealedBytes.AsSpan(1 + NonceSize + TagSize);
            var compressed = new byte[cipher.Length];
            using (var aes = new AesGcm(encryptionKey, TagSize))
                aes.Decrypt(nonce, cipher, tag, compressed, Aad);
            using var input = new GZipStream(new MemoryStream(compressed), CompressionMode.Decompress);
            using var output = new MemoryStream();
            input.CopyTo(output);
            return output.ToArray();
        }

        /// <summary>Servers store a hash of the write token, never the token.</summary>
        public static string HashToken(string token)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    }
}
