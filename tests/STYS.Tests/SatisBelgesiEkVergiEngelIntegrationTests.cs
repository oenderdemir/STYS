using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
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
        // (belgeTipi, hangi ek verginin > 0 olacağı) — bu MemberData yalnızca minimal (Kurum/Il/
        // Tesis seed ETMEYEN) senaryoları kapsar. SatisIadeFaturasi/AlisIadeFaturasi artık onay
        // aşamasında geçerli bir KarsiTarafFaturaNo/IadeEdilenBelgeId (gerçek, muhasebe onaylı bir
        // asıl fatura zinciri) gerektirdiğinden bu minimal kuruluma sığmıyor - bu iki tip AŞAĞIDA,
        // TAM Kurum/Il/Tesis/asıl-fatura zinciri kuran ayrı, adanmış testlerle
        // (SatisIadeFaturasi_KonaklamaVergisiIcerenBelge_..., AlisIadeFaturasi_OtvIcerenBelge_...)
        // kapsanıyor - burada eksik değildir.
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

    // ─────────────────────────────────────────────────────────────
    // SatisIadeFaturasi / AlisIadeFaturasi — geçerli asıl fatura + referans zinciriyle (Part F)
    // Bu iki senaryo, sınıfın geri kalanının aksine TAM bir Kurum/Il/Tesis/hesap planı/cari kart/
    // asıl fatura zinciri kurar - artık onay aşamasında zorunlu olan KarsiTarafFaturaNo/
    // IadeEdilenBelgeId doğrulamasını geçebilmek için gereklidir.
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task SatisIadeFaturasi_KonaklamaVergisiIcerenBelge_MuhasebeFisiEngellenirVeHicbirKayitOlusmaz()
    {
        var uniqueSuffix = $"{TestMarker}-SIF-{Guid.NewGuid():N}"[..24];
        int kurumId = 0, ilId = 0, tesisId = 0;

        try
        {
            await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
            var (kurum, il, tesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, uniqueSuffix);
            kurumId = kurum.Id; ilId = il.Id; tesisId = tesis.Id;

            var gelirHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(uniqueSuffix, MuhasebeAnaHesapKodlari.GelirSatis, "GELIR", tesisId);
            var kdvHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(uniqueSuffix, MuhasebeAnaHesapKodlari.KDVHesaplanan, "KDV", tesisId);
            var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(uniqueSuffix, "MUS", tesisId);
            dbContext.MuhasebeHesapPlanlari.AddRange(gelirHesap, kdvHesap, musteriHesap);
            await dbContext.SaveChangesAsync();
            var musteriKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(uniqueSuffix, "MUS", CariKartTipleri.Musteri, tesisId, musteriHesap.Id);
            dbContext.CariKartlar.Add(musteriKart);
            await dbContext.SaveChangesAsync();
            dbContext.MuhasebeDonemler.Add(new MuhasebeDonem
            {
                TesisId = tesisId, MaliYil = 2026, DonemNo = 1,
                BaslangicTarihi = new DateTime(2026, 1, 1), BitisTarihi = new DateTime(2026, 12, 31), KapaliMi = false
            });
            await dbContext.SaveChangesAsync();

            var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
            var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
            var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, donemService);

            // Geçerli asıl SatisFaturasi - FaturaKesildi durumuna kadar götürülür.
            var asilCreated = await service.CreateAsync(new CreateSatisBelgesiRequest
            {
                BelgeNo = $"BLG-{uniqueSuffix}-{Guid.NewGuid():N}"[..40],
                BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
                TesisId = tesisId,
                CariKartId = musteriKart.Id,
                BelgeTarihi = new DateTime(2026, 3, 1),
                MusteriAdSoyad = "Musteri " + uniqueSuffix,
                Satirlar =
                [
                    new CreateSatisBelgesiSatiriRequest
                    {
                        SiraNo = 1, Aciklama = "Asil satis", Miktar = 1, BirimFiyat = 1000m,
                        KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                    }
                ]
            });
            await service.MuhasebeOnayinaGonderAsync(asilCreated.Id!.Value);
            await service.MuhasebeOnaylaAsync(asilCreated.Id!.Value);
            await fisService.MuhasebeFisiOlusturAsync(asilCreated.Id.Value);
            dbContext.KurumFaturaNumaraSayaclari.Add(new KurumFaturaNumaraSayaci
            {
                KurumId = kurumId, MaliYil = 2026, SeriKodu = "EVK", SonNumara = 0, AktifMi = true
            });
            await dbContext.SaveChangesAsync();
            var asilKesildi = await service.FaturaKesAsync(asilCreated.Id.Value, new FaturaKesRequest { SeriKodu = "EVK" });

            // Ek vergili (Konaklama) SatisIadeFaturasi - geçerli KarsiTarafFaturaNo + IadeEdilenBelgeId ile.
            var iadeCreated = await service.CreateAsync(new CreateSatisBelgesiRequest
            {
                BelgeNo = $"BLG-{uniqueSuffix}-{Guid.NewGuid():N}"[..40],
                BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
                TesisId = tesisId,
                CariKartId = musteriKart.Id,
                BelgeTarihi = new DateTime(2026, 3, 5),
                MusteriAdSoyad = "Musteri " + uniqueSuffix,
                KarsiTarafFaturaNo = $"MUS-{Guid.NewGuid():N}"[..20],
                IadeEdilenBelgeId = asilKesildi.Id,
                Satirlar =
                [
                    new CreateSatisBelgesiSatiriRequest
                    {
                        SiraNo = 1, Aciklama = "Ek vergili iade satiri", Miktar = 1, BirimFiyat = 1000m,
                        KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m, KonaklamaVergisiOrani = 2m,
                        KaynakSatirId = asilKesildi.Satirlar[0].Id!.Value.ToString()
                    }
                ]
            });

            // Onaya gönderme/onaylama ek-vergi kontrolünü İÇERMEZ - sorunsuz ilerlemesi beklenir;
            // asıl engelleme fiş üretimi sırasında gerçekleşmelidir.
            await service.MuhasebeOnayinaGonderAsync(iadeCreated.Id!.Value);
            await service.MuhasebeOnaylaAsync(iadeCreated.Id!.Value);

            var ex = await Assert.ThrowsAsync<BaseException>(
                () => fisService.MuhasebeFisiOlusturAsync(iadeCreated.Id.Value, CancellationToken.None));
            Assert.Contains(
                "ÖTV, ÖİV veya konaklama vergisi içeren belgeler için muhasebe hesap eşlemeleri henüz tanımlanmamıştır",
                ex.Message);

            await SatisBelgesiMuhasebeTestSupport.AssertHicMuhasebeKaydiOlusmadiAsync(dbContext, iadeCreated.Id!.Value);
        }
        finally
        {
            if (kurumId > 0)
            {
                await using var cleanupContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
                await CleanupKurumAsync(cleanupContext, kurumId, tesisId, ilId, uniqueSuffix);
            }
        }
    }

    [IntegrationFact]
    public async Task AlisIadeFaturasi_OtvIcerenBelge_MuhasebeFisiEngellenirVeHicbirKayitOlusmaz()
    {
        var uniqueSuffix = $"{TestMarker}-AIF-{Guid.NewGuid():N}"[..24];
        int kurumId = 0, ilId = 0, tesisId = 0;

        try
        {
            await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
            var (kurum, il, tesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, uniqueSuffix);
            kurumId = kurum.Id; ilId = il.Id; tesisId = tesis.Id;

            var giderHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(uniqueSuffix, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, "GIDER", tesisId);
            var kdvHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(uniqueSuffix, MuhasebeAnaHesapKodlari.KDVIndirilecek, "KDV", tesisId);
            var stokHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(uniqueSuffix, MuhasebeAnaHesapKodlari.StokTicariMal, "STOK", tesisId);
            var tedarikciHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(uniqueSuffix, "TED", tesisId);
            dbContext.MuhasebeHesapPlanlari.AddRange(giderHesap, kdvHesap, stokHesap, tedarikciHesap);
            await dbContext.SaveChangesAsync();
            var tedarikciKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(uniqueSuffix, "TED", CariKartTipleri.Tedarikci, tesisId, tedarikciHesap.Id);
            tedarikciKart.VergiNoTckn = "1111111111";
            dbContext.CariKartlar.Add(tedarikciKart);
            await dbContext.SaveChangesAsync();
            dbContext.MuhasebeDonemler.Add(new MuhasebeDonem
            {
                TesisId = tesisId, MaliYil = 2026, DonemNo = 1,
                BaslangicTarihi = new DateTime(2026, 1, 1), BitisTarihi = new DateTime(2026, 12, 31), KapaliMi = false
            });
            await dbContext.SaveChangesAsync();

            var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
            var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
            var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, donemService);

            // Geçerli asıl AlisFaturasi - muhasebe onaylı ve fişli.
            var asilCreated = await service.CreateAsync(new CreateSatisBelgesiRequest
            {
                BelgeNo = $"BLG-{uniqueSuffix}-{Guid.NewGuid():N}"[..40],
                BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
                TesisId = tesisId,
                CariKartId = tedarikciKart.Id,
                BelgeTarihi = new DateTime(2026, 3, 1),
                MusteriAdSoyad = "Tedarikci " + uniqueSuffix,
                KarsiTarafFaturaNo = $"TED-{Guid.NewGuid():N}"[..20],
                Satirlar =
                [
                    new CreateSatisBelgesiSatiriRequest
                    {
                        SiraNo = 1, Aciklama = "Asil alis", Miktar = 1, BirimFiyat = 500m,
                        KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                    }
                ]
            });
            await service.MuhasebeOnayinaGonderAsync(asilCreated.Id!.Value);
            await service.MuhasebeOnaylaAsync(asilCreated.Id!.Value);
            await fisService.MuhasebeFisiOlusturAsync(asilCreated.Id.Value);

            // Ek vergili (ÖTV) AlisIadeFaturasi - geçerli IadeEdilenBelgeId ile.
            var iadeCreated = await service.CreateAsync(new CreateSatisBelgesiRequest
            {
                BelgeNo = $"BLG-{uniqueSuffix}-{Guid.NewGuid():N}"[..40],
                BelgeTipi = SatisBelgesiTipi.AlisIadeFaturasi,
                TesisId = tesisId,
                CariKartId = tedarikciKart.Id,
                BelgeTarihi = new DateTime(2026, 3, 5),
                MusteriAdSoyad = "Tedarikci " + uniqueSuffix,
                IadeEdilenBelgeId = asilCreated.Id,
                Satirlar =
                [
                    new CreateSatisBelgesiSatiriRequest
                    {
                        SiraNo = 1, Aciklama = "Ek vergili iade satiri", Miktar = 1, BirimFiyat = 500m,
                        KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m, OtvOrani = 25m,
                        KaynakSatirId = asilCreated.Satirlar[0].Id!.Value.ToString()
                    }
                ]
            });

            await service.MuhasebeOnayinaGonderAsync(iadeCreated.Id!.Value);
            await service.MuhasebeOnaylaAsync(iadeCreated.Id!.Value);

            var ex = await Assert.ThrowsAsync<BaseException>(
                () => fisService.MuhasebeFisiOlusturAsync(iadeCreated.Id.Value, CancellationToken.None));
            Assert.Contains(
                "ÖTV, ÖİV veya konaklama vergisi içeren belgeler için muhasebe hesap eşlemeleri henüz tanımlanmamıştır",
                ex.Message);

            await SatisBelgesiMuhasebeTestSupport.AssertHicMuhasebeKaydiOlusmadiAsync(dbContext, iadeCreated.Id!.Value);
        }
        finally
        {
            if (kurumId > 0)
            {
                await using var cleanupContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
                await CleanupKurumAsync(cleanupContext, kurumId, tesisId, ilId, uniqueSuffix);
            }
        }
    }

    private static async Task CleanupKurumAsync(StysAppDbContext dbContext, int kurumId, int tesisId, int ilId, string uniqueSuffix)
    {
        var belgeIds = await dbContext.SatisBelgeleri.Where(x => x.KurumId == kurumId).Select(x => x.Id).ToListAsync();
        var fisIds = new List<int>();
        if (belgeIds.Count > 0)
        {
            fisIds = await dbContext.MuhasebeFisler
                .Where(x => x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi && x.KaynakId != null && belgeIds.Contains(x.KaynakId.Value))
                .Select(x => x.Id).ToListAsync();
            await dbContext.CariHareketler
                .Where(x => x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi && x.KaynakId != null && belgeIds.Contains(x.KaynakId.Value))
                .ExecuteDeleteAsync();
            await dbContext.SatisBelgeleri.Where(x => belgeIds.Contains(x.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IadeEdilenBelgeId, (int?)null));
            await dbContext.SatisBelgeleri.Where(x => belgeIds.Contains(x.Id)).ExecuteDeleteAsync();
        }
        if (fisIds.Count > 0)
        {
            await dbContext.MuhasebeFisSatirlari.Where(x => fisIds.Contains(x.MuhasebeFisId)).ExecuteDeleteAsync();
            await dbContext.MuhasebeFisler.Where(x => fisIds.Contains(x.Id)).ExecuteDeleteAsync();
        }

        await dbContext.KurumFaturaNumaraSayaclari.Where(x => x.KurumId == kurumId).ExecuteDeleteAsync();
        await dbContext.MuhasebeDonemler.Where(x => x.TesisId == tesisId).ExecuteDeleteAsync();
        await dbContext.CariKartlar.Where(x => x.TesisId == tesisId).ExecuteDeleteAsync();
        await dbContext.MuhasebeHesapPlanlari.Where(x => x.Kod != null && x.Kod.Contains(uniqueSuffix)).ExecuteDeleteAsync();
        await dbContext.Tesisler.Where(x => x.Id == tesisId).ExecuteDeleteAsync();
        await dbContext.Iller.Where(x => x.Id == ilId).ExecuteDeleteAsync();
        await dbContext.Kurumlar.Where(x => x.Id == kurumId).ExecuteDeleteAsync();
    }
}
