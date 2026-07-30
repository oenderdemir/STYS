using System.Reflection;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Kdv.Entities;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Mapping;
using STYS.Muhasebe.SatisBelgeleri.Repositories;
using STYS.Muhasebe.SatisBelgeleri.Services;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// SatisBelgesiService içindeki KDV istisna tanımı doğrulamasının (ValidateKdvIstisnaTanimAsync),
/// belgenin satış/alış yönüne (SatisBelgesi.BelgeTipi üzerinden, IsSatisBelgesi()/IsAlisBelgesi()
/// ile) göre doğru alanı (SatisIslemlerindeKullanilirMi / AlisIslemlerindeKullanilirMi) kontrol
/// ettiğini doğrulayan testler.
///
/// Testler, private ValidateSatirRequestAsync / ValidateBelgeOnayaGonderilebilir instance
/// metotlarını reflection ile GERÇEK bir SatisBelgesiService örneği üzerinde çağırır; istisna
/// tanımı gerçek (InMemory) DbContext'ten okunur - sahte/mock bir yön kontrolü değil, üretim
/// kodundaki asıl doğrulama akışı çalıştırılır.
/// </summary>
public class SatisBelgesiKdvIstisnaYonuTests
{
    // ─────────────────────────────────────────────────────────────
    // ValidateSatirRequestAsync (satır ekleme/güncelleme validasyonu)
    // üzerinden yön kontrolü senaryoları
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SatisFaturasi_YalnizcaSatistaKullanilabilenIstisna_Kabul()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var tanim = await SeedIstisnaTanimAsync(dbContext, satis: true, alis: false);
        var service = CreateService(dbContext);
        var belge = BuildBelge(SatisBelgesiTipi.SatisFaturasi);

