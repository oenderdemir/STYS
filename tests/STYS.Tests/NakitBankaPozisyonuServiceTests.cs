using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Iller.Entities;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.NakitBankaPozisyonu.Dtos;
using STYS.Muhasebe.NakitBankaPozisyonu.Services;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

/// <summary>
/// NakitBankaPozisyonuService'in gercek is kurallarini GERCEK SQL Server'a karsi dogrular. Her
/// test KENDI Kurum/Il/Tesis/CariKart/MuhasebeHesapPlani/KasaBankaHesap/PosTahsilatValor/
/// MuhasebeFis kayitlarini (benzersiz "NBP-970-{guid}" isaretiyle) olusturur ve `finally` icinde
/// GERCEKTEN temizler (ID bazli, guvenli silme).
/// </summary>
[Trait("Category", "Integration")]
public class NakitBankaPozisyonuServiceTests : IAsyncLifetime
{
    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);

    private const string TestMarker = "NBP-970";

    private readonly List<int> _tesisIdler = [];
    private readonly List<int> _kurumIdler = [];
    private readonly List<int> _illIdler = [];
    private readonly List<int> _cariKartIdler = [];
    private readonly List<int> _hesapPlaniIdler = [];
    private readonly List<int> _kasaBankaHesapIdler = [];
    private readonly List<int> _belgeIdler = [];
    private readonly List<int> _valorIdler = [];
    private readonly List<int> _fisIdler = [];

    private static StysAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>().UseSqlServer(ConnectionString).Options;
        return new StysAppDbContext(options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor());
    }

    private static NakitBankaPozisyonuService CreateService(StysAppDbContext dbContext, params int[] erisilebilirTesisIds) =>
        new(dbContext, new FakeMuhasebeTesisScopeService(erisilebilirTesisIds));

    private async Task<int> YeniTesisAsync(StysAppDbContext dbContext, string suffix)
    {
        var kurum = new Kurum { Kod = suffix, Ad = "Test Kurum " + suffix, AktifMi = true };
        dbContext.Kurumlar.Add(kurum);
        var il = new Il { Ad = "Test Il " + suffix, AktifMi = true };
        dbContext.Iller.Add(il);
        await dbContext.SaveChangesAsync();
        _kurumIdler.Add(kurum.Id);
        _illIdler.Add(il.Id);

        var tesis = new Tesis { KurumId = kurum.Id, IlId = il.Id, Ad = "Test Tesis " + suffix, Telefon = "0000", Adres = "Test Adres", AktifMi = true };
        dbContext.Tesisler.Add(tesis);
        await dbContext.SaveChangesAsync();
        _tesisIdler.Add(tesis.Id);
        return tesis.Id;
    }

    private async Task<int> YeniHesapPlaniAsync(StysAppDbContext dbContext, string suffix, string etiket)
    {
        var hesap = new MuhasebeHesapPlani
        {
            Kod = $"{suffix}-{etiket}",
            TamKod = $"1.10.{suffix}-{etiket}",
            Ad = "Test " + etiket,
            SeviyeNo = 3,
            HesapTipi = HesapTipi.DetayHesap,
            AktifMi = true,
            DetayHesapMi = true,
            HareketGorebilirMi = true
        };
        dbContext.MuhasebeHesapPlanlari.Add(hesap);
        await dbContext.SaveChangesAsync();
        _hesapPlaniIdler.Add(hesap.Id);
        return hesap.Id;
    }

    private async Task<int> YeniKasaBankaHesabiAsync(
        StysAppDbContext dbContext, int tesisId, string tip, string suffix, string etiket,
        int? muhasebeHesapPlaniId, string paraBirimi = "TRY", string? iban = null, string? bankaAdi = null, bool aktifMi = true)
    {
        var hesap = new KasaBankaHesap
        {
            TesisId = tesisId,
            Tip = tip,
            Kod = $"{suffix}-{etiket}",
            Ad = "Test " + etiket,
            ParaBirimi = paraBirimi,
            AktifMi = aktifMi,
            MuhasebeHesapPlaniId = muhasebeHesapPlaniId,
            Iban = iban,
            BankaAdi = bankaAdi
        };
        dbContext.KasaBankaHesaplari.Add(hesap);
        await dbContext.SaveChangesAsync();
        _kasaBankaHesapIdler.Add(hesap.Id);
        return hesap.Id;
    }

    private async Task<int> YeniCariKartAsync(StysAppDbContext dbContext, int tesisId, string suffix)
    {
        var cari = new CariKart { TesisId = tesisId, CariTipi = CariKartTipleri.Musteri, CariKodu = suffix, UnvanAdSoyad = "Test Musteri " + suffix, AktifMi = true };
        dbContext.CariKartlar.Add(cari);
        await dbContext.SaveChangesAsync();
        _cariKartIdler.Add(cari.Id);
        return cari.Id;
    }

    private async Task<int> YeniTahsilatBelgesiAsync(StysAppDbContext dbContext, int cariKartId, int kasaBankaHesapId, decimal tutar, string suffix)
    {
        var belge = new TahsilatOdemeBelgesi
        {
            BelgeNo = suffix,
            BelgeTarihi = DateTime.UtcNow.Date,
            BelgeTipi = TahsilatOdemeBelgeTipleri.Tahsilat,
            CariKartId = cariKartId,
            Tutar = tutar,
            ParaBirimi = "TRY",
            OdemeYontemi = OdemeYontemleri.KrediKarti,
            KasaBankaHesapId = kasaBankaHesapId,
            Durum = TahsilatOdemeBelgeDurumlari.Aktif
        };
        dbContext.TahsilatOdemeBelgeleri.Add(belge);
        await dbContext.SaveChangesAsync();
        _belgeIdler.Add(belge.Id);
        return belge.Id;
    }

    private async Task<int> YeniValorAsync(
        StysAppDbContext dbContext, int tesisId, int belgeId, int krediKartiHesapId, int? bagliBankaHesapId,
        string durum, DateOnly beklenenValorTarihi, decimal brut, decimal komisyon, decimal net,
        string paraBirimi = "TRY", int? muhasebeFisId = null)
    {
        var valor = new PosTahsilatValor
        {
            TesisId = tesisId,
            TahsilatOdemeBelgesiId = belgeId,
            KrediKartiHesapId = krediKartiHesapId,
            BagliBankaHesapId = bagliBankaHesapId,
            OdemeTarihi = DateTime.UtcNow.Date,
            ValorGunSayisi = 0,
            ValorGunTuru = ValorGunTurleri.TakvimGunu,
            BeklenenValorTarihi = beklenenValorTarihi,
            OtomatikAktarimMi = false,
            BrutTutar = brut,
            KomisyonTutari = komisyon,
            NetTutar = net,
            ParaBirimi = paraBirimi,
            Durum = durum,
            MuhasebeFisId = muhasebeFisId
        };
        dbContext.PosTahsilatValorleri.Add(valor);
        await dbContext.SaveChangesAsync();
        _valorIdler.Add(valor.Id);
        return valor.Id;
    }

    private async Task<int> YeniFisAsync(StysAppDbContext dbContext, int tesisId, DateTime fisTarihi, string durum, params (int HesapPlaniId, decimal Borc, decimal Alacak)[] satirlar)
    {
        var toplamBorc = satirlar.Sum(x => x.Borc);
        var toplamAlacak = satirlar.Sum(x => x.Alacak);
        var fis = new MuhasebeFis
        {
            TesisId = tesisId,
            MaliYil = fisTarihi.Year,
            Donem = fisTarihi.Month,
            FisNo = $"{TestMarker}-{Guid.NewGuid():N}"[..20],
            FisTarihi = fisTarihi,
            FisTipi = MuhasebeFisTipleri.Mahsup,
            Durum = durum,
            ToplamBorc = toplamBorc,
            ToplamAlacak = toplamAlacak,
            YevmiyeNo = new Random().Next(100000, 999999)
        };
        dbContext.MuhasebeFisler.Add(fis);
        await dbContext.SaveChangesAsync();
        _fisIdler.Add(fis.Id);

        var siraNo = 1;
        foreach (var s in satirlar)
        {
            dbContext.MuhasebeFisSatirlari.Add(new MuhasebeFisSatir
            {
                MuhasebeFisId = fis.Id,
                MuhasebeHesapPlaniId = s.HesapPlaniId,
                SiraNo = siraNo++,
                Borc = s.Borc,
                Alacak = s.Alacak,
                ParaBirimi = "TRY"
            });
        }
        await dbContext.SaveChangesAsync();

        return fis.Id;
    }

    private static string YeniSuffix() => $"{TestMarker}-{Guid.NewGuid():N}"[..20];

    /// <summary>Servisin kendi "bugun" tanimiyla (Europe/Istanbul) BIREBIR ayni - testler
    /// DateTime.UtcNow.Date kullanirsa, UTC 21:00-23:59 saatleri arasinda (Istanbul UTC+3 oldugu
    /// icin) servisin "bugun"u testin "bugun"unden BIR GUN ILERI olur ve gecmis-tarih-raporu
    /// mantigi yanlislikla devreye girerdi - bu yuzden tum "bugun"e bagli testler bu helper'i
    /// kullanir.</summary>
    private static DateOnly BugunIstanbul()
    {
        TimeZoneInfo istanbul;
        try
        {
            istanbul = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        }
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istanbul));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>Her adim BAGIMSIZ ve kendi TAZE StysAppDbContext'ini acar - bir adimin basarisiz
    /// olmasi sonraki adimlarin calismasini ENGELLEMEZ (eskiden TEK try/catch icinde ilk hatada
    /// tum kalan adimlar atlaniyordu). FK sirasiyla: fis satirlari/fisler -> valor kayitlari ->
    /// tahsilat belgeleri -> kasa/banka hesaplari -> cari kartlar -> hesap planlari -> tesisler ->
    /// iller -> kurumlar. Tum sorgular IgnoreQueryFilters() kullanir (soft-delete edilmis test
    /// kayitlarinin normal filtre yuzunden sessizce atlanmasini onlemek icin) ve ONCEDEN kaydedilen
    /// ID listelerini kullanir - parent silindikten sonra child'lar parent uzerinden YENIDEN
    /// bulunmaya calisilmaz.</summary>
    private List<STYS.Tests.TestSupport.CleanupAdimi> OlusturCleanupAdimlari() =>
    [
        new("MuhasebeFisSatirlari + MuhasebeFisler silme", async () =>
        {
            if (_fisIdler.Count == 0) return;
            await using var dbContext = CreateDbContext();
            await dbContext.MuhasebeFisSatirlari.IgnoreQueryFilters().Where(x => _fisIdler.Contains(x.MuhasebeFisId)).ExecuteDeleteAsync();
            await dbContext.MuhasebeFisler.IgnoreQueryFilters().Where(x => _fisIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("PosTahsilatValorleri silme", async () =>
        {
            if (_valorIdler.Count == 0) return;
            await using var dbContext = CreateDbContext();
            await dbContext.PosTahsilatValorleri.IgnoreQueryFilters().Where(x => _valorIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("TahsilatOdemeBelgeleri silme", async () =>
        {
            if (_belgeIdler.Count == 0) return;
            await using var dbContext = CreateDbContext();
            await dbContext.TahsilatOdemeBelgeleri.IgnoreQueryFilters().Where(x => _belgeIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("KasaBankaHesaplari silme", async () =>
        {
            if (_kasaBankaHesapIdler.Count == 0) return;
            await using var dbContext = CreateDbContext();
            await dbContext.KasaBankaHesaplari.IgnoreQueryFilters().Where(x => _kasaBankaHesapIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("CariKartlar silme", async () =>
        {
            if (_cariKartIdler.Count == 0) return;
            await using var dbContext = CreateDbContext();
            await dbContext.CariKartlar.IgnoreQueryFilters().Where(x => _cariKartIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("MuhasebeHesapPlanlari silme", async () =>
        {
            if (_hesapPlaniIdler.Count == 0) return;
            await using var dbContext = CreateDbContext();
            await dbContext.MuhasebeHesapPlanlari.IgnoreQueryFilters().Where(x => _hesapPlaniIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("Tesisler silme", async () =>
        {
            if (_tesisIdler.Count == 0) return;
            await using var dbContext = CreateDbContext();
            await dbContext.Tesisler.IgnoreQueryFilters().Where(x => _tesisIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("Iller silme", async () =>
        {
            if (_illIdler.Count == 0) return;
            await using var dbContext = CreateDbContext();
            await dbContext.Iller.IgnoreQueryFilters().Where(x => _illIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("Kurumlar silme", async () =>
        {
            if (_kurumIdler.Count == 0) return;
            await using var dbContext = CreateDbContext();
            await dbContext.Kurumlar.IgnoreQueryFilters().Where(x => _kurumIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
    ];

    /// <summary>Cleanup adimlari "basarili" raporlasa bile KOR KOR guvenilmez - fiziksel olarak
    /// (IgnoreQueryFilters ile, soft-delete DAHIL) hicbir kayit kalmadigi AYRICA dogrulanir.</summary>
    private async Task<Dictionary<string, int>> DogrulaTemizlikKalintilariAsync()
    {
        await using var dbContext = CreateDbContext();
        var kalanlar = new Dictionary<string, int>();

        async Task KontrolEt<T>(string tabloAdi, IQueryable<T> sorgu)
        {
            var adet = await sorgu.CountAsync();
            if (adet > 0)
            {
                kalanlar[tabloAdi] = adet;
            }
        }

        if (_fisIdler.Count > 0)
        {
            await KontrolEt("MuhasebeFisSatirlari", dbContext.MuhasebeFisSatirlari.IgnoreQueryFilters().Where(x => _fisIdler.Contains(x.MuhasebeFisId)));
            await KontrolEt("MuhasebeFisler", dbContext.MuhasebeFisler.IgnoreQueryFilters().Where(x => _fisIdler.Contains(x.Id)));
        }
        if (_valorIdler.Count > 0)
        {
            await KontrolEt("PosTahsilatValorleri", dbContext.PosTahsilatValorleri.IgnoreQueryFilters().Where(x => _valorIdler.Contains(x.Id)));
        }
        if (_belgeIdler.Count > 0)
        {
            await KontrolEt("TahsilatOdemeBelgeleri", dbContext.TahsilatOdemeBelgeleri.IgnoreQueryFilters().Where(x => _belgeIdler.Contains(x.Id)));
        }
        if (_kasaBankaHesapIdler.Count > 0)
        {
            await KontrolEt("KasaBankaHesaplari", dbContext.KasaBankaHesaplari.IgnoreQueryFilters().Where(x => _kasaBankaHesapIdler.Contains(x.Id)));
        }
        if (_cariKartIdler.Count > 0)
        {
            await KontrolEt("CariKartlar", dbContext.CariKartlar.IgnoreQueryFilters().Where(x => _cariKartIdler.Contains(x.Id)));
        }
        if (_hesapPlaniIdler.Count > 0)
        {
            await KontrolEt("MuhasebeHesapPlanlari", dbContext.MuhasebeHesapPlanlari.IgnoreQueryFilters().Where(x => _hesapPlaniIdler.Contains(x.Id)));
        }
        if (_tesisIdler.Count > 0)
        {
            await KontrolEt("Tesisler", dbContext.Tesisler.IgnoreQueryFilters().Where(x => _tesisIdler.Contains(x.Id)));
        }
        if (_illIdler.Count > 0)
        {
            await KontrolEt("Iller", dbContext.Iller.IgnoreQueryFilters().Where(x => _illIdler.Contains(x.Id)));
        }
        if (_kurumIdler.Count > 0)
        {
            await KontrolEt("Kurumlar", dbContext.Kurumlar.IgnoreQueryFilters().Where(x => _kurumIdler.Contains(x.Id)));
        }

        return kalanlar;
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return;
        }

        var adimlar = OlusturCleanupAdimlari();
        var hatalar = (await STYS.Tests.TestSupport.TwoPhaseCleanupRunner.CalistirAsync(adimlar)).ToList();

        Dictionary<string, int> kalanlar;
        try
        {
            kalanlar = await DogrulaTemizlikKalintilariAsync();
        }
        catch (Exception ex)
        {
            kalanlar = [];
            hatalar.Add(new InvalidOperationException($"[dogrulama sorgusu basarisiz] {ex.GetType().Name} - {ex.Message}", ex));
        }

        if (kalanlar.Count > 0)
        {
            hatalar.Add(new InvalidOperationException(
                "Cleanup sonrasi kalinti kayit tespit edildi: " +
                string.Join(", ", kalanlar.Select(kv => $"{kv.Key}={kv.Value}"))));
        }

        if (hatalar.Count > 0)
        {
            // Test govdesi ZATEN basarisiz olmus olsa bile bu exception xUnit tarafindan AYRICA
            // raporlanir - DisposeAsync'in kendi hatasi test govdesinin hatasindan BAGIMSIZ olarak
            // gorunur, sessizce Console.Error'a yazip yutulmaz.
            throw new AggregateException(
                $"[NakitBankaPozisyonuServiceTests.DisposeAsync] {hatalar.Count} cleanup hatasi (kalinti veri olusmus olabilir).",
                hatalar);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 1) Tek kasa bakiyesi
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task TekKasaBakiyesi_DogruHesaplanir()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var hesapPlaniId = await YeniHesapPlaniAsync(dbContext, suffix, "KASA");
        await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.NakitKasa, suffix, "KASA1", hesapPlaniId);
        await YeniFisAsync(dbContext, tesisId, DateTime.UtcNow.Date, MuhasebeFisDurumlari.Onayli, (hesapPlaniId, 1000m, 0m), (hesapPlaniId, 0m, 200m));

        var svc = CreateService(dbContext, tesisId);
        var hesaplar = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId });

        Assert.Single(hesaplar.KasaHesaplari);
        Assert.Equal(800m, hesaplar.KasaHesaplari[0].MuhasebeBakiyesi);
    }

    // ─────────────────────────────────────────────────────────────
    // 2) Birden fazla kasa + farkli para birimleri dogrudan toplanmaz
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task BirdenFazlaKasaVeFarkliParaBirimi_AyriToplanir()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var hp1 = await YeniHesapPlaniAsync(dbContext, suffix, "KASATRY");
        var hp2 = await YeniHesapPlaniAsync(dbContext, suffix, "KASAUSD");
        await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.NakitKasa, suffix, "KASA-TRY", hp1, "TRY");
        await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.NakitKasa, suffix, "KASA-USD", hp2, "USD");
        await YeniFisAsync(dbContext, tesisId, DateTime.UtcNow.Date, MuhasebeFisDurumlari.Onayli, (hp1, 500m, 0m));
        await YeniFisAsync(dbContext, tesisId, DateTime.UtcNow.Date, MuhasebeFisDurumlari.Onayli, (hp2, 100m, 0m));

        var svc = CreateService(dbContext, tesisId);
        var sonuc = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId });
        Assert.Equal(2, sonuc.KasaHesaplari.Count);

        // Genel ozet karti YALNIZCA TRY'yi yansitir - USD hesabi buraya KARISTIRILMAZ.
        Assert.Equal(500m, sonuc.Ozet.ToplamNakit);
        var tryOzet = sonuc.Ozet.ParaBirimiOzetleri.Single(x => x.ParaBirimi == "TRY");
        var usdOzet = sonuc.Ozet.ParaBirimiOzetleri.Single(x => x.ParaBirimi == "USD");
        Assert.Equal(500m, tryOzet.ToplamNakit);
        Assert.Equal(100m, usdOzet.ToplamNakit);
    }

    // ─────────────────────────────────────────────────────────────
    // 3) Birden fazla IBAN (banka hesabi)
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task BirdenFazlaIban_HerBiriAyriSatirdaGorunur()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var hp1 = await YeniHesapPlaniAsync(dbContext, suffix, "BANKA1");
        var hp2 = await YeniHesapPlaniAsync(dbContext, suffix, "BANKA2");
        await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix, "BNK1", hp1, iban: "TR000000000000000000000001", bankaAdi: "Test Banka A");
        await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix, "BNK2", hp2, iban: "TR000000000000000000000002", bankaAdi: "Test Banka B");

        var svc = CreateService(dbContext, tesisId);
        var hesaplar = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId });
        Assert.Equal(2, hesaplar.BankaHesaplari.Count);
        Assert.Contains(hesaplar.BankaHesaplari, x => x.Iban == "TR000000000000000000000001");
        Assert.Contains(hesaplar.BankaHesaplari, x => x.Iban == "TR000000000000000000000002");
    }

    // ─────────────────────────────────────────────────────────────
    // 4-8, 18) Valor tarih gruplarinin ayriminmi (gecmis/bugun/yarin/2-7/7+), her kayit TEK bir
    // gruba girer.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task ValorTarihGruplari_HerKayitTekGrubaGirer()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var hpBanka = await YeniHesapPlaniAsync(dbContext, suffix, "BANKA");
        var bankaId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix, "BNK", hpBanka, iban: "TR00", bankaAdi: "Test Banka");
        var hpKk = await YeniHesapPlaniAsync(dbContext, suffix, "KK");
        var krediKartiHesapId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.KrediKarti, suffix, "KK1", hpKk);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);

        var bugun = BugunIstanbul();
        var senaryolar = new (DateOnly Tarih, decimal Net)[]
        {
            (bugun.AddDays(-3), 100m), // gecmis
            (bugun, 200m),             // bugun
            (bugun.AddDays(1), 300m),  // yarin
            (bugun.AddDays(2), 400m),  // takip 2-7
            (bugun.AddDays(7), 500m),  // takip 2-7 (sinir)
            (bugun.AddDays(8), 600m),  // 7 gunden sonra
        };

        foreach (var (tarih, net) in senaryolar)
        {
            var belgeId = await YeniTahsilatBelgesiAsync(dbContext, cariId, krediKartiHesapId, net, YeniSuffix());
            await YeniValorAsync(dbContext, tesisId, belgeId, krediKartiHesapId, bankaId, PosTahsilatValorDurumlari.ValorBekliyor, tarih, net, 0m, net);
        }

        var svc = CreateService(dbContext, tesisId);
        var hesaplar = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId, RaporTarihi = bugun });
        var banka = hesaplar.BankaHesaplari.Single(x => x.KasaBankaHesapId == bankaId);

        Assert.Equal(100m, banka.ValoruGecmisBekleyenNet);
        Assert.Equal(200m, banka.BugunGelecekNet);
        Assert.Equal(300m, banka.YarinGelecekNet);
        Assert.Equal(900m, banka.Takip2_7GunGelecekNet); // 400 + 500
        Assert.Equal(600m, banka.Sonraki7GundenSonraNet);

        // 18) Ayni valor kaydi ASLA iki gruba birden girmez - toplam net, tum gruplarin toplami ile
        // BIREBIR ayni olmali (cifte sayim yok).
        var beklenenToplam = senaryolar.Sum(x => x.Net);
        Assert.Equal(beklenenToplam, banka.ToplamBekleyenNet);
    }

    // ─────────────────────────────────────────────────────────────
    // 9) Aktarilmis (Aktarildi) valor bekleyen tutara EKLENMEZ.
    // 10) Iptal edilmis/ters kayitli valor bekleyen tutara EKLENMEZ.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task AktarilmisVeIptalEdilmisValorler_BekleyenTutaraEklenmez()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var hpBanka = await YeniHesapPlaniAsync(dbContext, suffix, "BANKA");
        var bankaId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix, "BNK", hpBanka, iban: "TR00");
        var hpKk = await YeniHesapPlaniAsync(dbContext, suffix, "KK");
        var krediKartiHesapId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.KrediKarti, suffix, "KK1", hpKk);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = BugunIstanbul();

        var belge1 = await YeniTahsilatBelgesiAsync(dbContext, cariId, krediKartiHesapId, 1000m, YeniSuffix());
        await YeniValorAsync(dbContext, tesisId, belge1, krediKartiHesapId, bankaId, PosTahsilatValorDurumlari.Aktarildi, bugun, 1000m, 20m, 980m);

        var belge2 = await YeniTahsilatBelgesiAsync(dbContext, cariId, krediKartiHesapId, 500m, YeniSuffix());
        await YeniValorAsync(dbContext, tesisId, belge2, krediKartiHesapId, bankaId, PosTahsilatValorDurumlari.Iptal, bugun, 500m, 0m, 500m);

        var belge3 = await YeniTahsilatBelgesiAsync(dbContext, cariId, krediKartiHesapId, 300m, YeniSuffix());
        await YeniValorAsync(dbContext, tesisId, belge3, krediKartiHesapId, bankaId, PosTahsilatValorDurumlari.AktarimFisiIptalEdildi, bugun, 300m, 0m, 300m);

        var svc = CreateService(dbContext, tesisId);
        var hesaplar = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId, RaporTarihi = bugun });
        var banka = hesaplar.BankaHesaplari.Single(x => x.KasaBankaHesapId == bankaId);

        Assert.Equal(0m, banka.ToplamBekleyenNet);
        Assert.Equal(0m, banka.MutabakatBekleyenNet);
        Assert.Equal(0m, banka.HataliNet);
    }

    // ─────────────────────────────────────────────────────────────
    // 11) Mutabakat bekleyen kayit tahmini bakiyeye EKLENMEZ (ayri gosterilir).
    // 12) Hatali kayit tahmini bakiyeye EKLENMEZ (ayri gosterilir).
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task MutabakatBekleyenVeHataliKayitlar_TahminiBakiyeyeEklenmezAyriGosterilir()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var hpBanka = await YeniHesapPlaniAsync(dbContext, suffix, "BANKA");
        var bankaId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix, "BNK", hpBanka, iban: "TR00");
        var hpKk = await YeniHesapPlaniAsync(dbContext, suffix, "KK");
        var krediKartiHesapId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.KrediKarti, suffix, "KK1", hpKk);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = BugunIstanbul();

        await YeniFisAsync(dbContext, tesisId, bugun.ToDateTime(TimeOnly.MinValue), MuhasebeFisDurumlari.Onayli, (hpBanka, 2000m, 0m));

        var belge1 = await YeniTahsilatBelgesiAsync(dbContext, cariId, krediKartiHesapId, 700m, YeniSuffix());
        await YeniValorAsync(dbContext, tesisId, belge1, krediKartiHesapId, bankaId, PosTahsilatValorDurumlari.MutabakatBekliyor, bugun, 700m, 0m, 700m);

        var belge2 = await YeniTahsilatBelgesiAsync(dbContext, cariId, krediKartiHesapId, 400m, YeniSuffix());
        await YeniValorAsync(dbContext, tesisId, belge2, krediKartiHesapId, bankaId, PosTahsilatValorDurumlari.Hata, bugun, 400m, 0m, 400m);

        var svc = CreateService(dbContext, tesisId);
        var hesaplar = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId, RaporTarihi = bugun });
        var banka = hesaplar.BankaHesaplari.Single(x => x.KasaBankaHesapId == bankaId);

        Assert.Equal(700m, banka.MutabakatBekleyenNet);
        Assert.Equal(1, banka.MutabakatBekleyenAdet);
        Assert.Equal(400m, banka.HataliNet);
        Assert.Equal(1, banka.HataliAdet);
        Assert.Equal(0m, banka.ToplamBekleyenNet);
        // Tahmini bakiye yalnizca STYS muhasebe bakiyesinden (2000) gelir - mutabakat/hatali DAHIL DEGIL.
        Assert.Equal(2000m, banka.TahminiBakiye);
    }

    // ─────────────────────────────────────────────────────────────
    // 13) Soft-delete edilmis kayitlarin rapor disi tutulmasi.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task SoftDeleteEdilmisKasaHesabiVeValor_RaporaDahilEdilmez()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "KASA");
        var kasaId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.NakitKasa, suffix, "KASA1", hp);
        await YeniFisAsync(dbContext, tesisId, DateTime.UtcNow.Date, MuhasebeFisDurumlari.Onayli, (hp, 500m, 0m));

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"UPDATE [muhasebe].[KasaBankaHesaplari] SET [IsDeleted] = 1 WHERE [Id] = {kasaId}");

        var svc = CreateService(dbContext, tesisId);
        var hesaplar = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId });

        Assert.Empty(hesaplar.KasaHesaplari);
    }

    // ─────────────────────────────────────────────────────────────
    // 14) Farkli tesis verilerinin karismamasi.
    // 15) Yetkisiz kurum/tesis verisinin dönmemesi (403).
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task FarkliTesisVerileriKarismaz_VeYetkisizTesisReddedilir()
    {
        var suffixA = YeniSuffix();
        var suffixB = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffixA);
        var tesisB = await YeniTesisAsync(dbContext, suffixB);

        var hpA = await YeniHesapPlaniAsync(dbContext, suffixA, "KASA");
        var hpB = await YeniHesapPlaniAsync(dbContext, suffixB, "KASA");
        await YeniKasaBankaHesabiAsync(dbContext, tesisA, KasaBankaHesapTipleri.NakitKasa, suffixA, "KASA1", hpA);
        await YeniKasaBankaHesabiAsync(dbContext, tesisB, KasaBankaHesapTipleri.NakitKasa, suffixB, "KASA1", hpB);
        await YeniFisAsync(dbContext, tesisA, DateTime.UtcNow.Date, MuhasebeFisDurumlari.Onayli, (hpA, 111m, 0m));
        await YeniFisAsync(dbContext, tesisB, DateTime.UtcNow.Date, MuhasebeFisDurumlari.Onayli, (hpB, 222m, 0m));

        // Yalnizca TesisA'ya erisimi olan bir kullaniciyi simule et.
        var svc = CreateService(dbContext, tesisA);

        var hesaplarA = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisA });
        Assert.Single(hesaplarA.KasaHesaplari);
        Assert.Equal(111m, hesaplarA.KasaHesaplari[0].MuhasebeBakiyesi);

        // TesisId verilmeden (scope'a gore) sorgulandiginda da yalnizca TesisA verisi donmeli.
        var hesaplarScope = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto());
        Assert.Single(hesaplarScope.KasaHesaplari);

        // TesisB'ye ERISIM YETKISI OLMADAN dogrudan istenirse 403 donmeli.
        var ex = await Assert.ThrowsAsync<BaseException>(() => svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisB }));
        Assert.Equal(403, ex.ErrorCode);
    }

    // ─────────────────────────────────────────────────────────────
    // 17) Negatif banka bakiyesi dogru gosterilir (mutlak deger alinmaz/gizlenmez).
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task NegatifBankaBakiyesi_OldugGibiGosterilir()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "BANKA");
        var bankaId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix, "BNK", hp, iban: "TR00");
        // Alacak > Borc -> negatif net bakiye (banka hesabi "eksi" gorunur).
        await YeniFisAsync(dbContext, tesisId, DateTime.UtcNow.Date, MuhasebeFisDurumlari.Onayli, (hp, 100m, 500m));

        var svc = CreateService(dbContext, tesisId);
        var hesaplar = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId });
        var banka = hesaplar.BankaHesaplari.Single(x => x.KasaBankaHesapId == bankaId);

        Assert.Equal(-400m, banka.StysMuhasebeBakiyesi);
    }

    // ─────────────────────────────────────────────────────────────
    // 19) IBAN ile muhasebe hesabi baglantisi bulunmayan kayit -> uyari.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task IbanliHesapMuhasebeBaglantisiYok_UyariUretilir()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix, "BNK", muhasebeHesapPlaniId: null, iban: "TR00", bankaAdi: "Test Banka");

        var svc = CreateService(dbContext, tesisId);
        var hesaplar = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId });

        Assert.Contains(hesaplar.Uyarilar, u => u.UyariTipi == NakitBankaPozisyonuUyariTipleri.IbanVarMuhasebeHesabiYok);
    }

    // ─────────────────────────────────────────────────────────────
    // 20) Gecmis rapor tarihi bakiyesi - rapor tarihinden SONRAKI fis satirlari HARIC TUTULUR.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task GecmisRaporTarihi_YalnizcaOTariheKadarkiHareketleriKapsar()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "KASA");
        await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.NakitKasa, suffix, "KASA1", hp);

        var eskiTarih = DateTime.UtcNow.Date.AddDays(-10);
        var yeniTarih = DateTime.UtcNow.Date;
        await YeniFisAsync(dbContext, tesisId, eskiTarih, MuhasebeFisDurumlari.Onayli, (hp, 300m, 0m));
        await YeniFisAsync(dbContext, tesisId, yeniTarih, MuhasebeFisDurumlari.Onayli, (hp, 700m, 0m));

        var svc = CreateService(dbContext, tesisId);

        // Rapor tarihi eski tarihte iken YALNIZCA o ana kadarki hareket (300) gorunmeli.
        var gecmisRapor = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId, RaporTarihi = DateOnly.FromDateTime(eskiTarih) });
        Assert.Equal(300m, gecmisRapor.KasaHesaplari.Single().MuhasebeBakiyesi);

        // Rapor tarihi bugun iken HER IKI hareket de (300+700=1000) gorunmeli.
        var bugunRapor = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId, RaporTarihi = DateOnly.FromDateTime(yeniTarih) });
        Assert.Equal(1000m, bugunRapor.KasaHesaplari.Single().MuhasebeBakiyesi);
    }

    // ─────────────────────────────────────────────────────────────
    // 21) GECMIS TARIH KARARI: POS valor tarihcesi (iptal zamani / durum gecis gecmisi) veri
    //     modelinde TUTULMADIGI icin gecmis tarihli raporda POS pozisyonu HIC hesaplanmaz - tum POS
    //     tutarlari finansal toplamlarin disinda birakilir ve durum acikca bildirilir. Muhasebe
    //     bakiyesi ise (gercek FisTarihi'ne dayandigi icin) gecmis tarihte hesaplanmaya DEVAM EDER.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task GecmisTarih_PosPozisyonuHicHesaplanmaz_MuhasebeBakiyesiHesaplanmayaDevamEder()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var hpBanka = await YeniHesapPlaniAsync(dbContext, suffix, "BANKA");
        var bankaId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix, "BNK", hpBanka, iban: "TR00");
        var hpKk = await YeniHesapPlaniAsync(dbContext, suffix, "KK");
        var krediKartiHesapId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.KrediKarti, suffix, "KK1", hpKk);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);

        var raporTarihi = BugunIstanbul().AddDays(-10);

        // Rapor tarihinden ONCE tarihli, gercek bir muhasebe hareketi.
        await YeniFisAsync(dbContext, tesisId, raporTarihi.AddDays(-1).ToDateTime(TimeOnly.MinValue), MuhasebeFisDurumlari.Onayli, (hpBanka, 750m, 0m));

        // Bekleyen bir POS kaydi - gecmis raporda HICBIR toplama girmemeli.
        var belge = await YeniTahsilatBelgesiAsync(dbContext, cariId, krediKartiHesapId, 1000m, YeniSuffix());
        await YeniValorAsync(dbContext, tesisId, belge, krediKartiHesapId, bankaId, PosTahsilatValorDurumlari.ValorBekliyor,
            raporTarihi, 1000m, 20m, 980m);

        var svc = CreateService(dbContext, tesisId);
        var sonuc = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId, RaporTarihi = raporTarihi });
        var banka = sonuc.BankaHesaplari.Single(x => x.KasaBankaHesapId == bankaId);

        Assert.True(sonuc.GecmisTarihRaporuMu);
        Assert.False(sonuc.PosPozisyonuHesaplandiMi);
        Assert.NotNull(sonuc.PosPozisyonuHesaplanmamaNedeni);
        Assert.Contains(sonuc.Uyarilar, u => u.UyariTipi == NakitBankaPozisyonuUyariTipleri.GecmisTarihPosPozisyonuHesaplanmadi);

        // POS'a ait HICBIR tutar uretilmedi.
        Assert.Equal(0m, banka.ToplamBekleyenNet);
        Assert.Equal(0m, banka.MutabakatBekleyenNet);
        Assert.Equal(0m, banka.HataliNet);
        Assert.Equal(0m, sonuc.Ozet.ToplamBekleyenNetPos);

        // Muhasebe bakiyesi ise gercekten hesaplandi ve tahmini bakiye YALNIZCA ona esit.
        Assert.Equal(750m, banka.StysMuhasebeBakiyesi);
        Assert.Equal(750m, banka.TahminiBakiye);
        Assert.Equal(750m, sonuc.Ozet.TahminiToplamBankaPozisyonu);
    }

    // ─────────────────────────────────────────────────────────────
    // 22) Bugunun raporunda POS pozisyonu normal sekilde hesaplanir.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task BugunRaporu_PosPozisyonuHesaplanir()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var hpBanka = await YeniHesapPlaniAsync(dbContext, suffix, "BANKA");
        var bankaId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix, "BNK", hpBanka, iban: "TR00");
        var hpKk = await YeniHesapPlaniAsync(dbContext, suffix, "KK");
        var krediKartiHesapId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.KrediKarti, suffix, "KK1", hpKk);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = BugunIstanbul();

        var belge = await YeniTahsilatBelgesiAsync(dbContext, cariId, krediKartiHesapId, 1000m, YeniSuffix());
        await YeniValorAsync(dbContext, tesisId, belge, krediKartiHesapId, bankaId, PosTahsilatValorDurumlari.ValorBekliyor,
            bugun, 1000m, 20m, 980m);

        var svc = CreateService(dbContext, tesisId);
        var sonuc = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId, RaporTarihi = bugun });
        var banka = sonuc.BankaHesaplari.Single(x => x.KasaBankaHesapId == bankaId);

        Assert.True(sonuc.PosPozisyonuHesaplandiMi);
        Assert.Equal(980m, banka.BugunGelecekNet);
        Assert.Equal(980m, banka.ToplamBekleyenNet);
    }

    // ─────────────────────────────────────────────────────────────
    // 23) Tanınmayan durum + ara durumlar normal bekleyene GIRMEZ (allowlist davranisi).
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task AraDurumlarVeTaninmayanDurum_NormalBekleyeneGIRMEZ()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var hpBanka = await YeniHesapPlaniAsync(dbContext, suffix, "BANKA");
        var bankaId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix, "BNK", hpBanka, iban: "TR00");
        var hpKk = await YeniHesapPlaniAsync(dbContext, suffix, "KK");
        var krediKartiHesapId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.KrediKarti, suffix, "KK1", hpKk);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = BugunIstanbul();

        var b1 = await YeniTahsilatBelgesiAsync(dbContext, cariId, krediKartiHesapId, 100m, YeniSuffix());
        await YeniValorAsync(dbContext, tesisId, b1, krediKartiHesapId, bankaId, PosTahsilatValorDurumlari.Aktariliyor, bugun, 100m, 0m, 100m);
        var b2 = await YeniTahsilatBelgesiAsync(dbContext, cariId, krediKartiHesapId, 200m, YeniSuffix());
        await YeniValorAsync(dbContext, tesisId, b2, krediKartiHesapId, bankaId, PosTahsilatValorDurumlari.TersKayitOlusturuluyor, bugun, 200m, 0m, 200m);

        // Projede TANIMLI OLMAYAN bir durum degeri - dogrudan SQL ile yazilir (guvenli varsayilan testi).
        var b3 = await YeniTahsilatBelgesiAsync(dbContext, cariId, krediKartiHesapId, 400m, YeniSuffix());
        var v3 = await YeniValorAsync(dbContext, tesisId, b3, krediKartiHesapId, bankaId, PosTahsilatValorDurumlari.ValorBekliyor, bugun, 400m, 0m, 400m);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Muhasebe].[PosTahsilatValorleri] SET [Durum] = 'GelecektekiYeniDurum' WHERE [Id] = {v3}");

        var svc = CreateService(dbContext, tesisId);
        var sonuc = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId, RaporTarihi = bugun });
        var banka = sonuc.BankaHesaplari.Single(x => x.KasaBankaHesapId == bankaId);

        Assert.Equal(0m, banka.ToplamBekleyenNet);
        Assert.Contains(sonuc.Uyarilar, u => u.UyariTipi == NakitBankaPozisyonuUyariTipleri.TaninmayanValorDurumu);
        Assert.Contains(banka.UyariliTutarlar, x => x.UyariTipi == NakitBankaPozisyonuUyariTipleri.AktarimSurecindeValor && x.ToplamNetTutar == 300m);
    }

    // ─────────────────────────────────────────────────────────────
    // 24) Pasif muhasebe hesabi normal pozisyona girmez ve sahte tahmini bakiye uretmez.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task PasifMuhasebeHesabi_PozisyonaDahilEdilmez_TahminiBakiyeUretilmez()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var hpBanka = await YeniHesapPlaniAsync(dbContext, suffix, "BANKA");
        var bankaId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix, "BNK", hpBanka, iban: "TR00");
        var hpKk = await YeniHesapPlaniAsync(dbContext, suffix, "KK");
        var krediKartiHesapId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.KrediKarti, suffix, "KK1", hpKk);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = BugunIstanbul();

        await YeniFisAsync(dbContext, tesisId, bugun.ToDateTime(TimeOnly.MinValue), MuhasebeFisDurumlari.Onayli, (hpBanka, 5000m, 0m));
        var belge = await YeniTahsilatBelgesiAsync(dbContext, cariId, krediKartiHesapId, 1000m, YeniSuffix());
        await YeniValorAsync(dbContext, tesisId, belge, krediKartiHesapId, bankaId, PosTahsilatValorDurumlari.ValorBekliyor, bugun, 1000m, 20m, 980m);

        // Bagli muhasebe hesabi PASIF hale getirildi.
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [Muhasebe].[MuhasebeHesapPlanlari] SET [AktifMi] = 0 WHERE [Id] = {hpBanka}");

        var svc = CreateService(dbContext, tesisId);
        var sonuc = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId, RaporTarihi = bugun });
        var banka = sonuc.BankaHesaplari.Single(x => x.KasaBankaHesapId == bankaId);

        Assert.False(banka.MuhasebeBakiyesiGecerliMi);
        Assert.Equal(0m, banka.StysMuhasebeBakiyesi);   // pasif hesabin bakiyesi hesaplanmaz
        Assert.Null(banka.TahminiBakiye);               // SAHTE tahmini bakiye URETILMEZ
        Assert.Equal(0m, banka.ToplamBekleyenNet);      // POS tutari da normal toplama girmez
        Assert.Contains(sonuc.Uyarilar, u => u.UyariTipi == NakitBankaPozisyonuUyariTipleri.PasifBaglantiliMuhasebeHesabi);
        Assert.Equal(0m, sonuc.Ozet.ToplamBankaMuhasebeBakiyesi);
    }

    // ─────────────────────────────────────────────────────────────
    // 25) Ayni hesap planinin FARKLI tesislerde kullanilmasi sahte mukerrerlik uyarisi URETMEZ.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task AyniHesapPlaniFarkliTesislerde_SahteMukerrerlikUyarisiUretmez()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffix + "A");
        var tesisB = await YeniTesisAsync(dbContext, suffix + "B");
        var hpPaylasilan = await YeniHesapPlaniAsync(dbContext, suffix, "PAYLASILAN");

        await YeniKasaBankaHesabiAsync(dbContext, tesisA, KasaBankaHesapTipleri.Banka, suffix + "A", "BNK", hpPaylasilan, iban: "TR01");
        await YeniKasaBankaHesabiAsync(dbContext, tesisB, KasaBankaHesapTipleri.Banka, suffix + "B", "BNK", hpPaylasilan, iban: "TR02");

        var svc = CreateService(dbContext, tesisA, tesisB);
        var sonuc = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto());

        // Farkli TESISLERDE ayni (global) hesap planini kullanmak mukerrerlik DEGILDIR.
        Assert.DoesNotContain(sonuc.Uyarilar,
            u => u.UyariTipi == NakitBankaPozisyonuUyariTipleri.AyniMuhasebeHesabinaBirdenFazlaAktifBankaHesabiBagli);
    }

    // ─────────────────────────────────────────────────────────────
    // 26) AYNI tesiste ayni hesap planina birden fazla banka hesabi baglanmasi UYARI URETIR.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task AyniTesisteAyniHesapPlani_MukerrerlikUyarisiUretir()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var hp = await YeniHesapPlaniAsync(dbContext, suffix, "PAYLASILAN");

        await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix + "1", "BNK", hp, iban: "TR01");
        await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix + "2", "BNK", hp, iban: "TR02");

        var svc = CreateService(dbContext, tesisId);
        var sonuc = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId });

        Assert.Contains(sonuc.Uyarilar,
            u => u.UyariTipi == NakitBankaPozisyonuUyariTipleri.AyniMuhasebeHesabinaBirdenFazlaAktifBankaHesabiBagli);
    }

    // ─────────────────────────────────────────────────────────────
    // 23) Ayni MuhasebeHesapPlaniId farkli iki tesiste kullanildiginda bakiyeler KARISMAZ -
    //     (TesisId, MuhasebeHesapPlaniId) bilesik anahtari dogrulanir.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task AyniHesapPlaniFarkliTesislerde_BakiyelerKarismaz()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisA = await YeniTesisAsync(dbContext, suffix + "A");
        var tesisB = await YeniTesisAsync(dbContext, suffix + "B");

        // Ayni MuhasebeHesapPlani.Id degeri A ve B kasa hesaplarina baglanacak sekilde AYNI hesap
        // planini kullanalim (kod duplikasyonu semantik olarak nadir ama Id BAZLI eslesme gercek
        // bug'i tetikler - composite anahtar OLMADAN GetBakiyelerAsync bu iki tesisin hareketlerini
        // TEK bir anahtarda toplardi).
        var hpPaylasilan = await YeniHesapPlaniAsync(dbContext, suffix, "PAYLASILAN");
        await YeniKasaBankaHesabiAsync(dbContext, tesisA, KasaBankaHesapTipleri.NakitKasa, suffix + "A", "KASA1", hpPaylasilan);
        await YeniKasaBankaHesabiAsync(dbContext, tesisB, KasaBankaHesapTipleri.NakitKasa, suffix + "B", "KASA1", hpPaylasilan);

        await YeniFisAsync(dbContext, tesisA, DateTime.UtcNow.Date, MuhasebeFisDurumlari.Onayli, (hpPaylasilan, 1000m, 0m));
        await YeniFisAsync(dbContext, tesisB, DateTime.UtcNow.Date, MuhasebeFisDurumlari.Onayli, (hpPaylasilan, 50m, 0m));

        var svc = CreateService(dbContext, tesisA, tesisB);

        var sonucA = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisA });
        var sonucB = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisB });

        // Composite anahtar OLMADAN her iki tesis de 1050 (1000+50) gorurdu - bug budur.
        Assert.Equal(1000m, sonucA.KasaHesaplari.Single().MuhasebeBakiyesi);
        Assert.Equal(50m, sonucB.KasaHesaplari.Single().MuhasebeBakiyesi);
    }

    // ─────────────────────────────────────────────────────────────
    // 24) ValorDurumu filtresi genel ozet/hesap toplamlarini ETKILEMEZ - yalnizca detay
    //     sorgularini etkilemesi gerekir (bkz. DTO doc).
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task ValorDurumuFiltresi_OzetToplamlariniEtkilemez()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var hpBanka = await YeniHesapPlaniAsync(dbContext, suffix, "BANKA");
        var bankaId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix, "BNK", hpBanka, iban: "TR00");
        var hpKk = await YeniHesapPlaniAsync(dbContext, suffix, "KK");
        var krediKartiHesapId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.KrediKarti, suffix, "KK1", hpKk);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = BugunIstanbul();

        var belge1 = await YeniTahsilatBelgesiAsync(dbContext, cariId, krediKartiHesapId, 700m, YeniSuffix());
        await YeniValorAsync(dbContext, tesisId, belge1, krediKartiHesapId, bankaId, PosTahsilatValorDurumlari.ValorBekliyor, bugun, 700m, 0m, 700m);
        var belge2 = await YeniTahsilatBelgesiAsync(dbContext, cariId, krediKartiHesapId, 400m, YeniSuffix());
        await YeniValorAsync(dbContext, tesisId, belge2, krediKartiHesapId, bankaId, PosTahsilatValorDurumlari.Hata, bugun, 400m, 0m, 400m);

        var svc = CreateService(dbContext, tesisId);

        var filtresiz = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId, RaporTarihi = bugun });
        var filtreliHata = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId, RaporTarihi = bugun, ValorDurumu = PosTahsilatValorDurumlari.Hata });

        var bankaFiltresiz = filtresiz.BankaHesaplari.Single(x => x.KasaBankaHesapId == bankaId);
        var bankaFiltreli = filtreliHata.BankaHesaplari.Single(x => x.KasaBankaHesapId == bankaId);

        // ValorDurumu=Hata verilse BILE ozet/hesap toplamlari AYNI kalmali (yalnizca detay etkilenir).
        Assert.Equal(bankaFiltresiz.ToplamBekleyenNet, bankaFiltreli.ToplamBekleyenNet);
        Assert.Equal(bankaFiltresiz.HataliNet, bankaFiltreli.HataliNet);
        Assert.Equal(filtresiz.Ozet.ToplamBekleyenNetPos, filtreliHata.Ozet.ToplamBekleyenNetPos);
    }

    // ─────────────────────────────────────────────────────────────
    // 25) Gelecek bir rapor tarihi REDDEDILIR (400).
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task GelecekRaporTarihi_Reddedilir()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var svc = CreateService(dbContext, tesisId);

        var gelecekTarih = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(5));
        var ex = await Assert.ThrowsAsync<BaseException>(() => svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId, RaporTarihi = gelecekTarih }));
        Assert.Equal(400, ex.ErrorCode);
    }

    // ─────────────────────────────────────────────────────────────
    // 26) Banka hesabi bulunamayan/pasif BagliBankaHesapId -> uyari, sessizce kaybolmaz.
    // FK kisiti gercek bir "hic var olmamis Id" senaryosunu engelledigi icin (DB seviyesinde
    // referans butunlugu zaten korunuyor), bu durum gercekte yalnizca hesap SONRADAN soft-delete
    // edildiginde ortaya cikabilir - test bunu simule eder.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task BagliBankaHesabiBulunamayanValor_UyariUretilirVeBekleyeneGirmez()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var hpBanka = await YeniHesapPlaniAsync(dbContext, suffix, "BANKA");
        var bankaId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix, "BNK", hpBanka, iban: "TR00");
        var hpKk = await YeniHesapPlaniAsync(dbContext, suffix, "KK");
        var krediKartiHesapId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.KrediKarti, suffix, "KK1", hpKk);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = BugunIstanbul();

        var belge = await YeniTahsilatBelgesiAsync(dbContext, cariId, krediKartiHesapId, 250m, YeniSuffix());
        await YeniValorAsync(dbContext, tesisId, belge, krediKartiHesapId, bankaId, PosTahsilatValorDurumlari.ValorBekliyor, bugun, 250m, 0m, 250m);

        // Banka hesabi SONRADAN soft-delete edildi (valor kaydi olusturulduktan sonra).
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Muhasebe].[KasaBankaHesaplari] SET [IsDeleted] = 1 WHERE [Id] = {bankaId}");

        var svc = CreateService(dbContext, tesisId);
        var sonuc = await svc.GetPozisyonAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId, RaporTarihi = bugun });

        Assert.Contains(sonuc.Uyarilar, u => u.UyariTipi == NakitBankaPozisyonuUyariTipleri.BankaHesabiBulunamadiVeyaPasif);
        Assert.DoesNotContain(sonuc.BankaHesaplari, x => x.ToplamBekleyenNet == 250m);
    }

    // ─────────────────────────────────────────────────────────────
    // 27) Gun detaylari sayfalamasi - toplam sayi ve sayfa sinirlari dogru calisir.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task ValorGunDetaylari_SayfalamaDogruCalisir()
    {
        var suffix = YeniSuffix();
        await using var dbContext = CreateDbContext();
        var tesisId = await YeniTesisAsync(dbContext, suffix);
        var hpBanka = await YeniHesapPlaniAsync(dbContext, suffix, "BANKA");
        var bankaId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.Banka, suffix, "BNK", hpBanka, iban: "TR00");
        var hpKk = await YeniHesapPlaniAsync(dbContext, suffix, "KK");
        var krediKartiHesapId = await YeniKasaBankaHesabiAsync(dbContext, tesisId, KasaBankaHesapTipleri.KrediKarti, suffix, "KK1", hpKk);
        var cariId = await YeniCariKartAsync(dbContext, tesisId, suffix);
        var bugun = BugunIstanbul();

        for (var i = 0; i < 7; i++)
        {
            var belge = await YeniTahsilatBelgesiAsync(dbContext, cariId, krediKartiHesapId, 10m, YeniSuffix());
            await YeniValorAsync(dbContext, tesisId, belge, krediKartiHesapId, bankaId, PosTahsilatValorDurumlari.ValorBekliyor, bugun, 10m, 0m, 10m);
        }

        var svc = CreateService(dbContext, tesisId);
        var sayfa1 = await svc.GetValorGunDetaylariAsync(bankaId, bugun, null, sayfa: 1, sayfaBoyutu: 3);
        var sayfa2 = await svc.GetValorGunDetaylariAsync(bankaId, bugun, null, sayfa: 2, sayfaBoyutu: 3);
        var sayfa3 = await svc.GetValorGunDetaylariAsync(bankaId, bugun, null, sayfa: 3, sayfaBoyutu: 3);

        Assert.Equal(7, sayfa1.TotalCount);
        Assert.Equal(3, sayfa1.Items.Count);
        Assert.Equal(3, sayfa2.Items.Count);
        Assert.Single(sayfa3.Items);
        // Sayfalar arasinda mukerrer/eksik kayit olmadigini dogrula (kararli siralama).
        var tumIdler = sayfa1.Items.Select(x => x.Id).Concat(sayfa2.Items.Select(x => x.Id)).Concat(sayfa3.Items.Select(x => x.Id)).ToList();
        Assert.Equal(7, tumIdler.Distinct().Count());
    }

    // ─────────────────────────────────────────────────────────────
    // Fake'ler
    // ─────────────────────────────────────────────────────────────

    private sealed class FakeCurrentUserAccessor : TOD.Platform.Security.Auth.Services.ICurrentUserAccessor
    {
        public string? GetCurrentUserName() => "nakit-banka-pozisyonu-test";
        public Guid? GetCurrentUserId() => Guid.NewGuid();
    }

    private sealed class FakeCurrentTenantAccessor : TOD.Platform.Security.Auth.Services.ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => null;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [];
        public bool IsSuperAdmin() => true;
        public bool IsKurumAdmin() => false;
    }

    /// <summary>Yalnizca constructor'da verilen tesis id'lerine erisime izin verir - baska bir
    /// tesis icin EnsureCanAccessTesisAsync 403 firlatir (gercek IUserAccessScopeService.IsScoped
    /// davranisiyla ayni sonucu, test icin cok daha basit bir sekilde saglar).</summary>
    private sealed class FakeMuhasebeTesisScopeService : IMuhasebeTesisScopeService
    {
        private readonly HashSet<int> _erisilebilirTesisIds;

        public FakeMuhasebeTesisScopeService(IEnumerable<int> erisilebilirTesisIds)
        {
            _erisilebilirTesisIds = erisilebilirTesisIds.ToHashSet();
        }

        public Task<int[]> GetEffectiveTesisIdsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_erisilebilirTesisIds.ToArray());

        public Task<int[]> GetEffectiveTesisIdsAsync(DomainAccessScope scope, CancellationToken cancellationToken = default) =>
            Task.FromResult(_erisilebilirTesisIds.ToArray());

        public Task EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken = default)
        {
            if (!_erisilebilirTesisIds.Contains(tesisId))
            {
                throw new BaseException("Bu tesis için yetkiniz bulunmuyor.", 403);
            }
            return Task.CompletedTask;
        }
    }
}
