using System.Buffers.Binary;
using System.Security.Cryptography;

namespace e2e.Infrastructure;

internal static class Totp
{
    internal static long CurrentStep => DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;

    internal static string Code(string secret, long step)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        List<byte> key = [];
        var bits = 0;
        var buffer = 0;
        foreach (var c in secret.TrimEnd('=').ToUpperInvariant())
        {
            var value = alphabet.IndexOf(c);
            if (value < 0) throw new ArgumentException("Invalid TOTP secret.");
            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8) { bits -= 8; key.Add((byte)(buffer >> bits)); }
        }
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        var digest = HMACSHA1.HashData(key.ToArray(), counter);
        var offset = digest[^1] & 15;
        var number = BinaryPrimitives.ReadUInt32BigEndian(digest.AsSpan(offset, 4)) & 0x7fffffff;
        return (number % 1_000_000).ToString("D6");
    }

    internal static string InvalidCode(string secret)
    {
        var step = CurrentStep;
        var valid = Enumerable.Range(-2, 5).Select(i => Code(secret, step + i)).ToHashSet();
        return Enumerable.Range(0, 10).Select(i => i.ToString("D6")).First(code => !valid.Contains(code));
    }
}
