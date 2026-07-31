using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
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
/// KarsiTarafFaturaNo ve IadeEdilenBelgeId aşamasının hardening turu — kontrol karakteri
/// doğrulaması, DB check constraint'i, unique index hata çevirisinin index'e özgü olması ve
/// UpdateAsync'in nihai değerlerle her zaman yeniden doğrulama yapması için GERÇEK SQL Server
/// üzerinde çalışan entegrasyon testleri.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class KarsiTarafFaturaNoHardeningIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "KTFNHARD-551";

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
        // IgnoreQueryFilters() ZORUNLUDUR - SatisBelgesi ITenantEntity olduğundan StysAppDbContext
        // normalde TÜM sorgulara otomatik "IsDeleted=0" filtresi ekler; bu testler soft-delete
        // edilmiş (IsDeleted=true) belgeler ÜRETTİĞİNDEN, filtre olmadan bu satırlar cleanup'ta
        // GÖRÜLMEZ ve CariKartlar/hesap planı silinirken FK ihlaline yol açar.
        var belgeIds = await dbContext.SatisBelgeleri.IgnoreQueryFilters()
            .Where(x => x.KurumId == kurumId).Select(x => x.Id).ToListAsync();
        var fisIds = new List<int>();
        if (belgeIds.Count > 0)
        {
            fisIds = await dbContext.MuhasebeFisler.IgnoreQueryFilters()
                .Where(x => x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi && x.KaynakId != null && belgeIds.Contains(x.KaynakId.Value))
                .Select(x => x.Id).ToListAsync();
            await dbContext.CariHareketler
                .Where(x => x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi && x.KaynakId != null && belgeIds.Contains(x.KaynakId.Value))
                .ExecuteDeleteAsync();
            await dbContext.SatisBelgeleri.IgnoreQueryFilters().Where(x => belgeIds.Contains(x.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IadeEdilenBelgeId, (int?)null));
            await dbContext.SatisBelgeleri.IgnoreQueryFilters().Where(x => belgeIds.Contains(x.Id)).ExecuteDeleteAsync();
        }
        if (fisIds.Count > 0)
        {
            await dbContext.MuhasebeFisSatirlari.IgnoreQueryFilters().Where(x => fisIds.Contains(x.MuhasebeFisId)).ExecuteDeleteAsync();
            await dbContext.MuhasebeFisler.IgnoreQueryFilters().Where(x => fisIds.Contains(x.Id)).ExecuteDeleteAsync();
        }

        await dbContext.KurumFaturaNumaraSayaclari.Where(x => x.KurumId == kurumId).ExecuteDeleteAsync();
        await dbContext.MuhasebeDonemler.Where(x => x.TesisId == tesisId).ExecuteDeleteAsync();
        await dbContext.CariKartlar.Where(x => x.TesisId == tesisId).ExecuteDeleteAsync();
        await dbContext.MuhasebeHesapPlanlari.Where(x => x.Kod != null && x.Kod.Contains(uniqueSuffix)).ExecuteDeleteAsync();
        await dbContext.Tesisler.Where(x => x.Id == tesisId).ExecuteDeleteAsync();
        await dbContext.Iller.Where(x => x.Id == ilId).ExecuteDeleteAsync();
        await dbContext.Kurumlar.Where(x => x.Id == kurumId).ExecuteDeleteAsync();
    }

    /// <summary>Yeni, no-tracking bir context ile belgeyi okur - "başlangıç" veya "sonuç" anlık görüntüsü almak için kullanılır.</summary>
    private static async Task<SatisBelgesi> ReadNoTrackingAsync(StysAppDbContext dbContext, int id)
        => await dbContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == id);

    /// <summary>
    /// Ret/değişmezlik testlerinde ortak kullanılan assertion: bir güncelleme reddedildiğinde (veya
    /// yalnızca ilgisiz bir alan güncellendiğinde) belgenin KİLİT alanlarının HİÇBİRİNİN
    /// değişmediğini doğrular. beklenen/guncel parametreleri her ikisi de AsNoTracking ile okunmuş
    /// olmalıdır.
    /// </summary>
    private static void AssertBelgeDegismedi(SatisBelgesi beklenen, SatisBelgesi guncel)
    {
        Assert.Equal(beklenen.BelgeTipi, guncel.BelgeTipi);
        Assert.Equal(beklenen.Durum, guncel.Durum);
        Assert.Equal(beklenen.CariKartId, guncel.CariKartId);
        Assert.Equal(beklenen.BelgeTarihi, guncel.BelgeTarihi);
        Assert.Equal(beklenen.KarsiTarafFaturaNo, guncel.KarsiTarafFaturaNo);
        Assert.Equal(beklenen.IadeEdilenBelgeId, guncel.IadeEdilenBelgeId);
        Assert.Equal(beklenen.ResmiFaturaNo, guncel.ResmiFaturaNo);
        Assert.Equal(beklenen.FaturaKesimTarihi, guncel.FaturaKesimTarihi);
        Assert.Equal(beklenen.MuhasebeFisId, guncel.MuhasebeFisId);
    }

    private async Task<SatisBelgesiDto> SeedOnaylanmisSatisFaturasiVeKesAsync(
        StysAppDbContext dbContext, DateTime belgeTarihi, string seriKodu, int? musteriKartId = null)
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
                    SiraNo = 1, Aciklama = "Test satir", Miktar = 1, BirimFiyat = 1000m,
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
    // A. Kontrol karakteri doğrulaması TRIM'DEN ÖNCE
    // ─────────────────────────────────────────────────────────────

    [IntegrationTheory]
    [InlineData("\tTED-1")]
    [InlineData("TED-1\t")]
    [InlineData("\nTED-1")]
    [InlineData("TED-1\n")]
    [InlineData("TED\r-1")]
    public async Task KarsiTarafFaturaNo_KontrolKarakteriTrimSonrasiBileKalsaReddedilir(string bozukDeger)
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
    public async Task KarsiTarafFaturaNo_NormalBasVeSonBosluk_KabulEdilirVeTemizlenir()
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
            KarsiTarafFaturaNo = "   TED-99   ",
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Test", Miktar = 1, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });

        Assert.Equal("TED-99", created.KarsiTarafFaturaNo);
    }

    // ─────────────────────────────────────────────────────────────
    // B. DB check constraint - doğrudan SQL
    // ─────────────────────────────────────────────────────────────

    [IntegrationTheory]
    [InlineData("TED-1 ")]
    [InlineData(" TED-1")]
    [InlineData("TED\t1")]
    [InlineData("TED\n1")]
    public async Task Migration_CheckConstraint_BozukKarsiTarafFaturaNoDegerleriniReddeder(string bozukDeger)
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO [muhasebe].[SatisBelgeleri] " +
            "(KurumId, BelgeNo, BelgeTipi, Durum, KaynakModul, TesisId, CariKartId, BelgeTarihi, KurumsalMi, ToplamMatrah, ToplamKdv, GenelToplam, IsDeleted, TicariDurum, MuhasebeDurumu, FaturalamaDurumu, KarsiTarafFaturaNo) " +
            "VALUES (@kurumId, @belgeNo, 5, 0, 1, @tesisId, @cariKartId, '2026-01-01', 0, 100, 20, 120, 0, 1, 1, 2, @karsiTarafFaturaNo)";
        cmd.Parameters.Add(new SqlParameter("@kurumId", _kurumId));
        cmd.Parameters.Add(new SqlParameter("@belgeNo", $"CKB-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40]));
        cmd.Parameters.Add(new SqlParameter("@tesisId", _tesisId));
        cmd.Parameters.Add(new SqlParameter("@cariKartId", _tedarikciKartId));
        cmd.Parameters.Add(new SqlParameter("@karsiTarafFaturaNo", bozukDeger));

        var ex = await Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Contains("CK_SatisBelgeleri_KarsiTarafFaturaNo_Format", ex.Message);
    }

    [IntegrationFact]
    public async Task Migration_CheckConstraint_GecerliDegerKabulEdilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var belgeNo = $"CKB-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40];
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO [muhasebe].[SatisBelgeleri] " +
                "(KurumId, BelgeNo, BelgeTipi, Durum, KaynakModul, TesisId, CariKartId, BelgeTarihi, KurumsalMi, ToplamMatrah, ToplamKdv, GenelToplam, IsDeleted, TicariDurum, MuhasebeDurumu, FaturalamaDurumu, KarsiTarafFaturaNo) " +
                "VALUES (@kurumId, @belgeNo, 5, 0, 1, @tesisId, @cariKartId, '2026-01-01', 0, 100, 20, 120, 0, 1, 1, 2, 'TED-GECERLI')";
            cmd.Parameters.Add(new SqlParameter("@kurumId", _kurumId));
            cmd.Parameters.Add(new SqlParameter("@belgeNo", belgeNo));
            cmd.Parameters.Add(new SqlParameter("@tesisId", _tesisId));
            cmd.Parameters.Add(new SqlParameter("@cariKartId", _tedarikciKartId));
            await cmd.ExecuteNonQueryAsync();
        }

        var eklenenSatir = await dbContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.BelgeNo == belgeNo);
        Assert.Equal("TED-GECERLI", eklenenSatir.KarsiTarafFaturaNo);
    }

    // ─────────────────────────────────────────────────────────────
    // C. Unique index hata çevirisi yalnızca ilgili index'e özgü olmalı
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task EszamanliAyniKarsiTarafFaturaNo_Anlasilir409Veriyor()
    {
        var numara = $"TED-{Guid.NewGuid():N}"[..20];

        async Task<(bool basarili, BaseException? hata)> DeneAsync()
        {
            try
            {
                await using var ctx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
                await SeedOnaylanmisAlisFaturasiAsync(ctx, new DateTime(2026, 3, 1), numara, _tedarikciKartId, fisOlustur: false);
                return (true, null);
            }
            catch (BaseException ex)
            {
                return (false, ex);
            }
        }

        var sonuclar = await Task.WhenAll(DeneAsync(), DeneAsync());

        Assert.Single(sonuclar, x => x.basarili);
        var basarisiz = Assert.Single(sonuclar, x => !x.basarili);
        Assert.Equal(409, basarisiz.hata!.ErrorCode);
        Assert.Contains("karşı taraf fatura numarası", basarisiz.hata.Message);
    }

    [IntegrationFact]
    public async Task EszamanliAyniBelgeNo_KarsiTarafFaturaNoMesajinaCevrilmez()
    {
        var ortakBelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40];

        async Task<Exception?> DeneAsync()
        {
            try
            {
                await using var ctx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
                var svc = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(ctx);
                await svc.CreateAsync(new CreateSatisBelgesiRequest
                {
                    BelgeNo = ortakBelgeNo,
                    BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
                    TesisId = _tesisId,
                    CariKartId = _musteriKartId,
                    BelgeTarihi = new DateTime(2026, 3, 1),
                    MusteriAdSoyad = "Musteri",
                    Satirlar =
                    [
                        new CreateSatisBelgesiSatiriRequest
                        {
                            SiraNo = 1, Aciklama = "Test", Miktar = 1, BirimFiyat = 100m,
                            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                        }
                    ]
                });
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        var sonuclar = await Task.WhenAll(DeneAsync(), DeneAsync());
        var hatalar = sonuclar.Where(x => x is not null).ToList();

        Assert.Single(hatalar);
        // Hata her ne olursa olsun (uygulama seviyesi BelgeNo duplicate kontrolü veya ham DB
        // hatası), "karşı taraf fatura numarası" mesajıyla MASKELENMEMİŞ olmalıdır.
        Assert.DoesNotContain("karşı taraf fatura numarası", hatalar[0]!.Message);
    }

    [IntegrationFact]
    public async Task EszamanliAyniKaynakId_KarsiTarafFaturaNoMesajinaCevrilmez()
    {
        var ortakKaynakId = $"KYN-{Guid.NewGuid():N}"[..20];

        async Task<Exception?> DeneAsync()
        {
            try
            {
                await using var ctx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
                var svc = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(ctx);
                await svc.CreateAsync(new CreateSatisBelgesiRequest
                {
                    BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
                    BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
                    KaynakModul = SatisKaynakModulu.Otel,
                    KaynakTipi = "TestKaynak",
                    KaynakId = ortakKaynakId,
                    TesisId = _tesisId,
                    CariKartId = _musteriKartId,
                    BelgeTarihi = new DateTime(2026, 3, 1),
                    MusteriAdSoyad = "Musteri",
                    Satirlar =
                    [
                        new CreateSatisBelgesiSatiriRequest
                        {
                            SiraNo = 1, Aciklama = "Test", Miktar = 1, BirimFiyat = 100m,
                            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                        }
                    ]
                });
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        var sonuclar = await Task.WhenAll(DeneAsync(), DeneAsync());
        var hatalar = sonuclar.Where(x => x is not null).ToList();

        Assert.Single(hatalar);
        Assert.DoesNotContain("karşı taraf fatura numarası", hatalar[0]!.Message);
    }

    // ─────────────────────────────────────────────────────────────
    // D. UpdateAsync - nihai değerlerle her zaman yeniden doğrulama
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task UpdateAsync_CariKartIdAsilIleUyumsuzHaleGetirilirse_Reddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var asil = await SeedOnaylanmisSatisFaturasiVeKesAsync(dbContext, new DateTime(2026, 3, 1), "UPC");

        var iade = await service.CreateAsync(new CreateSatisBelgesiRequest
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

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.UpdateAsync(iade.Id!.Value, new UpdateSatisBelgesiRequest { CariKartId = _musteriKart2Id }));
        Assert.Contains("cari kartı", ex.Message);

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var sonHal = await verifyContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == iade.Id);
        Assert.Equal(_musteriKartId, sonHal.CariKartId);
        Assert.Equal(asil.Id, sonHal.IadeEdilenBelgeId);
    }

    [IntegrationFact]
    public async Task UpdateAsync_BelgeTarihiAsilFaturadanEskiyeCekilirse_Reddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var asil = await SeedOnaylanmisSatisFaturasiVeKesAsync(dbContext, new DateTime(2026, 3, 10), "UPD");

        var iade = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 3, 15),
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

        // NOT: UpdateAsync'te belge.CariKartId = request.CariKartId satırı KOŞULSUZDUR (request'te
        // belirtilmezse null'a döner) - bu yüzden test edilmeyen alanları KORUMAK için CariKartId
        // burada AÇIKÇA (değişmeyen değeriyle) gönderilir.
        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.UpdateAsync(iade.Id!.Value, new UpdateSatisBelgesiRequest
            {
                CariKartId = _musteriKartId,
                BelgeTarihi = new DateTime(2026, 3, 1)
            }));
        Assert.Contains("eski olamaz", ex.Message);

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var sonHal = await verifyContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == iade.Id);
        Assert.Equal(new DateTime(2026, 3, 15), sonHal.BelgeTarihi);
    }

    [IntegrationFact]
    public async Task UpdateAsync_BelgeTipiReferansiylaUyumsuzTipeCevrilirse_Reddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var asil = await SeedOnaylanmisSatisFaturasiVeKesAsync(dbContext, new DateTime(2026, 3, 1), "UPT");

        var iade = await service.CreateAsync(new CreateSatisBelgesiRequest
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

        // BelgeTipi'ni SatisFaturasi'ye çevirmeye çalışıyor - hâlâ KarsiTarafFaturaNo VE
        // IadeEdilenBelgeId taşıdığından (istekte açıkça kaldırılmadığından) reddedilmeli.
        // KarsiTarafFaturaNo kontrolü IadeEdilenBelgeId kontrolünden ÖNCE çalıştığından, ilk
        // yakalanan uyumsuzluk KarsiTarafFaturaNo'nunkidir - ikisi de aynı kökten (yön uygunluğu)
        // geçerli bir ret nedenidir.
        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.UpdateAsync(iade.Id!.Value, new UpdateSatisBelgesiRequest
            {
                CariKartId = _musteriKartId,
                BelgeTipi = SatisBelgesiTipi.SatisFaturasi
            }));
        Assert.Contains("Karşı taraf fatura numarası yalnızca alış faturası veya satış iade faturası", ex.Message);

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var sonHal = await verifyContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == iade.Id);
        Assert.Equal(SatisBelgesiTipi.SatisIadeFaturasi, sonHal.BelgeTipi);
    }

    [IntegrationFact]
    public async Task UpdateAsync_AsilBelgeSonradanGecersizHaleGelirse_IlgisizAlanGuncellemesiBileReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var asil = await SeedOnaylanmisAlisFaturasiAsync(dbContext, new DateTime(2026, 3, 1));

        var iade = await service.CreateAsync(new CreateSatisBelgesiRequest
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

        // Asıl belge REFERANS KURULDUKTAN SONRA bozulur (KarsiTarafFaturaNo silinir).
        var asilDb = await dbContext.SatisBelgeleri.FirstAsync(x => x.Id == asil.Id);
        asilDb.KarsiTarafFaturaNo = null;
        await dbContext.SaveChangesAsync();

        // İade belgesinde IadeEdilenBelgeId'yle HİÇ İLGİSİ OLMAYAN bir alan (Aciklama) güncellenmeye
        // çalışılıyor - yine de nihai IadeEdilenBelgeId hâlâ mevcut olduğundan yeniden doğrulama
        // tetiklenir ve reddedilir.
        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.UpdateAsync(iade.Id!.Value, new UpdateSatisBelgesiRequest
            {
                CariKartId = _tedarikciKartId,
                Aciklama = "Yeni aciklama"
            }));
        Assert.Contains("tedarikçi fatura numarası", ex.Message);

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var sonHal = await verifyContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == iade.Id);
        Assert.Null(sonHal.Aciklama);
    }

    [IntegrationFact]
    public async Task UpdateAsync_KarsiTarafFaturaNoluBelgedeCariKartIdDegistirilirse_NihaiCariIleDuplicateKontrolTekrarlanir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var numara = $"TED-{Guid.NewGuid():N}"[..20];

        // tedarikci2 altında ZATEN aynı numarayla bir belge var (Taslak - UpdateAsync yalnızca
        // Taslak/Reddedildi durumundaki belgeleri güncelleyebildiğinden onaya GÖNDERİLMEZ).
        await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKart2Id,
            BelgeTarihi = new DateTime(2026, 3, 1),
            MusteriAdSoyad = "Tedarikci2",
            KarsiTarafFaturaNo = numara,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Test", Miktar = 1, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });

        // tedarikci1 altında AYNI numarayla ikinci bir belge (farklı cari olduğu için şu an geçerli).
        var belge1 = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKartId,
            BelgeTarihi = new DateTime(2026, 3, 1),
            MusteriAdSoyad = "Tedarikci1",
            KarsiTarafFaturaNo = numara,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Test", Miktar = 1, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });

        // belge1'in cari kartını tedarikci2'ye taşımaya çalış - artık ÇAKIŞIYOR, reddedilmeli.
        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.UpdateAsync(belge1.Id!.Value, new UpdateSatisBelgesiRequest { CariKartId = _tedarikciKart2Id }));
        Assert.Contains("zaten kayıtlı", ex.Message);

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var belge1SonHal = await verifyContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == belge1.Id);
        Assert.Equal(_tedarikciKartId, belge1SonHal.CariKartId);
    }

    // ─────────────────────────────────────────────────────────────
    // E. Eksik kabul testleri
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task AyniKarsiTarafFaturaNo_FarkliKurumda_KabulEdilir()
    {
        var uniqueSuffixB = $"{TestMarker}-B-{Guid.NewGuid():N}"[..24];
        int kurumBId = 0, ilBId = 0, tesisBId = 0;
        var numara = $"TED-{Guid.NewGuid():N}"[..20];

        try
        {
            await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
            var belgeA = await SeedOnaylanmisAlisFaturasiAsync(dbContext, new DateTime(2026, 3, 1), numara, fisOlustur: false);

            var (kurumB, ilB, tesisB) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, uniqueSuffixB);
            kurumBId = kurumB.Id; ilBId = ilB.Id; tesisBId = tesisB.Id;
            var tedarikciHesapB = SatisBelgesiMuhasebeTestSupport.BuildHesap(uniqueSuffixB, "TED", tesisBId);
            dbContext.MuhasebeHesapPlanlari.Add(tedarikciHesapB);
            await dbContext.SaveChangesAsync();
            var tedarikciKartB = SatisBelgesiMuhasebeTestSupport.BuildCariKart(uniqueSuffixB, "TED", CariKartTipleri.Tedarikci, tesisBId, tedarikciHesapB.Id);
            tedarikciKartB.VergiNoTckn = "9999999999";
            dbContext.CariKartlar.Add(tedarikciKartB);
            await dbContext.SaveChangesAsync();

            var serviceB = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
            var belgeB = await serviceB.CreateAsync(new CreateSatisBelgesiRequest
            {
                BelgeNo = $"BLG-{uniqueSuffixB}-{Guid.NewGuid():N}"[..40],
                BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
                TesisId = tesisBId,
                CariKartId = tedarikciKartB.Id,
                BelgeTarihi = new DateTime(2026, 3, 1),
                MusteriAdSoyad = "Tedarikci B",
                KarsiTarafFaturaNo = numara,
                Satirlar =
                [
                    new CreateSatisBelgesiSatiriRequest
                    {
                        SiraNo = 1, Aciklama = "Test", Miktar = 1, BirimFiyat = 100m,
                        KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                    }
                ]
            });

            Assert.Equal(numara, belgeA.KarsiTarafFaturaNo);
            Assert.Equal(numara, belgeB.KarsiTarafFaturaNo);
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

    [IntegrationFact]
    public async Task UpdateAsync_NullKarsiTarafFaturaNo_MevcutDegeriKorur()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var numara = $"TED-{Guid.NewGuid():N}"[..20];

        // UpdateAsync yalnızca Taslak/Reddedildi durumundaki belgeleri güncelleyebilir - bu yüzden
        // doğrudan Taslak bir belge oluşturulur (gereksiz bir onaylı asıl fatura seed'i YOKTUR).
        var taslak = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKartId,
            BelgeTarihi = new DateTime(2026, 3, 1),
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
        });

        var oncekiDb = await ReadNoTrackingAsync(dbContext, taslak.Id!.Value);

        var guncellenen = await service.UpdateAsync(taslak.Id!.Value, new UpdateSatisBelgesiRequest { Aciklama = "Yeni aciklama" });
        Assert.Equal(numara, guncellenen.KarsiTarafFaturaNo);

        // Yeni, no-tracking bir context ile: KarsiTarafFaturaNo VE CariKartId ikisi de korunmuş olmalı.
        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var sonrakiDb = await ReadNoTrackingAsync(verifyContext, taslak.Id.Value);
        Assert.Equal(numara, sonrakiDb.KarsiTarafFaturaNo);
        Assert.Equal(oncekiDb.CariKartId, sonrakiDb.CariKartId);
        Assert.Equal("Yeni aciklama", sonrakiDb.Aciklama);
    }

    [IntegrationFact]
    public async Task UpdateAsync_WhitespaceKarsiTarafFaturaNo_TaslaktaAcikcaTemizler()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var taslak = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKartId,
            BelgeTarihi = new DateTime(2026, 3, 1),
            MusteriAdSoyad = "Tedarikci",
            KarsiTarafFaturaNo = $"TED-{Guid.NewGuid():N}"[..20],
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Test", Miktar = 1, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });

        var guncellenen = await service.UpdateAsync(taslak.Id!.Value, new UpdateSatisBelgesiRequest { KarsiTarafFaturaNo = "   " });
        Assert.Null(guncellenen.KarsiTarafFaturaNo);
    }

    [IntegrationFact]
    public async Task UpdateAsync_IadeEdilenBelgeReferansiKaldir_CalisirVeIkisiBirlikteReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var asil = await SeedOnaylanmisSatisFaturasiVeKesAsync(dbContext, new DateTime(2026, 3, 1), "RMV");

        var iade = await service.CreateAsync(new CreateSatisBelgesiRequest
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

        // Hem ID hem kaldırma talebi birlikte reddedilir.
        var ex = await Assert.ThrowsAsync<BaseException>(() => service.UpdateAsync(iade.Id!.Value, new UpdateSatisBelgesiRequest
        {
            IadeEdilenBelgeId = asil.Id,
            IadeEdilenBelgeReferansiKaldir = true
        }));
        Assert.Contains("birlikte gönderilemez", ex.Message);

        // Yalnızca kaldırma talebi başarıyla çalışır.
        var guncellenen = await service.UpdateAsync(iade.Id!.Value, new UpdateSatisBelgesiRequest { IadeEdilenBelgeReferansiKaldir = true });
        Assert.Null(guncellenen.IadeEdilenBelgeId);
    }

    [IntegrationFact]
    public async Task SatisIadeFaturasi_SoftDeleteEdilmisAsilBelge_Reddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var asil = await SeedOnaylanmisSatisFaturasiVeKesAsync(dbContext, new DateTime(2026, 3, 1), "SFD");
        var asilDb = await dbContext.SatisBelgeleri.FirstAsync(x => x.Id == asil.Id);
        asilDb.IsDeleted = true;
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(new CreateSatisBelgesiRequest
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
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        }));

        // Soft-delete edilmiş belge, StysAppDbContext'in global sorgu filtresi (IsDeleted=0)
        // nedeniyle _db.SatisBelgeleri sorgusunda ZATEN görünmez - "silinmiş" dalına hiç
        // ulaşılmaz, doğrudan "bulunamadı" ile sonuçlanır (varlık bilgisi sızdırmayan, en az
        // bilgi veren, KABUL EDİLEBİLİR bir ret şeklidir).
        Assert.Contains("bulunamadı", ex.Message);
    }

    [IntegrationFact]
    public async Task SatisIadeFaturasi_AsilFisSilinmisse_Reddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var asil = await SeedOnaylanmisSatisFaturasiVeKesAsync(dbContext, new DateTime(2026, 3, 1), "SFS");
        var asilFisDb = await dbContext.MuhasebeFisler.FirstAsync(x => x.Id == asil.MuhasebeFisId!.Value);
        asilFisDb.IsDeleted = true;
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(new CreateSatisBelgesiRequest
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
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        }));

        // MuhasebeFis de global sorgu filtresine (IsDeleted=0) tabidir - soft-delete edilmiş fiş
        // sorguda hiç görünmez, "bulunamadı" ile sonuçlanır (ValidateMuhasebeFisDurumu'nun kendi
        // IsDeleted kontrolüne hiç ulaşılmaz).
        Assert.Contains("bulunamadı", ex.Message);
    }

    [IntegrationFact]
    public async Task SatisIadeFaturasi_AsilFisTersKayitsa_Reddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var asil = await SeedOnaylanmisSatisFaturasiVeKesAsync(dbContext, new DateTime(2026, 3, 1), "SFT");
        var asilFisDb = await dbContext.MuhasebeFisler.FirstAsync(x => x.Id == asil.MuhasebeFisId!.Value);
        asilFisDb.Durum = MuhasebeFisDurumlari.TersKayit;
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(new CreateSatisBelgesiRequest
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
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        }));

        Assert.Contains("ters kayıt fişidir", ex.Message);
    }

    [IntegrationFact]
    public async Task SatisIadeFaturasi_AsilResmiFaturaNoEksikse_Reddedilir()
    {
        // FaturaKesildi + ResmiFaturaNo/FaturaKesimTarihi bir arada üretilir - eksik durumu elle
        // simüle edilir (veri tutarsızlığı senaryosu).
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var asil = await SeedOnaylanmisSatisFaturasiVeKesAsync(dbContext, new DateTime(2026, 3, 1), "SFR");
        var asilDb = await dbContext.SatisBelgeleri.FirstAsync(x => x.Id == asil.Id);
        asilDb.ResmiFaturaNo = null;
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(new CreateSatisBelgesiRequest
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
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        }));

        Assert.Contains("resmî fatura numarası", ex.Message);
    }

    [IntegrationFact]
    public async Task SatisIadeFaturasi_AsilFaturaKesimTarihiEksikse_Reddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var asil = await SeedOnaylanmisSatisFaturasiVeKesAsync(dbContext, new DateTime(2026, 3, 1), "SFK");
        var asilDb = await dbContext.SatisBelgeleri.FirstAsync(x => x.Id == asil.Id);
        asilDb.FaturaKesimTarihi = null;
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(new CreateSatisBelgesiRequest
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
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        }));

        Assert.Contains("fatura kesim tarihi", ex.Message);
    }

    [IntegrationFact]
    public async Task AlisIadeFaturasi_IadeEdilenBelgeIdEksik_OnayaGonderilirkenReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var created = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisIadeFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKartId,
            BelgeTarihi = new DateTime(2026, 3, 5),
            MusteriAdSoyad = "Tedarikci",
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Iade", Miktar = 1, BirimFiyat = 500m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m,
                    KaynakSatirId = "1"
                }
            ]
        });

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.MuhasebeOnayinaGonderAsync(created.Id!.Value));
        Assert.Contains("iade edilen belge referansı zorunludur", ex.Message);
    }

    [IntegrationFact]
    public async Task AlisIadeFaturasi_AsilBelgeAlisFaturasiDegilse_Reddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var yanlisAsil = await SeedOnaylanmisSatisFaturasiVeKesAsync(dbContext, new DateTime(2026, 3, 1), "AIT");

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisIadeFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKartId,
            BelgeTarihi = new DateTime(2026, 3, 5),
            MusteriAdSoyad = "Tedarikci",
            IadeEdilenBelgeId = yanlisAsil.Id,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Iade", Miktar = 1, BirimFiyat = 500m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        }));

        Assert.Contains("AlisFaturasi", ex.Message);
    }

    [IntegrationFact]
    public async Task AlisIadeFaturasi_FarkliTedarikci_Reddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var asil = await SeedOnaylanmisAlisFaturasiAsync(dbContext, new DateTime(2026, 3, 1), tedarikciKartId: _tedarikciKartId);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisIadeFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKart2Id,
            BelgeTarihi = new DateTime(2026, 3, 5),
            MusteriAdSoyad = "Tedarikci2",
            IadeEdilenBelgeId = asil.Id,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Iade", Miktar = 1, BirimFiyat = 500m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        }));

        Assert.Contains("cari kartı", ex.Message);
    }

    [IntegrationFact]
    public async Task Migration_SelfFkVeRestrictDavranisiDogrulanir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var asil = await SeedOnaylanmisSatisFaturasiVeKesAsync(dbContext, new DateTime(2026, 3, 1), "FKR");

        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        // Referans veren en az bir çocuk satır oluştur.
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var iade = await service.CreateAsync(new CreateSatisBelgesiRequest
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

        // Asıl faturayı doğrudan SQL ile SİLMEYE (fiziksel DELETE) çalış - RESTRICT nedeniyle FK
        // ihlali ile reddedilmeli (iade satırı hâlâ referans veriyor).
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM [muhasebe].[SatisBelgeleri] WHERE Id = @id";
        cmd.Parameters.Add(new SqlParameter("@id", asil.Id));

        var ex = await Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Contains("FK_SatisBelgeleri_SatisBelgeleri_IadeEdilenBelgeId", ex.Message);

        var iadeHalaVarMi = await dbContext.SatisBelgeleri.AsNoTracking().AnyAsync(x => x.Id == iade.Id);
        Assert.True(iadeHalaVarMi);
    }

    [IntegrationFact]
    public async Task Migration_FiltreliUniqueIndexTanimiDogrulanir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
