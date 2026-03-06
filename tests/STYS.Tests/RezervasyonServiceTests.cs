using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Binalar.Entities;
using STYS.Fiyatlandirma.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.KonaklamaTipleri.Entities;
using STYS.MisafirTipleri.Entities;
using STYS.Odalar.Entities;
using STYS.OdaTipleri.Entities;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Dto;
using STYS.Rezervasyonlar.Entities;
using STYS.Rezervasyonlar.Services;
using STYS.Tesisler.Entities;

namespace STYS.Tests;

public class RezervasyonServiceTests
{
    [Fact]
    public async Task HesaplaSenaryoFiyati_TesisSaatineGoreGunSayisiniHesaplar()
    {
        await using var dbContext = CreateDbContext();
        await SeedSingleRoomFixtureAsync(
            dbContext,
            girisSaati: new TimeSpan(16, 0, 0),
            cikisSaati: new TimeSpan(11, 0, 0),
            odaFiyati: 100m);

        var service = CreateService(dbContext);
        var baslangic = new DateTime(2026, 3, 5, 12, 0, 0);
        var bitis = new DateTime(2026, 3, 7, 10, 0, 0);

        var result = await service.HesaplaSenaryoFiyatiAsync(new SenaryoFiyatHesaplaRequestDto
        {
            TesisId = 1,
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
            BaslangicTarihi = baslangic,
            BitisTarihi = bitis,
            Segmentler =
            [
                new SenaryoFiyatHesaplaSegmentDto
                {
                    BaslangicTarihi = baslangic,
                    BitisTarihi = bitis,
                    OdaAtamalari =
                    [
                        new SenaryoFiyatHesaplaOdaAtamaDto { OdaId = 100, AyrilanKisiSayisi = 1 }
                    ]
                }
            ]
        });

        Assert.Equal(200m, result.ToplamBazUcret);
        Assert.Equal(200m, result.ToplamNihaiUcret);
        Assert.Equal("TRY", result.ParaBirimi);
    }

    [Fact]
    public async Task SenaryoUretimi_AyniOdaDagilimiVarsaIkinciSegmentSenaryosunuEler()
    {
        await using var dbContext = CreateDbContext();
        await SeedSingleRoomFixtureAsync(dbContext, new TimeSpan(14, 0, 0), new TimeSpan(10, 0, 0), 250m);
        var service = CreateService(dbContext);

        var baslangic = new DateTime(2026, 3, 6, 14, 0, 0);
        var bitis = new DateTime(2026, 3, 10, 10, 0, 0);

        var scenarios = await service.GetKonaklamaSenaryolariAsync(new KonaklamaSenaryoAramaRequestDto
        {
            TesisId = 1,
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
            KisiSayisi = 1,
            BaslangicTarihi = baslangic,
            BitisTarihi = bitis
        });

        var scenario = Assert.Single(scenarios);
        Assert.Equal(0, scenario.OdaDegisimSayisi);
        Assert.Single(scenario.Segmentler);
    }

    [Fact]
    public async Task SenaryoUretimi_FiyataGoreArtanSiradaDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedTwoRoomPriceFixtureAsync(dbContext);
        var service = CreateService(dbContext);

        var scenarios = await service.GetKonaklamaSenaryolariAsync(new KonaklamaSenaryoAramaRequestDto
        {
            TesisId = 1,
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
            KisiSayisi = 1,
            BaslangicTarihi = new DateTime(2026, 3, 6, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 3, 7, 10, 0, 0)
        });

