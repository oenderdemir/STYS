using System.Security.Cryptography;

namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// Canonical snapshot hash doğrulaması için TEK, paylaşılan yardımcı. V1 okuyucusu
/// (<see cref="EBelgeCanonicalSnapshotReader"/>) ve V2 okuyucusu
/// (<see cref="EBelgeCanonicalSnapshotV2Reader"/>) aynı hash biçim/eşleşme kurallarını
/// burada TEK yerden kullanır - formül iki yerde ayrı ayrı tutulmaz.
/// </summary>
public static class EBelgeCanonicalSnapshotHashUtility
{
    public static bool IsValidHexHash(string sha256)
        => sha256.Length == 64 && sha256.All(IsHexChar);

    private static bool IsHexChar(char c)
        => (c >= '0' && c <= '9')
           || (c >= 'a' && c <= 'f')
           || (c >= 'A' && c <= 'F');

    public static string NormalizeHash(string sha256)
        => sha256.ToUpperInvariant();

    /// <summary>Verilen UTF-8 metnin SHA-256'sının, verilen (hex) hash ile eşleşip eşleşmediğini kontrol eder.</summary>
    public static bool MatchesUtf8(string utf8Metin, string sha256)
        => MatchesUtf8Bytes(System.Text.Encoding.UTF8.GetBytes(utf8Metin), sha256);

    /// <summary>
    /// Tam UTF-8 byte dizisinin SHA-256'sının, verilen (hex) hash ile eşleşip eşleşmediğini
    /// kontrol eder. Belirleyicilik sözleşmesi gereği hash, "exact UTF-8 bytes" üzerinden
    /// hesaplanmalıdır (bkz. hazırlık raporu, "Determinizm sözleşmesi") - metni yeniden
    /// string'e çevirip tekrar encode etmek (round-trip) yerine doğrudan byte dizisi üzerinden
    /// hesaplama tercih edilir.
    /// </summary>
    public static bool MatchesUtf8Bytes(ReadOnlySpan<byte> utf8Bytes, string sha256)
    {
        var actualHash = Convert.ToHexString(SHA256.HashData(utf8Bytes));
        return string.Equals(actualHash, sha256, StringComparison.OrdinalIgnoreCase);
    }
}
