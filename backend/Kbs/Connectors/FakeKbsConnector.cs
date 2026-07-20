using Microsoft.Extensions.Options;
using STYS.Kbs.Constants;
using STYS.Kbs.Dtos;
using STYS.Kbs.Options;

namespace STYS.Kbs.Connectors;

public class FakeKbsConnector(IOptions<KbsOptions> options) : IKbsConnector
{
    public string Saglayici => KbsEntegrasyonTipleri.Fake;
    public Task<KbsSonuc> GirisBildirAsync(KbsGirisTalebi talep, CancellationToken cancellationToken) => Task.FromResult(Create());
    public Task<KbsSonuc> CikisBildirAsync(KbsCikisTalebi talep, CancellationToken cancellationToken) => Task.FromResult(Create());
    public Task<KbsSonuc> OdaGuncelleAsync(KbsOdaGuncellemeTalebi talep, CancellationToken cancellationToken) => Task.FromResult(Create());
    public Task<KbsBaglantiTestSonucu> BaglantiKontrolAsync(int tesisId, CancellationToken cancellationToken) => Task.FromResult(new KbsBaglantiTestSonucu(true, "Sentetik KBS connector hazir.", false));

    private KbsSonuc Create() => options.Value.FakeResponse switch
    {
        "GeciciHata" => new(false, "FAKE-503", "Sentetik gecici servis hatasi.", KbsHataSiniflari.Transient),
        "Timeout" => new(false, "FAKE-TIMEOUT", "Sentetik zaman asimi.", KbsHataSiniflari.Transient),
        "YetkiIpHatasi" => new(false, "FAKE-AUTH", "Sentetik yetki veya IP hatasi.", KbsHataSiniflari.Configuration),
        "GecersizVeri" => new(false, "FAKE-VALIDATION", "Sentetik gecersiz veri.", KbsHataSiniflari.Permanent),
        "KayitZatenMevcut" => new(true, "FAKE-EXISTS", "Sentetik kayit zaten mevcut sonucu."),
        "KayitBulunamadi" => new(false, "FAKE-NOTFOUND", "Sentetik kayit bulunamadi.", KbsHataSiniflari.Permanent),
        "Belirsiz" => new(false, "FAKE-UNCERTAIN", "Sentetik cevap alinamadi; sonuc belirsiz.", KbsHataSiniflari.Uncertain, true),
        _ => new(true, "FAKE-OK", "Sentetik bildirim basarili.")
    };
}
