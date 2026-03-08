using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using STYS.Fiyatlandirma;
using STYS.Fiyatlandirma.Dto;
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
using STYS.SezonKurallari.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class RezervasyonServiceTests
{
    // Tesisin giris/cikis saatine gore gece sayisi hesaplanip baz/nihai tutar dogru uretilmeli.
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

    // Farkli tesis giris/cikis saat kombinasyonlarinda gece/adet bazli fiyat hesaplamasi dogru kalmali.
    [Theory]
    [MemberData(nameof(FarkliTesisSaatleriFiyatSenaryolari))]
    public async Task HesaplaSenaryoFiyati_FarkliGirisCikisSaatlerindeDogruHesaplar(
        TimeSpan girisSaati,
        TimeSpan cikisSaati,
        DateTime baslangic,
        DateTime bitis,
        int beklenenGeceSayisi)
    {
        await using var dbContext = CreateDbContext();
        await SeedSingleRoomFixtureAsync(
            dbContext,
            girisSaati: girisSaati,
            cikisSaati: cikisSaati,
            odaFiyati: 100m);

        var service = CreateService(dbContext);
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

        var beklenenToplam = beklenenGeceSayisi * 100m;
        Assert.Equal(beklenenToplam, result.ToplamBazUcret);
        Assert.Equal(beklenenToplam, result.ToplamNihaiUcret);
        Assert.Equal("TRY", result.ParaBirimi);
    }

    public static IEnumerable<object[]> FarkliTesisSaatleriFiyatSenaryolari()
    {
        // 14:00 giris - 10:00 cikis: tam 1 gece
        yield return
        [
            new TimeSpan(14, 0, 0),
            new TimeSpan(10, 0, 0),
            new DateTime(2026, 3, 7, 14, 0, 0),
            new DateTime(2026, 3, 8, 10, 0, 0),
            1
        ];

        // 14:00 giris - 10:00 cikis: gec baslangic + ertesi gun gec cikis => 2 gece
        yield return
        [
            new TimeSpan(14, 0, 0),
            new TimeSpan(10, 0, 0),
            new DateTime(2026, 3, 7, 22, 30, 0),
            new DateTime(2026, 3, 8, 22, 30, 0),
            2
        ];

        // 16:00 giris - 11:00 cikis: onceki regression benzeri 2 gece
        yield return
        [
            new TimeSpan(16, 0, 0),
            new TimeSpan(11, 0, 0),
            new DateTime(2026, 3, 5, 12, 0, 0),
            new DateTime(2026, 3, 7, 10, 0, 0),
            2
        ];

        // 12:00 giris - 12:00 cikis: tam 1 gece
        yield return
        [
            new TimeSpan(12, 0, 0),
            new TimeSpan(12, 0, 0),
            new DateTime(2026, 3, 7, 12, 0, 0),
            new DateTime(2026, 3, 8, 12, 0, 0),
            1
        ];

        // 18:00 giris - 09:00 cikis: check-in oncesi saatten baslasa da 1 gece
        yield return
        [
            new TimeSpan(18, 0, 0),
            new TimeSpan(9, 0, 0),
            new DateTime(2026, 3, 7, 8, 0, 0),
            new DateTime(2026, 3, 8, 8, 0, 0),
            1
        ];
    }

    // Rezervasyon girisi tesis giris saatinden sonra olsa da senaryo uretimi hata vermeden calismali.
    [Fact]
    public async Task SenaryoUretimi_GirisSaatindenSonraBaslayincaSenaryoUretebilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedSingleRoomFixtureAsync(
            dbContext,
            girisSaati: new TimeSpan(14, 0, 0),
            cikisSaati: new TimeSpan(10, 0, 0),
            odaFiyati: 1000m);

        var service = CreateService(dbContext);
        var scenarios = await service.GetKonaklamaSenaryolariAsync(new KonaklamaSenaryoAramaRequestDto
        {
            TesisId = 1,
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
            KisiSayisi = 1,
            BaslangicTarihi = new DateTime(2026, 3, 7, 22, 37, 0),
            BitisTarihi = new DateTime(2026, 3, 8, 22, 37, 0)
        });

        var firstScenario = Assert.Single(scenarios);
        Assert.Single(firstScenario.Segmentler);
        Assert.Equal(2000m, firstScenario.ToplamBazUcret);
        Assert.Equal(2000m, firstScenario.ToplamNihaiUcret);
    }

    // Iki segmentte oda dagilimi degismiyorsa anlamsiz segmentli senaryo uretilmemeli.
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

    // Uretilen senaryolar toplam ucrete gore artan sirada donmeli.
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

    // Stop-sale aktif sezon kurali varsa ilgili tarih araliginda konaklama senaryosu uretimi engellenmeli.
    [Fact]
    public async Task SenaryoUretimi_StopSaleAktifseHataVerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedSingleRoomFixtureAsync(dbContext, new TimeSpan(14, 0, 0), new TimeSpan(10, 0, 0), 250m);
        await SeedSezonKuraliAsync(
            dbContext,
            id: 7001,
            tesisId: 1,
            kod: "STOP-MART",
            ad: "Mart Stop Sale",
            baslangic: new DateTime(2026, 3, 1),
            bitis: new DateTime(2026, 3, 31),
            minimumGece: 1,
            stopSaleMi: true);

        var service = CreateService(dbContext);
        var exception = await Assert.ThrowsAsync<BaseException>(() => service.GetKonaklamaSenaryolariAsync(new KonaklamaSenaryoAramaRequestDto
        {
            TesisId = 1,
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
            KisiSayisi = 1,
            BaslangicTarihi = new DateTime(2026, 3, 7, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 3, 8, 10, 0, 0)
        }));

        Assert.Equal(400, exception.ErrorCode);
        Assert.Contains("stop-sale", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Sezon kuralindaki minimum gece kosulu saglanmiyorsa senaryo uretimi hata vermeli.
    [Fact]
    public async Task SenaryoUretimi_MinimumGeceSaglanmazsaHataVerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedSingleRoomFixtureAsync(dbContext, new TimeSpan(14, 0, 0), new TimeSpan(10, 0, 0), 250m);
        await SeedSezonKuraliAsync(
            dbContext,
            id: 7002,
            tesisId: 1,
            kod: "MIN-3",
            ad: "Minimum 3 Gece",
            baslangic: new DateTime(2026, 3, 1),
            bitis: new DateTime(2026, 3, 31),
            minimumGece: 3,
            stopSaleMi: false);

        var service = CreateService(dbContext);
        var exception = await Assert.ThrowsAsync<BaseException>(() => service.GetKonaklamaSenaryolariAsync(new KonaklamaSenaryoAramaRequestDto
        {
            TesisId = 1,
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
            KisiSayisi = 1,
            BaslangicTarihi = new DateTime(2026, 3, 7, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 3, 8, 10, 0, 0)
        }));

        Assert.Equal(400, exception.ErrorCode);
        Assert.Contains("minimum 3 gece", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Paylasimsiz bir oda cakisan rezervasyonla doluysa uygun oda listesine girmemeli.
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

    // Cakisan iki rezervasyon arasinda kalan aralikta oda degisimli senaryo uretilmeli.
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

    // Tek oda senaryosunda 8-12 Mart konaklamasi 4 gece olarak ucretlenmeli.
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

    // Farkli oda tip/fiyat kombinasyonunda segment bazli oda degisimi ve fiyat dogru hesaplanmali.
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

    // Paylasimli + standart karisik yapida tum donen senaryolarin adet/siralama/fiyat dogrulugu kontrol edilir.
    [Fact]
    public async Task SenaryoUretimi_StandartVePaylasimliOdadaTumSonuclariDogruDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedStandardAndSharedRoomsWithDifferentPricesAsync(dbContext);

        await SeedExistingReservationAsync(
            dbContext,
            odaId: 100,
            baslangic: new DateTime(2026, 3, 7, 14, 0, 0),
            bitis: new DateTime(2026, 3, 10, 10, 0, 0),
            kisiSayisi: 1,
            rezervasyonId: 950,
            odaNoSnapshot: "ODA-1");

        await SeedExistingReservationAsync(
            dbContext,
            odaId: 101,
            baslangic: new DateTime(2026, 3, 11, 14, 0, 0),
            bitis: new DateTime(2026, 3, 12, 10, 0, 0),
            kisiSayisi: 1,
            rezervasyonId: 960,
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

        Assert.Equal(2, scenarios.Count);
        Assert.True(scenarios[0].ToplamNihaiUcret <= scenarios[1].ToplamNihaiUcret);

        var fullStayScenario = scenarios[0];
        Assert.Equal(0, fullStayScenario.OdaDegisimSayisi);
        Assert.Single(fullStayScenario.Segmentler);
        Assert.Single(fullStayScenario.Segmentler[0].OdaAtamalari);
        Assert.Equal("ODA-2", fullStayScenario.Segmentler[0].OdaAtamalari[0].OdaNo);
        Assert.Equal(2000m, fullStayScenario.ToplamBazUcret);
        Assert.Equal(2000m, fullStayScenario.ToplamNihaiUcret);

        var roomSwitchScenario = scenarios[1];
        Assert.Equal(1, roomSwitchScenario.OdaDegisimSayisi);
        Assert.Equal(2, roomSwitchScenario.Segmentler.Count);
        Assert.Equal("ODA-2", roomSwitchScenario.Segmentler[0].OdaAtamalari[0].OdaNo);
        Assert.Equal("ODA-1", roomSwitchScenario.Segmentler[1].OdaAtamalari[0].OdaNo);
        Assert.Equal(2500m, roomSwitchScenario.ToplamBazUcret);
        Assert.Equal(2500m, roomSwitchScenario.ToplamNihaiUcret);
    }

    // Scope aktifken kullanici sadece yetkili oldugu tesisleri gorebilmeli.
    [Fact]
    public async Task ErisilebilirTesisler_ScopeTesisleriIleSinirlidir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);

        var service = CreateService(dbContext, DomainAccessScope.Scoped([], [2], []));
        var tesisler = await service.GetErisilebilirTesislerAsync();

        var tesis = Assert.Single(tesisler);
        Assert.Equal(2, tesis.Id);
        Assert.Equal("Beta Konukevi", tesis.Ad);
    }

    // Tesis bazli oda tipi listesi, farkli tiplerin tamamini dondurmeli.
    [Fact]
    public async Task OdaTipleriByTesis_OnFarkliTipteOdaTipiDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);

        var service = CreateService(dbContext);
        var odaTipleri = await service.GetOdaTipleriByTesisAsync(1);

        Assert.Equal(10, odaTipleri.Count);
        Assert.Equal(10, odaTipleri.Select(x => x.Id).Distinct().Count());
        Assert.All(odaTipleri, x => Assert.Equal(1, x.TesisId));
    }

    // Rezervasyon yokken 10 odali fixture'daki tum aktif odalar uygun listede gorunmeli.
    [Fact]
    public async Task UygunOdaArama_OnOdaliTesisteTumAktifOdalarDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);

        var service = CreateService(dbContext);
        var rooms = await service.GetUygunOdalarAsync(new UygunOdaAramaRequestDto
        {
            TesisId = 1,
            KisiSayisi = 1,
            BaslangicTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 3, 9, 10, 0, 0)
        });

        Assert.Equal(10, rooms.Count);
        Assert.Contains(rooms, x => x.OdaId == 100);
        Assert.Contains(rooms, x => x.OdaId == 109);
        Assert.DoesNotContain(rooms, x => x.OdaId == 200);
    }

    // Dolu paylasimsiz oda, uygun oda aramasinda disarida kalmali.
    [Fact]
    public async Task UygunOdaArama_PaylasimsizDoluOdayiGetirmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);

        await SeedExistingReservationAsync(
            dbContext,
            odaId: 100,
            baslangic: new DateTime(2026, 3, 8, 14, 0, 0),
            bitis: new DateTime(2026, 3, 9, 10, 0, 0),
            kisiSayisi: 1,
            rezervasyonId: 971,
            odaNoSnapshot: "A-101");

        var service = CreateService(dbContext);
        var rooms = await service.GetUygunOdalarAsync(new UygunOdaAramaRequestDto
        {
            TesisId = 1,
            KisiSayisi = 1,
            BaslangicTarihi = new DateTime(2026, 3, 8, 15, 0, 0),
            BitisTarihi = new DateTime(2026, 3, 9, 9, 0, 0)
        });

        Assert.Equal(9, rooms.Count);
        Assert.DoesNotContain(rooms, x => x.OdaId == 100);
    }

    // Paylasimli odada kalan kapasiteye gore 1 kisilik uygunluk varken 2 kisilik uygunluk olmayabilir.
    [Fact]
    public async Task UygunOdaArama_PaylasimliOdadaKalanKapasiteyiDikkateAlir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);

        await SeedExistingReservationAsync(
            dbContext,
            odaId: 105,
            baslangic: new DateTime(2026, 3, 8, 14, 0, 0),
            bitis: new DateTime(2026, 3, 9, 10, 0, 0),
            kisiSayisi: 1,
            rezervasyonId: 972,
            odaNoSnapshot: "B-201");

        var service = CreateService(dbContext);
        var onePersonRooms = await service.GetUygunOdalarAsync(new UygunOdaAramaRequestDto
        {
            TesisId = 1,
            KisiSayisi = 1,
            BaslangicTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 3, 9, 10, 0, 0)
        });
        var twoPersonRooms = await service.GetUygunOdalarAsync(new UygunOdaAramaRequestDto
        {
            TesisId = 1,
            KisiSayisi = 2,
            BaslangicTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 3, 9, 10, 0, 0)
        });

        Assert.Contains(onePersonRooms, x => x.OdaId == 105);
        Assert.DoesNotContain(twoPersonRooms, x => x.OdaId == 105);
    }

    // Oda tipi filtresi verildiginde senaryo atamalari yalnizca secilen oda tipinden olusmali.
    [Fact]
    public async Task SenaryoUretimi_OdaTipiFiltresiUygulandigindaYalnizcaSecilenTipiKullanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);

        var service = CreateService(dbContext);
        var scenarios = await service.GetKonaklamaSenaryolariAsync(new KonaklamaSenaryoAramaRequestDto
        {
            TesisId = 1,
            OdaTipiId = 202,
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
            KisiSayisi = 1,
            BaslangicTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 3, 10, 10, 0, 0)
        });

        Assert.NotEmpty(scenarios);
        Assert.All(
            scenarios.SelectMany(x => x.Segmentler).SelectMany(x => x.OdaAtamalari),
            atama => Assert.Equal(102, atama.OdaId));
    }

    // Senaryo listesi fiyat artan sirada ve en fazla 5 kayitla donmeli.
    [Fact]
    public async Task SenaryoUretimi_SonuclarFiyataGoreArtanSiraliDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);

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

        Assert.True(scenarios.Count >= 2);
        Assert.True(scenarios.Count <= 5);

        for (var i = 1; i < scenarios.Count; i++)
        {
            Assert.True(scenarios[i - 1].ToplamNihaiUcret <= scenarios[i].ToplamNihaiUcret);
        }
    }

    // Secili indirim kurallari oncelik/sira mantigina gore toplama uygulanmali.
    [Fact]
    public async Task SenaryoFiyati_SecilenIndirimKurallariniSiraylaUygular()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        await SeedDiscountRulesForPricingAsync(dbContext);

        var service = CreateService(dbContext);
        var result = await service.HesaplaSenaryoFiyatiAsync(new SenaryoFiyatHesaplaRequestDto
        {
            TesisId = 1,
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
            BaslangicTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 3, 12, 10, 0, 0),
            Segmentler =
            [
                new SenaryoFiyatHesaplaSegmentDto
                {
                    BaslangicTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
                    BitisTarihi = new DateTime(2026, 3, 12, 10, 0, 0),
                    OdaAtamalari =
                    [
                        new SenaryoFiyatHesaplaOdaAtamaDto { OdaId = 101, AyrilanKisiSayisi = 1 }
                    ]
                }
            ],
            SeciliIndirimKuraliIds = [5001, 5002]
        });

        Assert.Equal(3600m, result.ToplamBazUcret);
        Assert.Equal(3150m, result.ToplamNihaiUcret);
        Assert.Equal(2, result.UygulananIndirimler.Count);
        Assert.Equal(5002, result.UygulananIndirimler[0].IndirimKuraliId);
        Assert.Equal(5001, result.UygulananIndirimler[1].IndirimKuraliId);
    }

    // Rezervasyon kaydinda segmentler ve snapshot alanlari dogru persist edilip detayda okunabilmeli.
    [Fact]
    public async Task KaydetAsync_RezervasyonuSegmentleriVeSnapshotlariIleKaydeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        await SeedDiscountRulesForPricingAsync(dbContext);

        var service = CreateService(dbContext);
        var saveResult = await service.KaydetAsync(new RezervasyonKaydetRequestDto
        {
            TesisId = 1,
            KisiSayisi = 1,
            GirisTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            CikisTarihi = new DateTime(2026, 3, 12, 10, 0, 0),
            MisafirAdiSoyadi = "Test Misafir",
            MisafirTelefon = "5551112233",
            MisafirEposta = "test@example.com",
            ToplamBazUcret = 2500m,
            ToplamUcret = 2300m,
            ParaBirimi = "TRY",
            UygulananIndirimler =
            [
                new UygulananIndirimDto
                {
                    IndirimKuraliId = 5001,
                    KuralAdi = "Genel Yuzde 10",
                    IndirimTutari = 200m,
                    SonrasiTutar = 2300m
                }
            ],
            Segmentler =
            [
                new RezervasyonKaydetSegmentDto
                {
                    BaslangicTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
                    BitisTarihi = new DateTime(2026, 3, 10, 12, 0, 0),
                    OdaAtamalari =
                    [
                        new RezervasyonKaydetOdaAtamaDto { OdaId = 105, AyrilanKisiSayisi = 1 }
                    ]
                },
                new RezervasyonKaydetSegmentDto
                {
                    BaslangicTarihi = new DateTime(2026, 3, 10, 12, 0, 0),
                    BitisTarihi = new DateTime(2026, 3, 12, 10, 0, 0),
                    OdaAtamalari =
                    [
                        new RezervasyonKaydetOdaAtamaDto { OdaId = 101, AyrilanKisiSayisi = 1 }
                    ]
                }
            ]
        });

        Assert.True(saveResult.Id > 0);
        Assert.StartsWith("RZV-", saveResult.ReferansNo);

        var detail = await service.GetRezervasyonDetayAsync(saveResult.Id);
        Assert.NotNull(detail);
        Assert.Equal(2, detail!.Segmentler.Count);
        Assert.Equal("B-201", detail.Segmentler[0].OdaAtamalari[0].OdaNo);
        Assert.True(detail.Segmentler[0].OdaAtamalari[0].PaylasimliMi);
        Assert.Equal("A-102", detail.Segmentler[1].OdaAtamalari[0].OdaNo);
        Assert.Single(detail.UygulananIndirimler);
    }

    // Liste endpointi tesis filtresi ve giris tarihine gore azalan siralama kurallarini korumali.
    [Fact]
    public async Task Rezervasyonlar_TesisFiltreVeTariheGoreSiraliDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);

        await SeedExistingReservationAsync(
            dbContext,
            odaId: 100,
            baslangic: new DateTime(2026, 3, 8, 14, 0, 0),
            bitis: new DateTime(2026, 3, 9, 10, 0, 0),
            kisiSayisi: 1,
            rezervasyonId: 973,
            odaNoSnapshot: "A-101",
            tesisId: 1);

        await SeedExistingReservationAsync(
            dbContext,
            odaId: 200,
            baslangic: new DateTime(2026, 3, 12, 14, 0, 0),
            bitis: new DateTime(2026, 3, 13, 10, 0, 0),
            kisiSayisi: 1,
            rezervasyonId: 974,
            odaNoSnapshot: "C-101",
            tesisId: 2);

        var service = CreateService(dbContext);
        var allReservations = await service.GetRezervasyonlarAsync(null);
        var tesisOneReservations = await service.GetRezervasyonlarAsync(1);

        Assert.Equal(2, allReservations.Count);
        Assert.True(allReservations[0].GirisTarihi >= allReservations[1].GirisTarihi);
        var tesisOneReservation = Assert.Single(tesisOneReservations);
        Assert.Equal(1, tesisOneReservation.TesisId);
    }

    // Uygulanabilir indirim kurali listesi tesis/sistem kapsamina ve tarih araligina gore filtrelenmeli.
    [Fact]
    public async Task UygulanabilirIndirimKurallari_TesisVeSistemKurallariniDogruFiltreler()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        await SeedDiscountRulesForQueryAsync(dbContext);

        var service = CreateService(dbContext);
        var rules = await service.GetUygulanabilirIndirimKurallariAsync(
            tesisId: 1,
            misafirTipiId: 1,
            konaklamaTipiId: 1,
            baslangicTarihi: new DateTime(2026, 3, 8, 14, 0, 0),
            bitisTarihi: new DateTime(2026, 3, 10, 10, 0, 0));

        Assert.Contains(rules, x => x.Id == 5101);
        Assert.Contains(rules, x => x.Id == 5102);
        Assert.DoesNotContain(rules, x => x.Id == 5103);
        Assert.DoesNotContain(rules, x => x.Id == 5104);
    }

    // Custom indirim izni olan kullanici, sistemde kayitli rule olmadan manuel indirimle rezervasyon kaydedebilmeli.
    [Fact]
    public async Task KaydetAsync_CustomIndirimYetkisiVarsaKaydedebilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);

        var service = CreateService(
            dbContext,
            permissions: [StructurePermissions.RezervasyonYonetimi.CustomIndirimGirebilir]);

        var request = BuildCustomDiscountSaveRequest();
        var result = await service.KaydetAsync(request);
        var detail = await service.GetRezervasyonDetayAsync(result.Id);

        Assert.NotNull(detail);
        var customDiscount = Assert.Single(detail!.UygulananIndirimler);
        Assert.Equal(0, customDiscount.IndirimKuraliId);
        Assert.Equal(300m, customDiscount.IndirimTutari);
    }

    // Custom indirim izni olmayan kullanici manuel indirimle rezervasyon kaydedememeli (403).
    [Fact]
    public async Task KaydetAsync_CustomIndirimYetkisiYoksaHataVerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);

        var service = CreateService(dbContext);
        var request = BuildCustomDiscountSaveRequest();

        var exception = await Assert.ThrowsAsync<BaseException>(() => service.KaydetAsync(request));
        Assert.Equal(403, exception.ErrorCode);
    }

    // Konaklayan plani kaydedildiginde kisi ve oda atamalari rezervasyon bazinda geri okunabilmeli.
    [Fact]
    public async Task KonaklayanPlani_KaydedilipGeriOkunabilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);

        dbContext.Rezervasyonlar.Add(new Rezervasyon
        {
            Id = 980,
            ReferansNo = "TEST-RZV-980",
            TesisId = 1,
            KisiSayisi = 2,
            GirisTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            CikisTarihi = new DateTime(2026, 3, 9, 10, 0, 0),
            MisafirAdiSoyadi = "Test Lider",
            MisafirTelefon = "000",
            ToplamBazUcret = 1000m,
            ToplamUcret = 1000m,
            ParaBirimi = "TRY",
            RezervasyonDurumu = RezervasyonDurumlari.Onayli,
            AktifMi = true
        });

        dbContext.RezervasyonSegmentleri.Add(new RezervasyonSegment
        {
            Id = 981,
            RezervasyonId = 980,
            SegmentSirasi = 1,
            BaslangicTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 3, 9, 10, 0, 0)
        });

        dbContext.RezervasyonSegmentOdaAtamalari.AddRange(
            new RezervasyonSegmentOdaAtama
            {
                Id = 982,
                RezervasyonSegmentId = 981,
                OdaId = 101,
                AyrilanKisiSayisi = 1,
                OdaNoSnapshot = "A-102",
                BinaAdiSnapshot = "A Blok",
                OdaTipiAdiSnapshot = "Standart Double",
                PaylasimliMiSnapshot = false,
                KapasiteSnapshot = 2
            },
            new RezervasyonSegmentOdaAtama
            {
                Id = 983,
                RezervasyonSegmentId = 981,
                OdaId = 102,
                AyrilanKisiSayisi = 1,
                OdaNoSnapshot = "A-103",
                BinaAdiSnapshot = "A Blok",
                OdaTipiAdiSnapshot = "Deluxe Double",
                PaylasimliMiSnapshot = false,
                KapasiteSnapshot = 2
            });

        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var savedPlan = await service.KaydetKonaklayanPlaniAsync(980, new RezervasyonKonaklayanPlanKaydetRequestDto
        {
            Konaklayanlar =
            [
                new RezervasyonKonaklayanKisiKaydetDto
                {
                    SiraNo = 1,
                    AdSoyad = "Ali Kaya",
                    TcKimlikNo = "11111111111",
                    PasaportNo = null,
                    Atamalar = [new RezervasyonKonaklayanKisiAtamaKaydetDto { SegmentId = 981, OdaId = 101 }]
                },
                new RezervasyonKonaklayanKisiKaydetDto
                {
                    SiraNo = 2,
                    AdSoyad = "Ayse Kaya",
                    TcKimlikNo = "22222222222",
                    PasaportNo = null,
                    Atamalar = [new RezervasyonKonaklayanKisiAtamaKaydetDto { SegmentId = 981, OdaId = 102 }]
                }
            ]
        });

        Assert.Equal(2, savedPlan.Konaklayanlar.Count);
        Assert.Equal(101, savedPlan.Konaklayanlar.Single(x => x.SiraNo == 1).Atamalar.Single().OdaId);
        Assert.Equal(102, savedPlan.Konaklayanlar.Single(x => x.SiraNo == 2).Atamalar.Single().OdaId);

        var loadedPlan = await service.GetKonaklayanPlaniAsync(980);
        Assert.NotNull(loadedPlan);
        Assert.Equal("Ali Kaya", loadedPlan!.Konaklayanlar.Single(x => x.SiraNo == 1).AdSoyad);
    }

    // Segmentte oda kapasitesi asilirsa ayni odaya fazla kisi atamasi engellenmeli.
    [Fact]
    public async Task KonaklayanPlani_KapasiteAsiminiEngeller()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);

        dbContext.Rezervasyonlar.Add(new Rezervasyon
        {
            Id = 984,
            ReferansNo = "TEST-RZV-984",
            TesisId = 1,
            KisiSayisi = 2,
            GirisTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            CikisTarihi = new DateTime(2026, 3, 9, 10, 0, 0),
            MisafirAdiSoyadi = "Test Lider",
            MisafirTelefon = "000",
            ToplamBazUcret = 1000m,
            ToplamUcret = 1000m,
            ParaBirimi = "TRY",
            RezervasyonDurumu = RezervasyonDurumlari.Onayli,
            AktifMi = true
        });

        dbContext.RezervasyonSegmentleri.Add(new RezervasyonSegment
        {
            Id = 985,
            RezervasyonId = 984,
            SegmentSirasi = 1,
            BaslangicTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 3, 9, 10, 0, 0)
        });

        dbContext.RezervasyonSegmentOdaAtamalari.Add(new RezervasyonSegmentOdaAtama
        {
            Id = 986,
            RezervasyonSegmentId = 985,
            OdaId = 101,
            AyrilanKisiSayisi = 1,
            OdaNoSnapshot = "A-102",
            BinaAdiSnapshot = "A Blok",
            OdaTipiAdiSnapshot = "Standart Double",
            PaylasimliMiSnapshot = false,
            KapasiteSnapshot = 2
        });

        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<BaseException>(() => service.KaydetKonaklayanPlaniAsync(984, new RezervasyonKonaklayanPlanKaydetRequestDto
        {
            Konaklayanlar =
            [
                new RezervasyonKonaklayanKisiKaydetDto
                {
                    SiraNo = 1,
                    AdSoyad = "Ali Kaya",
                    Atamalar = [new RezervasyonKonaklayanKisiAtamaKaydetDto { SegmentId = 985, OdaId = 101 }]
                },
                new RezervasyonKonaklayanKisiKaydetDto
                {
                    SiraNo = 2,
                    AdSoyad = "Ayse Kaya",
                    Atamalar = [new RezervasyonKonaklayanKisiAtamaKaydetDto { SegmentId = 985, OdaId = 101 }]
                }
            ]
        }));

        Assert.Equal(400, exception.ErrorCode);
    }

    // Check-in, konaklayan plani tamamlanmadan yapilamamali.
    [Fact]
    public async Task CheckIn_KonaklayanPlaniEksikseHataVerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 990, segmentId: 991, withPlan: false);

        var service = CreateService(dbContext);
        var exception = await Assert.ThrowsAsync<BaseException>(() => service.TamamlaCheckInAsync(990));

        Assert.Equal(400, exception.ErrorCode);
        var updated = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 990);
        Assert.Equal(RezervasyonDurumlari.Onayli, updated.RezervasyonDurumu);
    }

    // Konaklayan plani tamamsa check-in durumu basariyla guncellenmeli.
    [Fact]
    public async Task CheckIn_KonaklayanPlaniTamamsaDurumuGunceller()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 992, segmentId: 993, withPlan: true);

        var service = CreateService(dbContext);
        var result = await service.TamamlaCheckInAsync(992);

        Assert.Equal(RezervasyonDurumlari.CheckInTamamlandi, result.RezervasyonDurumu);
        var updated = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 992);
        Assert.Equal(RezervasyonDurumlari.CheckInTamamlandi, updated.RezervasyonDurumu);
    }

    // Check-out isleminden once rezervasyon check-in durumuna alinmis olmali.
    [Fact]
    public async Task CheckOut_CheckInOlmadanHataVerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 994, segmentId: 995, withPlan: true);

        var service = CreateService(dbContext);
        var exception = await Assert.ThrowsAsync<BaseException>(() => service.TamamlaCheckOutAsync(994));

        Assert.Equal(400, exception.ErrorCode);
        var updated = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 994);
        Assert.Equal(RezervasyonDurumlari.Onayli, updated.RezervasyonDurumu);
    }

    // Check-out icin odeme tamamlandiginda durum basariyla CheckOutTamamlandi olmali.
    [Fact]
    public async Task CheckOut_CheckInSonrasiDurumuGunceller()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 996, segmentId: 997, withPlan: true);

        var service = CreateService(dbContext);
        await service.TamamlaCheckInAsync(996);
        await service.KaydetOdemeAsync(996, new RezervasyonOdemeKaydetRequestDto
        {
            OdemeTutari = 1000m,
            OdemeTipi = OdemeTipleri.Nakit
        });
        var result = await service.TamamlaCheckOutAsync(996);

        Assert.Equal(RezervasyonDurumlari.CheckOutTamamlandi, result.RezervasyonDurumu);
        var updated = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 996);
        Assert.Equal(RezervasyonDurumlari.CheckOutTamamlandi, updated.RezervasyonDurumu);
    }

    // Check-in yapilsa bile kalan bakiye varsa check-out engellenmeli.
    [Fact]
    public async Task CheckOut_OdemeTamamlanmadiysaHataVerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 9970, segmentId: 9971, withPlan: true);

        var service = CreateService(dbContext);
        await service.TamamlaCheckInAsync(9970);

        var exception = await Assert.ThrowsAsync<BaseException>(() => service.TamamlaCheckOutAsync(9970));

        Assert.Equal(400, exception.ErrorCode);
        var updated = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 9970);
        Assert.Equal(RezervasyonDurumlari.CheckInTamamlandi, updated.RezervasyonDurumu);
    }

    // Onayli rezervasyon iptal edildiginde durum Iptal olarak guncellenmeli.
    [Fact]
    public async Task IptalEt_OnayliRezervasyonDurumunuIptaleCeker()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 998, segmentId: 999, withPlan: false);
        var service = CreateService(dbContext);

        var result = await service.IptalEtAsync(998);

        Assert.Equal(RezervasyonDurumlari.Iptal, result.RezervasyonDurumu);
        var updated = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 998);
        Assert.Equal(RezervasyonDurumlari.Iptal, updated.RezervasyonDurumu);
    }

    // Iptal durumundaki rezervasyonun odalari hala musaitse iptal geri alinarak Taslak'a donmeli.
    [Fact]
    public async Task IptalEt_IptalDurumundaMusaitseTaslagaDondurur()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 1100, segmentId: 1101, withPlan: false);
        var service = CreateService(dbContext);
        await service.IptalEtAsync(1100);

        var result = await service.IptalEtAsync(1100);

        Assert.Equal(RezervasyonDurumlari.Taslak, result.RezervasyonDurumu);
        var updated = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 1100);
        Assert.Equal(RezervasyonDurumlari.Taslak, updated.RezervasyonDurumu);
    }

    // Iptal durumundaki rezervasyonun odalari dolmussa iptal geri alma islemi engellenmeli.
    [Fact]
    public async Task IptalEt_IptalDurumundaOdalarDoluysaHataVerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 1102, segmentId: 1103, withPlan: false);
        var service = CreateService(dbContext);
        await service.IptalEtAsync(1102);

        await SeedExistingReservationAsync(
            dbContext,
            odaId: 101,
            baslangic: new DateTime(2026, 3, 8, 14, 0, 0),
            bitis: new DateTime(2026, 3, 9, 10, 0, 0),
            kisiSayisi: 1,
            rezervasyonId: 1200,
            odaNoSnapshot: "A-102",
            tesisId: 1);

        var exception = await Assert.ThrowsAsync<BaseException>(() => service.IptalEtAsync(1102));
        Assert.Equal(400, exception.ErrorCode);

        var updated = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 1102);
        Assert.Equal(RezervasyonDurumlari.Iptal, updated.RezervasyonDurumu);
    }

    // Check-in tamamlanmis rezervasyon icin odeme kaydi eklenebilmeli ve kalan tutar azaltilmali.
    [Fact]
    public async Task KaydetOdeme_CheckInSonrasiOdemeAlir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 1000, segmentId: 1001, withPlan: true);
        var service = CreateService(dbContext);
        await service.TamamlaCheckInAsync(1000);

        var ozet = await service.KaydetOdemeAsync(1000, new RezervasyonOdemeKaydetRequestDto
        {
            OdemeTutari = 300m,
            OdemeTipi = OdemeTipleri.Nakit,
            Aciklama = "Pesin odeme"
        });

        Assert.Equal(1000, ozet.RezervasyonId);
        Assert.Equal(1000m, ozet.ToplamUcret);
        Assert.Equal(300m, ozet.OdenenTutar);
        Assert.Equal(700m, ozet.KalanTutar);
        var firstPayment = Assert.Single(ozet.Odemeler);
        Assert.Equal(OdemeTipleri.Nakit, firstPayment.OdemeTipi);
    }

    // Check-in oncesi (Onayli/Taslak) rezervasyonlarda da odeme alinabilmeli.
    [Fact]
    public async Task KaydetOdeme_CheckInOncesiOdemeAlir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 1002, segmentId: 1003, withPlan: true);
        var service = CreateService(dbContext);

        var ozet = await service.KaydetOdemeAsync(1002, new RezervasyonOdemeKaydetRequestDto
        {
            OdemeTutari = 200m,
            OdemeTipi = OdemeTipleri.KrediKarti
        });

        Assert.Equal(1002, ozet.RezervasyonId);
        Assert.Equal(200m, ozet.OdenenTutar);
        Assert.Equal(800m, ozet.KalanTutar);
        var firstPayment = Assert.Single(ozet.Odemeler);
        Assert.Equal(OdemeTipleri.KrediKarti, firstPayment.OdemeTipi);
    }

    // Liste sonucunda check-in butonu icin kullanilan plan-tamamlandi bilgisi dogru hesaplanmali.
    [Fact]
    public async Task RezervasyonListesi_KonaklayanPlaniTamamlandiBilgisiniDogruDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 1004, segmentId: 1005, withPlan: true);
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 1006, segmentId: 1007, withPlan: false);
        var service = CreateService(dbContext);

        var list = await service.GetRezervasyonlarAsync(1);
        var planned = Assert.Single(list, x => x.Id == 1004);
        var unplanned = Assert.Single(list, x => x.Id == 1006);

        Assert.True(planned.KonaklayanPlaniTamamlandi);
        Assert.False(unplanned.KonaklayanPlaniTamamlandi);
    }

    private static async Task SeedReservationForCheckFlowAsync(
        StysAppDbContext dbContext,
        int rezervasyonId,
        int segmentId,
        bool withPlan)
    {
        if (!await dbContext.Tesisler.AnyAsync())
        {
            await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        }

        dbContext.Rezervasyonlar.Add(new Rezervasyon
        {
            Id = rezervasyonId,
            ReferansNo = $"TEST-RZV-{rezervasyonId}",
            TesisId = 1,
            KisiSayisi = 1,
            GirisTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            CikisTarihi = new DateTime(2026, 3, 9, 10, 0, 0),
            MisafirAdiSoyadi = "Check Test",
            MisafirTelefon = "000",
            ToplamBazUcret = 1000m,
            ToplamUcret = 1000m,
            ParaBirimi = "TRY",
            RezervasyonDurumu = RezervasyonDurumlari.Onayli,
            AktifMi = true
        });

        dbContext.RezervasyonSegmentleri.Add(new RezervasyonSegment
        {
            Id = segmentId,
            RezervasyonId = rezervasyonId,
            SegmentSirasi = 1,
            BaslangicTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 3, 9, 10, 0, 0)
        });

        dbContext.RezervasyonSegmentOdaAtamalari.Add(new RezervasyonSegmentOdaAtama
        {
            Id = segmentId + 1,
            RezervasyonSegmentId = segmentId,
            OdaId = 101,
            AyrilanKisiSayisi = 1,
            OdaNoSnapshot = "A-102",
            BinaAdiSnapshot = "A Blok",
            OdaTipiAdiSnapshot = "Standart Double",
            PaylasimliMiSnapshot = false,
            KapasiteSnapshot = 2
        });

        if (withPlan)
        {
            dbContext.RezervasyonKonaklayanlar.Add(new RezervasyonKonaklayan
            {
                Id = rezervasyonId + 1000,
                RezervasyonId = rezervasyonId,
                SiraNo = 1,
                AdSoyad = "Ali Check",
                TcKimlikNo = "11111111111",
                PasaportNo = null
            });

            dbContext.RezervasyonKonaklayanSegmentAtamalari.Add(new RezervasyonKonaklayanSegmentAtama
            {
                Id = rezervasyonId + 1001,
                RezervasyonKonaklayanId = rezervasyonId + 1000,
                RezervasyonSegmentId = segmentId,
                OdaId = 101
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static RezervasyonService CreateService(
        StysAppDbContext dbContext,
        DomainAccessScope? scope = null,
        IReadOnlyCollection<string>? permissions = null)
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };

        var claims = (permissions ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(x => new Claim("permission", x))
            .ToList();

        httpContextAccessor.HttpContext!.User = new ClaimsPrincipal(
            new ClaimsIdentity(claims, authenticationType: claims.Count > 0 ? "TestAuth" : null));

        return new RezervasyonService(
            dbContext,
            new FakeUserAccessScopeService(scope ?? DomainAccessScope.Unscoped()),
            httpContextAccessor);
    }

    private static RezervasyonKaydetRequestDto BuildCustomDiscountSaveRequest()
    {
        return new RezervasyonKaydetRequestDto
        {
            TesisId = 1,
            KisiSayisi = 1,
            GirisTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            CikisTarihi = new DateTime(2026, 3, 10, 10, 0, 0),
            MisafirAdiSoyadi = "Custom Test Misafir",
            MisafirTelefon = "5550000000",
            MisafirEposta = null,
            TcKimlikNo = null,
            PasaportNo = null,
            Notlar = "Custom indirim testi",
            ToplamBazUcret = 1200m,
            ToplamUcret = 900m,
            ParaBirimi = "TRY",
            UygulananIndirimler =
            [
                new UygulananIndirimDto
                {
                    IndirimKuraliId = 0,
                    KuralAdi = "Manuel 300 TL",
                    IndirimTutari = 300m,
                    SonrasiTutar = 900m
                }
            ],
            Segmentler =
            [
                new RezervasyonKaydetSegmentDto
                {
                    BaslangicTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
                    BitisTarihi = new DateTime(2026, 3, 10, 10, 0, 0),
                    OdaAtamalari =
                    [
                        new RezervasyonKaydetOdaAtamaDto
                        {
                            OdaId = 101,
                            AyrilanKisiSayisi = 1
                        }
                    ]
                }
            ]
        };
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

    private static async Task SeedStandardAndSharedRoomsWithDifferentPricesAsync(StysAppDbContext dbContext)
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
                Ad = "Paylasimli",
                Kapasite = 2,
                PaylasimliMi = true,
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
                Fiyat = 500m,
                ParaBirimi = "TRY",
                BaslangicTarihi = new DateTime(2026, 3, 1),
                BitisTarihi = new DateTime(2026, 3, 31),
                AktifMi = true
            });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedReservationFixtureWithTenRoomsAsync(StysAppDbContext dbContext)
    {
        await SeedLookupsAsync(dbContext);

        dbContext.Tesisler.AddRange(
            new Tesis
            {
                Id = 1,
                Ad = "Alpha Konukevi",
                IlId = 1,
                Telefon = "000",
                Adres = "Adres 1",
                GirisSaati = new TimeSpan(14, 0, 0),
                CikisSaati = new TimeSpan(10, 0, 0),
                AktifMi = true
            },
            new Tesis
            {
                Id = 2,
                Ad = "Beta Konukevi",
                IlId = 1,
                Telefon = "111",
                Adres = "Adres 2",
                GirisSaati = new TimeSpan(14, 0, 0),
                CikisSaati = new TimeSpan(10, 0, 0),
                AktifMi = true
            });

        dbContext.Binalar.AddRange(
            new Bina { Id = 10, TesisId = 1, Ad = "A Blok", KatSayisi = 5, AktifMi = true },
            new Bina { Id = 11, TesisId = 1, Ad = "B Blok", KatSayisi = 5, AktifMi = true },
            new Bina { Id = 20, TesisId = 2, Ad = "C Blok", KatSayisi = 3, AktifMi = true });

        dbContext.OdaTipleri.AddRange(
            new OdaTipi { Id = 200, TesisId = 1, OdaSinifiId = 1, Ad = "Ekonomi Tek", Kapasite = 1, PaylasimliMi = false, AktifMi = true },
            new OdaTipi { Id = 201, TesisId = 1, OdaSinifiId = 1, Ad = "Standart Double", Kapasite = 2, PaylasimliMi = false, AktifMi = true },
            new OdaTipi { Id = 202, TesisId = 1, OdaSinifiId = 1, Ad = "Deluxe Double", Kapasite = 2, PaylasimliMi = false, AktifMi = true },
            new OdaTipi { Id = 203, TesisId = 1, OdaSinifiId = 1, Ad = "Suite", Kapasite = 3, PaylasimliMi = false, AktifMi = true },
            new OdaTipi { Id = 204, TesisId = 1, OdaSinifiId = 1, Ad = "Aile", Kapasite = 4, PaylasimliMi = false, AktifMi = true },
            new OdaTipi { Id = 205, TesisId = 1, OdaSinifiId = 1, Ad = "Hostel 2", Kapasite = 2, PaylasimliMi = true, AktifMi = true },
            new OdaTipi { Id = 206, TesisId = 1, OdaSinifiId = 1, Ad = "Hostel 4", Kapasite = 4, PaylasimliMi = true, AktifMi = true },
            new OdaTipi { Id = 207, TesisId = 1, OdaSinifiId = 1, Ad = "Business Tek", Kapasite = 1, PaylasimliMi = false, AktifMi = true },
            new OdaTipi { Id = 208, TesisId = 1, OdaSinifiId = 1, Ad = "Premium Double", Kapasite = 2, PaylasimliMi = false, AktifMi = true },
            new OdaTipi { Id = 209, TesisId = 1, OdaSinifiId = 1, Ad = "King Suite", Kapasite = 2, PaylasimliMi = false, AktifMi = true },
            new OdaTipi { Id = 300, TesisId = 2, OdaSinifiId = 1, Ad = "Standart T2", Kapasite = 2, PaylasimliMi = false, AktifMi = true });

        dbContext.Odalar.AddRange(
            new Oda { Id = 100, OdaNo = "A-101", BinaId = 10, TesisOdaTipiId = 200, KatNo = 1, AktifMi = true },
            new Oda { Id = 101, OdaNo = "A-102", BinaId = 10, TesisOdaTipiId = 201, KatNo = 1, AktifMi = true },
            new Oda { Id = 102, OdaNo = "A-103", BinaId = 10, TesisOdaTipiId = 202, KatNo = 1, AktifMi = true },
            new Oda { Id = 103, OdaNo = "A-104", BinaId = 10, TesisOdaTipiId = 203, KatNo = 1, AktifMi = true },
            new Oda { Id = 104, OdaNo = "A-105", BinaId = 10, TesisOdaTipiId = 204, KatNo = 1, AktifMi = true },
            new Oda { Id = 105, OdaNo = "B-201", BinaId = 11, TesisOdaTipiId = 205, KatNo = 2, AktifMi = true },
            new Oda { Id = 106, OdaNo = "B-202", BinaId = 11, TesisOdaTipiId = 206, KatNo = 2, AktifMi = true },
            new Oda { Id = 107, OdaNo = "B-203", BinaId = 11, TesisOdaTipiId = 207, KatNo = 2, AktifMi = true },
            new Oda { Id = 108, OdaNo = "B-204", BinaId = 11, TesisOdaTipiId = 208, KatNo = 2, AktifMi = true },
            new Oda { Id = 109, OdaNo = "B-205", BinaId = 11, TesisOdaTipiId = 209, KatNo = 2, AktifMi = true },
            new Oda { Id = 200, OdaNo = "C-101", BinaId = 20, TesisOdaTipiId = 300, KatNo = 1, AktifMi = true });

        dbContext.OdaFiyatlari.AddRange(
            new OdaFiyat { Id = 2000, TesisOdaTipiId = 200, KonaklamaTipiId = 1, MisafirTipiId = 1, KisiSayisi = 1, Fiyat = 600m, ParaBirimi = "TRY", BaslangicTarihi = new DateTime(2026, 3, 1), BitisTarihi = new DateTime(2026, 3, 31), AktifMi = true },
            new OdaFiyat { Id = 2001, TesisOdaTipiId = 201, KonaklamaTipiId = 1, MisafirTipiId = 1, KisiSayisi = 1, Fiyat = 900m, ParaBirimi = "TRY", BaslangicTarihi = new DateTime(2026, 3, 1), BitisTarihi = new DateTime(2026, 3, 31), AktifMi = true },
            new OdaFiyat { Id = 2002, TesisOdaTipiId = 202, KonaklamaTipiId = 1, MisafirTipiId = 1, KisiSayisi = 1, Fiyat = 1200m, ParaBirimi = "TRY", BaslangicTarihi = new DateTime(2026, 3, 1), BitisTarihi = new DateTime(2026, 3, 31), AktifMi = true },
            new OdaFiyat { Id = 2003, TesisOdaTipiId = 203, KonaklamaTipiId = 1, MisafirTipiId = 1, KisiSayisi = 1, Fiyat = 1700m, ParaBirimi = "TRY", BaslangicTarihi = new DateTime(2026, 3, 1), BitisTarihi = new DateTime(2026, 3, 31), AktifMi = true },
            new OdaFiyat { Id = 2004, TesisOdaTipiId = 204, KonaklamaTipiId = 1, MisafirTipiId = 1, KisiSayisi = 1, Fiyat = 2200m, ParaBirimi = "TRY", BaslangicTarihi = new DateTime(2026, 3, 1), BitisTarihi = new DateTime(2026, 3, 31), AktifMi = true },
            new OdaFiyat { Id = 2005, TesisOdaTipiId = 205, KonaklamaTipiId = 1, MisafirTipiId = 1, KisiSayisi = 1, Fiyat = 500m, ParaBirimi = "TRY", BaslangicTarihi = new DateTime(2026, 3, 1), BitisTarihi = new DateTime(2026, 3, 31), AktifMi = true },
            new OdaFiyat { Id = 2006, TesisOdaTipiId = 206, KonaklamaTipiId = 1, MisafirTipiId = 1, KisiSayisi = 1, Fiyat = 450m, ParaBirimi = "TRY", BaslangicTarihi = new DateTime(2026, 3, 1), BitisTarihi = new DateTime(2026, 3, 31), AktifMi = true },
            new OdaFiyat { Id = 2007, TesisOdaTipiId = 207, KonaklamaTipiId = 1, MisafirTipiId = 1, KisiSayisi = 1, Fiyat = 1100m, ParaBirimi = "TRY", BaslangicTarihi = new DateTime(2026, 3, 1), BitisTarihi = new DateTime(2026, 3, 31), AktifMi = true },
            new OdaFiyat { Id = 2008, TesisOdaTipiId = 208, KonaklamaTipiId = 1, MisafirTipiId = 1, KisiSayisi = 1, Fiyat = 1500m, ParaBirimi = "TRY", BaslangicTarihi = new DateTime(2026, 3, 1), BitisTarihi = new DateTime(2026, 3, 31), AktifMi = true },
            new OdaFiyat { Id = 2009, TesisOdaTipiId = 209, KonaklamaTipiId = 1, MisafirTipiId = 1, KisiSayisi = 1, Fiyat = 2500m, ParaBirimi = "TRY", BaslangicTarihi = new DateTime(2026, 3, 1), BitisTarihi = new DateTime(2026, 3, 31), AktifMi = true },
            new OdaFiyat { Id = 2300, TesisOdaTipiId = 300, KonaklamaTipiId = 1, MisafirTipiId = 1, KisiSayisi = 1, Fiyat = 800m, ParaBirimi = "TRY", BaslangicTarihi = new DateTime(2026, 3, 1), BitisTarihi = new DateTime(2026, 3, 31), AktifMi = true });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedDiscountRulesForPricingAsync(StysAppDbContext dbContext)
    {
        dbContext.IndirimKurallari.AddRange(
            new IndirimKurali
            {
                Id = 5001,
                Kod = "SYS-10",
                Ad = "Genel Yuzde 10",
                IndirimTipi = IndirimTipleri.Yuzde,
                Deger = 10m,
                KapsamTipi = IndirimKapsamTipleri.Sistem,
                BaslangicTarihi = new DateTime(2026, 3, 1),
                BitisTarihi = new DateTime(2026, 3, 31),
                Oncelik = 1,
                BirlesebilirMi = true,
                AktifMi = true
            },
            new IndirimKurali
            {
                Id = 5002,
                Kod = "TESIS-100",
                Ad = "Tesis Sabit 100",
                IndirimTipi = IndirimTipleri.Tutar,
                Deger = 100m,
                KapsamTipi = IndirimKapsamTipleri.Tesis,
                TesisId = 1,
                BaslangicTarihi = new DateTime(2026, 3, 1),
                BitisTarihi = new DateTime(2026, 3, 31),
                Oncelik = 10,
                BirlesebilirMi = true,
                AktifMi = true
            });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedDiscountRulesForQueryAsync(StysAppDbContext dbContext)
    {
        dbContext.IndirimKurallari.AddRange(
            new IndirimKurali
            {
                Id = 5101,
                Kod = "SYS-5",
                Ad = "Sistem Yuzde 5",
                IndirimTipi = IndirimTipleri.Yuzde,
                Deger = 5m,
                KapsamTipi = IndirimKapsamTipleri.Sistem,
                BaslangicTarihi = new DateTime(2026, 3, 1),
                BitisTarihi = new DateTime(2026, 3, 31),
                Oncelik = 1,
                BirlesebilirMi = true,
                AktifMi = true
            },
            new IndirimKurali
            {
                Id = 5102,
                Kod = "TESIS-50",
                Ad = "Tesis 50 TL",
                IndirimTipi = IndirimTipleri.Tutar,
                Deger = 50m,
                KapsamTipi = IndirimKapsamTipleri.Tesis,
                TesisId = 1,
                BaslangicTarihi = new DateTime(2026, 3, 1),
                BitisTarihi = new DateTime(2026, 3, 31),
                Oncelik = 1,
                BirlesebilirMi = true,
                AktifMi = true
            },
            new IndirimKurali
            {
                Id = 5103,
                Kod = "TESIS2-50",
                Ad = "Tesis2 50 TL",
                IndirimTipi = IndirimTipleri.Tutar,
                Deger = 50m,
                KapsamTipi = IndirimKapsamTipleri.Tesis,
                TesisId = 2,
                BaslangicTarihi = new DateTime(2026, 3, 1),
                BitisTarihi = new DateTime(2026, 3, 31),
                Oncelik = 1,
                BirlesebilirMi = true,
                AktifMi = true
            },
            new IndirimKurali
            {
                Id = 5104,
                Kod = "EXPIRED-20",
                Ad = "Suresi Gecmis Kural",
                IndirimTipi = IndirimTipleri.Yuzde,
                Deger = 20m,
                KapsamTipi = IndirimKapsamTipleri.Sistem,
                BaslangicTarihi = new DateTime(2026, 2, 1),
                BitisTarihi = new DateTime(2026, 2, 28),
                Oncelik = 1,
                BirlesebilirMi = true,
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
        string odaNoSnapshot = "ODA-A",
        int tesisId = 1)
    {
        dbContext.Rezervasyonlar.Add(new Rezervasyon
        {
            Id = rezervasyonId,
            ReferansNo = $"TEST-RZV-{rezervasyonId}",
            TesisId = tesisId,
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

    private static async Task SeedSezonKuraliAsync(
        StysAppDbContext dbContext,
        int id,
        int tesisId,
        string kod,
        string ad,
        DateTime baslangic,
        DateTime bitis,
        int minimumGece,
        bool stopSaleMi)
    {
        dbContext.SezonKurallari.Add(new SezonKurali
        {
            Id = id,
            TesisId = tesisId,
            Kod = kod,
            Ad = ad,
            BaslangicTarihi = baslangic.Date,
            BitisTarihi = bitis.Date,
            MinimumGece = minimumGece,
            StopSaleMi = stopSaleMi,
            AktifMi = true
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
