using System.Security.Cryptography;
using System.Text;
using STYS.Muhasebe.SatisBelgeleri;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.4.2: EBelgeCanonicalPayload'ın "exact UTF-8 byte + tek seferlik hash + kaynak
/// mutasyonuna karşı bağışıklık" sözleşmesini doğrular.
/// </summary>
public class EBelgeCanonicalPayloadTests
{
    [Fact]
    public void Sha256SaklananTamByteDizisiUzerindenHesaplanir()
    {
        var json = "{\"a\":1,\"b\":\"ç\"}";
        var utf8Bytes = Encoding.UTF8.GetBytes(json);

        var payload = EBelgeCanonicalPayload.FromUtf8Bytes(utf8Bytes);

        var beklenenHash = Convert.ToHexString(SHA256.HashData(utf8Bytes));
        Assert.Equal(beklenenHash, payload.Sha256Hex);
    }

    [Fact]
    public void ToUtf8StringSaklananAyniByteDizisindenTurer()
    {
        var json = "{\"deger\":\"İstanbul\"}";
        var utf8Bytes = Encoding.UTF8.GetBytes(json);

        var payload = EBelgeCanonicalPayload.FromUtf8Bytes(utf8Bytes);

        Assert.Equal(json, payload.ToUtf8String());
        // ToUtf8String, saklanan Utf8Bytes'ın AYNISINI string'e çevirir - hash da bu diziden.
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload.ToUtf8String()))), payload.Sha256Hex);
    }

    [Fact]
    public void KaynakByteDizisiSonradanDegistirilirseImmutablePayloadEtkilenmez()
    {
        var kaynakBytes = Encoding.UTF8.GetBytes("{\"deger\":1}");
        var orijinalHash = Convert.ToHexString(SHA256.HashData(kaynakBytes));

        var payload = EBelgeCanonicalPayload.FromUtf8Bytes(kaynakBytes);

        // Kaynak diziyi MUTASYONA uğrat - payload'ın kopyaladığı ImmutableArray etkilenmemeli.
        kaynakBytes[0] = 0xFF;
        kaynakBytes[1] = 0x00;

        Assert.Equal(orijinalHash, payload.Sha256Hex);
        Assert.Equal("{\"deger\":1}", payload.ToUtf8String());
        Assert.NotEqual(kaynakBytes[0], payload.Utf8Bytes[0]);
    }
}
