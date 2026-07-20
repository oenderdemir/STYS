using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using STYS.Infrastructure.EntityFramework;
using STYS.Kbs.Connectors;
using STYS.Kbs.Constants;
using STYS.Kbs.Entities;
using STYS.Kbs.Options;
using STYS.Kbs.Services;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using TOD.Platform.AspNetCore.Middleware;
using System.Reflection;

namespace STYS.Tests;

public class KbsIntegrationTests
{
    [Fact]
    public async Task AyniFiiliGiris_IkiBildirimOlusturmaz()
    {
        await using var db = CreateDb(1); var guest = await SeedAsync(db, 1); var service = new KbsBildirimOlusturmaService(db);
        var first = await service.FiiliGirisYapAsync(guest.Id); var second = await service.FiiliGirisYapAsync(guest.Id);
        Assert.False(first.ZatenKayitli); Assert.True(second.ZatenKayitli); Assert.Equal(1, await db.KbsBildirimler.CountAsync(x => x.BildirimTipi == KbsBildirimTipleri.Giris));
    }

    [Fact]
    public async Task AyniFiiliCikis_IkiBildirimOlusturmaz_veMaliCheckoutDegismez()
    {
        await using var db = CreateDb(1); var guest = await SeedAsync(db, 1, toplamUcret: 1000); var service = new KbsBildirimOlusturmaService(db);
        await service.FiiliGirisYapAsync(guest.Id); var first = await service.FiiliCikisYapAsync(guest.Id); var second = await service.FiiliCikisYapAsync(guest.Id);
        Assert.False(first.ZatenKayitli); Assert.True(second.ZatenKayitli); Assert.Equal(1, await db.KbsBildirimler.CountAsync(x => x.BildirimTipi == KbsBildirimTipleri.Cikis));
        Assert.Equal(RezervasyonDurumlari.Onayli, (await db.Rezervasyonlar.SingleAsync()).RezervasyonDurumu);
    }

    [Fact]
    public async Task EksikKimlikBilgisi_DogruValidasyonHatasiVerir()
    {
        await using var db = CreateDb(1); var guest = await SeedAsync(db, 1); guest.KimlikNo = null; await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<BaseException>(() => new KbsBildirimOlusturmaService(db).FiiliGirisYapAsync(guest.Id));
        Assert.Contains("kimlik numarasi", ex.Message, StringComparison.OrdinalIgnoreCase); Assert.Empty(db.KbsBildirimler);
    }

    [Theory]
    [InlineData("Basarili", true, null)]
    [InlineData("Timeout", false, KbsHataSiniflari.Transient)]
    [InlineData("YetkiIpHatasi", false, KbsHataSiniflari.Configuration)]
    [InlineData("Belirsiz", false, KbsHataSiniflari.Uncertain)]
    public async Task FakeConnector_SentetikSonuclariUretir(string response, bool success, string? errorClass)
    {
        var connector = new FakeKbsConnector(Options.Create(new KbsOptions { FakeResponse = response }));
        var result = await connector.GirisBildirAsync(new(1, 1, "Test", "Kisi", "10000000146", null, "TR", DateTime.UtcNow), default);
        Assert.Equal(success, result.Basarili); Assert.Equal(errorClass, result.HataSinifi);
    }

    [Fact]
    public async Task Worker_TimeoutRetryYapar_YetkiHatasiniRetryYapmaz_BelirsiziKorur()
    {
        Assert.Equal(KbsBildirimDurumlari.TekrarBekliyor, await ProcessFakeAsync("Timeout"));
        Assert.Equal(KbsBildirimDurumlari.MudahaleGerekli, await ProcessFakeAsync("YetkiIpHatasi"));
        Assert.Equal(KbsBildirimDurumlari.SonucuBelirsiz, await ProcessFakeAsync("Belirsiz"));
    }

