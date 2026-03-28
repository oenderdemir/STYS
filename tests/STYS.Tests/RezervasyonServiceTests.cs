using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using STYS.Fiyatlandirma;
using STYS.Fiyatlandirma.Dto;
using STYS.EkHizmetler.Entities;
using STYS.AccessScope;
using STYS.Binalar.Entities;
using STYS.Bildirimler.Dto;
using STYS.Bildirimler.Services;
using STYS.Fiyatlandirma.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.IsletmeAlanlari.Entities;
using STYS.KonaklamaTipleri;
using STYS.KonaklamaTipleri.Entities;
using STYS.MisafirTipleri.Entities;
using STYS.OdaKullanimBloklari;
using STYS.OdaKullanimBloklari.Entities;
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

    // Bos bir paylasimli oda olsa bile karma cinsiyetli grup ayni shared oda senaryosunda birlestirilmemeli.
    [Fact]
    public async Task SenaryoUretimi_KarmaCinsiyetliGrubuTekPaylasimliOdayaYerlestirmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedSingleSharedRoomScenarioFixtureAsync(dbContext);
        var service = CreateService(dbContext);

        var scenarios = await service.GetKonaklamaSenaryolariAsync(new KonaklamaSenaryoAramaRequestDto
        {
            TesisId = 1,
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
            KisiSayisi = 2,
            BaslangicTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 3, 9, 10, 0, 0),
            KonaklayanCinsiyetleri = [KonaklayanCinsiyetleri.Kadin, KonaklayanCinsiyetleri.Erkek]
        });

        Assert.Empty(scenarios);
    }

    // Mevcutta kadin bulunan paylasimli oda, erkek konaklayan icin arama sonucunda aday olmamali.
    [Fact]
    public async Task SenaryoUretimi_MevcutPaylasimliOdaCinsiyetineAykiriAdayUretmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedSingleSharedRoomScenarioFixtureAsync(dbContext);
        await SeedSharedRoomReservationWithGuestAsync(
            dbContext,
            rezervasyonId: 9700,
            segmentId: 9701,
            odaAtamaId: 9702,
            konaklayanId: 9703,
            konaklayanAtamaId: 9704,
            odaId: 100,
            cinsiyet: KonaklayanCinsiyetleri.Kadin,
            yatakNo: 1);

        var service = CreateService(dbContext);
        var scenarios = await service.GetKonaklamaSenaryolariAsync(new KonaklamaSenaryoAramaRequestDto
        {
            TesisId = 1,
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
            KisiSayisi = 1,
            BaslangicTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 3, 9, 10, 0, 0),
            KonaklayanCinsiyetleri = [KonaklayanCinsiyetleri.Erkek]
        });

        Assert.Empty(scenarios);
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

    // Konaklayan Gelmedi olarak netlestirilip atamasi kaldirildiysa, bos kalan oda tekrar uygun hale gelmeli.
    [Fact]
    public async Task UygunOdaArama_GelmeyenKonaklayanSonrasiOdayiTekrarUygunYapar()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 973, segmentId: 974, withPlan: true);

        var guest = await dbContext.RezervasyonKonaklayanlar.SingleAsync(x => x.RezervasyonId == 973);
        guest.KatilimDurumu = KonaklayanKatilimDurumlari.Gelmedi;
        var guestAssignments = await dbContext.RezervasyonKonaklayanSegmentAtamalari
            .Where(x => x.RezervasyonKonaklayanId == guest.Id)
            .ToListAsync();
        dbContext.RezervasyonKonaklayanSegmentAtamalari.RemoveRange(guestAssignments);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var rooms = await service.GetUygunOdalarAsync(new UygunOdaAramaRequestDto
        {
            TesisId = 1,
            KisiSayisi = 1,
            BaslangicTarihi = new DateTime(2026, 3, 8, 15, 0, 0),
            BitisTarihi = new DateTime(2026, 3, 9, 9, 0, 0)
        });

        Assert.Contains(rooms, x => x.OdaId == 101);
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
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
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

    // Paylasimli odada ayni yatak birden fazla kisiye atanamamali.
    [Fact]
    public async Task KonaklayanPlani_PaylasimliOdadaAyniYatagiBirdenFazlaKisiyeAtamayiEngeller()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);

        dbContext.Rezervasyonlar.Add(new Rezervasyon
        {
            Id = 987,
            ReferansNo = "TEST-RZV-987",
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
            Id = 988,
            RezervasyonId = 987,
            SegmentSirasi = 1,
            BaslangicTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 3, 9, 10, 0, 0)
        });

        dbContext.RezervasyonSegmentOdaAtamalari.Add(new RezervasyonSegmentOdaAtama
        {
            Id = 989,
            RezervasyonSegmentId = 988,
            OdaId = 101,
            AyrilanKisiSayisi = 2,
            OdaNoSnapshot = "A-102",
            BinaAdiSnapshot = "A Blok",
            OdaTipiAdiSnapshot = "Paylasimli Oda",
            PaylasimliMiSnapshot = true,
            KapasiteSnapshot = 4
        });

        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<BaseException>(() => service.KaydetKonaklayanPlaniAsync(987, new RezervasyonKonaklayanPlanKaydetRequestDto
        {
            Konaklayanlar =
            [
                new RezervasyonKonaklayanKisiKaydetDto
                {
                    SiraNo = 1,
                    AdSoyad = "Ali Kaya",
                    Cinsiyet = KonaklayanCinsiyetleri.Erkek,
                    Atamalar = [new RezervasyonKonaklayanKisiAtamaKaydetDto { SegmentId = 988, OdaId = 101, YatakNo = 1 }]
                },
                new RezervasyonKonaklayanKisiKaydetDto
                {
                    SiraNo = 2,
                    AdSoyad = "Ayse Kaya",
                    Cinsiyet = KonaklayanCinsiyetleri.Erkek,
                    Atamalar = [new RezervasyonKonaklayanKisiAtamaKaydetDto { SegmentId = 988, OdaId = 101, YatakNo = 1 }]
                }
            ]
        }));

        Assert.Equal(400, exception.ErrorCode);
    }

    // Paylasimli odada mevcut konaklayan kadinsa ayni araliktaki yeni konaklayan da kadin ise plan kaydi kabul edilmeli.
    [Fact]
    public async Task KonaklayanPlani_PaylasimliOdadaAyniCinsiyetiKabulEder()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        await SeedSharedRoomReservationWithGuestAsync(
            dbContext,
            rezervasyonId: 9900,
            segmentId: 9901,
            odaAtamaId: 9902,
            konaklayanId: 9903,
            konaklayanAtamaId: 9904,
            odaId: 105,
            cinsiyet: KonaklayanCinsiyetleri.Kadin,
            yatakNo: 2);

        dbContext.Rezervasyonlar.Add(new Rezervasyon
        {
            Id = 9905,
            ReferansNo = "TEST-RZV-9905",
            TesisId = 1,
            KisiSayisi = 1,
            GirisTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            CikisTarihi = new DateTime(2026, 3, 9, 10, 0, 0),
            MisafirAdiSoyadi = "Yeni Misafir",
            MisafirTelefon = "000",
            ToplamBazUcret = 500m,
            ToplamUcret = 500m,
            ParaBirimi = "TRY",
            RezervasyonDurumu = RezervasyonDurumlari.Onayli,
            AktifMi = true
        });
        dbContext.RezervasyonSegmentleri.Add(new RezervasyonSegment
        {
            Id = 9906,
            RezervasyonId = 9905,
            SegmentSirasi = 1,
            BaslangicTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 3, 9, 10, 0, 0)
        });
        dbContext.RezervasyonSegmentOdaAtamalari.Add(new RezervasyonSegmentOdaAtama
        {
            Id = 9907,
            RezervasyonSegmentId = 9906,
            OdaId = 105,
            AyrilanKisiSayisi = 1,
            OdaNoSnapshot = "B-201",
            BinaAdiSnapshot = "B Blok",
            OdaTipiAdiSnapshot = "Hostel 2",
            PaylasimliMiSnapshot = true,
            KapasiteSnapshot = 2
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.KaydetKonaklayanPlaniAsync(9905, new RezervasyonKonaklayanPlanKaydetRequestDto
        {
            Konaklayanlar =
            [
                new RezervasyonKonaklayanKisiKaydetDto
                {
                    SiraNo = 1,
                    AdSoyad = "Ayse Yeni",
                    Cinsiyet = KonaklayanCinsiyetleri.Kadin,
                    Atamalar = [new RezervasyonKonaklayanKisiAtamaKaydetDto { SegmentId = 9906, OdaId = 105, YatakNo = 1 }]
                }
            ]
        });

        var guest = Assert.Single(result.Konaklayanlar);
        Assert.Equal(KonaklayanCinsiyetleri.Kadin, guest.Cinsiyet);
    }

    // Paylasimli odada mevcut konaklayanla farkli cinsiyette yeni konaklayan ayni oda icin kaydedilememeli.
    [Fact]
    public async Task KonaklayanPlani_PaylasimliOdadaFarkliCinsiyetiReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        await SeedSharedRoomReservationWithGuestAsync(
            dbContext,
            rezervasyonId: 9910,
            segmentId: 9911,
            odaAtamaId: 9912,
            konaklayanId: 9913,
            konaklayanAtamaId: 9914,
            odaId: 105,
            cinsiyet: KonaklayanCinsiyetleri.Kadin,
            yatakNo: 2);

        dbContext.Rezervasyonlar.Add(new Rezervasyon
        {
            Id = 9915,
            ReferansNo = "TEST-RZV-9915",
            TesisId = 1,
            KisiSayisi = 1,
            GirisTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            CikisTarihi = new DateTime(2026, 3, 9, 10, 0, 0),
            MisafirAdiSoyadi = "Yeni Misafir",
            MisafirTelefon = "000",
            ToplamBazUcret = 500m,
            ToplamUcret = 500m,
            ParaBirimi = "TRY",
            RezervasyonDurumu = RezervasyonDurumlari.Onayli,
            AktifMi = true
        });
        dbContext.RezervasyonSegmentleri.Add(new RezervasyonSegment
        {
            Id = 9916,
            RezervasyonId = 9915,
            SegmentSirasi = 1,
            BaslangicTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 3, 9, 10, 0, 0)
        });
        dbContext.RezervasyonSegmentOdaAtamalari.Add(new RezervasyonSegmentOdaAtama
        {
            Id = 9917,
            RezervasyonSegmentId = 9916,
            OdaId = 105,
            AyrilanKisiSayisi = 1,
            OdaNoSnapshot = "B-201",
            BinaAdiSnapshot = "B Blok",
            OdaTipiAdiSnapshot = "Hostel 2",
            PaylasimliMiSnapshot = true,
            KapasiteSnapshot = 2
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var exception = await Assert.ThrowsAsync<BaseException>(() => service.KaydetKonaklayanPlaniAsync(9915, new RezervasyonKonaklayanPlanKaydetRequestDto
        {
            Konaklayanlar =
            [
                new RezervasyonKonaklayanKisiKaydetDto
                {
                    SiraNo = 1,
                    AdSoyad = "Mehmet Yeni",
                    Cinsiyet = KonaklayanCinsiyetleri.Erkek,
                    Atamalar = [new RezervasyonKonaklayanKisiAtamaKaydetDto { SegmentId = 9916, OdaId = 105, YatakNo = 1 }]
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

    // Check-in icin en az bir konaklayan Geldi olarak isaretlenmis olmali.
    [Fact]
    public async Task CheckIn_GelenMisafirYoksaHataVerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 9921, segmentId: 9922, withPlan: true);
        var guest = await dbContext.RezervasyonKonaklayanlar.SingleAsync(x => x.RezervasyonId == 9921);
        guest.KatilimDurumu = KonaklayanKatilimDurumlari.Bekleniyor;
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var exception = await Assert.ThrowsAsync<BaseException>(() => service.TamamlaCheckInAsync(9921));

        Assert.Equal(400, exception.ErrorCode);
    }

    // Check-in tamamlanmis ve aktif blokaj bulunan rezervasyonda oda degisimi secenekleri getirilebilmeli.
    [Fact]
    public async Task OdaDegisimi_CheckInTamamlanmisRezervasyondaSecenekleriGetirebilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(
            dbContext,
            rezervasyonId: 1024,
            segmentId: 1025,
            withPlan: true,
            konaklayanCinsiyet: KonaklayanCinsiyetleri.Erkek);
        await SeedRoomBlockForReservationAsync(dbContext, rezervasyonId: 1024, segmentId: 1025, odaId: 101);

        var reservation = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 1024);
        reservation.RezervasyonDurumu = RezervasyonDurumlari.CheckInTamamlandi;
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetOdaDegisimSecenekleriAsync(1024);

        var kayit = Assert.Single(result.Kayitlar);
        Assert.Contains(kayit.TasinacakKonaklayanlar, x => x.AdSoyad == "Ali Check");
        Assert.Contains(kayit.AdayOdalar, x => x.OdaId == 102);
        Assert.Contains(kayit.AdayOdalar, x => x.PaylasimliMi && x.OnerilenYatakNolari.Count > 0);
    }

    // Check-in sonrasi oda degisiminde paylasimli oda mevcut konaklayanla farkli cinsiyet ise aday olarak gelmemeli.
    [Fact]
    public async Task OdaDegisimi_CheckInSonrasiFarkliCinsiyetliPaylasimliOdayiEleme()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(
            dbContext,
            rezervasyonId: 10240,
            segmentId: 10241,
            withPlan: true,
            konaklayanCinsiyet: KonaklayanCinsiyetleri.Erkek);
        await SeedRoomBlockForReservationAsync(dbContext, rezervasyonId: 10240, segmentId: 10241, odaId: 101);
        await SeedSharedRoomReservationWithGuestAsync(
            dbContext,
            rezervasyonId: 10250,
            segmentId: 10251,
            odaAtamaId: 10252,
            konaklayanId: 10253,
            konaklayanAtamaId: 10254,
            odaId: 105,
            cinsiyet: KonaklayanCinsiyetleri.Kadin,
            yatakNo: 1);

        var reservation = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 10240);
        reservation.RezervasyonDurumu = RezervasyonDurumlari.CheckInTamamlandi;
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetOdaDegisimSecenekleriAsync(10240);
        var kayit = Assert.Single(result.Kayitlar);

        Assert.DoesNotContain(kayit.AdayOdalar, x => x.OdaId == 105);
    }

    // Check-in sonrasi oda degisiminde konaklayan atamasi yeni odaya tasinmali ve durum korunmali.
    [Fact]
    public async Task OdaDegisimi_CheckInSonrasiKonaklayanAtamasiniYeniOdayaTasir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 1026, segmentId: 1027, withPlan: true);
        await SeedRoomBlockForReservationAsync(dbContext, rezervasyonId: 1026, segmentId: 1027, odaId: 101);

        var reservation = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 1026);
        reservation.RezervasyonDurumu = RezervasyonDurumlari.CheckInTamamlandi;
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var secenekler = await service.GetOdaDegisimSecenekleriAsync(1026);
        var kayit = Assert.Single(secenekler.Kayitlar);

        var result = await service.KaydetOdaDegisimiAsync(1026, new RezervasyonOdaDegisimKaydetRequestDto
        {
            Atamalar =
            [
                new RezervasyonOdaDegisimKaydetAtamaDto
                {
                    RezervasyonSegmentOdaAtamaId = kayit.RezervasyonSegmentOdaAtamaId,
                    YeniOdaId = 102
                }
            ]
        });

        var updatedReservation = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 1026);
        var roomAssignment = await dbContext.RezervasyonSegmentOdaAtamalari.SingleAsync(x => x.RezervasyonSegmentId == 1027);
        var guestAssignment = await dbContext.RezervasyonKonaklayanSegmentAtamalari.SingleAsync(x => x.RezervasyonSegmentId == 1027);

        Assert.Equal(RezervasyonDurumlari.CheckInTamamlandi, result.RezervasyonDurumu);
        Assert.Equal(RezervasyonDurumlari.CheckInTamamlandi, updatedReservation.RezervasyonDurumu);
        Assert.Equal(102, roomAssignment.OdaId);
        Assert.Equal(102, guestAssignment.OdaId);
    }

    // Konaklayan plani henuz kaydedilmemisse ana misafir cinsiyeti ilk kisiye varsayilan olarak yansitilmali.
    [Fact]
    public async Task KonaklayanPlani_AnaMisafirCinsiyetiniIlkKisiyeVarsayilanYansitir()
    {
        await using var dbContext = CreateDbContext();
        await SeedSingleRoomFixtureAsync(dbContext, new TimeSpan(14, 0, 0), new TimeSpan(10, 0, 0), 1000m);

        var service = CreateService(dbContext);
        var result = await service.KaydetAsync(new RezervasyonKaydetRequestDto
        {
            TesisId = 1,
            KisiSayisi = 1,
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
            GirisTarihi = new DateTime(2026, 3, 10, 14, 0, 0),
            CikisTarihi = new DateTime(2026, 3, 11, 10, 0, 0),
            MisafirAdiSoyadi = "Ayse Ana Misafir",
            MisafirTelefon = "05550000000",
            MisafirCinsiyeti = KonaklayanCinsiyetleri.Kadin,
            ToplamBazUcret = 1000m,
            ToplamUcret = 1000m,
            ParaBirimi = "TRY",
            Segmentler =
            [
                new RezervasyonKaydetSegmentDto
                {
                    BaslangicTarihi = new DateTime(2026, 3, 10, 14, 0, 0),
                    BitisTarihi = new DateTime(2026, 3, 11, 10, 0, 0),
                    OdaAtamalari =
                    [
                        new RezervasyonKaydetOdaAtamaDto
                        {
                            OdaId = 100,
                            AyrilanKisiSayisi = 1
                        }
                    ]
                }
            ]
        });

        var plan = await service.GetKonaklayanPlaniAsync(result.Id);

        var firstGuest = Assert.Single(plan!.Konaklayanlar);
        Assert.Equal(KonaklayanCinsiyetleri.Kadin, firstGuest.Cinsiyet);
        Assert.Equal("Ayse Ana Misafir", firstGuest.AdSoyad);
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
        var guest = await dbContext.RezervasyonKonaklayanlar.SingleAsync(x => x.RezervasyonId == 996);
        Assert.Equal(KonaklayanKatilimDurumlari.Ayrildi, guest.KatilimDurumu);
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

    // Check-out oncesi bekleyen konaklayanlar Geldi veya Gelmedi olarak netlestirilmis olmali.
    [Fact]
    public async Task CheckOut_BekleyenMisafirVarkenHataVerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 9972, segmentId: 9973, withPlan: true);

        var reservation = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 9972);
        reservation.KisiSayisi = 2;
        dbContext.RezervasyonKonaklayanlar.Add(new RezervasyonKonaklayan
        {
            Id = 10972,
            RezervasyonId = 9972,
            SiraNo = 2,
            AdSoyad = "Bekleyen Misafir",
            KatilimDurumu = KonaklayanKatilimDurumlari.Bekleniyor
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        await service.TamamlaCheckInAsync(9972);
        await service.KaydetOdemeAsync(9972, new RezervasyonOdemeKaydetRequestDto
        {
            OdemeTutari = 1000m,
            OdemeTipi = OdemeTipleri.Nakit
        });

        var exception = await Assert.ThrowsAsync<BaseException>(() => service.TamamlaCheckOutAsync(9972));

        Assert.Equal(400, exception.ErrorCode);
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

    // Odeme alinmis rezervasyon dogrudan iptal edilememeli; once iade/mahsup akisi tamamlanmali.
    [Fact]
    public async Task IptalEt_OdemeAlinmisRezervasyondaHataVerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 9981, segmentId: 9982, withPlan: true);
        var service = CreateService(dbContext);
        await service.TamamlaCheckInAsync(9981);

        await service.KaydetOdemeAsync(9981, new RezervasyonOdemeKaydetRequestDto
        {
            OdemeTutari = 100m,
            OdemeTipi = OdemeTipleri.Nakit
        });

        var exception = await Assert.ThrowsAsync<BaseException>(() => service.IptalEtAsync(9981));

        Assert.Equal(400, exception.ErrorCode);
        var updated = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 9981);
        Assert.Equal(RezervasyonDurumlari.Onayli, updated.RezervasyonDurumu);
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

    // Check-in tamamlanmadan rezervasyona odeme eklenememeli.
    [Fact]
    public async Task KaydetOdeme_CheckInOncesiHataVerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 1002, segmentId: 1003, withPlan: true);
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<BaseException>(() => service.KaydetOdemeAsync(1002, new RezervasyonOdemeKaydetRequestDto
        {
            OdemeTutari = 200m,
            OdemeTipi = OdemeTipleri.KrediKarti
        }));

        Assert.Equal(400, exception.ErrorCode);
    }

    // Check-in tamamlanmadan rezervasyona ek hizmet eklenememeli.
    [Fact]
    public async Task KaydetEkHizmet_CheckInOncesiHataVerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 1008, segmentId: 1009, withPlan: true);
        await SeedEkHizmetTarifesiAsync(dbContext, tarifeId: 8008, tesisId: 1, birimFiyat: 75m);
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<BaseException>(() => service.KaydetEkHizmetAsync(1008, new RezervasyonEkHizmetKaydetRequestDto
        {
            RezervasyonKonaklayanId = 2008,
            EkHizmetTarifeId = 8008,
            HizmetTarihi = new DateTime(2026, 3, 8, 18, 0, 0),
            Miktar = 1
        }));

        Assert.Equal(400, exception.ErrorCode);
    }

    // Ek hizmet seceneklerinde yalnizca fiilen gelen konaklayanlar donmeli.
    [Fact]
    public async Task GetEkHizmetSecenekleri_SadeceGelenMisafirleriDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 10081, segmentId: 10082, withPlan: true);
        var reservation = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 10081);
        reservation.KisiSayisi = 2;
        dbContext.RezervasyonKonaklayanlar.Add(new RezervasyonKonaklayan
        {
            Id = 12081,
            RezervasyonId = 10081,
            SiraNo = 2,
            AdSoyad = "Gelmeyen Misafir",
            KatilimDurumu = KonaklayanKatilimDurumlari.Gelmedi
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetEkHizmetSecenekleriAsync(10081);

        var guest = Assert.Single(result.Misafirler);
        Assert.Equal("Ali Check", guest.AdSoyad);
    }

    // Check-in tamamlanmis rezervasyonda bekleyen hak kullanildi olarak isaretlenebilmeli.
    [Fact]
    public async Task GuncelleKonaklamaHakkiDurumu_CheckInSonrasiBekliyorHakkiKullanildiYapar()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 10083, segmentId: 10084, withPlan: true);
        dbContext.RezervasyonKonaklamaHaklari.Add(new RezervasyonKonaklamaHakki
        {
            Id = 13083,
            RezervasyonId = 10083,
            HizmetKodu = "Kahvalti",
            HizmetAdiSnapshot = "Kahvaltı",
            Miktar = 1,
            Periyot = "Gunluk",
            PeriyotAdiSnapshot = "Günlük",
            HakTarihi = new DateTime(2026, 3, 8),
            Durum = RezervasyonKonaklamaHakDurumlari.Bekliyor
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        await service.TamamlaCheckInAsync(10083);

        var detay = await service.GuncelleKonaklamaHakkiDurumuAsync(10083, 13083, new RezervasyonKonaklamaHakkiDurumGuncelleRequestDto
        {
            Durum = RezervasyonKonaklamaHakDurumlari.Kullanildi
        });

        var hak = await dbContext.RezervasyonKonaklamaHaklari.SingleAsync(x => x.Id == 13083);
        Assert.Equal(RezervasyonKonaklamaHakDurumlari.Kullanildi, hak.Durum);
        Assert.Contains(detay.KonaklamaHaklari, x => x.Id == 13083 && x.Durum == RezervasyonKonaklamaHakDurumlari.Kullanildi);
    }

    // Check-in tamamlanmadan konaklama hakki durumu manuel olarak degistirilememeli.
    [Fact]
    public async Task GuncelleKonaklamaHakkiDurumu_CheckInOncesiHataVerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 10085, segmentId: 10086, withPlan: true);
        dbContext.RezervasyonKonaklamaHaklari.Add(new RezervasyonKonaklamaHakki
        {
            Id = 13085,
            RezervasyonId = 10085,
            HizmetKodu = "Kahvalti",
            HizmetAdiSnapshot = "Kahvaltı",
            Miktar = 1,
            Periyot = "Gunluk",
            PeriyotAdiSnapshot = "Günlük",
            HakTarihi = new DateTime(2026, 3, 8),
            Durum = RezervasyonKonaklamaHakDurumlari.Bekliyor
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<BaseException>(() => service.GuncelleKonaklamaHakkiDurumuAsync(10085, 13085, new RezervasyonKonaklamaHakkiDurumGuncelleRequestDto
        {
            Durum = RezervasyonKonaklamaHakDurumlari.Kullanildi
        }));

        Assert.Equal(400, exception.ErrorCode);
        var hak = await dbContext.RezervasyonKonaklamaHaklari.SingleAsync(x => x.Id == 13085);
        Assert.Equal(RezervasyonKonaklamaHakDurumlari.Bekliyor, hak.Durum);
    }

    // Adetli hakta tuketim kaydi miktari doldugunda hak kullanildi olur ve log olusur.
    [Fact]
    public async Task KaydetKonaklamaHakkiTuketim_AdetliHakIcinKayitOlusturur()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 10087, segmentId: 10088, withPlan: true);
        var restoranId = await SeedIsletmeAlaniAsync(dbContext, binaId: 10, sinifId: 9001, alanId: 9002, sinifKod: "RESTORAN", sinifAd: "Restoran", ozelAd: "Ana Restoran");
        dbContext.RezervasyonKonaklamaHaklari.Add(new RezervasyonKonaklamaHakki
        {
            Id = 13087,
            RezervasyonId = 10087,
            HizmetKodu = "Kahvalti",
            HizmetAdiSnapshot = "Kahvaltı",
            Miktar = 1,
            Periyot = KonaklamaTipiIcerikPeriyotlari.Gunluk,
            PeriyotAdiSnapshot = "Günlük",
            KullanimTipi = KonaklamaTipiIcerikKullanimTipleri.Adetli,
            KullanimTipiAdiSnapshot = "Adetli",
            KullanimNoktasi = KonaklamaTipiIcerikKullanimNoktalari.Restoran,
            KullanimNoktasiAdiSnapshot = "Restoran",
            KullanimBaslangicSaati = new TimeSpan(7, 0, 0),
            KullanimBitisSaati = new TimeSpan(10, 0, 0),
            HakTarihi = new DateTime(2026, 3, 8),
            Durum = RezervasyonKonaklamaHakDurumlari.Bekliyor
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        await service.TamamlaCheckInAsync(10087);

        var detay = await service.KaydetKonaklamaHakkiTuketimAsync(10087, 13087, new RezervasyonKonaklamaHakkiTuketimKaydiKaydetRequestDto
        {
            TuketimTarihi = new DateTime(2026, 3, 8, 8, 15, 0),
            Miktar = 1,
            IsletmeAlaniId = restoranId,
            Aciklama = "Sabah servisi"
        });

        var hak = await dbContext.RezervasyonKonaklamaHaklari.SingleAsync(x => x.Id == 13087);
        var kayit = await dbContext.RezervasyonKonaklamaHakkiTuketimKayitlari.SingleAsync(x => x.RezervasyonKonaklamaHakkiId == 13087);
        Assert.Equal(RezervasyonKonaklamaHakDurumlari.Kullanildi, hak.Durum);
        Assert.Equal(restoranId, kayit.IsletmeAlaniId);
        Assert.Equal("Ana Restoran", kayit.TuketimNoktasiAdi);
        Assert.Contains(detay.KonaklamaHaklari, x => x.Id == 13087 && x.TuketilenMiktar == 1 && x.KalanMiktar == 0);
    }

    // Saat penceresi disinda tuketim kaydi eklenememeli.
    [Fact]
    public async Task KaydetKonaklamaHakkiTuketim_SaatPenceresiDisindaHataVerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 10089, segmentId: 10090, withPlan: true);
        var restoranId = await SeedIsletmeAlaniAsync(dbContext, binaId: 10, sinifId: 9011, alanId: 9012, sinifKod: "RESTORAN", sinifAd: "Restoran", ozelAd: "Ana Restoran");
        dbContext.RezervasyonKonaklamaHaklari.Add(new RezervasyonKonaklamaHakki
        {
            Id = 13089,
            RezervasyonId = 10089,
            HizmetKodu = "Kahvalti",
            HizmetAdiSnapshot = "Kahvaltı",
            Miktar = 1,
            Periyot = KonaklamaTipiIcerikPeriyotlari.Gunluk,
            PeriyotAdiSnapshot = "Günlük",
            KullanimTipi = KonaklamaTipiIcerikKullanimTipleri.Adetli,
            KullanimTipiAdiSnapshot = "Adetli",
            KullanimNoktasi = KonaklamaTipiIcerikKullanimNoktalari.Restoran,
            KullanimNoktasiAdiSnapshot = "Restoran",
            KullanimBaslangicSaati = new TimeSpan(7, 0, 0),
            KullanimBitisSaati = new TimeSpan(10, 0, 0),
            HakTarihi = new DateTime(2026, 3, 8),
            Durum = RezervasyonKonaklamaHakDurumlari.Bekliyor
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        await service.TamamlaCheckInAsync(10089);

        var exception = await Assert.ThrowsAsync<BaseException>(() => service.KaydetKonaklamaHakkiTuketimAsync(10089, 13089, new RezervasyonKonaklamaHakkiTuketimKaydiKaydetRequestDto
        {
            TuketimTarihi = new DateTime(2026, 3, 8, 11, 0, 0),
            Miktar = 1,
            IsletmeAlaniId = restoranId
        }));

        Assert.Equal(400, exception.ErrorCode);
        Assert.False(await dbContext.RezervasyonKonaklamaHakkiTuketimKayitlari.AnyAsync(x => x.RezervasyonKonaklamaHakkiId == 13089));
    }

    // Tuketim kaydi silinince adetli hak tekrar bekliyor durumuna donebilmeli.
    [Fact]
    public async Task SilKonaklamaHakkiTuketim_KaydiGeriAlir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 10091, segmentId: 10092, withPlan: true);
        var restoranId = await SeedIsletmeAlaniAsync(dbContext, binaId: 10, sinifId: 9021, alanId: 9022, sinifKod: "RESTORAN", sinifAd: "Restoran", ozelAd: "Ana Restoran");
        dbContext.RezervasyonKonaklamaHaklari.Add(new RezervasyonKonaklamaHakki
        {
            Id = 13091,
            RezervasyonId = 10091,
            HizmetKodu = "Kahvalti",
            HizmetAdiSnapshot = "Kahvaltı",
            Miktar = 1,
            Periyot = KonaklamaTipiIcerikPeriyotlari.Gunluk,
            PeriyotAdiSnapshot = "Günlük",
            KullanimTipi = KonaklamaTipiIcerikKullanimTipleri.Adetli,
            KullanimTipiAdiSnapshot = "Adetli",
            KullanimNoktasi = KonaklamaTipiIcerikKullanimNoktalari.Restoran,
            KullanimNoktasiAdiSnapshot = "Restoran",
            HakTarihi = new DateTime(2026, 3, 8),
            Durum = RezervasyonKonaklamaHakDurumlari.Bekliyor
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        await service.TamamlaCheckInAsync(10091);
        await service.KaydetKonaklamaHakkiTuketimAsync(10091, 13091, new RezervasyonKonaklamaHakkiTuketimKaydiKaydetRequestDto
        {
            TuketimTarihi = new DateTime(2026, 3, 8, 8, 0, 0),
            Miktar = 1,
            IsletmeAlaniId = restoranId
        });

        var kayit = await dbContext.RezervasyonKonaklamaHakkiTuketimKayitlari.SingleAsync(x => x.RezervasyonKonaklamaHakkiId == 13091 && !x.IsDeleted);
        var detay = await service.SilKonaklamaHakkiTuketimAsync(10091, 13091, kayit.Id);

        var hak = await dbContext.RezervasyonKonaklamaHaklari.SingleAsync(x => x.Id == 13091);
        Assert.Equal(RezervasyonKonaklamaHakDurumlari.Bekliyor, hak.Durum);
        Assert.Contains(detay.KonaklamaHaklari, x => x.Id == 13091 && x.TuketilenMiktar == 0 && x.KalanMiktar == 1);
    }

    // Konaklayan plana bagli ek hizmet eklendiginde odeme ozetindeki ek hizmet ve toplam tutarlar artmali.
    [Fact]
    public async Task KaydetEkHizmet_OdemeOzetineEklenir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 1010, segmentId: 1011, withPlan: true);
        await SeedEkHizmetTarifesiAsync(dbContext, tarifeId: 8010, tesisId: 1, birimFiyat: 150m);
        var service = CreateService(dbContext);
        await service.TamamlaCheckInAsync(1010);

        var ozet = await service.KaydetEkHizmetAsync(1010, new RezervasyonEkHizmetKaydetRequestDto
        {
            RezervasyonKonaklayanId = 2010,
            EkHizmetTarifeId = 8010,
            HizmetTarihi = new DateTime(2026, 3, 8, 18, 0, 0),
            Miktar = 2,
            Aciklama = "Aksam servisi"
        });

        Assert.Equal(1000m, ozet.KonaklamaUcreti);
        Assert.Equal(300m, ozet.EkHizmetToplami);
        Assert.Equal(1300m, ozet.ToplamUcret);
        var hizmet = Assert.Single(ozet.EkHizmetler);
        Assert.Equal(8010, hizmet.EkHizmetTarifeId);
        Assert.Equal(2010, hizmet.RezervasyonKonaklayanId);
        Assert.Equal(300m, hizmet.ToplamTutar);
        Assert.Equal("A-102", hizmet.OdaNo);
    }

    // Ek hizmet kaydedilirken tarife varsayilan fiyati yerine kullanicinin girdigi birim fiyat saklanabilmeli.
    [Fact]
    public async Task KaydetEkHizmet_OzelBirimFiyatlaKaydeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 1011, segmentId: 1012, withPlan: true);
        await SeedEkHizmetTarifesiAsync(dbContext, tarifeId: 8011, tesisId: 1, birimFiyat: 150m, ad: "Ayakkabi Boyama");
        var service = CreateService(dbContext);
        await service.TamamlaCheckInAsync(1011);

        var ozet = await service.KaydetEkHizmetAsync(1011, new RezervasyonEkHizmetKaydetRequestDto
        {
            RezervasyonKonaklayanId = 2011,
            EkHizmetTarifeId = 8011,
            HizmetTarihi = new DateTime(2026, 3, 8, 18, 15, 0),
            Miktar = 2,
            BirimFiyat = 125m,
            Aciklama = "Ozel fiyat"
        });

        var hizmet = Assert.Single(ozet.EkHizmetler);
        Assert.Equal(125m, hizmet.BirimFiyat);
        Assert.Equal(250m, hizmet.ToplamTutar);
        Assert.Equal(250m, ozet.EkHizmetToplami);
        Assert.Equal(1250m, ozet.ToplamUcret);
    }

    // Ek hizmet guncelleme sonrasinda miktar/tutar ve secilen tarife bilgisi yeni degerlerle donmeli.
    [Fact]
    public async Task GuncelleEkHizmet_TutariVeIcerigiYeniler()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 1012, segmentId: 1013, withPlan: true);
        await SeedEkHizmetTarifesiAsync(dbContext, tarifeId: 8012, tesisId: 1, birimFiyat: 120m, ad: "Kurutemizleme");
        await SeedEkHizmetTarifesiAsync(dbContext, tarifeId: 8013, tesisId: 1, birimFiyat: 250m, ad: "Odaya Kahvalti");
        var service = CreateService(dbContext);
        await service.TamamlaCheckInAsync(1012);

        var ilkOzet = await service.KaydetEkHizmetAsync(1012, new RezervasyonEkHizmetKaydetRequestDto
        {
            RezervasyonKonaklayanId = 2012,
            EkHizmetTarifeId = 8012,
            HizmetTarihi = new DateTime(2026, 3, 8, 17, 0, 0),
            Miktar = 1,
            Aciklama = "Ilk kayit"
        });

        var ilkKayit = Assert.Single(ilkOzet.EkHizmetler);

        var guncelOzet = await service.GuncelleEkHizmetAsync(1012, ilkKayit.Id, new RezervasyonEkHizmetKaydetRequestDto
        {
            RezervasyonKonaklayanId = 2012,
            EkHizmetTarifeId = 8013,
            HizmetTarihi = new DateTime(2026, 3, 8, 19, 30, 0),
            Miktar = 3,
            Aciklama = "Guncel kayit"
        });

        Assert.Equal(750m, guncelOzet.EkHizmetToplami);
        Assert.Equal(1750m, guncelOzet.ToplamUcret);
        var hizmet = Assert.Single(guncelOzet.EkHizmetler);
        Assert.Equal(8013, hizmet.EkHizmetTarifeId);
        Assert.Equal("Odaya Kahvalti", hizmet.TarifeAdi);
        Assert.Equal(3, hizmet.Miktar);
        Assert.Equal(750m, hizmet.ToplamTutar);
        Assert.Equal("Guncel kayit", hizmet.Aciklama);
    }

    // Ek hizmet guncellenirken kullanici birim fiyati override ederse toplam bu yeni birim fiyata gore hesaplanmali.
    [Fact]
    public async Task GuncelleEkHizmet_OzelBirimFiyatiGunceller()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 1013, segmentId: 1014, withPlan: true);
        await SeedEkHizmetTarifesiAsync(dbContext, tarifeId: 8015, tesisId: 1, birimFiyat: 180m, ad: "Transfer");
        var service = CreateService(dbContext);
        await service.TamamlaCheckInAsync(1013);

        var ilkOzet = await service.KaydetEkHizmetAsync(1013, new RezervasyonEkHizmetKaydetRequestDto
        {
            RezervasyonKonaklayanId = 2013,
            EkHizmetTarifeId = 8015,
            HizmetTarihi = new DateTime(2026, 3, 8, 12, 0, 0),
            Miktar = 1
        });

        var ilkKayit = Assert.Single(ilkOzet.EkHizmetler);

        var guncelOzet = await service.GuncelleEkHizmetAsync(1013, ilkKayit.Id, new RezervasyonEkHizmetKaydetRequestDto
        {
            RezervasyonKonaklayanId = 2013,
            EkHizmetTarifeId = 8015,
            HizmetTarihi = new DateTime(2026, 3, 8, 13, 0, 0),
            Miktar = 2,
            BirimFiyat = 95m,
            Aciklama = "Ozel kampanya"
        });

        var hizmet = Assert.Single(guncelOzet.EkHizmetler);
        Assert.Equal(95m, hizmet.BirimFiyat);
        Assert.Equal(190m, hizmet.ToplamTutar);
        Assert.Equal(190m, guncelOzet.EkHizmetToplami);
        Assert.Equal("Ozel kampanya", hizmet.Aciklama);
    }

    // Ek hizmet silinince o kalem toplamdan dusmeli ve ek hizmet listesi bosalmali.
    [Fact]
    public async Task SilEkHizmet_OdemeOzetindenDusurur()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 1014, segmentId: 1015, withPlan: true);
        await SeedEkHizmetTarifesiAsync(dbContext, tarifeId: 8014, tesisId: 1, birimFiyat: 90m, ad: "Ayakkabi Boyama");
        var service = CreateService(dbContext);
        await service.TamamlaCheckInAsync(1014);

        var ilkOzet = await service.KaydetEkHizmetAsync(1014, new RezervasyonEkHizmetKaydetRequestDto
        {
            RezervasyonKonaklayanId = 2014,
            EkHizmetTarifeId = 8014,
            HizmetTarihi = new DateTime(2026, 3, 8, 16, 0, 0),
            Miktar = 2,
            Aciklama = null
        });

        var hizmet = Assert.Single(ilkOzet.EkHizmetler);
        var silinmisOzet = await service.SilEkHizmetAsync(1014, hizmet.Id);

        Assert.Equal(0m, silinmisOzet.EkHizmetToplami);
        Assert.Equal(1000m, silinmisOzet.ToplamUcret);
        Assert.Empty(silinmisOzet.EkHizmetler);
    }

    // Check-out tamamlanana kadar rezervasyona yeni ek hizmet alinabilmeli; odeme alinmis olmasi bunu engellememeli.
    [Fact]
    public async Task EkHizmet_OdemeVarkenBile_CheckOutaKadarEklenebilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 1016, segmentId: 1017, withPlan: true);
        await SeedEkHizmetTarifesiAsync(dbContext, tarifeId: 8016, tesisId: 1, birimFiyat: 110m, ad: "Mini Bar");
        var service = CreateService(dbContext);
        await service.TamamlaCheckInAsync(1016);

        var ilkOzet = await service.KaydetEkHizmetAsync(1016, new RezervasyonEkHizmetKaydetRequestDto
        {
            RezervasyonKonaklayanId = 2016,
            EkHizmetTarifeId = 8016,
            HizmetTarihi = new DateTime(2026, 3, 8, 20, 0, 0),
            Miktar = 1,
            Aciklama = null
        });

        var hizmet = Assert.Single(ilkOzet.EkHizmetler);
        await service.KaydetOdemeAsync(1016, new RezervasyonOdemeKaydetRequestDto
        {
            OdemeTutari = 100m,
            OdemeTipi = OdemeTipleri.Nakit
        });

        var ikinciOzet = await service.KaydetEkHizmetAsync(1016, new RezervasyonEkHizmetKaydetRequestDto
        {
            RezervasyonKonaklayanId = 2016,
            EkHizmetTarifeId = 8016,
            HizmetTarihi = new DateTime(2026, 3, 8, 21, 0, 0),
            Miktar = 2,
            Aciklama = "Ikinci hizmet"
        });

        Assert.Equal(330m, ikinciOzet.EkHizmetToplami);
        Assert.Equal(1330m, ikinciOzet.ToplamUcret);
        Assert.Equal(2, ikinciOzet.EkHizmetler.Count);
    }

    // Kalan bakiye sifirsa ek hizmet silinmemeli; aksi halde rezervasyon fazla odenmis duruma duser.
    [Fact]
    public async Task EkHizmet_Silme_KalanBakiyeSifirkenEngellenir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 1018, segmentId: 1019, withPlan: true);
        await SeedEkHizmetTarifesiAsync(dbContext, tarifeId: 8018, tesisId: 1, birimFiyat: 200m, ad: "Transfer");
        var service = CreateService(dbContext);
        await service.TamamlaCheckInAsync(1018);

        var ozet = await service.KaydetEkHizmetAsync(1018, new RezervasyonEkHizmetKaydetRequestDto
        {
            RezervasyonKonaklayanId = 2018,
            EkHizmetTarifeId = 8018,
            HizmetTarihi = new DateTime(2026, 3, 8, 18, 0, 0),
            Miktar = 1,
            Aciklama = null
        });

        var hizmet = Assert.Single(ozet.EkHizmetler);
        await service.KaydetOdemeAsync(1018, new RezervasyonOdemeKaydetRequestDto
        {
            OdemeTutari = 1200m,
            OdemeTipi = OdemeTipleri.Nakit
        });

        var silEx = await Assert.ThrowsAsync<BaseException>(() => service.SilEkHizmetAsync(1018, hizmet.Id));
        Assert.Equal(400, silEx.ErrorCode);
    }

    // Ek hizmet silinince odenmis tutar yeni toplamdan buyuk kalacaksa silme engellenmeli.
    [Fact]
    public async Task EkHizmet_Silme_OdenmisTutarYeniToplamiAsarsaEngellenir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 1020, segmentId: 1021, withPlan: true);
        await SeedEkHizmetTarifesiAsync(dbContext, tarifeId: 8020, tesisId: 1, birimFiyat: 300m, ad: "Vip Servis");
        var service = CreateService(dbContext);
        await service.TamamlaCheckInAsync(1020);

        var ozet = await service.KaydetEkHizmetAsync(1020, new RezervasyonEkHizmetKaydetRequestDto
        {
            RezervasyonKonaklayanId = 2020,
            EkHizmetTarifeId = 8020,
            HizmetTarihi = new DateTime(2026, 3, 8, 18, 0, 0),
            Miktar = 1,
            Aciklama = null
        });

        var hizmet = Assert.Single(ozet.EkHizmetler);
        await service.KaydetOdemeAsync(1020, new RezervasyonOdemeKaydetRequestDto
        {
            OdemeTutari = 1100m,
            OdemeTipi = OdemeTipleri.Nakit
        });

        var silEx = await Assert.ThrowsAsync<BaseException>(() => service.SilEkHizmetAsync(1020, hizmet.Id));
        Assert.Equal(400, silEx.ErrorCode);
    }

    // Ek hizmet tutari dusurulurse ve yeni toplam odenmis tutarin altina inerse guncelleme engellenmeli.
    [Fact]
    public async Task EkHizmet_Guncelleme_OdenmisTutarYeniToplamiAsarsaEngellenir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 1022, segmentId: 1023, withPlan: true);
        await SeedEkHizmetTarifesiAsync(dbContext, tarifeId: 8022, tesisId: 1, birimFiyat: 300m, ad: "Laundry");
        var service = CreateService(dbContext);
        await service.TamamlaCheckInAsync(1022);

        var ozet = await service.KaydetEkHizmetAsync(1022, new RezervasyonEkHizmetKaydetRequestDto
        {
            RezervasyonKonaklayanId = 2022,
            EkHizmetTarifeId = 8022,
            HizmetTarihi = new DateTime(2026, 3, 8, 18, 0, 0),
            Miktar = 1,
            Aciklama = null
        });

        var hizmet = Assert.Single(ozet.EkHizmetler);
        await service.KaydetOdemeAsync(1022, new RezervasyonOdemeKaydetRequestDto
        {
            OdemeTutari = 1200m,
            OdemeTipi = OdemeTipleri.Nakit
        });

        var guncelleEx = await Assert.ThrowsAsync<BaseException>(() => service.GuncelleEkHizmetAsync(1022, hizmet.Id, new RezervasyonEkHizmetKaydetRequestDto
        {
            RezervasyonKonaklayanId = 2022,
            EkHizmetTarifeId = 8022,
            HizmetTarihi = new DateTime(2026, 3, 8, 18, 30, 0),
            Miktar = 0.5m,
            Aciklama = "Dusur"
        }));

        Assert.Equal(400, guncelleEx.ErrorCode);
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
        bool withPlan,
        string? konaklayanCinsiyet = null)
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
                PasaportNo = null,
                Cinsiyet = konaklayanCinsiyet,
                KatilimDurumu = KonaklayanKatilimDurumlari.Geldi
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

    private static async Task<int> SeedIsletmeAlaniAsync(
        StysAppDbContext dbContext,
        int binaId,
        int sinifId,
        int alanId,
        string sinifKod,
        string sinifAd,
        string? ozelAd = null)
    {
        if (!await dbContext.IsletmeAlaniSiniflari.AnyAsync(x => x.Id == sinifId))
        {
            dbContext.IsletmeAlaniSiniflari.Add(new IsletmeAlaniSinifi
            {
                Id = sinifId,
                Kod = sinifKod,
                Ad = sinifAd,
                AktifMi = true
            });
        }

        if (!await dbContext.IsletmeAlanlari.AnyAsync(x => x.Id == alanId))
        {
            dbContext.IsletmeAlanlari.Add(new IsletmeAlani
            {
                Id = alanId,
                BinaId = binaId,
                IsletmeAlaniSinifiId = sinifId,
                OzelAd = ozelAd,
                AktifMi = true
            });
        }

        await dbContext.SaveChangesAsync();
        return alanId;
    }

    private static async Task SeedRoomBlockForReservationAsync(
        StysAppDbContext dbContext,
        int rezervasyonId,
        int segmentId,
        int odaId)
    {
        var segment = await dbContext.RezervasyonSegmentleri.SingleAsync(x => x.Id == segmentId && x.RezervasyonId == rezervasyonId);
        dbContext.OdaKullanimBloklari.Add(new OdaKullanimBlok
        {
            Id = rezervasyonId + 5000,
            TesisId = 1,
            OdaId = odaId,
            BlokTipi = OdaKullanimBlokTipleri.Ariza,
            BaslangicTarihi = segment.BaslangicTarihi.AddHours(-1),
            BitisTarihi = segment.BitisTarihi.AddHours(1),
            Aciklama = "Test blokaji",
            AktifMi = true
        });

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
            new FakeBildirimService(),
            httpContextAccessor);
    }

    private static RezervasyonKaydetRequestDto BuildCustomDiscountSaveRequest()
    {
        return new RezervasyonKaydetRequestDto
        {
            TesisId = 1,
            KisiSayisi = 1,
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
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
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
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

    private static async Task SeedSingleSharedRoomScenarioFixtureAsync(StysAppDbContext dbContext)
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
            KatSayisi = 3,
            AktifMi = true
        });

        dbContext.OdaTipleri.Add(new OdaTipi
        {
            Id = 20,
            TesisId = 1,
            OdaSinifiId = 1,
            Ad = "Hostel 2",
            Kapasite = 2,
            PaylasimliMi = true,
            AktifMi = true
        });

        dbContext.Odalar.Add(new Oda
        {
            Id = 100,
            OdaNo = "PAY-1",
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
            Fiyat = 400m,
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

    private static async Task SeedSharedRoomReservationWithGuestAsync(
        StysAppDbContext dbContext,
        int rezervasyonId,
        int segmentId,
        int odaAtamaId,
        int konaklayanId,
        int konaklayanAtamaId,
        int odaId,
        string cinsiyet,
        int yatakNo)
    {
        dbContext.Rezervasyonlar.Add(new Rezervasyon
        {
            Id = rezervasyonId,
            ReferansNo = $"TEST-RZV-{rezervasyonId}",
            TesisId = 1,
            KisiSayisi = 1,
            GirisTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            CikisTarihi = new DateTime(2026, 3, 9, 10, 0, 0),
            MisafirAdiSoyadi = "Paylasimli Test",
            MisafirTelefon = "000",
            ToplamBazUcret = 500m,
            ToplamUcret = 500m,
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
            Id = odaAtamaId,
            RezervasyonSegmentId = segmentId,
            OdaId = odaId,
            AyrilanKisiSayisi = 1,
            OdaNoSnapshot = "B-201",
            BinaAdiSnapshot = "B Blok",
            OdaTipiAdiSnapshot = "Hostel 2",
            PaylasimliMiSnapshot = true,
            KapasiteSnapshot = 2
        });

        dbContext.RezervasyonKonaklayanlar.Add(new RezervasyonKonaklayan
        {
            Id = konaklayanId,
            RezervasyonId = rezervasyonId,
            SiraNo = 1,
            AdSoyad = "Mevcut Konaklayan",
            Cinsiyet = cinsiyet
        });

        dbContext.RezervasyonKonaklayanSegmentAtamalari.Add(new RezervasyonKonaklayanSegmentAtama
        {
            Id = konaklayanAtamaId,
            RezervasyonKonaklayanId = konaklayanId,
            RezervasyonSegmentId = segmentId,
            OdaId = odaId,
            YatakNo = yatakNo
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

    private static async Task SeedEkHizmetTarifesiAsync(
        StysAppDbContext dbContext,
        int tarifeId,
        int tesisId,
        decimal birimFiyat,
        string ad = "Ek Hizmet")
    {
        var ekHizmetId = tarifeId + 100000;

        dbContext.EkHizmetler.Add(new EkHizmet
        {
            Id = ekHizmetId,
            TesisId = tesisId,
            Ad = ad,
            BirimAdi = "Adet",
            AktifMi = true
        });

        dbContext.EkHizmetTarifeleri.Add(new EkHizmetTarife
        {
            Id = tarifeId,
            TesisId = tesisId,
            EkHizmetId = ekHizmetId,
            BirimFiyat = birimFiyat,
            ParaBirimi = "TRY",
            BaslangicTarihi = new DateTime(2026, 3, 1),
            BitisTarihi = new DateTime(2026, 3, 31),
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

    private sealed class FakeBildirimService : IBildirimService
    {
        public Task<List<BildirimDto>> GetCurrentUserBildirimlerAsync(int take = 20, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<BildirimDto>());

        public Task<int> GetCurrentUserUnreadCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<BildirimTercihDto> GetCurrentUserTercihAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new BildirimTercihDto());

        public Task<BildirimTercihDto> UpdateCurrentUserTercihAsync(BildirimTercihGuncelleRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(new BildirimTercihDto());

        public Task MarkAsReadAsync(int bildirimId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarkAllAsReadAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task PublishToTesisUsersAsync(int tesisId, BildirimOlusturRequestDto request, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task PublishToUsersAsync(IEnumerable<Guid> userIds, BildirimOlusturRequestDto request, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