        Assert.True(scenarios.Count >= 2);
        Assert.True(scenarios[0].ToplamNihaiUcret <= scenarios[1].ToplamNihaiUcret);
        Assert.Equal("ODA-B", scenarios[0].Segmentler[0].OdaAtamalari[0].OdaNo);
    }

    [Fact]
    public async Task UygunOdaArama_DoluPaylasimsizOdayiGetirmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedSingleRoomFixtureAsync(dbContext, new TimeSpan(14, 0, 0), new TimeSpan(10, 0, 0), 180m);
        await SeedExistingReservationAsync(
            dbContext,
            odaId: 100,
            baslangic: new DateTime(2026, 3, 7, 14, 0, 0),
            bitis: new DateTime(2026, 3, 8, 10, 0, 0),
            kisiSayisi: 1);

        var service = CreateService(dbContext);
        var rooms = await service.GetUygunOdalarAsync(new UygunOdaAramaRequestDto
        {
            TesisId = 1,
            KisiSayisi = 1,
            BaslangicTarihi = new DateTime(2026, 3, 7, 15, 0, 0),
            BitisTarihi = new DateTime(2026, 3, 8, 9, 0, 0)
        });

        Assert.Empty(rooms);
    }

    [Fact]
    public async Task SenaryoUretimi_IkiOdaCakismaliRezervasyondaOdaDegisimliSenaryoUretebilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTwoStandardRoomsWithSinglePriceAsync(dbContext, odaFiyati: 1000m);

        await SeedExistingReservationAsync(
            dbContext,
            odaId: 100,
            baslangic: new DateTime(2026, 3, 7, 14, 0, 0),
            bitis: new DateTime(2026, 3, 10, 10, 0, 0),
            kisiSayisi: 1,
            rezervasyonId: 910,
            odaNoSnapshot: "ODA-1");

        await SeedExistingReservationAsync(
            dbContext,
            odaId: 101,
            baslangic: new DateTime(2026, 3, 11, 14, 0, 0),
            bitis: new DateTime(2026, 3, 12, 10, 0, 0),
            kisiSayisi: 1,
            rezervasyonId: 920,
            odaNoSnapshot: "ODA-2");

        var service = CreateService(dbContext);
        var scenarios = await service.GetKonaklamaSenaryolariAsync(new KonaklamaSenaryoAramaRequestDto
        {
            TesisId = 1,
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
            KisiSayisi = 1,
            BaslangicTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 3, 12, 10, 0, 0)
        });

        var roomSwitchScenario = Assert.Single(scenarios, x => x.Segmentler.Count == 2);
        Assert.Equal(1, roomSwitchScenario.OdaDegisimSayisi);
        Assert.Equal("ODA-2", roomSwitchScenario.Segmentler[0].OdaAtamalari[0].OdaNo);
        Assert.Equal("ODA-1", roomSwitchScenario.Segmentler[1].OdaAtamalari[0].OdaNo);
        Assert.Equal(4000m, roomSwitchScenario.ToplamBazUcret);
        Assert.Equal(4000m, roomSwitchScenario.ToplamNihaiUcret);
    }

    [Fact]
    public async Task SenaryoUretimi_TekOdaIcin_8Mart12MartArasiDortBinTlHesaplar()
    {
        await using var dbContext = CreateDbContext();
        await SeedSingleRoomFixtureAsync(dbContext, new TimeSpan(14, 0, 0), new TimeSpan(10, 0, 0), 1000m);

        var service = CreateService(dbContext);
        var scenarios = await service.GetKonaklamaSenaryolariAsync(new KonaklamaSenaryoAramaRequestDto
        {
            TesisId = 1,
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
            KisiSayisi = 1,
            BaslangicTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 3, 12, 10, 0, 0)
        });

        var scenario = Assert.Single(scenarios);
        Assert.Equal(0, scenario.OdaDegisimSayisi);
        Assert.Single(scenario.Segmentler);
        Assert.Single(scenario.Segmentler[0].OdaAtamalari);
        Assert.Equal(100, scenario.Segmentler[0].OdaAtamalari[0].OdaId);
        Assert.Equal(4000m, scenario.ToplamBazUcret);
        Assert.Equal(4000m, scenario.ToplamNihaiUcret);
    }

    [Fact]
    public async Task SenaryoUretimi_FarkliOdaTipiVeFiyatlaOdaDegisimliSenaryoUretebilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTwoRoomsWithDifferentTypesAndPricesAsync(dbContext);

        await SeedExistingReservationAsync(
            dbContext,
            odaId: 100,
            baslangic: new DateTime(2026, 3, 7, 14, 0, 0),
            bitis: new DateTime(2026, 3, 10, 10, 0, 0),
            kisiSayisi: 1,
            rezervasyonId: 930,
            odaNoSnapshot: "ODA-1");

        await SeedExistingReservationAsync(
            dbContext,
            odaId: 101,
            baslangic: new DateTime(2026, 3, 11, 14, 0, 0),
            bitis: new DateTime(2026, 3, 12, 10, 0, 0),
            kisiSayisi: 1,
            rezervasyonId: 940,
            odaNoSnapshot: "ODA-2");

        var service = CreateService(dbContext);
        var scenarios = await service.GetKonaklamaSenaryolariAsync(new KonaklamaSenaryoAramaRequestDto
        {
            TesisId = 1,
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
            KisiSayisi = 1,
            BaslangicTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 3, 12, 10, 0, 0)
        });

        var roomSwitchScenario = Assert.Single(scenarios, x => x.Segmentler.Count == 2);
        Assert.Equal(1, roomSwitchScenario.OdaDegisimSayisi);
        Assert.Equal("ODA-2", roomSwitchScenario.Segmentler[0].OdaAtamalari[0].OdaNo);
        Assert.Equal("ODA-1", roomSwitchScenario.Segmentler[1].OdaAtamalari[0].OdaNo);
        Assert.Equal(5500m, roomSwitchScenario.ToplamBazUcret);
        Assert.Equal(5500m, roomSwitchScenario.ToplamNihaiUcret);
    }

    private static RezervasyonService CreateService(StysAppDbContext dbContext, DomainAccessScope? scope = null)
    {
        return new RezervasyonService(dbContext, new FakeUserAccessScopeService(scope ?? DomainAccessScope.Unscoped()));
    }

    private static StysAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase($"stys-tests-{Guid.NewGuid():N}")
            .Options;

        return new StysAppDbContext(options);
    }

    private static async Task SeedSingleRoomFixtureAsync(
        StysAppDbContext dbContext,
        TimeSpan girisSaati,
        TimeSpan cikisSaati,
        decimal odaFiyati)
    {
        await SeedLookupsAsync(dbContext);

        dbContext.Tesisler.Add(new Tesis
        {
            Id = 1,
            Ad = "Test Tesis",
            IlId = 1,
            Telefon = "000",
            Adres = "Adres",
            GirisSaati = girisSaati,
            CikisSaati = cikisSaati,
            AktifMi = true
        });

        dbContext.Binalar.Add(new Bina
        {
            Id = 10,
            TesisId = 1,
            Ad = "Bina-1",
            KatSayisi = 3,
            AktifMi = true
        });

        dbContext.OdaTipleri.Add(new OdaTipi
        {
            Id = 20,
            TesisId = 1,
            OdaSinifiId = 1,
            Ad = "Standart",
            Kapasite = 2,
            PaylasimliMi = false,
            AktifMi = true
        });

        dbContext.Odalar.Add(new Oda
        {
            Id = 100,
            OdaNo = "ODA-A",
            BinaId = 10,
            TesisOdaTipiId = 20,
            KatNo = 1,
            AktifMi = true
        });

        dbContext.OdaFiyatlari.Add(new OdaFiyat
        {
            Id = 1000,
            TesisOdaTipiId = 20,
            KonaklamaTipiId = 1,
            MisafirTipiId = 1,
            KisiSayisi = 1,
            Fiyat = odaFiyati,
            ParaBirimi = "TRY",
            BaslangicTarihi = new DateTime(2026, 3, 1),
            BitisTarihi = new DateTime(2026, 3, 31),
            AktifMi = true
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedTwoRoomPriceFixtureAsync(StysAppDbContext dbContext)
    {
        await SeedLookupsAsync(dbContext);

        dbContext.Tesisler.Add(new Tesis
        {
            Id = 1,
            Ad = "Test Tesis",
            IlId = 1,
            Telefon = "000",
            Adres = "Adres",
            GirisSaati = new TimeSpan(14, 0, 0),
            CikisSaati = new TimeSpan(10, 0, 0),
            AktifMi = true
        });

        dbContext.Binalar.Add(new Bina
        {
            Id = 10,
            TesisId = 1,
            Ad = "Bina-1",
            KatSayisi = 4,
            AktifMi = true
        });

        dbContext.OdaTipleri.AddRange(
            new OdaTipi
            {
                Id = 20,
                TesisId = 1,
                OdaSinifiId = 1,
                Ad = "Pahali Tip",
                Kapasite = 4,
                PaylasimliMi = false,
                AktifMi = true
            },
            new OdaTipi
            {
                Id = 21,
                TesisId = 1,
                OdaSinifiId = 1,
                Ad = "Uygun Tip",
                Kapasite = 1,
                PaylasimliMi = false,
                AktifMi = true
            });

        dbContext.Odalar.AddRange(
            new Oda
            {
                Id = 100,
                OdaNo = "ODA-A",
                BinaId = 10,
                TesisOdaTipiId = 20,
                KatNo = 1,
                AktifMi = true
            },
            new Oda
            {
                Id = 101,
                OdaNo = "ODA-B",
                BinaId = 10,
                TesisOdaTipiId = 21,
                KatNo = 1,
                AktifMi = true
            });

        dbContext.OdaFiyatlari.AddRange(
            new OdaFiyat
            {
                Id = 1000,
                TesisOdaTipiId = 20,
                KonaklamaTipiId = 1,
                MisafirTipiId = 1,
                KisiSayisi = 1,
                Fiyat = 300m,
                ParaBirimi = "TRY",
                BaslangicTarihi = new DateTime(2026, 3, 1),
                BitisTarihi = new DateTime(2026, 3, 31),
                AktifMi = true
            },
            new OdaFiyat
            {
                Id = 1001,
                TesisOdaTipiId = 21,
                KonaklamaTipiId = 1,
                MisafirTipiId = 1,
                KisiSayisi = 1,
                Fiyat = 100m,
                ParaBirimi = "TRY",
                BaslangicTarihi = new DateTime(2026, 3, 1),
                BitisTarihi = new DateTime(2026, 3, 31),
                AktifMi = true
            });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedTwoStandardRoomsWithSinglePriceAsync(StysAppDbContext dbContext, decimal odaFiyati)
    {
        await SeedLookupsAsync(dbContext);

        dbContext.Tesisler.Add(new Tesis
        {
            Id = 1,
            Ad = "Test Tesis",
            IlId = 1,
            Telefon = "000",
            Adres = "Adres",
            GirisSaati = new TimeSpan(14, 0, 0),
            CikisSaati = new TimeSpan(10, 0, 0),
            AktifMi = true
        });

        dbContext.Binalar.Add(new Bina
        {
            Id = 10,
            TesisId = 1,
            Ad = "Bina-1",
            KatSayisi = 4,
            AktifMi = true
        });

        dbContext.OdaTipleri.Add(new OdaTipi
        {
            Id = 20,
            TesisId = 1,
            OdaSinifiId = 1,
            Ad = "Standart",
            Kapasite = 2,
            PaylasimliMi = false,
            AktifMi = true
        });

        dbContext.Odalar.AddRange(
            new Oda
            {
                Id = 100,
                OdaNo = "ODA-1",
                BinaId = 10,
                TesisOdaTipiId = 20,
                KatNo = 1,
                AktifMi = true
            },
            new Oda
            {
                Id = 101,
                OdaNo = "ODA-2",
                BinaId = 10,
                TesisOdaTipiId = 20,
                KatNo = 1,
                AktifMi = true
            });

        dbContext.OdaFiyatlari.Add(new OdaFiyat
        {
            Id = 1000,
            TesisOdaTipiId = 20,
            KonaklamaTipiId = 1,
            MisafirTipiId = 1,
            KisiSayisi = 1,
            Fiyat = odaFiyati,
            ParaBirimi = "TRY",
            BaslangicTarihi = new DateTime(2026, 3, 1),
            BitisTarihi = new DateTime(2026, 3, 31),
            AktifMi = true
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedTwoRoomsWithDifferentTypesAndPricesAsync(StysAppDbContext dbContext)
    {
        await SeedLookupsAsync(dbContext);

        dbContext.Tesisler.Add(new Tesis
        {
            Id = 1,
            Ad = "Test Tesis",
            IlId = 1,
            Telefon = "000",
            Adres = "Adres",
            GirisSaati = new TimeSpan(14, 0, 0),
            CikisSaati = new TimeSpan(10, 0, 0),
            AktifMi = true
        });

        dbContext.Binalar.Add(new Bina
        {
            Id = 10,
            TesisId = 1,
            Ad = "Bina-1",
            KatSayisi = 4,
            AktifMi = true
        });

        dbContext.OdaTipleri.AddRange(
            new OdaTipi
            {
                Id = 20,
                TesisId = 1,
                OdaSinifiId = 1,
                Ad = "Standart",
                Kapasite = 2,
                PaylasimliMi = false,
                AktifMi = true
            },
            new OdaTipi
            {
                Id = 21,
                TesisId = 1,
                OdaSinifiId = 1,
                Ad = "Deluxe",
                Kapasite = 2,
                PaylasimliMi = false,
                AktifMi = true
            });

        dbContext.Odalar.AddRange(
            new Oda
            {
                Id = 100,
                OdaNo = "ODA-1",
                BinaId = 10,
                TesisOdaTipiId = 20,
                KatNo = 1,
                AktifMi = true
            },
            new Oda
            {
                Id = 101,
                OdaNo = "ODA-2",
                BinaId = 10,
                TesisOdaTipiId = 21,
                KatNo = 1,
                AktifMi = true
            });

        dbContext.OdaFiyatlari.AddRange(
            new OdaFiyat
            {
                Id = 1000,
                TesisOdaTipiId = 20,
                KonaklamaTipiId = 1,
                MisafirTipiId = 1,
                KisiSayisi = 1,
                Fiyat = 1000m,
                ParaBirimi = "TRY",
                BaslangicTarihi = new DateTime(2026, 3, 1),
                BitisTarihi = new DateTime(2026, 3, 31),
                AktifMi = true
            },
            new OdaFiyat
            {
                Id = 1001,
                TesisOdaTipiId = 21,
                KonaklamaTipiId = 1,
                MisafirTipiId = 1,
                KisiSayisi = 1,
                Fiyat = 1500m,
                ParaBirimi = "TRY",
                BaslangicTarihi = new DateTime(2026, 3, 1),
                BitisTarihi = new DateTime(2026, 3, 31),
                AktifMi = true
            });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedExistingReservationAsync(
        StysAppDbContext dbContext,
        int odaId,
        DateTime baslangic,
        DateTime bitis,
        int kisiSayisi,
        int rezervasyonId = 900,
        string odaNoSnapshot = "ODA-A")
    {
        dbContext.Rezervasyonlar.Add(new Rezervasyon
        {
            Id = rezervasyonId,
            ReferansNo = $"TEST-RZV-{rezervasyonId}",
            TesisId = 1,
            KisiSayisi = kisiSayisi,
            GirisTarihi = baslangic,
            CikisTarihi = bitis,
            MisafirAdiSoyadi = "Test Misafir",
            MisafirTelefon = "000",
            ToplamBazUcret = 100m,
            ToplamUcret = 100m,
            ParaBirimi = "TRY",
            RezervasyonDurumu = RezervasyonDurumlari.Onayli,
            AktifMi = true
        });

        dbContext.RezervasyonSegmentleri.Add(new RezervasyonSegment
        {
            Id = rezervasyonId + 1,
            RezervasyonId = rezervasyonId,
            SegmentSirasi = 1,
            BaslangicTarihi = baslangic,
            BitisTarihi = bitis
        });

        dbContext.RezervasyonSegmentOdaAtamalari.Add(new RezervasyonSegmentOdaAtama
        {
            Id = rezervasyonId + 2,
            RezervasyonSegmentId = rezervasyonId + 1,
            OdaId = odaId,
            AyrilanKisiSayisi = kisiSayisi,
            OdaNoSnapshot = odaNoSnapshot,
            BinaAdiSnapshot = "Bina-1",
            OdaTipiAdiSnapshot = "Standart",
            PaylasimliMiSnapshot = false,
            KapasiteSnapshot = 2
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedLookupsAsync(StysAppDbContext dbContext)
    {
        if (await dbContext.MisafirTipleri.AnyAsync() || await dbContext.KonaklamaTipleri.AnyAsync())
        {
            return;
        }

        dbContext.MisafirTipleri.Add(new MisafirTipi
        {
            Id = 1,
            Kod = "TEST-MISAFIR",
            Ad = "Test Misafir Tipi",
            AktifMi = true
        });

        dbContext.KonaklamaTipleri.Add(new KonaklamaTipi
        {
            Id = 1,
            Kod = "TEST-KONAKLAMA",
            Ad = "Test Konaklama Tipi",
            AktifMi = true
        });

        await dbContext.SaveChangesAsync();
    }

    private sealed class FakeUserAccessScopeService : IUserAccessScopeService
    {
        private readonly DomainAccessScope _scope;

        public FakeUserAccessScopeService(DomainAccessScope scope)
        {
            _scope = scope;
        }

        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_scope);
        }
    }
}
