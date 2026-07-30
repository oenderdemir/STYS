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
using STYS.Muhasebe.SatisBelgeleri;
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
/// SatisBelgesiTutarHesaplayici (satır/belge toplam formülü), bu formülü kullanan
/// SatisBelgesiService.CreateSatirFromRequest / HesaplaBelgeToplamlari, muhasebe fişi
/// stratejilerinin (Borç/Alacak dengesi) ve SatisBelgesiMuhasebeFisService'in ÖTV/ÖİV/
/// konaklama vergisi içeren belgeler için otomatik muhasebe fişi üretimini ENGELLEDİĞİNİ
/// doğrulayan testler.
///
/// "private static" metotlar reflection ile çağrılır - DbContext/servis bağımlılığı
/// OLMADAN test edilebilir saf fonksiyonlardır. DbContext gerektiren stratejiler ve
/// SatisBelgesiMuhasebeFisService, EF Core InMemory sağlayıcısı ile GERÇEK sınıflar
/// üzerinden (fake/mock değil) çağrılarak test edilir.
/// </summary>
public class SatisBelgesiHesaplamaTests
{
    // ─────────────────────────────────────────────────────────────
    // SatisBelgesiTutarHesaplayici — saf fonksiyon testleri
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void HesaplaSatirToplami_YalnizcaKdv_MatrahArtiKdv()
    {
        var toplam = SatisBelgesiTutarHesaplayici.HesaplaSatirToplami(
            matrah: 1000m, kdvTutari: 200m, tevkifatTutari: 0m, otvTutari: 0m, oivTutari: 0m, konaklamaVergisiTutari: 0m);

        Assert.Equal(1200m, toplam);
    }

    [Fact]
    public void HesaplaSatirToplami_KdvTevkifatli_TevkifatTutariDusulur()
    {
        var toplam = SatisBelgesiTutarHesaplayici.HesaplaSatirToplami(
            matrah: 1000m, kdvTutari: 200m, tevkifatTutari: 180m, otvTutari: 0m, oivTutari: 0m, konaklamaVergisiTutari: 0m);

        Assert.Equal(1020m, toplam); // 1000 + 200 - 180
    }

    [Fact]
    public void HesaplaSatirToplami_Otv_ToplamaDahilEdilir()
    {
        var toplam = SatisBelgesiTutarHesaplayici.HesaplaSatirToplami(
            matrah: 1000m, kdvTutari: 200m, tevkifatTutari: 0m, otvTutari: 150m, oivTutari: 0m, konaklamaVergisiTutari: 0m);

        Assert.Equal(1350m, toplam); // 1000 + 200 + 150 (ONCEDEN: 1200 idi, OTV kayboluyordu)
    }

    [Fact]
    public void HesaplaSatirToplami_Oiv_ToplamaDahilEdilir()
    {
        var toplam = SatisBelgesiTutarHesaplayici.HesaplaSatirToplami(
            matrah: 1000m, kdvTutari: 200m, tevkifatTutari: 0m, otvTutari: 0m, oivTutari: 75m, konaklamaVergisiTutari: 0m);

        Assert.Equal(1275m, toplam); // 1000 + 200 + 75 (ONCEDEN: 1200 idi, OIV kayboluyordu)
    }

    [Fact]
    public void HesaplaSatirToplami_KonaklamaVergisi_ToplamaDahilEdilir()
    {
        var toplam = SatisBelgesiTutarHesaplayici.HesaplaSatirToplami(
            matrah: 1000m, kdvTutari: 20m, tevkifatTutari: 0m, otvTutari: 0m, oivTutari: 0m, konaklamaVergisiTutari: 20m);

        Assert.Equal(1040m, toplam); // 1000 + 20 + 20 (ONCEDEN: 1020 idi, konaklama vergisi kayboluyordu)
    }

    [Fact]
    public void HesaplaSatirToplami_BirdenFazlaVergiBirlikte_HepsiToplanir()
    {
        var toplam = SatisBelgesiTutarHesaplayici.HesaplaSatirToplami(
            matrah: 1000m, kdvTutari: 100m, tevkifatTutari: 70m, otvTutari: 50m, oivTutari: 30m, konaklamaVergisiTutari: 20m);

        // 1000 + 100 - 70 + 50 + 30 + 20 = 1130
        Assert.Equal(1130m, toplam);
    }

