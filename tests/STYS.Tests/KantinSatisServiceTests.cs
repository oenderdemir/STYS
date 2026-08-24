using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.AccessScope;
using STYS.Iller.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.KantinYonetimi.KantinSatislari.Dtos;
using STYS.KantinYonetimi.KantinSatislari.Mapping;
using STYS.KantinYonetimi.KantinSatislari.Repositories;
using STYS.KantinYonetimi.KantinSatislari.Services;
using STYS.KantinYonetimi.Kantinler.Entities;
using STYS.KantinYonetimi.Kantinler.Mapping;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.KasaHareketleri.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokHareketleri.Services;
using STYS.Muhasebe.StokLotlari.Dtos;
using STYS.Muhasebe.StokLotlari.Entities;
using STYS.Muhasebe.StokSerileri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKodlari.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class KantinSatisServiceTests
{
    [Fact]
    public async Task Barkodla_AktifKantinUrun_Bulunur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.GetAktifUrunByBarkodAsync(1, "  abc123  ");

        Assert.NotNull(result);
        Assert.Equal(1, result!.KantinUrunId);
        Assert.Equal("STK-001", result.StokKodu);
    }

    [Fact]
    public async Task Barkodla_SoftDeleteKantinUrun_Bulunmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var urun = await dbContext.KantinUrunler.SingleAsync(x => x.Id == 1);
        urun.IsDeleted = true;
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var result = await service.GetAktifUrunByBarkodAsync(1, "ABC123");

        Assert.Null(result);
    }

    [Fact]
    public async Task SatirFiyati_ClienttanDegil_KantinUrundenSnapshotAlinir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await service.AddAsync(new KantinSatisDto { KantinId = 1, SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0) });

        var updated = await service.AddSatirAsync(satis.Id!.Value, new AddKantinSatisSatirRequest
        {
            KantinUrunId = 1,
            Miktar = 2
        });

        var satir = Assert.Single(updated.Satirlar);
        Assert.Equal(50m, satir.BirimSatisFiyati);
        Assert.Equal(100m, satir.ToplamTutar);
    }

    [Fact]
    public async Task KdvDahilFiyatHesabi_Dogru()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await service.AddAsync(new KantinSatisDto { KantinId = 1, SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0) });

        var updated = await service.AddSatirAsync(satis.Id!.Value, new AddKantinSatisSatirRequest
        {
            KantinUrunId = 1,
            Miktar = 2
        });

        var satir = Assert.Single(updated.Satirlar);
        Assert.Equal(92.59m, satir.Matrah);
        Assert.Equal(7.41m, satir.KdvTutari);
        Assert.Equal(100m, satir.ToplamTutar);
    }

    [Fact]
    public async Task OdemeToplami_SatisToplaminaEsitDegilse_KesinlestirmeReddedilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(service);
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest
        {
            OdemeYontemi = OdemeYontemleri.Nakit,
            Tutar = 10
        });

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.KesinlestirAsync(satis.Id!.Value));

        Assert.Equal("Ödeme toplamı satış toplamına eşit olmalıdır.", ex.Message);
    }

    [Fact]
    public async Task NakitOdeme_DefaultVeRequestHesapYoksa_Reddedilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var kantin = await dbContext.Kantinler.SingleAsync(x => x.Id == 1);
        kantin.VarsayilanNakitKasaId = null;
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(service);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest
        {
            OdemeYontemi = OdemeYontemleri.Nakit,
            Tutar = 50
        }));

        Assert.Equal("Nakit ödeme için kasa seçimi zorunludur.", ex.Message);
    }

    [Fact]
    public async Task NakitOdeme_RequestHesapYoksa_DefaultKasaCozulur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(service);

        var result = await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest
        {
            OdemeYontemi = OdemeYontemleri.Nakit,
            Tutar = 50
        });

        var odeme = Assert.Single(result.Odemeler);
        Assert.Equal(100, odeme.KasaBankaHesapId);
    }

    [Fact]
    public async Task NakitOdeme_GecerliRequestHesabi_DefaultKasayiEzer()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        dbContext.KasaBankaHesaplari.Add(new KasaBankaHesap
        {
            Id = 103,
            TesisId = 1,
            Tip = KasaBankaHesapTipleri.NakitKasa,
            Kod = "KASA-B",
            Ad = "Alternatif Nakit Kasa",
            AktifMi = true
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(service);

        var result = await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest
        {
            OdemeYontemi = OdemeYontemleri.Nakit,
            KasaBankaHesapId = 103,
            Tutar = 50
        });

        var odeme = Assert.Single(result.Odemeler);
        Assert.Equal(103, odeme.KasaBankaHesapId);
    }

    [Fact]
    public async Task NakitOdeme_YanlisHesapTipiyle_Reddedilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(service);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest
        {
            OdemeYontemi = OdemeYontemleri.Nakit,
            KasaBankaHesapId = 101,
            Tutar = 50
        }));

        Assert.Equal("Nakit ödeme hesabı tipi geçersiz.", ex.Message);
    }

    [Fact]
    public async Task KrediKartiOdeme_CrossTesisHesapla_Reddedilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(service);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest
        {
            OdemeYontemi = OdemeYontemleri.KrediKarti,
            KasaBankaHesapId = 300,
            Tutar = 50
        }));

        Assert.Equal("Seçilen ödeme hesabı satış ile aynı tesise ait olmalıdır.", ex.Message);
    }

    [Fact]
    public async Task YetersizStokta_KesinlestirmeRollbackOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await service.AddAsync(new KantinSatisDto { KantinId = 1, SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0) });
        await service.AddSatirAsync(satis.Id!.Value, new AddKantinSatisSatirRequest { KantinUrunId = 1, Miktar = 99 });
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 4950 });

        await Assert.ThrowsAsync<BaseException>(() => service.KesinlestirAsync(satis.Id!.Value));

        Assert.Equal(0, await dbContext.StokHareketleri.CountAsync(x => x.KaynakModul == "KantinSatisSatir"));
        var persisted = await service.GetByIdAsync(satis.Id!.Value);
        Assert.NotNull(persisted);
        Assert.Equal("Taslak", persisted!.Durum);
    }

    [Fact]
    public async Task Kesinlesme_StoktanDogruMiktariDuser()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(service);
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 50 });

        var result = await service.KesinlestirAsync(satis.Id!.Value);

        Assert.Equal("Kesinlesti", result.Durum);
        var hareket = await dbContext.StokHareketleri.SingleAsync(x => x.KaynakModul == "KantinSatisSatir");
        Assert.Equal(1m, hareket.Miktar);
        Assert.Equal(StokHareketTipleri.Cikis, hareket.HareketTipi);
    }

    [Fact]
    public async Task LotTakipliUrun_DogruLotla_Satilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await service.AddAsync(new KantinSatisDto { KantinId = 1, SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0) });
        await service.AddSatirAsync(satis.Id!.Value, new AddKantinSatisSatirRequest { KantinUrunId = 2, Miktar = 2, StokLotId = 1 });
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 40 });

        var result = await service.KesinlestirAsync(satis.Id!.Value);

        var satir = Assert.Single(result.Satirlar);
        Assert.Equal(1, satir.StokLotId);
        Assert.Equal("LOT-A", satir.LotNo);
    }

    [Fact]
    public async Task SeriTakipliUrun_QtyBirVeDogruSeriIle_Satilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await service.AddAsync(new KantinSatisDto { KantinId = 1, SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0) });
        await service.AddSatirAsync(satis.Id!.Value, new AddKantinSatisSatirRequest { KantinUrunId = 3, Miktar = 1, StokSeriId = 1 });
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.KrediKarti, KasaBankaHesapId = 102, Tutar = 75 });

        var result = await service.KesinlestirAsync(satis.Id!.Value);

        var satir = Assert.Single(result.Satirlar);
        Assert.Equal(1, satir.StokSeriId);
        Assert.Equal("SN001", satir.SeriNo);
    }

    [Fact]
    public async Task Kesinlestirme_IkinciKezCagrilinca_StokIkiKezDusmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(service);
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 50 });

        await service.KesinlestirAsync(satis.Id!.Value);
        await service.KesinlestirAsync(satis.Id!.Value);

        Assert.Equal(1, await dbContext.StokHareketleri.CountAsync(x => x.KaynakModul == "KantinSatisSatir"));
    }

    [Fact]
    public async Task KesinlesmisSatis_Degistirilemez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(service);
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 50 });
        await service.KesinlestirAsync(satis.Id!.Value);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddSatirAsync(satis.Id!.Value, new AddKantinSatisSatirRequest
        {
            KantinUrunId = 1,
            Miktar = 1
        }));

        Assert.Equal("Kesinleşmiş kantin satışları değiştirilemez.", ex.Message);
    }

    [Fact]
    public async Task K2_TahsilatVeKasaMuhasebeKaydiOlusturmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(service);
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 50 });

        var tahsilat = await dbContext.TahsilatOdemeBelgeleri.CountAsync();
        var kasa = await dbContext.KasaHareketleri.CountAsync();
        var pos = await dbContext.PosTahsilatValorleri.CountAsync();
        var fis = await dbContext.MuhasebeFisler.CountAsync();

        await service.KesinlestirAsync(satis.Id!.Value);

        Assert.Equal(tahsilat, await dbContext.TahsilatOdemeBelgeleri.CountAsync());
        Assert.Equal(kasa, await dbContext.KasaHareketleri.CountAsync());
        Assert.Equal(pos, await dbContext.PosTahsilatValorleri.CountAsync());
        Assert.Equal(fis, await dbContext.MuhasebeFisler.CountAsync());
    }

    [Fact]
    public void AddKantinSalesK2Migration_MigrationsAssemblydeDiscoverEdilir()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=StysMigrationDiscoveryKantinSatis;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var dbContext = new StysAppDbContext(options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor())
        {
            AllowExplicitTenantWritesWithoutAmbientTenant = true
        };

        var migrationsAssembly = dbContext.GetService<IMigrationsAssembly>();
        Assert.True(migrationsAssembly.Migrations.ContainsKey("20260824194650_AddKantinSalesK2"));
    }

    private static async Task<KantinSatisDto> CreateDraftWithSingleLineAsync(KantinSatisService service)
    {
        var satis = await service.AddAsync(new KantinSatisDto
        {
            KantinId = 1,
            SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0)
        });

        return await service.AddSatirAsync(satis.Id!.Value, new AddKantinSatisSatirRequest
        {
            KantinUrunId = 1,
            Miktar = 1
        });
    }

    private static async Task SeedBaseAsync(StysAppDbContext dbContext)
    {
        dbContext.Iller.Add(new Il { Id = 1, Ad = "Ankara", AktifMi = true });
        dbContext.Kurumlar.Add(new Kurum { Id = 1, Kod = "KRM", Ad = "Test Kurum", AktifMi = true });
        dbContext.Tesisler.AddRange(
            new Tesis { Id = 1, KurumId = 1, IlId = 1, Ad = "Tesis A", Telefon = "03120000000", Adres = "Adres A", AktifMi = true },
            new Tesis { Id = 2, KurumId = 1, IlId = 1, Ad = "Tesis B", Telefon = "03120000001", Adres = "Adres B", AktifMi = true });

        dbContext.Depolar.AddRange(
            new Depo { Id = 10, TesisId = 1, Kod = "DEP-A", Ad = "Merkez Depo", AktifMi = true },
            new Depo { Id = 20, TesisId = 2, Kod = "DEP-B", Ad = "Yan Depo", AktifMi = true });

        dbContext.KasaBankaHesaplari.AddRange(
            new KasaBankaHesap { Id = 100, TesisId = 1, Tip = KasaBankaHesapTipleri.NakitKasa, Kod = "KASA-A", Ad = "Nakit Kasa", AktifMi = true },
            new KasaBankaHesap { Id = 101, TesisId = 1, Tip = KasaBankaHesapTipleri.Banka, Kod = "BANKA-A", Ad = "Banka", AktifMi = true },
            new KasaBankaHesap { Id = 102, TesisId = 1, Tip = KasaBankaHesapTipleri.KrediKarti, Kod = "POS-A", Ad = "POS", AktifMi = true },
            new KasaBankaHesap { Id = 300, TesisId = 2, Tip = KasaBankaHesapTipleri.KrediKarti, Kod = "POS-B", Ad = "POS B", AktifMi = true });

        dbContext.TasinirKodlar.Add(new TasinirKod { Id = 1, Kod = "150.01", Ad = "Tüketim", AktifMi = true });

        dbContext.TasinirKartlar.AddRange(
            new TasinirKart
            {
                Id = 1,
                TesisId = 1,
                TasinirKodId = 1,
                StokKodu = "STK-001",
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
                Id = 2,
                TesisId = 1,
                TasinirKodId = 1,
                StokKodu = "STK-002",
                Ad = "Lotlu Ürün",
                Birim = "Adet",
                MalzemeTipi = "Sarf",
                KdvOrani = 10,
                AktifMi = true,
                TakipliMi = true,
                TakipTipi = TasinirKartTakipTipleri.Lot
            },
            new TasinirKart
            {
                Id = 3,
                TesisId = 1,
                TasinirKodId = 1,
                StokKodu = "STK-003",
                Ad = "Serili Ürün",
                Birim = "Adet",
                MalzemeTipi = "Sarf",
                KdvOrani = 20,
                AktifMi = true,
                TakipliMi = true,
                TakipTipi = TasinirKartTakipTipleri.Seri
            });

        dbContext.Kantinler.Add(new Kantin
        {
            Id = 1,
            TesisId = 1,
            DepoId = 10,
            VarsayilanNakitKasaId = 100,
            Kod = "KNT-01",
            Ad = "Merkez Kantin",
            AktifMi = true
        });

        dbContext.KantinUrunler.AddRange(
            new KantinUrun { Id = 1, KantinId = 1, TasinirKartId = 1, Barkod = "ABC123", SatisFiyati = 50, AktifMi = true },
            new KantinUrun { Id = 2, KantinId = 1, TasinirKartId = 2, Barkod = "LOT001", SatisFiyati = 20, AktifMi = true },
            new KantinUrun { Id = 3, KantinId = 1, TasinirKartId = 3, Barkod = "SER001", SatisFiyati = 75, AktifMi = true });

        dbContext.StokLotlar.Add(new StokLot { Id = 1, TesisId = 1, TasinirKartId = 2, LotNo = "LOT-A", SonKullanmaTarihi = new DateTime(2026, 12, 31), AktifMi = true });
        dbContext.StokSeriler.Add(new StokSeri { Id = 1, TesisId = 1, TasinirKartId = 3, SeriNo = "SN001", AktifMi = true });

        dbContext.StokHareketleri.AddRange(
            new StokHareket
            {
                Id = 1,
                DepoId = 10,
                TasinirKartId = 1,
                HareketTarihi = new DateTime(2026, 8, 24, 8, 0, 0),
                HareketTipi = StokHareketTipleri.Giris,
                Miktar = 5,
                BirimFiyat = 10,
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
                TasinirKartId = 2,
                StokLotId = 1,
                HareketTarihi = new DateTime(2026, 8, 24, 8, 0, 0),
                HareketTipi = StokHareketTipleri.Giris,
                Miktar = 10,
                BirimFiyat = 5,
                Tutar = 50,
                Durum = StokHareketDurumlari.Aktif,
                KdvUygulamaTipi = 1,
                KdvOrani = 10,
                KdvTutari = 5
            },
            new StokHareket
            {
                Id = 3,
                DepoId = 10,
                TasinirKartId = 3,
                StokSeriId = 1,
                HareketTarihi = new DateTime(2026, 8, 24, 8, 0, 0),
                HareketTipi = StokHareketTipleri.Giris,
                Miktar = 1,
                BirimFiyat = 60,
                Tutar = 60,
                Durum = StokHareketDurumlari.Aktif,
                KdvUygulamaTipi = 1,
                KdvOrani = 20,
                KdvTutari = 12
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

    private static KantinSatisService CreateService(StysAppDbContext dbContext, DomainAccessScope? scope = null)
    {
        var mapperConfig = new MapperConfiguration(cfg =>
        {
            cfg.AddMaps(typeof(KantinProfile).Assembly);
            cfg.AddMaps(typeof(KantinSatisProfile).Assembly);
        }, NullLoggerFactory.Instance);
        var mapper = mapperConfig.CreateMapper();

        return new KantinSatisService(
            dbContext,
            new KantinSatisRepository(dbContext, mapper),
            new FakeUserAccessScopeService(scope ?? DomainAccessScope.Unscoped()),
            new FakeStokHareketService(dbContext),
            mapper);
    }

    private sealed class FakeStokHareketService(StysAppDbContext dbContext) : IStokHareketService
    {
        private int _nextId = 1000;

        public async Task<StokHareketDto> AddWithinCurrentTransactionAsync(StokHareketDto dto, CancellationToken cancellationToken = default)
        {
            var mevcut = await dbContext.StokHareketleri
                .AsNoTracking()
                .Where(x => !x.IsDeleted
                    && x.Durum == StokHareketDurumlari.Aktif
                    && x.DepoId == dto.DepoId
                    && x.TasinirKartId == dto.TasinirKartId
                    && x.StokLotId == dto.StokLotId
                    && x.StokSeriId == dto.StokSeriId)
                .Select(x => new { x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu, x.Miktar })
                .ToListAsync(cancellationToken);

            var bakiye = mevcut.Sum(x => StokHareketTipleri.IsGirisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu) ? x.Miktar
                : StokHareketTipleri.IsCikisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu) ? -x.Miktar
                : 0m);

            if (StokHareketTipleri.IsCikisEtkisi(dto.HareketTipi, dto.TransferYonu, dto.SayimFarkiYonu) && bakiye < dto.Miktar)
            {
                throw new BaseException("Yetersiz stok.", 400);
            }

            var entity = new StokHareket
            {
                Id = _nextId++,
                DepoId = dto.DepoId,
                TasinirKartId = dto.TasinirKartId,
                HareketTarihi = dto.HareketTarihi,
                HareketTipi = dto.HareketTipi,
                Miktar = dto.Miktar,
                BirimFiyat = dto.BirimFiyat,
                Tutar = dto.Tutar,
                BelgeTarihi = dto.BelgeTarihi,
                Aciklama = dto.Aciklama,
                KaynakModul = dto.KaynakModul,
                KaynakId = dto.KaynakId,
                Durum = dto.Durum,
                KdvUygulamaTipi = dto.KdvUygulamaTipi,
                KdvOrani = dto.KdvOrani,
                KdvTutari = dto.KdvTutari,
                StokLotId = dto.StokLotId,
                StokSeriId = dto.StokSeriId,
                MaliyetBirimFiyat = dto.MaliyetBirimFiyat,
                MaliyetTutari = dto.MaliyetTutari
            };

            dbContext.StokHareketleri.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken);
            dto.Id = entity.Id;
            return dto;
        }

        public Task<IEnumerable<StokHareketDto>> GetAllAsync(Func<IQueryable<StokHareket>, IQueryable<StokHareket>>? include = null) => throw new NotImplementedException();
        public Task<StokHareketDto?> GetByIdAsync(int id, Func<IQueryable<StokHareket>, IQueryable<StokHareket>>? include = null) => throw new NotImplementedException();
        public Task<PagedResult<StokHareketDto>> GetPagedAsync(PagedRequest request, System.Linq.Expressions.Expression<Func<StokHareket, bool>>? predicate = null, Func<IQueryable<StokHareket>, IQueryable<StokHareket>>? include = null, Func<IQueryable<StokHareket>, IOrderedQueryable<StokHareket>>? orderBy = null) => throw new NotImplementedException();
        public Task<StokHareketDto> AddAsync(StokHareketDto dto) => throw new NotImplementedException();
        public Task<StokHareketDto> UpdateAsync(StokHareketDto dto) => throw new NotImplementedException();
        public Task DeleteAsync(int id) => throw new NotImplementedException();
        public Task<IEnumerable<StokHareketDto>> WhereAsync(System.Linq.Expressions.Expression<Func<StokHareket, bool>> predicate, Func<IQueryable<StokHareket>, IQueryable<StokHareket>>? include = null) => throw new NotImplementedException();
        public Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<StokHareket, bool>> predicate, Func<IQueryable<StokHareket>, IQueryable<StokHareket>>? include = null) => throw new NotImplementedException();
        public Task<List<StokBakiyeDto>> GetStokBakiyeAsync(int? tesisId, int? depoId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<StokKartOzetDto>> GetStokKartOzetAsync(int? tesisId, int? depoId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<StokDegerlemeDto>> GetStokDegerlemeAsync(int? tesisId, int? depoId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<StokDetayDto> GetStokDetayAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<StokLotBakiyeDto>> GetLotBakiyeleriAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<StokSeriBakiyeDto>> GetSeriBakiyeleriAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<StokHareketDto>> CreateTransferAsync(StokTransferRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<StokHareketDto>> CreateTransferWithinCurrentTransactionAsync(StokTransferRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task TransferIptalAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public string? GetCurrentUserName() => "kantin-satis-test";
        public Guid? GetCurrentUserId() => Guid.Parse("22222222-2222-2222-2222-222222222222");
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
        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default) => Task.FromResult(scope);
    }
}
