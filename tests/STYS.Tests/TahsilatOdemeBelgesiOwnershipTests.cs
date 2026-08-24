using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.AccessScope;
using STYS.Iller.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.CariHareketler.Mapping;
using STYS.Muhasebe.CariHareketler.Repositories;
using STYS.Muhasebe.CariHareketler.Services;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.CariKartlar.Mapping;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.MuhasebeDonemleri.Mapping;
using STYS.Muhasebe.MuhasebeDonemleri.Repositories;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeFisleri.Dtos;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Services;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.PosTahsilatValorleri.Services;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Dtos;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Mapping;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Repositories;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Services;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class TahsilatOdemeBelgesiOwnershipTests
{
    [Fact]
    public async Task KantinOwnedTahsilat_GenelUpdateIleDegistirilemez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateTahsilatService(dbContext);
        var belge = await SeedTahsilatBelgesiAsync(dbContext, MuhasebeKaynakModulleri.KantinSatisOdeme, 501);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.UpdateAsync(new TahsilatOdemeBelgesiDto
        {
            Id = belge.Id,
            BelgeNo = belge.BelgeNo,
            BelgeTarihi = belge.BelgeTarihi,
            BelgeTipi = belge.BelgeTipi,
            CariKartId = belge.CariKartId,
            Tutar = belge.Tutar + 1,
            ParaBirimi = belge.ParaBirimi,
            OdemeYontemi = belge.OdemeYontemi,
            Durum = belge.Durum,
            KasaBankaHesapId = belge.KasaBankaHesapId,
            KaynakModul = belge.KaynakModul,
            KaynakId = belge.KaynakId
        }));

        Assert.Equal("Bu tahsilat belgesi Kantin Satış workflow'u tarafından yönetilmektedir.", ex.Message);
    }

    [Fact]
    public async Task KantinOwnedTahsilat_GenelIptalIleYonetilemez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateTahsilatService(dbContext);
        var belge = await SeedTahsilatBelgesiAsync(dbContext, MuhasebeKaynakModulleri.KantinSatisOdeme, 502);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.IptalEtAsync(belge.Id));

        Assert.Equal("Bu tahsilat belgesi Kantin Satış workflow'u tarafından yönetilmektedir.", ex.Message);
    }

    [Fact]
    public async Task KantinOwnedTahsilat_GenelIptalGeriAlIleYonetilemez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateTahsilatService(dbContext);
        var belge = await SeedTahsilatBelgesiAsync(dbContext, MuhasebeKaynakModulleri.KantinSatisOdeme, 503, TahsilatOdemeBelgeDurumlari.Iptal);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.IptalGeriAlAsync(belge.Id));

        Assert.Equal("Bu tahsilat belgesi Kantin Satış workflow'u tarafından yönetilmektedir.", ex.Message);
    }

    [Fact]
    public async Task KantinOwnedTahsilat_IcinGenericMuhasebeFisiOlusturulamaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = new TahsilatOdemeBelgesiMuhasebeFisService(dbContext, CreateMapper(), CreateMuhasebeDonemService(dbContext));
        var belge = await SeedTahsilatBelgesiAsync(dbContext, MuhasebeKaynakModulleri.KantinSatisOdeme, 504);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.FisOlusturAsync(belge.Id));

        Assert.Equal("Bu tahsilat belgesi Kantin Satış workflow'u tarafından yönetilmektedir.", ex.Message);
    }

    [Fact]
    public async Task PublicAddAsync_KantinSourceSpoofingReddedilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateTahsilatService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddAsync(new TahsilatOdemeBelgesiDto
        {
            BelgeNo = "THS-SPOOF-1",
            BelgeTarihi = new DateTime(2026, 8, 24, 10, 0, 0),
            BelgeTipi = TahsilatOdemeBelgeTipleri.Tahsilat,
            CariKartId = 100,
            Tutar = 50,
            ParaBirimi = "TRY",
            OdemeYontemi = OdemeYontemleri.Nakit,
            Durum = TahsilatOdemeBelgeDurumlari.Aktif,
            KaynakModul = MuhasebeKaynakModulleri.KantinSatisOdeme,
            KaynakId = 42
        }));

        Assert.Equal("Bu tahsilat belgesi Kantin Satış workflow'u tarafından yönetilmektedir.", ex.Message);
    }

    [Fact]
    public async Task InternalAddWithinCurrentTransaction_KantinSourceIleOlusabilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateTahsilatService(dbContext);

        var result = await service.AddWithinCurrentTransactionAsync(new TahsilatOdemeBelgesiDto
        {
            BelgeNo = "KNT-THS-1",
            BelgeTarihi = new DateTime(2026, 8, 24, 10, 0, 0),
            BelgeTipi = TahsilatOdemeBelgeTipleri.Tahsilat,
            CariKartId = 100,
            Tutar = 50,
            ParaBirimi = "TRY",
            OdemeYontemi = OdemeYontemleri.Nakit,
            Durum = TahsilatOdemeBelgeDurumlari.Aktif,
            KasaBankaHesapId = 100,
            KaynakModul = MuhasebeKaynakModulleri.KantinSatisOdeme,
            KaynakId = 77
        }, requireCariMuhasebeHesabi: false);

        Assert.NotNull(result.Id);
        Assert.True(await dbContext.TahsilatOdemeBelgeleri.AnyAsync(x =>
            x.Id == result.Id
            && x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisOdeme
            && x.KaynakId == 77));
    }

    [Fact]
    public async Task BagimsizTahsilatBelgesiPublicAddVeUpdateDavranisiBozulmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateTahsilatService(dbContext);

        var belge = await service.AddAsync(new TahsilatOdemeBelgesiDto
        {
            BelgeNo = "THS-NORMAL-1",
            BelgeTarihi = new DateTime(2026, 8, 24, 10, 0, 0),
            BelgeTipi = TahsilatOdemeBelgeTipleri.Tahsilat,
            CariKartId = 100,
            Tutar = 50,
            ParaBirimi = "TRY",
            OdemeYontemi = OdemeYontemleri.Nakit,
            Durum = TahsilatOdemeBelgeDurumlari.Aktif,
            KasaBankaHesapId = 100,
            KaynakModul = MuhasebeKaynakModulleri.Rezervasyon,
            KaynakId = 88
        });

        belge.Tutar = 55;
        var updated = await service.UpdateAsync(belge);

        Assert.Equal(55, updated.Tutar);
        Assert.Equal(MuhasebeKaynakModulleri.Rezervasyon, updated.KaynakModul);
    }

    [Fact]
    public void TahsilatSourceUniqueIndex_ModeldeMevcut()
    {
        using var dbContext = CreateDbContext();

        var entityType = dbContext.Model.FindEntityType(typeof(TahsilatOdemeBelgesi));
        var index = Assert.Single(entityType!.GetIndexes().Where(x =>
            x.IsUnique
            && x.Properties.Select(p => p.Name).SequenceEqual(["KaynakModul", "KaynakId"])));

        Assert.Equal("[IsDeleted] = 0 AND [KaynakId] IS NOT NULL", index.GetFilter());
    }

    private static async Task SeedBaseAsync(StysAppDbContext dbContext)
    {
        dbContext.Iller.Add(new Il { Id = 1, Ad = "Ankara", AktifMi = true });
        dbContext.Kurumlar.Add(new Kurum { Id = 1, Kod = "KRM", Ad = "Test Kurum", AktifMi = true });
        dbContext.Tesisler.Add(new Tesis { Id = 1, KurumId = 1, IlId = 1, Ad = "Tesis A", Telefon = "03120000000", Adres = "Adres A", AktifMi = true });
        dbContext.MuhasebeHesapPlanlari.Add(new MuhasebeHesapPlani
        {
            Id = 1,
            Kod = "120.01",
            TamKod = "120.01",
            Ad = "Cari Hesap",
            AktifMi = true,
            DetayHesapMi = true,
            HareketGorebilirMi = true,
            HesapTipi = HesapTipi.DetayHesap
        });
        dbContext.CariKartlar.Add(new CariKart
        {
            Id = 100,
            TesisId = 1,
            CariTipi = CariKartTipleri.Musteri,
            CariKodu = "CR-001",
            UnvanAdSoyad = "Cari Kart",
            AktifMi = true,
            MuhasebeHesapPlaniId = 1
        });
        dbContext.KasaBankaHesaplari.Add(new KasaBankaHesap
        {
            Id = 100,
            TesisId = 1,
            Tip = KasaBankaHesapTipleri.NakitKasa,
            Kod = "KASA-1",
            Ad = "Nakit Kasa",
            AktifMi = true
        });
        dbContext.MuhasebeDonemler.Add(new MuhasebeDonem
        {
            Id = 1,
            TesisId = 1,
            MaliYil = 2026,
            DonemNo = 8,
            BaslangicTarihi = new DateTime(2026, 1, 1),
            BitisTarihi = new DateTime(2026, 12, 31),
            KapaliMi = false
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<TahsilatOdemeBelgesi> SeedTahsilatBelgesiAsync(
        StysAppDbContext dbContext,
        string kaynakModul,
        int kaynakId,
        string durum = TahsilatOdemeBelgeDurumlari.Aktif)
    {
        var belge = new TahsilatOdemeBelgesi
        {
            Id = 200 + kaynakId,
            BelgeNo = $"THS-{kaynakId}",
            BelgeTarihi = new DateTime(2026, 8, 24, 10, 0, 0),
            BelgeTipi = TahsilatOdemeBelgeTipleri.Tahsilat,
            CariKartId = 100,
            Tutar = 50,
            ParaBirimi = "TRY",
            OdemeYontemi = OdemeYontemleri.Nakit,
            Durum = durum,
            KasaBankaHesapId = 100,
            KaynakModul = kaynakModul,
            KaynakId = kaynakId
        };
        dbContext.TahsilatOdemeBelgeleri.Add(belge);
        await dbContext.SaveChangesAsync();
        return belge;
    }

    private static StysAppDbContext CreateDbContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new StysAppDbContext(options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor())
        {
            AllowExplicitTenantWritesWithoutAmbientTenant = true
        };
    }

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<TahsilatOdemeBelgesiProfile>();
            cfg.AddProfile<CariKartProfile>();
            cfg.AddProfile<CariHareketProfile>();
            cfg.AddProfile<MuhasebeDonemProfile>();
        }, NullLoggerFactory.Instance);

        return config.CreateMapper();
    }

    private static ITahsilatOdemeBelgesiService CreateTahsilatService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var tahsilatRepo = new TahsilatOdemeBelgesiRepository(dbContext, mapper);
        var cariKartRepo = new CariKartRepository(dbContext, mapper);
        var cariHareketRepo = new CariHareketRepository(dbContext, mapper);
        var muhasebeDonemService = CreateMuhasebeDonemService(dbContext);
        var userAccessScope = new FakeUserAccessScopeService();
        var cariHareketKapamaService = new CariHareketKapamaService(
            dbContext, tahsilatRepo, cariHareketRepo, muhasebeDonemService, userAccessScope, mapper);
        var posTahsilatValorSnapshotService = new PosTahsilatValorSnapshotService(
            dbContext,
            new ValorTarihHesaplamaService(new NoOpResmiTatilGunuProvider()),
            new FakeMuhasebeFisService());

        return new TahsilatOdemeBelgesiService(
            tahsilatRepo,
            cariKartRepo,
            cariHareketRepo,
            cariHareketKapamaService,
            dbContext,
            muhasebeDonemService,
            userAccessScope,
            posTahsilatValorSnapshotService,
            mapper);
    }

    private static IMuhasebeDonemService CreateMuhasebeDonemService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var repo = new MuhasebeDonemRepository(dbContext, mapper);
        return new MuhasebeDonemService(repo, mapper, dbContext, new FakeMuhasebeTesisScopeService());
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public string? GetCurrentUserName() => "tahsilat-test";
        public Guid? GetCurrentUserId() => Guid.Parse("33333333-3333-3333-3333-333333333333");
    }

    private sealed class FakeCurrentTenantAccessor : ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => null;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [];
        public bool IsSuperAdmin() => true;
        public bool IsKurumAdmin() => false;
    }

    private sealed class FakeUserAccessScopeService : IUserAccessScopeService
    {
        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(DomainAccessScope.Unscoped());
    }

    private sealed class FakeMuhasebeTesisScopeService : IMuhasebeTesisScopeService
    {
        public Task<int[]> GetEffectiveTesisIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<int>());

        public Task<int[]> GetEffectiveTesisIdsAsync(DomainAccessScope scope, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<int>());

        public Task EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeMuhasebeFisService : IMuhasebeFisService
    {
        public Task<IEnumerable<MuhasebeFisDto>> GetAllAsync(Func<IQueryable<MuhasebeFis>, IQueryable<MuhasebeFis>>? include = null) => throw new NotSupportedException();
        public Task<MuhasebeFisDto?> GetByIdAsync(int id, Func<IQueryable<MuhasebeFis>, IQueryable<MuhasebeFis>>? include = null) => throw new NotSupportedException();
        public Task<PagedResult<MuhasebeFisDto>> GetPagedAsync(PagedRequest request, System.Linq.Expressions.Expression<Func<MuhasebeFis, bool>>? predicate = null, Func<IQueryable<MuhasebeFis>, IQueryable<MuhasebeFis>>? include = null, Func<IQueryable<MuhasebeFis>, IOrderedQueryable<MuhasebeFis>>? orderBy = null) => throw new NotSupportedException();
        public Task<MuhasebeFisDto> AddAsync(MuhasebeFisDto dto) => throw new NotSupportedException();
        public Task<MuhasebeFisDto> UpdateAsync(MuhasebeFisDto dto) => throw new NotSupportedException();
        public Task DeleteAsync(int id) => throw new NotSupportedException();
        public Task<IEnumerable<MuhasebeFisDto>> WhereAsync(System.Linq.Expressions.Expression<Func<MuhasebeFis, bool>> predicate, Func<IQueryable<MuhasebeFis>, IQueryable<MuhasebeFis>>? include = null) => throw new NotSupportedException();
        public Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<MuhasebeFis, bool>> predicate, Func<IQueryable<MuhasebeFis>, IQueryable<MuhasebeFis>>? include = null) => throw new NotSupportedException();
        public Task<MuhasebeFisDto?> GetByIdWithSatirlarAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<MuhasebeFisDto>> GetByKaynakAsync(string kaynakModul, int kaynakId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MuhasebeFisDto> OnaylaAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MuhasebeFisDto> IptalEtAsync(int id, string? aciklama = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MuhasebeFisIptalSonucDto> PosValorTransferFisiniIptalEtAsync(int muhasebeFisId, int beklenenKaynakId, int beklenenTesisId, string aciklama, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MuhasebeFisIptalSonucDto> PosValorTransferFisiniGeriAlAsync(int tersKayitFisId, int beklenenKaynakId, int beklenenTesisId, string aciklama, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MuhasebeFisIptalSonucDto> SatisBelgesiFisiIptalEtAsync(int muhasebeFisId, int beklenenKaynakId, int beklenenTesisId, string aciklama, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<MuhasebeFisDto>> GetFilteredAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountFilteredAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<YevmiyeDefteriDto> GetYevmiyeDefteriAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]> ExportYevmiyeDefteriExcelAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MuavinDefterDto> GetMuavinDefterAsync(MuavinDefterFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]> ExportMuavinDefterExcelAsync(MuavinDefterFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MizanDto> GetMizanAsync(MizanFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MizanDto> GetMizanBakiyeAsync(MizanFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]> ExportMizanBakiyeExcelAsync(MizanFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MizanKarsilastirmaDto> KarsilastirMizanAsync(MizanFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TasinirMuhasebeFisiOlusturResultDto> TasinirMuhasebeFisiTaslagiOlusturAsync(TasinirMuhasebeFisiOlusturRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