    [Fact]
    public async Task Worker_YenidenBasladigindaYarimKalaniBelirsizOlarakGeriAlir()
    {
        await using var db = CreateDb(1); var guest = await SeedAsync(db, 1); var item = NewNotification(guest, KbsBildirimDurumlari.Gonderiliyor); item.GonderimTarihi = DateTime.UtcNow.AddHours(-1); db.KbsBildirimler.Add(item); await db.SaveChangesAsync();
        var worker = CreateWorker(db, "Basarili"); await worker.RecoverAsync(default);
        Assert.Equal(KbsBildirimDurumlari.SonucuBelirsiz, item.Durum);
    }

    [Fact]
    public async Task Tenant_BaskaKurumaAitBildirimiGoremez()
    {
        var database = Guid.NewGuid().ToString(); await using (var admin = CreateDb(null, database, true)) { admin.KbsBildirimler.Add(new KbsBildirim { KurumId = 2, TesisId = 2, RezervasyonId = 2, RezervasyonKonaklayanId = 2, BildirimTipi = "Giris", Saglayici = "Fake", Durum = "Hazir", IdempotencyKey = "x", OlayAnahtari = "x", PayloadHash = "x" }); await admin.SaveChangesAsync(); }
        await using var tenant = CreateDb(1, database); Assert.Empty(await tenant.KbsBildirimler.ToListAsync());
    }

    [Fact]
    public void HassasVeri_LogVeExcelKorumasindanGecer()
    {
        Assert.DoesNotContain("10000000146", KbsBildirimWorker.Mask("Kimlik 10000000146 hatali"));
        var maskMethod = typeof(RequestResponseLoggingMiddleware).GetMethod("MaskSensitiveData", BindingFlags.NonPublic | BindingFlags.Static)!;
        var maskedBody = (string?)maskMethod.Invoke(null, ["{\"tcKimlikNo\":\"10000000146\",\"pasaportNo\":\"P123456\",\"soapPayload\":\"raw\"}"]);
        Assert.DoesNotContain("10000000146", maskedBody); Assert.DoesNotContain("P123456", maskedBody); Assert.DoesNotContain("raw", maskedBody);
        Assert.StartsWith("'", KbsYonetimService.SanitizeExcelText("=HYPERLINK(\"https://invalid\")"));
        Assert.StartsWith("'", KbsYonetimService.SanitizeExcelText("+1+1"));
    }

    [Fact]
    public async Task EgmYuklemeOnayi_BildirimiBasariliYapmaz()
    {
        await using var db = CreateDb(1); var guest = await SeedAsync(db, 1); var item = NewNotification(guest, KbsBildirimDurumlari.DosyaUretildi); item.ExcelManifestHash = "manifest"; db.KbsBildirimler.Add(item); await db.SaveChangesAsync();
        var service = new KbsYonetimService(db, new KbsConnectorResolver([]), Options.Create(new KbsOptions())); await service.EgmYuklemeOnaylaAsync(guest.Rezervasyon!.TesisId, "manifest", default);
        Assert.Equal(KbsBildirimDurumlari.YuklemeOnayiBekliyor, item.Durum); Assert.NotEqual(KbsBildirimDurumlari.Basarili, item.Durum);
    }

    [Fact]
    public async Task CanliConnector_DevelopmentOrtamindaCagrilamaz_veAgIstegiYapmaz()
    {
        var connector = new JandarmaKbsConnector(Options.Create(new KbsOptions { LiveConnectorsEnabled = true }), new FakeHostEnvironment("Development"));
        var result = await connector.GirisBildirAsync(new(1, 1, "Sentetik", "Kisi", "10000000146", null, "TR", DateTime.UtcNow), default);
        Assert.False(result.Basarili); Assert.Equal("LIVE-DISABLED", result.Kod);
    }

    private static async Task<string> ProcessFakeAsync(string response)
    {
        await using var db = CreateDb(1); var guest = await SeedAsync(db, 1); var item = NewNotification(guest, KbsBildirimDurumlari.Hazir); db.KbsBildirimler.Add(item); await db.SaveChangesAsync();
        var worker = CreateWorker(db, response); await worker.ProcessBatchAsync(default); return item.Durum;
    }

