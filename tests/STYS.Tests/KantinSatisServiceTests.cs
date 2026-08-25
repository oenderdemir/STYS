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
using STYS.KantinYonetimi.KantinSatislari.Entities;
using STYS.KantinYonetimi.KantinSatislari.Mapping;
using STYS.KantinYonetimi.KantinSatislari.Repositories;
using STYS.KantinYonetimi.KantinSatislari.Services;
using STYS.KantinYonetimi.Kantinler.Entities;
using STYS.KantinYonetimi.Kantinler.Mapping;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.KasaHareketleri.Entities;
using STYS.Muhasebe.MuhasebeDonemleri.Dtos;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeFisleri.Dtos;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Repositories;
using STYS.Muhasebe.MuhasebeFisleri.Services;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Services;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Entities;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokHareketleri.Services;
using STYS.Muhasebe.StokMaliyetPolitikalari.Dtos;
using STYS.Muhasebe.StokMaliyetPolitikalari.Entities;
using STYS.Muhasebe.StokMaliyetPolitikalari.Services;
using STYS.Muhasebe.StokLotlari.Dtos;
using STYS.Muhasebe.StokLotlari.Entities;
using STYS.Muhasebe.StokSerileri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Dtos;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Services;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Dtos;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Entities;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Services;
using STYS.Muhasebe.TasinirKodlari.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.AspNetCore.Logging;
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
        var satis = await service.AddAsync(new KantinSatisDto { KantinId = 1, SatisNoktasiId = 1, SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0) });

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
        var satis = await service.AddAsync(new KantinSatisDto { KantinId = 1, SatisNoktasiId = 1, SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0) });

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
        var nokta = await dbContext.KantinSatisNoktalari.SingleAsync(x => x.Id == 1);
        nokta.VarsayilanNakitKasaId = null;
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
    public async Task KrediKartiOdeme_RequestHesapYoksa_DefaultPosCozulur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(service);

        var result = await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest
        {
            OdemeYontemi = OdemeYontemleri.KrediKarti,
            Tutar = 50
        });

        var odeme = Assert.Single(result.Odemeler);
        Assert.Equal(102, odeme.KasaBankaHesapId);
    }

    [Fact]
    public async Task KrediKartiOdeme_GecerliRequestHesabi_DefaultPosuEzer()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        dbContext.KasaBankaHesaplari.Add(new KasaBankaHesap
        {
            Id = 104,
            TesisId = 1,
            Tip = KasaBankaHesapTipleri.KrediKarti,
            Kod = "POS-B",
            Ad = "Alternatif POS",
            AktifMi = true
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(service);

        var result = await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest
        {
            OdemeYontemi = OdemeYontemleri.KrediKarti,
            KasaBankaHesapId = 104,
            Tutar = 50
        });

        var odeme = Assert.Single(result.Odemeler);
        Assert.Equal(104, odeme.KasaBankaHesapId);
    }

    [Fact]
    public async Task KrediKartiOdeme_RequestVeDefaultPosYoksa_Reddedilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var nokta = await dbContext.KantinSatisNoktalari.SingleAsync(x => x.Id == 1);
        nokta.VarsayilanPosHesapId = null;
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(service);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest
        {
            OdemeYontemi = OdemeYontemleri.KrediKarti,
            Tutar = 50
        }));

        Assert.Equal("Kredi kartı ödeme için POS hesabı seçimi zorunludur.", ex.Message);
    }

    [Fact]
    public async Task KrediKartiOdeme_NakitKasaFallbackOlmaz_Reddedilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var nokta = await dbContext.KantinSatisNoktalari.SingleAsync(x => x.Id == 1);
        nokta.VarsayilanPosHesapId = null;
        nokta.VarsayilanNakitKasaId = 100;
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(service);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest
        {
            OdemeYontemi = OdemeYontemleri.KrediKarti,
            Tutar = 50
        }));

        Assert.Equal("Kredi kartı ödeme için POS hesabı seçimi zorunludur.", ex.Message);
    }

    [Fact]
    public async Task SplitPayment_DefaultNakitVeDefaultPosIle_Calisir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await service.AddAsync(new KantinSatisDto { KantinId = 1, SatisNoktasiId = 1, SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0) });
        await service.AddSatirAsync(satis.Id!.Value, new AddKantinSatisSatirRequest { KantinUrunId = 1, Miktar = 5 });
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 100 });
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.KrediKarti, Tutar = 150 });

        var result = await service.KesinlestirAsync(satis.Id!.Value);

        Assert.Equal(2, result.Odemeler.Count);
        var nakit = Assert.Single(result.Odemeler, x => x.OdemeYontemi == OdemeYontemleri.Nakit);
        var krediKarti = Assert.Single(result.Odemeler, x => x.OdemeYontemi == OdemeYontemleri.KrediKarti);
        Assert.Equal(100, nakit.KasaBankaHesapId);
        Assert.Equal(102, krediKarti.KasaBankaHesapId);
    }

    [Fact]
    public async Task Satis_DogruSatisNoktasiIdIleOlusur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);

        var satis = await service.AddAsync(new KantinSatisDto { KantinId = 1, SatisNoktasiId = 1, SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0) });

        Assert.Equal(1, satis.SatisNoktasiId);
        Assert.Equal("ANA", satis.SatisNoktasiKod);
    }

    [Fact]
    public async Task Satis_BaskaKantininSatisNoktasiKullanilamaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        dbContext.Kantinler.Add(new Kantin
        {
            Id = 2,
            TesisId = 1,
            DepoId = 10,
            PerakendeCariKartId = 100,
            Kod = "KNT-02",
            Ad = "Yan Kantin",
            AktifMi = true
        });
        dbContext.KantinSatisNoktalari.Add(new KantinSatisNoktasi
        {
            Id = 2,
            KantinId = 2,
            Kod = "ANA",
            Ad = "Ana Satış Noktası",
            VarsayilanMi = true,
            AktifMi = true
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.AddAsync(new KantinSatisDto
        {
            KantinId = 1,
            SatisNoktasiId = 2,
            SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0)
        }));

        Assert.Equal("Satış noktası seçilen kantine ait olmalıdır.", ex.Message);
    }

    [Fact]
    public async Task IkiSatisNoktasi_FarkliKasaVePosKullanir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        dbContext.KasaBankaHesaplari.AddRange(
            new KasaBankaHesap { Id = 105, TesisId = 1, Tip = KasaBankaHesapTipleri.NakitKasa, Kod = "KASA-B", Ad = "Nakit Kasa B", AktifMi = true, MuhasebeHesapPlaniId = 1000 },
            new KasaBankaHesap { Id = 106, TesisId = 1, Tip = KasaBankaHesapTipleri.KrediKarti, Kod = "POS-B", Ad = "POS B", AktifMi = true, MuhasebeHesapPlaniId = 1001 });
        dbContext.KantinSatisNoktalari.Add(new KantinSatisNoktasi
        {
            Id = 2,
            KantinId = 1,
            Kod = "YAN",
            Ad = "Yan Satış Noktası",
            VarsayilanNakitKasaId = 105,
            VarsayilanPosHesapId = 106,
            VarsayilanMi = false,
            AktifMi = true
        });
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var satis = await service.AddAsync(new KantinSatisDto { KantinId = 1, SatisNoktasiId = 2, SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0) });
        await service.AddSatirAsync(satis.Id!.Value, new AddKantinSatisSatirRequest { KantinUrunId = 1, Miktar = 5 });
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 100 });
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.KrediKarti, Tutar = 150 });

        var result = await service.KesinlestirAsync(satis.Id!.Value);

        var nakit = Assert.Single(result.Odemeler, x => x.OdemeYontemi == OdemeYontemleri.Nakit);
        var krediKarti = Assert.Single(result.Odemeler, x => x.OdemeYontemi == OdemeYontemleri.KrediKarti);
        Assert.Equal(105, nakit.KasaBankaHesapId);
        Assert.Equal(106, krediKarti.KasaBankaHesapId);
    }

    [Fact]
    public async Task YetersizStokta_KesinlestirmeRollbackOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await service.AddAsync(new KantinSatisDto { KantinId = 1, SatisNoktasiId = 1, SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0) });
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
        var satis = await service.AddAsync(new KantinSatisDto { KantinId = 1, SatisNoktasiId = 1, SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0) });
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
        var satis = await service.AddAsync(new KantinSatisDto { KantinId = 1, SatisNoktasiId = 1, SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0) });
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
    public async Task K3A_MuhasebeFisiVeKasaMuhasebeKaydiOlusturmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(service);
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 50 });

        var kasa = await dbContext.KasaHareketleri.CountAsync();
        var pos = await dbContext.PosTahsilatValorleri.CountAsync();
        var fis = await dbContext.MuhasebeFisler.CountAsync();

        await service.KesinlestirAsync(satis.Id!.Value);

        Assert.Equal(1, await dbContext.TahsilatOdemeBelgeleri.CountAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisOdeme));
        Assert.Equal(kasa, await dbContext.KasaHareketleri.CountAsync());
        Assert.Equal(pos, await dbContext.PosTahsilatValorleri.CountAsync());
        Assert.Equal(fis, await dbContext.MuhasebeFisler.CountAsync());
    }

    [Fact]
    public void AddKantinSalesK3AMigration_MigrationsAssemblydeDiscoverEdilir()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=StysMigrationDiscoveryKantinSatisK3A;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var dbContext = new StysAppDbContext(options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor())
        {
            AllowExplicitTenantWritesWithoutAmbientTenant = true
        };

        var migrationsAssembly = dbContext.GetService<IMigrationsAssembly>();
        Assert.True(migrationsAssembly.Migrations.ContainsKey("20260824203217_AddKantinSalesK3A"));
    }

    [Fact]
    public async Task PerakendeCariOlmadan_YeniSatisKesinlesemez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var kantin = await dbContext.Kantinler.SingleAsync(x => x.Id == 1);
        kantin.PerakendeCariKartId = null;
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(service);
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 50 });

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.KesinlestirAsync(satis.Id!.Value));

        Assert.Equal("Kantin satışının kesinleşmesi için Perakende Cari seçimi zorunludur.", ex.Message);
    }

    [Fact]
    public async Task NakitOdeme_TahsilatBelgesiUretir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(service);
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 50 });

        var result = await service.KesinlestirAsync(satis.Id!.Value);

        var odeme = Assert.Single(result.Odemeler);
        Assert.NotNull(odeme.TahsilatOdemeBelgesiId);
        var belge = await dbContext.TahsilatOdemeBelgeleri.SingleAsync(x => x.Id == odeme.TahsilatOdemeBelgesiId);
        Assert.Equal(MuhasebeKaynakModulleri.KantinSatisOdeme, belge.KaynakModul);
        Assert.Equal(odeme.Id, belge.KaynakId);
        Assert.Equal(TahsilatOdemeBelgeTipleri.Tahsilat, belge.BelgeTipi);
        Assert.Equal(100, belge.CariKartId);
        Assert.Null(belge.MuhasebeFisId);
    }

    [Fact]
    public async Task KrediKartiOdeme_TahsilatBelgesiVePosValorUretir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await service.AddAsync(new KantinSatisDto { KantinId = 1, SatisNoktasiId = 1, SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0) });
        await service.AddSatirAsync(satis.Id!.Value, new AddKantinSatisSatirRequest { KantinUrunId = 3, Miktar = 1, StokSeriId = 1 });
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.KrediKarti, KasaBankaHesapId = 102, Tutar = 75 });

        var result = await service.KesinlestirAsync(satis.Id!.Value);

        var odeme = Assert.Single(result.Odemeler);
        Assert.NotNull(odeme.TahsilatOdemeBelgesiId);
        Assert.Single(await dbContext.PosTahsilatValorleri.Where(x => x.TahsilatOdemeBelgesiId == odeme.TahsilatOdemeBelgesiId).ToListAsync());
    }

    [Fact]
    public async Task SplitPayment_IkiAyrıTahsilatBelgesiUretir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await service.AddAsync(new KantinSatisDto { KantinId = 1, SatisNoktasiId = 1, SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0) });
        await service.AddSatirAsync(satis.Id!.Value, new AddKantinSatisSatirRequest { KantinUrunId = 1, Miktar = 5 });
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 100, KasaBankaHesapId = 100 });
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.KrediKarti, Tutar = 150, KasaBankaHesapId = 102 });

        var result = await service.KesinlestirAsync(satis.Id!.Value);

        Assert.Equal(2, result.Odemeler.Count);
        Assert.Equal(2, result.Odemeler.Count(x => x.TahsilatOdemeBelgesiId.HasValue));
        Assert.Equal(2, await dbContext.TahsilatOdemeBelgeleri.CountAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisOdeme));
    }

    [Fact]
    public async Task IkinciKesinlestirme_DuplicateTahsilatVeValorUretmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await service.AddAsync(new KantinSatisDto { KantinId = 1, SatisNoktasiId = 1, SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0) });
        await service.AddSatirAsync(satis.Id!.Value, new AddKantinSatisSatirRequest { KantinUrunId = 3, Miktar = 1, StokSeriId = 1 });
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.KrediKarti, KasaBankaHesapId = 102, Tutar = 75 });

        await service.KesinlestirAsync(satis.Id!.Value);
        await service.KesinlestirAsync(satis.Id!.Value);

        Assert.Equal(1, await dbContext.TahsilatOdemeBelgeleri.CountAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisOdeme));
        Assert.Equal(1, await dbContext.PosTahsilatValorleri.CountAsync());
    }

    [Fact]
    public async Task FinansalKayittaHataOlursa_StokDahilTumTransactionRollbackOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext, tahsilatService: new FailingTahsilatOdemeBelgesiService());
        var satis = await CreateDraftWithSingleLineAsync(service);
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 50 });

        await Assert.ThrowsAsync<BaseException>(() => service.KesinlestirAsync(satis.Id!.Value));

        Assert.Equal(0, await dbContext.StokHareketleri.CountAsync(x => x.KaynakModul == "KantinSatisSatir" && x.KaynakId != null && x.KaynakId > 3));
        Assert.Equal(0, await dbContext.TahsilatOdemeBelgeleri.CountAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisOdeme));
        var persisted = await service.GetByIdAsync(satis.Id!.Value);
        Assert.Equal("Taslak", persisted!.Durum);
    }

    [Fact]
    public async Task MevcutTahsilatTutariUyusmazsa_KesinlestirmeRollbackOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(service);
        satis = await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 50 });
        var odemeId = Assert.Single(satis.Odemeler).Id!.Value;

        dbContext.TahsilatOdemeBelgeleri.Add(new TahsilatOdemeBelgesi
        {
            Id = 900,
            BelgeNo = "KNT-1-1-1",
            BelgeTarihi = satis.SatisTarihi,
            BelgeTipi = TahsilatOdemeBelgeTipleri.Tahsilat,
            CariKartId = 100,
            Tutar = 49,
            ParaBirimi = "TRY",
            OdemeYontemi = OdemeYontemleri.Nakit,
            Durum = TahsilatOdemeBelgeDurumlari.Aktif,
            KasaBankaHesapId = 100,
            KaynakModul = MuhasebeKaynakModulleri.KantinSatisOdeme,
            KaynakId = odemeId
        });
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.KesinlestirAsync(satis.Id!.Value));

        Assert.Equal("Mevcut kantin tahsilat belgesi ödeme bilgileriyle uyumsuz.", ex.Message);
        Assert.Equal("Taslak", (await service.GetByIdAsync(satis.Id!.Value))!.Durum);
    }

    [Fact]
    public async Task MevcutTahsilatCariUyusmazsa_KesinlestirmeRollbackOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(service);
        satis = await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 50 });
        var odemeId = Assert.Single(satis.Odemeler).Id!.Value;

        dbContext.TahsilatOdemeBelgeleri.Add(new TahsilatOdemeBelgesi
        {
            Id = 901,
            BelgeNo = "KNT-1-1-2",
            BelgeTarihi = satis.SatisTarihi,
            BelgeTipi = TahsilatOdemeBelgeTipleri.Tahsilat,
            CariKartId = 101,
            Tutar = 50,
            ParaBirimi = "TRY",
            OdemeYontemi = OdemeYontemleri.Nakit,
            Durum = TahsilatOdemeBelgeDurumlari.Aktif,
            KasaBankaHesapId = 100,
            KaynakModul = MuhasebeKaynakModulleri.KantinSatisOdeme,
            KaynakId = odemeId
        });
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.KesinlestirAsync(satis.Id!.Value));

        Assert.Equal("Mevcut kantin tahsilat belgesi ödeme bilgileriyle uyumsuz.", ex.Message);
        Assert.Equal("Taslak", (await service.GetByIdAsync(satis.Id!.Value))!.Durum);
    }

    [Fact]
    public async Task MevcutTahsilatOdemeYontemiVeyaHesabiUyusmazsa_KesinlestirmeRollbackOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(service);
        satis = await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 50 });
        var odemeId = Assert.Single(satis.Odemeler).Id!.Value;

        dbContext.TahsilatOdemeBelgeleri.Add(new TahsilatOdemeBelgesi
        {
            Id = 902,
            BelgeNo = "KNT-1-1-3",
            BelgeTarihi = satis.SatisTarihi,
            BelgeTipi = TahsilatOdemeBelgeTipleri.Tahsilat,
            CariKartId = 100,
            Tutar = 50,
            ParaBirimi = "TRY",
            OdemeYontemi = OdemeYontemleri.KrediKarti,
            Durum = TahsilatOdemeBelgeDurumlari.Aktif,
            KasaBankaHesapId = 102,
            KaynakModul = MuhasebeKaynakModulleri.KantinSatisOdeme,
            KaynakId = odemeId
        });
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.KesinlestirAsync(satis.Id!.Value));

        Assert.Equal("Mevcut kantin tahsilat belgesi ödeme bilgileriyle uyumsuz.", ex.Message);
        Assert.Equal("Taslak", (await service.GetByIdAsync(satis.Id!.Value))!.Durum);
    }

    [Fact]
    public async Task PerakendeCariSonradanTedarikciYapildiysa_KesinlestirmeReddedilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var cari = await dbContext.CariKartlar.SingleAsync(x => x.Id == 100);
        cari.CariTipi = CariKartTipleri.Tedarikci;
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(service);
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 50 });

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.KesinlestirAsync(satis.Id!.Value));

        Assert.Equal("Perakende cari yalnızca müşteri veya kurumsal müşteri tipinde olabilir.", ex.Message);
    }

    [Fact]
    public async Task K3B_SplitPaymentVeMaliyet_Icin_DengeliMuhasebeFisiOlusur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var muhasebeService = CreateMuhasebeFisService(dbContext);

        var satis = await satisService.AddAsync(new KantinSatisDto { KantinId = 1, SatisNoktasiId = 1, SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0) });
        await satisService.AddSatirAsync(satis.Id!.Value, new AddKantinSatisSatirRequest { KantinUrunId = 1, Miktar = 1 });
        await satisService.AddSatirAsync(satis.Id!.Value, new AddKantinSatisSatirRequest { KantinUrunId = 2, Miktar = 1, StokLotId = 1 });
        await satisService.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 40, KasaBankaHesapId = 100 });
        await satisService.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.KrediKarti, Tutar = 30, KasaBankaHesapId = 102 });
        await satisService.KesinlestirAsync(satis.Id!.Value);

        var result = await muhasebeService.MuhasebeFisiOlusturAsync(satis.Id.Value);

        Assert.NotNull(result.MuhasebeFisId);
        Assert.NotNull(result.MuhasebeFisNo);

        var fis = await dbContext.MuhasebeFisler.Include(x => x.Satirlar).SingleAsync(x => x.Id == result.MuhasebeFisId);
        Assert.Equal(85m, fis.ToplamBorc);
        Assert.Equal(85m, fis.ToplamAlacak);
        Assert.Contains(fis.Satirlar, x => x.MuhasebeHesapPlaniId == 1000 && x.Borc == 40m);
        Assert.Contains(fis.Satirlar, x => x.MuhasebeHesapPlaniId == 1001 && x.Borc == 30m);
        Assert.Contains(fis.Satirlar, x => x.MuhasebeHesapPlaniId == 1200 && x.Alacak == 64.48m);
        Assert.Contains(fis.Satirlar, x => x.MuhasebeHesapPlaniId == 1308 && x.Alacak == 3.70m);
        Assert.Contains(fis.Satirlar, x => x.MuhasebeHesapPlaniId == 1310 && x.Alacak == 1.82m);
        Assert.Contains(fis.Satirlar, x => x.MuhasebeHesapPlaniId == 1400 && x.Borc == 15m);
        Assert.Contains(fis.Satirlar, x => x.MuhasebeHesapPlaniId == 1100 && x.Alacak == 10m);
        Assert.Contains(fis.Satirlar, x => x.MuhasebeHesapPlaniId == 1101 && x.Alacak == 5m);
    }

    [Fact]
    public async Task K3B_KrediKartiSatisinda_BorcBankayaDegil_PosHesabinaYazar()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var muhasebeService = CreateMuhasebeFisService(dbContext);

        var satis = await satisService.AddAsync(new KantinSatisDto { KantinId = 1, SatisNoktasiId = 1, SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0) });
        await satisService.AddSatirAsync(satis.Id!.Value, new AddKantinSatisSatirRequest { KantinUrunId = 3, Miktar = 1, StokSeriId = 1 });
        await satisService.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.KrediKarti, Tutar = 75, KasaBankaHesapId = 102 });
        await satisService.KesinlestirAsync(satis.Id!.Value);

        var result = await muhasebeService.MuhasebeFisiOlusturAsync(satis.Id.Value);
        var fis = await dbContext.MuhasebeFisler.Include(x => x.Satirlar).SingleAsync(x => x.Id == result.MuhasebeFisId);

        Assert.Contains(fis.Satirlar, x => x.MuhasebeHesapPlaniId == 1001 && x.Borc == 75m);
        Assert.DoesNotContain(fis.Satirlar, x => x.MuhasebeHesapPlaniId == 1002 && x.Borc > 0);
    }

    [Fact]
    public async Task K3B_OdemeHesabiMuhasebeBaglantisiYoksa_Reddedilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var pos = await dbContext.KasaBankaHesaplari.SingleAsync(x => x.Id == 102);
        pos.MuhasebeHesapPlaniId = null;
        await dbContext.SaveChangesAsync();

        var satisService = CreateService(dbContext);
        var muhasebeService = CreateMuhasebeFisService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(satisService);
        await satisService.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 50, KasaBankaHesapId = 100 });
        await satisService.KesinlestirAsync(satis.Id!.Value);

        var kasa = await dbContext.KasaBankaHesaplari.SingleAsync(x => x.Id == 100);
        kasa.MuhasebeHesapPlaniId = null;
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BaseException>(() => muhasebeService.MuhasebeFisiOlusturAsync(satis.Id.Value));
        Assert.Equal("Ödeme hesabı için muhasebe hesap planı bağlantısı zorunludur.", ex.Message);
    }

    [Fact]
    public async Task K3B_KdvMappingYoksa_Reddedilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        dbContext.MuhasebeVergiHesapEslemeleri.RemoveRange(dbContext.MuhasebeVergiHesapEslemeleri.Where(x => x.Oran == 8));
        await dbContext.SaveChangesAsync();

        var satisService = CreateService(dbContext);
        var muhasebeService = CreateMuhasebeFisService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(satisService);
        await satisService.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 50, KasaBankaHesapId = 100 });
        await satisService.KesinlestirAsync(satis.Id!.Value);

        var ex = await Assert.ThrowsAsync<BaseException>(() => muhasebeService.MuhasebeFisiOlusturAsync(satis.Id.Value));
        Assert.Equal("8% KDV oranı için satış KDV hesabı bulunamadı.", ex.Message);
    }

    [Fact]
    public async Task K3B_IkinciCagri_DuplicateMuhasebeFisiUretmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var muhasebeService = CreateMuhasebeFisService(dbContext);
        var satis = await CreateDraftWithSingleLineAsync(satisService);
        await satisService.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 50, KasaBankaHesapId = 100 });
        await satisService.KesinlestirAsync(satis.Id!.Value);

        await muhasebeService.MuhasebeFisiOlusturAsync(satis.Id.Value);
        var ex = await Assert.ThrowsAsync<BaseException>(() => muhasebeService.MuhasebeFisiOlusturAsync(satis.Id.Value));

        Assert.Equal("Bu kantin satışı için daha önce muhasebe fişi oluşturulmuş.", ex.Message);
        Assert.Equal(1, await dbContext.MuhasebeFisler.CountAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatis && x.KaynakId == satis.Id));
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

    [Fact]
    public void AddKantinSalesK3BAccountingMigration_MigrationsAssemblydeDiscoverEdilir()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=StysMigrationDiscoveryKantinSatisK3B;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var dbContext = new StysAppDbContext(options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor())
        {
            AllowExplicitTenantWritesWithoutAmbientTenant = true
        };

        var migrationsAssembly = dbContext.GetService<IMigrationsAssembly>();
        Assert.True(migrationsAssembly.Migrations.ContainsKey("20260824212438_AddKantinSalesK3BAccounting"));
    }

    [Fact]
    public void AddKantinSatisNoktalariMigration_MigrationsAssemblydeDiscoverEdilir()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=StysMigrationDiscoveryKantinSatisNoktalari;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var dbContext = new StysAppDbContext(options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor())
        {
            AllowExplicitTenantWritesWithoutAmbientTenant = true
        };

        var migrationsAssembly = dbContext.GetService<IMigrationsAssembly>();
        Assert.True(migrationsAssembly.Migrations.ContainsKey("20260825072325_AddKantinSatisNoktalari"));
    }

    private static async Task<KantinSatisDto> CreateKesinlesmisNakitSatisAsync(KantinSatisService service)
    {
        var satis = await CreateDraftWithSingleLineAsync(service);
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 50 });
        return await service.KesinlestirAsync(satis.Id!.Value);
    }

    [Fact]
    public async Task Iptal_NakitSatis_StokGeriGelirVeTahsilatIptalOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await CreateKesinlesmisNakitSatisAsync(service);

        var result = await service.IptalEtAsync(satis.Id!.Value, "Müşteri iptal istedi");

        Assert.Equal(KantinSatisDurumlari.IptalEdildi, result.Durum);
        Assert.NotNull(result.IptalTarihi);
        Assert.Equal("Müşteri iptal istedi", result.IptalAciklamasi);

        var reversal = await dbContext.StokHareketleri.SingleAsync(x => x.KaynakModul == "KantinSatisIptal");
        Assert.Equal(StokHareketTipleri.Giris, reversal.HareketTipi);
        Assert.Equal(1m, reversal.Miktar);
        Assert.Equal(10, reversal.DepoId);

        var belge = await dbContext.TahsilatOdemeBelgeleri.SingleAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisOdeme);
        Assert.Equal(TahsilatOdemeBelgeDurumlari.Iptal, belge.Durum);
    }

    [Fact]
    public async Task Iptal_KrediKarti_ValorVeTahsilatIptalOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await service.AddAsync(new KantinSatisDto { KantinId = 1, SatisNoktasiId = 1, SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0) });
        await service.AddSatirAsync(satis.Id!.Value, new AddKantinSatisSatirRequest { KantinUrunId = 3, Miktar = 1, StokSeriId = 1 });
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.KrediKarti, KasaBankaHesapId = 102, Tutar = 75 });
        await service.KesinlestirAsync(satis.Id!.Value);

        var result = await service.IptalEtAsync(satis.Id!.Value, "İptal");

        Assert.Equal(KantinSatisDurumlari.IptalEdildi, result.Durum);
        var belge = await dbContext.TahsilatOdemeBelgeleri.SingleAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisOdeme);
        Assert.Equal(TahsilatOdemeBelgeDurumlari.Iptal, belge.Durum);
        var valor = await dbContext.PosTahsilatValorleri.SingleAsync();
        Assert.Equal(PosTahsilatValorDurumlari.Iptal, valor.Durum);
    }

    [Fact]
    public async Task Iptal_SplitPayment_IkiTahsilatDaIptalOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await service.AddAsync(new KantinSatisDto { KantinId = 1, SatisNoktasiId = 1, SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0) });
        await service.AddSatirAsync(satis.Id!.Value, new AddKantinSatisSatirRequest { KantinUrunId = 1, Miktar = 5 });
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 100 });
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.KrediKarti, Tutar = 150 });
        await service.KesinlestirAsync(satis.Id!.Value);

        var result = await service.IptalEtAsync(satis.Id!.Value, "İptal");

        Assert.Equal(KantinSatisDurumlari.IptalEdildi, result.Durum);
        Assert.Equal(2, await dbContext.TahsilatOdemeBelgeleri.CountAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisOdeme && x.Durum == TahsilatOdemeBelgeDurumlari.Iptal));
    }

    [Fact]
    public async Task Iptal_MuhasebeFisTaslak_SoftDeleteOlurVeTersKayitYok()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var muhasebeFisService = CreateMuhasebeFisService(dbContext);
        var satis = await CreateKesinlesmisNakitSatisAsync(satisService);
        var fisli = await muhasebeFisService.MuhasebeFisiOlusturAsync(satis.Id!.Value);
        var fisId = fisli.MuhasebeFisId!.Value;

        var service = CreateService(dbContext, muhasebeFisService: new FakeMuhasebeFisService(dbContext));
        var result = await service.IptalEtAsync(satis.Id!.Value, "İptal");

        Assert.Equal(KantinSatisDurumlari.IptalEdildi, result.Durum);
        var fis = await dbContext.MuhasebeFisler.IgnoreQueryFilters().SingleAsync(x => x.Id == fisId);
        Assert.True(fis.IsDeleted);
        Assert.False(await dbContext.MuhasebeFisler.IgnoreQueryFilters().AnyAsync(x => x.Durum == MuhasebeFisDurumlari.TersKayit));
    }

    [Fact]
    public async Task Iptal_MuhasebeFisOnayli_TersKayitOlusur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var muhasebeFisService = CreateMuhasebeFisService(dbContext);
        var satis = await CreateKesinlesmisNakitSatisAsync(satisService);
        var fisli = await muhasebeFisService.MuhasebeFisiOlusturAsync(satis.Id!.Value);
        var fisId = fisli.MuhasebeFisId!.Value;

        var fis = await dbContext.MuhasebeFisler.SingleAsync(x => x.Id == fisId);
        fis.Durum = MuhasebeFisDurumlari.Onayli;
        fis.YevmiyeNo = 1;
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, muhasebeFisService: new FakeMuhasebeFisService(dbContext));
        var result = await service.IptalEtAsync(satis.Id!.Value, "İptal");

        Assert.Equal(KantinSatisDurumlari.IptalEdildi, result.Durum);
        var original = await dbContext.MuhasebeFisler.SingleAsync(x => x.Id == fisId);
        Assert.Equal(MuhasebeFisDurumlari.Iptal, original.Durum);
        Assert.NotNull(original.TersKayitFisId);
        var tersFis = await dbContext.MuhasebeFisler.SingleAsync(x => x.IptalEdilenFisId == fisId);
        Assert.Equal(MuhasebeFisDurumlari.TersKayit, tersFis.Durum);
    }

    [Fact]
    public async Task Iptal_FinansalAdimHata_ButunMutationRollbackOlur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var createService = CreateService(dbContext);
        var satis = await CreateKesinlesmisNakitSatisAsync(createService);

        var cancelService = CreateService(dbContext, tahsilatService: new FakeTahsilatOdemeBelgesiService(dbContext, iptaldaHataFirlat: true));
        await Assert.ThrowsAsync<BaseException>(() => cancelService.IptalEtAsync(satis.Id!.Value, "İptal"));

        // InMemory provider gerçek transaction rollback'i simüle edemez (stok ters hareketi kaydedilir),
        // bu yüzden burada transactional bağlamda KESİN olarak yazılmamış olan alanlar doğrulanır:
        // satış Durum'u ve satırın IptalStokHareketId'si son SaveChanges'a kadar ertelendiğinden iptal
        // adımındaki hata nedeniyle ASLA kalıcılaşmaz.
        dbContext.ChangeTracker.Clear();

        var persisted = await createService.GetByIdAsync(satis.Id!.Value);
        Assert.Equal(KantinSatisDurumlari.Kesinlesti, persisted!.Durum);

        var satir = await dbContext.KantinSatisSatirlari.SingleAsync(x => x.KantinSatisId == satis.Id!.Value);
        Assert.Null(satir.IptalStokHareketId);

        var belge = await dbContext.TahsilatOdemeBelgeleri.SingleAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisOdeme);
        Assert.Equal(TahsilatOdemeBelgeDurumlari.Aktif, belge.Durum);
    }

    [Fact]
    public async Task Iptal_IkinciCagri_YeniReversalUretmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await CreateKesinlesmisNakitSatisAsync(service);

        await service.IptalEtAsync(satis.Id!.Value, "İptal");
        var reversalCount = await dbContext.StokHareketleri.CountAsync(x => x.KaynakModul == "KantinSatisIptal");
        var tahsilatIptalCount = await dbContext.TahsilatOdemeBelgeleri.CountAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisOdeme && x.Durum == TahsilatOdemeBelgeDurumlari.Iptal);

        var second = await service.IptalEtAsync(satis.Id!.Value, "İptal");

        Assert.Equal(KantinSatisDurumlari.IptalEdildi, second.Durum);
        Assert.Equal(reversalCount, await dbContext.StokHareketleri.CountAsync(x => x.KaynakModul == "KantinSatisIptal"));
        Assert.Equal(tahsilatIptalCount, await dbContext.TahsilatOdemeBelgeleri.CountAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisOdeme && x.Durum == TahsilatOdemeBelgeDurumlari.Iptal));
    }

    [Fact]
    public async Task Iptal_KesinlesmisIadeVarken_ReddedilirVeStokDegismez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var iadeService = CreateIadeService(dbContext);
        var satis = await CreateKesinlesmis10UrunSatisAsync(dbContext, satisService);
        var satirId = satis.Satirlar.Single().Id!.Value;

        // 3 adet kesinleşmiş ürün iadesi.
        await iadeService.KesinlestirAsync((await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satirId, 3))).Id!.Value);

        var reversalOncesi = await dbContext.StokHareketleri.CountAsync(x => x.KaynakModul == "KantinSatisIptal");
        var iadeHareketOncesi = await dbContext.StokHareketleri.CountAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisIadeSatir);
        var bakiyeOncesi = await StokBakiyesiAsync(dbContext, 10, 1);

        var ex = await Assert.ThrowsAsync<BaseException>(() => satisService.IptalEtAsync(satis.Id!.Value, "İptal"));

        Assert.Equal("Bu satış için kesinleşmiş ürün iadesi bulunduğundan satış tamamen iptal edilemez.", ex.Message);

        // Yeni KantinSatisIptal (full reversal) stok hareketi oluşmaz; stok bakiyesi değişmez.
        Assert.Equal(reversalOncesi, await dbContext.StokHareketleri.CountAsync(x => x.KaynakModul == "KantinSatisIptal"));
        Assert.Equal(iadeHareketOncesi, await dbContext.StokHareketleri.CountAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisIadeSatir));
        Assert.Equal(bakiyeOncesi, await StokBakiyesiAsync(dbContext, 10, 1));

        // Satış kesinleşmiş durumda kalır.
        var persisted = await satisService.GetByIdAsync(satis.Id!.Value);
        Assert.Equal(KantinSatisDurumlari.Kesinlesti, persisted!.Durum);
    }

    private static async Task<decimal> StokBakiyesiAsync(StysAppDbContext dbContext, int depoId, int tasinirKartId)
    {
        var rows = await dbContext.StokHareketleri
            .AsNoTracking()
            .Where(x => x.DepoId == depoId && x.TasinirKartId == tasinirKartId && !x.IsDeleted && x.Durum == StokHareketDurumlari.Aktif)
            .Select(x => new { x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu, x.Miktar })
            .ToListAsync();

        return rows.Sum(x => StokHareketTipleri.IsGirisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu) ? x.Miktar : -x.Miktar);
    }

    [Fact]
    public async Task Iptal_DepoSonradanDegistirilmis_OriginalDepoyaGeriKoyar()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var service = CreateService(dbContext);
        var satis = await CreateKesinlesmisNakitSatisAsync(service);

        // Satış Depo A (Id=10)'dan kesinleşti; ardından kantinin deposu Depo B'ye değiştirildi.
        dbContext.Depolar.Add(new Depo { Id = 30, TesisId = 1, Kod = "DEP-B", Ad = "Yeni Depo", AktifMi = true });
        var kantin = await dbContext.Kantinler.SingleAsync(x => x.Id == 1);
        kantin.DepoId = 30;
        await dbContext.SaveChangesAsync();

        var result = await service.IptalEtAsync(satis.Id!.Value, "İptal");

        Assert.Equal(KantinSatisDurumlari.IptalEdildi, result.Durum);
        var reversal = await dbContext.StokHareketleri.SingleAsync(x => x.KaynakModul == "KantinSatisIptal");
        Assert.Equal(10, reversal.DepoId);
        Assert.False(await dbContext.StokHareketleri.AnyAsync(x => x.DepoId == 30));
    }

    [Fact]
    public async Task Iptal_FifoKatmanGeriYukleme_OrijinalMaliyetleYeniLayerOlusturur()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);

        dbContext.StokMaliyetKatmanlari.Add(new StokMaliyetKatmani
        {
            Id = 1,
            TesisId = 1,
            DepoId = 10,
            TasinirKartId = 1,
            KaynakStokHareketId = 1,
            KatmanKaynakTipi = StokMaliyetKatmanKaynakTipleri.StokHareketi,
            MaliyetYontemi = StokMaliyetYontemleri.FIFO,
            GirisTarihi = new DateTime(2026, 8, 1),
            IlkMiktar = 10,
            KalanMiktar = 7,
            BirimMaliyet = 10m
        });
        dbContext.StokMaliyetKatmanTuketimleri.Add(new StokMaliyetKatmanTuketimi
        {
            Id = 1,
            CikisStokHareketId = 99,
            StokMaliyetKatmaniId = 1,
            Miktar = 3,
            BirimMaliyet = 10m,
            Tutar = 30m
        });
        await dbContext.SaveChangesAsync();

        var restoreService = new StokMaliyetKatmaniRestoreService(dbContext);
        var original = new StokHareket { Id = 99, DepoId = 10, TasinirKartId = 1 };
        var reversal = new StokHareketDto { Id = 1000, DepoId = 10, TasinirKartId = 1, HareketTarihi = DateTime.UtcNow };

        await restoreService.RestoreLayeredCostIfNeededAsync(original, reversal);

        var katman = await dbContext.StokMaliyetKatmanlari.SingleAsync(x => x.Id == 1);
        Assert.Equal(7m, katman.KalanMiktar);

        var yeniLayer = await dbContext.StokMaliyetKatmanlari.SingleAsync(x => x.KaynakStokHareketId == 1000);
        Assert.Equal(3m, yeniLayer.IlkMiktar);
        Assert.Equal(10m, yeniLayer.BirimMaliyet);
        Assert.Equal(StokMaliyetYontemleri.FIFO, yeniLayer.MaliyetYontemi);
    }

    private static async Task<KantinSatisDto> CreateKesinlesmis10UrunSatisAsync(StysAppDbContext dbContext, KantinSatisService service)
    {
        dbContext.StokHareketleri.Add(new StokHareket
        {
            Id = 90,
            DepoId = 10,
            TasinirKartId = 1,
            HareketTarihi = new DateTime(2026, 8, 24, 7, 0, 0),
            HareketTipi = StokHareketTipleri.Giris,
            Miktar = 5,
            BirimFiyat = 10,
            Tutar = 50,
            Durum = StokHareketDurumlari.Aktif,
            KdvUygulamaTipi = 1,
            KdvOrani = 8,
            KdvTutari = 4
        });
        await dbContext.SaveChangesAsync();

        var satis = await service.AddAsync(new KantinSatisDto { KantinId = 1, SatisNoktasiId = 1, SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0) });
        await service.AddSatirAsync(satis.Id!.Value, new AddKantinSatisSatirRequest { KantinUrunId = 1, Miktar = 10 });
        await service.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 500 });
        return await service.KesinlestirAsync(satis.Id!.Value);
    }

    private static CreateKantinSatisIadeRequest IadeRequest(int kantinSatisId, int satirId, decimal miktar)
        => new()
        {
            KantinSatisId = kantinSatisId,
            Satirlar = [new CreateKantinSatisIadeSatirRequest { KantinSatisSatirId = satirId, Miktar = miktar }]
        };

    [Fact]
    public async Task Iade_OnUrununUcunuIadeEder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var iadeService = CreateIadeService(dbContext);
        var satis = await CreateKesinlesmis10UrunSatisAsync(dbContext, satisService);
        var satirId = satis.Satirlar.Single().Id!.Value;

        var iade = await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satirId, 3));
        var kesinlesmis = await iadeService.KesinlestirAsync(iade.Id!.Value);

        Assert.Equal(KantinSatisIadeDurumlari.Kesinlesti, kesinlesmis.Durum);
        Assert.Equal(KantinSatisIadeFinansalDurumlari.Bekliyor, kesinlesmis.FinansalIadeDurumu);

        var hareket = await dbContext.StokHareketleri.SingleAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisIadeSatir);
        Assert.Equal(StokHareketTipleri.Iade, hareket.HareketTipi);
        Assert.Equal(3m, hareket.Miktar);
        Assert.Equal(10, hareket.DepoId);
        Assert.Equal(1, hareket.TasinirKartId);

        var iadeSatir = await dbContext.KantinSatisIadeSatirlari.SingleAsync();
        Assert.Equal(hareket.Id, iadeSatir.StokHareketId);
    }

    [Fact]
    public async Task Iade_IkinciIadeKalanMiktariDogru()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var iadeService = CreateIadeService(dbContext);
        var satis = await CreateKesinlesmis10UrunSatisAsync(dbContext, satisService);
        var satirId = satis.Satirlar.Single().Id!.Value;

        var iade1 = await iadeService.KesinlestirAsync((await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satirId, 3))).Id!.Value);
        var iade2 = await iadeService.KesinlestirAsync((await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satirId, 7))).Id!.Value);

        Assert.Equal(KantinSatisIadeDurumlari.Kesinlesti, iade1.Durum);
        Assert.Equal(KantinSatisIadeDurumlari.Kesinlesti, iade2.Durum);

        var ozet = await iadeService.GetSatisIadeOzetiAsync(satis.Id!.Value);
        var ozetSatir = ozet.Single();
        Assert.Equal(10m, ozetSatir.SatilanMiktar);
        Assert.Equal(10m, ozetSatir.OncekiIadeMiktari);
        Assert.Equal(0m, ozetSatir.KalanMiktar);
    }

    [Fact]
    public async Task Iade_ToplamIadeSatisMiktariniAsamaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var iadeService = CreateIadeService(dbContext);
        var satis = await CreateKesinlesmis10UrunSatisAsync(dbContext, satisService);
        var satirId = satis.Satirlar.Single().Id!.Value;

        await iadeService.KesinlestirAsync((await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satirId, 3))).Id!.Value);

        var iade2 = await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satirId, 8));
        var ex = await Assert.ThrowsAsync<BaseException>(() => iadeService.KesinlestirAsync(iade2.Id!.Value));

        Assert.Equal("Kümülatif iade miktarı satış miktarını aşamaz.", ex.Message);
    }

    [Fact]
    public async Task Iade_TaslakQuotaTuketmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var iadeService = CreateIadeService(dbContext);
        var satis = await CreateKesinlesmis10UrunSatisAsync(dbContext, satisService);
        var satirId = satis.Satirlar.Single().Id!.Value;

        // Taslak iade (8) quota tüketmez.
        await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satirId, 8));

        // Başka bir iade (10) kesinleşebilir — Taslak 8 sayılmaz.
        var kesinlesmis = await iadeService.KesinlestirAsync((await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satirId, 10))).Id!.Value);
        Assert.Equal(KantinSatisIadeDurumlari.Kesinlesti, kesinlesmis.Durum);
    }

    [Fact]
    public async Task Iade_IptalEdilmisSatistanIadeOlmaz()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var iadeService = CreateIadeService(dbContext);
        var satis = await CreateKesinlesmisNakitSatisAsync(satisService);
        await satisService.IptalEtAsync(satis.Id!.Value, "iptal");

        var ex = await Assert.ThrowsAsync<BaseException>(() => iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satis.Satirlar.Single().Id!.Value, 1)));

        Assert.Equal("Yalnızca kesinleşmiş satışlardan iade yapılabilir.", ex.Message);
    }

    [Fact]
    public async Task Iade_OriginalDepoyaDoner()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var iadeService = CreateIadeService(dbContext);
        var satis = await CreateKesinlesmisNakitSatisAsync(satisService);

        dbContext.Depolar.Add(new Depo { Id = 30, TesisId = 1, Kod = "DEP-B", Ad = "Yeni Depo", AktifMi = true });
        var kantin = await dbContext.Kantinler.SingleAsync(x => x.Id == 1);
        kantin.DepoId = 30;
        await dbContext.SaveChangesAsync();

        await iadeService.KesinlestirAsync((await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satis.Satirlar.Single().Id!.Value, 1))).Id!.Value);

        var hareket = await dbContext.StokHareketleri.SingleAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisIadeSatir);
        Assert.Equal(10, hareket.DepoId);
        Assert.False(await dbContext.StokHareketleri.AnyAsync(x => x.DepoId == 30));
    }

    [Fact]
    public async Task Iade_LotVeSeriKorunur_SeriIkinciKezIadeEdilemez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var iadeService = CreateIadeService(dbContext);

        var satis = await satisService.AddAsync(new KantinSatisDto { KantinId = 1, SatisNoktasiId = 1, SatisTarihi = new DateTime(2026, 8, 24, 10, 0, 0) });
        await satisService.AddSatirAsync(satis.Id!.Value, new AddKantinSatisSatirRequest { KantinUrunId = 2, Miktar = 2, StokLotId = 1 });
        await satisService.AddSatirAsync(satis.Id!.Value, new AddKantinSatisSatirRequest { KantinUrunId = 3, Miktar = 1, StokSeriId = 1 });
        await satisService.AddOdemeAsync(satis.Id!.Value, new AddKantinSatisOdemeRequest { OdemeYontemi = OdemeYontemleri.Nakit, Tutar = 115 });
        var kesinlesmis = await satisService.KesinlestirAsync(satis.Id!.Value);

        var lotSatir = kesinlesmis.Satirlar.Single(x => x.KantinUrunId == 2);
        var seriSatir = kesinlesmis.Satirlar.Single(x => x.KantinUrunId == 3);

        // Lot takipli: 1 birim iade -> lot korunur.
        await iadeService.KesinlestirAsync((await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, lotSatir.Id!.Value, 1))).Id!.Value);
        var lotHareket = await dbContext.StokHareketleri.SingleAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisIadeSatir && x.StokLotId == 1);
        Assert.Equal(1, lotHareket.StokLotId);

        // Seri takipli: 1 birim iade -> seri korunur.
        await iadeService.KesinlestirAsync((await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, seriSatir.Id!.Value, 1))).Id!.Value);
        var seriHareket = await dbContext.StokHareketleri.SingleAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisIadeSatir && x.StokSeriId == 1);
        Assert.Equal(1, seriHareket.StokSeriId);

        // Seri takipli ürün ikinci kez iade edilemez.
        var seriIade2 = await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, seriSatir.Id!.Value, 1));
        var ex = await Assert.ThrowsAsync<BaseException>(() => iadeService.KesinlestirAsync(seriIade2.Id!.Value));
        Assert.Equal("Seri takipli ürün yalnızca bir kez iade edilebilir.", ex.Message);
    }

    [Fact]
    public async Task Iade_MaliyetOriginalHarekettenGelir_WeightedAverage()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var iadeService = CreateIadeService(dbContext);
        var satis = await CreateKesinlesmis10UrunSatisAsync(dbContext, satisService);

        await iadeService.KesinlestirAsync((await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satis.Satirlar.Single().Id!.Value, 3))).Id!.Value);

        var hareket = await dbContext.StokHareketleri.SingleAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisIadeSatir);
        Assert.Equal(10m, hareket.MaliyetBirimFiyat);
        Assert.Equal(30m, hareket.MaliyetTutari);
        // Weighted-average (katman yok) -> yeni layer üretilmez.
        Assert.False(await dbContext.StokMaliyetKatmanlari.AnyAsync());
    }

    [Fact]
    public async Task Iade_FifoPartialRestore_YeniLayerOlustururVeConsumptionDegismez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var satis = await CreateKesinlesmis10UrunSatisAsync(dbContext, satisService);

        var originalMovement = await dbContext.StokHareketleri.SingleAsync(x => x.KaynakModul == "KantinSatisSatir");
        dbContext.StokMaliyetKatmanlari.Add(new StokMaliyetKatmani
        {
            Id = 1,
            TesisId = 1,
            DepoId = 10,
            TasinirKartId = 1,
            KaynakStokHareketId = 90,
            KatmanKaynakTipi = StokMaliyetKatmanKaynakTipleri.StokHareketi,
            MaliyetYontemi = StokMaliyetYontemleri.FIFO,
            GirisTarihi = new DateTime(2026, 8, 24),
            IlkMiktar = 10,
            KalanMiktar = 0,
            BirimMaliyet = 10m
        });
        dbContext.StokMaliyetKatmanTuketimleri.Add(new StokMaliyetKatmanTuketimi
        {
            Id = 1,
            CikisStokHareketId = originalMovement.Id,
            StokMaliyetKatmaniId = 1,
            Miktar = 10,
            BirimMaliyet = 10m,
            Tutar = 100m
        });
        await dbContext.SaveChangesAsync();

        var iadeService = CreateIadeService(dbContext, stokMaliyetKatmaniRestoreService: new StokMaliyetKatmaniRestoreService(dbContext));
        var iade = await iadeService.KesinlestirAsync((await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satis.Satirlar.Single().Id!.Value, 3))).Id!.Value);

        Assert.Equal(KantinSatisIadeDurumlari.Kesinlesti, iade.Durum);

        // Orijinal consumption kaydı değişmez.
        var tuketim = await dbContext.StokMaliyetKatmanTuketimleri.SingleAsync(x => x.Id == 1);
        Assert.Equal(10m, tuketim.Miktar);

        // Yeni incoming layer iade miktarı ve orijinal maliyetle oluşur.
        var iadeHareket = await dbContext.StokHareketleri.SingleAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisIadeSatir);
        var yeniLayer = await dbContext.StokMaliyetKatmanlari.SingleAsync(x => x.KaynakStokHareketId == iadeHareket.Id);
        Assert.Equal(3m, yeniLayer.IlkMiktar);
        Assert.Equal(10m, yeniLayer.BirimMaliyet);
        Assert.Equal(StokMaliyetYontemleri.FIFO, yeniLayer.MaliyetYontemi);
    }

    private static async Task SeedKarimliFifoTuketimiAsync(StysAppDbContext dbContext, int cikisStokHareketId)
    {
        dbContext.StokMaliyetKatmanlari.AddRange(
            new StokMaliyetKatmani { Id = 1, TesisId = 1, DepoId = 10, TasinirKartId = 1, KaynakStokHareketId = 90, KatmanKaynakTipi = StokMaliyetKatmanKaynakTipleri.StokHareketi, MaliyetYontemi = StokMaliyetYontemleri.FIFO, GirisTarihi = new DateTime(2026, 8, 24), IlkMiktar = 2, KalanMiktar = 0, BirimMaliyet = 10m },
            new StokMaliyetKatmani { Id = 2, TesisId = 1, DepoId = 10, TasinirKartId = 1, KaynakStokHareketId = 90, KatmanKaynakTipi = StokMaliyetKatmanKaynakTipleri.StokHareketi, MaliyetYontemi = StokMaliyetYontemleri.FIFO, GirisTarihi = new DateTime(2026, 8, 25), IlkMiktar = 3, KalanMiktar = 0, BirimMaliyet = 12m });
        dbContext.StokMaliyetKatmanTuketimleri.AddRange(
            new StokMaliyetKatmanTuketimi { Id = 1, CikisStokHareketId = cikisStokHareketId, StokMaliyetKatmaniId = 1, Miktar = 2, BirimMaliyet = 10m, Tutar = 20m },
            new StokMaliyetKatmanTuketimi { Id = 2, CikisStokHareketId = cikisStokHareketId, StokMaliyetKatmaniId = 2, Miktar = 3, BirimMaliyet = 12m, Tutar = 36m });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedKarimliLifoTuketimiAsync(StysAppDbContext dbContext, int cikisStokHareketId)
    {
        dbContext.StokMaliyetKatmanlari.AddRange(
            new StokMaliyetKatmani { Id = 1, TesisId = 1, DepoId = 10, TasinirKartId = 1, KaynakStokHareketId = 90, KatmanKaynakTipi = StokMaliyetKatmanKaynakTipleri.StokHareketi, MaliyetYontemi = StokMaliyetYontemleri.LIFO, GirisTarihi = new DateTime(2026, 8, 25), IlkMiktar = 3, KalanMiktar = 0, BirimMaliyet = 12m },
            new StokMaliyetKatmani { Id = 2, TesisId = 1, DepoId = 10, TasinirKartId = 1, KaynakStokHareketId = 90, KatmanKaynakTipi = StokMaliyetKatmanKaynakTipleri.StokHareketi, MaliyetYontemi = StokMaliyetYontemleri.LIFO, GirisTarihi = new DateTime(2026, 8, 24), IlkMiktar = 2, KalanMiktar = 0, BirimMaliyet = 10m });
        dbContext.StokMaliyetKatmanTuketimleri.AddRange(
            new StokMaliyetKatmanTuketimi { Id = 1, CikisStokHareketId = cikisStokHareketId, StokMaliyetKatmaniId = 1, Miktar = 3, BirimMaliyet = 12m, Tutar = 36m },
            new StokMaliyetKatmanTuketimi { Id = 2, CikisStokHareketId = cikisStokHareketId, StokMaliyetKatmaniId = 2, Miktar = 2, BirimMaliyet = 10m, Tutar = 20m });
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Iade_FifoPartialRestore_MixedLayer_IkiBirim()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var satis = await CreateKesinlesmis10UrunSatisAsync(dbContext, satisService);
        var originalMovement = await dbContext.StokHareketleri.SingleAsync(x => x.KaynakModul == "KantinSatisSatir");
        await SeedKarimliFifoTuketimiAsync(dbContext, originalMovement.Id);

        var iadeService = CreateIadeService(dbContext, stokMaliyetKatmaniRestoreService: new StokMaliyetKatmaniRestoreService(dbContext));
        await iadeService.KesinlestirAsync((await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satis.Satirlar.Single().Id!.Value, 2))).Id!.Value);

        var iadeHareket = await dbContext.StokHareketleri.SingleAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisIadeSatir);
        var yeniLayer = await dbContext.StokMaliyetKatmanlari.SingleAsync(x => x.KaynakStokHareketId == iadeHareket.Id);
        Assert.Equal(2m, yeniLayer.IlkMiktar);
        Assert.Equal(10m, yeniLayer.BirimMaliyet);

        // Orijinal consumption kayıtları değişmez.
        Assert.Equal(2m, (await dbContext.StokMaliyetKatmanTuketimleri.SingleAsync(x => x.Id == 1)).Miktar);
        Assert.Equal(3m, (await dbContext.StokMaliyetKatmanTuketimleri.SingleAsync(x => x.Id == 2)).Miktar);
    }

    [Fact]
    public async Task Iade_FifoPartialRestore_MixedLayer_DortBirim()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var satis = await CreateKesinlesmis10UrunSatisAsync(dbContext, satisService);
        var originalMovement = await dbContext.StokHareketleri.SingleAsync(x => x.KaynakModul == "KantinSatisSatir");
        await SeedKarimliFifoTuketimiAsync(dbContext, originalMovement.Id);

        var iadeService = CreateIadeService(dbContext, stokMaliyetKatmaniRestoreService: new StokMaliyetKatmaniRestoreService(dbContext));
        await iadeService.KesinlestirAsync((await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satis.Satirlar.Single().Id!.Value, 4))).Id!.Value);

        var iadeHareket = await dbContext.StokHareketleri.SingleAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisIadeSatir);
        var yeniLayerlar = await dbContext.StokMaliyetKatmanlari
            .Where(x => x.KaynakStokHareketId == iadeHareket.Id)
            .OrderBy(x => x.Id)
            .ToListAsync();

        Assert.Equal(2, yeniLayerlar.Count);
        Assert.Equal(2m, yeniLayerlar[0].IlkMiktar);
        Assert.Equal(10m, yeniLayerlar[0].BirimMaliyet);
        Assert.Equal(2m, yeniLayerlar[1].IlkMiktar);
        Assert.Equal(12m, yeniLayerlar[1].BirimMaliyet);
    }

    [Fact]
    public async Task Iade_LifoPartialRestore_MixedLayer_DortBirim()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var satis = await CreateKesinlesmis10UrunSatisAsync(dbContext, satisService);
        var originalMovement = await dbContext.StokHareketleri.SingleAsync(x => x.KaynakModul == "KantinSatisSatir");
        await SeedKarimliLifoTuketimiAsync(dbContext, originalMovement.Id);

        var iadeService = CreateIadeService(dbContext, stokMaliyetKatmaniRestoreService: new StokMaliyetKatmaniRestoreService(dbContext));
        await iadeService.KesinlestirAsync((await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satis.Satirlar.Single().Id!.Value, 4))).Id!.Value);

        var iadeHareket = await dbContext.StokHareketleri.SingleAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisIadeSatir);
        var yeniLayerlar = await dbContext.StokMaliyetKatmanlari
            .Where(x => x.KaynakStokHareketId == iadeHareket.Id)
            .OrderBy(x => x.Id)
            .ToListAsync();

        // LIFO orijinal tüketim sırasını korur: önce 3 @ 12, sonra 1 @ 10.
        Assert.Equal(2, yeniLayerlar.Count);
        Assert.Equal(3m, yeniLayerlar[0].IlkMiktar);
        Assert.Equal(12m, yeniLayerlar[0].BirimMaliyet);
        Assert.Equal(1m, yeniLayerlar[1].IlkMiktar);
        Assert.Equal(10m, yeniLayerlar[1].BirimMaliyet);
    }

    [Fact]
    public async Task Iade_FifoPartialRestore_ArdisikIkiIade_OffsetDogru()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var satis = await CreateKesinlesmis10UrunSatisAsync(dbContext, satisService);
        var originalMovement = await dbContext.StokHareketleri.SingleAsync(x => x.KaynakModul == "KantinSatisSatir");
        await SeedKarimliFifoTuketimiAsync(dbContext, originalMovement.Id);

        var iadeService = CreateIadeService(dbContext, stokMaliyetKatmaniRestoreService: new StokMaliyetKatmaniRestoreService(dbContext));
        var satirId = satis.Satirlar.Single().Id!.Value;

        // İade #1 = 2 → 2 @ 10
        var iade1 = await iadeService.KesinlestirAsync((await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satirId, 2))).Id!.Value);
        var iade1Satir = await dbContext.KantinSatisIadeSatirlari.SingleAsync(x => x.KantinSatisIadeId == iade1.Id);
        var iade1Hareket = await dbContext.StokHareketleri.SingleAsync(x => x.Id == iade1Satir.StokHareketId);
        var iade1Layer = await dbContext.StokMaliyetKatmanlari.SingleAsync(x => x.KaynakStokHareketId == iade1Hareket.Id);
        Assert.Equal(2m, iade1Layer.IlkMiktar);
        Assert.Equal(10m, iade1Layer.BirimMaliyet);
        Assert.Equal(10m, iade1Hareket.MaliyetBirimFiyat);
        Assert.Equal(20m, iade1Hareket.MaliyetTutari);

        // İade #2 = 2 → 2 @ 12 (önceki 2 @ 10 skip edilir, baştan başlamaz).
        var iade2 = await iadeService.KesinlestirAsync((await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satirId, 2))).Id!.Value);
        var iade2Satir = await dbContext.KantinSatisIadeSatirlari.SingleAsync(x => x.KantinSatisIadeId == iade2.Id);
        var iade2Hareket = await dbContext.StokHareketleri.SingleAsync(x => x.Id == iade2Satir.StokHareketId);
        var iade2Layer = await dbContext.StokMaliyetKatmanlari.SingleAsync(x => x.KaynakStokHareketId == iade2Hareket.Id);
        Assert.Equal(2m, iade2Layer.IlkMiktar);
        Assert.Equal(12m, iade2Layer.BirimMaliyet);
        Assert.Equal(12m, iade2Hareket.MaliyetBirimFiyat);
        Assert.Equal(24m, iade2Hareket.MaliyetTutari);
    }

    [Fact]
    public async Task Iade_LifoPartialRestore_ArdisikIkiIade_OffsetDogru()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var satis = await CreateKesinlesmis10UrunSatisAsync(dbContext, satisService);
        var originalMovement = await dbContext.StokHareketleri.SingleAsync(x => x.KaynakModul == "KantinSatisSatir");
        await SeedKarimliLifoTuketimiAsync(dbContext, originalMovement.Id);

        var iadeService = CreateIadeService(dbContext, stokMaliyetKatmaniRestoreService: new StokMaliyetKatmaniRestoreService(dbContext));
        var satirId = satis.Satirlar.Single().Id!.Value;

        // İade #1 = 2 → 2 @ 12 (LIFO: ilk tüketim kaydı 3 @ 12).
        var iade1 = await iadeService.KesinlestirAsync((await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satirId, 2))).Id!.Value);
        var iade1Satir = await dbContext.KantinSatisIadeSatirlari.SingleAsync(x => x.KantinSatisIadeId == iade1.Id);
        var iade1Hareket = await dbContext.StokHareketleri.SingleAsync(x => x.Id == iade1Satir.StokHareketId);
        var iade1Layer = await dbContext.StokMaliyetKatmanlari.SingleAsync(x => x.KaynakStokHareketId == iade1Hareket.Id);
        Assert.Equal(2m, iade1Layer.IlkMiktar);
        Assert.Equal(12m, iade1Layer.BirimMaliyet);

        // İade #2 = 2 → 1 @ 12 + 1 @ 10 (önceki 2 @ 12 skip edilir).
        var iade2 = await iadeService.KesinlestirAsync((await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satirId, 2))).Id!.Value);
        var iade2Satir = await dbContext.KantinSatisIadeSatirlari.SingleAsync(x => x.KantinSatisIadeId == iade2.Id);
        var iade2Hareket = await dbContext.StokHareketleri.SingleAsync(x => x.Id == iade2Satir.StokHareketId);
        var iade2Layerlar = await dbContext.StokMaliyetKatmanlari
            .Where(x => x.KaynakStokHareketId == iade2Hareket.Id)
            .OrderBy(x => x.Id)
            .ToListAsync();
        Assert.Equal(2, iade2Layerlar.Count);
        Assert.Equal(1m, iade2Layerlar[0].IlkMiktar);
        Assert.Equal(12m, iade2Layerlar[0].BirimMaliyet);
        Assert.Equal(1m, iade2Layerlar[1].IlkMiktar);
        Assert.Equal(10m, iade2Layerlar[1].BirimMaliyet);
    }

    [Fact]
    public async Task Iade_MovementMaliyetToplami_RestoredLayerToplami_Esit()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var satis = await CreateKesinlesmis10UrunSatisAsync(dbContext, satisService);
        var originalMovement = await dbContext.StokHareketleri.SingleAsync(x => x.KaynakModul == "KantinSatisSatir");
        await SeedKarimliFifoTuketimiAsync(dbContext, originalMovement.Id);

        var iadeService = CreateIadeService(dbContext, stokMaliyetKatmaniRestoreService: new StokMaliyetKatmaniRestoreService(dbContext));
        var satirId = satis.Satirlar.Single().Id!.Value;

        // Mixed-layer 4 adet: 2 @ 10 + 2 @ 12 → toplam 44, efektif birim 11.
        await iadeService.KesinlestirAsync((await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satirId, 4))).Id!.Value);

        var iadeHareket = await dbContext.StokHareketleri.SingleAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisIadeSatir);
        var layerlar = await dbContext.StokMaliyetKatmanlari
            .Where(x => x.KaynakStokHareketId == iadeHareket.Id)
            .ToListAsync();

        var layerToplamMaliyet = layerlar.Sum(x => Math.Round(x.IlkMiktar * x.BirimMaliyet, 2, MidpointRounding.AwayFromZero));

        // StokHareket.MaliyetTutari ile restored layer toplam maliyeti birebir eşleşir.
        Assert.Equal(44m, iadeHareket.MaliyetTutari);
        Assert.Equal(44m, layerToplamMaliyet);
        Assert.Equal(11m, iadeHareket.MaliyetBirimFiyat);
    }

    [Fact]
    public async Task Iade_IkinciFinalizeDuplicateUretmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var iadeService = CreateIadeService(dbContext);
        var satis = await CreateKesinlesmis10UrunSatisAsync(dbContext, satisService);

        var iade = await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satis.Satirlar.Single().Id!.Value, 3));
        await iadeService.KesinlestirAsync(iade.Id!.Value);

        var hareketSayisi = await dbContext.StokHareketleri.CountAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisIadeSatir);
        var ikinci = await iadeService.KesinlestirAsync(iade.Id!.Value);

        Assert.Equal(KantinSatisIadeDurumlari.Kesinlesti, ikinci.Durum);
        Assert.Equal(hareketSayisi, await dbContext.StokHareketleri.CountAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisIadeSatir));
    }

    [Fact]
    public async Task Iade_Concurrency_CiftKesinlestirmeCumulativeReddeder()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        var satisService = CreateService(dbContext);
        var iadeService = CreateIadeService(dbContext);
        var satis = await CreateKesinlesmis10UrunSatisAsync(dbContext, satisService);
        var satirId = satis.Satirlar.Single().Id!.Value;

        // İki Taslak iade aynı anda (her biri 6).
        var iadeA = await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satirId, 6));
        var iadeB = await iadeService.CreateAsync(IadeRequest(satis.Id!.Value, satirId, 6));

        await iadeService.KesinlestirAsync(iadeA.Id!.Value);

        // İkinci kesinleştirme cumulative kontrolünde reddedilir (6 + 6 > 10).
        var ex = await Assert.ThrowsAsync<BaseException>(() => iadeService.KesinlestirAsync(iadeB.Id!.Value));
        Assert.Equal("Kümülatif iade miktarı satış miktarını aşamaz.", ex.Message);
    }

    [Fact]
    public async Task MuhasebeFisKantinOwned_GenelUpdateDeleteIptalReddedilir()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        dbContext.MuhasebeFisler.Add(new MuhasebeFis
        {
            Id = 700,
            TesisId = 1,
            MaliYil = 2026,
            Donem = 8,
            FisNo = "2026-KNT-000001",
            FisTarihi = new DateTime(2026, 8, 24),
            FisTipi = MuhasebeFisTipleri.Mahsup,
            KaynakModul = MuhasebeKaynakModulleri.KantinSatis,
            KaynakId = 1,
            Durum = MuhasebeFisDurumlari.Taslak,
            ToplamBorc = 0,
            ToplamAlacak = 0
        });
        await dbContext.SaveChangesAsync();

        var service = CreateRealMuhasebeFisService(dbContext);

        var updateEx = await Assert.ThrowsAsync<BaseException>(() => service.UpdateAsync(new MuhasebeFisDto { Id = 700 }));
        Assert.Equal("Kantin satış fişi satıştan bağımsız olarak güncellenemez. Satış iptal akışını kullanınız.", updateEx.Message);

        var deleteEx = await Assert.ThrowsAsync<BaseException>(() => service.DeleteAsync(700));
        Assert.Equal("Kantin satış fişi satıştan bağımsız olarak silinemez. Satış iptal akışını kullanınız.", deleteEx.Message);

        var iptalEx = await Assert.ThrowsAsync<BaseException>(() => service.IptalEtAsync(700, "deneme"));
        Assert.Contains("genel fiş iptali ile iptal edilemez", iptalEx.Message);
    }

    [Fact]
    public async Task MuhasebeFisKantinOwned_OnaylaEngellenmez()
    {
        await using var dbContext = CreateDbContext();
        await SeedBaseAsync(dbContext);
        dbContext.MuhasebeFisler.Add(new MuhasebeFis
        {
            Id = 701,
            TesisId = 1,
            MaliYil = 2026,
            Donem = 8,
            FisNo = "2026-KNT-000002",
            FisTarihi = new DateTime(2026, 8, 24),
            FisTipi = MuhasebeFisTipleri.Mahsup,
            KaynakModul = MuhasebeKaynakModulleri.KantinSatis,
            KaynakId = 1,
            Durum = MuhasebeFisDurumlari.Taslak,
            ToplamBorc = 0,
            ToplamAlacak = 0
        });
        await dbContext.SaveChangesAsync();

        var service = CreateRealMuhasebeFisService(dbContext);

        // OnaylaAsync Kantin source guard'ı içermez; Kantin Taslak fişi genel muhasebe ekranından
        // onaylanabilir olmalıdır. Burada farklı bir doğrulama hatası (ör. satır eksik) alınır ama
        // "Kantin satış fişi" guard mesajı ASLA gelmemelidir.
        var ex = await Record.ExceptionAsync(() => service.OnaylaAsync(701));
        Assert.False(ex is BaseException baseException && baseException.Message.Contains("Kantin satış fişi"));
    }

    private static async Task<KantinSatisDto> CreateDraftWithSingleLineAsync(KantinSatisService service)
    {
        var satis = await service.AddAsync(new KantinSatisDto
        {
            KantinId = 1,
            SatisNoktasiId = 1,
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
            new KasaBankaHesap { Id = 100, TesisId = 1, Tip = KasaBankaHesapTipleri.NakitKasa, Kod = "KASA-A", Ad = "Nakit Kasa", AktifMi = true, MuhasebeHesapPlaniId = 1000 },
            new KasaBankaHesap { Id = 101, TesisId = 1, Tip = KasaBankaHesapTipleri.Banka, Kod = "BANKA-A", Ad = "Banka", AktifMi = true, MuhasebeHesapPlaniId = 1002 },
            new KasaBankaHesap { Id = 102, TesisId = 1, Tip = KasaBankaHesapTipleri.KrediKarti, Kod = "POS-A", Ad = "POS", AktifMi = true, MuhasebeHesapPlaniId = 1001, ValorGunSayisi = 1, ValorGunTuru = "Gun", ValorGunundeOtomatikHesabaAktarMi = false },
            new KasaBankaHesap { Id = 300, TesisId = 2, Tip = KasaBankaHesapTipleri.KrediKarti, Kod = "POS-B", Ad = "POS B", AktifMi = true });

        dbContext.CariKartlar.AddRange(
            new CariKart { Id = 100, TesisId = 1, CariTipi = CariKartTipleri.Musteri, CariKodu = "PRK-A", UnvanAdSoyad = "Perakende Müşteri A", AktifMi = true },
            new CariKart { Id = 101, TesisId = 1, CariTipi = CariKartTipleri.Musteri, CariKodu = "PRK-A2", UnvanAdSoyad = "Perakende Müşteri A2", AktifMi = true },
            new CariKart { Id = 300, TesisId = 2, CariTipi = CariKartTipleri.Musteri, CariKodu = "PRK-B", UnvanAdSoyad = "Perakende Müşteri B", AktifMi = true });

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
                TakipTipi = TasinirKartTakipTipleri.Yok,
                MuhasebeHesapPlaniId = 1100
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
                TakipTipi = TasinirKartTakipTipleri.Lot,
                MuhasebeHesapPlaniId = 1101
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
                TakipTipi = TasinirKartTakipTipleri.Seri,
                MuhasebeHesapPlaniId = 1100
            });

        dbContext.MuhasebeHesapPlanlari.AddRange(
            new MuhasebeHesapPlani { Id = 1000, TesisId = 1, Kod = "100.01", TamKod = "1.10.100.TEST-NAKIT", AnaHesapKodu = MuhasebeAnaHesapKodlari.FinansalKasa, Ad = "Nakit Kasa Hesabı", AktifMi = true, DetayHesapMi = true, HareketGorebilirMi = true, HesapTipi = HesapTipi.DetayHesap },
            new MuhasebeHesapPlani { Id = 1001, TesisId = 1, Kod = "109.01", TamKod = "1.10.109.TEST-POS", AnaHesapKodu = MuhasebeAnaHesapKodlari.FinansalKrediKarti, Ad = "POS Hesabı", AktifMi = true, DetayHesapMi = true, HareketGorebilirMi = true, HesapTipi = HesapTipi.DetayHesap },
            new MuhasebeHesapPlani { Id = 1002, TesisId = 1, Kod = "102.01", TamKod = "1.10.102.TEST-BANKA", AnaHesapKodu = MuhasebeAnaHesapKodlari.FinansalBanka, Ad = "Banka Hesabı", AktifMi = true, DetayHesapMi = true, HareketGorebilirMi = true, HesapTipi = HesapTipi.DetayHesap },
            new MuhasebeHesapPlani { Id = 1100, TesisId = 1, Kod = "153.01", TamKod = "1.53.153.TEST-STOK-A", AnaHesapKodu = MuhasebeAnaHesapKodlari.StokTicariMal, Ad = "Stok Hesabı A", AktifMi = true, DetayHesapMi = true, HareketGorebilirMi = true, HesapTipi = HesapTipi.DetayHesap },
            new MuhasebeHesapPlani { Id = 1101, TesisId = 1, Kod = "153.02", TamKod = "1.53.153.TEST-STOK-B", AnaHesapKodu = MuhasebeAnaHesapKodlari.StokTicariMal, Ad = "Stok Hesabı B", AktifMi = true, DetayHesapMi = true, HareketGorebilirMi = true, HesapTipi = HesapTipi.DetayHesap },
            new MuhasebeHesapPlani { Id = 1200, TesisId = 1, Kod = "600.01", TamKod = "6.60.600.TEST-GELIR", AnaHesapKodu = MuhasebeAnaHesapKodlari.GelirSatis, Ad = "Satış Gelir Hesabı", AktifMi = true, DetayHesapMi = true, HareketGorebilirMi = true, HesapTipi = HesapTipi.DetayHesap },
            new MuhasebeHesapPlani { Id = 1308, TesisId = 1, Kod = "391.08", TamKod = "3.39.391.TEST-KDV8", AnaHesapKodu = MuhasebeAnaHesapKodlari.KDVHesaplanan, Ad = "%8 Hesaplanan KDV", AktifMi = true, DetayHesapMi = true, HareketGorebilirMi = true, HesapTipi = HesapTipi.DetayHesap },
            new MuhasebeHesapPlani { Id = 1310, TesisId = 1, Kod = "391.10", TamKod = "3.39.391.TEST-KDV10", AnaHesapKodu = MuhasebeAnaHesapKodlari.KDVHesaplanan, Ad = "%10 Hesaplanan KDV", AktifMi = true, DetayHesapMi = true, HareketGorebilirMi = true, HesapTipi = HesapTipi.DetayHesap },
            new MuhasebeHesapPlani { Id = 1320, TesisId = 1, Kod = "391.20", TamKod = "3.39.391.TEST-KDV20", AnaHesapKodu = MuhasebeAnaHesapKodlari.KDVHesaplanan, Ad = "%20 Hesaplanan KDV", AktifMi = true, DetayHesapMi = true, HareketGorebilirMi = true, HesapTipi = HesapTipi.DetayHesap },
            new MuhasebeHesapPlani { Id = 1400, TesisId = 1, Kod = "621.01", TamKod = "6.62.621.TEST-SMM", AnaHesapKodu = MuhasebeAnaHesapKodlari.SatilanTicariMallarMaliyeti, Ad = "Satılan Mallar Maliyeti", AktifMi = true, DetayHesapMi = true, HareketGorebilirMi = true, HesapTipi = HesapTipi.DetayHesap });

        dbContext.MuhasebeVergiHesapEslemeleri.AddRange(
            new MuhasebeVergiHesapEsleme { Id = 1, TesisId = 1, VergiTipi = "KDV", Oran = 8, SatisKdvHesapId = 1308, AlisKdvHesapId = 1308, AktifMi = true },
            new MuhasebeVergiHesapEsleme { Id = 2, TesisId = 1, VergiTipi = "KDV", Oran = 10, SatisKdvHesapId = 1310, AlisKdvHesapId = 1310, AktifMi = true },
            new MuhasebeVergiHesapEsleme { Id = 3, TesisId = 1, VergiTipi = "KDV", Oran = 20, SatisKdvHesapId = 1320, AlisKdvHesapId = 1320, AktifMi = true });

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

        dbContext.Kantinler.Add(new Kantin
        {
            Id = 1,
            TesisId = 1,
            DepoId = 10,
            PerakendeCariKartId = 100,
            Kod = "KNT-01",
            Ad = "Merkez Kantin",
            AktifMi = true
        });

        dbContext.KantinSatisNoktalari.Add(new KantinSatisNoktasi
        {
            Id = 1,
            KantinId = 1,
            Kod = "ANA",
            Ad = "Ana Satış Noktası",
            VarsayilanNakitKasaId = 100,
            VarsayilanPosHesapId = 102,
            VarsayilanMi = true,
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

    private static KantinSatisService CreateService(
        StysAppDbContext dbContext,
        DomainAccessScope? scope = null,
        ITahsilatOdemeBelgesiService? tahsilatService = null,
        IMuhasebeFisService? muhasebeFisService = null,
        IStokMaliyetKatmaniRestoreService? stokMaliyetKatmaniRestoreService = null)
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
            tahsilatService ?? new FakeTahsilatOdemeBelgesiService(dbContext),
            muhasebeFisService ?? new FakeMuhasebeFisService(dbContext),
            stokMaliyetKatmaniRestoreService ?? new FakeStokMaliyetKatmaniRestoreService(),
            new FakeCurrentUserAccessor(),
            mapper);
    }

    private static KantinSatisIadeService CreateIadeService(
        StysAppDbContext dbContext,
        IStokMaliyetKatmaniRestoreService? stokMaliyetKatmaniRestoreService = null)
        => new KantinSatisIadeService(
            dbContext,
            new FakeUserAccessScopeService(DomainAccessScope.Unscoped()),
            new FakeCurrentUserAccessor(),
            new FakeStokHareketService(dbContext),
            stokMaliyetKatmaniRestoreService ?? new FakeStokMaliyetKatmaniRestoreService());

    private static IKantinSatisMuhasebeFisService CreateMuhasebeFisService(StysAppDbContext dbContext)
        => new KantinSatisMuhasebeFisService(
            dbContext,
            CreateService(dbContext),
            new FakeMuhasebeDonemService(),
            new FakeTasinirKodMuhasebeHesapEslemeService(dbContext),
            NullLogger<KantinSatisMuhasebeFisService>.Instance);

    private static IMuhasebeFisService CreateRealMuhasebeFisService(StysAppDbContext dbContext)
    {
        var mapperConfig = new MapperConfiguration(cfg =>
        {
            cfg.AddMaps(typeof(KantinProfile).Assembly);
            cfg.AddMaps(typeof(KantinSatisProfile).Assembly);
        }, NullLoggerFactory.Instance);
        var mapper = mapperConfig.CreateMapper();

        return new MuhasebeFisService(
            new MuhasebeFisRepository(dbContext, mapper),
            mapper,
            dbContext,
            new FakeMuhasebeDonemService(),
            new MuhasebeHesapBakiyeGuncellemeService(dbContext),
            new FakeUserAccessScopeService(DomainAccessScope.Unscoped()),
            new NoOpDomainOperationLogger());
    }

    private sealed class FakeStokHareketService(StysAppDbContext dbContext) : IStokHareketService
    {
        private async Task<int> NextStokHareketIdAsync(CancellationToken cancellationToken)
            => await dbContext.StokHareketleri.AnyAsync(cancellationToken)
                ? await dbContext.StokHareketleri.MaxAsync(x => x.Id, cancellationToken) + 1
                : 1000;

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

            var maliyetBirimFiyat = dto.MaliyetBirimFiyat;
            var maliyetTutari = dto.MaliyetTutari;
            if (StokHareketTipleri.IsCikisEtkisi(dto.HareketTipi, dto.TransferYonu, dto.SayimFarkiYonu))
            {
                var giris = await dbContext.StokHareketleri
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted
                        && x.Durum == StokHareketDurumlari.Aktif
                        && x.DepoId == dto.DepoId
                        && x.TasinirKartId == dto.TasinirKartId
                        && x.StokLotId == dto.StokLotId
                        && x.StokSeriId == dto.StokSeriId
                        && StokHareketTipleri.IsGirisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu))
                    .OrderByDescending(x => x.HareketTarihi)
                    .ThenByDescending(x => x.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (giris is not null && giris.Miktar > 0)
                {
                    maliyetBirimFiyat ??= giris.BirimFiyat;
                    maliyetTutari ??= ParaTutarYuvarlamaHelper.Yuvarla(dto.Miktar * giris.BirimFiyat);
                }
            }

            var entity = new StokHareket
            {
                Id = await NextStokHareketIdAsync(cancellationToken),
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
                MaliyetBirimFiyat = maliyetBirimFiyat,
                MaliyetTutari = maliyetTutari
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

    private sealed class NoOpDomainOperationLogger : IDomainOperationLogger
    {
        public void Started(string eventName, object payload) { }
        public void Completed(string eventName, object payload) { }
        public void Warning(string eventName, object payload) { }
        public void Failed(string eventName, Exception exception, object payload) { }
    }

    private sealed class FakeMuhasebeDonemService : IMuhasebeDonemService
    {
        public Task<MuhasebeDonemDto?> GetAktifDonemAsync(int tesisId, DateTime tarih, CancellationToken cancellationToken = default)
            => Task.FromResult<MuhasebeDonemDto?>(new MuhasebeDonemDto
            {
                Id = 1,
                TesisId = tesisId,
                MaliYil = 2026,
                DonemNo = 8,
                BaslangicTarihi = new DateTime(2026, 8, 1),
                BitisTarihi = new DateTime(2026, 8, 31),
                KapaliMi = false
            });

        public Task<MuhasebeDonemDto?> GetDonemByTarihAsync(int tesisId, DateTime tarih, CancellationToken cancellationToken = default) => GetAktifDonemAsync(tesisId, tarih, cancellationToken);
        public Task DonemKapatAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DonemAcAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<MuhasebeDonemDto>> GetAllAsync(Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null) => throw new NotImplementedException();
        public Task<MuhasebeDonemDto?> GetByIdAsync(int id, Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null) => throw new NotImplementedException();
        public Task<PagedResult<MuhasebeDonemDto>> GetPagedAsync(PagedRequest request, System.Linq.Expressions.Expression<Func<MuhasebeDonem, bool>>? predicate = null, Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null, Func<IQueryable<MuhasebeDonem>, IOrderedQueryable<MuhasebeDonem>>? orderBy = null) => throw new NotImplementedException();
        public Task<MuhasebeDonemDto> AddAsync(MuhasebeDonemDto dto) => throw new NotImplementedException();
        public Task<MuhasebeDonemDto> UpdateAsync(MuhasebeDonemDto dto) => throw new NotImplementedException();
        public Task DeleteAsync(int id) => throw new NotImplementedException();
        public Task<IEnumerable<MuhasebeDonemDto>> WhereAsync(System.Linq.Expressions.Expression<Func<MuhasebeDonem, bool>> predicate, Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null) => throw new NotImplementedException();
        public Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<MuhasebeDonem, bool>> predicate, Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null) => throw new NotImplementedException();
    }

    private sealed class FakeTasinirKodMuhasebeHesapEslemeService(StysAppDbContext dbContext) : ITasinirKodMuhasebeHesapEslemeService
    {
        public Task<TasinirKodMuhasebeHesapEslemeDto?> GetVarsayilanAsync(int tasinirKodId, string malzemeTipi, string hareketTipi, CancellationToken cancellationToken = default)
            => dbContext.TasinirKodMuhasebeHesapEslemeleri
                .AsNoTracking()
                .Where(x => !x.IsDeleted
                    && x.TasinirKodId == tasinirKodId
                    && x.MalzemeTipi == malzemeTipi
                    && x.HareketTipi == hareketTipi
                    && x.VarsayilanMi
                    && x.AktifMi)
                .Select(x => new TasinirKodMuhasebeHesapEslemeDto
                {
                    Id = x.Id,
                    TasinirKodId = x.TasinirKodId,
                    MuhasebeHesapPlaniId = x.MuhasebeHesapPlaniId,
                    MalzemeTipi = x.MalzemeTipi,
                    HareketTipi = x.HareketTipi,
                    AktifMi = x.AktifMi,
                    VarsayilanMi = x.VarsayilanMi
                })
                .FirstOrDefaultAsync(cancellationToken);

        public Task<List<TasinirKodMuhasebeHesapEslemeDto>> GetByTasinirKodIdAsync(int tasinirKodId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IEnumerable<TasinirKodMuhasebeHesapEslemeDto>> GetAllAsync(Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null) => throw new NotImplementedException();
        public Task<TasinirKodMuhasebeHesapEslemeDto?> GetByIdAsync(int id, Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null) => throw new NotImplementedException();
        public Task<PagedResult<TasinirKodMuhasebeHesapEslemeDto>> GetPagedAsync(PagedRequest request, System.Linq.Expressions.Expression<Func<TasinirKodMuhasebeHesapEsleme, bool>>? predicate = null, Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null, Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IOrderedQueryable<TasinirKodMuhasebeHesapEsleme>>? orderBy = null) => throw new NotImplementedException();
        public Task<TasinirKodMuhasebeHesapEslemeDto> AddAsync(TasinirKodMuhasebeHesapEslemeDto dto) => throw new NotImplementedException();
        public Task<TasinirKodMuhasebeHesapEslemeDto> UpdateAsync(TasinirKodMuhasebeHesapEslemeDto dto) => throw new NotImplementedException();
        public Task DeleteAsync(int id) => throw new NotImplementedException();
        public Task<IEnumerable<TasinirKodMuhasebeHesapEslemeDto>> WhereAsync(System.Linq.Expressions.Expression<Func<TasinirKodMuhasebeHesapEsleme, bool>> predicate, Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null) => throw new NotImplementedException();
        public Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<TasinirKodMuhasebeHesapEsleme, bool>> predicate, Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null) => throw new NotImplementedException();
    }

    private sealed class FakeTahsilatOdemeBelgesiService(StysAppDbContext dbContext, bool iptaldaHataFirlat = false) : ITahsilatOdemeBelgesiService
    {
        private int _nextId = 2000;
        private int _nextValorId = 4000;

        public Task<TahsilatOdemeOzetDto> GetGunlukOzetAsync(DateTime gun, int? tesisId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task IptalEtAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task IptalGeriAlAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ValidateOlusturmaAsync(int cariKartId, string belgeTipi, string odemeYontemi, string durum, DateTime belgeTarihi, int? kapatilacakCariHareketId, bool requireCariMuhasebeHesabi, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public async Task IptalEtManagedSourceWithinCurrentTransactionAsync(int id, string expectedKaynakModul, int expectedKaynakId, CancellationToken cancellationToken = default)
        {
            if (iptaldaHataFirlat)
            {
                throw new BaseException("Tahsilat iptal edilemedi.", 400);
            }

            var belge = await dbContext.TahsilatOdemeBelgeleri.SingleAsync(x => x.Id == id, cancellationToken);
            if (!string.Equals(belge.KaynakModul, expectedKaynakModul, StringComparison.Ordinal) || belge.KaynakId != expectedKaynakId)
            {
                throw new BaseException("Tahsilat belgesi beklenen kaynak ile eşleşmiyor.", 400);
            }

            if (belge.Durum == TahsilatOdemeBelgeDurumlari.Iptal)
            {
                return;
            }

            belge.Durum = TahsilatOdemeBelgeDurumlari.Iptal;

            var valor = await dbContext.PosTahsilatValorleri
                .FirstOrDefaultAsync(x => x.TahsilatOdemeBelgesiId == id && !x.IsDeleted, cancellationToken);
            if (valor is not null)
            {
                valor.Durum = PosTahsilatValorDurumlari.Iptal;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        public Task<IEnumerable<TahsilatOdemeBelgesiDto>> GetAllAsync(Func<IQueryable<TahsilatOdemeBelgesi>, IQueryable<TahsilatOdemeBelgesi>>? include = null) => throw new NotImplementedException();
        public Task<TahsilatOdemeBelgesiDto?> GetByIdAsync(int id, Func<IQueryable<TahsilatOdemeBelgesi>, IQueryable<TahsilatOdemeBelgesi>>? include = null) => throw new NotImplementedException();
        public Task<PagedResult<TahsilatOdemeBelgesiDto>> GetPagedAsync(PagedRequest request, System.Linq.Expressions.Expression<Func<TahsilatOdemeBelgesi, bool>>? predicate = null, Func<IQueryable<TahsilatOdemeBelgesi>, IQueryable<TahsilatOdemeBelgesi>>? include = null, Func<IQueryable<TahsilatOdemeBelgesi>, IOrderedQueryable<TahsilatOdemeBelgesi>>? orderBy = null) => throw new NotImplementedException();
        public Task<TahsilatOdemeBelgesiDto> AddAsync(TahsilatOdemeBelgesiDto dto) => throw new NotImplementedException();
        public Task<TahsilatOdemeBelgesiDto> UpdateAsync(TahsilatOdemeBelgesiDto dto) => throw new NotImplementedException();
        public Task DeleteAsync(int id) => throw new NotImplementedException();
        public Task<IEnumerable<TahsilatOdemeBelgesiDto>> WhereAsync(System.Linq.Expressions.Expression<Func<TahsilatOdemeBelgesi, bool>> predicate, Func<IQueryable<TahsilatOdemeBelgesi>, IQueryable<TahsilatOdemeBelgesi>>? include = null) => throw new NotImplementedException();
        public Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<TahsilatOdemeBelgesi, bool>> predicate, Func<IQueryable<TahsilatOdemeBelgesi>, IQueryable<TahsilatOdemeBelgesi>>? include = null) => throw new NotImplementedException();

        public async Task<TahsilatOdemeBelgesiDto> AddWithinCurrentTransactionAsync(TahsilatOdemeBelgesiDto dto, bool requireCariMuhasebeHesabi, CancellationToken cancellationToken = default)
        {
            var entity = new TahsilatOdemeBelgesi
            {
                Id = _nextId++,
                BelgeNo = dto.BelgeNo,
                BelgeTarihi = dto.BelgeTarihi,
                BelgeTipi = dto.BelgeTipi,
                CariKartId = dto.CariKartId,
                Tutar = dto.Tutar,
                ParaBirimi = dto.ParaBirimi,
                OdemeYontemi = dto.OdemeYontemi,
                Aciklama = dto.Aciklama,
                KaynakModul = dto.KaynakModul,
                KaynakId = dto.KaynakId,
                KapatilacakCariHareketId = dto.KapatilacakCariHareketId,
                Durum = dto.Durum,
                KasaBankaHesapId = dto.KasaBankaHesapId
            };

            dbContext.TahsilatOdemeBelgeleri.Add(entity);

            if (dto.OdemeYontemi == OdemeYontemleri.KrediKarti && dto.KasaBankaHesapId.HasValue)
            {
                dbContext.PosTahsilatValorleri.Add(new PosTahsilatValor
                {
                    Id = _nextValorId++,
                    TesisId = 1,
                    TahsilatOdemeBelgesiId = entity.Id,
                    KrediKartiHesapId = dto.KasaBankaHesapId.Value,
                    OdemeTarihi = dto.BelgeTarihi,
                    ValorGunSayisi = 1,
                    ValorGunTuru = "Gun",
                    BeklenenValorTarihi = DateOnly.FromDateTime(dto.BelgeTarihi.AddDays(1)),
                    OtomatikAktarimMi = false,
                    BrutTutar = dto.Tutar,
                    KomisyonTutari = 0,
                    NetTutar = dto.Tutar,
                    ParaBirimi = dto.ParaBirimi,
                    Durum = PosTahsilatValorDurumlari.ValorBekliyor
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            dto.Id = entity.Id;
            return dto;
        }
    }

    private sealed class FakeStokMaliyetKatmaniRestoreService : IStokMaliyetKatmaniRestoreService
    {
        public Task RestoreLayeredCostIfNeededAsync(StokHareket originalMovement, StokHareketDto reversalMovement, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<StokMaliyetRestorePlan?> PlanPartialRestoreAsync(int originalMovementId, decimal alreadyRestoredQuantity, decimal returnQuantity, CancellationToken cancellationToken = default)
            => Task.FromResult<StokMaliyetRestorePlan?>(null);

        public Task RestorePlannedLayersAsync(StokMaliyetRestorePlan plan, StokHareketDto iadeMovement, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeMuhasebeFisService(StysAppDbContext dbContext) : IMuhasebeFisService
    {
        private int _nextFisId = 5000;

        public async Task<MuhasebeFisIptalSonucDto> KantinSatisFisiIptalEtAsync(int muhasebeFisId, int beklenenKaynakId, int beklenenTesisId, string aciklama, CancellationToken cancellationToken = default)
        {
            var fis = await dbContext.MuhasebeFisler
                .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
                .SingleAsync(x => x.Id == muhasebeFisId, cancellationToken);

            if (fis.KaynakModul != MuhasebeKaynakModulleri.KantinSatis || fis.KaynakId != beklenenKaynakId || fis.TesisId != beklenenTesisId)
            {
                throw new BaseException("Fiş bilgileri beklenenle eşleşmiyor, iptal reddedildi.", 400);
            }

            if (fis.Durum == MuhasebeFisDurumlari.Iptal)
            {
                var mevcut = await dbContext.MuhasebeFisler
                    .SingleAsync(x => x.IptalEdilenFisId == fis.Id && x.Durum == MuhasebeFisDurumlari.TersKayit, cancellationToken);
                return new MuhasebeFisIptalSonucDto { OrijinalFisId = fis.Id, TersKayitFisId = mevcut.Id, ZatenTersKayitliMi = true };
            }

            if (fis.Durum != MuhasebeFisDurumlari.Onayli)
            {
                throw new BaseException($"Fiş beklenmeyen bir durumda ({fis.Durum}).", 400);
            }

            var aktifSatirlar = fis.Satirlar.Where(s => !s.IsDeleted).ToList();
            var tersFis = new MuhasebeFis
            {
                Id = _nextFisId++,
                TesisId = fis.TesisId,
                MaliYil = fis.MaliYil,
                Donem = fis.Donem,
                FisNo = "TERS-" + fis.FisNo,
                YevmiyeNo = 1,
                FisTarihi = fis.FisTarihi,
                FisTipi = MuhasebeFisTipleri.Duzeltme,
                KaynakModul = fis.KaynakModul,
                KaynakId = fis.KaynakId,
                Durum = MuhasebeFisDurumlari.TersKayit,
                IptalEdilenFisId = fis.Id,
                Aciklama = aciklama,
                ToplamBorc = fis.ToplamAlacak,
                ToplamAlacak = fis.ToplamBorc,
                Satirlar = aktifSatirlar.Select(s => new MuhasebeFisSatir
                {
                    MuhasebeHesapPlaniId = s.MuhasebeHesapPlaniId,
                    SiraNo = s.SiraNo,
                    Borc = s.Alacak,
                    Alacak = s.Borc,
                    ParaBirimi = s.ParaBirimi,
                    Kur = s.Kur
                }).ToList()
            };

            await dbContext.MuhasebeFisler.AddAsync(tersFis, cancellationToken);
            fis.Durum = MuhasebeFisDurumlari.Iptal;
            fis.TersKayitFisId = tersFis.Id;
            await dbContext.SaveChangesAsync(cancellationToken);

            return new MuhasebeFisIptalSonucDto { OrijinalFisId = fis.Id, TersKayitFisId = tersFis.Id, ZatenTersKayitliMi = false };
        }

        public async Task KantinSatisFisiniSilAsync(int muhasebeFisId, int beklenenKaynakId, int beklenenTesisId, CancellationToken cancellationToken = default)
        {
            var fis = await dbContext.MuhasebeFisler
                .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
                .SingleAsync(x => x.Id == muhasebeFisId, cancellationToken);

            if (fis.KaynakModul != MuhasebeKaynakModulleri.KantinSatis || fis.KaynakId != beklenenKaynakId || fis.TesisId != beklenenTesisId)
            {
                throw new BaseException("Fiş bilgileri beklenenle eşleşmiyor, silme reddedildi.", 400);
            }

            if (fis.Durum != MuhasebeFisDurumlari.Taslak)
            {
                throw new BaseException("Yalnızca taslak durumundaki kantin satış fişi silinebilir.", 400);
            }

            fis.IsDeleted = true;
            foreach (var satir in fis.Satirlar.Where(s => !s.IsDeleted))
            {
                satir.IsDeleted = true;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        public Task<IEnumerable<MuhasebeFisDto>> GetAllAsync(Func<IQueryable<MuhasebeFis>, IQueryable<MuhasebeFis>>? include = null) => throw new NotSupportedException();
        public Task<MuhasebeFisDto?> GetByIdAsync(int id, Func<IQueryable<MuhasebeFis>, IQueryable<MuhasebeFis>>? include = null) => throw new NotSupportedException();
        public Task<PagedResult<MuhasebeFisDto>> GetPagedAsync(PagedRequest request, System.Linq.Expressions.Expression<Func<MuhasebeFis, bool>>? predicate = null, Func<IQueryable<MuhasebeFis>, IQueryable<MuhasebeFis>>? include = null, Func<IQueryable<MuhasebeFis>, IOrderedQueryable<MuhasebeFis>>? orderBy = null) => throw new NotSupportedException();
        public Task<MuhasebeFisDto> AddAsync(MuhasebeFisDto dto) => throw new NotSupportedException();
        public Task<MuhasebeFisDto> UpdateAsync(MuhasebeFisDto dto) => throw new NotSupportedException();
        public Task DeleteAsync(int id) => throw new NotSupportedException();
        public Task<IEnumerable<MuhasebeFisDto>> WhereAsync(System.Linq.Expressions.Expression<Func<MuhasebeFis, bool>> predicate, Func<IQueryable<MuhasebeFis>, IQueryable<MuhasebeFis>>? include = null) => throw new NotSupportedException();
        public Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<MuhasebeFis, bool>> predicate, Func<IQueryable<MuhasebeFis>, IQueryable<MuhasebeFis>>? include = null) => throw new NotSupportedException();
        public Task<MuhasebeFisDto?> GetByIdWithSatirlarAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<MuhasebeFisDto>> GetByKaynakAsync(string kaynakModul, int kaynakId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MuhasebeFisDto> OnaylaAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MuhasebeFisDto> IptalEtAsync(int id, string? aciklama = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MuhasebeFisIptalSonucDto> PosValorTransferFisiniIptalEtAsync(int muhasebeFisId, int beklenenKaynakId, int beklenenTesisId, string aciklama, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MuhasebeFisIptalSonucDto> PosValorTransferFisiniGeriAlAsync(int tersKayitFisId, int beklenenKaynakId, int beklenenTesisId, string aciklama, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MuhasebeFisIptalSonucDto> SatisBelgesiFisiIptalEtAsync(int muhasebeFisId, int beklenenKaynakId, int beklenenTesisId, string aciklama, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<MuhasebeFisDto>> GetFilteredAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountFilteredAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<YevmiyeDefteriDto> GetYevmiyeDefteriAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]> ExportYevmiyeDefteriExcelAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MuavinDefterDto> GetMuavinDefterAsync(MuavinDefterFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]> ExportMuavinDefterExcelAsync(MuavinDefterFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MizanDto> GetMizanAsync(MizanFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MizanDto> GetMizanBakiyeAsync(MizanFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]> ExportMizanBakiyeExcelAsync(MizanFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MizanKarsilastirmaDto> KarsilastirMizanAsync(MizanFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TasinirMuhasebeFisiOlusturResultDto> TasinirMuhasebeFisiTaslagiOlusturAsync(TasinirMuhasebeFisiOlusturRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FailingTahsilatOdemeBelgesiService : ITahsilatOdemeBelgesiService
    {
        public Task<TahsilatOdemeOzetDto> GetGunlukOzetAsync(DateTime gun, int? tesisId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task IptalEtAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task IptalGeriAlAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task IptalEtManagedSourceWithinCurrentTransactionAsync(int id, string expectedKaynakModul, int expectedKaynakId, CancellationToken cancellationToken = default)
            => throw new BaseException("Tahsilat iptal edilemedi.", 400);
        public Task ValidateOlusturmaAsync(int cariKartId, string belgeTipi, string odemeYontemi, string durum, DateTime belgeTarihi, int? kapatilacakCariHareketId, bool requireCariMuhasebeHesabi, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<TahsilatOdemeBelgesiDto>> GetAllAsync(Func<IQueryable<TahsilatOdemeBelgesi>, IQueryable<TahsilatOdemeBelgesi>>? include = null) => throw new NotImplementedException();
        public Task<TahsilatOdemeBelgesiDto?> GetByIdAsync(int id, Func<IQueryable<TahsilatOdemeBelgesi>, IQueryable<TahsilatOdemeBelgesi>>? include = null) => throw new NotImplementedException();
        public Task<PagedResult<TahsilatOdemeBelgesiDto>> GetPagedAsync(PagedRequest request, System.Linq.Expressions.Expression<Func<TahsilatOdemeBelgesi, bool>>? predicate = null, Func<IQueryable<TahsilatOdemeBelgesi>, IQueryable<TahsilatOdemeBelgesi>>? include = null, Func<IQueryable<TahsilatOdemeBelgesi>, IOrderedQueryable<TahsilatOdemeBelgesi>>? orderBy = null) => throw new NotImplementedException();
        public Task<TahsilatOdemeBelgesiDto> AddAsync(TahsilatOdemeBelgesiDto dto) => throw new NotImplementedException();
        public Task<TahsilatOdemeBelgesiDto> UpdateAsync(TahsilatOdemeBelgesiDto dto) => throw new NotImplementedException();
        public Task DeleteAsync(int id) => throw new NotImplementedException();
        public Task<IEnumerable<TahsilatOdemeBelgesiDto>> WhereAsync(System.Linq.Expressions.Expression<Func<TahsilatOdemeBelgesi, bool>> predicate, Func<IQueryable<TahsilatOdemeBelgesi>, IQueryable<TahsilatOdemeBelgesi>>? include = null) => throw new NotImplementedException();
        public Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<TahsilatOdemeBelgesi, bool>> predicate, Func<IQueryable<TahsilatOdemeBelgesi>, IQueryable<TahsilatOdemeBelgesi>>? include = null) => throw new NotImplementedException();
        public Task<TahsilatOdemeBelgesiDto> AddWithinCurrentTransactionAsync(TahsilatOdemeBelgesiDto dto, bool requireCariMuhasebeHesabi, CancellationToken cancellationToken = default)
            => throw new BaseException("Tahsilat oluşturulamadı.", 400);
    }
}
