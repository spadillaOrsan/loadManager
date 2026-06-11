using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace LoadManager.Helpers;

public static class AppSettingsCryptoHelper
{
    private const string Prefix = "enc:v1:";
    private const string CryptoPurpose = "LoadManager.UAAC.AppSettings.2026";
    private const string DefaultConfigurationPasswordHash = "5095a522aeb0532fd80c4af727e7296dc6c7cac61c35043fcb454a44cf3349b7";

    // SHA256 de la contrasena del Modo DEV (no se guarda la contrasena en claro).
    private const string DevModePasswordHash = "be33b3fe286896e06306d9fe5debe5b8da338f5a835f51a4861257ed73192e05";

    public static string GetDefaultConfigurationPasswordHash()
    {
        return DefaultConfigurationPasswordHash;
    }

    public static bool VerifyDevModePassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(DevModePasswordHash),
            Encoding.UTF8.GetBytes(HashPassword(password)));
    }

    public static bool VerifyConfigurationPassword(string? expectedHash, string password)
    {
        var normalizedHash = string.IsNullOrWhiteSpace(expectedHash)
            ? DefaultConfigurationPasswordHash
            : expectedHash.Trim();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(normalizedHash),
            Encoding.UTF8.GetBytes(HashPassword(password)));
    }

    public static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool IsEncrypted(string? value)
    {
        return value?.StartsWith(Prefix, StringComparison.Ordinal) == true;
    }

    public static string Encrypt(string value)
    {
        var plainBytes = Encoding.UTF8.GetBytes(value);
        var iv = RandomNumberGenerator.GetBytes(16);
        var keys = DeriveKeys();

        using var aes = Aes.Create();
        aes.Key = keys.EncryptionKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        var payload = new byte[iv.Length + cipherBytes.Length];
        Buffer.BlockCopy(iv, 0, payload, 0, iv.Length);
        Buffer.BlockCopy(cipherBytes, 0, payload, iv.Length, cipherBytes.Length);

        using var hmac = new HMACSHA256(keys.ValidationKey);
        var signature = hmac.ComputeHash(payload);
        var signedPayload = new byte[payload.Length + signature.Length];
        Buffer.BlockCopy(payload, 0, signedPayload, 0, payload.Length);
        Buffer.BlockCopy(signature, 0, signedPayload, payload.Length, signature.Length);

        return Prefix + Convert.ToBase64String(signedPayload);
    }

    public static string Decrypt(string value)
    {
        if (!IsEncrypted(value))
        {
            return value;
        }

        var signedPayload = Convert.FromBase64String(value[Prefix.Length..]);
        if (signedPayload.Length <= 48)
        {
            throw new InvalidOperationException("El valor cifrado de appsettings no tiene un formato valido.");
        }

        var payloadLength = signedPayload.Length - 32;
        var payload = signedPayload[..payloadLength];
        var signature = signedPayload[payloadLength..];
        var keys = DeriveKeys();

        using var hmac = new HMACSHA256(keys.ValidationKey);
        var expectedSignature = hmac.ComputeHash(payload);
        if (!CryptographicOperations.FixedTimeEquals(signature, expectedSignature))
        {
            throw new InvalidOperationException("El valor cifrado de appsettings no pudo validarse.");
        }

        var iv = payload[..16];
        var cipherBytes = payload[16..];

        using var aes = Aes.Create();
        aes.Key = keys.EncryptionKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    public static string ToInvariantString(object? value)
    {
        return value switch
        {
            null => string.Empty,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static (byte[] EncryptionKey, byte[] ValidationKey) DeriveKeys()
    {
        var seed = SHA512.HashData(Encoding.UTF8.GetBytes(CryptoPurpose));
        return (seed[..32], seed[32..64]);
    }
}
