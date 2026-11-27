using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

// One versioned text envelope for snapshots and individual native key records.
static class SOFileEnvelope
{
    public const string Header = "SODATA/1\n";
    private const int DigestSize = 16;
    private const int IVSize = 16;
    private const int TagSize = 32;
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

    public static byte[] NewDeviceKey()
    {
        using RandomNumberGenerator random = RandomNumberGenerator.Create();
        byte[] key = new byte[64]; // Independent AES-256 and HMAC-SHA256 keys.
        random.GetBytes(key);
        return key;
    }

    public static string Revision(byte[] bytes)
    {
        using SHA256 sha256 = SHA256.Create();
        return BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
    }

    public static bool HasEnvelope(byte[] bytes) => bytes.Length >= 7 && Encoding.ASCII.GetString(bytes, 0, 7) == "SODATA/";

    public static byte[] Encode(string fileName, string value, byte[] key)
    {
        byte[] payload = Utf8.GetBytes(value);
        byte[] clear = new byte[DigestSize + payload.Length];
        using MD5 md5 = MD5.Create();
        Buffer.BlockCopy(md5.ComputeHash(payload), 0, clear, 0, DigestSize);
        Buffer.BlockCopy(payload, 0, clear, DigestSize, payload.Length);
        using Aes aes = CreateCipher(key);
        aes.GenerateIV();
        using ICryptoTransform encryptor = aes.CreateEncryptor();
        byte[] cipher = encryptor.TransformFinalBlock(clear, 0, clear.Length);
        int signedLength = IVSize + cipher.Length;
        byte[] blob = new byte[signedLength + TagSize];
        Buffer.BlockCopy(aes.IV, 0, blob, 0, IVSize);
        Buffer.BlockCopy(cipher, 0, blob, IVSize, cipher.Length);
        Buffer.BlockCopy(ComputeTag(fileName, key, blob, signedLength), 0, blob, signedLength, TagSize);
        return Utf8.GetBytes(Header + Convert.ToBase64String(blob));
    }

    public static string Decode(string fileName, byte[] bytes, byte[] key)
    {
        try
        {
            string text = Utf8.GetString(bytes);
            if (!text.StartsWith(Header, StringComparison.Ordinal)) throw new InvalidDataException("Unsupported SO data envelope version.");
            string encoded = text.Substring(Header.Length);
            byte[] blob = Convert.FromBase64String(encoded);
            int cipherLength = blob.Length - IVSize - TagSize;
            if (cipherLength < 32 || cipherLength % 16 != 0 || Convert.ToBase64String(blob) != encoded) throw new InvalidDataException("Invalid SO data envelope.");
            int signedLength = IVSize + cipherLength;
            byte[] tag = ComputeTag(fileName, key, blob, signedLength);
            if (!CryptographicOperations.FixedTimeEquals(tag, new ReadOnlySpan<byte>(blob, signedLength, TagSize))) throw new InvalidDataException("SO data authentication failed.");
            using Aes aes = CreateCipher(key);
            byte[] iv = new byte[IVSize];
            Buffer.BlockCopy(blob, 0, iv, 0, IVSize);
            aes.IV = iv;
            using ICryptoTransform decryptor = aes.CreateDecryptor();
            byte[] clear = decryptor.TransformFinalBlock(blob, IVSize, cipherLength);
            if (clear.Length < DigestSize) throw new InvalidDataException("Truncated SO data payload.");
            using MD5 md5 = MD5.Create();
            byte[] digest = md5.ComputeHash(clear, DigestSize, clear.Length - DigestSize);
            if (!CryptographicOperations.FixedTimeEquals(digest, new ReadOnlySpan<byte>(clear, 0, DigestSize))) throw new InvalidDataException("SO data checksum mismatch.");
            return Utf8.GetString(clear, DigestSize, clear.Length - DigestSize);
        }
        catch (Exception error) when (error is FormatException || error is DecoderFallbackException || error is CryptographicException)
        {
            throw new InvalidDataException("Invalid encrypted SO data.", error);
        }
    }

    private static Aes CreateCipher(byte[] key)
    {
        byte[] cipherKey = new byte[32];
        Buffer.BlockCopy(key, 0, cipherKey, 0, cipherKey.Length);
        Aes aes = Aes.Create();
        aes.Key = cipherKey;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        return aes;
    }

    private static byte[] ComputeTag(string fileName, byte[] key, byte[] blob, int count)
    {
        byte[] signingKey = new byte[32];
        Buffer.BlockCopy(key, 32, signingKey, 0, signingKey.Length);
        using HMACSHA256 mac = new HMACSHA256(signingKey);
        // Bind both format and leaf name. Copying a valid balance over a different key must fail.
        byte[] label = Utf8.GetBytes(Header + fileName + "\0");
        mac.TransformBlock(label, 0, label.Length, label, 0);
        mac.TransformBlock(blob, 0, count, blob, 0);
        mac.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return mac.Hash;
    }

    public static string ReadLegacyJson(byte[] bytes)
    {
        try
        {
            if (bytes.Length > 0 && bytes[0] == 0) bytes = UnwrapLegacyString(bytes);
            string json = Utf8.GetString(bytes);
            if (json.Length > 0 && json[0] == '\uFEFF') json = json.Substring(1);
            if (!json.TrimStart().StartsWith("{", StringComparison.Ordinal)) throw new InvalidDataException("Expected a legacy JSON object.");
            return json;
        }
        catch (Exception error) when (error is EndOfStreamException || error is DecoderFallbackException || error is FormatException)
        {
            throw new InvalidDataException("Invalid legacy SO data.", error);
        }
    }

    private static byte[] UnwrapLegacyString(byte[] bytes)
    {
        // Read just the historical single-string record. No object deserialization takes place.
        using MemoryStream stream = new MemoryStream(bytes, false);
        stream.Position = 1;
        using BinaryReader reader = new BinaryReader(stream, Utf8);
        int rootId = reader.ReadInt32();
        if (rootId <= 0 || reader.ReadInt32() != -1 || reader.ReadInt32() != 1 || reader.ReadInt32() != 0) throw new InvalidDataException("Invalid legacy save header.");
        if (reader.ReadByte() != 6 || reader.ReadInt32() != rootId) throw new InvalidDataException("Expected one legacy save string.");
        byte[] payload = Convert.FromBase64String(reader.ReadString());
        if (reader.ReadByte() != 11 || stream.Position != stream.Length) throw new InvalidDataException("Invalid legacy save ending.");
        return payload;
    }
}
