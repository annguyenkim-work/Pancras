using System.Security.Cryptography;
using System.Text;

namespace Pancras.Domain.Employees;

/// <summary>TOTP theo RFC 6238: HMAC-SHA1, 6 chữ số, bước 30 giây. Tương thích Google/Microsoft Authenticator, Authy.</summary>
public static class Totp
{
    public const int StepSeconds = 30;

    public const int Digits = 6;

    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static byte[] NewSecret() => RandomNumberGenerator.GetBytes(20);

    public static string ToBase32(ReadOnlySpan<byte> data)
    {
        var builder = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;
        foreach (var value in data)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                builder.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            builder.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        }

        return builder.ToString();
    }

    public static byte[]? FromBase32(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var bytes = new List<byte>();
        var buffer = 0;
        var bits = 0;
        foreach (var character in text.Trim().TrimEnd('=').ToUpperInvariant())
        {
            if (character is ' ' or '-')
            {
                continue;
            }

            var index = Alphabet.IndexOf(character, StringComparison.Ordinal);
            if (index < 0)
            {
                return null;
            }

            buffer = (buffer << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return bytes.ToArray();
    }

    public static long StepFor(DateTimeOffset time) => time.ToUnixTimeSeconds() / StepSeconds;

    public static string Generate(ReadOnlySpan<byte> secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        for (var index = 7; index >= 0; index--)
        {
            counter[index] = (byte)(step & 0xFF);
            step >>= 8;
        }

        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(secret, counter, hash);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Trả về bước thời gian khớp (cho phép lệch một bước) hoặc null. Bước phải lớn hơn bước đã dùng để chống dùng lại.</summary>
    public static long? Verify(ReadOnlySpan<byte> secret, string? code, DateTimeOffset now, long lastUsedStep)
    {
        var normalized = new string((code ?? string.Empty).Where(char.IsDigit).ToArray());
        if (normalized.Length != Digits)
        {
            return null;
        }

        var current = StepFor(now);
        long? match = null;
        for (var offset = -1; offset <= 1; offset++)
        {
            var step = current + offset;
            var expected = Generate(secret, step);
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(normalized))
                && step > lastUsedStep)
            {
                match = step;
            }
        }

        return match;
    }

    public static string BuildUri(string issuer, string account, string base32Secret) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}"
        + $"?secret={base32Secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";
}

public static class RecoveryCodes
{
    public const int Count = 10;

    public static IReadOnlyList<string> Generate()
    {
        var codes = new List<string>(Count);
        for (var index = 0; index < Count; index++)
        {
            var bytes = RandomNumberGenerator.GetBytes(5);
            var text = Totp.ToBase32(bytes);
            codes.Add(text[..4] + "-" + text[4..8]);
        }

        return codes;
    }

    public static string Normalize(string? code) =>
        new string((code ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    public static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(code))));
}