    [Fact]
    public void HesaplaSatirToplami_VergiYok_SadeceMatrah()
    {
        var toplam = SatisBelgesiTutarHesaplayici.HesaplaSatirToplami(
            matrah: 500m, kdvTutari: 0m, tevkifatTutari: 0m, otvTutari: 0m, oivTutari: 0m, konaklamaVergisiTutari: 0m);

        Assert.Equal(500m, toplam);
    }

    [Fact]
    public void Yuvarla_AwayFromZero_MidpointYukariYuvarlanir()
    {
        Assert.Equal(0.83m, SatisBelgesiTutarHesaplayici.Yuvarla(0.825m));
        Assert.Equal(2.48m, SatisBelgesiTutarHesaplayici.Yuvarla(2.475m));
    }

    // ─────────────────────────────────────────────────────────────
    // SatisBelgesiService.CreateSatirFromRequest / HesaplaBelgeToplamlari
    // (private static — reflection ile cagrilir, DbContext gerekmez)
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void CreateSatirFromRequest_YalnizcaKdvliSatir_MevcutStandartDavranisBozulmaz()
    {
        var satir = InvokeCreateSatirFromRequest(new CreateSatisBelgesiSatiriRequest
        {
            SiraNo = 1,
            Aciklama = "Standart satis",
            Miktar = 1,
            BirimFiyat = 1000m,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
            KdvOrani = 20m
        });

        Assert.Equal(1000m, satir.Matrah);
        Assert.Equal(200m, satir.KdvTutari);
        Assert.Equal(0m, satir.TevkifatTutari);
        Assert.Equal(0m, satir.OtvTutari);
        Assert.Equal(0m, satir.OivTutari);
        Assert.Equal(0m, satir.KonaklamaVergisiTutari);
        Assert.Equal(1200m, satir.SatirToplami);
    }

    [Fact]
    public void CreateSatirFromRequest_KdvTevkifatliSatir_SatirToplamiTevkifatiDusurur()
    {
        var satir = InvokeCreateSatirFromRequest(new CreateSatisBelgesiSatiriRequest
        {
            SiraNo = 1,
            Aciklama = "Tevkifatli hizmet",
            Miktar = 1,
            BirimFiyat = 1000m,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Tevkifatli,
            KdvOrani = 20m,
            TevkifatPay = 5,
            TevkifatPayda = 10
        });

        Assert.Equal(1000m, satir.Matrah);
        Assert.Equal(200m, satir.KdvTutari);
        Assert.Equal(100m, satir.TevkifatTutari); // 200 * 5/10
        Assert.Equal(1100m, satir.SatirToplami); // 1000 + 200 - 100
    }

    [Fact]
    public void CreateSatirFromRequest_OtvOraniVerilenSatir_OtvTutariHesaplanirVeToplamaKatilir()
    {
        var satir = InvokeCreateSatirFromRequest(new CreateSatisBelgesiSatiriRequest
        {
            SiraNo = 1,
            Aciklama = "Otv'li urun",
            Miktar = 1,
            BirimFiyat = 1000m,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
            KdvOrani = 20m,
            OtvOrani = 25m
        });

        Assert.Equal(250m, satir.OtvTutari); // 1000 * 25%
        Assert.Equal(1450m, satir.SatirToplami); // 1000 + 200 + 250
    }

    [Fact]
    public void CreateSatirFromRequest_OivOraniVerilenSatir_OivTutariHesaplanirVeToplamaKatilir()
    {
        var satir = InvokeCreateSatirFromRequest(new CreateSatisBelgesiSatiriRequest
        {
            SiraNo = 1,
            Aciklama = "Oiv'li urun",
            Miktar = 1,
            BirimFiyat = 1000m,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
            KdvOrani = 20m,
            OivOrani = 10m
        });

        Assert.Equal(100m, satir.OivTutari); // 1000 * 10%
        Assert.Equal(1300m, satir.SatirToplami); // 1000 + 200 + 100
    }

    [Fact]
    public void CreateSatirFromRequest_KonaklamaVergisiOraniVerilenSatir_ToplamaKatilir()
    {
        var satir = InvokeCreateSatirFromRequest(new CreateSatisBelgesiSatiriRequest
        {
            SiraNo = 1,
            Aciklama = "Konaklama hizmeti",
            Miktar = 1,
            BirimFiyat = 1000m,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
            KdvOrani = 20m,
            KonaklamaVergisiOrani = 2m
        });

        Assert.Equal(20m, satir.KonaklamaVergisiTutari); // 1000 * 2%
        Assert.Equal(1220m, satir.SatirToplami); // 1000 + 200 + 20
    }