SELECT i.is_unique, i.filter_definition, STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal) AS kolonlar
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.name = 'IX_SatisBelgeleri_KurumId_CariKartId_KarsiTarafFaturaNo'
GROUP BY i.is_unique, i.filter_definition";
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "IX_SatisBelgeleri_KurumId_CariKartId_KarsiTarafFaturaNo index'i bulunamadı.");

        var isUnique = reader.GetBoolean(0);
        var filterDefinition = reader.GetString(1);
        var kolonlar = reader.GetString(2);

        Assert.True(isUnique);
        Assert.Equal("KurumId,CariKartId,KarsiTarafFaturaNo", kolonlar);
        Assert.Contains("[IsDeleted]=(0)", filterDefinition);
        Assert.Contains("[KarsiTarafFaturaNo] IS NOT NULL", filterDefinition);
    }

    [IntegrationFact]
    public async Task SoftDeleteEdilmisBelge_AyniHariciNumaraninYenidenKullanilmasiniEngellemez()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var numara = $"TED-{Guid.NewGuid():N}"[..20];

        var ilkTaslak = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKartId,
            BelgeTarihi = new DateTime(2026, 3, 1),
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
        });
        await service.DeleteAsync(ilkTaslak.Id!.Value);

        var ikinci = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKartId,
            BelgeTarihi = new DateTime(2026, 3, 1),
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
        });

        Assert.Equal(numara, ikinci.KarsiTarafFaturaNo);
    }

    // ─────────────────────────────────────────────────────────────
    // A.1/A.2 - Kısmi güncelleme (yalnızca Aciklama): CariKartId ve
    // KarsiTarafFaturaNo/IadeEdilenBelgeId korunur, referans nihai değerlerle
    // yeniden doğrulanır.
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task UpdateAsync_AlisFaturasi_PartialUpdate_SadeceAciklama_CariKartIdVeKarsiTarafFaturaNoKorunur()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var numara = $"TED-{Guid.NewGuid():N}"[..20];

        var taslak = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKartId,
            BelgeTarihi = new DateTime(2026, 3, 1),
            MusteriAdSoyad = "Tedarikci",
            Aciklama = "Eski aciklama",
            KarsiTarafFaturaNo = numara,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Test", Miktar = 1, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });

        var oncekiDb = await ReadNoTrackingAsync(dbContext, taslak.Id!.Value);

        await service.UpdateAsync(taslak.Id.Value, new UpdateSatisBelgesiRequest { Aciklama = "Yeni aciklama" });

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var sonrakiDb = await ReadNoTrackingAsync(verifyContext, taslak.Id.Value);

        AssertBelgeDegismedi(oncekiDb, sonrakiDb);
        Assert.Equal("Yeni aciklama", sonrakiDb.Aciklama);
    }

    [IntegrationFact]
    public async Task UpdateAsync_SatisIadeFaturasi_PartialUpdate_SadeceAciklama_CariKartIdVeIadeReferansiKorunur()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var asil = await SeedOnaylanmisSatisFaturasiVeKesAsync(dbContext, new DateTime(2026, 3, 1), "PUS");

        var iade = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 3, 5),
            MusteriAdSoyad = "Musteri",
            Aciklama = "Eski aciklama",
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

        var oncekiDb = await ReadNoTrackingAsync(dbContext, iade.Id!.Value);

        await service.UpdateAsync(iade.Id.Value, new UpdateSatisBelgesiRequest { Aciklama = "Yeni aciklama" });

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var sonrakiDb = await ReadNoTrackingAsync(verifyContext, iade.Id.Value);

        AssertBelgeDegismedi(oncekiDb, sonrakiDb);
        Assert.Equal("Yeni aciklama", sonrakiDb.Aciklama);
        Assert.Equal(asil.Id, sonrakiDb.IadeEdilenBelgeId);
    }

    [IntegrationFact]
    public async Task UpdateAsync_AlisIadeFaturasi_PartialUpdate_SadeceAciklama_CariKartIdVeIadeReferansiKorunur()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var asil = await SeedOnaylanmisAlisFaturasiAsync(dbContext, new DateTime(2026, 3, 1));

        var iade = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisIadeFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKartId,
            BelgeTarihi = new DateTime(2026, 3, 5),
            MusteriAdSoyad = "Tedarikci",
            Aciklama = "Eski aciklama",
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

        var oncekiDb = await ReadNoTrackingAsync(dbContext, iade.Id!.Value);

        await service.UpdateAsync(iade.Id.Value, new UpdateSatisBelgesiRequest { Aciklama = "Yeni aciklama" });

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var sonrakiDb = await ReadNoTrackingAsync(verifyContext, iade.Id.Value);

        AssertBelgeDegismedi(oncekiDb, sonrakiDb);
        Assert.Equal("Yeni aciklama", sonrakiDb.Aciklama);
        Assert.Equal(asil.Id, sonrakiDb.IadeEdilenBelgeId);
    }

    // ─────────────────────────────────────────────────────────────
    // B. Kontrol karakteri normalizasyonu - IsNullOrWhiteSpace kısa devresinden
    // SONRA değil, HER ZAMAN, ham değer üzerinde çalışmalı (\u0085 dahil).
    // ─────────────────────────────────────────────────────────────

    public static IEnumerable<object[]> KontrolKarakteriIcerenDegerler()
    {
        yield return new object[] { "\t" };
        yield return new object[] { "\n" };
        yield return new object[] { "\r" };
        yield return new object[] { "\u007F" };
        yield return new object[] { "\u0085" };
        yield return new object[] { "\tTED-1" };
        yield return new object[] { "TED-1\n" };
    }

    [IntegrationTheory]
    [MemberData(nameof(KontrolKarakteriIcerenDegerler))]
    public async Task CreateAsync_KontrolKarakteriIcerenDegerler_Reddedilir(string bozukDeger)
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

    [IntegrationTheory]
    [MemberData(nameof(KontrolKarakteriIcerenDegerler))]
    public async Task UpdateAsync_KontrolKarakteriIcerenDegerler_ReddedilirVeBelgeDegismezKalir(string bozukDeger)
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var numara = $"TED-{Guid.NewGuid():N}"[..20];

        var taslak = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKartId,
            BelgeTarihi = new DateTime(2026, 3, 1),
            MusteriAdSoyad = "Tedarikci",
            Aciklama = "Degismemeli",
            KarsiTarafFaturaNo = numara,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Test", Miktar = 1, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });

        var oncekiDb = await ReadNoTrackingAsync(dbContext, taslak.Id!.Value);

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.UpdateAsync(taslak.Id.Value, new UpdateSatisBelgesiRequest { KarsiTarafFaturaNo = bozukDeger }));
        Assert.Contains("kontrol karakteri", ex.Message);

        await using var verifyContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var sonrakiDb = await ReadNoTrackingAsync(verifyContext, taslak.Id.Value);

        AssertBelgeDegismedi(oncekiDb, sonrakiDb);
        Assert.Equal("Degismemeli", sonrakiDb.Aciklama);
    }

    [IntegrationFact]
    public async Task CreateAsync_YalnizcaNormalBosluklardanOlusanDeger_NullKabulEdilir()
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
            KarsiTarafFaturaNo = "   ",
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Test", Miktar = 1, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });

        Assert.Null(created.KarsiTarafFaturaNo);
    }

    [IntegrationFact]
    public async Task CreateAsync_U0000IcerenDeger_UygulamaSeviyesindeReddedilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        // U+0000 icin SQL Server constraint seviyesinde guvenilir bir kontrol pratik degildir (LIKE
        // desenlerinde NUL beklenmedik string kesilmesine yol acabilir - bkz. StysAppDbContext'teki
        // yorum) - bu yuzden bu deger yalnizca UYGULAMA seviyesinde (char.IsControl('\0')==true)
        // reddedildigi test edilir.
        var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
            TesisId = _tesisId,
            CariKartId = _tedarikciKartId,
            BelgeTarihi = new DateTime(2026, 3, 10),
            MusteriAdSoyad = "Tedarikci",
            KarsiTarafFaturaNo = "TED- -1",
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

    // ─────────────────────────────────────────────────────────────
    // C. HardenKarsiTarafFaturaNoControlCharacters migration - genisletilmis
    // CK_SatisBelgeleri_KarsiTarafFaturaNo_Format constraint'i, dogrudan SQL.
    // ─────────────────────────────────────────────────────────────

    public static IEnumerable<object[]> DbSeviyesindeReddedilenNCharKodlari()
    {
        yield return new object[] { 9 };
        yield return new object[] { 10 };
        yield return new object[] { 13 };
        yield return new object[] { 31 };
        yield return new object[] { 127 };
        yield return new object[] { 133 };
        yield return new object[] { 159 };
    }

    [IntegrationTheory]
    [MemberData(nameof(DbSeviyesindeReddedilenNCharKodlari))]
    public async Task Migration_HardenKarsiTarafFaturaNoControlCharacters_NCharDegerleriniReddeder(int nCharKodu)
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO [muhasebe].[SatisBelgeleri] " +
            "(KurumId, BelgeNo, BelgeTipi, Durum, KaynakModul, TesisId, CariKartId, BelgeTarihi, KurumsalMi, ToplamMatrah, ToplamKdv, GenelToplam, IsDeleted, TicariDurum, MuhasebeDurumu, FaturalamaDurumu, KarsiTarafFaturaNo) " +
            "VALUES (@kurumId, @belgeNo, 5, 0, 1, @tesisId, @cariKartId, '2026-01-01', 0, 100, 20, 120, 0, 1, 1, 2, 'TED-' + NCHAR(@nCharKodu) + '-1')";
        cmd.Parameters.Add(new SqlParameter("@kurumId", _kurumId));
        cmd.Parameters.Add(new SqlParameter("@belgeNo", $"CKC-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40]));
        cmd.Parameters.Add(new SqlParameter("@tesisId", _tesisId));
        cmd.Parameters.Add(new SqlParameter("@cariKartId", _tedarikciKartId));
        cmd.Parameters.Add(new SqlParameter("@nCharKodu", nCharKodu));

        var ex = await Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Contains("CK_SatisBelgeleri_KarsiTarafFaturaNo_Format", ex.Message);
    }
}
