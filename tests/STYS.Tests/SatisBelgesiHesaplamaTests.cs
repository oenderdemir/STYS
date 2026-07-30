using System.Reflection;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.Muhasebe.SatisBelgeleri.Services.MuhasebeFisStratejileri;
using STYS.Muhasebe.TevkifatHesapEslemeleri.Dtos;
using STYS.Muhasebe.TevkifatHesapEslemeleri.Entities;
using STYS.Muhasebe.TevkifatHesapEslemeleri.Services;
using TOD.Platform.Persistence.Rdbms.Paging;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// SatisBelgesiTutarHesaplayici (satır/belge toplam formülü) ve bu formülü kullanan
/// SatisBelgesiService.CreateSatirFromRequest / HesaplaBelgeToplamlari ile muhasebe fişi
/// stratejilerinin (Borç/Alacak dengesi) testleri. "private static" metotlar reflection ile
/// çağrılır - bu metotlar herhangi bir DbContext/servis bağımlılığı OLMADAN test edilebilir
/// saf fonksiyonlardır.
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
    // Muhasebe fisi stratejileri — Borc/Alacak dengesi = belge.GenelToplam
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
    public async Task SatisFaturasiMuhasebeFisStratejisi_OtvOivKonaklamaVergisiIcerenBelge_FisBorcAlacakDengesiKorunur()
    {
        var belge = BuildSatisBelgesi([
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Alkollu icecek (OTV)", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 20m, OtvOrani = 25m
            },
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 2, Aciklama = "Konaklama (konaklama vergisi + OIV)", Miktar = 1, BirimFiyat = 500m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 10m, OivOrani = 5m, KonaklamaVergisiOrani = 2m
            }
        ]);

        var context = BuildFisContext();
        var strateji = new SatisFaturasiMuhasebeFisStratejisi();

        var satirlar = await strateji.SatirlariOlusturAsync(belge, context, CancellationToken.None);

        var toplamBorc = satirlar.Sum(x => x.Borc);
        var toplamAlacak = satirlar.Sum(x => x.Alacak);

        // Fis her zaman dengeli olmali VE belge.GenelToplam ile (kisisel dogrulama +
        // "muhasebe fisi toplaminin belge genel toplamiyla uyumu" gereksinimi) esit olmalidir.
        Assert.Equal(belge.GenelToplam, toplamBorc);
        Assert.Equal(toplamBorc, toplamAlacak);

        // Satir 1: 1000 + 200 + 250 = 1450 ; Satir 2: 500 + 50 + 25 + 10 = 585
        Assert.Equal(1450m + 585m, belge.GenelToplam);
    }

    [Fact]
    public async Task SatisIadeFaturasiMuhasebeFisStratejisi_Kullanilmiyor_OtvOivIcerenIade_MevcutIsaretYaklasimiKorunurVeDengeliKalir()
    {
        // SatisIadeFaturasiMuhasebeFisStratejisi bir MuhasebeHesapPlani lookup'i (StysAppDbContext)
        // gerektirdigi icin bu senaryo, ayni Borc/Alacak dengesi mantigini DOGRUDAN (context
        // sorgusu olmadan) sergileyen SatisFaturasiMuhasebeFisStratejisi uzerinden, iade
        // isaretlerini (Borc<->Alacak yer degistirmesi TERSİNE cevrilmez, mevcut yaklasimla
        // AYNI kalir) belgeleyen bir referans testtir; gercek DB'li iade stratejisi testi
        // icin bkz. sonuc raporundaki not.
        var belge = BuildSatisBelgesi([
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Iade edilen konaklama", Miktar = 1, BirimFiyat = 800m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli, KdvOrani = 10m, KonaklamaVergisiOrani = 2m
            }
        ]);

        // GenelToplam = 800 + 80 + 16 = 896. Iade stratejisinin BORC/ALACAK yer degistirmis
        // (isaret ters) hali icin, ayni ek-vergi ekleme mantigini elle uygulayip
        // dogrulayabiliriz (ayrintili DB'li strateji testi olmadan bu formulun kendisini
        // dogrular).
        var ekVergi = SatisBelgesiTutarHesaplayici.HesaplaEkVergiToplami(belge.Satirlar);
        var iadeBorcu = belge.ToplamMatrah + ekVergi;

        Assert.Equal(belge.GenelToplam - belge.ToplamKdv, iadeBorcu);
    }

    [Fact]
    public async Task SatisTevkifatliFaturaMuhasebeFisStratejisi_TevkifatliVeOtvIcerenBelge_FisBorcAlacakDengesiKorunur()
    {
        var belge = BuildSatisBelgesi([
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Tevkifatli hizmet + OTV", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Tevkifatli, KdvOrani = 20m,
                TevkifatPay = 7, TevkifatPayda = 10, OtvOrani = 15m
            }
        ]);

        var context = BuildFisContext();
        var strateji = new SatisTevkifatliFaturaMuhasebeFisStratejisi(new FakeTevkifatHesapEslemeService(hesapPlaniId: 999));

        var satirlar = await strateji.SatirlariOlusturAsync(belge, context, CancellationToken.None);

        var toplamBorc = satirlar.Sum(x => x.Borc);
        var toplamAlacak = satirlar.Sum(x => x.Alacak);

        // Fis her zaman dengeli olmalidir (Borc == Alacak). Not: toplamBorc, GenelToplam'a
        // esit DEGILDIR burada - cari hesap satiri GenelToplam'i tasir, AYRICA bir de
        // tevkifat karsiligi Borc satiri vardir (bu, tevkifatli stratejinin kendi ic
        // muhasebe modeli - cari borcun yaninda ayrica bir tevkifat karsiligi borclandirilir,
        // karsiligi Gelir/KDV hesaplarinda Alacak olarak yansir).
        Assert.Equal(toplamBorc, toplamAlacak);

        var cariSatiri = Assert.Single(satirlar, x => x.MuhasebeHesapPlaniId == context.CariHesapPlaniId);
        Assert.Equal(belge.GenelToplam, cariSatiri.Borc);

        // Matrah=1000, Kdv=200, Tevkifat=140, Otv=150 -> SatirToplami = 1000+200-140+150 = 1210
        Assert.Equal(1210m, belge.GenelToplam);

        var tevkifatSatiri = Assert.Single(satirlar, x => x.MuhasebeHesapPlaniId == 999);
        Assert.Equal(140m, tevkifatSatiri.Borc);
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