    [Fact]
    public void CreateSatirFromRequest_BirdenFazlaVergiVeTevkifatBirlikte_HepsiToplamaKatilir()
    {
        var satir = InvokeCreateSatirFromRequest(new CreateSatisBelgesiSatiriRequest
        {
            SiraNo = 1,
            Aciklama = "Karma vergili satir",
            Miktar = 1,
            BirimFiyat = 1000m,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Tevkifatli,
            KdvOrani = 18m,
            TevkifatPay = 7,
            TevkifatPayda = 10,
            OtvOrani = 10m,
            OivOrani = 5m,
            KonaklamaVergisiOrani = 2m
        });

        // Matrah=1000, Kdv=180, Tevkifat=180*7/10=126, Otv=100, Oiv=50, Konaklama=20
        Assert.Equal(180m, satir.KdvTutari);
        Assert.Equal(126m, satir.TevkifatTutari);
        Assert.Equal(100m, satir.OtvTutari);
        Assert.Equal(50m, satir.OivTutari);
        Assert.Equal(20m, satir.KonaklamaVergisiTutari);
        // 1000 + 180 - 126 + 100 + 50 + 20 = 1224
        Assert.Equal(1224m, satir.SatirToplami);
    }

    [Fact]
    public void CreateSatirFromRequest_VergisizSatir_SatirToplamiSadeceMatrah()
    {
        var satir = InvokeCreateSatirFromRequest(new CreateSatisBelgesiSatiriRequest
        {
            SiraNo = 1,
            Aciklama = "Kdv kapsam disi",
            Miktar = 1,
            BirimFiyat = 500m,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.KdvKapsamDisi,
            KdvIstisnaTanimId = 1
        });

        Assert.Equal(500m, satir.Matrah);
        Assert.Equal(0m, satir.KdvTutari);
        Assert.Equal(500m, satir.SatirToplami);
    }

    [Fact]
    public void CreateSatirFromRequest_DogrudanTutarGirilenOtv_FallbackTutarDaYuvarlanirVeSatirToplaminaTutarliYansir()
    {
        // OtvOrani verilmediginde (0), OtvTutari dogrudan kullanicidan gelen bir tutar
        // olarak kabul edilir (ResolveRateBasedAmount fallback dali). Bu tutar da,
        // oran bazli daldakiyle AYNI kurala (2 ondalik, AwayFromZero) yuvarlanmalidir -
        // aksi halde satir bazinda kuruş farki olusabilir (bkz. gorev talebi madde 8).
        var satir = InvokeCreateSatirFromRequest(new CreateSatisBelgesiSatiriRequest
        {
            SiraNo = 1,
            Aciklama = "Dogrudan OTV tutari",
            Miktar = 1,
            BirimFiyat = 1000m,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
            KdvOrani = 20m,
            OtvTutari = 33.335m
        });

        Assert.Equal(33.34m, satir.OtvTutari); // 33.335 -> AwayFromZero -> 33.34
        Assert.Equal(1233.34m, satir.SatirToplami); // 1000 + 200 + 33.34
    }

    [Fact]
    public void CreateSatirFromRequest_KesirliMiktarVeBirimFiyatCarpimi_MatrahYuvarlanirVeBagimliHesaplarTutarliOlur()
    {
        // Matrah kolonu decimal(18,2)'dir (bkz. StysAppDbContext). Miktar*BirimFiyat
        // (ikisi de decimal(18,2)) 4 ondalik basamaga kadar ham deger uretebilir
        // (3 * 33.335 = 100.005). Bu deger kullanilmadan once yuvarlanmazsa, KDV/OTV/OIV/
        // konaklama vergisi hesaplari ile veritabanina yazilacak (2 ondalik) Matrah
        // TUTARSIZ kalir - bu test bu kuruş farkinin olusmadigini dogrular.
        var satir = InvokeCreateSatirFromRequest(new CreateSatisBelgesiSatiriRequest
        {
            SiraNo = 1,
            Aciklama = "Kesirli matrah",
            Miktar = 3m,
            BirimFiyat = 33.335m,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
            KdvOrani = 20m
        });

        Assert.Equal(100.01m, satir.Matrah); // 100.005 -> AwayFromZero -> 100.01
        Assert.Equal(20.00m, satir.KdvTutari); // 100.01 * 20% = 20.002 -> 20.00
        Assert.Equal(120.01m, satir.SatirToplami);
    }