    private static KbsBildirimWorker CreateWorker(StysAppDbContext db, string response)
    {
        var services = new ServiceCollection(); services.AddSingleton(db); services.AddSingleton<IOptions<KbsOptions>>(Options.Create(new KbsOptions { FakeResponse = response, MaxAttempts = 3, SendingRecoveryMinutes = 1 }));
        services.AddScoped<IKbsConnector, FakeKbsConnector>(); services.AddScoped<IKbsConnectorResolver, KbsConnectorResolver>(); var provider = services.BuildServiceProvider();
        return new KbsBildirimWorker(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new KbsOptions { MaxAttempts = 3, SendingRecoveryMinutes = 1 }), NullLogger<KbsBildirimWorker>.Instance);
    }

    private static KbsBildirim NewNotification(RezervasyonKonaklayan guest, string state) => new() { KurumId = 1, TesisId = guest.Rezervasyon!.TesisId, RezervasyonId = guest.RezervasyonId, RezervasyonKonaklayanId = guest.Id, BildirimTipi = KbsBildirimTipleri.Giris, Saglayici = KbsEntegrasyonTipleri.Fake, Durum = state, IdempotencyKey = Guid.NewGuid().ToString("N"), OlayAnahtari = Guid.NewGuid().ToString("N"), PayloadHash = "hash", SonrakiDenemeTarihi = DateTime.UtcNow.AddMinutes(-1) };

    private static async Task<RezervasyonKonaklayan> SeedAsync(StysAppDbContext db, int kurumId, decimal toplamUcret = 0)
    {
        var tesis = new Tesis { KurumId = kurumId, Ad = "Sentetik Tesis", Telefon = "000", Adres = "Test", IlId = 1 }; db.Tesisler.Add(tesis); await db.SaveChangesAsync();
        var reservation = new Rezervasyon { TesisId = tesis.Id, Tesis = tesis, ReferansNo = Guid.NewGuid().ToString("N"), KisiSayisi = 1, GirisTarihi = DateTime.UtcNow, CikisTarihi = DateTime.UtcNow.AddDays(1), ToplamUcret = toplamUcret, MisafirAdiSoyadi = "Sentetik Kisi", MisafirTelefon = "000", RezervasyonDurumu = RezervasyonDurumlari.Onayli };
        var guest = new RezervasyonKonaklayan { Rezervasyon = reservation, RezervasyonId = reservation.Id, SiraNo = 1, AdSoyad = "Sentetik Kisi", Ad = "Sentetik", Soyad = "Kisi", KimlikTuru = KbsKimlikTurleri.Tckn, KimlikNo = "10000000146" };
        db.Rezervasyonlar.Add(reservation); db.RezervasyonKonaklayanlar.Add(guest); db.KbsTesisAyarlari.Add(new KbsTesisAyari { KurumId = kurumId, TesisId = tesis.Id, KollukSistemi = KbsKollukSistemleri.Egm, EntegrasyonTipi = KbsEntegrasyonTipleri.Fake, AktifMi = true }); await db.SaveChangesAsync(); return guest;
    }

    private static StysAppDbContext CreateDb(int? kurumId, string? name = null, bool superAdmin = false)
    {
        var opts = new DbContextOptionsBuilder<StysAppDbContext>().UseInMemoryDatabase(name ?? Guid.NewGuid().ToString()).Options;
        return new StysAppDbContext(opts, null, new TenantAccessor(kurumId, superAdmin));
    }

    private sealed class TenantAccessor(int? kurumId, bool superAdmin) : ICurrentTenantAccessor { public int? GetCurrentKurumId() => kurumId; public IReadOnlyList<int> GetAccessibleKurumIds() => kurumId.HasValue ? [kurumId.Value] : []; public bool IsSuperAdmin() => superAdmin; public bool IsKurumAdmin() => false; }
    private sealed class FakeHostEnvironment(string name) : IHostEnvironment { public string EnvironmentName { get; set; } = name; public string ApplicationName { get; set; } = "Tests"; public string ContentRootPath { get; set; } = ""; public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider(); }
}
