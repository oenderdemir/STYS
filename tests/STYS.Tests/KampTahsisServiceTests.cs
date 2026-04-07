using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Kamp;
using STYS.Kamp.Dto;
using STYS.Kamp.Entities;
using STYS.Kamp.Services;
using STYS.Iller.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class KampTahsisServiceTests
{
    [Fact]
    public async Task OtomatikKararUygula_KontenjanaGoreEnYuksekSiradakileriTahsisEder()
    {
        await using var dbContext = CreateDbContext();
        await SeedFixtureAsync(dbContext);

        dbContext.KampBasvurulari.AddRange(
            new KampBasvuru
            {
                Id = 100,
                KampDonemiId = 10,
                TesisId = 1,
                KampBasvuruSahibiId = 100,
                KonaklamaBirimiTipi = "TestBirim",
                BasvuruSahibiAdiSoyadiSnapshot = "Birinci Aday",
                BasvuruSahibiTipiSnapshot = "KurumPersoneli",
                Durum = KampBasvuruDurumlari.TahsisEdildi,
                KatilimciSayisi = 4,
                OncelikSirasi = 1,
                Puan = 90,
                DonemToplamTutar = 100m,
                AvansToplamTutar = 10m,
                KalanOdemeTutari = 90m
            },
            new KampBasvuru
            {
                Id = 101,
                KampDonemiId = 10,
                TesisId = 1,
                KampBasvuruSahibiId = 101,
                KonaklamaBirimiTipi = "TestBirim",
                BasvuruSahibiAdiSoyadiSnapshot = "Ikinci Aday",
                BasvuruSahibiTipiSnapshot = "BagliKurulusPersoneli",
                Durum = KampBasvuruDurumlari.Beklemede,
                KatilimciSayisi = 4,
                OncelikSirasi = 2,
                Puan = 80,
                DonemToplamTutar = 100m,
                AvansToplamTutar = 10m,
                KalanOdemeTutari = 90m
            },
            new KampBasvuru
            {
                Id = 102,
                KampDonemiId = 10,
                TesisId = 1,
                KampBasvuruSahibiId = 102,
                KonaklamaBirimiTipi = "TestBirim",
                BasvuruSahibiAdiSoyadiSnapshot = "Ucuncu Aday",
                BasvuruSahibiTipiSnapshot = "Diger",
                Durum = KampBasvuruDurumlari.TahsisEdildi,
                KatilimciSayisi = 4,
                OncelikSirasi = 4,
                Puan = 20,
                DonemToplamTutar = 100m,
                AvansToplamTutar = 10m,
                KalanOdemeTutari = 90m
            },
            new KampBasvuru
            {
                Id = 103,
                KampDonemiId = 10,
                TesisId = 1,
                KampBasvuruSahibiId = 103,
                KonaklamaBirimiTipi = "TestBirim",
                BasvuruSahibiAdiSoyadiSnapshot = "Iptal Kayit",
                BasvuruSahibiTipiSnapshot = "Diger",
                Durum = KampBasvuruDurumlari.IptalEdildi,
                KatilimciSayisi = 4,
                OncelikSirasi = 1,
                Puan = 100,
                DonemToplamTutar = 100m,
                AvansToplamTutar = 10m,
                KalanOdemeTutari = 90m
            });

        await dbContext.SaveChangesAsync();

        var fakeParams = new FakeKampParametreService();
        var service = new KampTahsisService(dbContext, fakeParams);
        var result = await service.OtomatikKararUygulaAsync(new KampTahsisOtomatikKararRequestDto
        {
            KampDonemiId = 10,
            TesisId = 1
        });

        Assert.Equal(2, result.ToplamKontenjan);
        Assert.Equal(3, result.DegerlendirilenBasvuruSayisi);
        Assert.Equal(2, result.TahsisEdilenSayisi);
        Assert.Equal(1, result.TahsisEdilemeyenSayisi);
        Assert.Equal(2, result.GuncellenenKayitSayisi);

        var durumlar = await dbContext.KampBasvurulari
            .IgnoreQueryFilters()
            .Where(x => x.KampDonemiId == 10 && x.TesisId == 1)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Durum })
            .ToListAsync();

        Assert.Equal(KampBasvuruDurumlari.TahsisEdildi, durumlar.Single(x => x.Id == 100).Durum);
        Assert.Equal(KampBasvuruDurumlari.TahsisEdildi, durumlar.Single(x => x.Id == 101).Durum);
        Assert.Equal(KampBasvuruDurumlari.TahsisEdilemedi, durumlar.Single(x => x.Id == 102).Durum);
        Assert.Equal(KampBasvuruDurumlari.IptalEdildi, durumlar.Single(x => x.Id == 103).Durum);
    }

    [Fact]
    public async Task OtomatikKararUygula_AtamaYoksaHataVerir()
    {
        await using var dbContext = CreateDbContext();
        await SeedFixtureAsync(dbContext, atamaEkle: false);
        var fakeParams = new FakeKampParametreService();
        var service = new KampTahsisService(dbContext, fakeParams);

        var exception = await Assert.ThrowsAsync<BaseException>(() => service.OtomatikKararUygulaAsync(new KampTahsisOtomatikKararRequestDto
        {
            KampDonemiId = 10,
            TesisId = 1
        }));

        Assert.Equal(404, exception.ErrorCode);
    }

    private static StysAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new StysAppDbContext(options);
    }

    private static async Task SeedFixtureAsync(StysAppDbContext dbContext, bool atamaEkle = true)
    {
        dbContext.Iller.Add(new Il
        {
            Id = 1,
            Ad = "Mersin",
            AktifMi = true
        });

        dbContext.Tesisler.Add(new Tesis
        {
            Id = 1,
            IlId = 1,
            Ad = "Test Kamp Tesisi",
            Telefon = "000",
            Adres = "Adres",
            AktifMi = true
        });

        dbContext.KampProgramlari.Add(new KampProgrami
        {
            Id = 1,
            Kod = "YAZ",
            Ad = "Yaz Kampi",
            AktifMi = true
        });

        dbContext.KampDonemleri.Add(new KampDonemi
        {
            Id = 10,
            KampProgramiId = 1,
            Kod = "2025-YAZ-1",
            Ad = "2025 Yaz 1",
            Yil = 2025,
            BasvuruBaslangicTarihi = new DateTime(2025, 1, 1),
            BasvuruBitisTarihi = new DateTime(2025, 1, 31),
            KonaklamaBaslangicTarihi = new DateTime(2025, 6, 1),
            KonaklamaBitisTarihi = new DateTime(2025, 6, 6),
            MinimumGece = 5,
            MaksimumGece = 5,
            AktifMi = true
        });

        dbContext.KampBasvuruSahipleri.AddRange(
            new KampBasvuruSahibi { Id = 100, AdSoyad = "Birinci Aday", BasvuruSahibiTipi = "KurumPersoneli", HizmetYili = 0, AktifMi = true },
            new KampBasvuruSahibi { Id = 101, AdSoyad = "Ikinci Aday", BasvuruSahibiTipi = "BagliKurulusPersoneli", HizmetYili = 0, AktifMi = true },
            new KampBasvuruSahibi { Id = 102, AdSoyad = "Ucuncu Aday", BasvuruSahibiTipi = "Diger", HizmetYili = 0, AktifMi = true },
            new KampBasvuruSahibi { Id = 103, AdSoyad = "Iptal Kayit", BasvuruSahibiTipi = "Diger", HizmetYili = 0, AktifMi = true });

        if (atamaEkle)
        {
            dbContext.KampDonemiTesisleri.Add(new KampDonemiTesis
            {
                Id = 20,
                KampDonemiId = 10,
                TesisId = 1,
                AktifMi = true,
                BasvuruyaAcikMi = true,
                ToplamKontenjan = 2
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private sealed class FakeKampParametreService : IKampParametreService
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase)
        {
            [KampParametreKodlari.NoShowSuresiGun] = "2"
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