    [Fact]
    public void HesaplaBelgeToplamlari_BirdenFazlaSatirdanOlusanBelge_GenelToplamSatirToplamlariToplamidir()
    {
        var belge = new SatisBelgesi();
        belge.Satirlar.Add(InvokeCreateSatirFromRequest(new CreateSatisBelgesiSatiriRequest
        {
            SiraNo = 1, Aciklama = "Satir 1", Miktar = 1, BirimFiyat = 1000m,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m, OtvOrani = 10m
        }));
        belge.Satirlar.Add(InvokeCreateSatirFromRequest(new CreateSatisBelgesiSatiriRequest
        {
            SiraNo = 2, Aciklama = "Satir 2", Miktar = 1, BirimFiyat = 500m,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Tevkifatli, KdvOrani = 18m, TevkifatPay = 5, TevkifatPayda = 10
        }));
        belge.Satirlar.Add(InvokeCreateSatirFromRequest(new CreateSatisBelgesiSatiriRequest
        {
            SiraNo = 3, Aciklama = "Satir 3", Miktar = 1, BirimFiyat = 200m,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 10m, KonaklamaVergisiOrani = 2m
        }));

        InvokeHesaplaBelgeToplamlari(belge);

        // Satir 1: 1000 + 200 + 100 = 1300
        // Satir 2: 500 + 90 - 45 = 545
        // Satir 3: 200 + 20 + 4 = 224
        var beklenenGenelToplam = 1300m + 545m + 224m;
        Assert.Equal(1300m, belge.Satirlar.ElementAt(0).SatirToplami);
        Assert.Equal(545m, belge.Satirlar.ElementAt(1).SatirToplami);
        Assert.Equal(224m, belge.Satirlar.ElementAt(2).SatirToplami);
        Assert.Equal(beklenenGenelToplam, belge.GenelToplam);
        Assert.Equal(1700m, belge.ToplamMatrah); // 1000+500+200
        Assert.Equal(310m, belge.ToplamKdv); // 200+90+20
    }

    [Fact]
    public void HesaplaBelgeToplamlari_YuvarlamaFarkiOlusabilecekDeger_SatirBazliYuvarlamaKullanilir()
    {
        // Her satir: Matrah=10.00, KdvOrani=8.25% -> raw kdv = 0.825, satir bazinda
        // yuvarlaninca 0.83 (AwayFromZero) olur. UC ozdes satir icin:
        //   DOGRU (satir bazli yuvarlama + toplama) : 3 * 10.83 = 32.49
        //   YANLIS (ham degerleri topla, TEK seferde yuvarla): 30.00 + Round(3*0.825=2.475) = 32.48
        // Bu test, belge toplaminin ("tercih edilen") DOGRU yontemle uretildigini dogrular.
        var belge = new SatisBelgesi();
        for (var i = 1; i <= 3; i++)
        {
            belge.Satirlar.Add(InvokeCreateSatirFromRequest(new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = i,
                Aciklama = $"Yuvarlama test satiri {i}",
                Miktar = 1,
                BirimFiyat = 10.00m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
                KdvOrani = 8.25m
            }));
        }

        InvokeHesaplaBelgeToplamlari(belge);

