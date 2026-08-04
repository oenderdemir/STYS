using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// Bir canonical snapshot'ın exact UTF-8 byte dizisini ve bu dizi üzerinden hesaplanmış SHA-256'sını
/// birlikte, değiştirilemez biçimde taşır (bkz. görev sonuç raporu, "Immutable payload").
///
/// Sözleşme:
/// - <see cref="FromUtf8Bytes"/>, verilen byte dizisini KOPYALAR (ImmutableArray.Create) - kaynak
///   dizi sonradan mutasyona uğrasa bile bu payload etkilenmez.
/// - Hash, saklanan (kopyalanmış) dizi üzerinden TEK SEFER hesaplanır.
/// - <see cref="ToUtf8String"/>, JSON'u yeniden serialize ETMEZ; yalnızca saklanan AYNI byte
///   dizisini string'e çevirir - hash'in hesaplandığı dizi ile döndürülen string HER ZAMAN
///   aynı kaynaktan gelir.
/// </summary>
public readonly record struct EBelgeCanonicalPayload
{
    public ImmutableArray<byte> Utf8Bytes { get; }

    public string Sha256Hex { get; }

    private EBelgeCanonicalPayload(ImmutableArray<byte> utf8Bytes, string sha256Hex)
    {
        Utf8Bytes = utf8Bytes;
        Sha256Hex = sha256Hex;
    }

    public static EBelgeCanonicalPayload FromUtf8Bytes(byte[] utf8Bytes)
    {
        var immutable = ImmutableArray.Create(utf8Bytes);
        var hash = Convert.ToHexString(SHA256.HashData(immutable.AsSpan()));
        return new EBelgeCanonicalPayload(immutable, hash);
    }

    public string ToUtf8String() => Encoding.UTF8.GetString(Utf8Bytes.AsSpan());
}
