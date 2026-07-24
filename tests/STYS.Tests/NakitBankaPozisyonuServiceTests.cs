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

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return;
        }

        await using var dbContext = CreateDbContext();
        try
        {
            if (_fisIdler.Count > 0)
            {
                await dbContext.MuhasebeFisSatirlari.Where(x => _fisIdler.Contains(x.MuhasebeFisId)).ExecuteDeleteAsync();
                await dbContext.MuhasebeFisler.IgnoreQueryFilters().Where(x => _fisIdler.Contains(x.Id)).ExecuteDeleteAsync();
            }
            if (_valorIdler.Count > 0)
            {
                await dbContext.PosTahsilatValorleri.IgnoreQueryFilters().Where(x => _valorIdler.Contains(x.Id)).ExecuteDeleteAsync();
            }
            if (_belgeIdler.Count > 0)
            {
                await dbContext.TahsilatOdemeBelgeleri.IgnoreQueryFilters().Where(x => _belgeIdler.Contains(x.Id)).ExecuteDeleteAsync();
            }
            if (_kasaBankaHesapIdler.Count > 0)
            {
                await dbContext.KasaBankaHesaplari.IgnoreQueryFilters().Where(x => _kasaBankaHesapIdler.Contains(x.Id)).ExecuteDeleteAsync();
            }
            if (_cariKartIdler.Count > 0)
            {
                await dbContext.CariKartlar.IgnoreQueryFilters().Where(x => _cariKartIdler.Contains(x.Id)).ExecuteDeleteAsync();
            }
            if (_hesapPlaniIdler.Count > 0)
            {
                await dbContext.MuhasebeHesapPlanlari.IgnoreQueryFilters().Where(x => _hesapPlaniIdler.Contains(x.Id)).ExecuteDeleteAsync();
            }
            if (_tesisIdler.Count > 0)
            {
                await dbContext.Tesisler.IgnoreQueryFilters().Where(x => _tesisIdler.Contains(x.Id)).ExecuteDeleteAsync();
            }
            if (_illIdler.Count > 0)
            {
                await dbContext.Iller.IgnoreQueryFilters().Where(x => _illIdler.Contains(x.Id)).ExecuteDeleteAsync();
            }
            if (_kurumIdler.Count > 0)
            {
                await dbContext.Kurumlar.IgnoreQueryFilters().Where(x => _kurumIdler.Contains(x.Id)).ExecuteDeleteAsync();
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[NakitBankaPozisyonuServiceTests.DisposeAsync] temizlik hatasi: {ex.Message}");
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
        var hesaplar = await svc.GetHesaplarAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId });

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
        var hesaplar = await svc.GetHesaplarAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId });
        Assert.Equal(2, hesaplar.KasaHesaplari.Count);

        var ozet = await svc.GetOzetAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId });
        // Genel ozet karti YALNIZCA TRY'yi yansitir - USD hesabi buraya KARISTIRILMAZ.
        Assert.Equal(500m, ozet.ToplamNakit);
        var tryOzet = ozet.ParaBirimiOzetleri.Single(x => x.ParaBirimi == "TRY");
        var usdOzet = ozet.ParaBirimiOzetleri.Single(x => x.ParaBirimi == "USD");
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
        var hesaplar = await svc.GetHesaplarAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId });
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

        var bugun = DateOnly.FromDateTime(DateTime.UtcNow.Date);
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
        var hesaplar = await svc.GetHesaplarAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId, RaporTarihi = bugun });
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
        var bugun = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var belge1 = await YeniTahsilatBelgesiAsync(dbContext, cariId, krediKartiHesapId, 1000m, YeniSuffix());
        await YeniValorAsync(dbContext, tesisId, belge1, krediKartiHesapId, bankaId, PosTahsilatValorDurumlari.Aktarildi, bugun, 1000m, 20m, 980m);

        var belge2 = await YeniTahsilatBelgesiAsync(dbContext, cariId, krediKartiHesapId, 500m, YeniSuffix());
        await YeniValorAsync(dbContext, tesisId, belge2, krediKartiHesapId, bankaId, PosTahsilatValorDurumlari.Iptal, bugun, 500m, 0m, 500m);

        var belge3 = await YeniTahsilatBelgesiAsync(dbContext, cariId, krediKartiHesapId, 300m, YeniSuffix());
        await YeniValorAsync(dbContext, tesisId, belge3, krediKartiHesapId, bankaId, PosTahsilatValorDurumlari.AktarimFisiIptalEdildi, bugun, 300m, 0m, 300m);

        var svc = CreateService(dbContext, tesisId);
        var hesaplar = await svc.GetHesaplarAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId, RaporTarihi = bugun });
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
        var bugun = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        await YeniFisAsync(dbContext, tesisId, bugun.ToDateTime(TimeOnly.MinValue), MuhasebeFisDurumlari.Onayli, (hpBanka, 2000m, 0m));

        var belge1 = await YeniTahsilatBelgesiAsync(dbContext, cariId, krediKartiHesapId, 700m, YeniSuffix());
        await YeniValorAsync(dbContext, tesisId, belge1, krediKartiHesapId, bankaId, PosTahsilatValorDurumlari.MutabakatBekliyor, bugun, 700m, 0m, 700m);

        var belge2 = await YeniTahsilatBelgesiAsync(dbContext, cariId, krediKartiHesapId, 400m, YeniSuffix());
        await YeniValorAsync(dbContext, tesisId, belge2, krediKartiHesapId, bankaId, PosTahsilatValorDurumlari.Hata, bugun, 400m, 0m, 400m);

        var svc = CreateService(dbContext, tesisId);
        var hesaplar = await svc.GetHesaplarAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId, RaporTarihi = bugun });
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
        var hesaplar = await svc.GetHesaplarAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId });

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

        var hesaplarA = await svc.GetHesaplarAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisA });
        Assert.Single(hesaplarA.KasaHesaplari);
        Assert.Equal(111m, hesaplarA.KasaHesaplari[0].MuhasebeBakiyesi);

        // TesisId verilmeden (scope'a gore) sorgulandiginda da yalnizca TesisA verisi donmeli.
        var hesaplarScope = await svc.GetHesaplarAsync(new NakitBankaPozisyonuFilterDto());
        Assert.Single(hesaplarScope.KasaHesaplari);

        // TesisB'ye ERISIM YETKISI OLMADAN dogrudan istenirse 403 donmeli.
        var ex = await Assert.ThrowsAsync<BaseException>(() => svc.GetHesaplarAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisB }));
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
        var hesaplar = await svc.GetHesaplarAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId });
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
        var hesaplar = await svc.GetHesaplarAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId });

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
        var gecmisRapor = await svc.GetHesaplarAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId, RaporTarihi = DateOnly.FromDateTime(eskiTarih) });
        Assert.Equal(300m, gecmisRapor.KasaHesaplari.Single().MuhasebeBakiyesi);

        // Rapor tarihi bugun iken HER IKI hareket de (300+700=1000) gorunmeli.
        var bugunRapor = await svc.GetHesaplarAsync(new NakitBankaPozisyonuFilterDto { TesisId = tesisId, RaporTarihi = DateOnly.FromDateTime(yeniTarih) });
        Assert.Equal(1000m, bugunRapor.KasaHesaplari.Single().MuhasebeBakiyesi);
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
