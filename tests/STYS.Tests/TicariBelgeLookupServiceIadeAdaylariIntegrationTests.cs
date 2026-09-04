using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Dtos;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.CariKartlar.Services;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Dtos;
using STYS.Muhasebe.Kdv.Entities;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.Kdv.Services;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Tesisler.Dto;
using STYS.Tesisler.Entities;
using STYS.Tesisler.Services;
using STYS.TicariBelgeler.Dtos;
using STYS.TicariBelgeler.Services;
using TOD.Platform.Identity.Users.DTO;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// TicariBelgeLookupService.GetIadeAdaylariAsync/GetKaynakSatirlarAsync - ham SQL (FromSqlInterpolated/
/// SqlQueryRaw) kullandığı için GERÇEK SQL Server'a karşı, IadeEdilenBelgeEligibility ile PAYLAŞILAN
/// kriterlerin liste/autocomplete tarafında da doğru uygulandığını doğrular (bkz. görev E/F).
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class TicariBelgeLookupServiceIadeAdaylariIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "TBLKP-991";
    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _musteriKartId;
    private int _baskaMusteriKartId;

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
        var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "MUS", _tesisId);
        var baskaMusteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "MUS2", _tesisId);
        dbContext.MuhasebeHesapPlanlari.AddRange(gelirHesap, kdvSatisHesap, musteriHesap, baskaMusteriHesap);
        await dbContext.SaveChangesAsync();

        var musteriKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "MUS", CariKartTipleri.Musteri, _tesisId, musteriHesap.Id);
        var baskaMusteriKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "MUS2", CariKartTipleri.Musteri, _tesisId, baskaMusteriHesap.Id);
        dbContext.CariKartlar.AddRange(musteriKart, baskaMusteriKart);
        await dbContext.SaveChangesAsync();
        _musteriKartId = musteriKart.Id;
        _baskaMusteriKartId = baskaMusteriKart.Id;

        dbContext.MuhasebeDonemler.Add(new STYS.Muhasebe.MuhasebeDonemleri.Entities.MuhasebeDonem
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
        var belgeIds = await dbContext.SatisBelgeleri.IgnoreQueryFilters().Where(x => x.KurumId == _kurumId).Select(x => x.Id).ToListAsync();
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

        await dbContext.KurumFaturaNumaraSayaclari.Where(x => x.KurumId == _kurumId).ExecuteDeleteAsync();
        await dbContext.MuhasebeDonemler.Where(x => x.TesisId == _tesisId).ExecuteDeleteAsync();
        await dbContext.CariKartlar.Where(x => x.TesisId == _tesisId).ExecuteDeleteAsync();
        await dbContext.MuhasebeHesapPlanlari.Where(x => x.Kod != null && x.Kod.Contains(_uniqueSuffix)).ExecuteDeleteAsync();
        await dbContext.Tesisler.Where(x => x.Id == _tesisId).ExecuteDeleteAsync();
        await dbContext.Iller.Where(x => x.Id == _ilId).ExecuteDeleteAsync();
        await dbContext.Kurumlar.Where(x => x.Id == _kurumId).ExecuteDeleteAsync();
    }

    private TicariBelgeLookupService CreateLookupService(StysAppDbContext dbContext, DomainAccessScope? scope = null)
        => new(
            new FakeTesisService(),
            new FakeCariKartService(),
            new FakeKdvIstisnaTanimService(),
            new FakeUserAccessScopeService(scope ?? DomainAccessScope.Unscoped()),
            new FakeCurrentTenantAccessor(_kurumId),
            dbContext);

    private async Task<(SatisBelgesiDto Belge, int SatirId, decimal Miktar)> SeedKesilmisSatisFaturasiAsync(
        StysAppDbContext dbContext, string seriKodu, DateTime belgeTarihi, int? cariKartId = null, decimal miktar = 10m)
    {
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, donemService);

        var created = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            TesisId = _tesisId,
            CariKartId = cariKartId ?? _musteriKartId,
            BelgeTarihi = belgeTarihi,
            MusteriAdSoyad = "Test Musteri " + _uniqueSuffix,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Test satir", Miktar = miktar, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });
        var satirId = created.Satirlar[0].Id!.Value;

        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value);
        await service.MuhasebeOnaylaAsync(created.Id.Value);
        await fisService.MuhasebeFisiOlusturAsync(created.Id.Value);

        var sayacVarMi = await dbContext.KurumFaturaNumaraSayaclari.AsNoTracking()
            .AnyAsync(x => !x.IsDeleted && x.KurumId == _kurumId && x.MaliYil == belgeTarihi.Year && x.SeriKodu == seriKodu);
        if (!sayacVarMi)
        {
            dbContext.KurumFaturaNumaraSayaclari.Add(new STYS.Muhasebe.SatisBelgeleri.Entities.KurumFaturaNumaraSayaci
            {
                KurumId = _kurumId, MaliYil = belgeTarihi.Year, SeriKodu = seriKodu, SonNumara = 0, AktifMi = true
            });
            await dbContext.SaveChangesAsync();
        }

        var kesildi = await service.FaturaKesAsync(created.Id.Value, new FaturaKesRequest { SeriKodu = seriKodu });
        return (kesildi, satirId, miktar);
    }

    [IntegrationFact]
    public async Task GetIadeAdaylariAsync_GecerliAday_Donderilir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (asil, _, _) = await SeedKesilmisSatisFaturasiAsync(dbContext, "ADY1", new DateTime(2026, 3, 1));
        var lookup = CreateLookupService(dbContext);

        var sonuc = await lookup.GetIadeAdaylariAsync(new TicariBelgeIadeAdayiFilterDto
        {
            TesisId = _tesisId, BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
            CariKartId = _musteriKartId, BelgeTarihi = new DateTime(2026, 3, 10)
        });

        Assert.Contains(sonuc, x => x.Id == asil.Id);
    }

    [IntegrationFact]
    public async Task GetIadeAdaylariAsync_YanlisCari_HaricTutulur()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (asil, _, _) = await SeedKesilmisSatisFaturasiAsync(dbContext, "ADY2", new DateTime(2026, 3, 1));
        var lookup = CreateLookupService(dbContext);

        var sonuc = await lookup.GetIadeAdaylariAsync(new TicariBelgeIadeAdayiFilterDto
        {
            TesisId = _tesisId, BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
            CariKartId = _baskaMusteriKartId, BelgeTarihi = new DateTime(2026, 3, 10)
        });

        Assert.DoesNotContain(sonuc, x => x.Id == asil.Id);
    }

    [IntegrationFact]
    public async Task GetIadeAdaylariAsync_AsilTarihIadeTarihindenIleriyse_HaricTutulur()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (asil, _, _) = await SeedKesilmisSatisFaturasiAsync(dbContext, "ADY3", new DateTime(2026, 3, 20));
        var lookup = CreateLookupService(dbContext);

        var sonuc = await lookup.GetIadeAdaylariAsync(new TicariBelgeIadeAdayiFilterDto
        {
            TesisId = _tesisId, BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
            CariKartId = _musteriKartId, BelgeTarihi = new DateTime(2026, 3, 10) // iade tarihi asıldan ÖNCE
        });

        Assert.DoesNotContain(sonuc, x => x.Id == asil.Id);
    }

    [IntegrationFact]
    public async Task GetIadeAdaylariAsync_FaturasiKesilmemisAsil_HaricTutulur()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, donemService);

        // FaturaKesAsync ÇAĞRILMAZ - FaturalamaDurumu Kesildi'ye ULAŞMAZ.
        var created = await service.CreateAsync(new CreateSatisBelgesiRequest
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
                    SiraNo = 1, Aciklama = "Test", Miktar = 5m, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
                }
            ]
        });
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value);
        await service.MuhasebeOnaylaAsync(created.Id.Value);
        await fisService.MuhasebeFisiOlusturAsync(created.Id.Value);

        var lookup = CreateLookupService(dbContext);
        var sonuc = await lookup.GetIadeAdaylariAsync(new TicariBelgeIadeAdayiFilterDto
        {
            TesisId = _tesisId, BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
            CariKartId = _musteriKartId, BelgeTarihi = new DateTime(2026, 3, 10)
        });

        Assert.DoesNotContain(sonuc, x => x.Id == created.Id);
    }

    [IntegrationFact]
    public async Task GetIadeAdaylariAsync_IptalEdilmisFisliAday_HaricTutulur()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (asil, _, _) = await SeedKesilmisSatisFaturasiAsync(dbContext, "ADY5", new DateTime(2026, 3, 1));

        var fis = await dbContext.MuhasebeFisler.FirstAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi && x.KaynakId == asil.Id);
        fis.Durum = MuhasebeFisDurumlari.Iptal;
        await dbContext.SaveChangesAsync();

        var lookup = CreateLookupService(dbContext);
        var sonuc = await lookup.GetIadeAdaylariAsync(new TicariBelgeIadeAdayiFilterDto
        {
            TesisId = _tesisId, BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
            CariKartId = _musteriKartId, BelgeTarihi = new DateTime(2026, 3, 10)
        });

        Assert.DoesNotContain(sonuc, x => x.Id == asil.Id);
    }

    [IntegrationFact]
    public async Task GetIadeAdaylariAsync_SoftDeleteEdilmisAday_HaricTutulur()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (asil, _, _) = await SeedKesilmisSatisFaturasiAsync(dbContext, "ADY6", new DateTime(2026, 3, 1));

        var belge = await dbContext.SatisBelgeleri.FirstAsync(x => x.Id == asil.Id);
        belge.IsDeleted = true;
        await dbContext.SaveChangesAsync();

        var lookup = CreateLookupService(dbContext);
        var sonuc = await lookup.GetIadeAdaylariAsync(new TicariBelgeIadeAdayiFilterDto
        {
            TesisId = _tesisId, BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
            CariKartId = _musteriKartId, BelgeTarihi = new DateTime(2026, 3, 10)
        });

        Assert.DoesNotContain(sonuc, x => x.Id == asil.Id);
    }

    [IntegrationFact]
    public async Task GetIadeAdaylariAsync_MevcutBelgeninKendisi_HaricTutulur()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (asil, _, _) = await SeedKesilmisSatisFaturasiAsync(dbContext, "ADY7", new DateTime(2026, 3, 1));
        var lookup = CreateLookupService(dbContext);

        var sonuc = await lookup.GetIadeAdaylariAsync(new TicariBelgeIadeAdayiFilterDto
        {
            MevcutBelgeId = asil.Id, TesisId = _tesisId, BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
            CariKartId = _musteriKartId, BelgeTarihi = new DateTime(2026, 3, 10)
        });

        Assert.DoesNotContain(sonuc, x => x.Id == asil.Id);
    }

    [IntegrationFact]
    public async Task GetIadeAdaylariAsync_KapsamDisiTesis_403Firlatir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var lookup = CreateLookupService(dbContext, DomainAccessScope.Scoped([], [_tesisId + 999999], []));

        var ex = await Assert.ThrowsAsync<BaseException>(() => lookup.GetIadeAdaylariAsync(new TicariBelgeIadeAdayiFilterDto
        {
            TesisId = _tesisId, BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
            CariKartId = _musteriKartId, BelgeTarihi = new DateTime(2026, 3, 10)
        }));

        Assert.Equal(403, ex.ErrorCode);
    }

    [Fact]
    public void GetIadeAdaylariAsync_DtoIcindeMuhasebeFisIdVeyaLegacyDurumYok()
    {
        var dtoTipi = typeof(TicariBelgeIadeAdayiDto);
        Assert.Null(dtoTipi.GetProperty("MuhasebeFisId"));
        Assert.Null(dtoTipi.GetProperty("Durum"));
        Assert.Null(dtoTipi.GetProperty("MuhasebeDurumu"));
    }

    [IntegrationFact]
    public async Task GetKaynakSatirlarAsync_KalanIadeEdilebilirMiktarKumulatifOlarakHesaplanir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (asil, satirId, _) = await SeedKesilmisSatisFaturasiAsync(dbContext, "ADY8", new DateTime(2026, 3, 1), miktar: 10m);

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
                    SiraNo = 1, Aciklama = "Iade", Miktar = 4m, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m,
                    KaynakSatirId = satirId.ToString()
                }
            ]
        });
        await service.MuhasebeOnayinaGonderAsync(iade.Id!.Value);
        await service.MuhasebeOnaylaAsync(iade.Id.Value);

        var lookup = CreateLookupService(dbContext);
        var kaynakSatirlar = await lookup.GetKaynakSatirlarAsync(asil.Id!.Value, mevcutBelgeId: null);

        var kaynakSatir = Assert.Single(kaynakSatirlar);
        Assert.Equal(satirId, kaynakSatir.Id);
        Assert.Equal(10m, kaynakSatir.Miktar);
        Assert.Equal(6m, kaynakSatir.IadeEdilebilirKalanMiktar); // 10 - 4 (onaylanmis iade)
    }

    [IntegrationFact]
    public async Task GetKaynakSatirlarAsync_MevcutBelgeIdHaricTutulurKalanMiktarSelfHaric()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (asil, satirId, _) = await SeedKesilmisSatisFaturasiAsync(dbContext, "ADY9", new DateTime(2026, 3, 1), miktar: 10m);

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
                    SiraNo = 1, Aciklama = "Iade", Miktar = 4m, BirimFiyat = 100m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m,
                    KaynakSatirId = satirId.ToString()
                }
            ]
        });
        await service.MuhasebeOnayinaGonderAsync(iade.Id!.Value);
        await service.MuhasebeOnaylaAsync(iade.Id.Value);

        var lookup = CreateLookupService(dbContext);
        // mevcutBelgeId = iade.Id -> bu belgenin KENDİ 4'lük tüketimi kümülatif toplamdan HARİÇ
        // tutulur, kalan miktar (self-exclusion sayesinde) tekrar 10 olarak görünmelidir - bu, bir
        // KULLANICI o iade belgesini DÜZENLERKEN kendi mevcut satırının kendisini "tüketmiş" gibi
        // saymaması gerektiğini doğrular.
        var kaynakSatirlar = await lookup.GetKaynakSatirlarAsync(asil.Id!.Value, mevcutBelgeId: iade.Id!.Value);

        var kaynakSatir = Assert.Single(kaynakSatirlar);
        Assert.Equal(10m, kaynakSatir.IadeEdilebilirKalanMiktar);
    }

    // ── Fake'ler (bu iki metotta hiç dereference edilmeyen bağımlılıklar) ──

    private sealed class FakeCurrentTenantAccessor(int kurumId) : ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => kurumId;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [kurumId];
        public bool IsSuperAdmin() => false;
        public bool IsKurumAdmin() => false;
    }

    private sealed class FakeUserAccessScopeService(DomainAccessScope scope) : IUserAccessScopeService
    {
        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(scope);
    }

    private sealed class FakeTesisService : ITesisService
    {
        public Task<List<TesisDto>> GetAktifKurumTesisleriAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UserDto> CreateTesisYoneticisiUserAsync(int tesisId, UserDto dto) => throw new NotImplementedException();
        public Task<UserDto> CreateResepsiyonistUserAsync(int tesisId, UserDto dto) => throw new NotImplementedException();
        public Task<UserDto> CreateBinaYoneticisiUserAsync(int tesisId, UserDto dto) => throw new NotImplementedException();
        public Task<UserDto> CreateRestoranYoneticisiUserAsync(int tesisId, UserDto dto) => throw new NotImplementedException();
        public Task<UserDto> CreateRestoranGarsonuUserAsync(int tesisId, UserDto dto) => throw new NotImplementedException();
        public Task<UserDto> CreateMuhasebeciUserAsync(int tesisId, UserDto dto) => throw new NotImplementedException();
        public Task<IEnumerable<TesisDto>> GetAllAsync(Func<IQueryable<Tesis>, IQueryable<Tesis>>? include = null) => throw new NotImplementedException();
        public Task<TesisDto?> GetByIdAsync(int id, Func<IQueryable<Tesis>, IQueryable<Tesis>>? include = null) => throw new NotImplementedException();
        public Task<PagedResult<TesisDto>> GetPagedAsync(PagedRequest request, Expression<Func<Tesis, bool>>? predicate = null, Func<IQueryable<Tesis>, IQueryable<Tesis>>? include = null, Func<IQueryable<Tesis>, IOrderedQueryable<Tesis>>? orderBy = null) => throw new NotImplementedException();
        public Task<TesisDto> AddAsync(TesisDto dto) => throw new NotImplementedException();
        public Task<TesisDto> UpdateAsync(TesisDto dto) => throw new NotImplementedException();
        public Task DeleteAsync(int id) => throw new NotImplementedException();
        public Task<IEnumerable<TesisDto>> WhereAsync(Expression<Func<Tesis, bool>> predicate, Func<IQueryable<Tesis>, IQueryable<Tesis>>? include = null) => throw new NotImplementedException();
        public Task<bool> AnyAsync(Expression<Func<Tesis, bool>> predicate, Func<IQueryable<Tesis>, IQueryable<Tesis>>? include = null) => throw new NotImplementedException();
    }

    private sealed class FakeCariKartService : ICariKartService
    {
        public Task<IEnumerable<CariKartDto>> GetAllAsync(int? tesisId, Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null) => throw new NotImplementedException();
        public Task<PagedResult<CariKartDto>> GetPagedAsync(PagedRequest request, int? tesisId, Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null, Func<IQueryable<CariKart>, IOrderedQueryable<CariKart>>? orderBy = null) => throw new NotImplementedException();
        public Task<CariBakiyeDto> GetBakiyeAsync(int cariKartId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CariKartDto> CariKartAcilisBakiyesiDuzeltAsync(int cariKartId, decimal yeniTutar, string? yeniYonu, DateTime? duzeltmeTarihi = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<CariKartDto>> GetAllAsync(Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null) => throw new NotImplementedException();
        public Task<CariKartDto?> GetByIdAsync(int id, Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null) => throw new NotImplementedException();
        public Task<PagedResult<CariKartDto>> GetPagedAsync(PagedRequest request, Expression<Func<CariKart, bool>>? predicate = null, Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null, Func<IQueryable<CariKart>, IOrderedQueryable<CariKart>>? orderBy = null) => throw new NotImplementedException();
        public Task<CariKartDto> AddAsync(CariKartDto dto) => throw new NotImplementedException();
        public Task<CariKartDto> UpdateAsync(CariKartDto dto) => throw new NotImplementedException();
        public Task DeleteAsync(int id) => throw new NotImplementedException();
        public Task<CariKartDto> FindOrCreateMusteriCariKartAsync(CariKartDto dto, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<CariKartDto>> WhereAsync(Expression<Func<CariKart, bool>> predicate, Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null) => throw new NotImplementedException();
        public Task<bool> AnyAsync(Expression<Func<CariKart, bool>> predicate, Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null) => throw new NotImplementedException();
    }

    private sealed class FakeKdvIstisnaTanimService : IKdvIstisnaTanimService
    {
        public Task<List<KdvIstisnaTanimDto>> FilterAsync(KdvIstisnaTanimFilterDto filter, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<KdvIstisnaTanimDto>> GetAllAsync(Func<IQueryable<KdvIstisnaTanim>, IQueryable<KdvIstisnaTanim>>? include = null) => throw new NotImplementedException();
        public Task<KdvIstisnaTanimDto?> GetByIdAsync(int id, Func<IQueryable<KdvIstisnaTanim>, IQueryable<KdvIstisnaTanim>>? include = null) => throw new NotImplementedException();
        public Task<PagedResult<KdvIstisnaTanimDto>> GetPagedAsync(PagedRequest request, Expression<Func<KdvIstisnaTanim, bool>>? predicate = null, Func<IQueryable<KdvIstisnaTanim>, IQueryable<KdvIstisnaTanim>>? include = null, Func<IQueryable<KdvIstisnaTanim>, IOrderedQueryable<KdvIstisnaTanim>>? orderBy = null) => throw new NotImplementedException();
        public Task<KdvIstisnaTanimDto> AddAsync(KdvIstisnaTanimDto dto) => throw new NotImplementedException();
        public Task<KdvIstisnaTanimDto> UpdateAsync(KdvIstisnaTanimDto dto) => throw new NotImplementedException();
        public Task DeleteAsync(int id) => throw new NotImplementedException();
        public Task<IEnumerable<KdvIstisnaTanimDto>> WhereAsync(Expression<Func<KdvIstisnaTanim, bool>> predicate, Func<IQueryable<KdvIstisnaTanim>, IQueryable<KdvIstisnaTanim>>? include = null) => throw new NotImplementedException();
        public Task<bool> AnyAsync(Expression<Func<KdvIstisnaTanim, bool>> predicate, Func<IQueryable<KdvIstisnaTanim>, IQueryable<KdvIstisnaTanim>>? include = null) => throw new NotImplementedException();
    }
}
