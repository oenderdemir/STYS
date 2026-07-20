using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using STYS.Infrastructure.EntityFramework;
using STYS.Kbs.Connectors;
using STYS.Kbs.Constants;
using STYS.Kbs.Entities;
using STYS.Kbs.Dtos;
using STYS.Kbs.Options;
using STYS.Kbs.Payload;
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
    [Trait("Category", "SqlServerOptIn")]
    public async Task SqlServer_EszamanliFiiliGirisVeCikis_TekOutboxVeTekOlayUretir()
    {
        var configured = Environment.GetEnvironmentVariable("STYS_TEST_SQLSERVER_CONNECTION");
        if (string.IsNullOrWhiteSpace(configured)) return;
        var builder = new SqlConnectionStringBuilder(configured) { InitialCatalog = $"STYS_KBS_TEST_{Guid.NewGuid():N}" };
        var databaseName = builder.InitialCatalog;
        try
        {
            await using (var setup = CreateSqlDb(builder.ConnectionString)) { await setup.Database.EnsureCreatedAsync(); await SeedAsync(setup, 1); }
            int guestId; await using (var read = CreateSqlDb(builder.ConnectionString)) guestId = await read.RezervasyonKonaklayanlar.Select(x => x.Id).SingleAsync();
            await using var firstDb = CreateSqlDb(builder.ConnectionString); await using var secondDb = CreateSqlDb(builder.ConnectionString);
            var first = new KbsBildirimOlusturmaService(firstDb, new TestPayloadProtector()).FiiliGirisYapAsync(guestId);
            var second = new KbsBildirimOlusturmaService(secondDb, new TestPayloadProtector()).FiiliGirisYapAsync(guestId);
            var results = await Task.WhenAll(first, second);
            await using var verify = CreateSqlDb(builder.ConnectionString);
            Assert.Equal(1, await verify.KbsBildirimler.CountAsync(x => x.BildirimTipi == KbsBildirimTipleri.Giris));
            Assert.Single(results, x => !x.ZatenKayitli); Assert.Single(results, x => x.ZatenKayitli);
            await using var firstExitDb = CreateSqlDb(builder.ConnectionString); await using var secondExitDb = CreateSqlDb(builder.ConnectionString);
            var exits = await Task.WhenAll(new KbsBildirimOlusturmaService(firstExitDb, new TestPayloadProtector()).FiiliCikisYapAsync(guestId),
                new KbsBildirimOlusturmaService(secondExitDb, new TestPayloadProtector()).FiiliCikisYapAsync(guestId));
            verify.ChangeTracker.Clear(); Assert.Equal(1, await verify.KbsBildirimler.CountAsync(x => x.BildirimTipi == KbsBildirimTipleri.Cikis));
            Assert.Single(exits, x => !x.ZatenKayitli); Assert.Single(exits, x => x.ZatenKayitli);
        }
        finally
        {
            if (databaseName.StartsWith("STYS_KBS_TEST_", StringComparison.Ordinal))
                await using (var cleanup = CreateSqlDb(builder.ConnectionString)) await cleanup.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task AyniFiiliGiris_IkiBildirimOlusturmaz()
    {
        await using var db = CreateDb(1); var guest = await SeedAsync(db, 1); var service = new KbsBildirimOlusturmaService(db, new TestPayloadProtector());
        var first = await service.FiiliGirisYapAsync(guest.Id); var second = await service.FiiliGirisYapAsync(guest.Id);
        Assert.False(first.ZatenKayitli); Assert.True(second.ZatenKayitli); Assert.Equal(1, await db.KbsBildirimler.CountAsync(x => x.BildirimTipi == KbsBildirimTipleri.Giris));
    }

    [Fact]
    public async Task AyniFiiliCikis_IkiBildirimOlusturmaz_veMaliCheckoutDegismez()
    {
        await using var db = CreateDb(1); var guest = await SeedAsync(db, 1, toplamUcret: 1000); var service = new KbsBildirimOlusturmaService(db, new TestPayloadProtector());
        await service.FiiliGirisYapAsync(guest.Id); var first = await service.FiiliCikisYapAsync(guest.Id); var second = await service.FiiliCikisYapAsync(guest.Id);
        Assert.False(first.ZatenKayitli); Assert.True(second.ZatenKayitli); Assert.Equal(1, await db.KbsBildirimler.CountAsync(x => x.BildirimTipi == KbsBildirimTipleri.Cikis));
        Assert.Equal(RezervasyonDurumlari.Onayli, (await db.Rezervasyonlar.SingleAsync()).RezervasyonDurumu);
    }

    [Fact]
    public async Task EksikKimlikBilgisi_DogruValidasyonHatasiVerir()
    {
        await using var db = CreateDb(1); var guest = await SeedAsync(db, 1); guest.KimlikNo = null; await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<BaseException>(() => new KbsBildirimOlusturmaService(db, new TestPayloadProtector()).FiiliGirisYapAsync(guest.Id));
        Assert.Contains("kimlik numarasi", ex.Message, StringComparison.OrdinalIgnoreCase); Assert.Empty(db.KbsBildirimler);
    }

    [Fact]
    public async Task OutboxPayload_OlusturmaAnindakiVeriyiDegismezBicimdeSaklar()
    {
        await using var db = CreateDb(1); var guest = await SeedAsync(db, 1); var protector = new TestPayloadProtector();
        await new KbsBildirimOlusturmaService(db, protector).FiiliGirisYapAsync(guest.Id);
        var item = await db.KbsBildirimler.SingleAsync(); var before = KbsCanonicalPayload.Deserialize(protector.Unprotect(item.ProtectedPayload));
        guest.Ad = "SonradanDegisti"; guest.KimlikNo = "SONRADAN-DEGISTI"; await db.SaveChangesAsync();
        var after = KbsCanonicalPayload.Deserialize(protector.Unprotect(item.ProtectedPayload));
        Assert.Equal("Sentetik", before.Ad); Assert.Equal(before, after);
        Assert.Equal(KbsCanonicalPayload.Hash(KbsCanonicalPayload.Serialize(after)), item.PayloadHash);
    }

    [Fact]
    public async Task Worker_KonaklayanSonradanDegisseBileIlkSnapshotiGonderir()
    {
        await using var db = CreateDb(1); var guest = await SeedAsync(db, 1);
        await new KbsBildirimOlusturmaService(db, new TestPayloadProtector()).FiiliGirisYapAsync(guest.Id);
        guest.Ad = "SonradanDegisti"; guest.KimlikNo = "SONRADAN-DEGISTI"; await db.SaveChangesAsync();
        var connector = new CapturingConnector(); await CreateWorker(db, connector).ProcessBatchAsync(default);
        var request = Assert.Single(connector.Girisler); Assert.Equal("Sentetik", request.Ad); Assert.Equal("SENTETIK-KIMLIK-001", request.KimlikNo);
    }

    [Theory]
    [InlineData("Basarili", true, null)]
    [InlineData("Timeout", false, KbsHataSiniflari.Transient)]
    [InlineData("YetkiIpHatasi", false, KbsHataSiniflari.Configuration)]
    [InlineData("Belirsiz", false, KbsHataSiniflari.Uncertain)]
    public async Task FakeConnector_SentetikSonuclariUretir(string response, bool success, string? errorClass)
    {
        var connector = new FakeKbsConnector(Options.Create(new KbsOptions { FakeResponse = response }));
        var result = await connector.GirisBildirAsync(new(1, 1, "Test", "Kisi", "SENTETIK-KIMLIK-001", null, "TR", DateTime.UtcNow), default);
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
    public async Task Worker_BozukPayloadiIzoleEder_veSonrakiKaydiIsler()
    {
        await using var db = CreateDb(1); var firstGuest = await SeedAsync(db, 1); var secondGuest = await SeedGuestAsync(db, firstGuest.Rezervasyon!);
        var broken = NewNotification(firstGuest, KbsBildirimDurumlari.Hazir); broken.PayloadHash = new string('0', 64);
        var valid = NewNotification(secondGuest, KbsBildirimDurumlari.Hazir); db.KbsBildirimler.AddRange(broken, valid); await db.SaveChangesAsync();
        await CreateWorker(db, "Basarili").ProcessBatchAsync(default);
        Assert.Equal(KbsBildirimDurumlari.MudahaleGerekli, broken.Durum); Assert.Equal("PAYLOAD-HASH", broken.SonHataKodu);
        Assert.Equal(KbsBildirimDurumlari.Basarili, valid.Durum);
    }

    [Fact]
    public async Task Mutabakat_veEgmDogrulama_YalnizcaIzinliDurumlardanGecer_veDenetlenir()
    {
        await using var db = CreateDb(1); var guest = await SeedAsync(db, 1);
        var uncertain = NewNotification(guest, KbsBildirimDurumlari.SonucuBelirsiz); var egm = NewNotification(guest, KbsBildirimDurumlari.YuklemeOnayiBekliyor);
        db.KbsBildirimler.AddRange(uncertain, egm); await db.SaveChangesAsync();
        var service = new KbsYonetimService(db, new KbsConnectorResolver([]), Options.Create(new KbsOptions()), new TestPayloadProtector());
        await service.MutabakatYapAsync(uncertain.Id, new("Islendi", "Sentetik dis sistem kontrolu tamamlandi.", "REF-1"), default);
        await service.EgmDogrulaAsync(egm.Id, new(true, "Sentetik EGM yukleme sonucu kontrol edildi.", "REF-2"), default);
        Assert.Equal(KbsBildirimDurumlari.Dogrulandi, uncertain.Durum); Assert.Equal(KbsBildirimDurumlari.Dogrulandi, egm.Durum);
        Assert.Equal(2, await db.KbsDurumGecmisleri.CountAsync());
        await Assert.ThrowsAsync<BaseException>(() => service.TekrarKuyrugaAlAsync(uncertain.Id, default));
    }

    [Theory]
    [InlineData(KbsBildirimDurumlari.Gonderiliyor)]
    [InlineData(KbsBildirimDurumlari.SonucuBelirsiz)]
    [InlineData(KbsBildirimDurumlari.Basarili)]
    [InlineData(KbsBildirimDurumlari.Dogrulandi)]
    [InlineData(KbsBildirimDurumlari.DosyaUretildi)]
    [InlineData(KbsBildirimDurumlari.YuklemeOnayiBekliyor)]
    [InlineData(KbsBildirimDurumlari.Iptal)]
    public async Task GenericRetry_GuvenliOlmayanDurumlariReddeder(string state)
    {
        await using var db = CreateDb(1); var guest = await SeedAsync(db, 1); var item = NewNotification(guest, state); db.KbsBildirimler.Add(item); await db.SaveChangesAsync();
        var service = new KbsYonetimService(db, new KbsConnectorResolver([]), Options.Create(new KbsOptions()));
        await Assert.ThrowsAsync<BaseException>(() => service.TekrarKuyrugaAlAsync(item.Id, default));
    }

    [Fact]
    public async Task IslenmediMutabakati_SonrasindaRetryAcikOlur_vePayloadDegismez()
    {
        await using var db = CreateDb(1); var guest = await SeedAsync(db, 1); var item = NewNotification(guest, KbsBildirimDurumlari.SonucuBelirsiz); db.KbsBildirimler.Add(item); await db.SaveChangesAsync();
        var hash = item.PayloadHash; var payload = item.ProtectedPayload; var service = new KbsYonetimService(db, new KbsConnectorResolver([]), Options.Create(new KbsOptions()));
        await service.MutabakatYapAsync(item.Id, new("Islenmedi", "Sentetik kurum kontrolunde kayit bulunamadi.", null), default);
        Assert.Equal(KbsBildirimDurumlari.MudahaleGerekli, item.Durum); await service.TekrarKuyrugaAlAsync(item.Id, default);
        Assert.Equal(KbsBildirimDurumlari.Hazir, item.Durum); Assert.Equal(hash, item.PayloadHash); Assert.Equal(payload, item.ProtectedPayload);
    }

    [Fact]
    public async Task Tenant_BaskaKurumaAitBildirimdeMutabakatYapamaz()
    {
        var database = Guid.NewGuid().ToString(); long id;
        await using (var admin = CreateDb(null, database, true))
        {
            admin.KbsBildirimler.Add(new KbsBildirim { KurumId = 2, TesisId = 2, RezervasyonId = 2, RezervasyonKonaklayanId = 2, BildirimTipi = "Giris", Saglayici = "Fake", Durum = KbsBildirimDurumlari.SonucuBelirsiz, IdempotencyKey = "tenant-mutabakat", OlayAnahtari = "tenant-mutabakat", PayloadHash = new string('0', 64), ProtectedPayload = "sentetik" });
            await admin.SaveChangesAsync(); id = admin.KbsBildirimler.Single().Id;
        }
        await using var tenant = CreateDb(1, database); var service = new KbsYonetimService(tenant, new KbsConnectorResolver([]), Options.Create(new KbsOptions()));
        await Assert.ThrowsAsync<BaseException>(() => service.MutabakatYapAsync(id, new("Islendi", "Sentetik tenant kontrolu.", null), default));
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
        Assert.DoesNotContain("123456789", KbsBildirimWorker.Mask("Kimlik 123456789 hatali"));
        var maskMethod = typeof(RequestResponseLoggingMiddleware).GetMethod("MaskSensitiveData", BindingFlags.NonPublic | BindingFlags.Static)!;
        var maskedBody = (string?)maskMethod.Invoke(null, ["{\"tcKimlikNo\":\"SENTETIK-KIMLIK-001\",\"pasaportNo\":\"SENTETIK-BELGE\",\"soapPayload\":\"raw\"}"]);
        Assert.DoesNotContain("SENTETIK-KIMLIK-001", maskedBody); Assert.DoesNotContain("SENTETIK-BELGE", maskedBody); Assert.DoesNotContain("raw", maskedBody);
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
        var result = await connector.GirisBildirAsync(new(1, 1, "Sentetik", "Kisi", "SENTETIK-KIMLIK-001", null, "TR", DateTime.UtcNow), default);
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
        services.AddSingleton<IKbsPayloadProtector, TestPayloadProtector>(); services.AddScoped<IKbsConnector, FakeKbsConnector>(); services.AddScoped<IKbsConnectorResolver, KbsConnectorResolver>(); var provider = services.BuildServiceProvider();
        return new KbsBildirimWorker(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new KbsOptions { MaxAttempts = 3, SendingRecoveryMinutes = 1 }), NullLogger<KbsBildirimWorker>.Instance);
    }

    private static KbsBildirimWorker CreateWorker(StysAppDbContext db, IKbsConnector connector)
    {
        var services = new ServiceCollection(); services.AddSingleton(db); services.AddSingleton<IOptions<KbsOptions>>(Options.Create(new KbsOptions { MaxAttempts = 3, SendingRecoveryMinutes = 1 }));
        services.AddSingleton<IKbsPayloadProtector, TestPayloadProtector>(); services.AddSingleton(connector); services.AddScoped<IKbsConnectorResolver, KbsConnectorResolver>(); var provider = services.BuildServiceProvider();
        return new KbsBildirimWorker(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new KbsOptions { MaxAttempts = 3, SendingRecoveryMinutes = 1 }), NullLogger<KbsBildirimWorker>.Instance);
    }

    private static KbsBildirim NewNotification(RezervasyonKonaklayan guest, string state)
    {
        var snapshot = new KbsPayloadSnapshot { BildirimTipi = KbsBildirimTipleri.Giris, KurumId = 1, TesisId = guest.Rezervasyon!.TesisId, RezervasyonId = guest.RezervasyonId, RezervasyonKonaklayanId = guest.Id, OlayTarihi = DateTime.UtcNow, Ad = guest.Ad, Soyad = guest.Soyad, KimlikNo = guest.KimlikNo };
        var canonical = KbsCanonicalPayload.Serialize(snapshot);
        return new() { KurumId = 1, TesisId = guest.Rezervasyon.TesisId, RezervasyonId = guest.RezervasyonId, RezervasyonKonaklayanId = guest.Id, BildirimTipi = KbsBildirimTipleri.Giris, Saglayici = KbsEntegrasyonTipleri.Fake, Durum = state, IdempotencyKey = Guid.NewGuid().ToString("N"), OlayAnahtari = Guid.NewGuid().ToString("N"), PayloadVersion = snapshot.Version, PayloadHash = KbsCanonicalPayload.Hash(canonical), ProtectedPayload = canonical, SonrakiDenemeTarihi = DateTime.UtcNow.AddMinutes(-1) };
    }

    private static async Task<RezervasyonKonaklayan> SeedAsync(StysAppDbContext db, int kurumId, decimal toplamUcret = 0)
    {
        var tesis = new Tesis { KurumId = kurumId, Ad = "Sentetik Tesis", Telefon = "000", Adres = "Test", IlId = 1 }; db.Tesisler.Add(tesis); await db.SaveChangesAsync();
        var reservation = new Rezervasyon { TesisId = tesis.Id, Tesis = tesis, ReferansNo = Guid.NewGuid().ToString("N"), KisiSayisi = 1, GirisTarihi = DateTime.UtcNow, CikisTarihi = DateTime.UtcNow.AddDays(1), ToplamUcret = toplamUcret, MisafirAdiSoyadi = "Sentetik Kisi", MisafirTelefon = "000", RezervasyonDurumu = RezervasyonDurumlari.Onayli };
        var guest = new RezervasyonKonaklayan { Rezervasyon = reservation, RezervasyonId = reservation.Id, SiraNo = 1, AdSoyad = "Sentetik Kisi", Ad = "Sentetik", Soyad = "Kisi", KimlikTuru = KbsKimlikTurleri.Tckn, KimlikNo = "SENTETIK-KIMLIK-001" };
        db.Rezervasyonlar.Add(reservation); db.RezervasyonKonaklayanlar.Add(guest); db.KbsTesisAyarlari.Add(new KbsTesisAyari { KurumId = kurumId, TesisId = tesis.Id, KollukSistemi = KbsKollukSistemleri.Egm, EntegrasyonTipi = KbsEntegrasyonTipleri.Fake, AktifMi = true }); await db.SaveChangesAsync(); return guest;
    }

    private static async Task<RezervasyonKonaklayan> SeedGuestAsync(StysAppDbContext db, Rezervasyon reservation)
    {
        var guest = new RezervasyonKonaklayan { Rezervasyon = reservation, RezervasyonId = reservation.Id, SiraNo = 2, AdSoyad = "Sentetik Iki", Ad = "Sentetik", Soyad = "Iki", KimlikTuru = KbsKimlikTurleri.Tckn, KimlikNo = "SENTETIK-KIMLIK-002" };
        db.RezervasyonKonaklayanlar.Add(guest); await db.SaveChangesAsync(); return guest;
    }

    private static StysAppDbContext CreateDb(int? kurumId, string? name = null, bool superAdmin = false)
    {
        var opts = new DbContextOptionsBuilder<StysAppDbContext>().UseInMemoryDatabase(name ?? Guid.NewGuid().ToString()).Options;
        return new StysAppDbContext(opts, null, new TenantAccessor(kurumId, superAdmin));
    }

    private static StysAppDbContext CreateSqlDb(string connectionString)
    {
        var opts = new DbContextOptionsBuilder<StysAppDbContext>().UseSqlServer(connectionString).Options;
        return new StysAppDbContext(opts, null, new TenantAccessor(1, false));
    }

    private sealed class TenantAccessor(int? kurumId, bool superAdmin) : ICurrentTenantAccessor { public int? GetCurrentKurumId() => kurumId; public IReadOnlyList<int> GetAccessibleKurumIds() => kurumId.HasValue ? [kurumId.Value] : []; public bool IsSuperAdmin() => superAdmin; public bool IsKurumAdmin() => false; }
    private sealed class FakeHostEnvironment(string name) : IHostEnvironment { public string EnvironmentName { get; set; } = name; public string ApplicationName { get; set; } = "Tests"; public string ContentRootPath { get; set; } = ""; public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider(); }
    private sealed class TestPayloadProtector : IKbsPayloadProtector { public bool IsReady => true; public string Protect(string canonicalJson) => canonicalJson; public string Unprotect(string protectedPayload) => protectedPayload; }
    private sealed class CapturingConnector : IKbsConnector
    {
        public string Saglayici => KbsEntegrasyonTipleri.Fake; public List<KbsGirisTalebi> Girisler { get; } = [];
        public Task<KbsSonuc> GirisBildirAsync(KbsGirisTalebi talep, CancellationToken cancellationToken) { Girisler.Add(talep); return Task.FromResult(new KbsSonuc(true, "OK", "Sentetik basari.")); }
        public Task<KbsSonuc> CikisBildirAsync(KbsCikisTalebi talep, CancellationToken cancellationToken) => Task.FromResult(new KbsSonuc(true, "OK", "Sentetik basari."));
        public Task<KbsSonuc> OdaGuncelleAsync(KbsOdaGuncellemeTalebi talep, CancellationToken cancellationToken) => Task.FromResult(new KbsSonuc(true, "OK", "Sentetik basari."));
        public Task<KbsBaglantiTestSonucu> BaglantiKontrolAsync(int tesisId, CancellationToken cancellationToken) => Task.FromResult(new KbsBaglantiTestSonucu(true, "Sentetik."));
    }
}
