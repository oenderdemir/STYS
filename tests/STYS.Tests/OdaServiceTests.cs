using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.AccessScope;
using STYS.Binalar.Entities;
using STYS.Binalar.Repositories;
using STYS.Iller.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Odalar.Dto;
using STYS.Odalar.Entities;
using STYS.Odalar.Mapping;
using STYS.Odalar.Repositories;
using STYS.Odalar.Services;
using STYS.OdaOzellikleri;
using STYS.OdaOzellikleri.Entities;
using STYS.OdaOzellikleri.Repositories;
using STYS.OdaSiniflari.Entities;
using STYS.OdaTipleri.Entities;
using STYS.OdaTipleri.Repositories;
using STYS.Tesisler.Entities;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class OdaServiceTests
{
    [Fact]
    public async Task AddAsync_KapasiteBosIseOdaTipiKapasitesiniKullanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedFixtureAsync(dbContext, odaTipiKapasite: 4);
        var service = CreateService(dbContext);

        var result = await service.AddAsync(CreateOdaDto(kapasite: 0));

        Assert.Equal(4, result.Kapasite);
        Assert.Equal(4, await dbContext.Odalar.Select(x => x.Kapasite).SingleAsync());
    }

    [Fact]
    public async Task AddAsync_ManuelKapasiteVarsaOdaTipiKapasitesiniEzmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedFixtureAsync(dbContext, odaTipiKapasite: 4);
        var service = CreateService(dbContext);

        var result = await service.AddAsync(CreateOdaDto(kapasite: 2));

        Assert.Equal(2, result.Kapasite);
        Assert.Equal(2, await dbContext.Odalar.Select(x => x.Kapasite).SingleAsync());
    }

    [Fact]
    public async Task UpdateAsync_OdaTipiDegisseBileOdaKapasitesiniKorur()
    {
        await using var dbContext = CreateDbContext();
        await SeedFixtureAsync(dbContext, odaTipiKapasite: 2);
        dbContext.OdaTipleri.Add(new OdaTipi
        {
            Id = 2,
            TesisId = 1,
            OdaSinifiId = 1,
            Ad = "Deluxe",
            Kapasite = 6,
            AktifMi = true
        });
        dbContext.Odalar.Add(new Oda
        {
            Id = 10,
            OdaNo = "101",
            BinaId = 1,
            TesisOdaTipiId = 1,
            KatNo = 1,
            Kapasite = 3,
            AktifMi = true
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var result = await service.UpdateAsync(CreateOdaDto(id: 10, odaNo: "101", tesisOdaTipiId: 2, kapasite: 3));

        Assert.Equal(2, result.TesisOdaTipiId);
        Assert.Equal(3, result.Kapasite);
        var savedRoom = await dbContext.Odalar.IgnoreQueryFilters().SingleAsync(x => x.Id == 10);
        Assert.Equal(2, savedRoom.TesisOdaTipiId);
        Assert.Equal(3, savedRoom.Kapasite);
    }

    [Fact]
    public async Task UpdateAsync_OdaTipiKapasitesiDegisseMevcutOdaKapasitesiniGuncellemez()
    {
        await using var dbContext = CreateDbContext();
        await SeedFixtureAsync(dbContext, odaTipiKapasite: 2);
        dbContext.Odalar.Add(new Oda
        {
            Id = 10,
            OdaNo = "101",
            BinaId = 1,
            TesisOdaTipiId = 1,
            KatNo = 1,
            Kapasite = 3,
            AktifMi = true
        });
        await dbContext.SaveChangesAsync();
        dbContext.OdaTipleri.Local.Single(x => x.Id == 1).Kapasite = 7;
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var result = await service.UpdateAsync(CreateOdaDto(id: 10, odaNo: "101", kapasite: 3));

        Assert.Equal(3, result.Kapasite);
        Assert.Equal(3, await dbContext.Odalar.Select(x => x.Kapasite).SingleAsync());
    }

    [Fact]
    public async Task AddAsync_YatakSayisiOdaKapasitesiniAsarsaHataVerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedFixtureAsync(dbContext, odaTipiKapasite: 4, paylasimliMi: true, yatakSayisiOzelligiEkle: true);
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<BaseException>(() => service.AddAsync(CreateOdaDto(
            kapasite: 2,
            odaOzellikDegerleri:
            [
                new OdaOzellikDegerDto { OdaOzellikId = 1, Deger = "3" }
            ])));

        Assert.Equal(400, exception.ErrorCode);
        Assert.Equal("Yatak sayisi oda kapasitesini asamaz.", exception.Message);
    }

    private static OdaService CreateService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        return new OdaService(
            new OdaRepository(dbContext, mapper),
            new BinaRepository(dbContext, mapper),
            new OdaTipiRepository(dbContext, mapper),
            new OdaOzellikRepository(dbContext, mapper),
            new OdaOzellikDegerRepository(dbContext, mapper),
            new FakeUserAccessScopeService(),
            mapper);
    }

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<OdaProfile>(), NullLoggerFactory.Instance);
        return config.CreateMapper();
    }

    private static StysAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new StysAppDbContext(options, null, new FakeCurrentTenantAccessor());
    }

    private static async Task SeedFixtureAsync(
        StysAppDbContext dbContext,
        int odaTipiKapasite,
        bool paylasimliMi = false,
        bool yatakSayisiOzelligiEkle = false)
    {
        dbContext.Iller.Add(new Il { Id = 1, Ad = "Ankara", AktifMi = true });
        dbContext.Tesisler.Add(new Tesis
        {
            Id = 1,
            KurumId = 1,
            IlId = 1,
            Ad = "Test Tesisi",
            Telefon = "000",
            Adres = "Adres",
            AktifMi = true
        });
        dbContext.Binalar.Add(new Bina
        {
            Id = 1,
            TesisId = 1,
            Ad = "A Blok",
            KatSayisi = 5,
            AktifMi = true
        });
        dbContext.OdaSiniflari.Add(new OdaSinifi
        {
            Id = 1,
            Kod = "STD",
            Ad = "Standart",
            AktifMi = true
        });
        dbContext.OdaTipleri.Add(new OdaTipi
        {
            Id = 1,
            TesisId = 1,
            OdaSinifiId = 1,
            Ad = "Standart",
            Kapasite = odaTipiKapasite,
            PaylasimliMi = paylasimliMi,
            AktifMi = true
        });

        if (yatakSayisiOzelligiEkle)
        {
            dbContext.OdaOzellikleri.Add(new OdaOzellik
            {
                Id = 1,
                Kod = "YATAK_SAYISI",
                Ad = "Yatak Sayisi",
                VeriTipi = OdaOzellikVeriTipleri.Number,
                AktifMi = true
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static OdaDto CreateOdaDto(
        int? id = null,
        string odaNo = "101",
        int tesisOdaTipiId = 1,
        int kapasite = 1,
        ICollection<OdaOzellikDegerDto>? odaOzellikDegerleri = null)
    {
        return new OdaDto
        {
            Id = id,
            OdaNo = odaNo,
            BinaId = 1,
            TesisOdaTipiId = tesisOdaTipiId,
            KatNo = 1,
            Kapasite = kapasite,
            AktifMi = true,
            OdaOzellikDegerleri = odaOzellikDegerleri ?? []
        };
    }

    private sealed class FakeUserAccessScopeService : IUserAccessScopeService
    {
        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DomainAccessScope.Unscoped());
        }
    }

    private sealed class FakeCurrentTenantAccessor : ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => 1;

        public IReadOnlyList<int> GetAccessibleKurumIds() => [1];

        public bool IsSuperAdmin() => false;

        public bool IsKurumAdmin() => true;
    }
}
