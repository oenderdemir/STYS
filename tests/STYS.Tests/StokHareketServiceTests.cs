using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
using STYS.Muhasebe.MuhasebeDonemleri.Mapping;
using STYS.Muhasebe.MuhasebeDonemleri.Repositories;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokHareketleri.Mapping;
using STYS.Muhasebe.StokHareketleri.Repositories;
using STYS.Muhasebe.StokHareketleri.Services;
using STYS.Muhasebe.StokLotlari.Entities;
using STYS.Muhasebe.StokLotlari.Dtos;
using STYS.Muhasebe.StokLotlari.Services;
using STYS.Muhasebe.StokMaliyetPolitikalari.Entities;
using STYS.Muhasebe.StokMaliyetPolitikalari.Dtos;
using STYS.Muhasebe.StokMaliyetPolitikalari.Services;
using STYS.Muhasebe.StokSerileri.Entities;
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
    public async Task TransferIptalAsync_FIFO_KaynakKatmaniGeriYuklerVeHedefKatmanlariKaldirir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.FIFO);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        var created = await service.CreateTransferAsync(CreateTransferRequest(miktar: 5, birimFiyat: 999));

        await service.TransferIptalAsync(created[0].Id!.Value);

        var kaynakKatman = await dbContext.StokMaliyetKatmanlari
            .IgnoreQueryFilters()
            .SingleAsync(x => x.DepoId == 10 && x.KaynakStokHareketId != created[1].Id);
        var hedefKatman = await dbContext.StokMaliyetKatmanlari
            .IgnoreQueryFilters()
            .SingleAsync(x => x.KaynakStokHareketId == created[1].Id);
        var kaynakTuketimler = await dbContext.StokMaliyetKatmanTuketimleri
            .IgnoreQueryFilters()
            .Where(x => x.CikisStokHareketId == created[0].Id)
            .ToListAsync();
        var degerleme = await service.GetStokDegerlemeAsync(1, null);

        Assert.Equal(10, kaynakKatman.KalanMiktar);
        Assert.True(hedefKatman.IsDeleted);
        Assert.All(kaynakTuketimler, x => Assert.True(x.IsDeleted));
        var kaynakSatir = Assert.Single(degerleme, x => x.DepoId == 10);
        Assert.Equal(10, kaynakSatir.BakiyeMiktari);
        Assert.Equal(1000m, kaynakSatir.ToplamStokDegeri);
        Assert.DoesNotContain(degerleme, x => x.DepoId == 20);
    }

    [Fact]
    public async Task TransferIptalAsync_LIFO_KaynakKatmaniGeriYuklerVeHedefKatmanlariKaldirir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.LIFO);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 5, birimFiyat: 160));
        var created = await service.CreateTransferAsync(CreateTransferRequest(miktar: 7, birimFiyat: 999));

        await service.TransferIptalAsync(created[0].Id!.Value);

        var kaynakKatmanlar = await dbContext.StokMaliyetKatmanlari
            .IgnoreQueryFilters()
            .Where(x => x.DepoId == 10)
            .OrderBy(x => x.KaynakStokHareketId)
            .ToListAsync();
        var hedefKatmanlar = await dbContext.StokMaliyetKatmanlari
            .IgnoreQueryFilters()
            .Where(x => x.KaynakStokHareketId == created[1].Id)
            .OrderBy(x => x.Id)
            .ToListAsync();
        var kaynakTuketimler = await dbContext.StokMaliyetKatmanTuketimleri
            .IgnoreQueryFilters()
            .Where(x => x.CikisStokHareketId == created[0].Id)
            .ToListAsync();

        Assert.Equal([10m, 5m], kaynakKatmanlar.Select(x => x.KalanMiktar).ToArray());
        Assert.All(hedefKatmanlar, x => Assert.True(x.IsDeleted));
        Assert.All(kaynakTuketimler, x => Assert.True(x.IsDeleted));
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
    public async Task TransferIptalAsync_FIFO_HedefKatmanKullanildiysaReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.FIFO);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        var created = await service.CreateTransferAsync(CreateTransferRequest(miktar: 5, birimFiyat: 999));
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 5, depoId: 20, birimFiyat: 120));
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Cikis, 1, depoId: 20, birimFiyat: 1));

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.TransferIptalAsync(created[0].Id!.Value));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Hedef depodaki maliyet katmanları kullanıldığı için transfer iptal edilemez.", ex.Message);
    }

    [Fact]
    public async Task TransferIptalAsync_SeriHedefDepodanKullanildiysaReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true, takipTipi: TasinirKartTakipTipleri.Seri);
        var seriId = await CreateSeriAsync(dbContext, "SN001");
        var baskaSeriId = await CreateSeriAsync(dbContext, "SN002");
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 1, 1, StokHareketDurumlari.Aktif, stokSeriId: seriId);
        await SeedMovementAsync(dbContext, 20, 100, StokHareketTipleri.Giris, 1, 1, StokHareketDurumlari.Aktif, stokSeriId: baskaSeriId);
        var service = CreateService(dbContext);

        var created = await service.CreateTransferAsync(CreateTransferRequest(stokSeriId: seriId, miktar: 1));
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Cikis, 1, depoId: 20, stokSeriId: seriId));

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.TransferIptalAsync(created[0].Id!.Value));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Seri hedef depodan kullanıldığı için transfer iptal edilemez.", ex.Message);
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
    public async Task GetSktUyarilariAsync_GecmisLotuGecmisDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true);
        var lotId = await CreateLotAsync(dbContext, "LOT-OLD", new DateTime(2026, 8, 22));
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 5, 1, StokHareketDurumlari.Aktif, stokLotId: lotId);
        var service = CreateLotSktUyariService(dbContext);

        var result = await service.GetSktUyarilariAsync(1, null, null, false);

        var row = Assert.Single(result);
        Assert.Equal("LOT-OLD", row.LotNo);
        Assert.Equal(StokLotSktUyariDurumlari.Gecmis, row.Durum);
        Assert.Equal(-1, row.KalanGun);
    }

    [Fact]
    public async Task GetSktUyarilariAsync_YediGunlukLotuKritikDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true);
        var lotId = await CreateLotAsync(dbContext, "LOT-7", new DateTime(2026, 8, 30));
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 5, 1, StokHareketDurumlari.Aktif, stokLotId: lotId);
        var service = CreateLotSktUyariService(dbContext);

        var result = await service.GetSktUyarilariAsync(1, null, null, false);

        var row = Assert.Single(result);
        Assert.Equal(StokLotSktUyariDurumlari.Kritik, row.Durum);
        Assert.Equal(7, row.KalanGun);
    }

    [Fact]
    public async Task GetSktUyarilariAsync_OtuzGunlukLotuYaklasiyorDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true);
        var lotId = await CreateLotAsync(dbContext, "LOT-30", new DateTime(2026, 9, 22));
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 5, 1, StokHareketDurumlari.Aktif, stokLotId: lotId);
        var service = CreateLotSktUyariService(dbContext);

        var result = await service.GetSktUyarilariAsync(1, null, null, false);

        var row = Assert.Single(result);
        Assert.Equal(StokLotSktUyariDurumlari.Yaklasiyor, row.Durum);
        Assert.Equal(30, row.KalanGun);
    }

    [Fact]
    public async Task GetSktUyarilariAsync_OtuzBirGunlukLotuNormalDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true);
        var lotId = await CreateLotAsync(dbContext, "LOT-31", new DateTime(2026, 9, 23));
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 5, 1, StokHareketDurumlari.Aktif, stokLotId: lotId);
        var service = CreateLotSktUyariService(dbContext);

        var result = await service.GetSktUyarilariAsync(1, null, null, false);

        var row = Assert.Single(result);
        Assert.Equal(StokLotSktUyariDurumlari.Normal, row.Durum);
        Assert.Equal(31, row.KalanGun);
    }

    [Fact]
    public async Task GetSktUyarilariAsync_SifirBakiyeliLotuGostermez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true);
        var lotId = await CreateLotAsync(dbContext, "LOT-ZERO", new DateTime(2026, 9, 1));
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 5, 1, StokHareketDurumlari.Aktif, stokLotId: lotId);
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Cikis, 5, 1, StokHareketDurumlari.Aktif, stokLotId: lotId);
        var service = CreateLotSktUyariService(dbContext);

        var result = await service.GetSktUyarilariAsync(1, null, null, false);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSktUyarilariAsync_DepoVeTesisYetkisiniKorur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true);
        await SeedCrossTesisDataAsync(dbContext);

        dbContext.Depolar.Add(new Depo
        {
            Id = 30,
            TesisId = 2,
            Kod = "D-003",
            Ad = "Tesis 2 Deposu",
            AktifMi = true,
            MuhasebeHesapPlaniId = 1,
            MalzemeKayitTipi = DepoMalzemeKayitTipleri.MalzemeleriAyriKayittaTut
        });
        var lot1Id = await CreateLotAsync(dbContext, "LOT-T1", new DateTime(2026, 8, 30));
        dbContext.StokLotlar.Add(new StokLot
        {
            TesisId = 2,
            TasinirKartId = 101,
            LotNo = "LOT-T2",
            SonKullanmaTarihi = new DateTime(2026, 8, 30),
            AktifMi = true
        });
        await dbContext.SaveChangesAsync();
        var lot2Id = await dbContext.StokLotlar.Where(x => x.TesisId == 2).Select(x => x.Id).SingleAsync();

        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 5, 1, StokHareketDurumlari.Aktif, stokLotId: lot1Id);
        await SeedMovementAsync(dbContext, 30, 101, StokHareketTipleri.Giris, 5, 1, StokHareketDurumlari.Aktif, stokLotId: lot2Id);
        var service = CreateLotSktUyariService(dbContext);

        var result = await service.GetSktUyarilariAsync(1, null, null, false);

        var row = Assert.Single(result);
        Assert.Equal(10, row.DepoId);
        Assert.Equal("LOT-T1", row.LotNo);
    }

    [Fact]
    public async Task GetSktUyarilariAsync_TurkiyeSaatindeGunKaymasiYapmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true);
        var lotId = await CreateLotAsync(dbContext, "LOT-TRT", new DateTime(2026, 8, 24));
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 5, 1, StokHareketDurumlari.Aktif, stokLotId: lotId);
        var service = new StokLotSktUyariService(
            dbContext,
            new FakeMuhasebeTesisScopeService(),
            new FakeUserAccessScopeService(DomainAccessScope.Scoped([], [1], [])),
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 23, 22, 0, 0, TimeSpan.Zero)));

        var result = await service.GetSktUyarilariAsync(1, null, null, false);

        var row = Assert.Single(result);
        Assert.Equal(0, row.KalanGun);
        Assert.Equal(StokLotSktUyariDurumlari.Kritik, row.Durum);
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

    [Fact]
    public async Task GetStokDetayAsync_AyriKayitModundaUcAyrıGirisDetayiDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedDetayHareketleriAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.GetStokDetayAsync(10, 100);

        Assert.Equal("MalzemeleriAyriKayittaTut", result.MalzemeKayitTipi);
        Assert.Equal(3, result.Satirlar.Count);
        Assert.Equal(new decimal[] { 10, 5, 8 }, result.Satirlar.Select(x => x.Miktar));
        Assert.Equal(new decimal[] { 100, 100, 120 }, result.Satirlar.Select(x => x.BirimFiyat));
    }

    [Fact]
    public async Task GetStokDetayAsync_FiyatBazliModdaAyniFiyatliGirisleriGruplar()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, DepoMalzemeKayitTipleri.FiyatFarkliMalzemeleriAyriKayittaTut);
        await SeedDetayHareketleriAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.GetStokDetayAsync(10, 100);

        Assert.Equal("FiyatFarkliMalzemeleriAyriKayittaTut", result.MalzemeKayitTipi);
        Assert.Equal(2, result.Satirlar.Count);
        Assert.Collection(result.Satirlar.OrderBy(x => x.BirimFiyat),
            ilk =>
            {
                Assert.Equal(15, ilk.Miktar);
                Assert.Equal(100, ilk.BirimFiyat);
                Assert.Equal(1500, ilk.ToplamTutar);
            },
            ikinci =>
            {
                Assert.Equal(8, ikinci.Miktar);
                Assert.Equal(120, ikinci.BirimFiyat);
                Assert.Equal(960, ikinci.ToplamTutar);
            });
    }

    [Fact]
    public async Task GetStokDetayAsync_AyniKayitModundaAgirlikliOrtalamaDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, DepoMalzemeKayitTipleri.MalzemeleriAyniKayittaTut);
        await SeedDetayHareketleriAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.GetStokDetayAsync(10, 100);

        Assert.Equal("MalzemeleriAyniKayittaTut", result.MalzemeKayitTipi);
        var satir = Assert.Single(result.Satirlar);
        Assert.Equal(23, satir.Miktar);
        Assert.Equal(2460, satir.ToplamTutar);
        Assert.Equal(Math.Round(2460m / 23m, 2, MidpointRounding.AwayFromZero), satir.BirimFiyat);
    }

    [Fact]
    public async Task GetStokDetayVeStokBakiye_MalzemeKayitTipiDegisseDeToplamBakiyeDegismez()
    {
        foreach (var tip in new[]
                 {
                     DepoMalzemeKayitTipleri.MalzemeleriAyriKayittaTut,
                     DepoMalzemeKayitTipleri.FiyatFarkliMalzemeleriAyriKayittaTut,
                     DepoMalzemeKayitTipleri.MalzemeleriAyniKayittaTut
                 })
        {
            await using var dbContext = CreateDbContext();
            await SeedBaseAsync(dbContext, tip);
            await SeedBakiyeKorumaHareketleriAsync(dbContext);
            var service = CreateService(dbContext);

            var bakiye = await service.GetStokBakiyeAsync(1, 10);
            var detay = await service.GetStokDetayAsync(10, 100);

            var satir = Assert.Single(bakiye);
            Assert.Equal(70, satir.BakiyeMiktari);
            Assert.Equal(70, detay.BakiyeMiktari);
        }
    }

    [Fact]
    public async Task GetStokDetayAsync_IptalHareketleriniHesabaKatmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedDetayHareketleriAsync(dbContext);
        dbContext.StokHareketleri.Add(new StokHareket
        {
            DepoId = 10,
            TasinirKartId = 100,
            HareketTarihi = new DateTime(2026, 8, 11),
            HareketTipi = StokHareketTipleri.Giris,
            Miktar = 99,
            BirimFiyat = 77,
            Tutar = 7623,
            Durum = StokHareketDurumlari.Iptal,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
            KdvOrani = 20,
            KdvTutari = 1524.6m
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var result = await service.GetStokDetayAsync(10, 100);

        Assert.Equal(3, result.Satirlar.Count);
        Assert.DoesNotContain(result.Satirlar, x => x.BirimFiyat == 77);
    }

    [Fact]
    public async Task AddAsync_YetersizNormalCikisReddederVeYeniHareketOlusturmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, miktar: 10);
        var service = CreateService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Cikis, miktar: 11)));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Depoda bu işlem için yeterli stok bulunmamaktadır.", ex.Message);
        Assert.Equal(1, await dbContext.StokHareketleri.CountAsync());
    }

    [Fact]
    public async Task AddAsync_YeterliSarfIleBakiyeDusurur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, miktar: 10);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Sarf, miktar: 8));

        var bakiye = await service.GetStokBakiyeAsync(1, 10);
        Assert.Equal(2, Assert.Single(bakiye).BakiyeMiktari);
    }

    [Fact]
    public async Task UpdateAsync_CikisMiktariniArtirincaProjeksiyonBakiyeyiDogruHesaplar()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, miktar: 100);
        var existingCikisId = await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Cikis, 20, 1, StokHareketDurumlari.Aktif);
        var service = CreateService(dbContext);

        await service.UpdateAsync(CreateStokHareketDto(StokHareketTipleri.Cikis, miktar: 30, id: existingCikisId));

        var bakiye = await service.GetStokBakiyeAsync(1, 10);
        Assert.Equal(70, Assert.Single(bakiye).BakiyeMiktari);
    }

    [Fact]
    public async Task UpdateAsync_GirisKucultmeNegatifStokYaratirsaReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var girisId = await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 10, 1, StokHareketDurumlari.Aktif);
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Sarf, 8, 1, StokHareketDurumlari.Aktif);
        var service = CreateService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.UpdateAsync(CreateStokHareketDto(StokHareketTipleri.Giris, miktar: 5, id: girisId)));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Depoda bu işlem için yeterli stok bulunmamaktadır.", ex.Message);
    }

    [Fact]
    public async Task DeleteAsync_GirisSilininceNegatifStokOlusuyorsaReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var girisId = await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 10, 1, StokHareketDurumlari.Aktif);
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Cikis, 8, 1, StokHareketDurumlari.Aktif);
        var service = CreateService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.DeleteAsync(girisId));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Bu stok hareketi silinirse depo bakiyesi negatif olacağı için işlem yapılamaz.", ex.Message);
    }

    [Fact]
    public async Task AddUpdateVeGetStokDetay_FarkliTesisDepoKartKombinasyonunuReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedCrossTesisDataAsync(dbContext);
        var existingId = await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 5, 1, StokHareketDurumlari.Aktif);
        var service = CreateService(dbContext);

        var addEx = await Assert.ThrowsAsync<BaseException>(() => service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, miktar: 1, tasinirKartId: 101)));
        var updateEx = await Assert.ThrowsAsync<BaseException>(() => service.UpdateAsync(CreateStokHareketDto(StokHareketTipleri.Giris, miktar: 5, id: existingId, tasinirKartId: 101)));
        var detayEx = await Assert.ThrowsAsync<BaseException>(() => service.GetStokDetayAsync(10, 101));

        Assert.Equal("Seçilen depo ve taşınır kart aynı tesise ait olmalıdır.", addEx.Message);
        Assert.Equal("Seçilen depo ve taşınır kart aynı tesise ait olmalıdır.", updateEx.Message);
        Assert.Equal("Seçilen depo ve taşınır kart aynı tesise ait olmalıdır.", detayEx.Message);
    }

    [Fact]
    public async Task UpdateAsync_AktifGirisiIptalEtmekNegatifStokYaratirsaReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var girisId = await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 10, 1, StokHareketDurumlari.Aktif);
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Sarf, 8, 1, StokHareketDurumlari.Aktif);
        var service = CreateService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.UpdateAsync(CreateStokHareketDto(StokHareketTipleri.Giris, miktar: 10, id: girisId, durum: StokHareketDurumlari.Iptal)));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Depoda bu işlem için yeterli stok bulunmamaktadır.", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_TrackedEskiEntityYerineTransactionIciFreshSnapshotKullanir()
    {
        var databaseName = Guid.NewGuid().ToString();

        await using var dbContext = CreateDbContext(databaseName);
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, miktar: 100);
        var existingCikisId = await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Cikis, 20, 1, StokHareketDurumlari.Aktif);
        var service = CreateService(dbContext);

        var tracked = await dbContext.StokHareketleri.FirstAsync(x => x.Id == existingCikisId);
        Assert.Equal(20, tracked.Miktar);

        await using (var concurrentContext = CreateDbContext(databaseName))
        {
            var sameMovement = await concurrentContext.StokHareketleri.FirstAsync(x => x.Id == existingCikisId);
            sameMovement.Miktar = 90;
            sameMovement.Tutar = 90;
            await concurrentContext.SaveChangesAsync();
        }

        await service.UpdateAsync(CreateStokHareketDto(StokHareketTipleri.Cikis, miktar: 95, id: existingCikisId));

        var bakiye = await service.GetStokBakiyeAsync(1, 10);
        Assert.Equal(5, Assert.Single(bakiye).BakiyeMiktari);
    }

    [Fact]
    public async Task AddAsync_SayimFarkiFazlaGirisEtkisiYaratir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, miktar: 10);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.SayimFarki, miktar: 2, sayimFarkiYonu: StokSayimFarkiYonleri.Fazla));

        var bakiye = await service.GetStokBakiyeAsync(1, 10);
        Assert.Equal(12, Assert.Single(bakiye).BakiyeMiktari);
    }

    [Fact]
    public async Task AddAsync_SayimFarkiEksikCikisEtkisiYaratir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, miktar: 10);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.SayimFarki, miktar: 3, sayimFarkiYonu: StokSayimFarkiYonleri.Eksik));

        var bakiye = await service.GetStokBakiyeAsync(1, 10);
        Assert.Equal(7, Assert.Single(bakiye).BakiyeMiktari);
    }

    [Fact]
    public async Task AddAsync_SayimFarkiEksikYetersizStoktaReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, miktar: 2);
        var service = CreateService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddAsync(CreateStokHareketDto(StokHareketTipleri.SayimFarki, miktar: 3, sayimFarkiYonu: StokSayimFarkiYonleri.Eksik)));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Depoda bu işlem için yeterli stok bulunmamaktadır.", ex.Message);
    }

    [Fact]
    public async Task GetStokBakiyeAsync_LegacyNullSayimFarkiYonuGirisEtkisiGosterir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, miktar: 10);
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.SayimFarki, 2, 1, StokHareketDurumlari.Aktif);
        var service = CreateService(dbContext);

        var bakiye = await service.GetStokBakiyeAsync(1, 10);
        Assert.Equal(12, Assert.Single(bakiye).BakiyeMiktari);
    }

    [Fact]
    public async Task UpdateAsync_SayimFarkiYonDegisinceProjeksiyonNegatifseReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, miktar: 8);
        var sayimFarkiId = await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.SayimFarki, 5, 1, StokHareketDurumlari.Aktif, StokSayimFarkiYonleri.Fazla);
        var service = CreateService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.UpdateAsync(CreateStokHareketDto(
            StokHareketTipleri.SayimFarki,
            miktar: 10,
            id: sayimFarkiId,
            sayimFarkiYonu: StokSayimFarkiYonleri.Eksik)));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Depoda bu işlem için yeterli stok bulunmamaktadır.", ex.Message);
    }

    [Fact]
    public async Task AddAsync_SayimFarkiKdvAlanlariniKapsamDisiNormalizeEder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, miktar: 10);
        var kdvService = new FakeKdvUygulamaService();
        var service = CreateService(dbContext, kdvService: kdvService);

        var created = await service.AddAsync(CreateStokHareketDto(
            StokHareketTipleri.SayimFarki,
            miktar: 2,
            sayimFarkiYonu: StokSayimFarkiYonleri.Fazla,
            kdvUygulamaTipi: (int)KdvUygulamaTipi.Kdvli,
            kdvOrani: 20));

        Assert.Equal((int)KdvUygulamaTipi.KdvKapsamDisi, created.KdvUygulamaTipi);
        Assert.Equal(0, created.KdvOrani);
        Assert.Equal(0, created.KdvTutari);
        Assert.Null(created.KdvIstisnaTanimId);
        Assert.Equal(0, kdvService.CallCount);
    }

    [Fact]
    public async Task AddAsync_TakipliKarttaLotluGirisLotVeToplamBakiyeyiOlusturur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, lotNo: "LOT-A", sonKullanmaTarihi: new DateTime(2027, 1, 1)));

        var lotBakiye = Assert.Single(await service.GetLotBakiyeleriAsync(10, 100));
        var toplamBakiye = Assert.Single(await service.GetStokBakiyeAsync(1, 10));

        Assert.Equal("LOT-A", lotBakiye.LotNo);
        Assert.Equal(10, lotBakiye.BakiyeMiktari);
        Assert.Equal(10, toplamBakiye.BakiyeMiktari);
    }

    [Fact]
    public async Task AddAsync_LotBazliYetersizStoguToplamYeterliOlsaBileReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true);
        var lotAId = await CreateLotAsync(dbContext, "LOT-A", new DateTime(2027, 1, 1));
        var lotBId = await CreateLotAsync(dbContext, "LOT-B", new DateTime(2027, 2, 1));
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 3, 1, StokHareketDurumlari.Aktif, stokLotId: lotAId);
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 17, 1, StokHareketDurumlari.Aktif, stokLotId: lotBId);
        var service = CreateService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Cikis, 5, stokLotId: lotAId)));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Seçilen lotta bu işlem için yeterli stok bulunmamaktadır.", ex.Message);
    }

    [Fact]
    public async Task CreateTransferAsync_TakipliKarttaAyniLotKimliginiKorur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true);
        var lotAId = await CreateLotAsync(dbContext, "LOT-A", new DateTime(2027, 1, 1));
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 10, 1, StokHareketDurumlari.Aktif, stokLotId: lotAId);
        var service = CreateService(dbContext);

        var created = await service.CreateTransferAsync(CreateTransferRequest(stokLotId: lotAId, miktar: 4));

        var lotBakiyeleriKaynak = await service.GetLotBakiyeleriAsync(10, 100);
        var lotBakiyeleriHedef = await service.GetLotBakiyeleriAsync(20, 100);

        Assert.Equal(2, created.Count);
        Assert.All(created, x => Assert.Equal(lotAId, x.StokLotId));
        Assert.Equal(6, Assert.Single(lotBakiyeleriKaynak).BakiyeMiktari);
        Assert.Equal(4, Assert.Single(lotBakiyeleriHedef).BakiyeMiktari);
    }

    [Fact]
    public async Task AddAsync_TakipliKarttaLotZorunludurTakipsizKarttaDegildir()
    {
        await using var trackedContext = CreateDbContext();
        await SeedBaseAsync(trackedContext, takipliMi: true);
        var trackedService = CreateService(trackedContext);

        var trackedEx = await Assert.ThrowsAsync<BaseException>(() => trackedService.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 5)));

        Assert.Equal(400, trackedEx.ErrorCode);
        Assert.Equal("Takipli taşınır kart için lot numarası zorunludur.", trackedEx.Message);

        await using var untrackedContext = CreateDbContext();
        await SeedBaseAsync(untrackedContext, takipliMi: false);
        var untrackedService = CreateService(untrackedContext);

        await untrackedService.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 5));

        Assert.Equal(1, await untrackedContext.StokHareketleri.CountAsync());
    }

    [Fact]
    public async Task AddAsync_SeriTakipliKarttaGirisSeriyiDepodaMevcutYapar()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true, takipTipi: TasinirKartTakipTipleri.Seri);
        var service = CreateService(dbContext);

        var created = await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 1, seriNo: "SN001"));

        var seri = Assert.Single(await service.GetSeriBakiyeleriAsync(10, 100));
        Assert.Equal("SN001", created.SeriNo);
        Assert.Equal("SN001", seri.SeriNo);
    }

    [Fact]
    public async Task AddAsync_SeriTakipliKarttaAyniSeriStoktaysaIkinciGirisReddedilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true, takipTipi: TasinirKartTakipTipleri.Seri);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 1, seriNo: "SN001"));

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 1, seriNo: "SN001")));

        Assert.Equal("Seri numarası için seçilen depo hareketi geçersizdir.", ex.Message);
    }

    [Fact]
    public async Task AddAsync_SeriTakipliKarttaMiktarIkiyseReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true, takipTipi: TasinirKartTakipTipleri.Seri);
        var service = CreateService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 2, seriNo: "SN001")));

        Assert.Equal("Seri takipli taşınır kartlarda miktar 1 olmalıdır.", ex.Message);
    }

    [Fact]
    public async Task AddAsync_SeriCikisSonrasiKaynakDepodaGorunmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true, takipTipi: TasinirKartTakipTipleri.Seri);
        var seriId = await CreateSeriAsync(dbContext, "SN001");
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 1, 1, StokHareketDurumlari.Aktif, stokSeriId: seriId);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Cikis, 1, stokSeriId: seriId));

        Assert.Empty(await service.GetSeriBakiyeleriAsync(10, 100));
    }

    [Fact]
    public async Task CreateTransferAsync_SeriTakipliKarttaAyniSeriKimliginiKorurVeHedefeTasir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true, takipTipi: TasinirKartTakipTipleri.Seri);
        var seriId = await CreateSeriAsync(dbContext, "SN001");
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 1, 1, StokHareketDurumlari.Aktif, stokSeriId: seriId);
        var service = CreateService(dbContext);

        var created = await service.CreateTransferAsync(CreateTransferRequest(stokSeriId: seriId, miktar: 1));

        Assert.All(created, x => Assert.Equal(seriId, x.StokSeriId));
        Assert.Empty(await service.GetSeriBakiyeleriAsync(10, 100));
        Assert.Equal("SN001", Assert.Single(await service.GetSeriBakiyeleriAsync(20, 100)).SeriNo);
    }

    [Fact]
    public async Task AddAsync_AyniLotNoIkinciGiristeYeniLotOlusturmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 5, lotNo: "LOT-A", sonKullanmaTarihi: new DateTime(2027, 1, 1)));
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 7, lotNo: "LOT-A", sonKullanmaTarihi: new DateTime(2027, 1, 1)));

        Assert.Equal(1, await dbContext.StokLotlar.CountAsync());
        var hareketler = await dbContext.StokHareketleri.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(hareketler[0].StokLotId, hareketler[1].StokLotId);
    }

    [Fact]
    public async Task AddAsync_AyniLotNoFarkliSktIleReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 5, lotNo: "LOT-A", sonKullanmaTarihi: new DateTime(2027, 1, 1)));

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddAsync(CreateStokHareketDto(
            StokHareketTipleri.Giris,
            3,
            lotNo: "LOT-A",
            sonKullanmaTarihi: new DateTime(2027, 6, 1))));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Ayni lot numarasi farkli son kullanma tarihi ile kullanilamaz.", ex.Message);
    }

    [Fact]
    public async Task GetByIdVeGetPagedAsync_LotNoVeSktAlanlariniDoldurur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true);
        var service = CreateService(dbContext);

        var created = await service.AddAsync(CreateStokHareketDto(
            StokHareketTipleri.Giris,
            10,
            lotNo: " LOT-A ",
            sonKullanmaTarihi: new DateTime(2027, 1, 1)));

        var byId = await service.GetByIdAsync(created.Id!.Value);
        var paged = await service.GetPagedAsync(new PagedRequest { PageNumber = 1, PageSize = 10 });
        var pagedItem = Assert.Single(paged.Items.Where(x => x.Id == created.Id));

        Assert.NotNull(byId);
        Assert.Equal("LOT-A", byId!.LotNo);
        Assert.Equal(new DateTime(2027, 1, 1), byId.SonKullanmaTarihi);
        Assert.Equal("LOT-A", pagedItem.LotNo);
        Assert.Equal(new DateTime(2027, 1, 1), pagedItem.SonKullanmaTarihi);
    }

    [Fact]
    public async Task GetStokDetayAsync_FiyatBazliModdaAyniFiyatliFarkliLotlariBirleştirmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, DepoMalzemeKayitTipleri.FiyatFarkliMalzemeleriAyriKayittaTut, takipliMi: true);
        var lotAId = await CreateLotAsync(dbContext, "LOT-A", new DateTime(2027, 1, 1));
        var lotBId = await CreateLotAsync(dbContext, "LOT-B", new DateTime(2027, 2, 1));
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 5, 100, StokHareketDurumlari.Aktif, stokLotId: lotAId);
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 7, 100, StokHareketDurumlari.Aktif, stokLotId: lotBId);
        var service = CreateService(dbContext);

        var result = await service.GetStokDetayAsync(10, 100);

        Assert.Equal(2, result.Satirlar.Count);
        Assert.Equal(["LOT-A", "LOT-B"], result.Satirlar.Select(x => x.LotNo).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task GetStokDetayAsync_AyniKayitModundaFarkliLotlariAyriSatirdaGosterir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, DepoMalzemeKayitTipleri.MalzemeleriAyniKayittaTut, takipliMi: true);
        var lotAId = await CreateLotAsync(dbContext, "LOT-A", new DateTime(2027, 1, 1));
        var lotBId = await CreateLotAsync(dbContext, "LOT-B", new DateTime(2027, 2, 1));
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 5, 100, StokHareketDurumlari.Aktif, stokLotId: lotAId);
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 7, 120, StokHareketDurumlari.Aktif, stokLotId: lotBId);
        var service = CreateService(dbContext);

        var result = await service.GetStokDetayAsync(10, 100);

        Assert.Equal(2, result.Satirlar.Count);
        Assert.Equal(["LOT-A", "LOT-B"], result.Satirlar.Select(x => x.LotNo).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task GetStokDetayAsync_AyniLotVeAyniFiyatFiyatBazliModdaBirlesebilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, DepoMalzemeKayitTipleri.FiyatFarkliMalzemeleriAyriKayittaTut, takipliMi: true);
        var lotAId = await CreateLotAsync(dbContext, "LOT-A", new DateTime(2027, 1, 1));
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 5, 100, StokHareketDurumlari.Aktif, stokLotId: lotAId);
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 7, 100, StokHareketDurumlari.Aktif, stokLotId: lotAId);
        var service = CreateService(dbContext);

        var result = await service.GetStokDetayAsync(10, 100);

        var satir = Assert.Single(result.Satirlar);
        Assert.Equal("LOT-A", satir.LotNo);
        Assert.Equal(12, satir.Miktar);
        Assert.Equal(100, satir.BirimFiyat);
    }

    [Fact]
    public async Task AddAsync_LegacyLotsuzStokVarkenLotluCikisReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true);
        await SeedMovementAsync(dbContext, 10, 100, StokHareketTipleri.Giris, 10, 1, StokHareketDurumlari.Aktif);
        var lotAId = await CreateLotAsync(dbContext, "LOT-A", new DateTime(2027, 1, 1));
        var service = CreateService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddAsync(CreateStokHareketDto(
            StokHareketTipleri.Cikis,
            2,
            stokLotId: lotAId)));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Bu takipli kart için lota dağıtılmamış eski stok bulundu. Lotlu çıkış yapmadan önce legacy stokları açılış işlemiyle dağıtınız.", ex.Message);
    }

    [Fact]
    public async Task GetStokDegerlemeAsync_IkiGirisIleAgirlikliOrtalamaHesaplar()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 5, birimFiyat: 160));

        var satir = Assert.Single(await service.GetStokDegerlemeAsync(1, 10));

        Assert.Equal(15, satir.BakiyeMiktari);
        Assert.Equal(120m, satir.OrtalamaMaliyet);
        Assert.Equal(1800m, satir.ToplamStokDegeri);
    }

    [Fact]
    public async Task AddAsync_CikisMaliyetSnapshotiniMevcutOrtalamadanAlirVeKalanDegeriDusurur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 5, birimFiyat: 160));

        var cikis = await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Cikis, 4, birimFiyat: 1));
        var satir = Assert.Single(await service.GetStokDegerlemeAsync(1, 10));

        Assert.Equal(120m, cikis.MaliyetBirimFiyat);
        Assert.Equal(480m, cikis.MaliyetTutari);
        Assert.Equal(11, satir.BakiyeMiktari);
        Assert.Equal(1320m, satir.ToplamStokDegeri);
    }

    [Fact]
    public async Task CreateTransferAsync_MaliyetiKaynakIleHedefeTasir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 5, birimFiyat: 160));

        var transfer = await service.CreateTransferAsync(CreateTransferRequest(miktar: 4, birimFiyat: 999));
        var cikis = Assert.Single(transfer.Where(x => x.TransferYonu == StokTransferYonleri.Cikis));
        var giris = Assert.Single(transfer.Where(x => x.TransferYonu == StokTransferYonleri.Giris));
        var degerleme = await service.GetStokDegerlemeAsync(1, null);
        var kaynak = Assert.Single(degerleme.Where(x => x.DepoId == 10));
        var hedef = Assert.Single(degerleme.Where(x => x.DepoId == 20));

        Assert.Equal(120m, cikis.MaliyetBirimFiyat);
        Assert.Equal(480m, cikis.MaliyetTutari);
        Assert.Equal(cikis.MaliyetBirimFiyat, giris.MaliyetBirimFiyat);
        Assert.Equal(cikis.MaliyetTutari, giris.MaliyetTutari);
        Assert.Equal(11, kaynak.BakiyeMiktari);
        Assert.Equal(1320m, kaynak.ToplamStokDegeri);
        Assert.Equal(4, hedef.BakiyeMiktari);
        Assert.Equal(480m, hedef.ToplamStokDegeri);
    }

    [Fact]
    public async Task AddAsync_SayimFarkiEksikMevcutOrtalamayiKullanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 5, birimFiyat: 160));

        var hareket = await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.SayimFarki, 3, birimFiyat: 0, sayimFarkiYonu: StokSayimFarkiYonleri.Eksik));

        Assert.Equal(120m, hareket.MaliyetBirimFiyat);
        Assert.Equal(360m, hareket.MaliyetTutari);
    }

    [Fact]
    public async Task AddAsync_SayimFarkiFazlaMevcutOrtalamayiKullanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 5, birimFiyat: 160));

        var hareket = await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.SayimFarki, 2, birimFiyat: 0, sayimFarkiYonu: StokSayimFarkiYonleri.Fazla));
        var satir = Assert.Single(await service.GetStokDegerlemeAsync(1, 10));

        Assert.Equal(120m, hareket.MaliyetBirimFiyat);
        Assert.Equal(240m, hareket.MaliyetTutari);
        Assert.Equal(17, satir.BakiyeMiktari);
        Assert.Equal(2040m, satir.ToplamStokDegeri);
    }

    [Fact]
    public async Task AddAsync_LotTakipliHareketKartBazliOrtalamaMaliyetKullanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext, takipliMi: true, takipTipi: TasinirKartTakipTipleri.Lot);
        var lotAId = await CreateLotAsync(dbContext, "LOT-A", new DateTime(2027, 1, 1));
        var lotBId = await CreateLotAsync(dbContext, "LOT-B", new DateTime(2027, 6, 1));
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100, stokLotId: lotAId));
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 5, birimFiyat: 160, stokLotId: lotBId));

        var cikis = await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Cikis, 4, birimFiyat: 1, stokLotId: lotAId));

        Assert.Equal(120m, cikis.MaliyetBirimFiyat);
        Assert.Equal(480m, cikis.MaliyetTutari);
    }

    [Fact]
    public async Task UpdateAsync_SonrakiMaliyetSnapshotlariVarsaMaliyetEtkiliGecmisiReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);

        var giris = await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Cikis, 4, birimFiyat: 1));

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.UpdateAsync(CreateStokHareketDto(
            StokHareketTipleri.Giris,
            10,
            id: giris.Id,
            birimFiyat: 110)));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Bu stok hareketi sonraki maliyet snapshot'larını etkileyeceği için güncellenemez.", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_MaliyetEtkisizAlanDegisirseMevcutSnapshotiKorur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        var cikis = await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Cikis, 4, birimFiyat: 1));
        var sonrakiGiris = CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 200);
        sonrakiGiris.HareketTarihi = new DateTime(2026, 8, 22);
        await service.AddAsync(sonrakiGiris);

        var updated = await service.UpdateAsync(new StokHareketDto
        {
            Id = cikis.Id,
            DepoId = cikis.DepoId,
            TasinirKartId = cikis.TasinirKartId,
            HareketTarihi = cikis.HareketTarihi,
            HareketTipi = cikis.HareketTipi,
            Miktar = cikis.Miktar,
            BirimFiyat = cikis.BirimFiyat,
            Tutar = cikis.Tutar,
            BelgeNo = cikis.BelgeNo,
            BelgeTarihi = cikis.BelgeTarihi,
            Aciklama = "Yalnızca açıklama güncellendi",
            CariKartId = cikis.CariKartId,
            KaynakModul = cikis.KaynakModul,
            KaynakId = cikis.KaynakId,
            TransferGrupId = cikis.TransferGrupId,
            TransferYonu = cikis.TransferYonu,
            SayimFarkiYonu = cikis.SayimFarkiYonu,
            StokLotId = cikis.StokLotId,
            StokSeriId = cikis.StokSeriId,
            LotNo = cikis.LotNo,
            SeriNo = cikis.SeriNo,
            SonKullanmaTarihi = cikis.SonKullanmaTarihi,
            KarsiDepoId = cikis.KarsiDepoId,
            Durum = cikis.Durum,
            KdvUygulamaTipi = cikis.KdvUygulamaTipi,
            KdvIstisnaTanimId = cikis.KdvIstisnaTanimId,
            KdvOrani = cikis.KdvOrani
        });

        Assert.Equal(cikis.MaliyetBirimFiyat, updated.MaliyetBirimFiyat);
        Assert.Equal(cikis.MaliyetTutari, updated.MaliyetTutari);
    }

    [Fact]
    public async Task AddAsync_GeriyeTarihliMaliyetEtkiliGirisSonrakiSnapshotVarkenReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);

        var ilkGiris = CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100);
        ilkGiris.HareketTarihi = new DateTime(2026, 8, 1);
        await service.AddAsync(ilkGiris);

        var cikis = CreateStokHareketDto(StokHareketTipleri.Cikis, 5, birimFiyat: 1);
        cikis.HareketTarihi = new DateTime(2026, 8, 10);
        await service.AddAsync(cikis);

        var geriyeTarihliGiris = CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 200);
        geriyeTarihliGiris.HareketTarihi = new DateTime(2026, 8, 5);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddAsync(geriyeTarihliGiris));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Bu tarihten sonra maliyet snapshot'ı oluşmuş stok hareketleri bulunduğu için geriye tarihli hareket eklenemez.", ex.Message);
    }

    [Fact]
    public async Task GetStokDegerlemeAsync_LegacyCikisBulunursaMaliyetEksikUyarisiDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        dbContext.StokHareketleri.Add(new StokHareket
        {
            DepoId = 10,
            TasinirKartId = 100,
            HareketTarihi = new DateTime(2026, 8, 22),
            HareketTipi = StokHareketTipleri.Cikis,
            Miktar = 2,
            BirimFiyat = 1,
            Tutar = 2,
            Durum = StokHareketDurumlari.Aktif,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
            KdvOrani = 20,
            KdvTutari = 0.4m
        });
        await dbContext.SaveChangesAsync();

        var satir = Assert.Single(await service.GetStokDegerlemeAsync(1, 10));

        Assert.True(satir.MaliyetEksikMi);
    }

    [Fact]
    public async Task GetStokDegerlemeAsync_GuncelMaliyetliKayitlardaMaliyetEksikUyarisiFalseDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Cikis, 2, birimFiyat: 1));

        var satir = Assert.Single(await service.GetStokDegerlemeAsync(1, 10));

        Assert.False(satir.MaliyetEksikMi);
    }

    [Fact]
    public async Task AddAsync_MaliyetPolitikasiYoksaMaliyetEtkiliHareketiReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        dbContext.StokMaliyetPolitikalari.RemoveRange(dbContext.StokMaliyetPolitikalari);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100)));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("2026 mali yılı için stok maliyet yöntemi seçilmelidir.", ex.Message);
    }

    [Fact]
    public async Task StokMaliyetPolitikasiService_TarihIcinMuhasebeDonemiYoksaFallbackYapmadanHataVerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        dbContext.MuhasebeDonemler.RemoveRange(dbContext.MuhasebeDonemler);
        await dbContext.SaveChangesAsync();
        var politikaService = CreatePolicyService(dbContext, CreateRealMuhasebeDonemService(dbContext));

        var ex = await Assert.ThrowsAsync<BaseException>(() => politikaService.GetCurrentAsync(1, new DateTime(2027, 1, 15)));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal("Bu tarih için muhasebe dönemi tanımlanmamıştır.", ex.Message);
    }

    [Fact]
    public async Task StokMaliyetPolitikasiService_KapaliDonemdenDeMaliYilCozebilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var donem = await dbContext.MuhasebeDonemler.SingleAsync();
        donem.KapaliMi = true;
        await dbContext.SaveChangesAsync();
        var politikaService = CreatePolicyService(dbContext, CreateRealMuhasebeDonemService(dbContext));

        var result = await politikaService.GetCurrentAsync(1, new DateTime(2026, 8, 15));

        Assert.Equal(2026, result.MaliYil);
        Assert.True(result.PolitikaSecildiMi);
        Assert.Equal(StokMaliyetYontemleri.AgirlikliOrtalama, result.MaliyetYontemi);
    }

    [Fact]
    public async Task StokMaliyetPolitikasiService_2026Politikasini2027IcinOtomatikKullanmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        dbContext.MuhasebeDonemler.Add(new MuhasebeDonem
        {
            Id = 2,
            TesisId = 1,
            MaliYil = 2027,
            DonemNo = 1,
            BaslangicTarihi = new DateTime(2027, 1, 1),
            BitisTarihi = new DateTime(2027, 1, 31),
            KapaliMi = false
        });
        await dbContext.SaveChangesAsync();
        var politikaService = CreatePolicyService(dbContext, CreateRealMuhasebeDonemService(dbContext));

        var current = await politikaService.GetCurrentAsync(1, new DateTime(2027, 1, 15));
        var ex = await Assert.ThrowsAsync<BaseException>(() => politikaService.GetRequiredMaliyetYontemiAsync(1, new DateTime(2027, 1, 15)));

        Assert.Equal(2027, current.MaliYil);
        Assert.False(current.PolitikaSecildiMi);
        Assert.Null(current.MaliyetYontemi);
        Assert.Equal("2027 mali yılı için stok maliyet yöntemi seçilmelidir.", ex.Message);
    }

    [Fact]
    public async Task StokMaliyetPolitikasiService_2026FifoAcikKatmanVarken2027AgirlikliOrtalamaOlusturmayiReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        dbContext.MuhasebeDonemler.Add(new MuhasebeDonem
        {
            Id = 2,
            TesisId = 1,
            MaliYil = 2027,
            DonemNo = 1,
            BaslangicTarihi = new DateTime(2027, 1, 1),
            BitisTarihi = new DateTime(2027, 1, 31),
            KapaliMi = false
        });
        await dbContext.SaveChangesAsync();
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.FIFO);
        var service = CreateService(dbContext);
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        var politikaService = CreatePolicyService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => politikaService.UpsertAsync(new UpsertStokMaliyetPolitikasiRequest
        {
            TesisId = 1,
            MaliYil = 2027,
            MaliyetYontemi = StokMaliyetYontemleri.AgirlikliOrtalama
        }));

        Assert.Equal("Açık maliyet katmanları bulunduğu için stok maliyet yöntemi değiştirilemez.", ex.Message);
    }

    [Fact]
    public async Task StokMaliyetPolitikasiService_2026FifoAcikKatmanVarken2027FifoOlusturabilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        dbContext.MuhasebeDonemler.Add(new MuhasebeDonem
        {
            Id = 2,
            TesisId = 1,
            MaliYil = 2027,
            DonemNo = 1,
            BaslangicTarihi = new DateTime(2027, 1, 1),
            BitisTarihi = new DateTime(2027, 1, 31),
            KapaliMi = false
        });
        await dbContext.SaveChangesAsync();
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.FIFO);
        var service = CreateService(dbContext);
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        var politikaService = CreatePolicyService(dbContext);

        var result = await politikaService.UpsertAsync(new UpsertStokMaliyetPolitikasiRequest
        {
            TesisId = 1,
            MaliYil = 2027,
            MaliyetYontemi = StokMaliyetYontemleri.FIFO
        });

        Assert.Equal(StokMaliyetYontemleri.FIFO, result.MaliyetYontemi);
    }

    [Fact]
    public async Task StokMaliyetPolitikasiService_2025LifoTukendi2026FifoAcik2027FifoOlusturabilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        dbContext.MuhasebeDonemler.AddRange(
            new MuhasebeDonem
            {
                Id = 2,
                TesisId = 1,
                MaliYil = 2025,
                DonemNo = 1,
                BaslangicTarihi = new DateTime(2025, 1, 1),
                BitisTarihi = new DateTime(2025, 12, 31),
                KapaliMi = true
            },
            new MuhasebeDonem
            {
                Id = 3,
                TesisId = 1,
                MaliYil = 2027,
                DonemNo = 1,
                BaslangicTarihi = new DateTime(2027, 1, 1),
                BitisTarihi = new DateTime(2027, 1, 31),
                KapaliMi = false
            });
        dbContext.StokMaliyetPolitikalari.Add(new STYS.Muhasebe.StokMaliyetPolitikalari.Entities.StokMaliyetPolitikasi
        {
            TesisId = 1,
            MaliYil = 2025,
            MaliyetYontemi = StokMaliyetYontemleri.LIFO
        });
        await dbContext.SaveChangesAsync();
        await SeedFifoKatmanAsync(dbContext, 10, 100, 3, 50, StokMaliyetKatmanKaynakTipleri.StokHareketi, new DateTime(2025, 6, 1), StokMaliyetYontemleri.LIFO, 0);
        await SeedFifoKatmanAsync(dbContext, 10, 100, 5, 80, StokMaliyetKatmanKaynakTipleri.StokHareketi, new DateTime(2026, 8, 1), StokMaliyetYontemleri.FIFO);
        var politikaService = CreatePolicyService(dbContext);

        var result = await politikaService.UpsertAsync(new UpsertStokMaliyetPolitikasiRequest
        {
            TesisId = 1,
            MaliYil = 2027,
            MaliyetYontemi = StokMaliyetYontemleri.FIFO
        });

        Assert.Equal(StokMaliyetYontemleri.FIFO, result.MaliyetYontemi);
    }

    [Fact]
    public async Task StokMaliyetPolitikasiService_2025FifoTukendi2026LifoAcik2027LifoOlusturabilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        dbContext.MuhasebeDonemler.AddRange(
            new MuhasebeDonem
            {
                Id = 2,
                TesisId = 1,
                MaliYil = 2025,
                DonemNo = 1,
                BaslangicTarihi = new DateTime(2025, 1, 1),
                BitisTarihi = new DateTime(2025, 12, 31),
                KapaliMi = true
            },
            new MuhasebeDonem
            {
                Id = 3,
                TesisId = 1,
                MaliYil = 2027,
                DonemNo = 1,
                BaslangicTarihi = new DateTime(2027, 1, 1),
                BitisTarihi = new DateTime(2027, 1, 31),
                KapaliMi = false
            });
        dbContext.StokMaliyetPolitikalari.Add(new STYS.Muhasebe.StokMaliyetPolitikalari.Entities.StokMaliyetPolitikasi
        {
            TesisId = 1,
            MaliYil = 2025,
            MaliyetYontemi = StokMaliyetYontemleri.FIFO
        });
        await dbContext.SaveChangesAsync();
        await SeedFifoKatmanAsync(dbContext, 10, 100, 3, 50, StokMaliyetKatmanKaynakTipleri.StokHareketi, new DateTime(2025, 6, 1), StokMaliyetYontemleri.FIFO, 0);
        await SeedFifoKatmanAsync(dbContext, 10, 100, 5, 80, StokMaliyetKatmanKaynakTipleri.StokHareketi, new DateTime(2026, 8, 1), StokMaliyetYontemleri.LIFO);
        var politikaService = CreatePolicyService(dbContext);

        var result = await politikaService.UpsertAsync(new UpsertStokMaliyetPolitikasiRequest
        {
            TesisId = 1,
            MaliYil = 2027,
            MaliyetYontemi = StokMaliyetYontemleri.LIFO
        });

        Assert.Equal(StokMaliyetYontemleri.LIFO, result.MaliyetYontemi);
    }

    [Fact]
    public async Task StokMaliyetPolitikasiService_AcikLifoKatmanVarkenFifoSeciminiReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedFifoKatmanAsync(dbContext, 10, 100, 5, 80, StokMaliyetKatmanKaynakTipleri.StokHareketi, new DateTime(2026, 8, 1), StokMaliyetYontemleri.LIFO);
        var politikaService = CreatePolicyService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => politikaService.UpsertAsync(new UpsertStokMaliyetPolitikasiRequest
        {
            TesisId = 1,
            MaliYil = 2026,
            MaliyetYontemi = StokMaliyetYontemleri.FIFO
        }));

        Assert.Equal("Açık maliyet katmanları bulunduğu için stok maliyet yöntemi değiştirilemez.", ex.Message);
    }

    [Fact]
    public async Task StokMaliyetPolitikasiService_AcikFifoVeLifoKatmaniBirlikteVarsaTutarsizlikHatasiVerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedFifoKatmanAsync(dbContext, 10, 100, 5, 80, StokMaliyetKatmanKaynakTipleri.StokHareketi, new DateTime(2026, 8, 1), StokMaliyetYontemleri.FIFO);
        await SeedFifoKatmanAsync(dbContext, 20, 100, 4, 90, StokMaliyetKatmanKaynakTipleri.StokHareketi, new DateTime(2026, 8, 2), StokMaliyetYontemleri.LIFO);
        var politikaService = CreatePolicyService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => politikaService.UpsertAsync(new UpsertStokMaliyetPolitikasiRequest
        {
            TesisId = 1,
            MaliYil = 2026,
            MaliyetYontemi = StokMaliyetYontemleri.FIFO
        }));

        Assert.Equal("Tesiste farklı maliyet yöntemlerine ait açık maliyet katmanları bulunduğu için işlem yapılamaz.", ex.Message);
    }

    [Fact]
    public async Task StokMaliyetPolitikasiService_2026FifoAcikKatmanVarken2027LifoOlusturmayiReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        dbContext.MuhasebeDonemler.Add(new MuhasebeDonem
        {
            Id = 2,
            TesisId = 1,
            MaliYil = 2027,
            DonemNo = 1,
            BaslangicTarihi = new DateTime(2027, 1, 1),
            BitisTarihi = new DateTime(2027, 1, 31),
            KapaliMi = false
        });
        await dbContext.SaveChangesAsync();
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.FIFO);
        var service = CreateService(dbContext);
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        var politikaService = CreatePolicyService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => politikaService.UpsertAsync(new UpsertStokMaliyetPolitikasiRequest
        {
            TesisId = 1,
            MaliYil = 2027,
            MaliyetYontemi = StokMaliyetYontemleri.LIFO
        }));

        Assert.Equal("Açık maliyet katmanları bulunduğu için stok maliyet yöntemi değiştirilemez.", ex.Message);
    }

    [Fact]
    public async Task CreateFifoBaslangicStoguAsync_LifoPolitikasindaDaCalisir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.LIFO);
        await SeedSourceStockAsync(dbContext, 10);
        var politikaService = CreatePolicyService(dbContext);

        await politikaService.CreateFifoBaslangicStoguAsync(new CreateFifoBaslangicStoguRequest
        {
            TesisId = 1,
            MaliYil = 2026,
            Satirlar = [new CreateFifoBaslangicStoguSatirRequest { DepoId = 10, TasinirKartId = 100, BirimMaliyet = 80 }]
        });

        var katman = await dbContext.StokMaliyetKatmanlari.SingleAsync(x => x.KatmanKaynakTipi == StokMaliyetKatmanKaynakTipleri.BaslangicStogu);

        Assert.Equal(10, katman.KalanMiktar);
        Assert.Equal(80m, katman.BirimMaliyet);
        Assert.Equal(StokMaliyetYontemleri.LIFO, katman.MaliyetYontemi);
    }

    [Fact]
    public async Task GetFifoBaslangicStoguAsync_FizikselStokVarKatmanYoksaKatmansizMiktariDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.FIFO);
        await SeedSourceStockAsync(dbContext, 10);
        var politikaService = CreatePolicyService(dbContext);

        var satir = Assert.Single(await politikaService.GetFifoBaslangicStoguAsync(1, 2026));

        Assert.Equal(10, satir.MevcutStokMiktari);
        Assert.Equal(0, satir.FifoKatmanMiktari);
        Assert.Equal(10, satir.KatmansizMiktar);
    }

    [Fact]
    public async Task CreateFifoBaslangicStoguAsync_KatmanOlustururVeTekrarindaIkinciKatmanUretmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.FIFO);
        await SeedSourceStockAsync(dbContext, 10);
        var politikaService = CreatePolicyService(dbContext);

        await politikaService.CreateFifoBaslangicStoguAsync(new CreateFifoBaslangicStoguRequest
        {
            TesisId = 1,
            MaliYil = 2026,
            Satirlar = [new CreateFifoBaslangicStoguSatirRequest { DepoId = 10, TasinirKartId = 100, BirimMaliyet = 80 }]
        });
        await politikaService.CreateFifoBaslangicStoguAsync(new CreateFifoBaslangicStoguRequest
        {
            TesisId = 1,
            MaliYil = 2026,
            Satirlar = [new CreateFifoBaslangicStoguSatirRequest { DepoId = 10, TasinirKartId = 100, BirimMaliyet = 80 }]
        });

        var katmanlar = await dbContext.StokMaliyetKatmanlari
            .Where(x => x.DepoId == 10 && x.TasinirKartId == 100)
            .OrderBy(x => x.Id)
            .ToListAsync();

        var katman = Assert.Single(katmanlar);
        Assert.Equal(StokMaliyetKatmanKaynakTipleri.BaslangicStogu, katman.KatmanKaynakTipi);
        Assert.Null(katman.KaynakStokHareketId);
        Assert.Equal(10, katman.IlkMiktar);
        Assert.Equal(10, katman.KalanMiktar);
        Assert.Equal(80m, katman.BirimMaliyet);
        Assert.Equal(StokMaliyetYontemleri.FIFO, katman.MaliyetYontemi);
    }

    [Fact]
    public async Task CreateFifoBaslangicStoguAsync_MevcutKatmanVarkenSadeceKatmansizMiktarIcinOlusturur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.FIFO);
        await SeedSourceStockAsync(dbContext, 10);
        await SeedFifoKatmanAsync(dbContext, depoId: 10, tasinirKartId: 100, miktar: 6, birimMaliyet: 70, kaynakTipi: StokMaliyetKatmanKaynakTipleri.BaslangicStogu);
        var politikaService = CreatePolicyService(dbContext);

        await politikaService.CreateFifoBaslangicStoguAsync(new CreateFifoBaslangicStoguRequest
        {
            TesisId = 1,
            MaliYil = 2026,
            Satirlar = [new CreateFifoBaslangicStoguSatirRequest { DepoId = 10, TasinirKartId = 100, BirimMaliyet = 80 }]
        });

        var katmanlar = await dbContext.StokMaliyetKatmanlari
            .Where(x => x.DepoId == 10 && x.TasinirKartId == 100)
            .OrderBy(x => x.Id)
            .ToListAsync();

        Assert.Equal(2, katmanlar.Count);
        Assert.Equal([6m, 4m], katmanlar.Select(x => x.IlkMiktar).ToArray());
    }

    [Fact]
    public async Task CreateFifoBaslangicStoguAsync_FifoOlmayanPolitikadaReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SeedSourceStockAsync(dbContext, 10);
        var politikaService = CreatePolicyService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => politikaService.CreateFifoBaslangicStoguAsync(new CreateFifoBaslangicStoguRequest
        {
            TesisId = 1,
            MaliYil = 2026,
            Satirlar = [new CreateFifoBaslangicStoguSatirRequest { DepoId = 10, TasinirKartId = 100, BirimMaliyet = 80 }]
        }));

        Assert.Equal("Maliyet başlangıç stoğu yalnızca FIFO veya LIFO maliyet politikası seçiliyse oluşturulabilir.", ex.Message);
    }

    [Fact]
    public async Task CreateFifoBaslangicStoguAsync_BaslangicKatmaniSonrakiGirisleBirlikteFifoSirasiIleTuketilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.FIFO);
        await SeedSourceStockAsync(dbContext, 10);
        var politikaService = CreatePolicyService(dbContext);
        var service = CreateService(dbContext);

        await politikaService.CreateFifoBaslangicStoguAsync(new CreateFifoBaslangicStoguRequest
        {
            TesisId = 1,
            MaliYil = 2026,
            Satirlar = [new CreateFifoBaslangicStoguSatirRequest { DepoId = 10, TasinirKartId = 100, BirimMaliyet = 80 }]
        });
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 5, birimFiyat: 120));

        var ilkCikis = await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Cikis, 4, birimFiyat: 1));
        var ikinciCikis = await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Cikis, 8, birimFiyat: 1));

        Assert.Equal(80m, ilkCikis.MaliyetBirimFiyat);
        Assert.Equal(90m, ikinciCikis.MaliyetBirimFiyat);
        Assert.Equal(720m, ikinciCikis.MaliyetTutari);
    }

    [Fact]
    public async Task GetFifoBaslangicStoguAsync_DevredenTamFifoKatmaniVarsaYeniIhtiyacCikmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        dbContext.MuhasebeDonemler.Add(new MuhasebeDonem
        {
            Id = 2,
            TesisId = 1,
            MaliYil = 2027,
            DonemNo = 1,
            BaslangicTarihi = new DateTime(2027, 1, 1),
            BitisTarihi = new DateTime(2027, 1, 31),
            KapaliMi = false
        });
        await dbContext.SaveChangesAsync();
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.FIFO);
        dbContext.StokMaliyetPolitikalari.Add(new STYS.Muhasebe.StokMaliyetPolitikalari.Entities.StokMaliyetPolitikasi
        {
            TesisId = 1,
            MaliYil = 2027,
            MaliyetYontemi = StokMaliyetYontemleri.FIFO
        });
        await SeedSourceStockAsync(dbContext, 10);
        await SeedFifoKatmanAsync(dbContext, depoId: 10, tasinirKartId: 100, miktar: 10, birimMaliyet: 80, kaynakTipi: StokMaliyetKatmanKaynakTipleri.BaslangicStogu, girisTarihi: new DateTime(2026, 1, 1));
        await dbContext.SaveChangesAsync();
        var politikaService = CreatePolicyService(dbContext);

        var result = await politikaService.GetFifoBaslangicStoguAsync(1, 2027);

        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateFifoBaslangicStoguAsync_NegatifMaliyetiReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.FIFO);
        await SeedSourceStockAsync(dbContext, 10);
        var politikaService = CreatePolicyService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => politikaService.CreateFifoBaslangicStoguAsync(new CreateFifoBaslangicStoguRequest
        {
            TesisId = 1,
            MaliYil = 2026,
            Satirlar = [new CreateFifoBaslangicStoguSatirRequest { DepoId = 10, TasinirKartId = 100, BirimMaliyet = -1 }]
        }));

        Assert.Equal("Başlangıç birim maliyeti negatif olamaz.", ex.Message);
    }

    [Fact]
    public async Task CreateFifoBaslangicStoguAsync_SoftDeleteDonemiMaliYilBaslangicindaKullanmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.FIFO);
        await SeedSourceStockAsync(dbContext, 10);
        var silinmisDonem = new MuhasebeDonem
        {
            Id = 2,
            TesisId = 1,
            MaliYil = 2026,
            DonemNo = 0,
            BaslangicTarihi = new DateTime(2025, 12, 1),
            BitisTarihi = new DateTime(2025, 12, 31),
            KapaliMi = false
        };
        dbContext.MuhasebeDonemler.Add(silinmisDonem);
        await dbContext.SaveChangesAsync();
        silinmisDonem.IsDeleted = true;
        await dbContext.SaveChangesAsync();
        var politikaService = CreatePolicyService(dbContext);

        await politikaService.CreateFifoBaslangicStoguAsync(new CreateFifoBaslangicStoguRequest
        {
            TesisId = 1,
            MaliYil = 2026,
            Satirlar = [new CreateFifoBaslangicStoguSatirRequest { DepoId = 10, TasinirKartId = 100, BirimMaliyet = 80 }]
        });

        var katman = await dbContext.StokMaliyetKatmanlari.SingleAsync(x => x.KatmanKaynakTipi == StokMaliyetKatmanKaynakTipleri.BaslangicStogu);

        Assert.Equal(new DateTime(2026, 8, 1), katman.GirisTarihi);
    }

    [Fact]
    public async Task StokMaliyetPolitikasiService_SnapshotVarkenYontemDegisikliginiReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var politikaService = CreatePolicyService(dbContext);
        var service = CreateService(dbContext);

        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.FIFO);
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));

        var ex = await Assert.ThrowsAsync<BaseException>(() => politikaService.UpsertAsync(new UpsertStokMaliyetPolitikasiRequest
        {
            TesisId = 1,
            MaliYil = 2026,
            MaliyetYontemi = StokMaliyetYontemleri.AgirlikliOrtalama
        }));

        Assert.Equal("Açık maliyet katmanları bulunduğu için stok maliyet yöntemi değiştirilemez.", ex.Message);
    }

    [Fact]
    public async Task StokMaliyetPolitikasiService_SnapshotYokkenDesteklenenYontemeGuncelleyebilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var politika = await dbContext.StokMaliyetPolitikalari.SingleAsync();
        politika.MaliyetYontemi = StokMaliyetYontemleri.FIFO;
        await dbContext.SaveChangesAsync();
        var politikaService = CreatePolicyService(dbContext);

        var result = await politikaService.UpsertAsync(new UpsertStokMaliyetPolitikasiRequest
        {
            TesisId = 1,
            MaliYil = 2026,
            MaliyetYontemi = StokMaliyetYontemleri.AgirlikliOrtalama
        });

        Assert.Equal(StokMaliyetYontemleri.AgirlikliOrtalama, result.MaliyetYontemi);
    }

    [Fact]
    public async Task StokMaliyetPolitikasiService_FIFOseciminiKaydedebilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var politikaService = CreatePolicyService(dbContext);

        var result = await politikaService.UpsertAsync(new UpsertStokMaliyetPolitikasiRequest
        {
            TesisId = 1,
            MaliYil = 2026,
            MaliyetYontemi = StokMaliyetYontemleri.FIFO
        });

        Assert.Equal(StokMaliyetYontemleri.FIFO, result.MaliyetYontemi);
    }

    [Fact]
    public async Task AddAsync_FIFO_CikisKatmanlariEnEskiGirislerdenTuketir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.FIFO);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 5, birimFiyat: 160));

        var ilkCikis = await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Cikis, 4, birimFiyat: 1));
        var ikinciCikis = await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Cikis, 8, birimFiyat: 1));
        var katmanlar = await dbContext.StokMaliyetKatmanlari
            .OrderBy(x => x.KaynakStokHareketId)
            .ToListAsync();

        Assert.Equal(100m, ilkCikis.MaliyetBirimFiyat);
        Assert.Equal(400m, ilkCikis.MaliyetTutari);
        Assert.Equal(115m, ikinciCikis.MaliyetBirimFiyat);
        Assert.Equal(920m, ikinciCikis.MaliyetTutari);
        Assert.Equal([0m, 3m], katmanlar.Select(x => x.KalanMiktar).ToArray());
    }

    [Fact]
    public async Task AddAsync_FIFO_GirisKatmaniMaliyetYontemiFifoOlarakYazar()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.FIFO);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));

        var katman = await dbContext.StokMaliyetKatmanlari.SingleAsync();

        Assert.Equal(StokMaliyetYontemleri.FIFO, katman.MaliyetYontemi);
    }

    [Fact]
    public async Task AddAsync_LIFO_CikisKatmanlariEnYeniGirislerdenTuketir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.LIFO);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 5, birimFiyat: 160));

        var ilkCikis = await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Cikis, 4, birimFiyat: 1));
        var ikinciCikis = await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Cikis, 8, birimFiyat: 1));
        var katmanlar = await dbContext.StokMaliyetKatmanlari
            .OrderBy(x => x.KaynakStokHareketId)
            .ToListAsync();

        Assert.Equal(160m, ilkCikis.MaliyetBirimFiyat);
        Assert.Equal(640m, ilkCikis.MaliyetTutari);
        Assert.Equal(107.5m, ikinciCikis.MaliyetBirimFiyat);
        Assert.Equal(860m, ikinciCikis.MaliyetTutari);
        Assert.Equal([3m, 0m], katmanlar.Select(x => x.KalanMiktar).ToArray());
    }

    [Fact]
    public async Task AddAsync_LIFO_GirisKatmaniMaliyetYontemiLifoOlarakYazar()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.LIFO);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));

        var katman = await dbContext.StokMaliyetKatmanlari.SingleAsync();

        Assert.Equal(StokMaliyetYontemleri.LIFO, katman.MaliyetYontemi);
    }

    [Fact]
    public async Task AddAsync_FIFO_TekCikistaIkiKatmanTuketimiOlusturur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.FIFO);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 5, birimFiyat: 160));

        var cikis = await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Cikis, 12, birimFiyat: 1));
        var tuketimler = await dbContext.StokMaliyetKatmanTuketimleri
            .Where(x => x.CikisStokHareketId == cikis.Id)
            .OrderBy(x => x.StokMaliyetKatmaniId)
            .ToListAsync();

        Assert.Equal(2, tuketimler.Count);
        Assert.Equal([10m, 2m], tuketimler.Select(x => x.Miktar).ToArray());
        Assert.Equal([100m, 160m], tuketimler.Select(x => x.BirimMaliyet).ToArray());
        Assert.Equal(1320m, tuketimler.Sum(x => x.Tutar));
    }

    [Fact]
    public async Task AddAsync_LIFO_TekCikistaIkiKatmanTuketimiOlusturur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.LIFO);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 5, birimFiyat: 160));

        var cikis = await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Cikis, 12, birimFiyat: 1));
        var tuketimler = await dbContext.StokMaliyetKatmanTuketimleri
            .Where(x => x.CikisStokHareketId == cikis.Id)
            .OrderBy(x => x.Id)
            .ToListAsync();

        Assert.Equal(2, tuketimler.Count);
        Assert.Equal([5m, 7m], tuketimler.Select(x => x.Miktar).ToArray());
        Assert.Equal([160m, 100m], tuketimler.Select(x => x.BirimMaliyet).ToArray());
        Assert.Equal(1500m, tuketimler.Sum(x => x.Tutar));
    }

    [Fact]
    public async Task CreateTransferAsync_FIFO_KatmanBilesiminiHedefDepoyaAyricaTasir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.FIFO);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 5, birimFiyat: 160));

        var transfer = await service.CreateTransferAsync(CreateTransferRequest(miktar: 12, birimFiyat: 999));
        var hedefGiris = Assert.Single(transfer.Where(x => x.TransferYonu == StokTransferYonleri.Giris));
        var hedefKatmanlar = await dbContext.StokMaliyetKatmanlari
            .Where(x => x.DepoId == 20 && x.KaynakStokHareketId == hedefGiris.Id)
            .OrderBy(x => x.Id)
            .ToListAsync();

        Assert.Equal(2, hedefKatmanlar.Count);
        Assert.Equal([10m, 2m], hedefKatmanlar.Select(x => x.IlkMiktar).ToArray());
        Assert.Equal([100m, 160m], hedefKatmanlar.Select(x => x.BirimMaliyet).ToArray());
        Assert.All(hedefKatmanlar, x => Assert.Equal(StokMaliyetYontemleri.FIFO, x.MaliyetYontemi));
    }

    [Fact]
    public async Task CreateTransferAsync_LIFO_KatmanBilesiminiHedefDepoyaAyricaTasir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.LIFO);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 5, birimFiyat: 160));

        var transfer = await service.CreateTransferAsync(CreateTransferRequest(miktar: 12, birimFiyat: 999));
        var hedefGiris = Assert.Single(transfer.Where(x => x.TransferYonu == StokTransferYonleri.Giris));
        var hedefKatmanlar = await dbContext.StokMaliyetKatmanlari
            .Where(x => x.DepoId == 20 && x.KaynakStokHareketId == hedefGiris.Id)
            .OrderBy(x => x.Id)
            .ToListAsync();

        Assert.Equal(2, hedefKatmanlar.Count);
        Assert.Equal([5m, 7m], hedefKatmanlar.Select(x => x.IlkMiktar).ToArray());
        Assert.Equal([160m, 100m], hedefKatmanlar.Select(x => x.BirimMaliyet).ToArray());
        Assert.All(hedefKatmanlar, x => Assert.Equal(StokMaliyetYontemleri.LIFO, x.MaliyetYontemi));
    }

    [Fact]
    public async Task AddAsync_FIFO_SayimFarkiEksikKatmanTuketir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.FIFO);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 5, birimFiyat: 160));

        var hareket = await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.SayimFarki, 12, birimFiyat: 0, sayimFarkiYonu: StokSayimFarkiYonleri.Eksik));
        var tuketimler = await dbContext.StokMaliyetKatmanTuketimleri
            .Where(x => x.CikisStokHareketId == hareket.Id)
            .OrderBy(x => x.StokMaliyetKatmaniId)
            .ToListAsync();

        Assert.Equal(110m, hareket.MaliyetBirimFiyat);
        Assert.Equal(1320m, hareket.MaliyetTutari);
        Assert.Equal(2, tuketimler.Count);
        Assert.Equal([10m, 2m], tuketimler.Select(x => x.Miktar).ToArray());
    }

    [Fact]
    public async Task AddAsync_LIFO_SayimFarkiEksikKatmanTuketir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.LIFO);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 5, birimFiyat: 160));

        var hareket = await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.SayimFarki, 12, birimFiyat: 0, sayimFarkiYonu: StokSayimFarkiYonleri.Eksik));
        var tuketimler = await dbContext.StokMaliyetKatmanTuketimleri
            .Where(x => x.CikisStokHareketId == hareket.Id)
            .OrderBy(x => x.Id)
            .ToListAsync();

        Assert.Equal(125m, hareket.MaliyetBirimFiyat);
        Assert.Equal(1500m, hareket.MaliyetTutari);
        Assert.Equal(2, tuketimler.Count);
        Assert.Equal([5m, 7m], tuketimler.Select(x => x.Miktar).ToArray());
    }

    [Fact]
    public async Task UpdateAsync_FIFO_MaliyetEtkisizAlanDegisirseSnapshotVeKatmanlarKorunur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.FIFO);
        var service = CreateService(dbContext);

        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));
        await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 5, birimFiyat: 160));
        var cikis = await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Cikis, 4, birimFiyat: 1));
        var oncekiTuketimler = await dbContext.StokMaliyetKatmanTuketimleri
            .Where(x => x.CikisStokHareketId == cikis.Id)
            .Select(x => new { x.StokMaliyetKatmaniId, x.Miktar, x.BirimMaliyet, x.Tutar })
            .ToListAsync();

        var updated = await service.UpdateAsync(new StokHareketDto
        {
            Id = cikis.Id,
            DepoId = cikis.DepoId,
            TasinirKartId = cikis.TasinirKartId,
            HareketTarihi = cikis.HareketTarihi,
            HareketTipi = cikis.HareketTipi,
            Miktar = cikis.Miktar,
            BirimFiyat = cikis.BirimFiyat,
            Tutar = cikis.Tutar,
            BelgeNo = cikis.BelgeNo,
            BelgeTarihi = cikis.BelgeTarihi,
            Aciklama = "Yalnızca açıklama güncellendi",
            CariKartId = cikis.CariKartId,
            KaynakModul = cikis.KaynakModul,
            KaynakId = cikis.KaynakId,
            TransferGrupId = cikis.TransferGrupId,
            TransferYonu = cikis.TransferYonu,
            SayimFarkiYonu = cikis.SayimFarkiYonu,
            StokLotId = cikis.StokLotId,
            StokSeriId = cikis.StokSeriId,
            LotNo = cikis.LotNo,
            SeriNo = cikis.SeriNo,
            SonKullanmaTarihi = cikis.SonKullanmaTarihi,
            KarsiDepoId = cikis.KarsiDepoId,
            Durum = cikis.Durum,
            KdvUygulamaTipi = cikis.KdvUygulamaTipi,
            KdvIstisnaTanimId = cikis.KdvIstisnaTanimId,
            KdvOrani = cikis.KdvOrani
        });

        var sonrakiTuketimler = await dbContext.StokMaliyetKatmanTuketimleri
            .Where(x => x.CikisStokHareketId == cikis.Id)
            .Select(x => new { x.StokMaliyetKatmaniId, x.Miktar, x.BirimMaliyet, x.Tutar })
            .ToListAsync();

        Assert.Equal(cikis.MaliyetBirimFiyat, updated.MaliyetBirimFiyat);
        Assert.Equal(cikis.MaliyetTutari, updated.MaliyetTutari);
        Assert.Equal(oncekiTuketimler, sonrakiTuketimler);
    }

    [Fact]
    public async Task UpdateDeleteAsync_FIFO_MaliyetEtkiliHareketlerdeReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.FIFO);
        var service = CreateService(dbContext);

        var giris = await service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Giris, 10, birimFiyat: 100));

        var updateEx = await Assert.ThrowsAsync<BaseException>(() => service.UpdateAsync(CreateStokHareketDto(
            StokHareketTipleri.Giris,
            10,
            id: giris.Id,
            birimFiyat: 110)));
        var deleteEx = await Assert.ThrowsAsync<BaseException>(() => service.DeleteAsync(giris.Id!.Value));

        Assert.Equal("Maliyet katmanları oluştuğu için bu stok hareketinde maliyet etkili alanlar güncellenemez.", updateEx.Message);
        Assert.Equal("Maliyet katmanları oluştuğu için bu stok hareketi silinemez.", deleteEx.Message);
    }

    [Fact]
    public async Task AddAsync_FIFO_KatmansizLegacyStoktaCikisiReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        await SetCostPolicyAsync(dbContext, StokMaliyetYontemleri.FIFO);
        await SeedSourceStockAsync(dbContext, 10);
        var service = CreateService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddAsync(CreateStokHareketDto(StokHareketTipleri.Cikis, 2, birimFiyat: 1)));

        Assert.Equal("Mevcut stok için maliyet katmanı bulunmuyor. Maliyet başlangıç stoğu oluşturulmalıdır.", ex.Message);
    }

    [Fact]
    public void AddStokMaliyetPolitikasiMigration_BackfillSqlIcerir()
    {
        var migrationPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "backend", "Infrastructure", "EntityFramework", "Migrations", "AddStokMaliyetPolitikasiPerFiscalYear.cs");
        if (!File.Exists(migrationPath))
        {
            migrationPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "backend", "Infrastructure", "EntityFramework", "Migrations");
            var file = Directory.GetFiles(migrationPath, "*AddStokMaliyetPolitikasiPerFiscalYear.cs").Single();
            migrationPath = file;
        }

        var content = File.ReadAllText(migrationPath);

        Assert.Contains("AgirlikliOrtalama", content);
        Assert.Contains("StokMaliyetPolitikalari", content);
        Assert.Contains("MaliyetBirimFiyat", content);
    }

    [Fact]
    public void AddFifoOpeningStockLayersMigration_KatmanKaynakTipiniStokHareketiOlarakBackfillEder()
    {
        var migrationPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "backend", "Infrastructure", "EntityFramework", "Migrations", "20260823203700_AddFifoOpeningStockLayers.cs");
        if (!File.Exists(migrationPath))
        {
            migrationPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "backend", "Infrastructure", "EntityFramework", "Migrations");
            migrationPath = Directory.GetFiles(migrationPath, "*AddFifoOpeningStockLayers.cs").Single();
        }

        var content = File.ReadAllText(migrationPath);

        Assert.Contains("defaultValue: \"StokHareketi\"", content);
        Assert.Contains("UPDATE [muhasebe].[StokMaliyetKatmanlari]", content);
        Assert.Contains("SET [KatmanKaynakTipi] = N'StokHareketi'", content);
    }

    [Fact]
    public void AddCostMethodToStokMaliyetKatmanlariMigration_MaliyetYonteminiBackfillEder()
    {
        var migrationPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "backend", "Infrastructure", "EntityFramework", "Migrations", "20260823220000_AddCostMethodToStokMaliyetKatmanlari.cs");
        var content = File.ReadAllText(migrationPath);

        Assert.Contains("name: \"MaliyetYontemi\"", content);
        Assert.Contains("politika.[MaliyetYontemi]", content);
        Assert.Contains("SET [MaliyetYontemi] = N'FIFO'", content);
        Assert.Contains("CK_StokMaliyetKatmanlari_MaliyetYontemi", content);
    }

    [Fact]
    public void AddCostMethodToStokMaliyetKatmanlariMigration_MigrationsAssemblydeDiscoverEdilir()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=StysMigrationDiscovery;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var dbContext = new StysAppDbContext(options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor())
        {
            AllowExplicitTenantWritesWithoutAmbientTenant = true
        };

        var migrationsAssembly = dbContext.GetService<IMigrationsAssembly>();

        Assert.True(migrationsAssembly.Migrations.ContainsKey("20260823220000_AddCostMethodToStokMaliyetKatmanlari"));
    }

    [Fact]
    public void AddStockLotExpiryWarningsMenuMigration_MigrationsAssemblydeDiscoverEdilir()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=StysMigrationDiscovery;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var dbContext = new StysAppDbContext(options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor())
        {
            AllowExplicitTenantWritesWithoutAmbientTenant = true
        };

        var migrationsAssembly = dbContext.GetService<IMigrationsAssembly>();

        Assert.True(migrationsAssembly.Migrations.ContainsKey("20260823231000_AddStockLotExpiryWarningsMenu"));
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

    private static StokHareketService CreateService(
        StysAppDbContext dbContext,
        FakeMuhasebeDonemService? muhasebeDonemService = null,
        FakeKdvUygulamaService? kdvService = null)
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
            kdvService ?? new FakeKdvUygulamaService(),
            CreatePolicyService(dbContext, muhasebeDonemService),
            new StokMaliyetStrategyResolver([new AgirlikliOrtalamaMaliyetStrategy(dbContext), new FifoMaliyetStrategy(dbContext), new LifoMaliyetStrategy(dbContext)]),
            mapper);
    }

    private static IStokMaliyetPolitikasiService CreatePolicyService(
        StysAppDbContext dbContext,
        IMuhasebeDonemService? muhasebeDonemService = null)
        => new StokMaliyetPolitikasiService(
            dbContext,
            muhasebeDonemService ?? new FakeMuhasebeDonemService(),
            new FakeMuhasebeTesisScopeService(),
            new FakeUserAccessScopeService(DomainAccessScope.Scoped([], [1], [])),
            new StokHareketRepository(dbContext, new MapperConfiguration(cfg => cfg.AddProfile<StokHareketProfile>(), NullLoggerFactory.Instance).CreateMapper()));

    private static IStokLotSktUyariService CreateLotSktUyariService(StysAppDbContext dbContext)
        => new StokLotSktUyariService(
            dbContext,
            new FakeMuhasebeTesisScopeService(),
            new FakeUserAccessScopeService(DomainAccessScope.Scoped([], [1], [])),
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 23, 0, 0, 0, TimeSpan.Zero)));

    private static IMuhasebeDonemService CreateRealMuhasebeDonemService(StysAppDbContext dbContext)
    {
        var mapperConfig = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MuhasebeDonemProfile>();
        }, NullLoggerFactory.Instance);

        var mapper = mapperConfig.CreateMapper();
        return new MuhasebeDonemService(
            new MuhasebeDonemRepository(dbContext, mapper),
            mapper,
            dbContext,
            new FakeMuhasebeTesisScopeService());
    }

    private static StokTransferRequest CreateTransferRequest(int? stokLotId = null, int? stokSeriId = null, string? seriNo = null, decimal miktar = 10, decimal birimFiyat = 1)
    {
        return new StokTransferRequest
        {
            KaynakDepoId = 10,
            HedefDepoId = 20,
            TasinirKartId = 100,
            StokLotId = stokLotId,
            StokSeriId = stokSeriId,
            SeriNo = seriNo,
            HareketTarihi = new DateTime(2026, 8, 21),
            Miktar = miktar,
            BirimFiyat = birimFiyat,
            BelgeNo = "TR-001"
        };
    }

    private static async Task SeedBaseAsync(StysAppDbContext dbContext, DepoMalzemeKayitTipleri malzemeKayitTipi = DepoMalzemeKayitTipleri.MalzemeleriAyriKayittaTut, bool takipliMi = false, string? takipTipi = null)
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
        dbContext.MuhasebeDonemler.Add(new MuhasebeDonem
        {
            Id = 1,
            TesisId = 1,
            MaliYil = 2026,
            DonemNo = 8,
            BaslangicTarihi = new DateTime(2026, 8, 1),
            BitisTarihi = new DateTime(2026, 8, 31),
            KapaliMi = false
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
                MalzemeKayitTipi = malzemeKayitTipi
            },
            new Depo
            {
                Id = 20,
                TesisId = 1,
                Kod = "D-002",
                Ad = "Mutfak Deposu",
                AktifMi = true,
                MuhasebeHesapPlaniId = 1,
                MalzemeKayitTipi = malzemeKayitTipi
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
            TakipliMi = takipliMi,
            TakipTipi = takipTipi ?? (takipliMi ? TasinirKartTakipTipleri.Lot : TasinirKartTakipTipleri.Yok),
            KdvOrani = 20,
            AktifMi = true
        });

        dbContext.StokMaliyetPolitikalari.Add(new STYS.Muhasebe.StokMaliyetPolitikalari.Entities.StokMaliyetPolitikasi
        {
            TesisId = 1,
            MaliYil = 2026,
            MaliyetYontemi = StokMaliyetYontemleri.AgirlikliOrtalama
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

    private static async Task SeedFifoKatmanAsync(
        StysAppDbContext dbContext,
        int depoId,
        int tasinirKartId,
        decimal miktar,
        decimal birimMaliyet,
        string kaynakTipi,
        DateTime? girisTarihi = null,
        string maliyetYontemi = StokMaliyetYontemleri.FIFO,
        decimal? kalanMiktar = null)
    {
        dbContext.StokMaliyetKatmanlari.Add(new StokMaliyetKatmani
        {
            TesisId = 1,
            DepoId = depoId,
            TasinirKartId = tasinirKartId,
            KaynakStokHareketId = null,
            KatmanKaynakTipi = kaynakTipi,
            MaliyetYontemi = maliyetYontemi,
            GirisTarihi = girisTarihi ?? new DateTime(2026, 1, 1),
            IlkMiktar = miktar,
            KalanMiktar = kalanMiktar ?? miktar,
            BirimMaliyet = birimMaliyet
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SetCostPolicyAsync(StysAppDbContext dbContext, string maliyetYontemi)
    {
        var politika = await dbContext.StokMaliyetPolitikalari.SingleAsync();
        politika.MaliyetYontemi = maliyetYontemi;
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedDetayHareketleriAsync(StysAppDbContext dbContext)
    {
        dbContext.StokHareketleri.AddRange(
            new StokHareket
            {
                DepoId = 10,
                TasinirKartId = 100,
                HareketTarihi = new DateTime(2026, 8, 1),
                HareketTipi = StokHareketTipleri.Giris,
                Miktar = 10,
                BirimFiyat = 100,
                Tutar = 1000,
                Durum = StokHareketDurumlari.Aktif,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
                KdvOrani = 20,
                KdvTutari = 200
            },
            new StokHareket
            {
                DepoId = 10,
                TasinirKartId = 100,
                HareketTarihi = new DateTime(2026, 8, 5),
                HareketTipi = StokHareketTipleri.Giris,
                Miktar = 5,
                BirimFiyat = 100,
                Tutar = 500,
                Durum = StokHareketDurumlari.Aktif,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
                KdvOrani = 20,
                KdvTutari = 100
            },
            new StokHareket
            {
                DepoId = 10,
                TasinirKartId = 100,
                HareketTarihi = new DateTime(2026, 8, 10),
                HareketTipi = StokHareketTipleri.Giris,
                Miktar = 8,
                BirimFiyat = 120,
                Tutar = 960,
                Durum = StokHareketDurumlari.Aktif,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
                KdvOrani = 20,
                KdvTutari = 192
            });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedBakiyeKorumaHareketleriAsync(StysAppDbContext dbContext)
    {
        dbContext.StokHareketleri.AddRange(
            new StokHareket
            {
                DepoId = 10,
                TasinirKartId = 100,
                HareketTarihi = new DateTime(2026, 8, 1),
                HareketTipi = StokHareketTipleri.Giris,
                Miktar = 100,
                BirimFiyat = 10,
                Tutar = 1000,
                Durum = StokHareketDurumlari.Aktif,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
                KdvOrani = 20,
                KdvTutari = 200
            },
            new StokHareket
            {
                DepoId = 10,
                TasinirKartId = 100,
                HareketTarihi = new DateTime(2026, 8, 2),
                HareketTipi = StokHareketTipleri.Cikis,
                Miktar = 20,
                BirimFiyat = 10,
                Tutar = 200,
                Durum = StokHareketDurumlari.Aktif,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
                KdvOrani = 20,
                KdvTutari = 40
            },
            new StokHareket
            {
                DepoId = 10,
                TasinirKartId = 100,
                HareketTarihi = new DateTime(2026, 8, 3),
                HareketTipi = StokHareketTipleri.Transfer,
                TransferYonu = StokTransferYonleri.Cikis,
                KarsiDepoId = 20,
                TransferGrupId = Guid.NewGuid(),
                Miktar = 10,
                BirimFiyat = 10,
                Tutar = 100,
                Durum = StokHareketDurumlari.Aktif,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.KdvKapsamDisi,
                KdvOrani = 0,
                KdvTutari = 0
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

    private static StokHareketDto CreateStokHareketDto(
        string hareketTipi,
        decimal miktar,
        int? id = null,
        int depoId = 10,
        int tasinirKartId = 100,
        string durum = StokHareketDurumlari.Aktif,
        string? sayimFarkiYonu = null,
        int kdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
        decimal kdvOrani = 20,
        decimal birimFiyat = 1,
        int? stokLotId = null,
        int? stokSeriId = null,
        string? seriNo = null,
        string? lotNo = null,
        DateTime? sonKullanmaTarihi = null)
    {
        return new StokHareketDto
        {
            Id = id,
            DepoId = depoId,
            TasinirKartId = tasinirKartId,
            StokLotId = stokLotId,
            StokSeriId = stokSeriId,
            LotNo = lotNo,
            SeriNo = seriNo,
            SonKullanmaTarihi = sonKullanmaTarihi,
            HareketTarihi = new DateTime(2026, 8, 21),
            HareketTipi = hareketTipi,
            SayimFarkiYonu = sayimFarkiYonu,
            Miktar = miktar,
            BirimFiyat = birimFiyat,
            Tutar = miktar * birimFiyat,
            Durum = durum,
            KdvUygulamaTipi = kdvUygulamaTipi,
            KdvOrani = kdvOrani
        };
    }

    private static async Task<int> SeedMovementAsync(
        StysAppDbContext dbContext,
        int depoId,
        int tasinirKartId,
        string hareketTipi,
        decimal miktar,
        decimal birimFiyat,
        string durum,
        string? sayimFarkiYonu = null,
        int? stokLotId = null,
        int? stokSeriId = null)
    {
        var entity = new StokHareket
        {
            DepoId = depoId,
            TasinirKartId = tasinirKartId,
            StokLotId = stokLotId,
            StokSeriId = stokSeriId,
            HareketTarihi = new DateTime(2026, 8, 21),
            HareketTipi = hareketTipi,
            SayimFarkiYonu = sayimFarkiYonu,
            Miktar = miktar,
            BirimFiyat = birimFiyat,
            Tutar = miktar * birimFiyat,
            Durum = durum,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
            KdvOrani = 20,
            KdvTutari = Math.Round(miktar * birimFiyat * 0.2m, 2, MidpointRounding.AwayFromZero)
        };

        dbContext.StokHareketleri.Add(entity);
        await dbContext.SaveChangesAsync();
        return entity.Id;
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

    private static async Task<int> CreateLotAsync(StysAppDbContext dbContext, string lotNo, DateTime? sonKullanmaTarihi)
    {
        var lot = new StokLot
        {
            TesisId = 1,
            TasinirKartId = 100,
            LotNo = lotNo,
            SonKullanmaTarihi = sonKullanmaTarihi,
            AktifMi = true
        };

        dbContext.StokLotlar.Add(lot);
        await dbContext.SaveChangesAsync();
        return lot.Id;
    }

    private static async Task SeedCrossTesisDataAsync(StysAppDbContext dbContext)
    {
        dbContext.Tesisler.Add(new Tesis
        {
            Id = 2,
            KurumId = 1,
            IlId = 1,
            Ad = "Tesis 2",
            Telefon = "111",
            Adres = "Adres 2",
            AktifMi = true
        });

        dbContext.TasinirKartlar.Add(new TasinirKart
        {
            Id = 101,
            TesisId = 2,
            TasinirKodId = 200,
            MuhasebeHesapPlaniId = 1,
            StokKodu = "STK-101",
            Ad = "Diger Kart",
            Birim = "Adet",
            MalzemeTipi = MalzemeTipleri.Diger,
            KdvOrani = 20,
            AktifMi = true
        });

        await dbContext.SaveChangesAsync();
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

        public Task<MuhasebeDonemDto?> GetDonemByTarihAsync(int tesisId, DateTime tarih, CancellationToken cancellationToken = default)
            => GetAktifDonemAsync(tesisId, tarih, cancellationToken);

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
        public int CallCount { get; private set; }

        public Task<KdvUygulamaResult> ValidateAndSnapshotAsync(int kdvUygulamaTipi, int? kdvIstisnaTanimId, decimal kdvOrani, decimal tutar, DateTime islemTarihi, KdvIslemYonu islemYonu, CancellationToken cancellationToken = default)
        {
            CallCount++;
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

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _zaman;

        public FixedTimeProvider(DateTimeOffset zaman) => _zaman = zaman;

        public override DateTimeOffset GetUtcNow() => _zaman;
    }

    private sealed class FakeMuhasebeTesisScopeService : IMuhasebeTesisScopeService
    {
        public Task EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int[]> GetEffectiveTesisIdsAsync(CancellationToken cancellationToken = default) => Task.FromResult(new[] { 1 });
        public Task<int[]> GetEffectiveTesisIdsAsync(DomainAccessScope scope, CancellationToken cancellationToken = default) => Task.FromResult(new[] { 1 });
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
