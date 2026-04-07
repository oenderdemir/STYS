using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Kamp;
using STYS.Kamp.Dto;
using STYS.Kamp.Entities;
using STYS.Kamp.Services;
using STYS.Tesisler.Entities;

namespace STYS.Tests;

public class KampKurallariTests
{
    [Fact]
    public async Task Puanlama_TarimOrmanPersoneliIcinTalimatPuanlariniUygular()
    {
        await using var dbContext = CreateDbContext();
        var kampProgramiId = await SeedLookupDataAsync(dbContext);

        var service = new KampPuanlamaService(dbContext);
        var onizleme = new KampBasvuruOnizlemeDto();
        var request = new KampBasvuruRequestDto
        {
            BasvuruSahibiTipi = "KurumPersoneli",
            HizmetYili = 12,
            GecmisKatilimYillari = [2023],
            Katilimcilar =
            [
                new KampBasvuruKatilimciDto { AdSoyad = "A", DogumTarihi = new DateTime(1990, 1, 1), BasvuruSahibiMi = true },
                new KampBasvuruKatilimciDto { AdSoyad = "B", DogumTarihi = new DateTime(1992, 1, 1) },
                new KampBasvuruKatilimciDto { AdSoyad = "C", DogumTarihi = new DateTime(2015, 1, 1) }
            ]
        };

        await service.PuanlaAsync(request, onizleme, kampProgramiId, 2025, request.GecmisKatilimYillari);

        Assert.Equal(1, onizleme.OncelikSirasi);
        Assert.Equal(62, onizleme.Puan);
    }

    [Fact]
    public async Task UcretHesaplama_CocukKurallariVeEkHizmetleriUygular()
    {
        await using var dbContext = CreateDbContext();
        await SeedLookupDataAsync(dbContext);

        var service = new KampUcretHesaplamaService(dbContext, new FakeKampParametreService());
        var onizleme = new KampBasvuruOnizlemeDto();
        var donem = new KampDonemi
        {
            KonaklamaBaslangicTarihi = new DateTime(2025, 6, 2),
            KonaklamaBitisTarihi = new DateTime(2025, 6, 6)
        };
        var tesis = new Tesis { Ad = "Test Tesisi" };
        var request = new KampBasvuruRequestDto
        {
            KonaklamaBirimiTipi = "TestBirim",
            KlimaTalepEdildiMi = true,
            Katilimcilar =
            [
                new KampBasvuruKatilimciDto { AdSoyad = "Yetiskin Kamu", KatilimciTipi = "Kamu", DogumTarihi = new DateTime(1985, 1, 1), BasvuruSahibiMi = true },
                new KampBasvuruKatilimciDto { AdSoyad = "Yetiskin Diger", KatilimciTipi = "Diger", DogumTarihi = new DateTime(1988, 1, 1) },
                new KampBasvuruKatilimciDto { AdSoyad = "Cocuk", KatilimciTipi = "Diger", DogumTarihi = new DateTime(2020, 6, 1) },
                new KampBasvuruKatilimciDto { AdSoyad = "Bebek", KatilimciTipi = "Diger", DogumTarihi = new DateTime(2023, 6, 1), YemekTalepEdiyorMu = true }
            ]
        };

        await service.HesaplaAsync(request, donem, tesis, onizleme);

        Assert.Equal(5668.75m, onizleme.GunlukToplamTutar);
        Assert.Equal(28343.75m, onizleme.DonemToplamTutar);
        Assert.Equal(6800m, onizleme.AvansToplamTutar);
        Assert.Equal(21543.75m, onizleme.KalanOdemeTutari);
    }

    [Fact]
    public void IadeHesaplama_BirHaftadanAzKalaGunlukKesintiUygular()
    {
        var fakeParams = new FakeKampParametreService();
        var service = new KampIadeService(fakeParams);
        var result = service.Hesapla(new KampIadeHesaplamaRequestDto
        {
            BasvuruDurumu = KampBasvuruDurumlari.Beklemede,
            KampBaslangicTarihi = new DateTime(2025, 6, 10),
            VazgecmeTarihi = new DateTime(2025, 6, 5),
            AvansTutari = 6800m,
            DonemToplamTutari = 25875m,
            OdenenToplamTutar = 6800m,
            ToplamGunSayisi = 5,
            KullanilmayanGunSayisi = 0,
            MazeretliZorunluAyrilisMi = false
        });

        Assert.True(result.IadeVarMi);
        Assert.Equal(1700m, result.KesintiTutari);
        Assert.Equal(5100m, result.IadeTutari);
        Assert.Equal(KampIadeNedenleri.GecBildirimMazeretsiz, result.Gerekce);
    }

