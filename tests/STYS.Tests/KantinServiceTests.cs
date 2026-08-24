using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.AccessScope;
using STYS.Iller.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.KantinYonetimi.Kantinler.Dtos;
using STYS.KantinYonetimi.Kantinler.Mapping;
using STYS.KantinYonetimi.Kantinler.Repositories;
using STYS.KantinYonetimi.Kantinler.Services;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokHareketleri.Mapping;
using STYS.Muhasebe.StokHareketleri.Repositories;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKartlari.Mapping;
using STYS.Muhasebe.TasinirKodlari.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class KantinServiceTests
{
    [Fact]
    public async Task Kantin_AyniTesisDeposuIleOlusturulur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);

        var created = await service.AddAsync(new KantinDto
        {
            TesisId = 1,
            DepoId = 10,
            Kod = " KNT-01 ",
            Ad = " Merkez Kantin ",
            AktifMi = true
        });

        Assert.NotNull(created.Id);
        Assert.Equal(1, created.TesisId);
        Assert.Equal(10, created.DepoId);
        Assert.Equal("KNT-01", created.Kod);
        Assert.Equal("Merkez Kantin", created.Ad);
    }

    [Fact]
    public async Task Kantin_CrossTesisDepoReddedilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddAsync(new KantinDto
        {
            TesisId = 1,
            DepoId = 20,
            Kod = "KNT-01",
            Ad = "Merkez Kantin"
        }));

        Assert.Equal("Seçilen depo kantin ile aynı tesise ait olmalıdır.", ex.Message);
    }

    [Fact]
    public async Task Kantin_CrossTesisVarsayilanKasaReddedilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddAsync(new KantinDto
        {
            TesisId = 1,
            DepoId = 10,
            VarsayilanNakitKasaId = 200,
            Kod = "KNT-01",
            Ad = "Merkez Kantin"
        }));

        Assert.Equal("Seçilen varsayılan kasa kantin ile aynı tesise ait olmalıdır.", ex.Message);
    }

    [Fact]
    public async Task Kantin_NakitKasaOlmayanVarsayilanHesapReddedilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddAsync(new KantinDto
        {
            TesisId = 1,
            DepoId = 10,
            VarsayilanNakitKasaId = 101,
            Kod = "KNT-01",
            Ad = "Merkez Kantin"
        }));

        Assert.Equal("Varsayılan kasa yalnızca nakit kasa tipinde olabilir.", ex.Message);
    }

    [Fact]
    public async Task Kantin_CrossTesisPerakendeCariReddedilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddAsync(new KantinDto
        {
            TesisId = 1,
            DepoId = 10,
            PerakendeCariKartId = 200,
            Kod = "KNT-01",
            Ad = "Merkez Kantin"
        }));

        Assert.Equal("Seçilen perakende cari kantin ile aynı tesise ait olmalıdır.", ex.Message);
    }

    [Fact]
    public async Task Kantin_KoduAyniTesisIcindeUniqueOlmalidir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        dbContext.Kantinler.Add(new STYS.KantinYonetimi.Kantinler.Entities.Kantin
        {
            TesisId = 1,
            DepoId = 10,
            Kod = "KNT-01",
            Ad = "Eski Kantin",
            AktifMi = true
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddAsync(new KantinDto
        {
            TesisId = 1,
            DepoId = 10,
            Kod = "knt-01",
            Ad = "Yeni Kantin"
        }));

        Assert.Equal("Aynı tesis içinde bu kantin kodu zaten kullanılıyor.", ex.Message);
    }

    [Fact]
    public async Task AyniUrun_BirKantineIkiKezEklenemez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var kantin = await CreateKantinAsync(service);

        await service.AddUrunAsync(kantin.Id!.Value, new KantinUrunDto
        {
            TasinirKartId = 100,
            SatisFiyati = 15
        });

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddUrunAsync(kantin.Id!.Value, new KantinUrunDto
        {
            TasinirKartId = 100,
            SatisFiyati = 20
        }));

        Assert.Equal("Aynı taşınır kart aynı kantine birden fazla eklenemez.", ex.Message);
    }

    [Fact]
    public async Task CrossTesisTasinirKartReddedilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var kantin = await CreateKantinAsync(service);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddUrunAsync(kantin.Id!.Value, new KantinUrunDto
        {
            TasinirKartId = 200,
            SatisFiyati = 15
        }));

        Assert.Equal("Seçilen taşınır kart kantin ile aynı tesise ait olmalıdır.", ex.Message);
    }

    [Fact]
    public async Task NegatifSatisFiyatiReddedilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var kantin = await CreateKantinAsync(service);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddUrunAsync(kantin.Id!.Value, new KantinUrunDto
        {
            TasinirKartId = 100,
            SatisFiyati = -1
        }));

        Assert.Equal("Satış fiyatı negatif olamaz.", ex.Message);
    }

    [Fact]
    public async Task Barkod_AyniKantinIcindeDuplicateOlamaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var kantin = await CreateKantinAsync(service);

        await service.AddUrunAsync(kantin.Id!.Value, new KantinUrunDto
        {
            TasinirKartId = 100,
            Barkod = " abc123 ",
            SatisFiyati = 10
        });

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddUrunAsync(kantin.Id!.Value, new KantinUrunDto
        {
            TasinirKartId = 101,
            Barkod = "ABC123",
            SatisFiyati = 11
        }));

        Assert.Equal("Aynı kantin içinde bu barkod zaten kullanılıyor.", ex.Message);
    }

    [Fact]
    public async Task UrunListesi_DogruDepoStokBakiyesiniGosterir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedStockAsync(dbContext);
        var service = CreateService(dbContext);
        var kantin = await CreateKantinAsync(service);

        await service.AddUrunAsync(kantin.Id!.Value, new KantinUrunDto
        {
            TasinirKartId = 100,
            SatisFiyati = 25
        });

        var urun = Assert.Single(await service.GetUrunlerAsync(kantin.Id!.Value));

        Assert.Equal("STK-001", urun.StokKodu);
        Assert.Equal("Deterjan", urun.UrunAdi);
        Assert.Equal("Adet", urun.Birim);
        Assert.Equal(8m, urun.KdvOrani);
        Assert.Equal(7m, urun.MevcutStok);
    }

    [Fact]
    public async Task KantinIslemleri_StokHareketVeTahsilatBelgesiOlusturmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);

        var beforeStok = await dbContext.StokHareketleri.CountAsync();
        var beforeTahsilat = await dbContext.TahsilatOdemeBelgeleri.CountAsync();

        var kantin = await CreateKantinAsync(service);
        await service.UpdateAsync(new KantinDto
        {
            Id = kantin.Id,
            TesisId = 1,
            DepoId = 10,
            Kod = "KNT-01",
            Ad = "Merkez Kantin Güncel",
            AktifMi = true
        });

        var urun = await service.AddUrunAsync(kantin.Id!.Value, new KantinUrunDto
        {
            TasinirKartId = 100,
            SatisFiyati = 30
        });

        await service.UpdateUrunAsync(kantin.Id!.Value, new KantinUrunDto
        {
            Id = urun.Id,
            TasinirKartId = 100,
            SatisFiyati = 35,
            AktifMi = true
        });

        Assert.Equal(beforeStok, await dbContext.StokHareketleri.CountAsync());
        Assert.Equal(beforeTahsilat, await dbContext.TahsilatOdemeBelgeleri.CountAsync());
    }

    [Fact]
    public void AddKantinModuleK1Migration_MigrationsAssemblydeDiscoverEdilir()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=StysMigrationDiscoveryKantin;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var dbContext = new StysAppDbContext(options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor())
        {
            AllowExplicitTenantWritesWithoutAmbientTenant = true
        };

        var migrationsAssembly = dbContext.GetService<IMigrationsAssembly>();

        Assert.True(migrationsAssembly.Migrations.ContainsKey("20260824191516_AddKantinModuleK1"));
    }

    private static async Task<KantinDto> CreateKantinAsync(KantinService service)
        => await service.AddAsync(new KantinDto
        {
            TesisId = 1,
            DepoId = 10,
            Kod = "KNT-01",
            Ad = "Merkez Kantin",
            AktifMi = true
        });

    private static async Task SeedBaseAsync(StysAppDbContext dbContext)
    {
        dbContext.Iller.Add(new Il
        {
            Id = 1,
            Ad = "Ankara",
            AktifMi = true
        });

        dbContext.Kurumlar.Add(new Kurum
        {
            Id = 1,
            Kod = "KRM",
            Ad = "Test Kurum",
            AktifMi = true
        });

        dbContext.Tesisler.AddRange(
            new Tesis
            {
                Id = 1,
                KurumId = 1,
                IlId = 1,
                Ad = "Tesis A",
                Telefon = "03120000000",
                Adres = "Adres A",
                AktifMi = true
            },
            new Tesis
            {
                Id = 2,
                KurumId = 1,
                IlId = 1,
                Ad = "Tesis B",
                Telefon = "03120000001",
                Adres = "Adres B",
                AktifMi = true
            });

        dbContext.Depolar.AddRange(
            new Depo { Id = 10, TesisId = 1, Kod = "DEP-A", Ad = "Merkez Depo", AktifMi = true },
            new Depo { Id = 20, TesisId = 2, Kod = "DEP-B", Ad = "Yan Depo", AktifMi = true });

        dbContext.KasaBankaHesaplari.AddRange(
            new KasaBankaHesap
            {
                Id = 100,
                TesisId = 1,
                Tip = KasaBankaHesapTipleri.NakitKasa,
                Kod = "KASA-A",
                Ad = "Merkez Nakit Kasa",
                AktifMi = true
            },
            new KasaBankaHesap
            {
                Id = 101,
                TesisId = 1,
                Tip = KasaBankaHesapTipleri.Banka,
                Kod = "BANKA-A",
                Ad = "Banka Hesabı",
                AktifMi = true
            },
            new KasaBankaHesap
            {
                Id = 200,
                TesisId = 2,
                Tip = KasaBankaHesapTipleri.NakitKasa,
                Kod = "KASA-B",
                Ad = "Yan Nakit Kasa",
                AktifMi = true
            });

        dbContext.CariKartlar.AddRange(
            new CariKart
            {
                Id = 100,
                TesisId = 1,
                CariTipi = CariKartTipleri.Musteri,
                CariKodu = "PRK-A",
                UnvanAdSoyad = "Perakende Müşteri A",
                AktifMi = true
            },
            new CariKart
            {
                Id = 200,
                TesisId = 2,
                CariTipi = CariKartTipleri.Musteri,
                CariKodu = "PRK-B",
                UnvanAdSoyad = "Perakende Müşteri B",
                AktifMi = true
            });

        dbContext.TasinirKodlar.Add(new TasinirKod
        {
            Id = 1,
            Kod = "150.01",
            Ad = "Temizlik Malzemeleri",
            AktifMi = true
        });

        dbContext.TasinirKartlar.AddRange(
            new TasinirKart
            {
                Id = 100,
                TesisId = 1,
                TasinirKodId = 1,
                StokKodu = "STK-001",
                Ad = "Deterjan",
                Birim = "Adet",
                MalzemeTipi = "Sarf",
                KdvOrani = 8,
                AktifMi = true,
                TakipliMi = false,
                TakipTipi = TasinirKartTakipTipleri.Yok
            },
            new TasinirKart
            {
                Id = 101,
                TesisId = 1,
                TasinirKodId = 1,
                StokKodu = "STK-002",
                Ad = "Su",
                Birim = "Adet",
                MalzemeTipi = "Sarf",
                KdvOrani = 8,
                AktifMi = true,
                TakipliMi = false,
                TakipTipi = TasinirKartTakipTipleri.Yok
            },
            new TasinirKart
            {
                Id = 200,
                TesisId = 2,
                TasinirKodId = 1,
                StokKodu = "STK-900",
                Ad = "Farklı Tesis Ürünü",
                Birim = "Adet",
                MalzemeTipi = "Sarf",
                KdvOrani = 20,
                AktifMi = true,
                TakipliMi = false,
                TakipTipi = TasinirKartTakipTipleri.Yok
            });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedStockAsync(StysAppDbContext dbContext)
    {
        dbContext.StokHareketleri.AddRange(
            new StokHareket
            {
                Id = 1,
                DepoId = 10,
                TasinirKartId = 100,
                HareketTarihi = new DateTime(2026, 8, 24, 9, 0, 0),
                HareketTipi = StokHareketTipleri.Giris,
                Miktar = 10,
                BirimFiyat = 5,
                Tutar = 50,
                Durum = StokHareketDurumlari.Aktif,
                KdvUygulamaTipi = 1,
                KdvOrani = 8,
                KdvTutari = 4
            },
            new StokHareket
            {
                Id = 2,
                DepoId = 10,
                TasinirKartId = 100,
                HareketTarihi = new DateTime(2026, 8, 24, 10, 0, 0),
                HareketTipi = StokHareketTipleri.Cikis,
                Miktar = 3,
                BirimFiyat = 5,
                Tutar = 15,
                Durum = StokHareketDurumlari.Aktif,
                KdvUygulamaTipi = 1,
                KdvOrani = 8,
                KdvTutari = 1.2m
            },
            new StokHareket
            {
                Id = 3,
                DepoId = 20,
                TasinirKartId = 100,
                HareketTarihi = new DateTime(2026, 8, 24, 11, 0, 0),
                HareketTipi = StokHareketTipleri.Giris,
                Miktar = 99,
                BirimFiyat = 5,
                Tutar = 495,
                Durum = StokHareketDurumlari.Aktif,
                KdvUygulamaTipi = 1,
                KdvOrani = 8,
                KdvTutari = 39.6m
            });

        await dbContext.SaveChangesAsync();
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

    private static KantinService CreateService(StysAppDbContext dbContext, DomainAccessScope? scope = null)
    {
        var mapperConfig = new MapperConfiguration(cfg =>
        {
            cfg.AddMaps(typeof(KantinProfile).Assembly);
            cfg.AddMaps(typeof(TasinirKartProfile).Assembly);
            cfg.AddMaps(typeof(StokHareketProfile).Assembly);
        }, NullLoggerFactory.Instance);
        var mapper = mapperConfig.CreateMapper();

        return new KantinService(
            dbContext,
            new FakeUserAccessScopeService(scope ?? DomainAccessScope.Unscoped()),
            new StokHareketRepository(dbContext, mapper),
            new KantinRepository(dbContext, mapper),
            new KantinUrunRepository(dbContext, mapper),
            mapper);
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public string? GetCurrentUserName() => "kantin-test";
        public Guid? GetCurrentUserId() => Guid.Parse("11111111-1111-1111-1111-111111111111");
    }

    private sealed class FakeCurrentTenantAccessor : ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => null;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [];
        public bool IsSuperAdmin() => true;
        public bool IsKurumAdmin() => false;
    }

    private sealed class FakeUserAccessScopeService(DomainAccessScope scope) : IUserAccessScopeService
    {
        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(scope);
    }
}
