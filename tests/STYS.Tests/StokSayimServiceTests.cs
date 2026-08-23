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
using STYS.Muhasebe.StokLotlari.Entities;
using STYS.Muhasebe.StokSayimlari.Dtos;
using STYS.Muhasebe.StokSayimlari.Mapping;
using STYS.Muhasebe.StokSayimlari.Repositories;
using STYS.Muhasebe.StokSayimlari.Services;
using STYS.Muhasebe.StokSerileri.Entities;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKartlari.Repositories;
using STYS.Muhasebe.TasinirKodlari.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class StokSayimServiceTests
{
    [Fact]
    public async Task KesinlestirAsync_Sistem10Sayilan12_IcinFazla2Olusturur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 10, 1, StokHareketDurumlari.Aktif);
        var service = CreateSayimService(dbContext);

        var sayim = await service.AddAsync(new StokSayimDto { DepoId = 10, TesisId = 1, SayimTarihi = new DateTime(2026, 8, 23, 10, 0, 0) });
        sayim.Satirlar[0].SayilanMiktar = 12;
        await service.UpdateSatirlarAsync(sayim.Id!.Value, new UpdateStokSayimSatirlarRequest
        {
            Satirlar = [new UpdateStokSayimSatirRequest { Id = sayim.Satirlar[0].Id!.Value, SayilanMiktar = 12 }]
        });

        await service.KesinlestirAsync(sayim.Id.Value);

        var hareket = await dbContext.StokHareketleri.SingleAsync(x => x.HareketTipi == StokHareketTipleri.SayimFarki);
        Assert.Equal(StokSayimFarkiYonleri.Fazla, hareket.SayimFarkiYonu);
        Assert.Equal(2, hareket.Miktar);
    }

    [Fact]
    public async Task KesinlestirAsync_Sistem10Sayilan7_IcinEksik3Olusturur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 10, 1, StokHareketDurumlari.Aktif);
        var service = CreateSayimService(dbContext);

        var sayim = await service.AddAsync(new StokSayimDto { DepoId = 10, TesisId = 1, SayimTarihi = new DateTime(2026, 8, 23, 10, 0, 0) });
        await service.UpdateSatirlarAsync(sayim.Id!.Value, new UpdateStokSayimSatirlarRequest
        {
            Satirlar = [new UpdateStokSayimSatirRequest { Id = sayim.Satirlar[0].Id!.Value, SayilanMiktar = 7 }]
        });

        await service.KesinlestirAsync(sayim.Id.Value);

        var hareket = await dbContext.StokHareketleri.SingleAsync(x => x.HareketTipi == StokHareketTipleri.SayimFarki);
        Assert.Equal(StokSayimFarkiYonleri.Eksik, hareket.SayimFarkiYonu);
        Assert.Equal(3, hareket.Miktar);
    }

    [Fact]
    public async Task KesinlestirAsync_FarkSifirsaHareketOlusturmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 10, 1, StokHareketDurumlari.Aktif);
        var service = CreateSayimService(dbContext);

        var sayim = await service.AddAsync(new StokSayimDto { DepoId = 10, TesisId = 1, SayimTarihi = new DateTime(2026, 8, 23, 10, 0, 0) });
        await service.KesinlestirAsync(sayim.Id!.Value);

        Assert.DoesNotContain(await dbContext.StokHareketleri.ToListAsync(), x => x.HareketTipi == StokHareketTipleri.SayimFarki);
    }

    [Fact]
    public async Task KesinlestirAsync_LotSayimindaDogruStokLotIdIleHareketOlusturur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true, takipTipi: TasinirKartTakipTipleri.Lot);
        var lotId = await CreateLotAsync(dbContext, "LOT-A", new DateTime(2027, 1, 1));
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 10, 1, StokHareketDurumlari.Aktif, stokLotId: lotId);
        var service = CreateSayimService(dbContext);

        var sayim = await service.AddAsync(new StokSayimDto { DepoId = 10, TesisId = 1, SayimTarihi = new DateTime(2026, 8, 23, 10, 0, 0) });
        var satir = Assert.Single(sayim.Satirlar);
        await service.UpdateSatirlarAsync(sayim.Id!.Value, new UpdateStokSayimSatirlarRequest
        {
            Satirlar = [new UpdateStokSayimSatirRequest { Id = satir.Id!.Value, SayilanMiktar = 7 }]
        });

        await service.KesinlestirAsync(sayim.Id.Value);

        var hareket = await dbContext.StokHareketleri.SingleAsync(x => x.HareketTipi == StokHareketTipleri.SayimFarki);
        Assert.Equal(lotId, hareket.StokLotId);
        Assert.Equal(StokSayimFarkiYonleri.Eksik, hareket.SayimFarkiYonu);
    }

    [Fact]
    public async Task KesinlestirAsync_Seri1den0aIninceEksik1VeDogruSeriKimligiOlusturur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true, takipTipi: TasinirKartTakipTipleri.Seri);
        var seriId = await CreateSeriAsync(dbContext, "SN001");
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 1, 1, StokHareketDurumlari.Aktif, stokSeriId: seriId);
        var service = CreateSayimService(dbContext);

        var sayim = await service.AddAsync(new StokSayimDto { DepoId = 10, TesisId = 1, SayimTarihi = new DateTime(2026, 8, 23, 10, 0, 0) });
        var satir = Assert.Single(sayim.Satirlar);
        await service.UpdateSatirlarAsync(sayim.Id!.Value, new UpdateStokSayimSatirlarRequest
        {
            Satirlar = [new UpdateStokSayimSatirRequest { Id = satir.Id!.Value, SayilanMiktar = 0 }]
        });

        await service.KesinlestirAsync(sayim.Id.Value);

        var hareket = await dbContext.StokHareketleri.SingleAsync(x => x.HareketTipi == StokHareketTipleri.SayimFarki);
        Assert.Equal(seriId, hareket.StokSeriId);
        Assert.Equal(StokSayimFarkiYonleri.Eksik, hareket.SayimFarkiYonu);
        Assert.Equal(1, hareket.Miktar);
    }

    [Fact]
    public async Task KesinlestirAsync_SayimBasladiktanSonraStokDegisirseReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 10, 1, StokHareketDurumlari.Aktif);
        var service = CreateSayimService(dbContext);

        var sayim = await service.AddAsync(new StokSayimDto { DepoId = 10, TesisId = 1, SayimTarihi = new DateTime(2026, 8, 23, 10, 0, 0) });
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 2, 1, StokHareketDurumlari.Aktif);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.KesinlestirAsync(sayim.Id!.Value));

        Assert.Equal(409, ex.ErrorCode);
        Assert.Equal("Sayım sırasında stok hareketi oluştu. Sayım bilgilerini yenileyiniz.", ex.Message);
    }

    [Fact]
    public async Task KesinlestirAsync_KesinlesmisSayimIkinciKezKesinlestirilemez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 10, 1, StokHareketDurumlari.Aktif);
        var service = CreateSayimService(dbContext);

        var sayim = await service.AddAsync(new StokSayimDto { DepoId = 10, TesisId = 1, SayimTarihi = new DateTime(2026, 8, 23, 10, 0, 0) });
        await service.UpdateSatirlarAsync(sayim.Id!.Value, new UpdateStokSayimSatirlarRequest
        {
            Satirlar = [new UpdateStokSayimSatirRequest { Id = sayim.Satirlar[0].Id!.Value, SayilanMiktar = 12 }]
        });
        await service.KesinlestirAsync(sayim.Id.Value);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.KesinlestirAsync(sayim.Id.Value));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Sadece taslak stok sayımları değiştirilebilir.", ex.Message);
    }

    private static StysAppDbContext CreateDbContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new StysAppDbContext(options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor())
        {
            AllowExplicitTenantWritesWithoutAmbientTenant = true
        };
    }

    private static StokSayimService CreateSayimService(StysAppDbContext dbContext)
    {
        var mapperConfig = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<StokHareketProfile>();
            cfg.AddProfile<StokSayimProfile>();
        }, NullLoggerFactory.Instance);
        var mapper = mapperConfig.CreateMapper();
        var userScope = new FakeUserAccessScopeService(DomainAccessScope.Scoped([], [1], []));
        var stokHareketService = new StokHareketService(
            dbContext,
            new StokHareketRepository(dbContext, mapper),
            new DepoRepository(dbContext, mapper),
            new TasinirKartRepository(dbContext, mapper),
            new CariKartRepository(dbContext, mapper),
            new FakeMuhasebeDonemService(),
            userScope,
            new FakeKdvUygulamaService(),
            mapper);

        return new StokSayimService(
            dbContext,
            new StokSayimRepository(dbContext, mapper),
            new DepoRepository(dbContext, mapper),
            new TasinirKartRepository(dbContext, mapper),
            userScope,
            stokHareketService,
            mapper);
    }

    private static async Task SeedBaseAsync(StysAppDbContext dbContext, bool takipliMi = false, string? takipTipi = null)
    {
        dbContext.Kurumlar.Add(new Kurum { Id = 1, Ad = "Kurum", Kod = "KRM", TenantKey = "tenant", AktifMi = true });
        dbContext.Iller.Add(new Il { Id = 1, Ad = "Ankara", AktifMi = true });
        dbContext.Tesisler.Add(new Tesis { Id = 1, KurumId = 1, IlId = 1, Ad = "Tesis", Telefon = "123", Adres = "Adres", AktifMi = true });
        dbContext.MuhasebeHesapPlanlari.Add(new MuhasebeHesapPlani { Id = 1, Kod = "150.01.0001", TamKod = "150.01.0001", Ad = "Stok", SeviyeNo = 3, AktifMi = true });
        dbContext.TasinirKodlar.Add(new TasinirKod { Id = 200, Kod = "TK-001", Ad = "Kod", AktifMi = true });
        dbContext.Depolar.Add(new Depo
        {
            Id = 10,
            TesisId = 1,
            MuhasebeHesapPlaniId = 1,
            Kod = "D10",
            Ad = "Ana Depo",
            MalzemeKayitTipi = DepoMalzemeKayitTipleri.MalzemeleriAyriKayittaTut,
            AktifMi = true
        });
        dbContext.TasinirKartlar.Add(new TasinirKart
        {
            Id = 100,
            TesisId = 1,
            TasinirKodId = 200,
            MuhasebeHesapPlaniId = 1,
            StokKodu = "STK-100",
            Ad = "Kart 100",
            Birim = "Adet",
            MalzemeTipi = MalzemeTipleri.Diger,
            KdvOrani = 20,
            TakipliMi = takipliMi,
            TakipTipi = takipTipi ?? (takipliMi ? TasinirKartTakipTipleri.Lot : TasinirKartTakipTipleri.Yok),
            AktifMi = true
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task<int> SeedMovementAsync(StysAppDbContext dbContext, int depoId, int tasinirKartId, string hareketTipi, decimal miktar, decimal birimFiyat, string durum, string? sayimFarkiYonu = null, int? stokLotId = null, int? stokSeriId = null)
    {
        var hareket = new StokHareket
        {
            DepoId = depoId,
            TasinirKartId = tasinirKartId,
            HareketTarihi = new DateTime(2026, 8, 23, 9, 0, 0),
            HareketTipi = hareketTipi,
            Miktar = miktar,
            BirimFiyat = birimFiyat,
            Tutar = miktar * birimFiyat,
            Durum = durum,
            SayimFarkiYonu = sayimFarkiYonu,
            StokLotId = stokLotId,
            StokSeriId = stokSeriId,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.KdvKapsamDisi,
            KdvOrani = 0,
            KdvTutari = 0
        };
        dbContext.StokHareketleri.Add(hareket);
        await dbContext.SaveChangesAsync();
        return hareket.Id;
    }

    private static async Task<int> CreateLotAsync(StysAppDbContext dbContext, string lotNo, DateTime? skt)
    {
        var lot = new StokLot
        {
            TesisId = 1,
            TasinirKartId = 100,
            LotNo = lotNo,
            SonKullanmaTarihi = skt,
            AktifMi = true
        };
        dbContext.StokLotlar.Add(lot);
        await dbContext.SaveChangesAsync();
        return lot.Id;
    }

    private static async Task<int> CreateSeriAsync(StysAppDbContext dbContext, string seriNo)
    {
        var seri = new StokSeri
        {
            TesisId = 1,
            TasinirKartId = 100,
            SeriNo = seriNo,
            AktifMi = true
        };
        dbContext.StokSeriler.Add(seri);
        await dbContext.SaveChangesAsync();
        return seri.Id;
    }

    private sealed class FakeMuhasebeDonemService : IMuhasebeDonemService
    {
        public Task<MuhasebeDonemDto?> GetAktifDonemAsync(int tesisId, DateTime tarih, CancellationToken cancellationToken = default)
            => Task.FromResult<MuhasebeDonemDto?>(new MuhasebeDonemDto
            {
                Id = 1,
                TesisId = tesisId,
                BaslangicTarihi = new DateTime(tarih.Year, tarih.Month, 1),
                BitisTarihi = new DateTime(tarih.Year, tarih.Month, DateTime.DaysInMonth(tarih.Year, tarih.Month)),
                KapaliMi = false,
                MaliYil = tarih.Year,
                DonemNo = tarih.Month
            });

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
            => Task.FromResult(new KdvUygulamaResult
            {
                KdvUygulamaTipi = kdvUygulamaTipi,
                KdvIstisnaTanimId = kdvIstisnaTanimId,
                KdvOrani = kdvOrani,
                KdvTutari = 0
            });
    }

    private sealed class FakeUserAccessScopeService : IUserAccessScopeService
    {
        private readonly DomainAccessScope _scope;
        public FakeUserAccessScopeService(DomainAccessScope scope) => _scope = scope;
        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default) => Task.FromResult(_scope);
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public Guid? GetCurrentUserId() => Guid.NewGuid();
        public string? GetCurrentUserName() => "test-user";
    }

    private sealed class FakeCurrentTenantAccessor : ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => 1;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [1];
        public bool IsSuperAdmin() => false;
        public bool IsKurumAdmin() => false;
    }
}