    private static StysAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new StysAppDbContext(options);
    }

    private static async Task<int> SeedLookupDataAsync(StysAppDbContext dbContext)
    {
        var kampProgrami = new KampProgrami
        {
            Kod = "GENEL",
            Ad = "Genel Kamp Programi",
            AktifMi = true
        };
        dbContext.KampProgramlari.Add(kampProgrami);

        dbContext.KampBasvuruSahibiTipleri.Add(new KampBasvuruSahibiTipi
        {
            Kod = "KurumPersoneli",
            Ad = "Kurum Personeli",
            OncelikSirasi = 1,
            TabanPuan = 40,
            HizmetYiliPuaniAktifMi = true,
            EmekliBonusPuani = 0,
            AktifMi = true
        });

        dbContext.KampKatilimciTipleri.AddRange(
            new KampKatilimciTipi { Kod = "Kamu", Ad = "Kamu", KamuTarifesiUygulanirMi = true, AktifMi = true },
            new KampKatilimciTipi { Kod = "Diger", Ad = "Diger", KamuTarifesiUygulanirMi = false, AktifMi = true });

        dbContext.KampKuralSetleri.Add(new KampKuralSeti
        {
            KampProgrami = kampProgrami,
            KampYili = 2025,
            OncekiYilSayisi = 2,
            KatilimCezaPuani = 20,
            KatilimciBasinaPuan = 10,
            AktifMi = true
        });

        var basvuruSahibiTipi = dbContext.KampBasvuruSahibiTipleri.Local.First();
        dbContext.KampProgramiBasvuruSahibiTipKurallari.Add(new KampProgramiBasvuruSahibiTipKurali
        {
            KampProgrami = kampProgrami,
            KampBasvuruSahibiTipi = basvuruSahibiTipi,
            OncelikSirasi = basvuruSahibiTipi.OncelikSirasi,
            TabanPuan = basvuruSahibiTipi.TabanPuan,
            HizmetYiliPuaniAktifMi = basvuruSahibiTipi.HizmetYiliPuaniAktifMi,
            EmekliBonusPuani = basvuruSahibiTipi.EmekliBonusPuani,
            VarsayilanKatilimciTipiKodu = "Kamu",
            AktifMi = true
        });

        await dbContext.SaveChangesAsync();
        return kampProgrami.Id;
    }

    private sealed class FakeKampParametreService : IKampParametreService
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase)
        {
            [KampKonaklamaBirimiTipleri.BuildParametreKodu("TestBirim", KampKonaklamaBirimiTipleri.AlanMinKisi)] = "4",
            [KampKonaklamaBirimiTipleri.BuildParametreKodu("TestBirim", KampKonaklamaBirimiTipleri.AlanMaksKisi)] = "5",
            [KampKonaklamaBirimiTipleri.BuildParametreKodu("TestBirim", KampKonaklamaBirimiTipleri.AlanKamuGunluk)] = "1550",
            [KampKonaklamaBirimiTipleri.BuildParametreKodu("TestBirim", KampKonaklamaBirimiTipleri.AlanDigerGunluk)] = "2325",
            [KampKonaklamaBirimiTipleri.BuildParametreKodu("TestBirim", KampKonaklamaBirimiTipleri.AlanBuzdolabiGunluk)] = "40",
            [KampKonaklamaBirimiTipleri.BuildParametreKodu("TestBirim", KampKonaklamaBirimiTipleri.AlanTelevizyonGunluk)] = "40",
            [KampKonaklamaBirimiTipleri.BuildParametreKodu("TestBirim", KampKonaklamaBirimiTipleri.AlanKlimaGunluk)] = "50"
        };

        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public decimal GetDecimal(string kod, decimal defaultValue = 0m)
            => _values.TryGetValue(kod, out var value) && decimal.TryParse(value, out var parsed) ? parsed : defaultValue;

        public int GetInt(string kod, int defaultValue = 0)
            => _values.TryGetValue(kod, out var value) && int.TryParse(value, out var parsed) ? parsed : defaultValue;

        public DateTime GetDate(string kod, DateTime defaultValue = default) => defaultValue;

        public string? GetString(string kod, string? defaultValue = null)
            => _values.TryGetValue(kod, out var value) ? value : defaultValue;

        public IReadOnlyDictionary<string, string> GetByPrefix(string prefix)
            => _values
                .Where(x => x.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    }
}