        await InvokeValidateSatirRequestAsync(service, BuildSatirRequest(tanim.Id), belge);
        // Exception firlatilmadan tamamlanmasi = kabul edildi
    }

    [Fact]
    public async Task AlisFaturasi_YalnizcaSatistaKullanilabilenIstisna_Reddedilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var tanim = await SeedIstisnaTanimAsync(dbContext, satis: true, alis: false);
        var service = CreateService(dbContext);
        var belge = BuildBelge(SatisBelgesiTipi.AlisFaturasi);

        var ex = await Assert.ThrowsAsync<BaseException>(
            () => InvokeValidateSatirRequestAsync(service, BuildSatirRequest(tanim.Id), belge));

        Assert.Contains("KDV istisna tanımı alış işlemlerinde kullanılamaz", ex.Message);
        Assert.Contains($"{tanim.Kod} — {tanim.Ad}", ex.Message);
    }

    [Fact]
    public async Task AlisFaturasi_YalnizcaAlistaKullanilabilenIstisna_Kabul()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var tanim = await SeedIstisnaTanimAsync(dbContext, satis: false, alis: true);
        var service = CreateService(dbContext);
        var belge = BuildBelge(SatisBelgesiTipi.AlisFaturasi);

        await InvokeValidateSatirRequestAsync(service, BuildSatirRequest(tanim.Id), belge);
    }

    [Fact]
    public async Task SatisFaturasi_YalnizcaAlistaKullanilabilenIstisna_Reddedilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var tanim = await SeedIstisnaTanimAsync(dbContext, satis: false, alis: true);
        var service = CreateService(dbContext);
        var belge = BuildBelge(SatisBelgesiTipi.SatisFaturasi);

        var ex = await Assert.ThrowsAsync<BaseException>(
            () => InvokeValidateSatirRequestAsync(service, BuildSatirRequest(tanim.Id), belge));

        Assert.Contains("KDV istisna tanımı satış işlemlerinde kullanılamaz", ex.Message);
        Assert.Contains($"{tanim.Kod} — {tanim.Ad}", ex.Message);
    }

    [Fact]
    public async Task HemSatisHemAlistaKullanilabilenIstisna_HerIkiYondeDeKabul()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var tanim = await SeedIstisnaTanimAsync(dbContext, satis: true, alis: true);
        var service = CreateService(dbContext);

        await InvokeValidateSatirRequestAsync(service, BuildSatirRequest(tanim.Id), BuildBelge(SatisBelgesiTipi.SatisFaturasi));
        await InvokeValidateSatirRequestAsync(service, BuildSatirRequest(tanim.Id), BuildBelge(SatisBelgesiTipi.AlisFaturasi));
    }

    [Fact]
    public async Task AlisIadeFaturasi_AlisYonuOlarakDegerlendirilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var alisTanimi = await SeedIstisnaTanimAsync(dbContext, satis: false, alis: true);
        var satisTanimi = await SeedIstisnaTanimAsync(dbContext, satis: true, alis: false);
        var service = CreateService(dbContext);
        var belge = BuildBelge(SatisBelgesiTipi.AlisIadeFaturasi);

        await InvokeValidateSatirRequestAsync(service, BuildSatirRequest(alisTanimi.Id), belge);

        var ex = await Assert.ThrowsAsync<BaseException>(
            () => InvokeValidateSatirRequestAsync(service, BuildSatirRequest(satisTanimi.Id), belge));
        Assert.Contains("alış işlemlerinde kullanılamaz", ex.Message);
    }

    [Fact]
    public async Task SatisIadeFaturasi_SatisYonuOlarakDegerlendirilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var satisTanimi = await SeedIstisnaTanimAsync(dbContext, satis: true, alis: false);
        var alisTanimi = await SeedIstisnaTanimAsync(dbContext, satis: false, alis: true);
        var service = CreateService(dbContext);
        var belge = BuildBelge(SatisBelgesiTipi.SatisIadeFaturasi);

        await InvokeValidateSatirRequestAsync(service, BuildSatirRequest(satisTanimi.Id), belge);

        var ex = await Assert.ThrowsAsync<BaseException>(
            () => InvokeValidateSatirRequestAsync(service, BuildSatirRequest(alisTanimi.Id), belge));
        Assert.Contains("satış işlemlerinde kullanılamaz", ex.Message);
    }

    [Fact]
    public async Task Proforma_MevcutExtensionDavranisinaUygunSekildeSatisYonundeDegerlendirilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var satisTanimi = await SeedIstisnaTanimAsync(dbContext, satis: true, alis: false);
        var alisTanimi = await SeedIstisnaTanimAsync(dbContext, satis: false, alis: true);
        var service = CreateService(dbContext);
        var belge = BuildBelge(SatisBelgesiTipi.Proforma);

        await InvokeValidateSatirRequestAsync(service, BuildSatirRequest(satisTanimi.Id), belge);

        var ex = await Assert.ThrowsAsync<BaseException>(
            () => InvokeValidateSatirRequestAsync(service, BuildSatirRequest(alisTanimi.Id), belge));
        Assert.Contains("satış işlemlerinde kullanılamaz", ex.Message);
    }

    [Fact]
    public async Task PasifIstisnaTanimi_HerIkiYondeDeReddedilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var tanim = await SeedIstisnaTanimAsync(dbContext, satis: true, alis: true, aktifMi: false);
        var service = CreateService(dbContext);

        var exSatis = await Assert.ThrowsAsync<BaseException>(
            () => InvokeValidateSatirRequestAsync(service, BuildSatirRequest(tanim.Id), BuildBelge(SatisBelgesiTipi.SatisFaturasi)));
        Assert.Contains("pasif durumda", exSatis.Message);

        var exAlis = await Assert.ThrowsAsync<BaseException>(
            () => InvokeValidateSatirRequestAsync(service, BuildSatirRequest(tanim.Id), BuildBelge(SatisBelgesiTipi.AlisFaturasi)));
        Assert.Contains("pasif durumda", exAlis.Message);
    }

    [Fact]
    public async Task SoftDeleteEdilmisIstisnaTanimi_Reddedilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var tanim = await SeedIstisnaTanimAsync(dbContext, satis: true, alis: true);
        tanim.IsDeleted = true;
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var ex = await Assert.ThrowsAsync<BaseException>(
            () => InvokeValidateSatirRequestAsync(service, BuildSatirRequest(tanim.Id), BuildBelge(SatisBelgesiTipi.SatisFaturasi)));
        Assert.Contains("bulunamadı", ex.Message);
    }

    [Fact]
    public async Task UygulamaTipiUyusmayanIstisna_Reddedilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var tanim = await SeedIstisnaTanimAsync(dbContext, satis: true, alis: true, uygulamaTipi: KdvUygulamaTipi.TamIstisna);
        var service = CreateService(dbContext);
        var belge = BuildBelge(SatisBelgesiTipi.SatisFaturasi);

        var request = BuildSatirRequest(tanim.Id);
        request.KdvUygulamaTipi = (int)KdvUygulamaTipi.KismiIstisna; // tanimla UYUSMAYAN tip

        var ex = await Assert.ThrowsAsync<BaseException>(
            () => InvokeValidateSatirRequestAsync(service, request, belge));
        Assert.Contains("uygulama tipi", ex.Message);
    }

    [Fact]
    public async Task GecerlilikTarihiDisindaKalanIstisna_Reddedilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var tanim = await SeedIstisnaTanimAsync(
            dbContext, satis: true, alis: true,
            gecerlilikBaslangic: new DateTime(2026, 6, 1),
            gecerlilikBitis: new DateTime(2026, 6, 30));
        var service = CreateService(dbContext);
        // Belge tarihi gecerlilik araligi DISINDA (Temmuz)
        var belge = BuildBelge(SatisBelgesiTipi.SatisFaturasi, belgeTarihi: new DateTime(2026, 7, 15));

        var ex = await Assert.ThrowsAsync<BaseException>(
            () => InvokeValidateSatirRequestAsync(service, BuildSatirRequest(tanim.Id), belge));
        Assert.Contains("geçerlilik süresi", ex.Message);
    }

    // ─────────────────────────────────────────────────────────────
    // ValidateBelgeOnayaGonderilebilir — onaya gönderme / muhasebe
    // onayı öncesi yeniden doğrulamada AYNI yön kontrolünün uygulandığı
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task OnayaGonderilirkenYenidenDogrulama_AlisFaturasindaSatisaOzelIstisna_Reddedilir()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var satisTanimi = await SeedIstisnaTanimAsync(dbContext, satis: true, alis: false);
        var service = CreateService(dbContext);

        var belge = BuildOnayaHazirBelge(SatisBelgesiTipi.AlisFaturasi, satisTanimi.Id);

        var ex = await Assert.ThrowsAsync<BaseException>(
            () => InvokeValidateBelgeOnayaGonderilebilir(service, belge));
        Assert.Contains("KDV istisna tanımı alış işlemlerinde kullanılamaz", ex.Message);
    }

    [Fact]
    public async Task OnayaGonderilirkenYenidenDogrulama_SatisFaturasindaSatisaOzelIstisna_Kabul()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var satisTanimi = await SeedIstisnaTanimAsync(dbContext, satis: true, alis: false);
        var service = CreateService(dbContext);

        var belge = BuildOnayaHazirBelge(SatisBelgesiTipi.SatisFaturasi, satisTanimi.Id);

        await InvokeValidateBelgeOnayaGonderilebilir(service, belge);
    }

    // ─────────────────────────────────────────────────────────────
    // Yardimcilar
    // ─────────────────────────────────────────────────────────────

    private static StysAppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
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

    private static SatisBelgesiService CreateService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var repository = new SatisBelgesiRepository(dbContext, mapper);

        // ValidateSatirRequestAsync / ValidateBelgeOnayaGonderilebilir yalnizca _db'yi
        // kullanir; asagidaki bagimliliklar bu testlerde HIC cagrilmaz (CreateAsync/
        // UpdateAsync gibi ust seviye public metotlar cagrilmiyor), bu yuzden null
        // birakilmalari guvenlidir.
        return new SatisBelgesiService(
            repository,
            dbContext,
            mapper,
            null!,
            null!,
            null!,
            NullLogger<SatisBelgesiService>.Instance,
            null!);
    }

    private static async Task<KdvIstisnaTanim> SeedIstisnaTanimAsync(
        StysAppDbContext dbContext,
        bool satis,
        bool alis,
        bool aktifMi = true,
        KdvUygulamaTipi uygulamaTipi = KdvUygulamaTipi.TamIstisna,
        DateTime? gecerlilikBaslangic = null,
        DateTime? gecerlilikBitis = null)
    {
        var tanim = new KdvIstisnaTanim
        {
            Kod = $"T{Guid.NewGuid():N}"[..10],
            Ad = "Test İstisna Tanımı",
            UygulamaTipi = uygulamaTipi,
            SatisIslemlerindeKullanilirMi = satis,
            AlisIslemlerindeKullanilirMi = alis,
            AktifMi = aktifMi,
            GecerlilikBaslangicTarihi = gecerlilikBaslangic,
            GecerlilikBitisTarihi = gecerlilikBitis
        };

        dbContext.KdvIstisnaTanimlari.Add(tanim);
        await dbContext.SaveChangesAsync();
        return tanim;
    }

    private static SatisBelgesi BuildBelge(SatisBelgesiTipi belgeTipi, DateTime? belgeTarihi = null) => new()
    {
        BelgeNo = "TEST-1",
        BelgeTipi = belgeTipi,
        TesisId = 1,
        BelgeTarihi = belgeTarihi ?? new DateTime(2026, 1, 15)
    };

    private static CreateSatisBelgesiSatiriRequest BuildSatirRequest(int kdvIstisnaTanimId) => new()
    {
        SiraNo = 1,
        Aciklama = "Istisnali satir",
        Miktar = 1,
        BirimFiyat = 500m,
        KdvUygulamaTipi = (int)KdvUygulamaTipi.TamIstisna,
        KdvIstisnaTanimId = kdvIstisnaTanimId
    };

    /// <summary>
    /// ValidateBelgeOnayaGonderilebilir'in KDV-istisna disindaki tum kontrollerini
    /// (matrah/toplam tutarliligi, musteri alanlari vb.) gecerek istisna yon kontrolune
    /// ulasan, aksi halde eksiksiz gecerli bir belge kurar.
    /// </summary>
    private static SatisBelgesi BuildOnayaHazirBelge(SatisBelgesiTipi belgeTipi, int kdvIstisnaTanimId)
    {
        var satir = InvokeCreateSatirFromRequest(BuildSatirRequest(kdvIstisnaTanimId));

        var belge = new SatisBelgesi
        {
            BelgeNo = "TEST-1",
            BelgeTipi = belgeTipi,
            TesisId = 1,
            BelgeTarihi = new DateTime(2026, 1, 15),
            KurumsalMi = false,
            MusteriAdSoyad = "Test Musteri"
        };
        belge.Satirlar.Add(satir);
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

    private static async Task InvokeValidateSatirRequestAsync(
        SatisBelgesiService service, CreateSatisBelgesiSatiriRequest request, SatisBelgesi belge)
    {
        var method = typeof(SatisBelgesiService).GetMethod("ValidateSatirRequestAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("ValidateSatirRequestAsync metodu bulunamadi.");

        var task = (Task)method.Invoke(service, [request, belge, CancellationToken.None])!;
        await task;
    }

    private static async Task InvokeValidateBelgeOnayaGonderilebilir(SatisBelgesiService service, SatisBelgesi belge)
    {
        var method = typeof(SatisBelgesiService).GetMethod("ValidateBelgeOnayaGonderilebilir", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("ValidateBelgeOnayaGonderilebilir metodu bulunamadi.");

        var task = (Task)method.Invoke(service, [belge, CancellationToken.None])!;
        await task;
    }
}
