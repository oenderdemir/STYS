using System.Reflection;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeDonemleri.Dtos;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Mapping;
using STYS.Muhasebe.SatisBelgeleri.Repositories;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.Muhasebe.SatisBelgeleri.Services.MuhasebeFisStratejileri;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Dtos;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Entities;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Services;
using STYS.Muhasebe.TevkifatHesapEslemeleri.Dtos;
using STYS.Muhasebe.TevkifatHesapEslemeleri.Entities;
using STYS.Muhasebe.TevkifatHesapEslemeleri.Services;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Alış (ve alış tevkifatlı / alış iade) belgelerinde muhasebe fişinin, tesisteki "ilk"
/// 320 Satıcılar detay hesabı yerine, belgenin kendi tedarikçisine (SatisBelgesi.CariKartId
/// → CariKart.MuhasebeHesapPlaniId) bağlı hesabı kullandığını doğrulayan testler.
///
/// Testler, gerçek SatisBelgesiMuhasebeFisService.MuhasebeFisiOlusturAsync akışını
/// (InMemory DbContext + gerçek repository/strateji sınıfları ile) çalıştırıp oluşan
/// MuhasebeFisSatir ve CariHareket kayıtlarını doğrudan doğrular; yalnızca
/// BuildAlisFisContextAsync'i reflection ile çağırmakla sınırlı DEĞİLDİR.
/// </summary>
public class SatisBelgesiAlisTedarikciHesabiTests
{
    // ─────────────────────────────────────────────────────────────
    // Ana senaryo: iki farklı tedarikçi, iki farklı hesap — doğru
    // tedarikçinin hesabı seçilmeli (gerçek servis akışı)
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task MuhasebeFisiOlusturAsync_IkiTedarikciVarken_IkinciTedarikciSeciliyse_IkinciTedarikcininHesabiKullanilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var hesap1 = await SeedDetayHesapAsync(dbContext, "320-TEDARIKCI-1");
        var hesap2 = await SeedDetayHesapAsync(dbContext, "320-TEDARIKCI-2");
        var tedarikci1 = await SeedTedarikciCariKartAsync(dbContext, hesap1.Id);
        var tedarikci2 = await SeedTedarikciCariKartAsync(dbContext, hesap2.Id);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.StokTicariMal, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.KDVIndirilecek, tesisId: 1);

        var belge = await SeedMuhasebeOnaylanmisBelgeAsync(dbContext, SatisBelgesiTipi.AlisFaturasi, tedarikci2.Id, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Hizmet alimi", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);

        var service = CreateMuhasebeFisService(dbContext);
        var dto = await service.MuhasebeFisiOlusturAsync(belge.Id, CancellationToken.None);

        var fis = await dbContext.MuhasebeFisler
            .Include(x => x.Satirlar)
            .FirstAsync(x => x.Id == dto.MuhasebeFisId);

        var tedarikciSatiri = Assert.Single(fis.Satirlar, s => s.CariKartId == tedarikci2.Id);
        Assert.Equal(hesap2.Id, tedarikciSatiri.MuhasebeHesapPlaniId);

        // Yanlislikla ilk tedarikcinin (veya tesisteki "ilk" 320 hesabinin) SECILMEDIGINI
        // acikca dogrula.
        Assert.DoesNotContain(fis.Satirlar, s => s.MuhasebeHesapPlaniId == hesap1.Id);
        Assert.DoesNotContain(fis.Satirlar, s => s.CariKartId == tedarikci1.Id);
    }

    [Fact]
    public async Task MuhasebeFisiOlusturAsync_StandartAlisFaturasi_TedarikciHesabiVeCariKartIdDogru()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var hesap = await SeedDetayHesapAsync(dbContext, "320-TEDARIKCI-A");
        var tedarikci = await SeedTedarikciCariKartAsync(dbContext, hesap.Id);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.StokTicariMal, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.KDVIndirilecek, tesisId: 1);

        var belge = await SeedMuhasebeOnaylanmisBelgeAsync(dbContext, SatisBelgesiTipi.AlisFaturasi, tedarikci.Id, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Hizmet alimi", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);

        var service = CreateMuhasebeFisService(dbContext);
        var dto = await service.MuhasebeFisiOlusturAsync(belge.Id, CancellationToken.None);

        var fis = await dbContext.MuhasebeFisler.Include(x => x.Satirlar).FirstAsync(x => x.Id == dto.MuhasebeFisId);
        var tedarikciSatiri = Assert.Single(fis.Satirlar, s => s.CariKartId == tedarikci.Id);
        Assert.Equal(hesap.Id, tedarikciSatiri.MuhasebeHesapPlaniId);
        Assert.Equal(1200m, tedarikciSatiri.Alacak); // tedarikci: alacak
        Assert.Equal(0m, tedarikciSatiri.Borc);

        var cariHareket = await dbContext.CariHareketler.FirstAsync(x => x.KaynakId == belge.Id);
        Assert.Equal(tedarikci.Id, cariHareket.CariKartId);

        var toplamBorc = fis.Satirlar.Sum(s => s.Borc);
        var toplamAlacak = fis.Satirlar.Sum(s => s.Alacak);
        Assert.Equal(toplamBorc, toplamAlacak);
    }

    [Fact]
    public async Task MuhasebeFisiOlusturAsync_CariKartIdEksikAlisFaturasi_Reddedilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.StokTicariMal, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.KDVIndirilecek, tesisId: 1);

        var belge = await SeedMuhasebeOnaylanmisBelgeAsync(dbContext, SatisBelgesiTipi.AlisFaturasi, cariKartId: null, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Hizmet alimi", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);

        var service = CreateMuhasebeFisService(dbContext);
        var ex = await Assert.ThrowsAsync<BaseException>(
            () => service.MuhasebeFisiOlusturAsync(belge.Id, CancellationToken.None));

        Assert.Contains("Alış belgesinde tedarikçi cari kart tanımlı değil", ex.Message);
        await AssertHicKayitOlusmadiAsync(dbContext, belge.Id);
    }

    // ─────────────────────────────────────────────────────────────
    // Diğer alış stratejileri (tevkifatlı, iade) — dogrudan strateji
    // cagrisiyla tedarikci hesabi + CariKartId dogrulamasi
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AlisTevkifatliFaturaMuhasebeFisStratejisi_TedarikciHesabiVeCariKartIdDogru()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var giderHesap = await SeedDetayHesapAsync(dbContext, "TEST-GIDER-TEVK");
        var kdvHesap = await SeedDetayHesapAsync(dbContext, "TEST-KDV-TEVK");
        var tedarikciHesap = await SeedDetayHesapAsync(dbContext, "320-TEVK");
        var tedarikci = await SeedTedarikciCariKartAsync(dbContext, tedarikciHesap.Id);

        var belge = BuildSatisBelgesi([
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Tevkifatli hizmet alimi", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Tevkifatli, KdvOrani = 20m,
                TevkifatPay = 5, TevkifatPayda = 10
            }
        ]);
        belge.BelgeTipi = SatisBelgesiTipi.AlisFaturasi;

        var context = BuildAlisFisContext(tedarikciHesap.Id, kdvHesap.Id, tedarikci.Id, hizmetGiderHesapPlaniId: giderHesap.Id);
        var strateji = new AlisTevkifatliFaturaMuhasebeFisStratejisi(
            dbContext, new FakeTasinirKodMuhasebeHesapEslemeService(), new FakeTevkifatHesapEslemeService(hesapPlaniId: 999));

        var satirlar = await strateji.SatirlariOlusturAsync(belge, context, CancellationToken.None);

        var tedarikciSatiri = Assert.Single(satirlar, s => s.MuhasebeHesapPlaniId == tedarikciHesap.Id);
        Assert.Equal(tedarikci.Id, tedarikciSatiri.CariKartId);
        Assert.Equal(1100m, tedarikciSatiri.Alacak); // 1000+200-100

        var toplamBorc = satirlar.Sum(x => x.Borc);
        var toplamAlacak = satirlar.Sum(x => x.Alacak);
        Assert.Equal(toplamBorc, toplamAlacak);
    }

    [Fact]
    public async Task AlisIadeFaturasiMuhasebeFisStratejisi_TedarikciHesabiVeCariKartIdDogru()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var giderHesap = await SeedDetayHesapAsync(dbContext, "TEST-GIDER-IADE");
        var kdvHesap = await SeedDetayHesapAsync(dbContext, "TEST-KDV-IADE");
        var tedarikciHesap = await SeedDetayHesapAsync(dbContext, "320-IADE");
        var tedarikci = await SeedTedarikciCariKartAsync(dbContext, tedarikciHesap.Id);

        var belge = BuildSatisBelgesi([
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Iade edilen hizmet", Miktar = 1, BirimFiyat = 500m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);
        belge.BelgeTipi = SatisBelgesiTipi.AlisIadeFaturasi;

        var context = BuildAlisFisContext(tedarikciHesap.Id, kdvHesap.Id, tedarikci.Id, hizmetGiderHesapPlaniId: giderHesap.Id);
        var strateji = new AlisIadeFaturasiMuhasebeFisStratejisi(dbContext, new FakeTasinirKodMuhasebeHesapEslemeService());

        var satirlar = await strateji.SatirlariOlusturAsync(belge, context, CancellationToken.None);

        var tedarikciSatiri = Assert.Single(satirlar, s => s.MuhasebeHesapPlaniId == tedarikciHesap.Id);
        Assert.Equal(tedarikci.Id, tedarikciSatiri.CariKartId);
        Assert.Equal(600m, tedarikciSatiri.Borc); // tedarikci: borc (iade)
        Assert.Equal(0m, tedarikciSatiri.Alacak);

        var toplamBorc = satirlar.Sum(x => x.Borc);
        var toplamAlacak = satirlar.Sum(x => x.Alacak);
        Assert.Equal(toplamBorc, toplamAlacak);
    }

    // ─────────────────────────────────────────────────────────────
    // Reddedilme senaryolari (tam servis akisi)
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task MuhasebeFisiOlusturAsync_BulunamayanCariKart_Reddedilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.StokTicariMal, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.KDVIndirilecek, tesisId: 1);

        var belge = await SeedMuhasebeOnaylanmisBelgeAsync(dbContext, SatisBelgesiTipi.AlisFaturasi, cariKartId: 99999, [
            StandartAlisSatiri()
        ]);

        var service = CreateMuhasebeFisService(dbContext);
        var ex = await Assert.ThrowsAsync<BaseException>(
            () => service.MuhasebeFisiOlusturAsync(belge.Id, CancellationToken.None));
        Assert.Contains("Cari kart bulunamadı", ex.Message);
        await AssertHicKayitOlusmadiAsync(dbContext, belge.Id);
    }

    [Fact]
    public async Task MuhasebeFisiOlusturAsync_SoftDeleteEdilmisCariKart_Reddedilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var hesap = await SeedDetayHesapAsync(dbContext, "320-SD");
        var tedarikci = await SeedTedarikciCariKartAsync(dbContext, hesap.Id);
        tedarikci.IsDeleted = true;
        await dbContext.SaveChangesAsync();
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.StokTicariMal, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.KDVIndirilecek, tesisId: 1);

        var belge = await SeedMuhasebeOnaylanmisBelgeAsync(dbContext, SatisBelgesiTipi.AlisFaturasi, tedarikci.Id, [StandartAlisSatiri()]);

        var service = CreateMuhasebeFisService(dbContext);
        var ex = await Assert.ThrowsAsync<BaseException>(
            () => service.MuhasebeFisiOlusturAsync(belge.Id, CancellationToken.None));
        Assert.Contains("Cari kart bulunamadı", ex.Message);
        await AssertHicKayitOlusmadiAsync(dbContext, belge.Id);
    }

    [Fact]
    public async Task MuhasebeFisiOlusturAsync_PasifCariKart_Reddedilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var hesap = await SeedDetayHesapAsync(dbContext, "320-PASIF");
        var tedarikci = await SeedTedarikciCariKartAsync(dbContext, hesap.Id, aktifMi: false);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.StokTicariMal, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.KDVIndirilecek, tesisId: 1);

        var belge = await SeedMuhasebeOnaylanmisBelgeAsync(dbContext, SatisBelgesiTipi.AlisFaturasi, tedarikci.Id, [StandartAlisSatiri()]);

        var service = CreateMuhasebeFisService(dbContext);
        var ex = await Assert.ThrowsAsync<BaseException>(
            () => service.MuhasebeFisiOlusturAsync(belge.Id, CancellationToken.None));
        Assert.Contains("pasif durumda", ex.Message);
        await AssertHicKayitOlusmadiAsync(dbContext, belge.Id);
    }

    [Fact]
    public async Task MuhasebeFisiOlusturAsync_MusteriTipliCariKart_AlisFaturasindaReddedilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var hesap = await SeedDetayHesapAsync(dbContext, "120-MUSTERI");
        var musteri = await SeedCariKartAsync(dbContext, hesap.Id, CariKartTipleri.Musteri);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.StokTicariMal, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.KDVIndirilecek, tesisId: 1);

        var belge = await SeedMuhasebeOnaylanmisBelgeAsync(dbContext, SatisBelgesiTipi.AlisFaturasi, musteri.Id, [StandartAlisSatiri()]);

        var service = CreateMuhasebeFisService(dbContext);
        var ex = await Assert.ThrowsAsync<BaseException>(
            () => service.MuhasebeFisiOlusturAsync(belge.Id, CancellationToken.None));
        Assert.Contains("tedarikçi cari kart seçilmelidir", ex.Message);
        await AssertHicKayitOlusmadiAsync(dbContext, belge.Id);
    }

    [Fact]
    public async Task MuhasebeFisiOlusturAsync_BaskaTesiseBagliTedarikci_Reddedilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var hesap = await SeedDetayHesapAsync(dbContext, "320-BASKA-TESIS");
        var tedarikci = await SeedTedarikciCariKartAsync(dbContext, hesap.Id, tesisId: 2);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.StokTicariMal, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.KDVIndirilecek, tesisId: 1);

        var belge = await SeedMuhasebeOnaylanmisBelgeAsync(dbContext, SatisBelgesiTipi.AlisFaturasi, tedarikci.Id, [StandartAlisSatiri()]);

        var service = CreateMuhasebeFisService(dbContext);
        var ex = await Assert.ThrowsAsync<BaseException>(
            () => service.MuhasebeFisiOlusturAsync(belge.Id, CancellationToken.None));
        Assert.Contains("belge tesisiyle uyumlu değil", ex.Message);
        await AssertHicKayitOlusmadiAsync(dbContext, belge.Id);
    }

    [Fact]
    public async Task MuhasebeFisiOlusturAsync_MuhasebeHesapPlaniIdBulunmayanTedarikci_Reddedilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var tedarikci = await SeedTedarikciCariKartAsync(dbContext, muhasebeHesapPlaniId: null);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.StokTicariMal, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.KDVIndirilecek, tesisId: 1);

        var belge = await SeedMuhasebeOnaylanmisBelgeAsync(dbContext, SatisBelgesiTipi.AlisFaturasi, tedarikci.Id, [StandartAlisSatiri()]);

        var service = CreateMuhasebeFisService(dbContext);
        var ex = await Assert.ThrowsAsync<BaseException>(
            () => service.MuhasebeFisiOlusturAsync(belge.Id, CancellationToken.None));
        Assert.Contains("hesap planı bağlantısı bulunamadı", ex.Message);
        await AssertHicKayitOlusmadiAsync(dbContext, belge.Id);
    }

    [Fact]
    public async Task MuhasebeFisiOlusturAsync_SilinmisHesapPlanaBagliTedarikci_Reddedilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var hesap = await SeedDetayHesapAsync(dbContext, "320-SILINMIS");
        hesap.IsDeleted = true;
        await dbContext.SaveChangesAsync();
        var tedarikci = await SeedTedarikciCariKartAsync(dbContext, hesap.Id);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.StokTicariMal, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.KDVIndirilecek, tesisId: 1);

        var belge = await SeedMuhasebeOnaylanmisBelgeAsync(dbContext, SatisBelgesiTipi.AlisFaturasi, tedarikci.Id, [StandartAlisSatiri()]);

        var service = CreateMuhasebeFisService(dbContext);
        var ex = await Assert.ThrowsAsync<BaseException>(
            () => service.MuhasebeFisiOlusturAsync(belge.Id, CancellationToken.None));
        Assert.Contains("aktif/hareket görebilir/detay hesap değil", ex.Message);
        await AssertHicKayitOlusmadiAsync(dbContext, belge.Id);
    }

    [Fact]
    public async Task MuhasebeFisiOlusturAsync_PasifHesapPlanaBagliTedarikci_Reddedilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var hesap = await SeedDetayHesapAsync(dbContext, "320-PASIF-HESAP");
        hesap.AktifMi = false;
        await dbContext.SaveChangesAsync();
        var tedarikci = await SeedTedarikciCariKartAsync(dbContext, hesap.Id);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.StokTicariMal, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.KDVIndirilecek, tesisId: 1);

        var belge = await SeedMuhasebeOnaylanmisBelgeAsync(dbContext, SatisBelgesiTipi.AlisFaturasi, tedarikci.Id, [StandartAlisSatiri()]);

        var service = CreateMuhasebeFisService(dbContext);
        var ex = await Assert.ThrowsAsync<BaseException>(
            () => service.MuhasebeFisiOlusturAsync(belge.Id, CancellationToken.None));
        Assert.Contains("aktif/hareket görebilir/detay hesap değil", ex.Message);
        await AssertHicKayitOlusmadiAsync(dbContext, belge.Id);
    }

    [Fact]
    public async Task MuhasebeFisiOlusturAsync_HareketGoremeyenHesapPlanaBagliTedarikci_Reddedilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var hesap = await SeedDetayHesapAsync(dbContext, "320-HAREKETSIZ");
        hesap.HareketGorebilirMi = false;
        await dbContext.SaveChangesAsync();
        var tedarikci = await SeedTedarikciCariKartAsync(dbContext, hesap.Id);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.StokTicariMal, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.KDVIndirilecek, tesisId: 1);

        var belge = await SeedMuhasebeOnaylanmisBelgeAsync(dbContext, SatisBelgesiTipi.AlisFaturasi, tedarikci.Id, [StandartAlisSatiri()]);

        var service = CreateMuhasebeFisService(dbContext);
        var ex = await Assert.ThrowsAsync<BaseException>(
            () => service.MuhasebeFisiOlusturAsync(belge.Id, CancellationToken.None));
        Assert.Contains("aktif/hareket görebilir/detay hesap değil", ex.Message);
        await AssertHicKayitOlusmadiAsync(dbContext, belge.Id);
    }

    [Fact]
    public async Task MuhasebeFisiOlusturAsync_DetayOlmayanHesapPlanaBagliTedarikci_Reddedilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var hesap = await SeedDetayHesapAsync(dbContext, "320-ANAHESAP");
        hesap.DetayHesapMi = false;
        await dbContext.SaveChangesAsync();
        var tedarikci = await SeedTedarikciCariKartAsync(dbContext, hesap.Id);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.StokTicariMal, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.KDVIndirilecek, tesisId: 1);

        var belge = await SeedMuhasebeOnaylanmisBelgeAsync(dbContext, SatisBelgesiTipi.AlisFaturasi, tedarikci.Id, [StandartAlisSatiri()]);

        var service = CreateMuhasebeFisService(dbContext);
        var ex = await Assert.ThrowsAsync<BaseException>(
            () => service.MuhasebeFisiOlusturAsync(belge.Id, CancellationToken.None));
        Assert.Contains("aktif/hareket görebilir/detay hesap değil", ex.Message);
        await AssertHicKayitOlusmadiAsync(dbContext, belge.Id);
    }

    [Fact]
    public async Task MuhasebeFisiOlusturAsync_BaskaTesiseBagliHesapPlanaBagliTedarikci_Reddedilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var hesap = await SeedDetayHesapAsync(dbContext, "320-BASKA-TESIS-HESAP", tesisId: 2);
        var tedarikci = await SeedTedarikciCariKartAsync(dbContext, hesap.Id);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.StokTicariMal, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.KDVIndirilecek, tesisId: 1);

        var belge = await SeedMuhasebeOnaylanmisBelgeAsync(dbContext, SatisBelgesiTipi.AlisFaturasi, tedarikci.Id, [StandartAlisSatiri()]);

        var service = CreateMuhasebeFisService(dbContext);
        var ex = await Assert.ThrowsAsync<BaseException>(
            () => service.MuhasebeFisiOlusturAsync(belge.Id, CancellationToken.None));
        Assert.Contains("aktif/hareket görebilir/detay hesap değil", ex.Message);
        await AssertHicKayitOlusmadiAsync(dbContext, belge.Id);
    }

    [Fact]
    public async Task MuhasebeFisiOlusturAsync_GlobalHesapPlanaBagliGecerliTedarikci_Kabul()
    {
        await using var dbContext = CreateInMemoryDbContext();
        // TesisId=null (global) hesap plani - alis belgesinin kendi tesisiyle de gecerli olmali.
        var hesap = await SeedDetayHesapAsync(dbContext, "320-GLOBAL", tesisId: null);
        var tedarikci = await SeedTedarikciCariKartAsync(dbContext, hesap.Id);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.StokTicariMal, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.KDVIndirilecek, tesisId: 1);

        var belge = await SeedMuhasebeOnaylanmisBelgeAsync(dbContext, SatisBelgesiTipi.AlisFaturasi, tedarikci.Id, [StandartAlisSatiri()]);

        var service = CreateMuhasebeFisService(dbContext);
        var dto = await service.MuhasebeFisiOlusturAsync(belge.Id, CancellationToken.None);

        Assert.NotNull(dto.MuhasebeFisId);
        var fis = await dbContext.MuhasebeFisler.Include(x => x.Satirlar).FirstAsync(x => x.Id == dto.MuhasebeFisId);
        Assert.Contains(fis.Satirlar, s => s.MuhasebeHesapPlaniId == hesap.Id && s.CariKartId == tedarikci.Id);
    }

    // ─────────────────────────────────────────────────────────────
    // Yardimcilar
    // ─────────────────────────────────────────────────────────────

    private static CreateSatisBelgesiSatiriRequest StandartAlisSatiri() => new()
    {
        SiraNo = 1, Aciklama = "Hizmet alimi", Miktar = 1, BirimFiyat = 1000m,
        KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
    };

    private static StysAppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        // SatisBelgesi artik ITenantEntity oldugundan (bkz. kurum sahipligi), StysAppDbContext.
        // ApplyTenantRules SaveChanges'te bir tenant accessor bekler; SuperAdmin modunda,
        // testlerde acikca atanan KurumId degerleri oldugu gibi kabul edilir.
        return new StysAppDbContext(options, null, new FakeSuperAdminTenantAccessor());
    }

    private sealed class FakeSuperAdminTenantAccessor : TOD.Platform.Security.Auth.Services.ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => null;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [];
        public bool IsSuperAdmin() => true;
        public bool IsKurumAdmin() => false;
    }

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<SatisBelgesiProfile>();
        }, NullLoggerFactory.Instance);
        return config.CreateMapper();
    }

    private static async Task<MuhasebeHesapPlani> SeedDetayHesapAsync(StysAppDbContext dbContext, string tamKod, int? tesisId = 1)
    {
        var hesap = new MuhasebeHesapPlani
        {
            Kod = tamKod,
            TamKod = tamKod,
            Ad = $"Test Hesap {tamKod}",
            HesapTipi = HesapTipi.DetayHesap,
            AktifMi = true,
            DetayHesapMi = true,
            HareketGorebilirMi = true,
            TesisId = tesisId
        };
        dbContext.MuhasebeHesapPlanlari.Add(hesap);
        await dbContext.SaveChangesAsync();
        return hesap;
    }

    private static async Task<CariKart> SeedTedarikciCariKartAsync(
        StysAppDbContext dbContext, int? muhasebeHesapPlaniId = null, bool aktifMi = true, int? tesisId = 1)
        => await SeedCariKartAsync(dbContext, muhasebeHesapPlaniId, CariKartTipleri.Tedarikci, aktifMi, tesisId);

    private static async Task<CariKart> SeedCariKartAsync(
        StysAppDbContext dbContext, int? muhasebeHesapPlaniId, string cariTipi, bool aktifMi = true, int? tesisId = 1)
    {
        var cari = new CariKart
        {
            CariTipi = cariTipi,
            CariKodu = $"TEST-{Guid.NewGuid():N}"[..12],
            UnvanAdSoyad = "Test Cari",
            AktifMi = aktifMi,
            TesisId = tesisId,
            MuhasebeHesapPlaniId = muhasebeHesapPlaniId
        };
        dbContext.CariKartlar.Add(cari);
        await dbContext.SaveChangesAsync();
        return cari;
    }

    private static async Task<SatisBelgesi> SeedMuhasebeOnaylanmisBelgeAsync(
        StysAppDbContext dbContext,
        SatisBelgesiTipi belgeTipi,
        int? cariKartId,
        IEnumerable<CreateSatisBelgesiSatiriRequest> satirRequestleri)
    {
        var belge = BuildSatisBelgesi(satirRequestleri);
        belge.KurumId = 1;
        belge.BelgeTipi = belgeTipi;
        belge.CariKartId = cariKartId;
        belge.Durum = SatisBelgesiDurumu.MuhasebeOnaylandi;

        dbContext.SatisBelgeleri.Add(belge);
        await dbContext.SaveChangesAsync();
        return belge;
    }

    private static SatisBelgesi BuildSatisBelgesi(IEnumerable<CreateSatisBelgesiSatiriRequest> satirRequestleri)
    {
        var belge = new SatisBelgesi
        {
            BelgeNo = "TEST-1",
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            TesisId = 1,
            CariKartId = 1,
            BelgeTarihi = new DateTime(2026, 1, 15)
        };

        foreach (var request in satirRequestleri)
        {
            belge.Satirlar.Add(InvokeCreateSatirFromRequest(request));
        }

        InvokeHesaplaBelgeToplamlari(belge);
        return belge;
    }

    private static SatisBelgesiSatiri InvokeCreateSatirFromRequest(CreateSatisBelgesiSatiriRequest request)
    {
        var method = typeof(SatisBelgesiService).GetMethod("CreateSatirFromRequest", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("CreateSatirFromRequest metodu bulunamadi.");
        return (SatisBelgesiSatiri)method.Invoke(null, [request])!;
    }

    private static void InvokeHesaplaBelgeToplamlari(SatisBelgesi belge)
    {
        var method = typeof(SatisBelgesiService).GetMethod("HesaplaBelgeToplamlari", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("HesaplaBelgeToplamlari metodu bulunamadi.");
        method.Invoke(null, [belge]);
    }

    private static SatisBelgesiMuhasebeFisContext BuildAlisFisContext(
        int cariHesapPlaniId, int kdvHesapPlaniId, int? cariKartId, int? stokHesapPlaniId = null, int? hizmetGiderHesapPlaniId = null) => new()
    {
        TesisId = 1,
        MaliYil = 2026,
        Donem = 1,
        FisTarihi = new DateTime(2026, 1, 15),
        FisNo = "FIS-1",
        BelgeNo = "TEST-1",
        CariHesapPlaniId = cariHesapPlaniId,
        CariKartId = cariKartId,
        GelirHesapPlaniId = 0,
        KdvHesapPlaniId = kdvHesapPlaniId,
        StokHesapPlaniId = stokHesapPlaniId,
        HizmetGiderHesapPlaniId = hizmetGiderHesapPlaniId
    };

    private static async Task AssertHicKayitOlusmadiAsync(StysAppDbContext dbContext, int belgeId)
    {
        Assert.False(await dbContext.MuhasebeFisler.AnyAsync(x => x.KaynakId == belgeId));
        Assert.False(await dbContext.CariHareketler.AnyAsync(x => x.KaynakId == belgeId));
        Assert.False(await dbContext.StokHareketleri.AnyAsync(x => x.KaynakId == belgeId));

        var guncelBelge = await dbContext.SatisBelgeleri.FirstOrDefaultAsync(x => x.Id == belgeId);
        Assert.NotNull(guncelBelge);
        Assert.False(guncelBelge!.MuhasebeFisId.HasValue);
    }

    private static ISatisBelgesiMuhasebeFisService CreateMuhasebeFisService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var repository = new SatisBelgesiRepository(dbContext, mapper);

        var stratejiler = new List<ISatisBelgesiMuhasebeFisStratejisi>
        {
            new SatisFaturasiMuhasebeFisStratejisi(),
            new SatisIadeFaturasiMuhasebeFisStratejisi(dbContext),
            new SatisTevkifatliFaturaMuhasebeFisStratejisi(new FakeTevkifatHesapEslemeService(hesapPlaniId: 999)),
            new AlisFaturasiMuhasebeFisStratejisi(dbContext, new FakeTasinirKodMuhasebeHesapEslemeService()),
            new AlisIadeFaturasiMuhasebeFisStratejisi(dbContext, new FakeTasinirKodMuhasebeHesapEslemeService()),
            new AlisTevkifatliFaturaMuhasebeFisStratejisi(
                dbContext, new FakeTasinirKodMuhasebeHesapEslemeService(), new FakeTevkifatHesapEslemeService(hesapPlaniId: 999))
        };

        return new SatisBelgesiMuhasebeFisService(
            repository,
            dbContext,
            mapper,
            new FakeMuhasebeDonemService(),
            stratejiler,
            NullLogger<SatisBelgesiMuhasebeFisService>.Instance);
    }

    private sealed class FakeMuhasebeDonemService : IMuhasebeDonemService
    {
        public Task<MuhasebeDonemDto?> GetAktifDonemAsync(int tesisId, DateTime tarih, CancellationToken cancellationToken = default)
            => Task.FromResult<MuhasebeDonemDto?>(new MuhasebeDonemDto
            {
                Id = 1,
                TesisId = tesisId,
                MaliYil = 2026,
                DonemNo = 1,
                BaslangicTarihi = new DateTime(2026, 1, 1),
                BitisTarihi = new DateTime(2026, 1, 31),
                KapaliMi = false
            });

        public Task DonemKapatAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task DonemAcAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IEnumerable<MuhasebeDonemDto>> GetAllAsync(Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null)
            => throw new NotImplementedException();

        public Task<MuhasebeDonemDto?> GetByIdAsync(int id, Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null)
            => throw new NotImplementedException();

        public Task<PagedResult<MuhasebeDonemDto>> GetPagedAsync(
            PagedRequest request,
            System.Linq.Expressions.Expression<Func<MuhasebeDonem, bool>>? predicate = null,
            Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null,
            Func<IQueryable<MuhasebeDonem>, IOrderedQueryable<MuhasebeDonem>>? orderBy = null)
            => throw new NotImplementedException();

        public Task<MuhasebeDonemDto> AddAsync(MuhasebeDonemDto dto) => throw new NotImplementedException();

        public Task<MuhasebeDonemDto> UpdateAsync(MuhasebeDonemDto dto) => throw new NotImplementedException();

        public Task DeleteAsync(int id) => throw new NotImplementedException();

        public Task<IEnumerable<MuhasebeDonemDto>> WhereAsync(
            System.Linq.Expressions.Expression<Func<MuhasebeDonem, bool>> predicate,
            Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null)
            => throw new NotImplementedException();

        public Task<bool> AnyAsync(
            System.Linq.Expressions.Expression<Func<MuhasebeDonem, bool>> predicate,
            Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null)
            => throw new NotImplementedException();
    }

    private sealed class FakeTasinirKodMuhasebeHesapEslemeService : ITasinirKodMuhasebeHesapEslemeService
    {
        public Task<List<TasinirKodMuhasebeHesapEslemeDto>> GetByTasinirKodIdAsync(int tasinirKodId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<TasinirKodMuhasebeHesapEslemeDto?> GetVarsayilanAsync(int tasinirKodId, string malzemeTipi, string hareketTipi, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IEnumerable<TasinirKodMuhasebeHesapEslemeDto>> GetAllAsync(Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null)
            => throw new NotImplementedException();

        public Task<TasinirKodMuhasebeHesapEslemeDto?> GetByIdAsync(int id, Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null)
            => throw new NotImplementedException();

        public Task<PagedResult<TasinirKodMuhasebeHesapEslemeDto>> GetPagedAsync(
            PagedRequest request,
            System.Linq.Expressions.Expression<Func<TasinirKodMuhasebeHesapEsleme, bool>>? predicate = null,
            Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null,
            Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IOrderedQueryable<TasinirKodMuhasebeHesapEsleme>>? orderBy = null)
            => throw new NotImplementedException();

        public Task<TasinirKodMuhasebeHesapEslemeDto> AddAsync(TasinirKodMuhasebeHesapEslemeDto dto) => throw new NotImplementedException();

        public Task<TasinirKodMuhasebeHesapEslemeDto> UpdateAsync(TasinirKodMuhasebeHesapEslemeDto dto) => throw new NotImplementedException();

        public Task DeleteAsync(int id) => throw new NotImplementedException();

        public Task<IEnumerable<TasinirKodMuhasebeHesapEslemeDto>> WhereAsync(
            System.Linq.Expressions.Expression<Func<TasinirKodMuhasebeHesapEsleme, bool>> predicate,
            Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null)
            => throw new NotImplementedException();

        public Task<bool> AnyAsync(
            System.Linq.Expressions.Expression<Func<TasinirKodMuhasebeHesapEsleme, bool>> predicate,
            Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null)
            => throw new NotImplementedException();
    }

    private sealed class FakeTevkifatHesapEslemeService(int hesapPlaniId) : ITevkifatHesapEslemeService
    {
        public Task<TevkifatHesapEslemeDto?> GetAktifEslemeAsync(int? tesisId, string islemYonu, int tevkifatPay, int tevkifatPayda, CancellationToken cancellationToken = default)
            => Task.FromResult<TevkifatHesapEslemeDto?>(new TevkifatHesapEslemeDto
            {
                Id = 1,
                TesisId = tesisId,
                IslemYonu = islemYonu,
                TevkifatPay = tevkifatPay,
                TevkifatPayda = tevkifatPayda,
                MuhasebeHesapPlaniId = hesapPlaniId,
                AktifMi = true
            });

        public Task<IEnumerable<TevkifatHesapEslemeDto>> GetAllAsync(int? tesisId = null, string? islemYonu = null, bool? aktifMi = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PagedResult<TevkifatHesapEslemeDto>> GetPagedAsync(PagedRequest request, int? tesisId = null, string? islemYonu = null, bool? aktifMi = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IEnumerable<TevkifatHesapEslemeDto>> GetAllAsync(Func<IQueryable<TevkifatHesapEsleme>, IQueryable<TevkifatHesapEsleme>>? include = null)
            => throw new NotImplementedException();

        public Task<TevkifatHesapEslemeDto?> GetByIdAsync(int id, Func<IQueryable<TevkifatHesapEsleme>, IQueryable<TevkifatHesapEsleme>>? include = null)
            => throw new NotImplementedException();

        public Task<PagedResult<TevkifatHesapEslemeDto>> GetPagedAsync(PagedRequest request, System.Linq.Expressions.Expression<Func<TevkifatHesapEsleme, bool>>? predicate = null, Func<IQueryable<TevkifatHesapEsleme>, IQueryable<TevkifatHesapEsleme>>? include = null, Func<IQueryable<TevkifatHesapEsleme>, IOrderedQueryable<TevkifatHesapEsleme>>? orderBy = null)
            => throw new NotImplementedException();

        public Task<TevkifatHesapEslemeDto> AddAsync(TevkifatHesapEslemeDto dto) => throw new NotImplementedException();

        public Task<TevkifatHesapEslemeDto> UpdateAsync(TevkifatHesapEslemeDto dto) => throw new NotImplementedException();

        public Task DeleteAsync(int id) => throw new NotImplementedException();

        public Task<IEnumerable<TevkifatHesapEslemeDto>> WhereAsync(System.Linq.Expressions.Expression<Func<TevkifatHesapEsleme, bool>> predicate, Func<IQueryable<TevkifatHesapEsleme>, IQueryable<TevkifatHesapEsleme>>? include = null)
            => throw new NotImplementedException();

        public Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<TevkifatHesapEsleme, bool>> predicate, Func<IQueryable<TevkifatHesapEsleme>, IQueryable<TevkifatHesapEsleme>>? include = null)
            => throw new NotImplementedException();
    }
}
