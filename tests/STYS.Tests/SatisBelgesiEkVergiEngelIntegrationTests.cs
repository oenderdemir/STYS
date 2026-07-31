using Microsoft.EntityFrameworkCore;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Part B — dc861c7'nin ürettiği "ÖTV/ÖİV/konaklama vergisi içeren belgeler için otomatik
/// muhasebe fişi üretimi tamamen engellenir" davranışını GERÇEK SQL Server üzerinde, gerçek
/// public servis akışlarıyla (CreateAsync → MuhasebeOnayinaGonderAsync → MuhasebeOnaylaAsync →
/// MuhasebeFisiOlusturAsync) doğrulayan, ve hatanın ATOMIK olduğunu (hiçbir kayıt kalıcı
/// olarak oluşmadığını) kanıtlayan regresyon testleri.
///
/// Bu belge tipleri hiçbir Tesis/CariKart/hesap planı FK'sine bağımlı olmadığından (engelleme,
/// SatisBelgesiMuhasebeFisService.MuhasebeFisiOlusturAsync içinde hesap planı/cari kart
/// çözümlemesinden ÖNCE gerçekleşir), bu testler Kurum/Il/Tesis seed ETMEZ — yalnızca belgenin
/// kendisini oluşturur.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class SatisBelgesiEkVergiEngelIntegrationTests
{
    private const string TestMarker = "EKVERGI-913";

    public static IEnumerable<object[]> EkVergiliBelgeSenaryolari()
    {
        // (belgeTipi, hangi ek verginin > 0 olacağı) — ek vergi engellemesi satır bazlıdır
        // (belge tipinden BAĞIMSIZ), bu yüzden SatisFaturasi+AlisFaturasi kapsaması yeterlidir.
        // SatisIadeFaturasi/AlisIadeFaturasi bu sürümden itibaren onay aşamasında geçerli bir
        // KarsiTarafFaturaNo/IadeEdilenBelgeId (gerçek, muhasebe onaylı bir asıl fatura zinciri)
        // gerektirdiğinden - bu testin BİLEREK Kurum/Il/Tesis seed ETMEYEN, minimal tasarımıyla
        // (bkz. sınıf açıklaması) çelişir; bu iki tip buradan çıkarıldı, kapsamları FaturaNumara/
        // FaturaReferans entegrasyon testlerinde ayrıca doğrulanıyor.
        yield return [SatisBelgesiTipi.SatisFaturasi, "Otv"];
        yield return [SatisBelgesiTipi.AlisFaturasi, "Oiv"];
    }

    [IntegrationTheory]
    [MemberData(nameof(EkVergiliBelgeSenaryolari))]
    public async Task EkVergiIcerenBelge_MuhasebeFisiEngellenirVeHicbirKayitKalıcıOlusmaz(SatisBelgesiTipi belgeTipi, string vergiAlani)
    {
        var uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();

        var satirRequest = new CreateSatisBelgesiSatiriRequest
        {
            SiraNo = 1,
            Aciklama = "Ek vergili satir",
            Miktar = 1,
            BirimFiyat = 1000m,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
            KdvOrani = 20m
        };

        switch (vergiAlani)
        {
            case "Otv":
                satirRequest.OtvOrani = 25m;
                break;
            case "Oiv":
                satirRequest.OivOrani = 10m;
                break;
            case "Konaklama":
                satirRequest.KonaklamaVergisiOrani = 2m;
                break;
            default:
                throw new InvalidOperationException($"Bilinmeyen vergi alani: {vergiAlani}");
        }

        var request = new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{uniqueSuffix}",
            BelgeTipi = belgeTipi,
            TesisId = 1,
            BelgeTarihi = new DateTime(2026, 1, 15),
            MusteriAdSoyad = "Test Musteri " + uniqueSuffix,
            // AlisFaturasi (gelen belge) onay aşamasında artık KarsiTarafFaturaNo zorunlu tutar.
            KarsiTarafFaturaNo = belgeTipi == SatisBelgesiTipi.AlisFaturasi ? $"TED-{uniqueSuffix}" : null,
            Satirlar = [satirRequest]
        };

        try
        {
            var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
            // Onaya gönderme/onaylama, ek-vergi kontrolünü İÇERMEZ (bu kontrol yalnızca
            // MuhasebeFisiOlusturAsync'te vardır) — bu yüzden buraya kadar sorunsuz ilerlemesi
            // beklenir; asıl engelleme fiş üretimi sırasında gerçekleşmelidir.
            var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisService, request);

            var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);
            var ex = await Assert.ThrowsAsync<BaseException>(
                () => fisService.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None));

            Assert.Contains(
                "ÖTV, ÖİV veya konaklama vergisi içeren belgeler için muhasebe hesap eşlemeleri henüz tanımlanmamıştır",
                ex.Message);

            // Hata, transaction rollback'i ile TAMAMEN geri alınmış olmalı: hiçbir MuhasebeFis,
            // MuhasebeFisSatir, CariHareket, StokHareketi kalıcı olarak oluşmamalı ve belgenin
            // MuhasebeFisId'si null kalmalı — GERÇEK DB'den (no-tracking) yeniden okunarak
            // doğrulanır.
            await SatisBelgesiMuhasebeTestSupport.AssertHicMuhasebeKaydiOlusmadiAsync(dbContext, onaylanmis.Id!.Value);
        }
        finally
        {
            await using var cleanupContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
            await SatisBelgesiMuhasebeTestSupport.CleanupAsync(cleanupContext, uniqueSuffix);
        }
    }
}
