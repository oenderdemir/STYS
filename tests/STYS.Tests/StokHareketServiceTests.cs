using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.AccessScope;
using STYS.Iller.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.Depolar.Repositories;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.Kdv.Services;
using STYS.Muhasebe.MuhasebeDonemleri.Dtos;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokHareketleri.Mapping;
using STYS.Muhasebe.StokHareketleri.Repositories;
using STYS.Muhasebe.StokHareketleri.Services;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKartlari.Repositories;
using STYS.Muhasebe.TasinirKodlari.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class StokHareketServiceTests
{
    [Fact]
    public async Task TransferIptalAsync_DonemKontrolundeDogruTesisIdKullanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, miktar: 20);
        var donemService = new FakeMuhasebeDonemService();
        var service = CreateService(dbContext, donemService);

        var created = await service.CreateTransferAsync(CreateTransferRequest());
        donemService.Calls.Clear();

        await service.TransferIptalAsync(created[0].Id!.Value);

        Assert.NotEmpty(donemService.Calls);
        Assert.All(donemService.Calls, x => Assert.Equal(1, x));
        Assert.DoesNotContain(10, donemService.Calls);
        Assert.DoesNotContain(20, donemService.Calls);
    }

    [Fact]
    public async Task TransferIptalAsync_KullanilmamisTransferiIptalEder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, miktar: 20);
        var service = CreateService(dbContext);

        var created = await service.CreateTransferAsync(CreateTransferRequest());

        await service.TransferIptalAsync(created[0].Id!.Value);

        var transferHareketleri = await dbContext.StokHareketleri
            .Where(x => x.TransferGrupId == created[0].TransferGrupId)
            .OrderBy(x => x.Id)
            .ToListAsync();

        Assert.Equal(2, transferHareketleri.Count);
        Assert.All(transferHareketleri, x => Assert.Equal(StokHareketDurumlari.Iptal, x.Durum));
    }

    [Fact]
    public async Task TransferIptalAsync_HedefStokKullanildiysaReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, miktar: 20);
        var service = CreateService(dbContext);

        var created = await service.CreateTransferAsync(CreateTransferRequest());
        dbContext.StokHareketleri.Add(new StokHareket
        {
            DepoId = 20,
            TasinirKartId = 100,
            HareketTarihi = new DateTime(2026, 8, 21),
            HareketTipi = StokHareketTipleri.Sarf,
            Miktar = 8,
            BirimFiyat = 1,
            Tutar = 8,
            Durum = StokHareketDurumlari.Aktif,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.KdvKapsamDisi,
            KdvOrani = 0,
            KdvTutari = 0
        });
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.TransferIptalAsync(created[0].Id!.Value));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Hedef depodaki stok kullanıldığı için transfer iptal edilemez.", ex.Message);

        var transferHareketleri = await dbContext.StokHareketleri
            .Where(x => x.TransferGrupId == created[0].TransferGrupId)
            .ToListAsync();
        Assert.All(transferHareketleri, x => Assert.Equal(StokHareketDurumlari.Aktif, x.Durum));
    }

    [Fact]
    public async Task TransferIptalAsync_GrupButunluguBozuksaReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, miktar: 20);
        var service = CreateService(dbContext);

        var created = await service.CreateTransferAsync(CreateTransferRequest());
        var girisAyagi = await dbContext.StokHareketleri.SingleAsync(x =>
            x.TransferGrupId == created[0].TransferGrupId
            && x.TransferYonu == StokTransferYonleri.Giris);
        girisAyagi.Durum = StokHareketDurumlari.Iptal;
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.TransferIptalAsync(created[0].Id!.Value));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Transfer grup butunlugu bozuk oldugu icin iptal islemi yapilamaz.", ex.Message);

        var hareketler = await dbContext.StokHareketleri
            .Where(x => x.TransferGrupId == created[0].TransferGrupId)
            .OrderBy(x => x.Id)
            .ToListAsync();
        Assert.Equal(new[] { StokHareketDurumlari.Aktif, StokHareketDurumlari.Iptal }, hareketler.Select(x => x.Durum));
    }

    [Fact]
    public async Task UpdateVeDelete_DonemKontrolundeDepoIdYerineTesisIdKullanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var hareketId = await SeedNormalStokHareketiAsync(dbContext);
        var donemService = new FakeMuhasebeDonemService();
        var service = CreateService(dbContext, donemService);

        await service.UpdateAsync(new StokHareketDto
        {
            Id = hareketId,
            DepoId = 10,
            TasinirKartId = 100,
            HareketTarihi = new DateTime(2026, 8, 21),
            HareketTipi = StokHareketTipleri.Giris,
            Miktar = 6,
            BirimFiyat = 2,
            Tutar = 12,
            Durum = StokHareketDurumlari.Aktif,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
            KdvOrani = 20
        });

        await service.DeleteAsync(hareketId);

        Assert.NotEmpty(donemService.Calls);
        Assert.All(donemService.Calls, x => Assert.Equal(1, x));
        Assert.DoesNotContain(10, donemService.Calls);
    }

    private static StysAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new StysAppDbContext(options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor())
        {
            AllowExplicitTenantWritesWithoutAmbientTenant = true
        };
    }

    private static StokHareketService CreateService(
        StysAppDbContext dbContext,
        FakeMuhasebeDonemService? muhasebeDonemService = null)
    {
        var mapperConfig = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<StokHareketProfile>();
        }, NullLoggerFactory.Instance);

        var mapper = mapperConfig.CreateMapper();
        return new StokHareketService(
            dbContext,
            new StokHareketRepository(dbContext, mapper),
            new DepoRepository(dbContext, mapper),
            new TasinirKartRepository(dbContext, mapper),
            new CariKartRepository(dbContext, mapper),
            muhasebeDonemService ?? new FakeMuhasebeDonemService(),
            new FakeUserAccessScopeService(DomainAccessScope.Scoped([], [1], [])),
            new FakeKdvUygulamaService(),
            mapper);
    }

    private static StokTransferRequest CreateTransferRequest()
    {
        return new StokTransferRequest
        {
            KaynakDepoId = 10,
            HedefDepoId = 20,
            TasinirKartId = 100,
            HareketTarihi = new DateTime(2026, 8, 21),
            Miktar = 10,
            BirimFiyat = 1,
            BelgeNo = "TR-001"
        };
    }

    private static async Task SeedBaseAsync(StysAppDbContext dbContext)
    {
        dbContext.Kurumlar.Add(new Kurum
        {
            Id = 1,
            Kod = "TRT",
            Ad = "TRT",
            AktifMi = true
        });

        dbContext.Iller.Add(new Il
        {
            Id = 1,
            Ad = "Ankara",
            AktifMi = true
        });

        dbContext.Tesisler.Add(new Tesis
        {
            Id = 1,
            KurumId = 1,
            IlId = 1,
            Ad = "Tesis 1",
            Telefon = "000",
            Adres = "Adres 1",
            AktifMi = true
        });

        dbContext.MuhasebeHesapPlanlari.Add(new MuhasebeHesapPlani
        {
            Id = 1,
            Kod = "150",
            TamKod = "150.01",
            Ad = "Stok Hesabi",
            SeviyeNo = 2,
            HesapTipi = HesapTipi.DetayHesap,
            AktifMi = true,
            DetayHesapMi = true,
            HareketGorebilirMi = true,
            TesisId = 1
        });

        dbContext.TasinirKodlar.Add(new TasinirKod
        {
            Id = 200,
            TamKod = "150.01.01",
            Kod = "1500101",
            Ad = "Temizlik Malzemeleri",
            DuzeyNo = 3,
            AktifMi = true
        });

        dbContext.Depolar.AddRange(
            new Depo
            {
                Id = 10,
                TesisId = 1,
                Kod = "D-001",
                Ad = "Ana Depo",
                AktifMi = true,
                MuhasebeHesapPlaniId = 1,
                MalzemeKayitTipi = DepoMalzemeKayitTipleri.MalzemeleriAyriKayittaTut
            },
            new Depo
            {
                Id = 20,
                TesisId = 1,
                Kod = "D-002",
                Ad = "Mutfak Deposu",
                AktifMi = true,
                MuhasebeHesapPlaniId = 1,
                MalzemeKayitTipi = DepoMalzemeKayitTipleri.MalzemeleriAyriKayittaTut
            });

        dbContext.TasinirKartlar.Add(new TasinirKart
        {
            Id = 100,
            TesisId = 1,
            TasinirKodId = 200,
            MuhasebeHesapPlaniId = 1,
            StokKodu = "STK-100",
            Ad = "Finish Quantum",
            Birim = "Adet",
            MalzemeTipi = MalzemeTipleri.Diger,
            KdvOrani = 20,
            AktifMi = true
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedSourceStockAsync(StysAppDbContext dbContext, decimal miktar)
    {
        dbContext.StokHareketleri.Add(new StokHareket
        {
            DepoId = 10,
            TasinirKartId = 100,
            HareketTarihi = new DateTime(2026, 8, 20),
            HareketTipi = StokHareketTipleri.Giris,
            Miktar = miktar,
            BirimFiyat = 1,
            Tutar = miktar,
            Durum = StokHareketDurumlari.Aktif,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
            KdvOrani = 20,
            KdvTutari = Math.Round(miktar * 0.2m, 2, MidpointRounding.AwayFromZero)
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<int> SeedNormalStokHareketiAsync(StysAppDbContext dbContext)
    {
        var entity = new StokHareket
        {
            DepoId = 10,
            TasinirKartId = 100,
            HareketTarihi = new DateTime(2026, 8, 21),
            HareketTipi = StokHareketTipleri.Giris,
            Miktar = 5,
            BirimFiyat = 2,
            Tutar = 10,
            Durum = StokHareketDurumlari.Aktif,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
            KdvOrani = 20,
            KdvTutari = 2
        };

        dbContext.StokHareketleri.Add(entity);
        await dbContext.SaveChangesAsync();
        return entity.Id;
    }

    private sealed class FakeMuhasebeDonemService : IMuhasebeDonemService
    {
        public List<int> Calls { get; } = [];

        public Task<MuhasebeDonemDto?> GetAktifDonemAsync(int tesisId, DateTime tarih, CancellationToken cancellationToken = default)
        {
            Calls.Add(tesisId);
            return Task.FromResult<MuhasebeDonemDto?>(new MuhasebeDonemDto
            {
                Id = 1,
                TesisId = tesisId,
                MaliYil = tarih.Year,
                DonemNo = tarih.Month,
                BaslangicTarihi = new DateTime(tarih.Year, tarih.Month, 1),
                BitisTarihi = new DateTime(tarih.Year, tarih.Month, DateTime.DaysInMonth(tarih.Year, tarih.Month)),
                KapaliMi = false
            });
        }

        public Task DonemKapatAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DonemAcAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IEnumerable<MuhasebeDonemDto>> GetAllAsync(Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null) => throw new NotSupportedException();
        public Task<MuhasebeDonemDto?> GetByIdAsync(int id, Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null) => throw new NotSupportedException();
        public Task<PagedResult<MuhasebeDonemDto>> GetPagedAsync(PagedRequest request, System.Linq.Expressions.Expression<Func<MuhasebeDonem, bool>>? predicate = null, Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null, Func<IQueryable<MuhasebeDonem>, IOrderedQueryable<MuhasebeDonem>>? orderBy = null) => throw new NotSupportedException();
        public Task<MuhasebeDonemDto> AddAsync(MuhasebeDonemDto dto) => throw new NotSupportedException();
        public Task<MuhasebeDonemDto> UpdateAsync(MuhasebeDonemDto dto) => throw new NotSupportedException();
        public Task DeleteAsync(int id) => throw new NotSupportedException();
        public Task<IEnumerable<MuhasebeDonemDto>> WhereAsync(System.Linq.Expressions.Expression<Func<MuhasebeDonem, bool>> predicate, Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null) => throw new NotSupportedException();
        public Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<MuhasebeDonem, bool>> predicate, Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null) => throw new NotSupportedException();
    }

    private sealed class FakeKdvUygulamaService : IKdvUygulamaService
    {
        public Task<KdvUygulamaResult> ValidateAndSnapshotAsync(int kdvUygulamaTipi, int? kdvIstisnaTanimId, decimal kdvOrani, decimal tutar, DateTime islemTarihi, KdvIslemYonu islemYonu, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new KdvUygulamaResult
            {
                KdvUygulamaTipi = kdvUygulamaTipi,
                KdvIstisnaTanimId = kdvIstisnaTanimId,
                KdvOrani = kdvOrani,
                KdvTutari = kdvUygulamaTipi == (int)KdvUygulamaTipi.Kdvli
                    ? Math.Round(tutar * kdvOrani / 100m, 2, MidpointRounding.AwayFromZero)
                    : 0
            });
        }
    }

    private sealed class FakeUserAccessScopeService : IUserAccessScopeService
    {
        private readonly DomainAccessScope _scope;

        public FakeUserAccessScopeService(DomainAccessScope scope) => _scope = scope;

        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_scope);
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public string? GetCurrentUserName() => "test-user";
        public Guid? GetCurrentUserId() => Guid.NewGuid();
    }

    private sealed class FakeCurrentTenantAccessor : ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => 1;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [1];
        public bool IsSuperAdmin() => false;
        public bool IsKurumAdmin() => false;
    }
}
