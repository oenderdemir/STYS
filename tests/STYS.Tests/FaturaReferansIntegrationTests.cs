using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Gelen belgelerin karşı taraf fatura numarası (KarsiTarafFaturaNo) ve iade faturalarının iade
/// edilen asıl faturaya referansı (IadeEdilenBelgeId) için GERÇEK SQL Server üzerinde çalışan
/// entegrasyon testleri.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class FaturaReferansIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "FATREF-204";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _musteriKartId;
    private int _musteriKart2Id;
    private int _tedarikciKartId;
    private int _tedarikciKart2Id;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return;
        }

        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (kurum, il, tesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, _uniqueSuffix);
        _kurumId = kurum.Id;
        _ilId = il.Id;
        _tesisId = tesis.Id;

        var gelirHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GelirSatis, "GELIR", _tesisId);
        var kdvSatisHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVHesaplanan, "KDVS", _tesisId);
        var kdvAlisHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVIndirilecek, "KDVA", _tesisId);
        var giderHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, "GIDER", _tesisId);
        var satisIadeHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.SatisIade, "IADE", _tesisId);
        var stokHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.StokTicariMal, "STOK", _tesisId);
        var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "MUS", _tesisId);
        var musteriHesap2 = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "MUS2", _tesisId);
        var tedarikciHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "TED", _tesisId);
        var tedarikciHesap2 = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "TED2", _tesisId);
        dbContext.MuhasebeHesapPlanlari.AddRange(
            gelirHesap, kdvSatisHesap, kdvAlisHesap, giderHesap, satisIadeHesap, stokHesap,
            musteriHesap, musteriHesap2, tedarikciHesap, tedarikciHesap2);
        await dbContext.SaveChangesAsync();

        var musteriKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "MUS", CariKartTipleri.Musteri, _tesisId, musteriHesap.Id);
        var musteriKart2 = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "MUS2", CariKartTipleri.Musteri, _tesisId, musteriHesap2.Id);
        var tedarikciKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "TED", CariKartTipleri.Tedarikci, _tesisId, tedarikciHesap.Id);
        tedarikciKart.VergiNoTckn = "1111111111";
        var tedarikciKart2 = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "TED2", CariKartTipleri.Tedarikci, _tesisId, tedarikciHesap2.Id);
        tedarikciKart2.VergiNoTckn = "2222222222";
        dbContext.CariKartlar.AddRange(musteriKart, musteriKart2, tedarikciKart, tedarikciKart2);
        await dbContext.SaveChangesAsync();
        _musteriKartId = musteriKart.Id;
        _musteriKart2Id = musteriKart2.Id;
        _tedarikciKartId = tedarikciKart.Id;
        _tedarikciKart2Id = tedarikciKart2.Id;

        dbContext.MuhasebeDonemler.Add(new MuhasebeDonem
        {
            TesisId = _tesisId, MaliYil = 2026, DonemNo = 1,
            BaslangicTarihi = new DateTime(2026, 1, 1), BitisTarihi = new DateTime(2026, 12, 31), KapaliMi = false
        });
        await dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString) || _kurumId <= 0)
        {
            return;
        }

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await CleanupKurumAsync(dbContext, _kurumId, _tesisId, _ilId, _uniqueSuffix);
    }

    // ─────────────────────────────────────────────────────────────
    // Yardımcılar
    // ─────────────────────────────────────────────────────────────

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
            // Self-referencing FK: iade belgeleri asıl faturaları önce silinemeyecek şekilde
            // referans eder - önce IadeEdilenBelgeId'yi temizle, sonra hepsini tek seferde sil.
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

    private async Task<SatisBelgesiDto> SeedOnaylanmisSatisFaturasiVeKesAsync(
        StysAppDbContext dbContext, DateTime belgeTarihi, string seriKodu, int? musteriKartId = null, decimal miktar = 1m)
    {
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, donemService);

        var created = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            TesisId = _tesisId,
            CariKartId = musteriKartId ?? _musteriKartId,
            BelgeTarihi = belgeTarihi,
            MusteriAdSoyad = "Test Musteri " + _uniqueSuffix,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Test satir", Miktar = miktar, BirimFiyat = 1000m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value);
        await service.MuhasebeOnaylaAsync(created.Id!.Value);
        await fisService.MuhasebeFisiOlusturAsync(created.Id.Value);

        var sayacVarMi = await dbContext.KurumFaturaNumaraSayaclari.AsNoTracking()
            .AnyAsync(x => !x.IsDeleted && x.KurumId == _kurumId && x.MaliYil == belgeTarihi.Year && x.SeriKodu == seriKodu);
        if (!sayacVarMi)
        {
            dbContext.KurumFaturaNumaraSayaclari.Add(new KurumFaturaNumaraSayaci
            {
                KurumId = _kurumId, MaliYil = belgeTarihi.Year, SeriKodu = seriKodu, SonNumara = 0, AktifMi = true
            });
            await dbContext.SaveChangesAsync();
        }

        return await service.FaturaKesAsync(created.Id.Value, new FaturaKesRequest { SeriKodu = seriKodu });
    }

    private async Task<SatisBelgesiDto> SeedOnaylanmisAlisFaturasiAsync(
        StysAppDbContext dbContext, DateTime belgeTarihi, string? karsiTarafFaturaNo = null, int? tedarikciKartId = null, bool fisOlustur = true)
    {
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
            TesisId = _tesisId,
            CariKartId = tedarikciKartId ?? _tedarikciKartId,
            BelgeTarihi = belgeTarihi,
            MusteriAdSoyad = "Test Tedarikci " + _uniqueSuffix,
            KarsiTarafFaturaNo = karsiTarafFaturaNo ?? $"TED-{Guid.NewGuid():N}"[..20],
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Test satir", Miktar = 1, BirimFiyat = 500m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value);
        await service.MuhasebeOnaylaAsync(created.Id!.Value);

        if (fisOlustur)
        {
            var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
            var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, donemService);
            return await fisService.MuhasebeFisiOlusturAsync(created.Id.Value);
        }

        return await service.GetByIdAsync(created.Id.Value);
    }

    // ─────────────────────────────────────────────────────────────
    // KarsiTarafFaturaNo
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task AlisFaturasi_KarsiTarafFaturaNoIleOlusturulurVeOkunur_BasVeSonBosluklarTemizlenir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKartId,
            BelgeTarihi = new DateTime(2026, 3, 10),
            MusteriAdSoyad = "Tedarikci",
            KarsiTarafFaturaNo = "  TED-0001  ",
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Test", Miktar = 1, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });

        Assert.Equal("TED-0001", created.KarsiTarafFaturaNo);

        var okunan = await service.GetByIdAsync(created.Id!.Value);
        Assert.Equal("TED-0001", okunan.KarsiTarafFaturaNo);
    }

    [IntegrationFact]
    public async Task AlisFaturasi_BosKarsiTarafFaturaNoIle_OnayAsamasindaReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKartId,
            BelgeTarihi = new DateTime(2026, 3, 10),
            MusteriAdSoyad = "Tedarikci",
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Test", Miktar = 1, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.MuhasebeOnayinaGonderAsync(created.Id!.Value));
        Assert.Contains("karşı taraf fatura numarası zorunludur", ex.Message);

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var belgeSonHal = await verifyContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == created.Id);
        Assert.Equal(SatisBelgesiDurumu.Taslak, belgeSonHal.Durum);
    }

    [IntegrationTheory]
    [InlineData("TED\t0001")]
    [InlineData("TED\n0001")]
    [InlineData("TED\r0001")]
    public async Task AlisFaturasi_KontrolKarakteriIcerenKarsiTarafFaturaNo_CreateAsyncReddeder(string bozukDeger)
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKartId,
            BelgeTarihi = new DateTime(2026, 3, 10),
            MusteriAdSoyad = "Tedarikci",
            KarsiTarafFaturaNo = bozukDeger,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Test", Miktar = 1, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        }));

        Assert.Contains("kontrol karakteri", ex.Message);
    }

    [IntegrationFact]
    public async Task SatisFaturasi_KarsiTarafFaturaNoIle_CreateAsyncReddeder()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 3, 10),
            MusteriAdSoyad = "Musteri",
            KarsiTarafFaturaNo = "MUS-0001",
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Test", Miktar = 1, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        }));

        Assert.Contains("yalnızca alış faturası veya satış iade faturası", ex.Message);
    }

    [IntegrationFact]
    public async Task AlisFaturasi_AyniKurumAyniCariAyniNumara_ReddedilirVeHicbirSeyDegismez()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var numara = $"TED-{Guid.NewGuid():N}"[..20];

        var ilk = await SeedOnaylanmisAlisFaturasiAsync(dbContext, new DateTime(2026, 3, 1), numara, fisOlustur: false);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKartId,
            BelgeTarihi = new DateTime(2026, 3, 2),
            MusteriAdSoyad = "Tedarikci",
            KarsiTarafFaturaNo = numara,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Test", Miktar = 1, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        }));
        Assert.Equal(409, ex.ErrorCode);

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var kayitSayisi = await verifyContext.SatisBelgeleri.AsNoTracking()
            .CountAsync(x => x.KurumId == _kurumId && x.CariKartId == _tedarikciKartId && x.KarsiTarafFaturaNo == numara);
        Assert.Equal(1, kayitSayisi);

        var ilkSonHal = await verifyContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == ilk.Id);
        Assert.Equal(numara, ilkSonHal.KarsiTarafFaturaNo);
    }

    [IntegrationFact]
    public async Task AlisFaturasi_AyniNumaraFarkliCariKart_KabulEdilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var numara = $"TED-{Guid.NewGuid():N}"[..20];

        var b1 = await SeedOnaylanmisAlisFaturasiAsync(dbContext, new DateTime(2026, 3, 1), numara, _tedarikciKartId, fisOlustur: false);
        var b2 = await SeedOnaylanmisAlisFaturasiAsync(dbContext, new DateTime(2026, 3, 1), numara, _tedarikciKart2Id, fisOlustur: false);

        Assert.Equal(numara, b1.KarsiTarafFaturaNo);
        Assert.Equal(numara, b2.KarsiTarafFaturaNo);
    }

    [IntegrationFact]
    public async Task AlisFaturasi_AyniNumaraEszamanliIkiFarkliBelgeyeAyniCaride_YalnizcaBiriBasarili()
    {
        var numara = $"TED-{Guid.NewGuid():N}"[..20];

        async Task<(bool basarili, int? errorCode)> DeneAsync()
        {
            try
            {
                await using var ctx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
                await SeedOnaylanmisAlisFaturasiAsync(ctx, new DateTime(2026, 3, 1), numara, _tedarikciKartId, fisOlustur: false);
                return (true, null);
            }
            catch (BaseException ex)
            {
                return (false, ex.ErrorCode);
            }
        }

        var t1 = DeneAsync();
        var t2 = DeneAsync();
        var sonuclar = await Task.WhenAll(t1, t2);

        Assert.Single(sonuclar, x => x.basarili);
        Assert.Single(sonuclar, x => !x.basarili);

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var kayitSayisi = await verifyContext.SatisBelgeleri.AsNoTracking()
            .CountAsync(x => x.KurumId == _kurumId && x.CariKartId == _tedarikciKartId && x.KarsiTarafFaturaNo == numara);
        Assert.Equal(1, kayitSayisi);
    }

    // ─────────────────────────────────────────────────────────────
    // SatisIadeFaturasi -> SatisFaturasi referansı
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task SatisIadeFaturasi_GecerliAsilFaturaIle_OnayaGonderilebilirVeDtoAsilBilgileriniGosterir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var asil = await SeedOnaylanmisSatisFaturasiVeKesAsync(dbContext, new DateTime(2026, 3, 1), "SIF");

        var created = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 3, 5),
            MusteriAdSoyad = "Musteri",
            KarsiTarafFaturaNo = $"MUS-{Guid.NewGuid():N}"[..20],
            IadeEdilenBelgeId = asil.Id,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Iade", Miktar = 1, BirimFiyat = 1000m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m,
                    KaynakSatirId = asil.Satirlar[0].Id!.Value.ToString()
                }
            ]
        });

        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value);

        var okunan = await service.GetByIdAsync(created.Id.Value);
        Assert.Equal(asil.BelgeNo, okunan.IadeEdilenBelgeNo);
        Assert.Equal(asil.ResmiFaturaNo, okunan.IadeEdilenFaturaNo);
        Assert.Equal(asil.BelgeTarihi, okunan.IadeEdilenBelgeTarihi);
        Assert.Equal(SatisBelgesiTipi.SatisFaturasi, okunan.IadeEdilenBelgeTipi);
    }

    [IntegrationFact]
    public async Task SatisIadeFaturasi_IadeEdilenBelgeIdEksik_OnayaGonderilirkenReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 3, 5),
            MusteriAdSoyad = "Musteri",
            KarsiTarafFaturaNo = $"MUS-{Guid.NewGuid():N}"[..20],
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Iade", Miktar = 1, BirimFiyat = 1000m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m,
                    // IadeEdilenBelgeId bu testte KASTEN verilmiyor - KaynakSatirId'nin biçim/varlık
                    // kontrolü (ValidateIadeSatirlariAsync) yine de geçmelidir; sahiplik/miktar
                    // kontrolleri asıl fatura seçilene kadar ERTELENİR.
                    KaynakSatirId = "1"
                }
            ]
        });

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.MuhasebeOnayinaGonderAsync(created.Id!.Value));
        Assert.Contains("iade edilen belge referansı zorunludur", ex.Message);
    }

    [IntegrationFact]
    public async Task SatisIadeFaturasi_AsilBelgeSatisFaturasiDegilse_Reddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var yanlisAsil = await SeedOnaylanmisAlisFaturasiAsync(dbContext, new DateTime(2026, 3, 1), fisOlustur: false);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 3, 5),
            MusteriAdSoyad = "Musteri",
            KarsiTarafFaturaNo = $"MUS-{Guid.NewGuid():N}"[..20],
            IadeEdilenBelgeId = yanlisAsil.Id,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Iade", Miktar = 1, BirimFiyat = 1000m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        }));

        Assert.Contains("SatisFaturasi", ex.Message);
    }

    [IntegrationFact]
    public async Task SatisIadeFaturasi_FarkliCariKart_Reddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var asil = await SeedOnaylanmisSatisFaturasiVeKesAsync(dbContext, new DateTime(2026, 3, 1), "SFC");

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKart2Id,
            BelgeTarihi = new DateTime(2026, 3, 5),
            MusteriAdSoyad = "Musteri2",
            KarsiTarafFaturaNo = $"MUS-{Guid.NewGuid():N}"[..20],
            IadeEdilenBelgeId = asil.Id,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Iade", Miktar = 1, BirimFiyat = 1000m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        }));

        Assert.Contains("cari kartı", ex.Message);
    }

    [IntegrationFact]
    public async Task SatisIadeFaturasi_AsilBelgeFaturaKesildiDegilse_Reddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, donemService);

        var asilCreated = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 3, 1),
            MusteriAdSoyad = "Musteri",
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Satis", Miktar = 1, BirimFiyat = 1000m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });
        await service.MuhasebeOnayinaGonderAsync(asilCreated.Id!.Value);
        await service.MuhasebeOnaylaAsync(asilCreated.Id!.Value);
        await fisService.MuhasebeFisiOlusturAsync(asilCreated.Id.Value);
        // Bilerek FaturaKesAsync ÇAĞRILMADI - asıl belge hâlâ MuhasebeOnaylandi durumunda.

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 3, 5),
            MusteriAdSoyad = "Musteri",
            KarsiTarafFaturaNo = $"MUS-{Guid.NewGuid():N}"[..20],
            IadeEdilenBelgeId = asilCreated.Id,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Iade", Miktar = 1, BirimFiyat = 1000m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        }));

        Assert.Contains("FaturaKesildi", ex.Message);
    }

    [IntegrationFact]
    public async Task SatisIadeFaturasi_IadeTarihiAsilFaturadanEski_Reddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var asil = await SeedOnaylanmisSatisFaturasiVeKesAsync(dbContext, new DateTime(2026, 3, 10), "SFE");

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 3, 1), // asıldan ÖNCE
            MusteriAdSoyad = "Musteri",
            KarsiTarafFaturaNo = $"MUS-{Guid.NewGuid():N}"[..20],
            IadeEdilenBelgeId = asil.Id,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Iade", Miktar = 1, BirimFiyat = 1000m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        }));

        Assert.Contains("eski olamaz", ex.Message);
    }

    [IntegrationFact]
    public async Task Belge_KendisiniIadeEdilenBelgeOlarakGosteremez()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 3, 5),
            MusteriAdSoyad = "Musteri",
            KarsiTarafFaturaNo = $"MUS-{Guid.NewGuid():N}"[..20],
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Iade", Miktar = 1, BirimFiyat = 1000m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m,
                    KaynakSatirId = "1"
                }
            ]
        });

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.UpdateAsync(created.Id!.Value, new UpdateSatisBelgesiRequest { IadeEdilenBelgeId = created.Id }));

        Assert.Contains("kendisini iade edilen belge olarak gösteremez", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────
    // AlisIadeFaturasi -> AlisFaturasi referansı
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task AlisIadeFaturasi_GecerliAsilFaturaIle_FaturaKesAsyncCalisirVeAsilBelgeDegismez()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, donemService);

        var asilKarsiTarafNo = $"TED-{Guid.NewGuid():N}"[..20];
        var asil = await SeedOnaylanmisAlisFaturasiAsync(dbContext, new DateTime(2026, 3, 1), asilKarsiTarafNo);

        var iadeCreated = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisIadeFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKartId,
            BelgeTarihi = new DateTime(2026, 3, 5),
            MusteriAdSoyad = "Tedarikci",
            IadeEdilenBelgeId = asil.Id,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Iade", Miktar = 1, BirimFiyat = 500m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m,
                    KaynakSatirId = asil.Satirlar[0].Id!.Value.ToString()
                }
            ]
        });
        await service.MuhasebeOnayinaGonderAsync(iadeCreated.Id!.Value);
        await service.MuhasebeOnaylaAsync(iadeCreated.Id!.Value);
        await fisService.MuhasebeFisiOlusturAsync(iadeCreated.Id.Value);

        dbContext.KurumFaturaNumaraSayaclari.Add(new KurumFaturaNumaraSayaci
        {
            KurumId = _kurumId, MaliYil = 2026, SeriKodu = "AIF", SonNumara = 0, AktifMi = true
        });
        await dbContext.SaveChangesAsync();

        var sonuc = await service.FaturaKesAsync(iadeCreated.Id.Value, new FaturaKesRequest { SeriKodu = "AIF" });
        Assert.Equal(SatisBelgesiDurumu.FaturaKesildi, sonuc.Durum);
        Assert.Null(sonuc.KarsiTarafFaturaNo);

        var asilSonHal = await dbContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == asil.Id);
        Assert.Equal(asilKarsiTarafNo, asilSonHal.KarsiTarafFaturaNo);
    }

    [IntegrationFact]
    public async Task AlisIadeFaturasi_AsilAlisFaturasindaKarsiTarafFaturaNoSonradanSilinirse_FaturaKesAsyncReddeder()
    {
        // Referans GEÇERLİ bir asıl fatura ile kurulur (create/onay/fiş - hepsi başarılı); asıl
        // fatura ANCAK BUNDAN SONRA (elle) bozulur - onaya gönderme ile fatura kesme arasında asıl
        // belgenin bozulmasının SESSİZCE kabul edilmediğini kanıtlayan asıl senaryo budur (bkz. E.3/E.4).
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, donemService);

        var asil = await SeedOnaylanmisAlisFaturasiAsync(dbContext, new DateTime(2026, 3, 1));

        var iadeCreated = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisIadeFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKartId,
            BelgeTarihi = new DateTime(2026, 3, 5),
            MusteriAdSoyad = "Tedarikci",
            IadeEdilenBelgeId = asil.Id,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Iade", Miktar = 1, BirimFiyat = 500m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m,
                    KaynakSatirId = asil.Satirlar[0].Id!.Value.ToString()
                }
            ]
        });
        await service.MuhasebeOnayinaGonderAsync(iadeCreated.Id!.Value);
        await service.MuhasebeOnaylaAsync(iadeCreated.Id!.Value);
        await fisService.MuhasebeFisiOlusturAsync(iadeCreated.Id.Value);

        dbContext.KurumFaturaNumaraSayaclari.Add(new KurumFaturaNumaraSayaci
        {
            KurumId = _kurumId, MaliYil = 2026, SeriKodu = "AIE", SonNumara = 0, AktifMi = true
        });
        await dbContext.SaveChangesAsync();

        // Referans KURULDUKTAN SONRA asıl faturanın KarsiTarafFaturaNo'su elle silinir.
        var asilDb = await dbContext.SatisBelgeleri.FirstAsync(x => x.Id == asil.Id);
        asilDb.KarsiTarafFaturaNo = null;
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.FaturaKesAsync(iadeCreated.Id.Value, new FaturaKesRequest { SeriKodu = "AIE" }));
        Assert.Contains("tedarikçi fatura numarası", ex.Message);

        var sayacDb = await dbContext.KurumFaturaNumaraSayaclari.AsNoTracking()
            .FirstAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == "AIE");
        Assert.Equal(0, sayacDb.SonNumara);

        var iadeSonHal = await dbContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == iadeCreated.Id);
        Assert.Null(iadeSonHal.ResmiFaturaNo);
        Assert.Equal(SatisBelgesiDurumu.MuhasebeOnaylandi, iadeSonHal.Durum);
    }

    [IntegrationFact]
    public async Task AlisIadeFaturasi_AsilFisSonradanIptalEdilirse_FaturaKesAsyncReddeder()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, donemService);

        var asil = await SeedOnaylanmisAlisFaturasiAsync(dbContext, new DateTime(2026, 3, 1));

        var iadeCreated = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisIadeFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKartId,
            BelgeTarihi = new DateTime(2026, 3, 5),
            MusteriAdSoyad = "Tedarikci",
            IadeEdilenBelgeId = asil.Id,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Iade", Miktar = 1, BirimFiyat = 500m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m,
                    KaynakSatirId = asil.Satirlar[0].Id!.Value.ToString()
                }
            ]
        });
        await service.MuhasebeOnayinaGonderAsync(iadeCreated.Id!.Value);
        await service.MuhasebeOnaylaAsync(iadeCreated.Id!.Value);
        await fisService.MuhasebeFisiOlusturAsync(iadeCreated.Id.Value);

        dbContext.KurumFaturaNumaraSayaclari.Add(new KurumFaturaNumaraSayaci
        {
            KurumId = _kurumId, MaliYil = 2026, SeriKodu = "AI2", SonNumara = 0, AktifMi = true
        });
        await dbContext.SaveChangesAsync();

        // Referans KURULDUKTAN SONRA asıl faturanın fişi iptal edilir.
        var asilFisDb = await dbContext.MuhasebeFisler.FirstAsync(x => x.Id == asil.MuhasebeFisId!.Value);
        asilFisDb.Durum = MuhasebeFisDurumlari.Iptal;
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.FaturaKesAsync(iadeCreated.Id.Value, new FaturaKesRequest { SeriKodu = "AI2" }));
        Assert.Contains("iptal edilmiş", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────
    // Çoklu (kısmi) iade
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task AyniAsilFaturaya_IkiFarkliKismiIade_BaglanabilirVeUniqueIndexYok()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        // Asıl satır miktarı 2 - iki kısmi iade (1+1) TOPLAMDA asıl miktara tam eşit olacak
        // şekilde kurulur (bkz. SatisBelgesiService.ValidateIadeSatirlariAsync - kümülatif
        // miktar sınırı artık uygulanır, ayrıntı için görev sonuç raporuna bakınız).
        var asil = await SeedOnaylanmisSatisFaturasiVeKesAsync(dbContext, new DateTime(2026, 3, 1), "MLT", miktar: 2m);
        var asilSatirId = asil.Satirlar[0].Id!.Value;

        async Task<SatisBelgesiDto> IadeOlusturAsync()
        {
            var created = await service.CreateAsync(new CreateSatisBelgesiRequest
            {
                BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
                BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
                TesisId = _tesisId,
                CariKartId = _musteriKartId,
                BelgeTarihi = new DateTime(2026, 3, 5),
                MusteriAdSoyad = "Musteri",
                KarsiTarafFaturaNo = $"MUS-{Guid.NewGuid():N}"[..20],
                IadeEdilenBelgeId = asil.Id,
                Satirlar =
                [
                    new CreateSatisBelgesiSatiriRequest
                    {
                        SiraNo = 1, Aciklama = "Kismi iade", Miktar = 1, BirimFiyat = 1000m,
                        KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m,
                        KaynakSatirId = asilSatirId.ToString()
                    }
                ]
            });
            await service.MuhasebeOnayinaGonderAsync(created.Id!.Value);
            await service.MuhasebeOnaylaAsync(created.Id!.Value);
            return created;
        }

        var iade1 = await IadeOlusturAsync();
        var iade2 = await IadeOlusturAsync();

        Assert.NotEqual(iade1.Id, iade2.Id);

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var iadeSayisi = await verifyContext.SatisBelgeleri.AsNoTracking()
            .CountAsync(x => x.IadeEdilenBelgeId == asil.Id);
        Assert.Equal(2, iadeSayisi);
    }

    // ─────────────────────────────────────────────────────────────
    // Tenant güvenliği
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task SuperAdminDahi_BaskaKurumunBelgesiniIadeReferansiOlarakSecemez()
    {
        var uniqueSuffixB = $"{TestMarker}-B-{Guid.NewGuid():N}"[..24];
        int kurumBId = 0, ilBId = 0, tesisBId = 0;

        try
        {
            await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
            var (kurumB, ilB, tesisB) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, uniqueSuffixB);
            kurumBId = kurumB.Id; ilBId = ilB.Id; tesisBId = tesisB.Id;

            var gelirHesapB = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(uniqueSuffixB, MuhasebeAnaHesapKodlari.GelirSatis, "GELIR", tesisBId);
            var kdvHesapB = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(uniqueSuffixB, MuhasebeAnaHesapKodlari.KDVHesaplanan, "KDV", tesisBId);
            var musteriHesapB = SatisBelgesiMuhasebeTestSupport.BuildHesap(uniqueSuffixB, "MUS", tesisBId);
            dbContext.MuhasebeHesapPlanlari.AddRange(gelirHesapB, kdvHesapB, musteriHesapB);
            await dbContext.SaveChangesAsync();
            var musteriKartB = SatisBelgesiMuhasebeTestSupport.BuildCariKart(uniqueSuffixB, "MUS", CariKartTipleri.Musteri, tesisBId, musteriHesapB.Id);
            dbContext.CariKartlar.Add(musteriKartB);
            await dbContext.SaveChangesAsync();
            dbContext.MuhasebeDonemler.Add(new MuhasebeDonem
            {
                TesisId = tesisBId, MaliYil = 2026, DonemNo = 1,
                BaslangicTarihi = new DateTime(2026, 1, 1), BitisTarihi = new DateTime(2026, 12, 31), KapaliMi = false
            });
            await dbContext.SaveChangesAsync();

            var serviceB = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
            var donemServiceB = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
            var fisServiceB = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, donemServiceB);

            var asilBCreated = await serviceB.CreateAsync(new CreateSatisBelgesiRequest
            {
                BelgeNo = $"BLG-{uniqueSuffixB}-{Guid.NewGuid():N}"[..40],
                BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
                TesisId = tesisBId,
                CariKartId = musteriKartB.Id,
                BelgeTarihi = new DateTime(2026, 3, 1),
                MusteriAdSoyad = "Musteri B",
                Satirlar =
                [
                    new CreateSatisBelgesiSatiriRequest
                    {
                        SiraNo = 1, Aciklama = "Satis B", Miktar = 1, BirimFiyat = 1000m,
                        KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                    }
                ]
            });
            await serviceB.MuhasebeOnayinaGonderAsync(asilBCreated.Id!.Value);
            await serviceB.MuhasebeOnaylaAsync(asilBCreated.Id!.Value);
            await fisServiceB.MuhasebeFisiOlusturAsync(asilBCreated.Id.Value);
            dbContext.KurumFaturaNumaraSayaclari.Add(new KurumFaturaNumaraSayaci
            {
                KurumId = kurumBId, MaliYil = 2026, SeriKodu = "TNB", SonNumara = 0, AktifMi = true
            });
            await dbContext.SaveChangesAsync();
            var asilB = await serviceB.FaturaKesAsync(asilBCreated.Id.Value, new FaturaKesRequest { SeriKodu = "TNB" });

            // SuperAdmin (paylaşılan test dbContext) DAHİ kurumA belgesine kurumB'nin faturasını
            // iade referansı olarak GÖSTEREMEMELİDİR - KurumId eşitliği her koşulda zorunludur.
            var serviceA = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
            var ex = await Assert.ThrowsAsync<BaseException>(() => serviceA.CreateAsync(new CreateSatisBelgesiRequest
            {
                BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
                BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
                TesisId = _tesisId,
                CariKartId = _musteriKartId,
                BelgeTarihi = new DateTime(2026, 3, 5),
                MusteriAdSoyad = "Musteri A",
                KarsiTarafFaturaNo = $"MUS-{Guid.NewGuid():N}"[..20],
                IadeEdilenBelgeId = asilB.Id,
                Satirlar =
                [
                    new CreateSatisBelgesiSatiriRequest
                    {
                        SiraNo = 1, Aciklama = "Iade A", Miktar = 1, BirimFiyat = 1000m,
                        KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                    }
                ]
            }));

            Assert.Equal(404, ex.ErrorCode);
        }
        finally
        {
            if (kurumBId > 0)
            {
                await using var cleanupContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
                await CleanupKurumAsync(cleanupContext, kurumBId, tesisBId, ilBId, uniqueSuffixB);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Migration / DB seviyesi kontroller
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task Migration_KolonlarNullableVeConstraintlerCalisiyor()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await using var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
SELECT IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'muhasebe' AND TABLE_NAME = 'SatisBelgeleri' AND COLUMN_NAME = 'KarsiTarafFaturaNo'";
            var nullable = (string?)await cmd.ExecuteScalarAsync();
            Assert.Equal("YES", nullable);
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
SELECT IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'muhasebe' AND TABLE_NAME = 'SatisBelgeleri' AND COLUMN_NAME = 'IadeEdilenBelgeId'";
            var nullable = (string?)await cmd.ExecuteScalarAsync();
            Assert.Equal("YES", nullable);
        }

        // Giden belgeye (BelgeTipi=2 SatisFaturasi) doğrudan SQL ile KarsiTarafFaturaNo yazmak
        // check constraint tarafından reddedilmeli.
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $@"
INSERT INTO [muhasebe].[SatisBelgeleri]
    (KurumId, BelgeNo, BelgeTipi, Durum, KaynakModul, TesisId, BelgeTarihi, KurumsalMi, ToplamMatrah, ToplamKdv, GenelToplam, IsDeleted, TicariDurum, MuhasebeDurumu, FaturalamaDurumu, KarsiTarafFaturaNo)
VALUES
    ({_kurumId}, 'CKTEST-{_uniqueSuffix}', 2, 0, 1, {_tesisId}, '2026-01-01', 0, 100, 20, 120, 0, 1, 1, 2, 'YANLIS');";
            var ex = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Contains("CK_SatisBelgeleri_KarsiTarafFaturaNo_BelgeTipi", ex.Message);
        }

        // İade olmayan bir belgeye (BelgeTipi=5 AlisFaturasi) doğrudan SQL ile IadeEdilenBelgeId
        // yazmak check constraint tarafından reddedilmeli.
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $@"
INSERT INTO [muhasebe].[SatisBelgeleri]
    (KurumId, BelgeNo, BelgeTipi, Durum, KaynakModul, TesisId, BelgeTarihi, KurumsalMi, ToplamMatrah, ToplamKdv, GenelToplam, IsDeleted, TicariDurum, MuhasebeDurumu, FaturalamaDurumu, IadeEdilenBelgeId)
VALUES
    ({_kurumId}, 'CKTEST2-{_uniqueSuffix}', 5, 0, 1, {_tesisId}, '2026-01-01', 0, 100, 20, 120, 0, 1, 1, 2, 999999);";
            var ex = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Contains("CK_SatisBelgeleri_IadeEdilenBelgeId_BelgeTipi", ex.Message);
        }
    }
}