        Assert.All(belge.Satirlar, s => Assert.Equal(10.83m, s.SatirToplami));
        Assert.Equal(32.49m, belge.GenelToplam);
        Assert.NotEqual(32.48m, belge.GenelToplam); // Naif "once topla, sonra bir kez yuvarla" yontemiyle FARKLI sonuc.
    }

    // ─────────────────────────────────────────────────────────────
    // Muhasebe fisi stratejileri — Borc/Alacak dengesi
    // (ÖTV/ÖİV/konaklama vergisi ARTIK bu stratejilere hic ULASMAZ - bkz.
    // SatisBelgesiMuhasebeFisService'in engelleme testleri altta. Bu yuzden
    // bu bolumdeki senaryolarin HICBIRI ek vergi ICERMEZ.)
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SatisFaturasiMuhasebeFisStratejisi_MevcutStandartFatura_BorcAlacakDengeliVeMevcutSekildeKalir()
    {
        var belge = BuildSatisBelgesi([
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Standart oda ucreti", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);

        var context = BuildFisContext();
        var strateji = new SatisFaturasiMuhasebeFisStratejisi();

        var satirlar = await strateji.SatirlariOlusturAsync(belge, context, CancellationToken.None);

        var toplamBorc = satirlar.Sum(x => x.Borc);
        var toplamAlacak = satirlar.Sum(x => x.Alacak);

        Assert.Equal(belge.GenelToplam, toplamBorc);
        Assert.Equal(toplamBorc, toplamAlacak);
        Assert.Equal(1200m, belge.GenelToplam); // 1000 + 200, ONCEKI/mevcut davranis bozulmadi
    }

    [Fact]
    public async Task SatisTevkifatliFaturaMuhasebeFisStratejisi_TevkifatliFatura_MevcutDavranislaDengeliKalir()
    {
        var belge = BuildSatisBelgesi([
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Tevkifatli hizmet", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Tevkifatli, KdvOrani = 20m,
                TevkifatPay = 7, TevkifatPayda = 10
            }
        ]);

        var context = BuildFisContext();
        var strateji = new SatisTevkifatliFaturaMuhasebeFisStratejisi(new FakeTevkifatHesapEslemeService(hesapPlaniId: 999));

        var satirlar = await strateji.SatirlariOlusturAsync(belge, context, CancellationToken.None);

        var toplamBorc = satirlar.Sum(x => x.Borc);
        var toplamAlacak = satirlar.Sum(x => x.Alacak);
        Assert.Equal(toplamBorc, toplamAlacak);

        var cariSatiri = Assert.Single(satirlar, x => x.MuhasebeHesapPlaniId == context.CariHesapPlaniId);
        Assert.Equal(belge.GenelToplam, cariSatiri.Borc);

        // Matrah=1000, Kdv=200, Tevkifat=140 -> SatirToplami = 1000 + 200 - 140 = 1060
        Assert.Equal(1060m, belge.GenelToplam);

        var tevkifatSatiri = Assert.Single(satirlar, x => x.MuhasebeHesapPlaniId == 999);
        Assert.Equal(140m, tevkifatSatiri.Borc);
    }

    [Fact]
    public async Task SatisIadeFaturasiMuhasebeFisStratejisi_StandartIade_GercekStratejiCagrisiylaDengeliKalir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.SatisIade, tesisId: 1);

        var belge = BuildSatisBelgesi([
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Iade edilen konaklama", Miktar = 1, BirimFiyat = 800m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 10m
            }
        ]);
        belge.BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi;

        var context = BuildFisContext();
        var strateji = new SatisIadeFaturasiMuhasebeFisStratejisi(dbContext);

        var satirlar = await strateji.SatirlariOlusturAsync(belge, context, CancellationToken.None);

        var toplamBorc = satirlar.Sum(x => x.Borc);
        var toplamAlacak = satirlar.Sum(x => x.Alacak);
        Assert.Equal(toplamBorc, toplamAlacak);
        Assert.Equal(belge.GenelToplam, toplamAlacak);
        Assert.Equal(880m, belge.GenelToplam); // 800 + 80
    }

    [Fact]
    public async Task AlisFaturasiMuhasebeFisStratejisi_StandartFatura_GercekStratejiCagrisiylaDengeliKalir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var giderHesap = await SeedDetayHesapAsync(dbContext, "TEST-GIDER-1");
        var kdvHesap = await SeedDetayHesapAsync(dbContext, "TEST-KDV-1");
        var cariHesap = await SeedDetayHesapAsync(dbContext, "TEST-CARI-1");

        var belge = BuildSatisBelgesi([
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Hizmet alimi", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);
        belge.BelgeTipi = SatisBelgesiTipi.AlisFaturasi;

        var context = BuildAlisFisContext(cariHesap.Id, kdvHesap.Id, hizmetGiderHesapPlaniId: giderHesap.Id);
        var strateji = new AlisFaturasiMuhasebeFisStratejisi(dbContext, new FakeTasinirKodMuhasebeHesapEslemeService());

        var satirlar = await strateji.SatirlariOlusturAsync(belge, context, CancellationToken.None);

        var toplamBorc = satirlar.Sum(x => x.Borc);
        var toplamAlacak = satirlar.Sum(x => x.Alacak);
        Assert.Equal(toplamBorc, toplamAlacak);
        Assert.Equal(belge.GenelToplam, toplamAlacak);
        Assert.Equal(1200m, belge.GenelToplam); // 1000 + 200, standart alis davranisi
    }

    [Fact]
    public async Task AlisIadeFaturasiMuhasebeFisStratejisi_StandartIade_GercekStratejiCagrisiylaDengeliKalir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var giderHesap = await SeedDetayHesapAsync(dbContext, "TEST-GIDER-2");
        var kdvHesap = await SeedDetayHesapAsync(dbContext, "TEST-KDV-2");
        var cariHesap = await SeedDetayHesapAsync(dbContext, "TEST-CARI-2");

        var belge = BuildSatisBelgesi([
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Iade edilen hizmet", Miktar = 1, BirimFiyat = 500m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);
        belge.BelgeTipi = SatisBelgesiTipi.AlisIadeFaturasi;

        var context = BuildAlisFisContext(cariHesap.Id, kdvHesap.Id, hizmetGiderHesapPlaniId: giderHesap.Id);
        var strateji = new AlisIadeFaturasiMuhasebeFisStratejisi(dbContext, new FakeTasinirKodMuhasebeHesapEslemeService());

        var satirlar = await strateji.SatirlariOlusturAsync(belge, context, CancellationToken.None);

        var toplamBorc = satirlar.Sum(x => x.Borc);
        var toplamAlacak = satirlar.Sum(x => x.Alacak);
        Assert.Equal(toplamBorc, toplamAlacak);
        Assert.Equal(belge.GenelToplam, toplamBorc);
        Assert.Equal(600m, belge.GenelToplam); // 500 + 100
    }

    [Fact]
    public async Task AlisTevkifatliFaturaMuhasebeFisStratejisi_TevkifatliFatura_GercekStratejiCagrisiylaDengeliKalir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var giderHesap = await SeedDetayHesapAsync(dbContext, "TEST-GIDER-3");
        var kdvHesap = await SeedDetayHesapAsync(dbContext, "TEST-KDV-3");
        var cariHesap = await SeedDetayHesapAsync(dbContext, "TEST-CARI-3");

        var belge = BuildSatisBelgesi([
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Tevkifatli hizmet alimi", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Tevkifatli, KdvOrani = 20m,
                TevkifatPay = 5, TevkifatPayda = 10
            }
        ]);
        belge.BelgeTipi = SatisBelgesiTipi.AlisFaturasi;

        var context = BuildAlisFisContext(cariHesap.Id, kdvHesap.Id, hizmetGiderHesapPlaniId: giderHesap.Id);
        var strateji = new AlisTevkifatliFaturaMuhasebeFisStratejisi(
            dbContext, new FakeTasinirKodMuhasebeHesapEslemeService(), new FakeTevkifatHesapEslemeService(hesapPlaniId: 999));

        var satirlar = await strateji.SatirlariOlusturAsync(belge, context, CancellationToken.None);

        var toplamBorc = satirlar.Sum(x => x.Borc);
        var toplamAlacak = satirlar.Sum(x => x.Alacak);
        Assert.Equal(toplamBorc, toplamAlacak);

        // Matrah=1000, Kdv=200, Tevkifat=100 -> GenelToplam (cari alacak) = 1000+200-100=1100
        Assert.Equal(1100m, belge.GenelToplam);
        var cariSatiri = Assert.Single(satirlar, x => x.MuhasebeHesapPlaniId == context.CariHesapPlaniId);
        Assert.Equal(belge.GenelToplam, cariSatiri.Alacak);
    }

    // ─────────────────────────────────────────────────────────────
    // SatisBelgesiMuhasebeFisService — ÖTV/ÖİV/konaklama vergisi icin
    // otomatik muhasebe fisi engelleme testleri (gercek servis cagrisi,
    // InMemory DbContext).
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task MuhasebeFisiOlusturAsync_OtvIcerenSatisFaturasi_EngellenirVeHicKayitOlusmaz()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var belge = await SeedMuhasebeOnaylanmisBelgeAsync(dbContext, SatisBelgesiTipi.SatisFaturasi, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Alkollu icecek", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m, OtvOrani = 25m
            }
        ]);

        var service = CreateMuhasebeFisService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(
            () => service.MuhasebeFisiOlusturAsync(belge.Id, CancellationToken.None));

        Assert.Contains(
            "ÖTV, ÖİV veya konaklama vergisi içeren belgeler için muhasebe hesap eşlemeleri henüz tanımlanmamıştır",
            ex.Message);

        await AssertHicKayitOlusmadiAsync(dbContext, belge.Id);
    }

    [Fact]
    public async Task MuhasebeFisiOlusturAsync_OivIcerenSatisFaturasi_Engellenir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var belge = await SeedMuhasebeOnaylanmisBelgeAsync(dbContext, SatisBelgesiTipi.SatisFaturasi, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Oiv'li urun", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m, OivOrani = 10m
            }
        ]);

        var service = CreateMuhasebeFisService(dbContext);

        await Assert.ThrowsAsync<BaseException>(() => service.MuhasebeFisiOlusturAsync(belge.Id, CancellationToken.None));

        await AssertHicKayitOlusmadiAsync(dbContext, belge.Id);
    }

    [Fact]
    public async Task MuhasebeFisiOlusturAsync_KonaklamaVergisiIcerenSatisFaturasi_Engellenir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var belge = await SeedMuhasebeOnaylanmisBelgeAsync(dbContext, SatisBelgesiTipi.SatisFaturasi, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Konaklama hizmeti", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 10m, KonaklamaVergisiOrani = 2m
            }
        ]);

        var service = CreateMuhasebeFisService(dbContext);

        await Assert.ThrowsAsync<BaseException>(() => service.MuhasebeFisiOlusturAsync(belge.Id, CancellationToken.None));

        await AssertHicKayitOlusmadiAsync(dbContext, belge.Id);
    }

    [Fact]
    public async Task MuhasebeFisiOlusturAsync_OtvIcerenAlisFaturasi_Engellenir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var belge = await SeedMuhasebeOnaylanmisBelgeAsync(dbContext, SatisBelgesiTipi.AlisFaturasi, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Alkollu icecek alimi", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m, OtvOrani = 25m
            }
        ]);

        var service = CreateMuhasebeFisService(dbContext);

        await Assert.ThrowsAsync<BaseException>(() => service.MuhasebeFisiOlusturAsync(belge.Id, CancellationToken.None));

        await AssertHicKayitOlusmadiAsync(dbContext, belge.Id);
    }

    [Fact]
    public async Task MuhasebeFisiOlusturAsync_KonaklamaVergisiIcerenSatisIadeFaturasi_Engellenir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var belge = await SeedMuhasebeOnaylanmisBelgeAsync(dbContext, SatisBelgesiTipi.SatisIadeFaturasi, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Iade edilen konaklama", Miktar = 1, BirimFiyat = 800m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 10m, KonaklamaVergisiOrani = 2m
            }
        ]);

        var service = CreateMuhasebeFisService(dbContext);

        await Assert.ThrowsAsync<BaseException>(() => service.MuhasebeFisiOlusturAsync(belge.Id, CancellationToken.None));

        await AssertHicKayitOlusmadiAsync(dbContext, belge.Id);
    }

    [Fact]
    public async Task MuhasebeFisiOlusturAsync_OtvIcerenAlisIadeFaturasi_Engellenir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var belge = await SeedMuhasebeOnaylanmisBelgeAsync(dbContext, SatisBelgesiTipi.AlisIadeFaturasi, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Iade edilen alkollu icecek", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m, OtvOrani = 25m
            }
        ]);

        var service = CreateMuhasebeFisService(dbContext);

        await Assert.ThrowsAsync<BaseException>(() => service.MuhasebeFisiOlusturAsync(belge.Id, CancellationToken.None));

        await AssertHicKayitOlusmadiAsync(dbContext, belge.Id);
    }

    [Fact]
    public async Task MuhasebeFisiOlusturAsync_EkVergiIcermeyenStandartSatisFaturasi_EngellenmezVeFisOlusur()
    {
        // Regresyon: guard sadece ek vergi ICEREN belgeleri engeller; standart KDV'li
        // (ek vergisiz) belgelerin muhasebe fisi olusturma davranisi DEGISMEMELIDIR.
        await using var dbContext = CreateInMemoryDbContext();
        var belge = await SeedMuhasebeOnaylanmisBelgeAsync(dbContext, SatisBelgesiTipi.SatisFaturasi, [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Standart oda ucreti", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]);

        await SeedCariMusteriKartAsync(dbContext, belge.CariKartId!.Value);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.GelirSatis, tesisId: 1);
        await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.KDVHesaplanan, tesisId: 1);

        var service = CreateMuhasebeFisService(dbContext);

        var dto = await service.MuhasebeFisiOlusturAsync(belge.Id, CancellationToken.None);

        Assert.NotNull(dto.MuhasebeFisId);
        Assert.True(await dbContext.MuhasebeFisler.AnyAsync(x => x.KaynakId == belge.Id));
    }

    // ─────────────────────────────────────────────────────────────
    // Yardimcilar
    // ─────────────────────────────────────────────────────────────

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

    private static SatisBelgesiMuhasebeFisContext BuildFisContext() => new()
    {
        TesisId = 1,
        MaliYil = 2026,
        Donem = 1,
        FisTarihi = new DateTime(2026, 1, 15),
        FisNo = "FIS-1",
        BelgeNo = "TEST-1",
        CariHesapPlaniId = 100,
        CariKartId = 1,
        GelirHesapPlaniId = 200,
        KdvHesapPlaniId = 300
    };

    private static SatisBelgesiMuhasebeFisContext BuildAlisFisContext(
        int cariHesapPlaniId, int kdvHesapPlaniId, int? stokHesapPlaniId = null, int? hizmetGiderHesapPlaniId = null) => new()
    {
        TesisId = 1,
        MaliYil = 2026,
        Donem = 1,
        FisTarihi = new DateTime(2026, 1, 15),
        FisNo = "FIS-1",
        BelgeNo = "TEST-1",
        CariHesapPlaniId = cariHesapPlaniId,
        GelirHesapPlaniId = 0,
        KdvHesapPlaniId = kdvHesapPlaniId,
        StokHesapPlaniId = stokHesapPlaniId,
        HizmetGiderHesapPlaniId = hizmetGiderHesapPlaniId
    };

    private static StysAppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            // SatisBelgesiMuhasebeFisService.BeginTransactionAsync kullanir; InMemory
            // saglayici gercek transaction desteklemez ama bunu (varsayilan olarak hata
            // firlatan) bir uyari olarak bildirir - servis gercek SQL Server'da
            // transaction'a ihtiyac duydugu icin bu uyari burada bilinçli olarak yok sayilir.
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new StysAppDbContext(options);
    }

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<SatisBelgesiProfile>();
        }, NullLoggerFactory.Instance);

        return config.CreateMapper();
    }

    private static async Task<MuhasebeHesapPlani> SeedDetayHesapAsync(StysAppDbContext dbContext, string tamKod, int? tesisId = null)
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

    private static async Task<SatisBelgesi> SeedMuhasebeOnaylanmisBelgeAsync(
        StysAppDbContext dbContext,
        SatisBelgesiTipi belgeTipi,
        IEnumerable<CreateSatisBelgesiSatiriRequest> satirRequestleri)
    {
        var belge = BuildSatisBelgesi(satirRequestleri);
        belge.BelgeTipi = belgeTipi;
        belge.Durum = SatisBelgesiDurumu.MuhasebeOnaylandi;

        dbContext.SatisBelgeleri.Add(belge);
        await dbContext.SaveChangesAsync();
        return belge;
    }

    private static async Task SeedCariMusteriKartAsync(StysAppDbContext dbContext, int cariKartId)
    {
        // Sadece "EkVergiIcermeyenStandartSatisFaturasi_EngellenmezVeFisOlusur" regresyon
        // testinde, gercek fis olusturma akisinin sonuna kadar gitmesi icin gereken minimum
        // CariKart + hesap plani baglantisini kurar.
        var hesap = await SeedDetayHesapAsync(dbContext, MuhasebeAnaHesapKodlari.CariMusteri, tesisId: 1);
        dbContext.CariKartlar.Add(new CariKart
        {
            Id = cariKartId,
            CariTipi = CariKartTipleri.Musteri,
            CariKodu = $"TEST-{cariKartId}",
            UnvanAdSoyad = "Test Musteri",
            AktifMi = true,
            TesisId = 1,
            MuhasebeHesapPlaniId = hesap.Id
        });
        await dbContext.SaveChangesAsync();
    }

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
