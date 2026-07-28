using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
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
using STYS.Kurumlar.Entities;
using STYS.MisafirTipleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
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
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Licensing.Abstractions;
using TOD.Platform.Security.Auth.Services;
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

    // TRT Trabzon'da oda kapasitesi tam doldugunda kisi basi tarifeye donulmeli.
    [Fact]
    public async Task SenaryoUretimi_TrtTrabzonOdaTamDoluysaKisiBasiFiyatUygular()
    {
        await using var dbContext = CreateDbContext();
        await SeedTrtTrabzonFixtureAsync(dbContext);

        var service = CreateService(dbContext);
        var scenarios = await service.GetKonaklamaSenaryolariAsync(new KonaklamaSenaryoAramaRequestDto
        {
            TesisId = 1001,
            OdaTipiId = 21,
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
            KisiSayisi = 2,
            BaslangicTarihi = new DateTime(2026, 6, 18, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 6, 19, 10, 0, 0),
            TekKisilikFiyatUygulansinMi = false,
            KonaklayanCinsiyetleri = [KonaklayanCinsiyetleri.Kadin, KonaklayanCinsiyetleri.Erkek]
        });

        var scenario = Assert.Single(scenarios);
        Assert.Single(scenario.Segmentler);
        Assert.Equal(2400m, scenario.ToplamBazUcret);
        Assert.Equal(2400m, scenario.ToplamNihaiUcret);
    }

    // TRT Trabzon'da oda kapasitesi dolmadiysa ozel kullanim gunluk bedeli uygulanmali.
    [Fact]
    public async Task SenaryoUretimi_TrtTrabzonOdaKapasitesiDolmadiysaOzelKullanimUygular()
    {
        await using var dbContext = CreateDbContext();
        await SeedTrtTrabzonFixtureAsync(dbContext);

        var service = CreateService(dbContext);
        var scenarios = await service.GetKonaklamaSenaryolariAsync(new KonaklamaSenaryoAramaRequestDto
        {
            TesisId = 1001,
            OdaTipiId = 21,
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
            KisiSayisi = 1,
            BaslangicTarihi = new DateTime(2026, 6, 18, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 6, 19, 10, 0, 0),
            TekKisilikFiyatUygulansinMi = false
        });

        var scenario = Assert.Single(scenarios);
        Assert.Single(scenario.Segmentler);
        Assert.Equal("OzelKullanim", scenario.FiyatlamaTipi);
        Assert.Equal(1500m, scenario.ToplamBazUcret);
        Assert.Equal(1500m, scenario.ToplamNihaiUcret);
    }

    // Ozel kullanim tarifi olmayan 3 kisilik oda, kapasite dolmadiysa kisi basi gibi etiketlenmeli.
    [Fact]
    public async Task SenaryoUretimi_TrtTrabzonUcYatakliOdaOzelKullanimFiyatiYoksaKisiBasiEtiketlenir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTrtTrabzonFixtureAsync(dbContext);

        var service = CreateService(dbContext);
        var scenarios = await service.GetKonaklamaSenaryolariAsync(new KonaklamaSenaryoAramaRequestDto
        {
            TesisId = 1001,
            OdaTipiId = 23,
            MisafirTipiId = 2,
            KonaklamaTipiId = 1,
            KisiSayisi = 1,
            BaslangicTarihi = new DateTime(2026, 6, 18, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 6, 19, 10, 0, 0),
            TekKisilikFiyatUygulansinMi = false,
            KonaklayanCinsiyetleri = [KonaklayanCinsiyetleri.Kadin]
        });

        var scenario = Assert.Single(scenarios);
        Assert.Equal("KisiBasi", scenario.FiyatlamaTipi);
        Assert.Equal(1200m, scenario.ToplamBazUcret);
        Assert.Equal(1200m, scenario.ToplamNihaiUcret);
    }

    // Tek kisilik fiyat secimi coklu konaklayan icin de uygulanabilmeli.
    [Fact]
    public async Task SenaryoUretimi_TrtTrabzonTekKisilikFiyatCokluKisiIcinDeUygulanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTrtTrabzonFixtureAsync(dbContext);

        var service = CreateService(dbContext);
        var scenarios = await service.GetKonaklamaSenaryolariAsync(new KonaklamaSenaryoAramaRequestDto
        {
            TesisId = 1001,
            OdaTipiId = 23,
            MisafirTipiId = 2,
            KonaklamaTipiId = 1,
            KisiSayisi = 2,
            BaslangicTarihi = new DateTime(2026, 6, 18, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 6, 19, 10, 0, 0),
            TekKisilikFiyatUygulansinMi = true,
            KonaklayanCinsiyetleri = [KonaklayanCinsiyetleri.Kadin, KonaklayanCinsiyetleri.Erkek]
        });

        var scenario = Assert.Single(scenarios);
        Assert.Equal("TekKisilikFiyat", scenario.FiyatlamaTipi);
        Assert.Equal(2400m, scenario.ToplamBazUcret);
        Assert.Equal(2400m, scenario.ToplamNihaiUcret);
    }

    // Oda tipi secilmediginde uygun oda tipleri icin birden fazla alternatif donmeli.
    [Fact]
    public async Task SenaryoUretimi_TrtTrabzonOdaTipiSecilmedigindeUygunAlternatiflerDondurur()
    {
        await using var dbContext = CreateDbContext();
        await SeedTrtTrabzonFixtureAsync(dbContext);

        var service = CreateService(dbContext);
        var scenarios = await service.GetKonaklamaSenaryolariAsync(new KonaklamaSenaryoAramaRequestDto
        {
            TesisId = 1001,
            OdaTipiId = null,
            MisafirTipiId = 2,
            KonaklamaTipiId = 1,
            KisiSayisi = 1,
            BaslangicTarihi = new DateTime(2026, 6, 18, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 6, 19, 10, 0, 0),
            TekKisilikFiyatUygulansinMi = false,
            KonaklayanCinsiyetleri = [KonaklayanCinsiyetleri.Kadin]
        });

        Assert.True(scenarios.Count >= 3);

        var odaTipleri = scenarios
            .SelectMany(x => x.Segmentler)
            .SelectMany(x => x.OdaAtamalari)
            .Select(x => x.OdaTipiId)
            .Distinct()
            .ToHashSet();

        Assert.Contains(21, odaTipleri);
        Assert.Contains(22, odaTipleri);
        Assert.Contains(23, odaTipleri);
    }

    // Ozel kullanim, kapasite dolmadiginda kisi basi fiyat gibi carpilmali.
    [Fact]
    public async Task SenaryoUretimi_OzelKullanimKapasiteDolmadiysaKisiBasiGibiHesaplanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedOzelKullanimKisiBasiFixtureAsync(dbContext);

        var service = CreateService(dbContext);
        var scenarios = await service.GetKonaklamaSenaryolariAsync(new KonaklamaSenaryoAramaRequestDto
        {
            TesisId = 2001,
            OdaTipiId = 301,
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
            KisiSayisi = 2,
            BaslangicTarihi = new DateTime(2026, 6, 18, 14, 0, 0),
            BitisTarihi = new DateTime(2026, 6, 19, 10, 0, 0),
            TekKisilikFiyatUygulansinMi = false
        });

        var scenario = Assert.Single(scenarios);
        Assert.Single(scenario.Segmentler);
        Assert.Equal(3000m, scenario.ToplamBazUcret);
        Assert.Equal(3000m, scenario.ToplamNihaiUcret);
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

        // ODA-2 paylasimli oldugu icin, mevcut dolulugun cinsiyeti bilinmiyorsa
        // GetRoomAvailabilitiesAsync odayi guvenlik amacli tamamen disliyor;
        // SeedExistingReservationAsync konaklayan/cinsiyet kaydi eklemedigi icin
        // bu bilgiyi burada tamamliyoruz.
        dbContext.RezervasyonKonaklayanlar.Add(new RezervasyonKonaklayan
        {
            Id = 9601,
            RezervasyonId = 960,
            SiraNo = 1,
            AdSoyad = "Mevcut Misafir",
            Cinsiyet = KonaklayanCinsiyetleri.Erkek,
            KatilimDurumu = KonaklayanKatilimDurumlari.Geldi
        });
        dbContext.RezervasyonKonaklayanSegmentAtamalari.Add(new RezervasyonKonaklayanSegmentAtama
        {
            Id = 9602,
            RezervasyonKonaklayanId = 9601,
            RezervasyonSegmentId = 961,
            OdaId = 101
        });
        await dbContext.SaveChangesAsync();

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

        var service = CreateService(
            dbContext,
            DomainAccessScope.Scoped([], [2], []),
            currentTenantAccessor: new FakeScopedCurrentTenantAccessor(1));
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
            OdemeTipi = OdemeTipleri.Nakit,
            KasaBankaHesapId = 1
        });
        var result = await service.TamamlaCheckOutAsync(996);

        Assert.Equal(RezervasyonDurumlari.CheckOutTamamlandi, result.RezervasyonDurumu);
        var updated = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 996);
        Assert.Equal(RezervasyonDurumlari.CheckOutTamamlandi, updated.RezervasyonDurumu);
        var guest = await dbContext.RezervasyonKonaklayanlar.SingleAsync(x => x.RezervasyonId == 996);
        Assert.Equal(KonaklayanKatilimDurumlari.Ayrildi, guest.KatilimDurumu);
    }

    // Gelir Tahakkuku Senaryo 9: gelir belgesi taslagi olusturma basarisiz olsa bile
    // check-out islemi commit edilmis kalmalidir (best-effort izolasyonu).
    [Fact]
    public async Task CheckOut_GelirBelgesiTaslakBasarisizOlsaBileCheckOutTamamlanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 9960, segmentId: 9961, withPlan: true);

        var fakeGelirTahakkuk = new FakeRezervasyonGelirTahakkukService { FailOnOlustur = true };
        var service = CreateService(dbContext, rezervasyonGelirTahakkukService: fakeGelirTahakkuk);
        await service.TamamlaCheckInAsync(9960);
        await service.KaydetOdemeAsync(9960, new RezervasyonOdemeKaydetRequestDto
        {
            OdemeTutari = 1000m,
            OdemeTipi = OdemeTipleri.Nakit,
            KasaBankaHesapId = 1
        });

        var result = await service.TamamlaCheckOutAsync(9960);

        Assert.Equal(RezervasyonDurumlari.CheckOutTamamlandi, result.RezervasyonDurumu);
        var updated = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 9960);
        Assert.Equal(RezervasyonDurumlari.CheckOutTamamlandi, updated.RezervasyonDurumu);
        Assert.Equal(1, fakeGelirTahakkuk.OlusturCagriSayisi);
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
            Id = 19972,
            RezervasyonId = 9972,
            SiraNo = 2,
            AdSoyad = "Bekleyen Misafir",
            KatilimDurumu = KonaklayanKatilimDurumlari.Bekleniyor
        });
        dbContext.RezervasyonKonaklayanSegmentAtamalari.Add(new RezervasyonKonaklayanSegmentAtama
        {
            Id = 19973,
            RezervasyonKonaklayanId = 19972,
            RezervasyonSegmentId = 9973,
            OdaId = 101
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        await service.TamamlaCheckInAsync(9972);
        await service.KaydetOdemeAsync(9972, new RezervasyonOdemeKaydetRequestDto
        {
            OdemeTutari = 1000m,
            OdemeTipi = OdemeTipleri.Nakit,
            KasaBankaHesapId = 1
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
            OdemeTipi = OdemeTipleri.Nakit,
            KasaBankaHesapId = 1
        });

        var exception = await Assert.ThrowsAsync<BaseException>(() => service.IptalEtAsync(9981));

        Assert.Equal(400, exception.ErrorCode);
        var updated = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 9981);
        Assert.Equal(RezervasyonDurumlari.CheckInTamamlandi, updated.RezervasyonDurumu);
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
            KasaBankaHesapId = 1,
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

    // Ek hizmet seceneklerinde paket icerigi uyarisi isim benzerligiyle degil explicit hizmet kodu eslesmesiyle gelmeli.
    [Fact]
    public async Task GetEkHizmetSecenekleri_PaketIcerikKodunaGoreUyariDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationForCheckFlowAsync(dbContext, rezervasyonId: 10082, segmentId: 10083, withPlan: true);
        await SeedEkHizmetTarifesiAsync(
            dbContext,
            tarifeId: 8009,
            tesisId: 1,
            birimFiyat: 90m,
            ad: "Sabah Servisi",
            paketIcerikHizmetKodu: KonaklamaTipiIcerikHizmetKodlari.Kahvalti);

        // Paket icerigi uyarisi rezervasyonun KonaklamaTipiId'sine bagli; shared fixture bunu set etmiyor.
        var rezervasyon = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 10082);
        rezervasyon.KonaklamaTipiId = 1;

        dbContext.KonaklamaTipiIcerikKalemleri.Add(new KonaklamaTipiIcerikKalemi
        {
            Id = 18009,
            KonaklamaTipiId = 1,
            HizmetKodu = KonaklamaTipiIcerikHizmetKodlari.Kahvalti,
            Miktar = 1,
            Periyot = KonaklamaTipiIcerikPeriyotlari.Gunluk
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetEkHizmetSecenekleriAsync(10082);

        var tarife = Assert.Single(result.Tarifeler);
        Assert.Contains("Kahvalti", tarife.PaketIcerigiUyariMesaji);
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
            HizmetTarihi = new DateTime(2026, 3, 8, 15, 0, 0),
            Miktar = 1
        });

        var ilkKayit = Assert.Single(ilkOzet.EkHizmetler);

        var guncelOzet = await service.GuncelleEkHizmetAsync(1013, ilkKayit.Id, new RezervasyonEkHizmetKaydetRequestDto
        {
            RezervasyonKonaklayanId = 2013,
            EkHizmetTarifeId = 8015,
            HizmetTarihi = new DateTime(2026, 3, 8, 16, 0, 0),
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
            OdemeTipi = OdemeTipleri.Nakit,
            KasaBankaHesapId = 1
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
            OdemeTipi = OdemeTipleri.Nakit,
            KasaBankaHesapId = 1
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
            OdemeTipi = OdemeTipleri.Nakit,
            KasaBankaHesapId = 1
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
            OdemeTipi = OdemeTipleri.Nakit,
            KasaBankaHesapId = 1
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
        IReadOnlyCollection<string>? permissions = null,
        IRezervasyonGelirTahakkukService? rezervasyonGelirTahakkukService = null,
        ICurrentTenantAccessor? currentTenantAccessor = null)
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
            httpContextAccessor,
            new FakeLicenseService(),
            currentTenantAccessor ?? new FakeCurrentTenantAccessor(),
            new NoOpDomainOperationLogger(),
            new FakeRezervasyonOdemeMuhasebeService(),
            rezervasyonGelirTahakkukService ?? new FakeRezervasyonGelirTahakkukService());
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

        return new StysAppDbContext(options, null, new FakeCurrentTenantAccessor());
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
            KurumId = 1,
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

    private static async Task SeedTrtTrabzonFixtureAsync(StysAppDbContext dbContext)
    {
        await SeedLookupsAsync(dbContext);

        dbContext.Kurumlar.Add(new Kurum
        {
            Id = 1000,
            Kod = "TRT",
            Ad = "TRT",
            AktifMi = true
        });

        dbContext.Tesisler.Add(new Tesis
        {
            Id = 1001,
            Ad = "Trabzon Misafirhane",
            KurumId = 1000,
            IlId = 61,
            Telefon = "+90 462 000 00 00",
            Adres = "Trabzon Merkez",
            Eposta = "trabzon.misafirhane@trt.test",
            GirisSaati = new TimeSpan(14, 0, 0),
            CikisSaati = new TimeSpan(10, 0, 0),
            AktifMi = true
        });

        dbContext.Binalar.Add(new Bina
        {
            Id = 1002,
            TesisId = 1001,
            Ad = "Ana Bina",
            KatSayisi = 4,
            AktifMi = true
        });

        dbContext.OdaTipleri.Add(new OdaTipi
        {
            Id = 21,
            TesisId = 1001,
            OdaSinifiId = 2,
            Ad = "Tek Kişilk İki Yatak",
            Kapasite = 2,
            PaylasimliMi = false,
            AktifMi = true
        });

        dbContext.OdaTipleri.Add(new OdaTipi
        {
            Id = 23,
            TesisId = 1001,
            OdaSinifiId = 2,
            Ad = "Uc Yatakli",
            Kapasite = 3,
            PaylasimliMi = false,
            AktifMi = true
        });

        dbContext.OdaTipleri.Add(new OdaTipi
        {
            Id = 22,
            TesisId = 1001,
            OdaSinifiId = 2,
            Ad = "Suit Oda Cift Kisilik Yatak",
            Kapasite = 2,
            PaylasimliMi = false,
            AktifMi = true
        });

        dbContext.Odalar.Add(new Oda
        {
            Id = 13,
            OdaNo = "101",
            BinaId = 1002,
            TesisOdaTipiId = 21,
            KatNo = 1,
            AktifMi = true
        });

        dbContext.Odalar.Add(new Oda
        {
            Id = 21,
            OdaNo = "109",
            BinaId = 1002,
            TesisOdaTipiId = 23,
            KatNo = 1,
            AktifMi = true
        });

        dbContext.Odalar.Add(new Oda
        {
            Id = 22,
            OdaNo = "107",
            BinaId = 1002,
            TesisOdaTipiId = 22,
            KatNo = 1,
            AktifMi = true
        });

        dbContext.OdaFiyatlari.AddRange(
            new OdaFiyat
            {
                Id = 189,
                TesisOdaTipiId = 21,
                KonaklamaTipiId = 1,
                MisafirTipiId = 1,
                KisiSayisi = 1,
                KullanimSekli = OdaFiyatKullanimSekilleri.KisiBasi,
                Fiyat = 1200m,
                ParaBirimi = "TRY",
                BaslangicTarihi = new DateTime(2026, 6, 17),
                BitisTarihi = new DateTime(2026, 12, 31),
                AktifMi = true
            },
            new OdaFiyat
            {
                Id = 190,
                TesisOdaTipiId = 21,
                KonaklamaTipiId = 1,
                MisafirTipiId = 1,
                KisiSayisi = 1,
                KullanimSekli = OdaFiyatKullanimSekilleri.OzelKullanim,
                Fiyat = 1500m,
                ParaBirimi = "TRY",
                BaslangicTarihi = new DateTime(2026, 6, 17),
                BitisTarihi = new DateTime(2026, 12, 31),
                AktifMi = true
            });

        dbContext.OdaFiyatlari.Add(new OdaFiyat
        {
            Id = 191,
            TesisOdaTipiId = 23,
            KonaklamaTipiId = 1,
            MisafirTipiId = 2,
            KisiSayisi = 1,
            KullanimSekli = OdaFiyatKullanimSekilleri.KisiBasi,
            Fiyat = 1200m,
            ParaBirimi = "TRY",
            BaslangicTarihi = new DateTime(2026, 6, 17),
            BitisTarihi = new DateTime(2026, 12, 31),
            AktifMi = true
        });

        dbContext.OdaFiyatlari.AddRange(
            new OdaFiyat
            {
                Id = 192,
                TesisOdaTipiId = 21,
                KonaklamaTipiId = 1,
                MisafirTipiId = 2,
                KisiSayisi = 1,
                KullanimSekli = OdaFiyatKullanimSekilleri.KisiBasi,
                Fiyat = 1200m,
                ParaBirimi = "TRY",
                BaslangicTarihi = new DateTime(2026, 6, 17),
                BitisTarihi = new DateTime(2026, 12, 31),
                AktifMi = true
            },
            new OdaFiyat
            {
                Id = 193,
                TesisOdaTipiId = 21,
                KonaklamaTipiId = 1,
                MisafirTipiId = 2,
                KisiSayisi = 1,
                KullanimSekli = OdaFiyatKullanimSekilleri.OzelKullanim,
                Fiyat = 1500m,
                ParaBirimi = "TRY",
                BaslangicTarihi = new DateTime(2026, 6, 17),
                BitisTarihi = new DateTime(2026, 12, 31),
                AktifMi = true
            },
            new OdaFiyat
            {
                Id = 194,
                TesisOdaTipiId = 22,
                KonaklamaTipiId = 1,
                MisafirTipiId = 2,
                KisiSayisi = 1,
                KullanimSekli = OdaFiyatKullanimSekilleri.KisiBasi,
                Fiyat = 1200m,
                ParaBirimi = "TRY",
                BaslangicTarihi = new DateTime(2026, 6, 17),
                BitisTarihi = new DateTime(2026, 12, 31),
                AktifMi = true
            },
            new OdaFiyat
            {
                Id = 195,
                TesisOdaTipiId = 23,
                KonaklamaTipiId = 1,
                MisafirTipiId = 2,
                KisiSayisi = 1,
                KullanimSekli = OdaFiyatKullanimSekilleri.KisiBasi,
                Fiyat = 1200m,
                ParaBirimi = "TRY",
                BaslangicTarihi = new DateTime(2026, 6, 17),
                BitisTarihi = new DateTime(2026, 12, 31),
                AktifMi = true
            });

        dbContext.TesisMisafirTipleri.Add(new TesisMisafirTipi
        {
            Id = 5001,
            TesisId = 1001,
            MisafirTipiId = 1,
            AktifMi = true
        });

        dbContext.TesisMisafirTipleri.Add(new TesisMisafirTipi
        {
            Id = 5003,
            TesisId = 1001,
            MisafirTipiId = 2,
            AktifMi = true
        });

        dbContext.TesisKonaklamaTipleri.Add(new TesisKonaklamaTipi
        {
            Id = 5002,
            TesisId = 1001,
            KonaklamaTipiId = 1,
            AktifMi = true
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedOzelKullanimKisiBasiFixtureAsync(StysAppDbContext dbContext)
    {
        await SeedLookupsAsync(dbContext);

        dbContext.Kurumlar.Add(new Kurum
        {
            Id = 1000,
            Kod = "TRT",
            Ad = "TRT",
            AktifMi = true
        });

        dbContext.Tesisler.Add(new Tesis
        {
            Id = 2001,
            Ad = "KisiBasi Ozel Kullanim Tesisi",
            KurumId = 1000,
            IlId = 61,
            Telefon = "+90 462 000 00 01",
            Adres = "Trabzon",
            GirisSaati = new TimeSpan(14, 0, 0),
            CikisSaati = new TimeSpan(10, 0, 0),
            AktifMi = true
        });

        dbContext.Binalar.Add(new Bina
        {
            Id = 2002,
            TesisId = 2001,
            Ad = "Blok A",
            KatSayisi = 3,
            AktifMi = true
        });

        dbContext.OdaTipleri.Add(new OdaTipi
        {
            Id = 301,
            TesisId = 2001,
            OdaSinifiId = 2,
            Ad = "Uclu Oda",
            Kapasite = 3,
            PaylasimliMi = false,
            AktifMi = true
        });

        dbContext.Odalar.Add(new Oda
        {
            Id = 3001,
            OdaNo = "301",
            BinaId = 2002,
            TesisOdaTipiId = 301,
            KatNo = 3,
            AktifMi = true
        });

        dbContext.OdaFiyatlari.AddRange(
            new OdaFiyat
            {
                Id = 30010,
                TesisOdaTipiId = 301,
                KonaklamaTipiId = 1,
                MisafirTipiId = 1,
                KisiSayisi = 1,
                KullanimSekli = OdaFiyatKullanimSekilleri.KisiBasi,
                Fiyat = 1200m,
                ParaBirimi = "TRY",
                BaslangicTarihi = new DateTime(2026, 6, 17),
                BitisTarihi = new DateTime(2026, 12, 31),
                AktifMi = true
            },
            new OdaFiyat
            {
                Id = 30011,
                TesisOdaTipiId = 301,
                KonaklamaTipiId = 1,
                MisafirTipiId = 1,
                KisiSayisi = 1,
                KullanimSekli = OdaFiyatKullanimSekilleri.OzelKullanim,
                Fiyat = 1500m,
                ParaBirimi = "TRY",
                BaslangicTarihi = new DateTime(2026, 6, 17),
                BitisTarihi = new DateTime(2026, 12, 31),
                AktifMi = true
            });

        dbContext.TesisMisafirTipleri.Add(new TesisMisafirTipi
        {
            Id = 30012,
            TesisId = 2001,
            MisafirTipiId = 1,
            AktifMi = true
        });

        dbContext.TesisKonaklamaTipleri.Add(new TesisKonaklamaTipi
        {
            Id = 30013,
            TesisId = 2001,
            KonaklamaTipiId = 1,
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
            KurumId = 1,
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
            KurumId = 1,
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
            KurumId = 1,
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
            KurumId = 1,
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
            KurumId = 1,
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
                KurumId = 1,
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
                KurumId = 1,
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
        string ad = "Ek Hizmet",
        string? paketIcerikHizmetKodu = null)
    {
        var ekHizmetId = tarifeId + 100000;

        dbContext.EkHizmetler.Add(new EkHizmet
        {
            Id = ekHizmetId,
            TesisId = tesisId,
            Ad = ad,
            BirimAdi = "Adet",
            PaketIcerikHizmetKodu = paketIcerikHizmetKodu,
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

        dbContext.MisafirTipleri.Add(new MisafirTipi
        {
            Id = 2,
            Kod = "TEST-DIGER",
            Ad = "Test Diger Tipi",
            AktifMi = true
        });

        dbContext.KonaklamaTipleri.Add(new KonaklamaTipi
        {
            Id = 1,
            Kod = "TEST-KONAKLAMA",
            Ad = "Test Konaklama Tipi",
            AktifMi = true
        });

        // TesisId=1 kullanan fixture'lar icin varsayilan misafir/konaklama tipi izinleri.
        dbContext.TesisMisafirTipleri.Add(new TesisMisafirTipi
        {
            Id = 9901,
            TesisId = 1,
            MisafirTipiId = 1,
            AktifMi = true
        });

        dbContext.TesisMisafirTipleri.Add(new TesisMisafirTipi
        {
            Id = 9902,
            TesisId = 1,
            MisafirTipiId = 2,
            AktifMi = true
        });

        dbContext.TesisKonaklamaTipleri.Add(new TesisKonaklamaTipi
        {
            Id = 9903,
            TesisId = 1,
            KonaklamaTipiId = 1,
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

    private sealed class FakeLicenseService : ILicenseService
    {
        public Task<LicenseValidationResult> GetCurrentStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(LicenseValidationResult.Failure("test"));

        public Task<bool> IsModuleLicensedAsync(string moduleCode, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public void InvalidateCache()
        {
        }

        public Task EnsureLicensedAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task EnsureModuleLicensedAsync(string moduleCode, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeCurrentTenantAccessor : ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => null;

        public IReadOnlyList<int> GetAccessibleKurumIds() => [];

        public bool IsSuperAdmin() => true;

        public bool IsKurumAdmin() => false;
    }

    // GetErisilebilirTesislerAsync gibi CurrentKurumId'nin dolu olmasini zorunlu tutan
    // (superadmin bypass'i olmayan) uc noktalari test etmek icin kullanilir.
    private sealed class FakeScopedCurrentTenantAccessor : ICurrentTenantAccessor
    {
        private readonly int _kurumId;

        public FakeScopedCurrentTenantAccessor(int kurumId)
        {
            _kurumId = kurumId;
        }

        public int? GetCurrentKurumId() => _kurumId;

        public IReadOnlyList<int> GetAccessibleKurumIds() => [_kurumId];

        public bool IsSuperAdmin() => true;

        public bool IsKurumAdmin() => false;
    }

    private sealed class NoOpDomainOperationLogger : IDomainOperationLogger
    {
        public void Started(string eventName, object payload)
        {
        }

        public void Completed(string eventName, object payload)
        {
        }

        public void Warning(string eventName, object payload)
        {
        }

        public void Failed(string eventName, Exception exception, object payload)
        {
        }
    }

    /// <summary>
    /// InMemory provider gercek TahsilatOdemeBelgesi/CariKart/KasaBankaHesap muhasebe zincirini
    /// (unique index, FK, SqlException tabanli retry) desteklemedigi icin bu Fake, gercek
    /// RezervasyonOdemeMuhasebeService'in SADECE KaydetOdemeAsync'i cagiran testler acisindan
    /// gozlemlenebilir davranisini (KasaBankaHesapId zorunlulugu) yansitir; TahsilatOdemeBelgesi
    /// uretmez. Muhasebe entegrasyonunun uctan uca davranisi ayri, gercek SQL Server'a karsi
    /// calisan bir test dosyasinda dogrulanir.
    /// </summary>
    private sealed class FakeRezervasyonOdemeMuhasebeService : IRezervasyonOdemeMuhasebeService
    {
        private static readonly string[] NakitHareketiGerektirenler = ["Nakit", "KrediKarti", "HavaleEft"];

        public Task TahsilatOlusturAsync(
            Rezervasyon rezervasyon,
            RezervasyonOdeme odeme,
            int? kasaBankaHesapId,
            int? cariKartIdOverride,
            CancellationToken cancellationToken = default)
        {
            if (NakitHareketiGerektirenler.Contains(odeme.OdemeTipi) && !kasaBankaHesapId.HasValue)
            {
                throw new BaseException(
                    $"'{odeme.OdemeTipi}' odeme tipi icin kasa/banka/POS hesabi secimi zorunludur.", 400);
            }

            odeme.KasaBankaHesapId = kasaBankaHesapId;
            return Task.CompletedTask;
        }

        public Task TahsilatIptalEtAsync(RezervasyonOdeme odeme, string? iptalAciklama, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    /// <summary>Check-out'un best-effort gelir belgesi tetiklemesini gerçek muhasebe altyapısına
    /// bağlamadan doğrulamak için kullanılan sahte servis. FailOnOlustur=true verilirse
    /// check-out'un bu hatayı yuttuğunu (best-effort) kanıtlamak için istisna fırlatır.</summary>
    private sealed class FakeRezervasyonGelirTahakkukService : IRezervasyonGelirTahakkukService
    {
        public bool FailOnOlustur { get; set; }
        public int OlusturCagriSayisi { get; private set; }

        public Task<SatisBelgesiDto> OlusturTaslakAsync(int rezervasyonId, CancellationToken cancellationToken = default)
        {
            OlusturCagriSayisi++;
            if (FailOnOlustur)
            {
                throw new BaseException("Test: gelir belgesi taslagi olusturulamadi.", 500);
            }

            return Task.FromResult(new SatisBelgesiDto { Id = 1, BelgeNo = "TEST-1" });
        }

        public Task<RezervasyonGelirOzetiDto> GetGelirOzetiAsync(int rezervasyonId, CancellationToken cancellationToken = default)
            => Task.FromResult(new RezervasyonGelirOzetiDto { RezervasyonId = rezervasyonId });

        public Task<RezervasyonTahsilatKapamaSonucuDto> KapatOncekiTahsilatlariAsync(int rezervasyonId, CancellationToken cancellationToken = default)
            => Task.FromResult(new RezervasyonTahsilatKapamaSonucuDto());
    }

    // ─────────────────────────────────────────────────────────────
    // Uzatma secenekleri (check-in yapilmis rezervasyonlar icin salt-okunur uzatma API'si)
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task UzatmaSecenekleri_MevcutOdaBosken_AyniOdadaDevamIlkSecenekOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        await SeedUzatmaRezervasyonuAsync(
            dbContext, 5001, 5002, odaId: 101,
            girisTarihi: new DateTime(2026, 3, 8, 14, 0, 0),
            cikisTarihi: new DateTime(2026, 3, 9, 10, 0, 0));

        var service = CreateService(dbContext);
        var result = await service.GetUzatmaSecenekleriAsync(5001, new RezervasyonUzatmaSecenekleriRequestDto
        {
            YeniCikisTarihi = new DateTime(2026, 3, 10, 10, 0, 0)
        });

        Assert.Equal(RezervasyonUzatmaSonucKodlari.SecenekBulundu, result.SonucKodu);
        Assert.NotEmpty(result.Secenekler);
        Assert.Equal(RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam, result.Secenekler[0].SenaryoTipi);
        Assert.Equal(0, result.Secenekler[0].OdaDegisimSayisi);
        Assert.Equal(101, Assert.Single(result.Secenekler[0].Segmentler.Single().OdaAtamalari).OdaId);
    }

    [Fact]
    public async Task UzatmaSecenekleri_MevcutOdaSonrakiRezervasyonaBagliyken_CheckoutGunundeOdaDegisimiUretilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);

        await SeedUzatmaRezervasyonuAsync(dbContext, 5011, 5012, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis);
        // Oda 101, uzatma araliginin TAMAMINDA baska bir rezervasyona bagli.
        await SeedDigerRezervasyonuAsync(dbContext, 5013, 5014, odaId: 101, cikis, yeniCikis);

        var service = CreateService(dbContext);
        var result = await service.GetUzatmaSecenekleriAsync(5011, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });

        Assert.Equal(RezervasyonUzatmaSonucKodlari.SecenekBulundu, result.SonucKodu);
        Assert.Contains(result.Secenekler, x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.CheckoutGunundeOdaDegisimi);
        Assert.DoesNotContain(result.Secenekler, x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam);
        Assert.DoesNotContain(result.Secenekler, x => x.Segmentler.Any(s => s.OdaAtamalari.Any(a => a.OdaId == 101)));
    }

    [Fact]
    public async Task UzatmaSecenekleri_TekOdaTumAralikUygunDegilAmaGercekSinirdaIkiSegmentKurulabiliyorsa_UzatmaSirasindaOdaDegisimiUretilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedTwoRoomUzatmaFixtureAsync(dbContext);

        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var boundary = new DateTime(2026, 3, 10, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 11, 10, 0, 0);

        await SeedUzatmaRezervasyonuAsync(dbContext, 5021, 5022, odaId: 100, new DateTime(2026, 3, 8, 14, 0, 0), cikis);
        // Oda 100 (mevcut oda), uzatmanin ILK gecesinde baska rezervasyona bagli.
        await SeedDigerRezervasyonuAsync(dbContext, 5023, 5024, odaId: 100, cikis, boundary);
        // Oda 101, uzatmanin IKINCI gecesinde baska rezervasyona bagli.
        await SeedDigerRezervasyonuAsync(dbContext, 5025, 5026, odaId: 101, boundary, yeniCikis);

        var service = CreateService(dbContext);
        var result = await service.GetUzatmaSecenekleriAsync(5021, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });

        Assert.Equal(RezervasyonUzatmaSonucKodlari.SecenekBulundu, result.SonucKodu);
        var secim = Assert.Single(result.Secenekler, x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.UzatmaSirasindaOdaDegisimi);
        Assert.Equal(2, secim.Segmentler.Count);
        Assert.Equal(boundary, secim.Segmentler[0].BitisTarihi);
        Assert.Equal(boundary, secim.Segmentler[1].BaslangicTarihi);
        Assert.Equal(101, secim.Segmentler[0].OdaAtamalari.Single().OdaId);
        Assert.Equal(100, secim.Segmentler[1].OdaAtamalari.Single().OdaId);
    }

    [Fact]
    public async Task UzatmaSecenekleri_HicUygunPlanYoksa_MusaitlikYokVeBosListeDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedSingleRoomFixtureAsync(dbContext, new TimeSpan(14, 0, 0), new TimeSpan(10, 0, 0), 500m);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);

        await SeedUzatmaRezervasyonuAsync(dbContext, 5031, 5032, odaId: 100, new DateTime(2026, 3, 8, 14, 0, 0), cikis);
        // Tesisteki TEK oda, uzatmanin TAMAMINDA baska bir rezervasyona bagli.
        await SeedDigerRezervasyonuAsync(dbContext, 5033, 5034, odaId: 100, cikis, yeniCikis);

        var service = CreateService(dbContext);
        var result = await service.GetUzatmaSecenekleriAsync(5031, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });

        Assert.Equal(RezervasyonUzatmaSonucKodlari.MusaitlikYok, result.SonucKodu);
        Assert.Empty(result.Secenekler);
        Assert.False(string.IsNullOrWhiteSpace(result.Mesaj));
    }

    [Fact]
    public async Task UzatmaSecenekleri_BakimArizaliOdaSecilmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);

        await SeedUzatmaRezervasyonuAsync(dbContext, 5041, 5042, odaId: 100, new DateTime(2026, 3, 8, 14, 0, 0), cikis);

        // Mevcut oda (100) uzatma boyunca BASKA rezervasyona bagli DEGIL ama BAKIM blogu var.
        dbContext.OdaKullanimBloklari.Add(new OdaKullanimBlok
        {
            Id = 9001,
            TesisId = 1,
            OdaId = 100,
            BlokTipi = OdaKullanimBlokTipleri.Bakim,
            BaslangicTarihi = cikis,
            BitisTarihi = yeniCikis,
            Aciklama = "Test bakim",
            AktifMi = true
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetUzatmaSecenekleriAsync(5041, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });

        Assert.DoesNotContain(result.Secenekler, x => x.Segmentler.Any(s => s.OdaAtamalari.Any(a => a.OdaId == 100)));
    }

    [Fact]
    public async Task UzatmaSecenekleri_IptalVeCheckoutTamamlanmisRezervasyonlarDoluluguEngellemez()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);

        await SeedUzatmaRezervasyonuAsync(dbContext, 5051, 5052, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis);

        // Ayni odada (101), uzatma araligini kapsayan IPTAL edilmis bir rezervasyon var - engellememeli.
        await SeedDigerRezervasyonuAsync(dbContext, 5053, 5054, odaId: 101, cikis, yeniCikis, durum: RezervasyonDurumlari.Iptal);
        // Ayni odada (101), uzatma araligini kapsayan CHECK-OUT TAMAMLANMIS bir rezervasyon var - engellememeli.
        await SeedDigerRezervasyonuAsync(dbContext, 5055, 5056, odaId: 101, cikis, yeniCikis, durum: RezervasyonDurumlari.CheckOutTamamlandi);

        var service = CreateService(dbContext);
        var result = await service.GetUzatmaSecenekleriAsync(5051, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });

        Assert.Equal(RezervasyonUzatmaSonucKodlari.SecenekBulundu, result.SonucKodu);
        Assert.Contains(result.Secenekler, x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam);
    }

    [Fact]
    public async Task UzatmaSecenekleri_PaylasimliOdaCinsiyetKuraliKorunur()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);

        await SeedUzatmaRezervasyonuAsync(
            dbContext, 5061, 5062, odaId: 105, new DateTime(2026, 3, 8, 14, 0, 0), cikis,
            cinsiyet: KonaklayanCinsiyetleri.Kadin);

        // Ayni paylasimli odada (105), uzatma araliginda FARKLI cinsiyette baska bir konaklayan var.
        await SeedDigerPaylasimliRezervasyonuAsync(dbContext, 5063, 5064, odaId: 105, cikis, yeniCikis, KonaklayanCinsiyetleri.Erkek);

        var service = CreateService(dbContext);
        var result = await service.GetUzatmaSecenekleriAsync(5061, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });

        Assert.DoesNotContain(result.Secenekler, x => x.Segmentler.Any(s => s.OdaAtamalari.Any(a => a.OdaId == 105)));
    }

    [Fact]
    public async Task UzatmaSecenekleri_YeniCikisTarihiMevcutCikistanKucukVeyaEsitse_400Doner()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        await SeedUzatmaRezervasyonuAsync(dbContext, 5073, 5074, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis);

        var service = CreateService(dbContext);

        var exEsit = await Assert.ThrowsAsync<BaseException>(() =>
            service.GetUzatmaSecenekleriAsync(5073, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = cikis }));
        Assert.Equal(400, exEsit.ErrorCode);

        var exKucuk = await Assert.ThrowsAsync<BaseException>(() =>
            service.GetUzatmaSecenekleriAsync(5073, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = cikis.AddHours(-1) }));
        Assert.Equal(400, exKucuk.ErrorCode);
    }

    [Theory]
    [InlineData(RezervasyonDurumlari.Taslak)]
    [InlineData(RezervasyonDurumlari.Onayli)]
    [InlineData(RezervasyonDurumlari.CheckOutTamamlandi)]
    [InlineData(RezervasyonDurumlari.Iptal)]
    public async Task UzatmaSecenekleri_CheckInTamamlandiDisindaDurumlardaReddedilir(string durum)
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        await SeedUzatmaRezervasyonuAsync(
            dbContext, 5080, 5081, odaId: 101,
            new DateTime(2026, 3, 8, 14, 0, 0), new DateTime(2026, 3, 9, 10, 0, 0),
            durum: durum);

        var service = CreateService(dbContext);
        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.GetUzatmaSecenekleriAsync(5080, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = new DateTime(2026, 3, 10, 10, 0, 0) }));
        Assert.Equal(400, ex.ErrorCode);
    }

    [Fact]
    public async Task UzatmaSecenekleri_TenantErisimKuraliKorunur()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        await SeedUzatmaRezervasyonuAsync(dbContext, 5091, 5092, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), new DateTime(2026, 3, 9, 10, 0, 0));

        // Rezervasyonun tesisi KurumId=1'e ait - farkli (superadmin olmayan) bir kurum erisimiyle
        // rezervasyon GORULEMEMELIDIR.
        var service = CreateService(dbContext, currentTenantAccessor: new FakeNonSuperAdminTenantAccessor(2));

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.GetUzatmaSecenekleriAsync(5091, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = new DateTime(2026, 3, 10, 10, 0, 0) }));
        Assert.Equal(404, ex.ErrorCode);
    }

    [Fact]
    public async Task UzatmaSecenekleri_TesisErisimKapsamiKorunur()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        await SeedUzatmaRezervasyonuAsync(dbContext, 5093, 5094, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), new DateTime(2026, 3, 9, 10, 0, 0));

        // Kullanici yalnizca TesisId=2 kapsamina yetkili - TesisId=1'deki rezervasyona erisemez.
        var service = CreateService(dbContext, scope: DomainAccessScope.Scoped([], [2], []));

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.GetUzatmaSecenekleriAsync(5093, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = new DateTime(2026, 3, 10, 10, 0, 0) }));
        Assert.Equal(403, ex.ErrorCode);
    }

    [Fact]
    public async Task UzatmaSecenekleri_SonucSiralamasiVeKodlariDeterministiktir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        await SeedUzatmaRezervasyonuAsync(dbContext, 5101, 5102, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), new DateTime(2026, 3, 9, 10, 0, 0));

        var service = CreateService(dbContext);
        var request = new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = new DateTime(2026, 3, 10, 10, 0, 0) };

        var sonuc1 = await service.GetUzatmaSecenekleriAsync(5101, request);
        var sonuc2 = await service.GetUzatmaSecenekleriAsync(5101, request);

        Assert.Equal(sonuc1.Secenekler.Select(x => x.SenaryoKodu), sonuc2.Secenekler.Select(x => x.SenaryoKodu));
        Assert.Equal(sonuc1.Secenekler.Select(x => x.SenaryoTipi), sonuc2.Secenekler.Select(x => x.SenaryoTipi));
        Assert.StartsWith("UZT-", sonuc1.Secenekler[0].SenaryoKodu);
        Assert.Equal(RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam, sonuc1.Secenekler[0].SenaryoTipi);
    }

    [Fact]
    public async Task UzatmaSecenekleri_AramaVeritabaninda_HicbirDegisiklikOlusturmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        await SeedUzatmaRezervasyonuAsync(dbContext, 5111, 5112, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), new DateTime(2026, 3, 9, 10, 0, 0));

        var oncekiRezervasyonSayisi = await dbContext.Rezervasyonlar.CountAsync();
        var oncekiSegmentSayisi = await dbContext.RezervasyonSegmentleri.CountAsync();
        var oncekiAtamaSayisi = await dbContext.RezervasyonSegmentOdaAtamalari.CountAsync();
        var oncekiDurum = (await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 5111)).RezervasyonDurumu;

        var service = CreateService(dbContext);
        await service.GetUzatmaSecenekleriAsync(5111, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = new DateTime(2026, 3, 10, 10, 0, 0) });

        Assert.Equal(oncekiRezervasyonSayisi, await dbContext.Rezervasyonlar.CountAsync());
        Assert.Equal(oncekiSegmentSayisi, await dbContext.RezervasyonSegmentleri.CountAsync());
        Assert.Equal(oncekiAtamaSayisi, await dbContext.RezervasyonSegmentOdaAtamalari.CountAsync());
        Assert.Equal(oncekiDurum, (await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 5111)).RezervasyonDurumu);
    }

    [Fact]
    public async Task UzatmaSecenekleri_AyniSinirdaBirdenFazlaIkiSegmentliAday_IlkAdaydaAramaDurmazVeMevcutTipEslesenTercihEdilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedThreeRoomUzatmaFixtureAsync(dbContext);

        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var boundary = new DateTime(2026, 3, 10, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 11, 10, 0, 0);

        await SeedUzatmaRezervasyonuAsync(dbContext, 5201, 5202, odaId: 100, new DateTime(2026, 3, 8, 14, 0, 0), cikis);
        // Mevcut oda (100), uzatmanin ILK gecesinde baska rezervasyona bagli.
        await SeedDigerRezervasyonuAsync(dbContext, 5203, 5204, odaId: 100, cikis, boundary);
        // Ayni tipteki alternatif oda (101) VE farkli tipteki daha buyuk kapasiteli oda (102),
        // uzatmanin IKINCI gecesinde baska rezervasyonlara bagli - boylece ILK gece icin HER IKISI
        // de (greedy=yuksek kapasiteli 102 VE tip-eslesen 101) gecerli birer aday olur.
        await SeedDigerRezervasyonuAsync(dbContext, 5205, 5206, odaId: 101, boundary, yeniCikis);
        await SeedDigerRezervasyonuAsync(dbContext, 5207, 5208, odaId: 102, boundary, yeniCikis);

        var service = CreateService(dbContext);
        var result = await service.GetUzatmaSecenekleriAsync(5201, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });

        Assert.Equal(RezervasyonUzatmaSonucKodlari.SecenekBulundu, result.SonucKodu);

        // Eski (tek greedy denemeli) yaklasim yalnizca daha yuksek kapasiteli Oda 102'yi bulurdu -
        // yeni yaklasim, mevcut oda TIPIYLE eslesen Oda 101 alternatifini de bulmali ve ilk
        // UzatmaSirasindaOdaDegisimi secenegi olarak TERCIH etmelidir.
        Assert.Contains(result.Secenekler, x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.UzatmaSirasindaOdaDegisimi
            && x.Segmentler[0].OdaAtamalari.Any(a => a.OdaId == 101));

        var ilkUzatmaSirasindaSecenek = result.Secenekler.First(x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.UzatmaSirasindaOdaDegisimi);
        Assert.Equal(101, ilkUzatmaSirasindaSecenek.Segmentler[0].OdaAtamalari.Single().OdaId);
    }

    [Fact]
    public async Task UzatmaSecenekleri_IlkGercekSinirdaPlanYokIkinciSinirdaVarsaBulunur()
    {
        await using var dbContext = CreateDbContext();
        await SeedTwoRoomUzatmaFixtureAsync(dbContext);

        var d0 = new DateTime(2026, 3, 9, 10, 0, 0);
        var d1 = new DateTime(2026, 3, 10, 10, 0, 0);
        var d2 = new DateTime(2026, 3, 11, 10, 0, 0);
        var d3 = new DateTime(2026, 3, 12, 10, 0, 0);

        await SeedUzatmaRezervasyonuAsync(dbContext, 5211, 5212, odaId: 100, new DateTime(2026, 3, 8, 14, 0, 0), d0);
        // Oda 100 (mevcut oda), uzatmanin SON gecesinde (d2-d3) baska rezervasyona bagli.
        await SeedDigerRezervasyonuAsync(dbContext, 5213, 5214, odaId: 100, d2, d3);
        // Oda 101, uzatmanin ORTA gecesinde (d1-d2) baska rezervasyona bagli.
        await SeedDigerRezervasyonuAsync(dbContext, 5215, 5216, odaId: 101, d1, d2);

        var service = CreateService(dbContext);
        var result = await service.GetUzatmaSecenekleriAsync(5211, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = d3 });

        Assert.Equal(RezervasyonUzatmaSonucKodlari.SecenekBulundu, result.SonucKodu);
        var secim = Assert.Single(result.Secenekler, x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.UzatmaSirasindaOdaDegisimi);

        // Ilk gercek sinir (d1) HER IKI segment icin de basarisiz olmalidir; bulunan bolme
        // tarihi, ikinci (basarili) gercek sinir olan d2 olmalidir.
        Assert.Equal(d2, secim.Segmentler[0].BitisTarihi);
        Assert.Equal(100, secim.Segmentler[0].OdaAtamalari.Single().OdaId);
        Assert.Equal(101, secim.Segmentler[1].OdaAtamalari.Single().OdaId);
    }

    [Fact]
    public async Task UzatmaSecenekleri_OdaBolunmesi_101IkiKisidenBirBirKisiye_BirOdaDegisimiSayilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedPaylasimliUzatmaFixtureAsync(dbContext);

        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);

        // Mevcut rezervasyon: 2 kisi, TAMAMEN Oda 101'de (paylasimli, kapasite 2).
        await SeedUzatmaRezervasyonuAsync(dbContext, 5221, 5222, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis, kisiSayisi: 2);

        // Uzatma boyunca Oda 101'in 1 yatagi BASKA (cinsiyeti bilinen) bir konaklayan tarafindan
        // dolduruluyor - geriye yalnizca 1 kisilik yer kaliyor, tam plan icin Oda 102 gerekiyor.
        await SeedDigerPaylasimliRezervasyonuAsync(dbContext, 5223, 5224, odaId: 101, cikis, yeniCikis, KonaklayanCinsiyetleri.Kadin);

        var service = CreateService(dbContext);
        var result = await service.GetUzatmaSecenekleriAsync(5221, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });

        Assert.Equal(RezervasyonUzatmaSonucKodlari.SecenekBulundu, result.SonucKodu);
        var secim = Assert.Single(result.Secenekler, x =>
            x.Segmentler.SelectMany(s => s.OdaAtamalari).Any(a => a.OdaId == 101) &&
            x.Segmentler.SelectMany(s => s.OdaAtamalari).Any(a => a.OdaId == 102));

        // {101:2} -> {101:1,102:1}: yalnizca 1 kisi oda degistiriyor - sonuc TAM OLARAK 1 olmalidir.
        Assert.Equal(1, secim.OdaDegisimSayisi);
        Assert.Equal(RezervasyonUzatmaSenaryoTipleri.CheckoutGunundeOdaDegisimi, secim.SenaryoTipi);
        Assert.DoesNotContain(result.Secenekler, x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam);
    }

    [Fact]
    public async Task UzatmaSecenekleri_OdaBirlesmesi_101Bir102BirdenTekOdaya_BirOdaDegisimiSayilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedPaylasimliUzatmaFixtureAsync(dbContext);

        var girisTarihi = new DateTime(2026, 3, 8, 14, 0, 0);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);

        // Mevcut rezervasyon: 2 kisi, SON segmentte Oda 101 (1 kisi) ve Oda 102 (1 kisi) olarak
        // BOLUNMUS - CalculateRoomChangeCount'un TERS yon ({101:1,102:1} -> {101:2}) icin de AYNI
        // (simetrik) sonucu urettigini dogrular.
        dbContext.Rezervasyonlar.Add(new Rezervasyon
        {
            Id = 5341,
            ReferansNo = "UZT-5341",
            TesisId = 1,
            KisiSayisi = 2,
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
            GirisTarihi = girisTarihi,
            CikisTarihi = cikis,
            MisafirAdiSoyadi = "Birlesme Test",
            MisafirTelefon = "000",
            ToplamBazUcret = 100m,
            ToplamUcret = 100m,
            ParaBirimi = "TRY",
            RezervasyonDurumu = RezervasyonDurumlari.CheckInTamamlandi,
            AktifMi = true
        });
        dbContext.RezervasyonSegmentleri.Add(new RezervasyonSegment
        {
            Id = 5342,
            RezervasyonId = 5341,
            SegmentSirasi = 1,
            BaslangicTarihi = girisTarihi,
            BitisTarihi = cikis
        });
        dbContext.RezervasyonSegmentOdaAtamalari.AddRange(
            new RezervasyonSegmentOdaAtama
            {
                Id = 5343, RezervasyonSegmentId = 5342, OdaId = 101, AyrilanKisiSayisi = 1,
                OdaNoSnapshot = "P-1", BinaAdiSnapshot = "Blok", OdaTipiAdiSnapshot = "Paylasimli Cift",
                PaylasimliMiSnapshot = true, KapasiteSnapshot = 2
            },
            new RezervasyonSegmentOdaAtama
            {
                Id = 5344, RezervasyonSegmentId = 5342, OdaId = 102, AyrilanKisiSayisi = 1,
                OdaNoSnapshot = "T-1", BinaAdiSnapshot = "Blok", OdaTipiAdiSnapshot = "Tekli",
                PaylasimliMiSnapshot = false, KapasiteSnapshot = 1
            });
        await dbContext.SaveChangesAsync();

        // Uzatma boyunca Oda 102 baska bir rezervasyona bagli (kullanilamaz), Oda 101 ise
        // TAMAMEN BOS (2 kisilik kapasitenin tamami musait) - tam plan icin ikisi birlesir.
        await SeedDigerRezervasyonuAsync(dbContext, 5345, 5346, odaId: 102, cikis, yeniCikis);

        var service = CreateService(dbContext);
        var result = await service.GetUzatmaSecenekleriAsync(5341, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });

        Assert.Equal(RezervasyonUzatmaSonucKodlari.SecenekBulundu, result.SonucKodu);
        var secim = Assert.Single(result.Secenekler, x => x.Segmentler.SelectMany(s => s.OdaAtamalari).All(a => a.OdaId == 101));

        Assert.Equal(2, secim.Segmentler.Single().OdaAtamalari.Single().AyrilanKisiSayisi);
        Assert.Equal(1, secim.OdaDegisimSayisi);
    }

    [Fact]
    public async Task UzatmaSecenekleri_GercekIkiSegmentliPlan_101Bir102BirdenTekOdayaBirlesme_BirDegisiklikSayilirVeElenmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedPaylasimliUzatmaFixtureAsync(dbContext);

        var girisTarihi = new DateTime(2026, 3, 8, 14, 0, 0);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var boundary = new DateTime(2026, 3, 10, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 11, 10, 0, 0);

        await SeedUzatmaRezervasyonuAsync(dbContext, 5351, 5352, odaId: 101, girisTarihi, cikis, kisiSayisi: 2);

        // Oda 101'in 1 yatagi, uzatmanin ILK gecesinde BASKA bir konaklayan tarafindan
        // dolduruluyor (ikinci gece bosaliyor).
        await SeedDigerPaylasimliRezervasyonuAsync(dbContext, 5353, 5354, odaId: 101, cikis, boundary, KonaklayanCinsiyetleri.Kadin);
        // Oda 102, uzatmanin IKINCI gecesinde baska bir rezervasyona bagli (ilk gece serbest).
        await SeedDigerRezervasyonuAsync(dbContext, 5355, 5356, odaId: 102, boundary, yeniCikis);

        var service = CreateService(dbContext);
        var result = await service.GetUzatmaSecenekleriAsync(5351, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });

        Assert.Equal(RezervasyonUzatmaSonucKodlari.SecenekBulundu, result.SonucKodu);
        // Birinci segment {101:1,102:1}, ikinci segment {101:2} olan bu plan bir kisinin
        // 102'den 101'e gecmesini gerektirir (OdaDegisimSayisi=1) ve >1 degisiklik denilerek
        // ELENMEMELIDIR.
        var secim = Assert.Single(result.Secenekler, x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.UzatmaSirasindaOdaDegisimi);

        Assert.Equal(2, secim.Segmentler.Count);
        Assert.Equal(2, secim.Segmentler[0].OdaAtamalari.Count);
        Assert.Contains(secim.Segmentler[0].OdaAtamalari, a => a.OdaId == 101 && a.AyrilanKisiSayisi == 1);
        Assert.Contains(secim.Segmentler[0].OdaAtamalari, a => a.OdaId == 102 && a.AyrilanKisiSayisi == 1);
        Assert.Equal(101, Assert.Single(secim.Segmentler[1].OdaAtamalari).OdaId);
        Assert.Equal(2, secim.Segmentler[1].OdaAtamalari.Single().AyrilanKisiSayisi);
        Assert.Equal(1, secim.OdaDegisimSayisi);
    }

    [Fact]
    public void CalculateRoomChangeCount_SimetriktirVeKisiSayisiniDikkateAlir()
    {
        var iki101 = new List<KonaklamaSenaryoOdaAtamaDto> { UzatmaOdaAtamasi(101, 2) };
        var bir101Bir102 = new List<KonaklamaSenaryoOdaAtamaDto> { UzatmaOdaAtamasi(101, 1), UzatmaOdaAtamasi(102, 1) };
        var bir103Bir104 = new List<KonaklamaSenaryoOdaAtamaDto> { UzatmaOdaAtamasi(103, 1), UzatmaOdaAtamasi(104, 1) };

        // {101:2} -> {101:1,102:1} ve tersi: ikisi de 1 (simetrik).
        var ileri = InvokeCalculateRoomChangeCount(iki101, bir101Bir102);
        var geri = InvokeCalculateRoomChangeCount(bir101Bir102, iki101);
        Assert.Equal(1, ileri);
        Assert.Equal(1, geri);
        Assert.Equal(ileri, geri);

        // {101:1,102:1} -> {103:1,104:1}: iki farkli kisi oda degistiriyor -> 2 (her iki yonde de).
        Assert.Equal(2, InvokeCalculateRoomChangeCount(bir101Bir102, bir103Bir104));
        Assert.Equal(2, InvokeCalculateRoomChangeCount(bir103Bir104, bir101Bir102));

        // Hic kimse oda degistirmiyorsa 0.
        Assert.Equal(0, InvokeCalculateRoomChangeCount(iki101, iki101));

        // Basit, TEK kisilik A->B tam takasi -> 1 (iki degil).
        var odaA = new List<KonaklamaSenaryoOdaAtamaDto> { UzatmaOdaAtamasi(105, 1) };
        var odaB = new List<KonaklamaSenaryoOdaAtamaDto> { UzatmaOdaAtamasi(106, 1) };
        Assert.Equal(1, InvokeCalculateRoomChangeCount(odaA, odaB));
        Assert.Equal(1, InvokeCalculateRoomChangeCount(odaB, odaA));
    }

    [Fact]
    public async Task UzatmaSecenekleri_BakimKaldirilinceKapasiteYeterliAmaCinsiyetUyumuSaglanamiyorsa_MesajBakimiTekNedenGostermez()
    {
        await using var dbContext = CreateDbContext();
        await SeedPaylasimliUzatmaFixtureAsync(dbContext);

        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);

        // Mevcut rezervasyon: 1 kisi (Erkek), Oda 102'de (tekli).
        await SeedUzatmaRezervasyonuAsync(dbContext, 5361, 5362, odaId: 102, new DateTime(2026, 3, 8, 14, 0, 0), cikis, cinsiyet: KonaklayanCinsiyetleri.Erkek);

        // Oda 102, uzatma boyunca GERCEK baska bir rezervasyona bagli.
        await SeedDigerRezervasyonuAsync(dbContext, 5363, 5364, odaId: 102, cikis, yeniCikis);

        // Oda 101 (paylasimli), uzatma boyunca hem bir BAKIM blogu HEM DE Kadin cinsiyetli baska
        // bir konaklayan tarafindan dolu - blok kaldirilsa DAHI cinsiyet uyumsuzlugu (Erkek
        // rezervasyon, Kadin dolu oda) nedeniyle KULLANILAMAZ.
        await SeedDigerPaylasimliRezervasyonuAsync(dbContext, 5365, 5366, odaId: 101, cikis, yeniCikis, KonaklayanCinsiyetleri.Kadin);
        dbContext.OdaKullanimBloklari.Add(new OdaKullanimBlok
        {
            Id = 9201,
            TesisId = 1,
            OdaId = 101,
            BlokTipi = OdaKullanimBlokTipleri.Bakim,
            BaslangicTarihi = cikis,
            BitisTarihi = yeniCikis,
            Aciklama = "Cinsiyet uyumsuzlugu testi",
            AktifMi = true
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetUzatmaSecenekleriAsync(5361, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });

        Assert.Equal(RezervasyonUzatmaSonucKodlari.MusaitlikYok, result.SonucKodu);
        Assert.Empty(result.Secenekler);
        Assert.DoesNotContain("bakim", result.Mesaj, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UzatmaSecenekleri_BakimKaldirilinceCinsiyetDahilTumKurallarSaglaniyorsa_BakimaOzguMesajDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedPaylasimliUzatmaFixtureAsync(dbContext);

        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);

        // Mevcut rezervasyon: 1 kisi (Kadin), Oda 102'de (tekli).
        await SeedUzatmaRezervasyonuAsync(dbContext, 5371, 5372, odaId: 102, new DateTime(2026, 3, 8, 14, 0, 0), cikis, cinsiyet: KonaklayanCinsiyetleri.Kadin);

        // Oda 102, uzatma boyunca GERCEK baska bir rezervasyona bagli.
        await SeedDigerRezervasyonuAsync(dbContext, 5373, 5374, odaId: 102, cikis, yeniCikis);

        // Oda 101 (paylasimli) BASKA HICBIR rezervasyona bagli DEGIL - yalnizca bir bakim blogu
        // var; blok KALDIRILSAYDI kapasite VE cinsiyet (baska konaklayan yok, kisit yok) acisindan
        // TAM bir plan kurulabilirdi.
        dbContext.OdaKullanimBloklari.Add(new OdaKullanimBlok
        {
            Id = 9202,
            TesisId = 1,
            OdaId = 101,
            BlokTipi = OdaKullanimBlokTipleri.Bakim,
            BaslangicTarihi = cikis,
            BitisTarihi = yeniCikis,
            Aciklama = "Cinsiyet uyumlu bakim testi",
            AktifMi = true
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetUzatmaSecenekleriAsync(5371, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });

        Assert.Equal(RezervasyonUzatmaSonucKodlari.MusaitlikYok, result.SonucKodu);
        Assert.Empty(result.Secenekler);
        Assert.Contains("bakim", result.Mesaj, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UzatmaSecenekleri_CheckOutTamamlanmisVeIptalRezervasyonlarSinirVeyaEngelOlusturmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedTwoRoomUzatmaFixtureAsync(dbContext);

        var d0 = new DateTime(2026, 3, 9, 10, 0, 0);
        var d1 = new DateTime(2026, 3, 10, 10, 0, 0);
        var d2 = new DateTime(2026, 3, 11, 10, 0, 0);

        await SeedUzatmaRezervasyonuAsync(dbContext, 5381, 5382, odaId: 100, new DateTime(2026, 3, 8, 14, 0, 0), d0);
        // Gercek/aktif sinir: Oda 100, uzatmanin SON gecesinde (d1-d2) baska rezervasyona bagli.
        await SeedDigerRezervasyonuAsync(dbContext, 5383, 5384, odaId: 100, d1, d2);
        // Oda 101 uzerinde CHECKOUT TAMAMLANMIS ve IPTAL "gorunumlu" kayitlar var - bunlar Oda
        // 101'i GERCEKTE dolu/blokeli GOSTERMEMELI ve tarihleri sinir OLUSTURMAMALIDIR (bkz.
        // GetCurrentOccupancyByRoomAsync ile AYNI durum filtresi).
        await SeedDigerRezervasyonuAsync(dbContext, 5385, 5386, odaId: 101, d0, d1, durum: RezervasyonDurumlari.CheckOutTamamlandi);
        await SeedDigerRezervasyonuAsync(dbContext, 5387, 5388, odaId: 101, d1, d2, durum: RezervasyonDurumlari.Iptal);

        var service = CreateService(dbContext);
        var result = await service.GetUzatmaSecenekleriAsync(5381, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = d2 });

        // Oda 101, CheckOutTamamlandi/Iptal kayitlara ragmen TAMAMEN BOS kabul edilmelidir - bu
        // nedenle TEK segmentli bir CheckoutGunundeOdaDegisimi (Oda 101, tum uzatma boyunca)
        // bulunmalidir; iki-segmentli bir plana GEREK KALMAMALIDIR.
        Assert.Equal(RezervasyonUzatmaSonucKodlari.SecenekBulundu, result.SonucKodu);
        var secim = Assert.Single(result.Secenekler);
        Assert.Equal(RezervasyonUzatmaSenaryoTipleri.CheckoutGunundeOdaDegisimi, secim.SenaryoTipi);
        Assert.Single(secim.Segmentler);
        Assert.Equal(101, Assert.Single(secim.Segmentler[0].OdaAtamalari).OdaId);
    }

    [Fact]
    public async Task UzatmaSecenekleri_IkiSegmentliPlandaAyniOdaFarkliKisiSayisi_AyniKabulEdilmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedPaylasimliUzatmaFixtureAsync(dbContext);

        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var boundary = new DateTime(2026, 3, 10, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 11, 10, 0, 0);

        // Mevcut rezervasyon: 2 kisi, TAMAMEN Oda 101'de.
        await SeedUzatmaRezervasyonuAsync(dbContext, 5231, 5232, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis, kisiSayisi: 2);

        // Oda 102, uzatmanin ILK gecesinde baska rezervasyona bagli (yalnizca ikinci gece kullanilabilir).
        await SeedDigerRezervasyonuAsync(dbContext, 5233, 5234, odaId: 102, cikis, boundary);
        // Oda 101'in 1 yatagi, uzatmanin IKINCI gecesinde BASKA bir konaklayan tarafindan dolduruluyor.
        await SeedDigerPaylasimliRezervasyonuAsync(dbContext, 5235, 5236, odaId: 101, boundary, yeniCikis, KonaklayanCinsiyetleri.Kadin);

        var service = CreateService(dbContext);
        var result = await service.GetUzatmaSecenekleriAsync(5231, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });

        Assert.Equal(RezervasyonUzatmaSonucKodlari.SecenekBulundu, result.SonucKodu);
        var secim = Assert.Single(result.Secenekler, x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.UzatmaSirasindaOdaDegisimi);

        Assert.Equal(2, secim.Segmentler.Count);
        Assert.Equal(101, Assert.Single(secim.Segmentler[0].OdaAtamalari).OdaId);
        Assert.Equal(2, secim.Segmentler[0].OdaAtamalari.Single().AyrilanKisiSayisi);
        Assert.Equal(2, secim.Segmentler[1].OdaAtamalari.Count);
        Assert.Contains(secim.Segmentler[1].OdaAtamalari, a => a.OdaId == 101 && a.AyrilanKisiSayisi == 1);
        Assert.Contains(secim.Segmentler[1].OdaAtamalari, a => a.OdaId == 102 && a.AyrilanKisiSayisi == 1);
        // Oda ID'leri (101) AYNI kalsa da segmentler arasinda ayrilan kisi sayisi degistigi icin
        // bu SIFIR oda degisimi SAYILMAMALIDIR.
        Assert.Equal(1, secim.OdaDegisimSayisi);
    }

    [Fact]
    public async Task UzatmaSecenekleri_MevcutSegmentEksikKisiSayisiIcerirse_AyniOdadaDevamUretilmezVeEksikKisiFiyatlandirilmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedPaylasimliUzatmaFixtureAsync(dbContext);

        var girisTarihi = new DateTime(2026, 3, 8, 14, 0, 0);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);

        // Rezervasyonun KisiSayisi=2 ANCAK mevcut segmentteki oda atamasi yalnizca 1 kisi
        // gosteriyor (eksik/tutarsiz bir dagilim senaryosu - ör. veri girisi hatasi).
        dbContext.Rezervasyonlar.Add(new Rezervasyon
        {
            Id = 5241,
            ReferansNo = "UZT-5241",
            TesisId = 1,
            KisiSayisi = 2,
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
            GirisTarihi = girisTarihi,
            CikisTarihi = cikis,
            MisafirAdiSoyadi = "Eksik Dagilim Test",
            MisafirTelefon = "000",
            ToplamBazUcret = 100m,
            ToplamUcret = 100m,
            ParaBirimi = "TRY",
            RezervasyonDurumu = RezervasyonDurumlari.CheckInTamamlandi,
            AktifMi = true
        });
        dbContext.RezervasyonSegmentleri.Add(new RezervasyonSegment
        {
            Id = 5242,
            RezervasyonId = 5241,
            SegmentSirasi = 1,
            BaslangicTarihi = girisTarihi,
            BitisTarihi = cikis
        });
        dbContext.RezervasyonSegmentOdaAtamalari.Add(new RezervasyonSegmentOdaAtama
        {
            Id = 5243,
            RezervasyonSegmentId = 5242,
            OdaId = 101,
            AyrilanKisiSayisi = 1,
            OdaNoSnapshot = "P-1",
            BinaAdiSnapshot = "Blok",
            OdaTipiAdiSnapshot = "Paylasimli Cift",
            PaylasimliMiSnapshot = true,
            KapasiteSnapshot = 2
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetUzatmaSecenekleriAsync(5241, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });

        Assert.Equal(RezervasyonUzatmaSonucKodlari.SecenekBulundu, result.SonucKodu);
        Assert.DoesNotContain(result.Secenekler, x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam);
        // Bulunan HER alternatif TAM kisi sayisini (2) karsilamalidir - eksik (1 kisilik) dagilim
        // ne secenek olarak sunulur ne de fiyatlandirilir.
        foreach (var secenek in result.Secenekler)
        {
            Assert.Equal(2, secenek.Segmentler.SelectMany(s => s.OdaAtamalari).Sum(a => a.AyrilanKisiSayisi));
        }
    }

    [Fact]
    public async Task UzatmaSecenekleri_BakimBlokuVarAmaDigerOdalarDaDoluysa_MesajYalnizcaBakimiNedenGostermez()
    {
        await using var dbContext = CreateDbContext();
        await SeedTwoRoomUzatmaFixtureAsync(dbContext);

        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);

        await SeedUzatmaRezervasyonuAsync(dbContext, 5251, 5252, odaId: 100, new DateTime(2026, 3, 8, 14, 0, 0), cikis);

        // Mevcut oda (100), uzatma boyunca GERCEK baska bir rezervasyona bagli.
        await SeedDigerRezervasyonuAsync(dbContext, 5253, 5254, odaId: 100, cikis, yeniCikis);

        // Alternatif oda (101), uzatma boyunca HEM gercek baska bir rezervasyona HEM DE
        // (rastlantisal) bir bakim blogluna bagli - blok kaldirilsa DAHI gercek rezervasyon
        // nedeniyle KULLANILAMAZ, dolayisiyla mesaj bakim/arizayi yanlislikla neden GOSTERMEMELIDIR.
        await SeedDigerRezervasyonuAsync(dbContext, 5255, 5256, odaId: 101, cikis, yeniCikis);
        dbContext.OdaKullanimBloklari.Add(new OdaKullanimBlok
        {
            Id = 9101,
            TesisId = 1,
            OdaId = 101,
            BlokTipi = OdaKullanimBlokTipleri.Bakim,
            BaslangicTarihi = cikis,
            BitisTarihi = yeniCikis,
            Aciklama = "Rastlantisal bakim",
            AktifMi = true
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetUzatmaSecenekleriAsync(5251, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });

        Assert.Equal(RezervasyonUzatmaSonucKodlari.MusaitlikYok, result.SonucKodu);
        Assert.Empty(result.Secenekler);
        Assert.DoesNotContain("bakim", result.Mesaj, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UzatmaSecenekleri_BakimBlokuKaldirilinceKurulabilenPlan_BakimaOzguMesajDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedTwoRoomUzatmaFixtureAsync(dbContext);

        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);

        await SeedUzatmaRezervasyonuAsync(dbContext, 5261, 5262, odaId: 100, new DateTime(2026, 3, 8, 14, 0, 0), cikis);

        // Mevcut oda (100), uzatma boyunca GERCEK baska bir rezervasyona bagli.
        await SeedDigerRezervasyonuAsync(dbContext, 5263, 5264, odaId: 100, cikis, yeniCikis);

        // Alternatif oda (101) BASKA HICBIR rezervasyona bagli DEGIL - yalnizca bir bakim blogu
        // var; blok KALDIRILSAYDI bu oda TAM kisi sayisini karsilardi.
        dbContext.OdaKullanimBloklari.Add(new OdaKullanimBlok
        {
            Id = 9102,
            TesisId = 1,
            OdaId = 101,
            BlokTipi = OdaKullanimBlokTipleri.Bakim,
            BaslangicTarihi = cikis,
            BitisTarihi = yeniCikis,
            Aciklama = "Gercek nedenli bakim",
            AktifMi = true
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.GetUzatmaSecenekleriAsync(5261, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });

        Assert.Equal(RezervasyonUzatmaSonucKodlari.MusaitlikYok, result.SonucKodu);
        Assert.Empty(result.Secenekler);
        Assert.Contains("bakim", result.Mesaj, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UzatmaSecenekleri_SonuclarEnFazlaBesAdetVeIcerikDeterministiktir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);

        await SeedUzatmaRezervasyonuAsync(dbContext, 5271, 5272, odaId: 100, new DateTime(2026, 3, 8, 14, 0, 0), cikis);
        // Mevcut oda (100), uzatma boyunca baska bir rezervasyona bagli - COK sayida alternatif
        // (farkli oda tipi/kapasitede) tek segmentli secenek uretilmesi beklenir.
        await SeedDigerRezervasyonuAsync(dbContext, 5273, 5274, odaId: 100, cikis, yeniCikis);

        var service = CreateService(dbContext);
        var request = new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis };

        var sonuc1 = await service.GetUzatmaSecenekleriAsync(5271, request);
        var sonuc2 = await service.GetUzatmaSecenekleriAsync(5271, request);

        Assert.True(sonuc1.Secenekler.Count <= 5);
        Assert.Equal(sonuc1.Secenekler.Select(x => x.SenaryoKodu), sonuc2.Secenekler.Select(x => x.SenaryoKodu));
        Assert.Equal(sonuc1.Secenekler.Select(x => x.SenaryoTipi), sonuc2.Secenekler.Select(x => x.SenaryoTipi));
        for (var i = 0; i < sonuc1.Secenekler.Count; i++)
        {
            Assert.Equal(
                sonuc1.Secenekler[i].Segmentler.SelectMany(s => s.OdaAtamalari).Select(a => (a.OdaId, a.AyrilanKisiSayisi)),
                sonuc2.Secenekler[i].Segmentler.SelectMany(s => s.OdaAtamalari).Select(a => (a.OdaId, a.AyrilanKisiSayisi)));
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Uzatma kaydet (RezervasyonUzatAsync - secilen uzatma senaryosunun atomik kaydedilmesi)
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Uzat_AyniOdadaDevamSecilirse_SonSegmentVeCikisTarihiUzarYeniSegmentOlusturmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);
        await SeedUzatmaRezervasyonuAsync(dbContext, 6001, 6002, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis);

        var service = CreateService(dbContext);
        var secenekler = await service.GetUzatmaSecenekleriAsync(6001, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });
        var secim = secenekler.Secenekler.Single(x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam);

        var sonuc = await service.RezervasyonUzatAsync(6001, new RezervasyonUzatRequestDto { YeniCikisTarihi = yeniCikis, SenaryoKodu = secim.SenaryoKodu });

        Assert.Equal(RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam, sonuc.SenaryoTipi);
        Assert.Equal(yeniCikis, sonuc.YeniCikisTarihi);

        var reservation = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 6001);
        Assert.Equal(yeniCikis, reservation.CikisTarihi);

        var segment = Assert.Single(await dbContext.RezervasyonSegmentleri.Where(x => x.RezervasyonId == 6001).ToListAsync());
        Assert.Equal(yeniCikis, segment.BitisTarihi);
    }

    [Fact]
    public async Task Uzat_CheckoutGunundeOdaDegisimiSecilirse_EskiCikistaBaslayanTekYeniSegmentOlusturulur()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);

        await SeedUzatmaRezervasyonuAsync(dbContext, 6011, 6012, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis);
        await SeedDigerRezervasyonuAsync(dbContext, 6013, 6014, odaId: 101, cikis, yeniCikis);

        var service = CreateService(dbContext);
        var secenekler = await service.GetUzatmaSecenekleriAsync(6011, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });
        var secim = secenekler.Secenekler.First(x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.CheckoutGunundeOdaDegisimi);

        await service.RezervasyonUzatAsync(6011, new RezervasyonUzatRequestDto { YeniCikisTarihi = yeniCikis, SenaryoKodu = secim.SenaryoKodu });

        var segmentler = await dbContext.RezervasyonSegmentleri.Where(x => x.RezervasyonId == 6011).OrderBy(x => x.SegmentSirasi).ToListAsync();
        Assert.Equal(2, segmentler.Count);
        Assert.Equal(cikis, segmentler[0].BitisTarihi);
        Assert.Equal(cikis, segmentler[1].BaslangicTarihi);
        Assert.Equal(yeniCikis, segmentler[1].BitisTarihi);
        Assert.Equal(2, segmentler[1].SegmentSirasi);
    }

    [Fact]
    public async Task Uzat_UzatmaSirasindaOdaDegisimiSecilirse_GercekSinirdaIkiYeniSegmentOlusurBoslukVeCakismaOlmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedTwoRoomUzatmaFixtureAsync(dbContext);

        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var boundary = new DateTime(2026, 3, 10, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 11, 10, 0, 0);

        await SeedUzatmaRezervasyonuAsync(dbContext, 6021, 6022, odaId: 100, new DateTime(2026, 3, 8, 14, 0, 0), cikis);
        await SeedDigerRezervasyonuAsync(dbContext, 6023, 6024, odaId: 100, cikis, boundary);
        await SeedDigerRezervasyonuAsync(dbContext, 6025, 6026, odaId: 101, boundary, yeniCikis);

        var service = CreateService(dbContext);
        var secenekler = await service.GetUzatmaSecenekleriAsync(6021, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });
        var secim = Assert.Single(secenekler.Secenekler, x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.UzatmaSirasindaOdaDegisimi);

        await service.RezervasyonUzatAsync(6021, new RezervasyonUzatRequestDto { YeniCikisTarihi = yeniCikis, SenaryoKodu = secim.SenaryoKodu });

        var segmentler = await dbContext.RezervasyonSegmentleri.Where(x => x.RezervasyonId == 6021).OrderBy(x => x.SegmentSirasi).ToListAsync();
        Assert.Equal(3, segmentler.Count);
        Assert.Equal(cikis, segmentler[0].BitisTarihi);
        Assert.Equal(cikis, segmentler[1].BaslangicTarihi);
        Assert.Equal(boundary, segmentler[1].BitisTarihi);
        Assert.Equal(boundary, segmentler[2].BaslangicTarihi);
        Assert.Equal(yeniCikis, segmentler[2].BitisTarihi);
        Assert.Equal(2, segmentler[1].SegmentSirasi);
        Assert.Equal(3, segmentler[2].SegmentSirasi);

        var reservation = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 6021);
        Assert.Equal(yeniCikis, reservation.CikisTarihi);
    }

    [Fact]
    public async Task Uzat_HerSegmentteToplamAyrilanKisiSayisiRezervasyonKisiSayisinaEsittir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);

        await SeedUzatmaRezervasyonuAsync(dbContext, 6031, 6032, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis, kisiSayisi: 2);
        await SeedDigerRezervasyonuAsync(dbContext, 6033, 6034, odaId: 101, cikis, yeniCikis, ayrilanKisiSayisi: 2);

        var service = CreateService(dbContext);
        var secenekler = await service.GetUzatmaSecenekleriAsync(6031, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });
        var secim = secenekler.Secenekler.First(x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.CheckoutGunundeOdaDegisimi);

        await service.RezervasyonUzatAsync(6031, new RezervasyonUzatRequestDto { YeniCikisTarihi = yeniCikis, SenaryoKodu = secim.SenaryoKodu });

        var segmentler = await dbContext.RezervasyonSegmentleri
            .Include(x => x.OdaAtamalari)
            .Where(x => x.RezervasyonId == 6031)
            .ToListAsync();

        Assert.NotEmpty(segmentler);
        Assert.All(segmentler, segment => Assert.Equal(2, segment.OdaAtamalari.Sum(x => x.AyrilanKisiSayisi)));
    }

    [Fact]
    public async Task Uzat_AktifKonaklayanIcinYeniSegmentteOdaAtamasiOlusur()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);

        await SeedUzatmaRezervasyonuAsync(dbContext, 6041, 6042, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis);
        await SeedDigerRezervasyonuAsync(dbContext, 6043, 6044, odaId: 101, cikis, yeniCikis);

        var service = CreateService(dbContext);
        var secenekler = await service.GetUzatmaSecenekleriAsync(6041, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });
        var secim = secenekler.Secenekler.First(x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.CheckoutGunundeOdaDegisimi);
        var yeniOdaId = secim.Segmentler[0].OdaAtamalari.Single().OdaId;

        await service.RezervasyonUzatAsync(6041, new RezervasyonUzatRequestDto { YeniCikisTarihi = yeniCikis, SenaryoKodu = secim.SenaryoKodu });

        var yeniSegment = await dbContext.RezervasyonSegmentleri.Where(x => x.RezervasyonId == 6041).OrderByDescending(x => x.SegmentSirasi).FirstAsync();
        var guestAtama = await dbContext.RezervasyonKonaklayanSegmentAtamalari.SingleAsync(x => x.RezervasyonSegmentId == yeniSegment.Id);

        Assert.Equal(yeniOdaId, guestAtama.OdaId);
    }

    [Fact]
    public async Task Uzat_PaylasimliOdaGecisindeYatakCakismasiOlusmazVeKapasiteIcindeAtanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedPaylasimliUzatmaFixtureAsync(dbContext);
        dbContext.Odalar.Add(new Oda { Id = 103, OdaNo = "P-2", BinaId = 10, TesisOdaTipiId = 30, KatNo = 1, AktifMi = true });
        await dbContext.SaveChangesAsync();

        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);

        await SeedUzatmaRezervasyonuAsync(dbContext, 6051, 6052, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis, cinsiyet: KonaklayanCinsiyetleri.Kadin);
        // Mevcut oda (101), uzatma boyunca ayni cinsiyetten baska bir rezervasyona bagli.
        await SeedDigerPaylasimliRezervasyonuAsync(dbContext, 6053, 6054, odaId: 101, cikis, yeniCikis, KonaklayanCinsiyetleri.Kadin);
        // Alternatif paylasimli oda 103'un 1 numarali yataginda, uzatma boyunca ayni cinsiyetten baska bir misafir var.
        await SeedDigerPaylasimliRezervasyonuAsync(dbContext, 6055, 6056, odaId: 103, cikis, yeniCikis, KonaklayanCinsiyetleri.Kadin);
        var digerAtama = await dbContext.RezervasyonKonaklayanSegmentAtamalari.SingleAsync(x => x.RezervasyonSegmentId == 6056);
        digerAtama.YatakNo = 1;
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var secenekler = await service.GetUzatmaSecenekleriAsync(6051, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });
        var secim = secenekler.Secenekler.First(x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.CheckoutGunundeOdaDegisimi);
        var hedefOda = secim.Segmentler[0].OdaAtamalari.Single();

        await service.RezervasyonUzatAsync(6051, new RezervasyonUzatRequestDto { YeniCikisTarihi = yeniCikis, SenaryoKodu = secim.SenaryoKodu });

        var yeniSegment = await dbContext.RezervasyonSegmentleri.Where(x => x.RezervasyonId == 6051).OrderByDescending(x => x.SegmentSirasi).FirstAsync();
        var yeniAtama = await dbContext.RezervasyonKonaklayanSegmentAtamalari.SingleAsync(x => x.RezervasyonSegmentId == yeniSegment.Id);

        Assert.Equal(hedefOda.OdaId, yeniAtama.OdaId);

        if (hedefOda.PaylasimliMi)
        {
            Assert.NotNull(yeniAtama.YatakNo);

            var digerYataklar = await dbContext.RezervasyonKonaklayanSegmentAtamalari
                .Where(x => x.OdaId == hedefOda.OdaId && x.RezervasyonSegmentId != yeniSegment.Id && x.YatakNo.HasValue)
                .Select(x => x.YatakNo)
                .ToListAsync();
            Assert.DoesNotContain(yeniAtama.YatakNo, digerYataklar);
        }
        else
        {
            Assert.Null(yeniAtama.YatakNo);
        }
    }

    [Fact]
    public async Task Uzat_SegmentGecisindeMumkunOlanKonaklayanAyniOdadaKalirYalnizcaGerekenTasinir()
    {
        await using var dbContext = CreateDbContext();
        await SeedPaylasimliUzatmaFixtureAsync(dbContext);

        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);

        await SeedUzatmaRezervasyonuAsync(dbContext, 6071, 6072, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis, kisiSayisi: 2);

        dbContext.RezervasyonKonaklayanlar.AddRange(
            new RezervasyonKonaklayan { Id = 607101, RezervasyonId = 6071, SiraNo = 1, AdSoyad = "Misafir A", Cinsiyet = KonaklayanCinsiyetleri.Kadin, KatilimDurumu = KonaklayanKatilimDurumlari.Geldi },
            new RezervasyonKonaklayan { Id = 607102, RezervasyonId = 6071, SiraNo = 2, AdSoyad = "Misafir B", Cinsiyet = KonaklayanCinsiyetleri.Kadin, KatilimDurumu = KonaklayanKatilimDurumlari.Geldi });
        dbContext.RezervasyonKonaklayanSegmentAtamalari.AddRange(
            new RezervasyonKonaklayanSegmentAtama { Id = 607111, RezervasyonKonaklayanId = 607101, RezervasyonSegmentId = 6072, OdaId = 101, YatakNo = 1 },
            new RezervasyonKonaklayanSegmentAtama { Id = 607112, RezervasyonKonaklayanId = 607102, RezervasyonSegmentId = 6072, OdaId = 101, YatakNo = 2 });
        await dbContext.SaveChangesAsync();

        // Uzatma boyunca oda 101'in 1 yatagi baska (ayni cinsiyet) bir misafir tarafindan
        // dolduruluyor - geriye 1 kisilik yer kaliyor, tam plan icin oda 102 gerekiyor.
        await SeedDigerPaylasimliRezervasyonuAsync(dbContext, 6073, 6074, odaId: 101, cikis, yeniCikis, KonaklayanCinsiyetleri.Kadin);

        var service = CreateService(dbContext);
        var secenekler = await service.GetUzatmaSecenekleriAsync(6071, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });
        var secim = secenekler.Secenekler.Single(x =>
            x.Segmentler.SelectMany(s => s.OdaAtamalari).Any(a => a.OdaId == 101) &&
            x.Segmentler.SelectMany(s => s.OdaAtamalari).Any(a => a.OdaId == 102));

        await service.RezervasyonUzatAsync(6071, new RezervasyonUzatRequestDto { YeniCikisTarihi = yeniCikis, SenaryoKodu = secim.SenaryoKodu });

        var yeniSegment = await dbContext.RezervasyonSegmentleri.Where(x => x.RezervasyonId == 6071).OrderByDescending(x => x.SegmentSirasi).FirstAsync();
        var yeniAtamalar = await dbContext.RezervasyonKonaklayanSegmentAtamalari.Where(x => x.RezervasyonSegmentId == yeniSegment.Id).ToListAsync();

        Assert.Equal(2, yeniAtamalar.Count);
        Assert.Contains(yeniAtamalar, x => x.OdaId == 101);
        Assert.Contains(yeniAtamalar, x => x.OdaId == 102);
        // Onceki odasinda kalabilecek slot ilk ONCELIK sirasindaki (dusuk SiraNo) konaklayana verilir.
        Assert.Equal(607101, yeniAtamalar.Single(x => x.OdaId == 101).RezervasyonKonaklayanId);
        Assert.Equal(607102, yeniAtamalar.Single(x => x.OdaId == 102).RezervasyonKonaklayanId);
    }

    [Fact]
    public async Task Uzat_ToplamUcretlereYalnizcaYenidenHesaplananUzatmaTutariEklenir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);
        await SeedUzatmaRezervasyonuAsync(dbContext, 6081, 6082, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis);

        var service = CreateService(dbContext);
        var secenekler = await service.GetUzatmaSecenekleriAsync(6081, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });
        var secim = secenekler.Secenekler.Single(x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam);
        Assert.True(secim.EkNihaiUcret > 0);

        var sonuc = await service.RezervasyonUzatAsync(6081, new RezervasyonUzatRequestDto { YeniCikisTarihi = yeniCikis, SenaryoKodu = secim.SenaryoKodu });

        var reservation = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 6081);
        Assert.Equal(100m + secim.EkBazUcret, reservation.ToplamBazUcret);
        Assert.Equal(100m + secim.EkNihaiUcret, reservation.ToplamUcret);
        Assert.Equal(reservation.ToplamBazUcret, sonuc.YeniToplamBazUcret);
        Assert.Equal(reservation.ToplamUcret, sonuc.YeniToplamUcret);
    }

    [Fact]
    public async Task Uzat_MevcutIndirimTekrarUygulanmazVeEskiIndirimKaydiKorunur()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);
        await SeedUzatmaRezervasyonuAsync(dbContext, 6091, 6092, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis);

        var reservation = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 6091);
        var indirimJson = JsonSerializer.Serialize(new List<UygulananIndirimDto>
        {
            new() { IndirimKuraliId = 0, KuralAdi = "Manuel 50", IndirimTutari = 50m, SonrasiTutar = 50m }
        });
        reservation.UygulananIndirimlerJson = indirimJson;
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var secenekler = await service.GetUzatmaSecenekleriAsync(6091, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });
        var secim = secenekler.Secenekler.Single(x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam);
        Assert.NotNull(secim.FiyatUyarisi);

        var eskiToplamUcret = reservation.ToplamUcret;
        await service.RezervasyonUzatAsync(6091, new RezervasyonUzatRequestDto { YeniCikisTarihi = yeniCikis, SenaryoKodu = secim.SenaryoKodu });

        var guncelReservation = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 6091);
        Assert.Equal(indirimJson, guncelReservation.UygulananIndirimlerJson);
        Assert.Equal(eskiToplamUcret + secim.EkNihaiUcret, guncelReservation.ToplamUcret);
    }

    [Fact]
    public async Task Uzat_MevcutTahsilatKayitlariDegismezVeYeniTahsilatOlusturulmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);
        await SeedUzatmaRezervasyonuAsync(dbContext, 6101, 6102, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis);

        var odemeSayisiOnce = await dbContext.RezervasyonOdemeler.CountAsync(x => x.RezervasyonId == 6101);

        var service = CreateService(dbContext);
        var secenekler = await service.GetUzatmaSecenekleriAsync(6101, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });
        var secim = secenekler.Secenekler.Single(x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam);

        await service.RezervasyonUzatAsync(6101, new RezervasyonUzatRequestDto { YeniCikisTarihi = yeniCikis, SenaryoKodu = secim.SenaryoKodu });

        var odemeSayisiSonra = await dbContext.RezervasyonOdemeler.CountAsync(x => x.RezervasyonId == 6101);
        Assert.Equal(odemeSayisiOnce, odemeSayisiSonra);
    }

    [Fact]
    public async Task Uzat_SadeceUzatmaDonemininKonaklamaHaklariEklenirMukerrerOlusmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var girisTarihi = new DateTime(2026, 3, 8, 14, 0, 0);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        // Iki gecelik uzatma (3/9 VE 3/10) kullanilir ki eski cikis gunu (3/9), yeni konaklamada
        // ARTIK son gece olmasin - boylece "eski cikis gununun ara gun haline gelmesi" senaryosu
        // GERCEKTEN test edilsin (CheckOutGunuGecerliMi=false kurali artik yalnizca YENI son gece
        // olan 3/10'a uygulanmali, 3/9'a degil).
        var yeniCikis = new DateTime(2026, 3, 11, 10, 0, 0);
        await SeedUzatmaRezervasyonuAsync(dbContext, 6111, 6112, odaId: 101, girisTarihi, cikis);

        dbContext.KonaklamaTipiIcerikKalemleri.Add(new KonaklamaTipiIcerikKalemi
        {
            Id = 61121,
            KonaklamaTipiId = 1,
            HizmetKodu = KonaklamaTipiIcerikHizmetKodlari.Kahvalti,
            Miktar = 1,
            Periyot = KonaklamaTipiIcerikPeriyotlari.Gunluk,
            CheckInGunuGecerliMi = true,
            CheckOutGunuGecerliMi = false
        });
        // Giris gunu icin ONCEDEN uretilmis hak - mukerrer OLUSTURULMAMALI.
        dbContext.RezervasyonKonaklamaHaklari.Add(new RezervasyonKonaklamaHakki
        {
            Id = 61122,
            RezervasyonId = 6111,
            HizmetKodu = KonaklamaTipiIcerikHizmetKodlari.Kahvalti,
            HizmetAdiSnapshot = "Kahvalti",
            Miktar = 1,
            Periyot = KonaklamaTipiIcerikPeriyotlari.Gunluk,
            KullanimTipi = KonaklamaTipiIcerikKullanimTipleri.Adetli,
            KullanimNoktasi = KonaklamaTipiIcerikKullanimNoktalari.Restoran,
            CheckInGunuGecerliMi = true,
            CheckOutGunuGecerliMi = false,
            HakTarihi = girisTarihi.Date,
            Durum = RezervasyonKonaklamaHakDurumlari.Bekliyor,
            AktifMi = true
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var secenekler = await service.GetUzatmaSecenekleriAsync(6111, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });
        var secim = secenekler.Secenekler.Single(x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam);

        await service.RezervasyonUzatAsync(6111, new RezervasyonUzatRequestDto { YeniCikisTarihi = yeniCikis, SenaryoKodu = secim.SenaryoKodu });

        var haklar = await dbContext.RezervasyonKonaklamaHaklari.Where(x => x.RezervasyonId == 6111).OrderBy(x => x.HakTarihi).ToListAsync();

        Assert.Equal(2, haklar.Count);
        Assert.Contains(haklar, x => x.HakTarihi == girisTarihi.Date);
        Assert.Contains(haklar, x => x.HakTarihi == cikis.Date);
    }

    [Fact]
    public async Task Uzat_DegisiklikGecmisindeEskiYeniTarihVeFiyatlarVeSegmentPlaniBulunur()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);
        await SeedUzatmaRezervasyonuAsync(dbContext, 6121, 6122, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis);

        var service = CreateService(dbContext);
        var secenekler = await service.GetUzatmaSecenekleriAsync(6121, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });
        var secim = secenekler.Secenekler.Single(x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam);

        await service.RezervasyonUzatAsync(6121, new RezervasyonUzatRequestDto { YeniCikisTarihi = yeniCikis, SenaryoKodu = secim.SenaryoKodu });

        var kayit = await dbContext.RezervasyonDegisiklikGecmisleri.SingleAsync(x => x.RezervasyonId == 6121 && x.IslemTipi == RezervasyonGecmisIslemTipleri.Uzatildi);

        Assert.Contains(cikis.ToString("dd.MM.yyyy"), kayit.Aciklama);
        Assert.Contains(yeniCikis.ToString("dd.MM.yyyy"), kayit.Aciklama);
        Assert.Contains(secim.SenaryoTipi, kayit.Aciklama);
        Assert.Contains("EskiCikisTarihi", kayit.OncekiDegerJson);
        Assert.Contains("YeniCikisTarihi", kayit.YeniDegerJson);
        Assert.Contains(secim.SenaryoKodu, kayit.YeniDegerJson);
    }

    [Fact]
    public async Task Uzat_SecenekGosterildiktenSonraOdaDolarsaKaydetme409DonerVeDegisiklikBirakmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);
        await SeedUzatmaRezervasyonuAsync(dbContext, 6131, 6132, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis);

        var service = CreateService(dbContext);
        var secenekler = await service.GetUzatmaSecenekleriAsync(6131, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });
        var secim = secenekler.Secenekler.Single(x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam);

        // Secenekler gosterildikten SONRA, oda 101 baska bir rezervasyona baglanir (musaitlik degisir).
        await SeedDigerRezervasyonuAsync(dbContext, 6133, 6134, odaId: 101, cikis, yeniCikis);

        var segmentSayisiOnce = await dbContext.RezervasyonSegmentleri.CountAsync(x => x.RezervasyonId == 6131);
        var eskiToplamUcret = (await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 6131)).ToplamUcret;

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.RezervasyonUzatAsync(6131, new RezervasyonUzatRequestDto { YeniCikisTarihi = yeniCikis, SenaryoKodu = secim.SenaryoKodu }));

        Assert.Equal(409, ex.ErrorCode);

        var reservation = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 6131);
        Assert.Equal(cikis, reservation.CikisTarihi);
        Assert.Equal(eskiToplamUcret, reservation.ToplamUcret);
        Assert.Equal(segmentSayisiOnce, await dbContext.RezervasyonSegmentleri.CountAsync(x => x.RezervasyonId == 6131));
    }

    [Fact]
    public void Uzat_SenaryoKodu_IcerigeGoreDegisirSiraNumarasinaGoreDegismez()
    {
        var method = typeof(RezervasyonService).GetMethod("CreateUzatmaPlanKodu", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("CreateUzatmaPlanKodu metodu bulunamadi.");

        var segmentA = new List<KonaklamaSenaryoSegmentDto>
        {
            new() { BaslangicTarihi = new DateTime(2026, 3, 9), BitisTarihi = new DateTime(2026, 3, 10), OdaAtamalari = [UzatmaOdaAtamasi(101, 1)] }
        };
        var segmentB = new List<KonaklamaSenaryoSegmentDto>
        {
            new() { BaslangicTarihi = new DateTime(2026, 3, 9), BitisTarihi = new DateTime(2026, 3, 10), OdaAtamalari = [UzatmaOdaAtamasi(102, 1)] }
        };

        var kod1 = (string)method.Invoke(null, [1, new DateTime(2026, 3, 9), new DateTime(2026, 3, 10), segmentA])!;
        var kod2 = (string)method.Invoke(null, [1, new DateTime(2026, 3, 9), new DateTime(2026, 3, 10), segmentA])!;
        var kod3 = (string)method.Invoke(null, [1, new DateTime(2026, 3, 9), new DateTime(2026, 3, 10), segmentB])!;

        Assert.Equal(kod1, kod2);
        Assert.NotEqual(kod1, kod3);
    }

    [Fact]
    public async Task Uzat_MusaitlikDegisincePlanFarklilastigindaAyniSiraNumarasindakiKodDaFarklilasir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);
        await SeedUzatmaRezervasyonuAsync(dbContext, 6141, 6142, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis);

        var service = CreateService(dbContext);
        var oncekiSecenekler = await service.GetUzatmaSecenekleriAsync(6141, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });
        var oncekiIlkKod = oncekiSecenekler.Secenekler[0].SenaryoKodu;

        // Musaitlik degisir: oda 101 artik uzatma boyunca baska bir rezervasyona bagli - ilk
        // siradaki plan artik BASKA bir plandir.
        await SeedDigerRezervasyonuAsync(dbContext, 6143, 6144, odaId: 101, cikis, yeniCikis);

        var yeniSecenekler = await service.GetUzatmaSecenekleriAsync(6141, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });
        var yeniIlkKod = yeniSecenekler.Secenekler[0].SenaryoKodu;

        Assert.NotEqual(oncekiIlkKod, yeniIlkKod);
    }

    [Fact]
    public async Task Uzat_IstemcidenGelenFiyatVeyaSegmentBilgisiKullanilmaz()
    {
        var properties = typeof(RezervasyonUzatRequestDto).GetProperties().Select(p => p.Name).OrderBy(x => x).ToList();
        Assert.Equal(["SenaryoKodu", "YeniCikisTarihi"], properties);

        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);
        await SeedUzatmaRezervasyonuAsync(dbContext, 6151, 6152, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis);

        var service = CreateService(dbContext);
        var secenekler = await service.GetUzatmaSecenekleriAsync(6151, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });
        var secim = secenekler.Secenekler.Single(x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam);

        var sonuc = await service.RezervasyonUzatAsync(6151, new RezervasyonUzatRequestDto { YeniCikisTarihi = yeniCikis, SenaryoKodu = secim.SenaryoKodu });

        Assert.Equal(secim.EkNihaiUcret, sonuc.EkNihaiUcret);
        Assert.Equal(secim.EkBazUcret, sonuc.EkBazUcret);
    }

    [Fact]
    public async Task Uzat_GecersizTarihte400Doner()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        await SeedUzatmaRezervasyonuAsync(dbContext, 6161, 6162, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis);

        var service = CreateService(dbContext);
        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.RezervasyonUzatAsync(6161, new RezervasyonUzatRequestDto { YeniCikisTarihi = cikis, SenaryoKodu = "UZT-000000000000" }));

        Assert.Equal(400, ex.ErrorCode);
    }

    [Fact]
    public async Task Uzat_CheckInTamamlandiDisindaDurumda400Doner()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);
        await SeedUzatmaRezervasyonuAsync(
            dbContext, 6171, 6172, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis,
            durum: RezervasyonDurumlari.Onayli);

        var service = CreateService(dbContext);
        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.RezervasyonUzatAsync(6171, new RezervasyonUzatRequestDto { YeniCikisTarihi = yeniCikis, SenaryoKodu = "UZT-000000000000" }));

        Assert.Equal(400, ex.ErrorCode);
    }

    [Fact]
    public async Task Uzat_TenantErisimIhlaliReddedilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);
        await SeedUzatmaRezervasyonuAsync(dbContext, 6181, 6182, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis);

        var service = CreateService(dbContext, currentTenantAccessor: new FakeNonSuperAdminTenantAccessor(2));
        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.RezervasyonUzatAsync(6181, new RezervasyonUzatRequestDto { YeniCikisTarihi = yeniCikis, SenaryoKodu = "UZT-000000000000" }));

        Assert.Equal(404, ex.ErrorCode);
    }

    [Fact]
    public async Task Uzat_IkinciAyniIstek_UcretVeSegmentleriCogaltmazEnAzindan409Doner()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);
        await SeedUzatmaRezervasyonuAsync(dbContext, 6191, 6192, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis);

        var service = CreateService(dbContext);
        var secenekler = await service.GetUzatmaSecenekleriAsync(6191, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });
        var secim = secenekler.Secenekler.Single(x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam);
        var request = new RezervasyonUzatRequestDto { YeniCikisTarihi = yeniCikis, SenaryoKodu = secim.SenaryoKodu };

        await service.RezervasyonUzatAsync(6191, request);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.RezervasyonUzatAsync(6191, request));
        Assert.Equal(409, ex.ErrorCode);

        var reservation = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 6191);
        Assert.Equal(yeniCikis, reservation.CikisTarihi);
        Assert.Equal(100m + secim.EkBazUcret, reservation.ToplamBazUcret);
        Assert.Equal(1, await dbContext.RezervasyonSegmentleri.CountAsync(x => x.RezervasyonId == 6191));
    }

    [Fact]
    public async Task Uzat_ParaBirimiTutarsizsaHataOlusurVeHicbirDegisiklikKaydedilmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);
        await SeedUzatmaRezervasyonuAsync(dbContext, 6201, 6202, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis);

        var service = CreateService(dbContext);
        var secenekler = await service.GetUzatmaSecenekleriAsync(6201, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });
        var secim = secenekler.Secenekler.Single(x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam);

        var reservation = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 6201);
        reservation.ParaBirimi = "USD";
        await dbContext.SaveChangesAsync();

        var segmentSayisiOnce = await dbContext.RezervasyonSegmentleri.CountAsync(x => x.RezervasyonId == 6201);

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            service.RezervasyonUzatAsync(6201, new RezervasyonUzatRequestDto { YeniCikisTarihi = yeniCikis, SenaryoKodu = secim.SenaryoKodu }));

        Assert.Equal(400, ex.ErrorCode);

        var reservationAfter = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 6201);
        Assert.Equal(cikis, reservationAfter.CikisTarihi);
        Assert.Equal(segmentSayisiOnce, await dbContext.RezervasyonSegmentleri.CountAsync(x => x.RezervasyonId == 6201));
        Assert.Empty(await dbContext.RezervasyonDegisiklikGecmisleri.Where(x => x.RezervasyonId == 6201 && x.IslemTipi == RezervasyonGecmisIslemTipleri.Uzatildi).ToListAsync());
    }

    [Fact]
    public async Task Uzat_BasariliIslemSonrasi_EskiCikisTarihindenBaslayanAyniDonemTekrarUzatmaOlarakSunulmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 10, 10, 0, 0);
        await SeedUzatmaRezervasyonuAsync(dbContext, 6211, 6212, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), cikis);

        var service = CreateService(dbContext);
        var secenekler = await service.GetUzatmaSecenekleriAsync(6211, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });
        var secim = secenekler.Secenekler.Single(x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam);

        await service.RezervasyonUzatAsync(6211, new RezervasyonUzatRequestDto { YeniCikisTarihi = yeniCikis, SenaryoKodu = secim.SenaryoKodu });

        await Assert.ThrowsAsync<BaseException>(() =>
            service.GetUzatmaSecenekleriAsync(6211, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis }));
    }

    [Fact]
    public async Task UzatmaSecenekleri_SenaryoKoduArtikUZTOnEkiyleBaslarVeSaltOkunurDavranisBozulmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedReservationFixtureWithTenRoomsAsync(dbContext);
        await SeedUzatmaRezervasyonuAsync(dbContext, 6221, 6222, odaId: 101, new DateTime(2026, 3, 8, 14, 0, 0), new DateTime(2026, 3, 9, 10, 0, 0));

        var service = CreateService(dbContext);
        var result = await service.GetUzatmaSecenekleriAsync(6221, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = new DateTime(2026, 3, 10, 10, 0, 0) });

        Assert.NotEmpty(result.Secenekler);
        Assert.All(result.Secenekler, x => Assert.StartsWith("UZT-", x.SenaryoKodu));

        Assert.Equal(0, await dbContext.RezervasyonSegmentleri.CountAsync(x => x.RezervasyonId == 6221 && x.SegmentSirasi > 1));
        var reservation = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == 6221);
        Assert.Equal(new DateTime(2026, 3, 9, 10, 0, 0), reservation.CikisTarihi);
    }

    [Fact]
    public async Task UzatmaSecenekleri_SadeceCheckOutTamamlanmisKendineOzguTarihFazladanBolmeNoktasiOlusturmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedTwoRoomUzatmaFixtureAsync(dbContext);
        var girisTarihi = new DateTime(2026, 3, 8, 14, 0, 0);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var boundary = new DateTime(2026, 3, 10, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 11, 10, 0, 0);

        await SeedUzatmaRezervasyonuAsync(dbContext, 6231, 6232, odaId: 100, girisTarihi, cikis);
        await SeedDigerRezervasyonuAsync(dbContext, 6233, 6234, odaId: 100, cikis, boundary);
        await SeedDigerRezervasyonuAsync(dbContext, 6235, 6236, odaId: 101, boundary, yeniCikis);
        // Kendi tarih araligi CheckOutTamamlandi oldugu icin GERCEK bir sinir OLUSTURMAMALIDIR -
        // farkli (yanlis) bir bolme noktasi eklememelidir.
        await SeedDigerRezervasyonuAsync(dbContext, 6237, 6238, odaId: 100, boundary.AddHours(5), yeniCikis, durum: RezervasyonDurumlari.CheckOutTamamlandi);

        var service = CreateService(dbContext);
        var result = await service.GetUzatmaSecenekleriAsync(6231, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });

        var secim = Assert.Single(result.Secenekler, x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.UzatmaSirasindaOdaDegisimi);
        Assert.Equal(2, secim.Segmentler.Count);
        Assert.Equal(boundary, secim.Segmentler[0].BitisTarihi);
    }

    [Fact]
    public async Task UzatmaSecenekleri_SadeceIptalKendineOzguTarihFazladanBolmeNoktasiOlusturmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedTwoRoomUzatmaFixtureAsync(dbContext);
        var girisTarihi = new DateTime(2026, 3, 8, 14, 0, 0);
        var cikis = new DateTime(2026, 3, 9, 10, 0, 0);
        var boundary = new DateTime(2026, 3, 10, 10, 0, 0);
        var yeniCikis = new DateTime(2026, 3, 11, 10, 0, 0);

        await SeedUzatmaRezervasyonuAsync(dbContext, 6241, 6242, odaId: 100, girisTarihi, cikis);
        await SeedDigerRezervasyonuAsync(dbContext, 6243, 6244, odaId: 100, cikis, boundary);
        await SeedDigerRezervasyonuAsync(dbContext, 6245, 6246, odaId: 101, boundary, yeniCikis);
        // Kendi tarih araligi Iptal oldugu icin GERCEK bir sinir OLUSTURMAMALIDIR.
        await SeedDigerRezervasyonuAsync(dbContext, 6247, 6248, odaId: 100, boundary.AddHours(5), yeniCikis, durum: RezervasyonDurumlari.Iptal);

        var service = CreateService(dbContext);
        var result = await service.GetUzatmaSecenekleriAsync(6241, new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikis });

        var secim = Assert.Single(result.Secenekler, x => x.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.UzatmaSirasindaOdaDegisimi);
        Assert.Equal(2, secim.Segmentler.Count);
        Assert.Equal(boundary, secim.Segmentler[0].BitisTarihi);
    }

    private static KonaklamaSenaryoOdaAtamaDto UzatmaOdaAtamasi(int odaId, int ayrilanKisiSayisi) =>
        new() { OdaId = odaId, AyrilanKisiSayisi = ayrilanKisiSayisi };

    private static int InvokeCalculateRoomChangeCount(
        List<KonaklamaSenaryoOdaAtamaDto> oncekiAtamalar,
        List<KonaklamaSenaryoOdaAtamaDto> sonrakiAtamalar)
    {
        var method = typeof(RezervasyonService).GetMethod("CalculateRoomChangeCount", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("CalculateRoomChangeCount metodu bulunamadi.");
        return (int)method.Invoke(null, [oncekiAtamalar, sonrakiAtamalar])!;
    }

    private static async Task<int> SeedUzatmaRezervasyonuAsync(
        StysAppDbContext dbContext,
        int rezervasyonId,
        int segmentId,
        int odaId,
        DateTime girisTarihi,
        DateTime cikisTarihi,
        int kisiSayisi = 1,
        string? cinsiyet = null,
        string durum = RezervasyonDurumlari.CheckInTamamlandi)
    {
        var oda = await (
                from o in dbContext.Odalar
                join b in dbContext.Binalar on o.BinaId equals b.Id
                join t in dbContext.OdaTipleri on o.TesisOdaTipiId equals t.Id
                where o.Id == odaId
                select new { o.OdaNo, BinaAdi = b.Ad, OdaTipiAdi = t.Ad, t.PaylasimliMi, t.Kapasite })
            .SingleAsync();

        dbContext.Rezervasyonlar.Add(new Rezervasyon
        {
            Id = rezervasyonId,
            ReferansNo = $"UZT-{rezervasyonId}",
            TesisId = 1,
            KisiSayisi = kisiSayisi,
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
            GirisTarihi = girisTarihi,
            CikisTarihi = cikisTarihi,
            MisafirAdiSoyadi = "Uzatma Test",
            MisafirTelefon = "000",
            ToplamBazUcret = 100m,
            ToplamUcret = 100m,
            ParaBirimi = "TRY",
            RezervasyonDurumu = durum,
            AktifMi = true
        });

        dbContext.RezervasyonSegmentleri.Add(new RezervasyonSegment
        {
            Id = segmentId,
            RezervasyonId = rezervasyonId,
            SegmentSirasi = 1,
            BaslangicTarihi = girisTarihi,
            BitisTarihi = cikisTarihi
        });

        dbContext.RezervasyonSegmentOdaAtamalari.Add(new RezervasyonSegmentOdaAtama
        {
            Id = segmentId + 1,
            RezervasyonSegmentId = segmentId,
            OdaId = odaId,
            AyrilanKisiSayisi = kisiSayisi,
            OdaNoSnapshot = oda.OdaNo,
            BinaAdiSnapshot = oda.BinaAdi,
            OdaTipiAdiSnapshot = oda.OdaTipiAdi,
            PaylasimliMiSnapshot = oda.PaylasimliMi,
            KapasiteSnapshot = oda.Kapasite
        });

        if (kisiSayisi == 1)
        {
            dbContext.RezervasyonKonaklayanlar.Add(new RezervasyonKonaklayan
            {
                Id = rezervasyonId * 100 + 1,
                RezervasyonId = rezervasyonId,
                SiraNo = 1,
                AdSoyad = "Test Misafir",
                Cinsiyet = cinsiyet,
                KatilimDurumu = KonaklayanKatilimDurumlari.Geldi
            });

            dbContext.RezervasyonKonaklayanSegmentAtamalari.Add(new RezervasyonKonaklayanSegmentAtama
            {
                Id = rezervasyonId * 100 + 2,
                RezervasyonKonaklayanId = rezervasyonId * 100 + 1,
                RezervasyonSegmentId = segmentId,
                OdaId = odaId
            });
        }

        await dbContext.SaveChangesAsync();
        return rezervasyonId;
    }

    private static async Task<int> SeedDigerRezervasyonuAsync(
        StysAppDbContext dbContext,
        int rezervasyonId,
        int segmentId,
        int odaId,
        DateTime baslangicTarihi,
        DateTime bitisTarihi,
        int ayrilanKisiSayisi = 1,
        string durum = RezervasyonDurumlari.Onayli)
    {
        var oda = await (
                from o in dbContext.Odalar
                join b in dbContext.Binalar on o.BinaId equals b.Id
                join t in dbContext.OdaTipleri on o.TesisOdaTipiId equals t.Id
                where o.Id == odaId
                select new { o.OdaNo, BinaAdi = b.Ad, OdaTipiAdi = t.Ad, t.PaylasimliMi, t.Kapasite })
            .SingleAsync();

        dbContext.Rezervasyonlar.Add(new Rezervasyon
        {
            Id = rezervasyonId,
            ReferansNo = $"DGR-{rezervasyonId}",
            TesisId = 1,
            KisiSayisi = ayrilanKisiSayisi,
            MisafirTipiId = 1,
            KonaklamaTipiId = 1,
            GirisTarihi = baslangicTarihi,
            CikisTarihi = bitisTarihi,
            MisafirAdiSoyadi = "Diger Misafir",
            MisafirTelefon = "000",
            ToplamBazUcret = 100m,
            ToplamUcret = 100m,
            ParaBirimi = "TRY",
            RezervasyonDurumu = durum,
            AktifMi = true
        });

        dbContext.RezervasyonSegmentleri.Add(new RezervasyonSegment
        {
            Id = segmentId,
            RezervasyonId = rezervasyonId,
            SegmentSirasi = 1,
            BaslangicTarihi = baslangicTarihi,
            BitisTarihi = bitisTarihi
        });

        dbContext.RezervasyonSegmentOdaAtamalari.Add(new RezervasyonSegmentOdaAtama
        {
            Id = segmentId + 1,
            RezervasyonSegmentId = segmentId,
            OdaId = odaId,
            AyrilanKisiSayisi = ayrilanKisiSayisi,
            OdaNoSnapshot = oda.OdaNo,
            BinaAdiSnapshot = oda.BinaAdi,
            OdaTipiAdiSnapshot = oda.OdaTipiAdi,
            PaylasimliMiSnapshot = oda.PaylasimliMi,
            KapasiteSnapshot = oda.Kapasite
        });

        await dbContext.SaveChangesAsync();
        return rezervasyonId;
    }

    private static async Task<int> SeedDigerPaylasimliRezervasyonuAsync(
        StysAppDbContext dbContext,
        int rezervasyonId,
        int segmentId,
        int odaId,
        DateTime baslangicTarihi,
        DateTime bitisTarihi,
        string cinsiyet)
    {
        await SeedDigerRezervasyonuAsync(dbContext, rezervasyonId, segmentId, odaId, baslangicTarihi, bitisTarihi, ayrilanKisiSayisi: 1);

        dbContext.RezervasyonKonaklayanlar.Add(new RezervasyonKonaklayan
        {
            Id = rezervasyonId * 100 + 1,
            RezervasyonId = rezervasyonId,
            SiraNo = 1,
            AdSoyad = "Diger Konaklayan",
            Cinsiyet = cinsiyet,
            KatilimDurumu = KonaklayanKatilimDurumlari.Geldi
        });

        dbContext.RezervasyonKonaklayanSegmentAtamalari.Add(new RezervasyonKonaklayanSegmentAtama
        {
            Id = rezervasyonId * 100 + 2,
            RezervasyonKonaklayanId = rezervasyonId * 100 + 1,
            RezervasyonSegmentId = segmentId,
            OdaId = odaId
        });

        await dbContext.SaveChangesAsync();
        return rezervasyonId;
    }

    private static async Task SeedTwoRoomUzatmaFixtureAsync(StysAppDbContext dbContext)
    {
        await SeedLookupsAsync(dbContext);

        dbContext.Tesisler.Add(new Tesis
        {
            Id = 1,
            KurumId = 1,
            Ad = "Uzatma Test Tesis",
            IlId = 1,
            Telefon = "000",
            Adres = "Adres",
            GirisSaati = new TimeSpan(14, 0, 0),
            CikisSaati = new TimeSpan(10, 0, 0),
            AktifMi = true
        });

        dbContext.Binalar.Add(new Bina { Id = 10, TesisId = 1, Ad = "Blok", KatSayisi = 2, AktifMi = true });

        dbContext.OdaTipleri.AddRange(
            new OdaTipi { Id = 20, TesisId = 1, OdaSinifiId = 1, Ad = "Tip A", Kapasite = 1, PaylasimliMi = false, AktifMi = true },
            new OdaTipi { Id = 21, TesisId = 1, OdaSinifiId = 1, Ad = "Tip B", Kapasite = 2, PaylasimliMi = false, AktifMi = true });

        dbContext.Odalar.AddRange(
            new Oda { Id = 100, OdaNo = "A-1", BinaId = 10, TesisOdaTipiId = 20, KatNo = 1, AktifMi = true },
            new Oda { Id = 101, OdaNo = "A-2", BinaId = 10, TesisOdaTipiId = 21, KatNo = 1, AktifMi = true });

        dbContext.OdaFiyatlari.AddRange(
            new OdaFiyat { Id = 1000, TesisOdaTipiId = 20, KonaklamaTipiId = 1, MisafirTipiId = 1, KisiSayisi = 1, Fiyat = 500m, ParaBirimi = "TRY", BaslangicTarihi = new DateTime(2026, 3, 1), BitisTarihi = new DateTime(2026, 3, 31), AktifMi = true },
            new OdaFiyat { Id = 1001, TesisOdaTipiId = 21, KonaklamaTipiId = 1, MisafirTipiId = 1, KisiSayisi = 1, Fiyat = 700m, ParaBirimi = "TRY", BaslangicTarihi = new DateTime(2026, 3, 1), BitisTarihi = new DateTime(2026, 3, 31), AktifMi = true });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedThreeRoomUzatmaFixtureAsync(StysAppDbContext dbContext)
    {
        await SeedLookupsAsync(dbContext);

        dbContext.Tesisler.Add(new Tesis
        {
            Id = 1,
            KurumId = 1,
            Ad = "Uc Oda Uzatma Tesis",
            IlId = 1,
            Telefon = "000",
            Adres = "Adres",
            GirisSaati = new TimeSpan(14, 0, 0),
            CikisSaati = new TimeSpan(10, 0, 0),
            AktifMi = true
        });

        dbContext.Binalar.Add(new Bina { Id = 10, TesisId = 1, Ad = "Blok", KatSayisi = 2, AktifMi = true });

        dbContext.OdaTipleri.AddRange(
            new OdaTipi { Id = 40, TesisId = 1, OdaSinifiId = 1, Ad = "Tip A", Kapasite = 1, PaylasimliMi = false, AktifMi = true },
            new OdaTipi { Id = 41, TesisId = 1, OdaSinifiId = 1, Ad = "Tip B", Kapasite = 3, PaylasimliMi = false, AktifMi = true });

        dbContext.Odalar.AddRange(
            new Oda { Id = 100, OdaNo = "A-1", BinaId = 10, TesisOdaTipiId = 40, KatNo = 1, AktifMi = true },
            new Oda { Id = 101, OdaNo = "A-2", BinaId = 10, TesisOdaTipiId = 40, KatNo = 1, AktifMi = true },
            new Oda { Id = 102, OdaNo = "B-1", BinaId = 10, TesisOdaTipiId = 41, KatNo = 1, AktifMi = true });

        dbContext.OdaFiyatlari.AddRange(
            new OdaFiyat { Id = 4000, TesisOdaTipiId = 40, KonaklamaTipiId = 1, MisafirTipiId = 1, KisiSayisi = 1, Fiyat = 500m, ParaBirimi = "TRY", BaslangicTarihi = new DateTime(2026, 3, 1), BitisTarihi = new DateTime(2026, 3, 31), AktifMi = true },
            new OdaFiyat { Id = 4001, TesisOdaTipiId = 41, KonaklamaTipiId = 1, MisafirTipiId = 1, KisiSayisi = 1, Fiyat = 700m, ParaBirimi = "TRY", BaslangicTarihi = new DateTime(2026, 3, 1), BitisTarihi = new DateTime(2026, 3, 31), AktifMi = true });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedPaylasimliUzatmaFixtureAsync(StysAppDbContext dbContext)
    {
        await SeedLookupsAsync(dbContext);

        dbContext.Tesisler.Add(new Tesis
        {
            Id = 1,
            KurumId = 1,
            Ad = "Paylasimli Uzatma Tesis",
            IlId = 1,
            Telefon = "000",
            Adres = "Adres",
            GirisSaati = new TimeSpan(14, 0, 0),
            CikisSaati = new TimeSpan(10, 0, 0),
            AktifMi = true
        });

        dbContext.Binalar.Add(new Bina { Id = 10, TesisId = 1, Ad = "Blok", KatSayisi = 2, AktifMi = true });

        dbContext.OdaTipleri.AddRange(
            new OdaTipi { Id = 30, TesisId = 1, OdaSinifiId = 1, Ad = "Paylasimli Cift", Kapasite = 2, PaylasimliMi = true, AktifMi = true },
            new OdaTipi { Id = 31, TesisId = 1, OdaSinifiId = 1, Ad = "Tekli", Kapasite = 1, PaylasimliMi = false, AktifMi = true });

        dbContext.Odalar.AddRange(
            new Oda { Id = 101, OdaNo = "P-1", BinaId = 10, TesisOdaTipiId = 30, KatNo = 1, AktifMi = true },
            new Oda { Id = 102, OdaNo = "T-1", BinaId = 10, TesisOdaTipiId = 31, KatNo = 1, AktifMi = true });

        dbContext.OdaFiyatlari.AddRange(
            new OdaFiyat { Id = 3000, TesisOdaTipiId = 30, KonaklamaTipiId = 1, MisafirTipiId = 1, KisiSayisi = 1, Fiyat = 400m, ParaBirimi = "TRY", BaslangicTarihi = new DateTime(2026, 3, 1), BitisTarihi = new DateTime(2026, 3, 31), AktifMi = true },
            new OdaFiyat { Id = 3001, TesisOdaTipiId = 31, KonaklamaTipiId = 1, MisafirTipiId = 1, KisiSayisi = 1, Fiyat = 600m, ParaBirimi = "TRY", BaslangicTarihi = new DateTime(2026, 3, 1), BitisTarihi = new DateTime(2026, 3, 31), AktifMi = true });

        await dbContext.SaveChangesAsync();
    }

    private sealed class FakeNonSuperAdminTenantAccessor : ICurrentTenantAccessor
    {
        private readonly int _kurumId;

        public FakeNonSuperAdminTenantAccessor(int kurumId)
        {
            _kurumId = kurumId;
        }

        public int? GetCurrentKurumId() => _kurumId;

        public IReadOnlyList<int> GetAccessibleKurumIds() => [_kurumId];

        public bool IsSuperAdmin() => false;

        public bool IsKurumAdmin() => true;
    }
}
