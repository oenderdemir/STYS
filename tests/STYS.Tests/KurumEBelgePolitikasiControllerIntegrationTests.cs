using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Controllers;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.UserKurums.Entities;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.10 görev md.17 - <see cref="KurumEBelgePolitikasiController"/>'ın kurum bazlı yetkilendirme
/// (cross-tenant reddi, SuperAdmin/KurumAdmin ayrımı) VE response DTO şeklini (entity DOĞRUDAN
/// dönmez, VKN/kurum kimlik detayı EKLENMEZ) gerçek SQL Server'a karşı doğrular. TodIdentityDbContext
/// InMemory sağlayıcı ile kurulur (UserKurums sorguları İÇİN yeterli - RowVersion/check constraint
/// gerektirmez); StysAppDbContext GERÇEK SQL Server'dır (RowVersion/check constraint'ler gerçek olmalı).
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
[Trait("Domain", "EBelge")]
[Trait("TestLevel", "SqlIntegration")]
[Trait("Dependency", "SqlServer")]
public class KurumEBelgePolitikasiControllerIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "EBF-API";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;

    public async Task InitializeAsync()
    {
        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (kurum, il, tesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, _uniqueSuffix);
        _kurumId = kurum.Id;
        _ilId = il.Id;
        _tesisId = tesis.Id;

        await dbContext.Set<KurumEBelgePolitikasi>().IgnoreQueryFilters()
            .Where(p => p.KurumId == _kurumId)
            .ExecuteDeleteAsync();
    }

    public async Task DisposeAsync()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix, _tesisId, _kurumId, _ilId);
    }

    private static StysAppDbContext CreateDbContext() => SatisBelgesiMuhasebeTestSupport.CreateDbContext();

    private static TodIdentityDbContext CreateIdentityDbContext()
    {
        var options = new DbContextOptionsBuilder<TodIdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TodIdentityDbContext(options);
    }

    private static KurumEBelgePolitikasiController CreateController(
        StysAppDbContext dbContext,
        TodIdentityDbContext identityDbContext,
        bool isSuperAdmin,
        bool isKurumAdmin,
        int? currentKurumId,
        Guid? currentUserId = null,
        TimeProvider? timeProvider = null,
        EBelgeProcessingOptions? processingOptions = null,
        IEBelgeSigningActivationGate? signingGate = null,
        EBelgeUblOptions? ublOptions = null) =>
        new(
            new EBelgeKurumPolitikaYonetimServisi(
                dbContext,
                new EBelgeYontemYetenekSaglayici(),
                Options.Create(processingOptions ?? new EBelgeProcessingOptions { Enabled = true, NotBeforeLocalDate = "2020-01-01" }),
                timeProvider ?? TimeProvider.System),
            new EBelgeKurumReadinessService(
                dbContext,
                new EBelgeYontemYetenekSaglayici(),
                new EBelgeProcessingActivationGate(
                    Options.Create(processingOptions ?? new EBelgeProcessingOptions { Enabled = true, NotBeforeLocalDate = "2020-01-01" }),
                    timeProvider ?? TimeProvider.System,
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<EBelgeProcessingActivationGate>.Instance),
                signingGate ?? new AlwaysKapaliSigningGate(),
                timeProvider ?? TimeProvider.System,
                Options.Create(processingOptions ?? new EBelgeProcessingOptions { Enabled = true, NotBeforeLocalDate = "2020-01-01" }),
                // Faz 2B.11.1 - testlerde VARSAYILAN "kapalı" (production varsayımıyla TUTARLI, bkz.
                // görev md.17); UBL-spesifik readiness testleri BU parametreyi AÇIKÇA override eder.
                Options.Create(ublOptions ?? new EBelgeUblOptions { Enabled = false })),
            dbContext,
            identityDbContext,
            new FakeCurrentUserAccessor(currentUserId ?? Guid.NewGuid()),
            new FakeCurrentTenantAccessor(currentKurumId, isSuperAdmin, isKurumAdmin));

    /// <summary>Faz 2B.11 - readiness testlerinde signing gate'in DAVRANIŞI test EDİLMİYOR (bu, GİB Portal readiness'ini bloke ETMEMELİDİR - bkz. görev md.30) - bu yüzden VARSAYILAN olarak "her zaman kapalı" bir sabit kullanılır; gate açık/kapalı OLMASI HİÇBİR readiness testinin sonucunu DEĞİŞTİRMEMELİDİR.</summary>
    private sealed class AlwaysKapaliSigningGate : IEBelgeSigningActivationGate
    {
        public bool ShouldCreateSigningMessage() => false;
        public bool CanSignNow() => false;
    }

    /// <summary>
    /// Controller eylemleri `Ok(dto)` döndürür - bu, `ActionResult&lt;T&gt;.Result`'ı DOLDURUR,
    /// `.Value`'yu DEĞİL (yalnız T'DEN implicit dönüşüm `.Value`'yu doldurur). Bu yüzden test
    /// tarafında değer, `OkObjectResult.Value` üzerinden ÇIKARILIR.
    /// </summary>
    private static T? AsOk<T>(Microsoft.AspNetCore.Mvc.ActionResult<T> result)
    {
        var ok = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);
        return (T?)ok.Value;
    }

    private static KurumEBelgePolitikasiGuncellemeDto Dto(
        EBelgeEntegrasyonYontemi yontem, bool aktifMi, DateTime? aktivasyon, string rowVersion = "") => new()
    {
        EntegrasyonYontemi = yontem,
        AktifMi = aktifMi,
        AktivasyonYerelTarihi = aktivasyon,
        DegisiklikNedeni = "test",
        RowVersion = rowVersion,
    };

    [IntegrationFact]
    public async Task SuperAdminHerKurumunPolitikasiniGorebilirVeGuncelleyebilir()
    {
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        var controller = CreateController(dbContext, identityDbContext, isSuperAdmin: true, isKurumAdmin: false, currentKurumId: null);

        var bosSonuc = await controller.Get(_kurumId, CancellationToken.None);
        Assert.Null(AsOk(bosSonuc));

        var guncellenenSonuc = await controller.Update(_kurumId, Dto(EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1)), CancellationToken.None);
        var guncellenen = AsOk(guncellenenSonuc);

        Assert.NotNull(guncellenen);
        Assert.Equal(_kurumId, guncellenen!.KurumId);
        Assert.Equal(EBelgeEntegrasyonYontemi.GibPortal, guncellenen.EntegrasyonYontemi);
        Assert.Equal(1, guncellenen.PolitikaSurumu);
        Assert.NotEmpty(guncellenen.RowVersion);
    }

    [IntegrationFact]
    public async Task YetkisizKullaniciBaskaKurumunPolitikasiniGoremez()
    {
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        // UserKurums içinde bu kurum için HİÇ kayıt YOK.
        var controller = CreateController(dbContext, identityDbContext, isSuperAdmin: false, isKurumAdmin: false, currentKurumId: null);

        var ex = await Assert.ThrowsAsync<BaseException>(() => controller.Get(_kurumId, CancellationToken.None));
        Assert.Equal(403, ex.ErrorCode);
    }

    [IntegrationFact]
    public async Task UserKurumlarIcindeAktifKaydiOlanKullaniciGorebilirAmaYonetemez()
    {
        var userId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        identityDbContext.UserKurums.Add(new UserKurum
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            KurumId = _kurumId,
            AktifMi = true,
            VarsayilanMi = true,
            IsKurumAdmin = false,
        });
        await identityDbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, identityDbContext, isSuperAdmin: false, isKurumAdmin: false, currentKurumId: null, currentUserId: userId);

        var goruntule = await controller.Get(_kurumId, CancellationToken.None);
        Assert.Null(AsOk(goruntule)); // henüz politika yok, ama yetki reddi de YOK

        var ex = await Assert.ThrowsAsync<BaseException>(
            () => controller.Update(_kurumId, Dto(EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1)), CancellationToken.None));
        Assert.Equal(403, ex.ErrorCode);
    }

    [IntegrationFact]
    public async Task KurumAdminKendiKurumunuYonetebilirBaskaKurumuYonetemez()
    {
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        var controller = CreateController(dbContext, identityDbContext, isSuperAdmin: false, isKurumAdmin: true, currentKurumId: _kurumId);

        var guncellenenSonuc = await controller.Update(_kurumId, Dto(EBelgeEntegrasyonYontemi.Kullanilmayacak, true, new DateTime(2020, 1, 1)), CancellationToken.None);
        Assert.Equal(EBelgeEntegrasyonYontemi.Kullanilmayacak, AsOk(guncellenenSonuc)!.EntegrasyonYontemi);

        var baskaKurumId = _kurumId + 999_000;
        var ex = await Assert.ThrowsAsync<BaseException>(
            () => controller.Update(baskaKurumId, Dto(EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1)), CancellationToken.None));
        Assert.Equal(403, ex.ErrorCode);
    }

    [IntegrationFact]
    public async Task RevizyonlarActorBilgisiniOtomatikYaziliCreatedByAlanindanAlir()
    {
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        var controller = CreateController(dbContext, identityDbContext, isSuperAdmin: true, isKurumAdmin: false, currentKurumId: null);

        await controller.Update(_kurumId, Dto(EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1)), CancellationToken.None);

        var revizyonlarSonuc = await controller.GetRevizyonlar(_kurumId, CancellationToken.None);

        var revizyon = Assert.Single(AsOk(revizyonlarSonuc)!);
        Assert.NotNull(revizyon.DegistirenKullanici);
        Assert.Equal(0, revizyon.EskiSurum);
        Assert.Equal(1, revizyon.YeniSurum);
    }

    [IntegrationFact]
    public async Task EskiRowVersionIleGuncellemeSafeConcurrencyHatasiVerir()
    {
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        var controller = CreateController(dbContext, identityDbContext, isSuperAdmin: true, isKurumAdmin: false, currentKurumId: null);

        var ilkSonuc = await controller.Update(_kurumId, Dto(EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1)), CancellationToken.None);
        var ilk = AsOk(ilkSonuc)!;

        await using var dbContext2 = CreateDbContext();
        var controller2 = CreateController(dbContext2, identityDbContext, isSuperAdmin: true, isKurumAdmin: false, currentKurumId: null);
        await controller2.Update(_kurumId, Dto(EBelgeEntegrasyonYontemi.GibPortal, false, null, ilk.RowVersion), CancellationToken.None);

        await using var dbContext3 = CreateDbContext();
        var controller3 = CreateController(dbContext3, identityDbContext, isSuperAdmin: true, isKurumAdmin: false, currentKurumId: null);

        var ex = await Assert.ThrowsAsync<EBelgeKurumPolitikaConcurrencyException>(
            () => controller3.Update(_kurumId, Dto(EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1), ilk.RowVersion), CancellationToken.None));

        Assert.Equal(409, ex.ErrorCode);
    }

    // ──────────────────────────── Faz 2B.11 - readiness endpoint'i ────────────────────────────

    private async Task ClearKurumPolitikaAsync(StysAppDbContext dbContext) =>
        await dbContext.Set<KurumEBelgePolitikasi>().IgnoreQueryFilters()
            .Where(p => p.KurumId == _kurumId)
            .ExecuteDeleteAsync();

    private async Task SetKurumSellerFieldsAsync(string? vergiNo = "1111111111", string? vergiDairesi = "Test VD", string? adres = "Test Adres", string? ilce = "Kadıköy", string? il = "İstanbul")
    {
        await using var dbContext = CreateDbContext();
        var kurum = await dbContext.Kurumlar.SingleAsync(x => x.Id == _kurumId);
        kurum.VergiNo = vergiNo;
        kurum.VergiDairesi = vergiDairesi;
        kurum.Adres = adres;
        kurum.Ilce = ilce;
        kurum.Il = il;
        await dbContext.SaveChangesAsync();
    }

    // ---- Authorization (görev md.42 madde 1-4) ----

    [IntegrationFact]
    public async Task ErisilebilirKurumIcinReadinessBasariylaDoner()
    {
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        var controller = CreateController(dbContext, identityDbContext, isSuperAdmin: true, isKurumAdmin: false, currentKurumId: null);

        var readiness = AsOk(await controller.GetReadiness(_kurumId, CancellationToken.None));

        Assert.NotNull(readiness);
        Assert.Equal(_kurumId, readiness!.KurumId);
    }

    [IntegrationFact]
    public async Task YetkisizKullaniciBaskaKurumunReadinessiniGoremez()
    {
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        var controller = CreateController(dbContext, identityDbContext, isSuperAdmin: false, isKurumAdmin: false, currentKurumId: null);

        var ex = await Assert.ThrowsAsync<BaseException>(() => controller.GetReadiness(_kurumId, CancellationToken.None));
        Assert.Equal(403, ex.ErrorCode);
    }

    /// <summary>
    /// Görev md.4/md.42 madde 3-4 - readiness endpoint'i YENİ bir permission İCAT ETMEZ; mevcut
    /// `GET` (politika okuma) İLE AYNI `[Permission(.View, SuperAdmin)]` sözleşmesini taşır - bu,
    /// reflection üzerinden DOĞRUDAN kanıtlanır (gerçek ASP.NET Core authorization pipeline'ı bu
    /// testlerde ÇALIŞTIRILMIYOR, mevcut dosyadaki DİĞER testlerle AYNI kısıt).
    /// </summary>
    [IntegrationFact]
    public async Task ReadinessEndpointiPolitikaGetIleAyniPermissionSozlesmesiniTasir()
    {
        var controllerType = typeof(KurumEBelgePolitikasiController);
        var getMethod = controllerType.GetMethod(nameof(KurumEBelgePolitikasiController.Get))!;
        var getReadinessMethod = controllerType.GetMethod(nameof(KurumEBelgePolitikasiController.GetReadiness))!;

        var getPermission = getMethod.GetCustomAttributesData().Single(a => a.AttributeType.Name == "PermissionAttribute");
        var getReadinessPermission = getReadinessMethod.GetCustomAttributesData().Single(a => a.AttributeType.Name == "PermissionAttribute");

        var getArgs = getPermission.ConstructorArguments.Select(a => a.Value?.ToString()).ToList();
        var getReadinessArgs = getReadinessPermission.ConstructorArguments.Select(a => a.Value?.ToString()).ToList();

        Assert.Equal(getArgs, getReadinessArgs);
    }

    // ---- Readiness hesaplaması (görev md.42 madde 5-15) ----

    [IntegrationFact]
    public async Task PolitikaYokIkenConfiguredFalseDoner()
    {
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        await ClearKurumPolitikaAsync(dbContext);
        var controller = CreateController(dbContext, identityDbContext, isSuperAdmin: true, isKurumAdmin: false, currentKurumId: null);

        var readiness = AsOk(await controller.GetReadiness(_kurumId, CancellationToken.None))!;

        Assert.False(readiness.PolitikaYapilandirilmisMi);
        Assert.False(readiness.IslemeHazirMi);
        Assert.Contains(EBelgeKurumReadinessKodlari.PolitikaYapilandirilmadi, readiness.BlokajNedenleri);
    }

    [IntegrationFact]
    public async Task KullanilmayacakIcinYerelUblVeSigningUygulanamazAmaHazirSayilir()
    {
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        var controller = CreateController(dbContext, identityDbContext, isSuperAdmin: true, isKurumAdmin: false, currentKurumId: null);
        await controller.Update(_kurumId, Dto(EBelgeEntegrasyonYontemi.Kullanilmayacak, true, new DateTime(2020, 1, 1)), CancellationToken.None);

        var readiness = AsOk(await controller.GetReadiness(_kurumId, CancellationToken.None))!;

        Assert.True(readiness.IslemeHazirMi);
        Assert.Empty(readiness.BlokajNedenleri);
        Assert.False(readiness.YerelSnapshotGerekliMi);
        Assert.False(readiness.YerelUnsignedUblGerekliMi);
        Assert.False(readiness.YerelImzaGerekliMi);
        Assert.False(readiness.SigningGateUygulanabilirMi);
    }

    [IntegrationFact]
    public async Task HariciMuhasebeSistemindeSigningUygulanamaz()
    {
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        var controller = CreateController(dbContext, identityDbContext, isSuperAdmin: true, isKurumAdmin: false, currentKurumId: null);
        var dto = Dto(EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi, true, new DateTime(2020, 1, 1));
        var updated = await controller.Update(_kurumId, dto with { HariciSistemKodu = "ERP-1" }, CancellationToken.None);
        Assert.NotNull(AsOk(updated));

        var readiness = AsOk(await controller.GetReadiness(_kurumId, CancellationToken.None))!;

        Assert.True(readiness.IslemeHazirMi);
        Assert.False(readiness.YerelImzaGerekliMi);
        Assert.False(readiness.SigningGateUygulanabilirMi);
    }

    [IntegrationFact]
    public async Task GibPortalEksikVergiNoIleNotReadyVeVergiNoKoduDoner()
    {
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        await SetKurumSellerFieldsAsync(vergiNo: null);
        var controller = CreateController(dbContext, identityDbContext, isSuperAdmin: true, isKurumAdmin: false, currentKurumId: null);
        await controller.Update(_kurumId, Dto(EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1)), CancellationToken.None);

        var readiness = AsOk(await controller.GetReadiness(_kurumId, CancellationToken.None))!;

        Assert.False(readiness.IslemeHazirMi);
        Assert.False(readiness.SaticiAnaVerileriHazirMi);
        Assert.Contains(EBelgeKurumReadinessKodlari.VergiNo, readiness.EksikSaticiAlanlari);
        Assert.Contains(EBelgeKurumReadinessKodlari.SaticiAnaVerileriEksik, readiness.BlokajNedenleri);
    }

    [IntegrationFact]
    public async Task GibPortalEksikAdresIleGuvenliAlanKoduDoner()
    {
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        await SetKurumSellerFieldsAsync(adres: null);
        var controller = CreateController(dbContext, identityDbContext, isSuperAdmin: true, isKurumAdmin: false, currentKurumId: null);
        await controller.Update(_kurumId, Dto(EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1)), CancellationToken.None);

        var readiness = AsOk(await controller.GetReadiness(_kurumId, CancellationToken.None))!;

        Assert.Contains(EBelgeKurumReadinessKodlari.Adres, readiness.EksikSaticiAlanlari);
        // Güvenlik: eksik alan listesi yalnız SABİT kod taşır, gerçek adres DEĞERİNİ İÇERMEZ.
        Assert.DoesNotContain(readiness.EksikSaticiAlanlari, x => x.Contains("Test", StringComparison.OrdinalIgnoreCase));
    }

    [IntegrationFact]
    public async Task GibPortalTumSaticiAlanlariDoluysaSaticiHazirVeIslemeHazir()
    {
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        await SetKurumSellerFieldsAsync();
        // UBL feature flag AÇIKÇA açılır - bu test satıcı/aktivasyon/global-kapı koşullarını
        // kapsar, UBL flag'i AYRICA (bkz. aşağıdaki UblFeatureKapali* testleri) kapsanır.
        var controller = CreateController(dbContext, identityDbContext, isSuperAdmin: true, isKurumAdmin: false, currentKurumId: null,
            ublOptions: new EBelgeUblOptions { Enabled = true });
        await controller.Update(_kurumId, Dto(EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1)), CancellationToken.None);

        var readiness = AsOk(await controller.GetReadiness(_kurumId, CancellationToken.None))!;

        Assert.True(readiness.SaticiAnaVerileriHazirMi);
        Assert.Empty(readiness.EksikSaticiAlanlari);
        Assert.True(readiness.IslemeHazirMi);
        Assert.Empty(readiness.BlokajNedenleri);
    }

    [IntegrationFact]
    public async Task GibPortalSigningGateKapaliOlsaBileReadinessiBlokeEtmez()
    {
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        await SetKurumSellerFieldsAsync();
        // CreateController varsayılanı ZATEN "her zaman kapalı" bir signing gate kullanır (bkz.
        // AlwaysKapaliSigningGate) - bu test bunu AÇIKÇA doğrular. UBL feature flag AÇIKÇA açılır -
        // bu testin amacı SIGNING gate'in etkisiz olduğunu kanıtlamaktır, UBL flag'in etkisi DEĞİL.
        var controller = CreateController(dbContext, identityDbContext, isSuperAdmin: true, isKurumAdmin: false, currentKurumId: null,
            ublOptions: new EBelgeUblOptions { Enabled = true });
        await controller.Update(_kurumId, Dto(EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1)), CancellationToken.None);

        var readiness = AsOk(await controller.GetReadiness(_kurumId, CancellationToken.None))!;

        Assert.False(readiness.SigningSuAnMumkunMu);
        Assert.False(readiness.SigningGateUygulanabilirMi); // GibPortal yerel imza GEREKTİRMEZ
        Assert.True(readiness.IslemeHazirMi);
        Assert.DoesNotContain(EBelgeKurumReadinessKodlari.SigningGateKapali, readiness.BlokajNedenleri);
    }

    // ---- Faz 2B.11.1 görev md.6 - UBL feature flag readiness davranışı ----

    [IntegrationFact]
    public async Task GibPortalUblDisabledIkenIslemeHazirDegilVeUblFeatureKapaliKoduDoner()
    {
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        await SetKurumSellerFieldsAsync();
        var controller = CreateController(dbContext, identityDbContext, isSuperAdmin: true, isKurumAdmin: false, currentKurumId: null,
            processingOptions: new EBelgeProcessingOptions { Enabled = true, NotBeforeLocalDate = "2020-01-01" },
            ublOptions: new EBelgeUblOptions { Enabled = false });
        await controller.Update(_kurumId, Dto(EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1)), CancellationToken.None);

        var readiness = AsOk(await controller.GetReadiness(_kurumId, CancellationToken.None))!;

        Assert.True(readiness.SaticiAnaVerileriHazirMi);
        Assert.True(readiness.GlobalProcessingAktifMi);
        Assert.True(readiness.UblFeatureUygulanabilirMi);
        Assert.False(readiness.UblFeatureAktifMi);
        Assert.False(readiness.IslemeHazirMi);
        Assert.Contains(EBelgeKurumReadinessKodlari.UblFeatureKapali, readiness.BlokajNedenleri);
    }

    [IntegrationFact]
    public async Task GibPortalUblEnabledIkenAyniKosullardaIslemeHazirOlabilir()
    {
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        await SetKurumSellerFieldsAsync();
        var controller = CreateController(dbContext, identityDbContext, isSuperAdmin: true, isKurumAdmin: false, currentKurumId: null,
            processingOptions: new EBelgeProcessingOptions { Enabled = true, NotBeforeLocalDate = "2020-01-01" },
            ublOptions: new EBelgeUblOptions { Enabled = true });
        await controller.Update(_kurumId, Dto(EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1)), CancellationToken.None);

        var readiness = AsOk(await controller.GetReadiness(_kurumId, CancellationToken.None))!;

        Assert.True(readiness.UblFeatureUygulanabilirMi);
        Assert.True(readiness.UblFeatureAktifMi);
        Assert.True(readiness.IslemeHazirMi);
        Assert.DoesNotContain(EBelgeKurumReadinessKodlari.UblFeatureKapali, readiness.BlokajNedenleri);
    }

    [IntegrationFact]
    public async Task KullanilmayacakUblDisabledIkenEtkilenmezVeHazirSayilir()
    {
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        var controller = CreateController(dbContext, identityDbContext, isSuperAdmin: true, isKurumAdmin: false, currentKurumId: null,
            ublOptions: new EBelgeUblOptions { Enabled = false });
        await controller.Update(_kurumId, Dto(EBelgeEntegrasyonYontemi.Kullanilmayacak, true, new DateTime(2020, 1, 1)), CancellationToken.None);

        var readiness = AsOk(await controller.GetReadiness(_kurumId, CancellationToken.None))!;

        Assert.False(readiness.UblFeatureUygulanabilirMi); // yerel UBL bu yöntemde N/A
        Assert.True(readiness.IslemeHazirMi);
        Assert.DoesNotContain(EBelgeKurumReadinessKodlari.UblFeatureKapali, readiness.BlokajNedenleri);
    }

    [IntegrationFact]
    public async Task HariciMuhasebeSistemiUblDisabledIkenEtkilenmezVeHazirSayilir()
    {
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        var controller = CreateController(dbContext, identityDbContext, isSuperAdmin: true, isKurumAdmin: false, currentKurumId: null,
            ublOptions: new EBelgeUblOptions { Enabled = false });
        var dto = Dto(EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi, true, new DateTime(2020, 1, 1));
        await controller.Update(_kurumId, dto with { HariciSistemKodu = "ERP-1" }, CancellationToken.None);

        var readiness = AsOk(await controller.GetReadiness(_kurumId, CancellationToken.None))!;

        Assert.False(readiness.UblFeatureUygulanabilirMi); // yerel UBL bu yöntemde N/A
        Assert.True(readiness.IslemeHazirMi);
        Assert.DoesNotContain(EBelgeKurumReadinessKodlari.UblFeatureKapali, readiness.BlokajNedenleri);
    }

    [IntegrationFact]
    public async Task OzelEntegratorMethodOperasyonelDegildir()
    {
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        var controller = CreateController(dbContext, identityDbContext, isSuperAdmin: true, isKurumAdmin: false, currentKurumId: null);

        // OzelEntegrator henüz operasyonel OLMADIĞINDAN Update ZATEN reddeder (mevcut
        // ValidateHedefDurum davranışı) - readiness'in `Yontemler` LİSTESİ üzerinden, POLİTİKA
        // hiç yazılmadan da capability'nin doğru yansıdığı doğrulanır.
        var readiness = AsOk(await controller.GetReadiness(_kurumId, CancellationToken.None))!;

        var ozelEntegrator = readiness.Yontemler.Single(y => y.Yontem == EBelgeEntegrasyonYontemi.OzelEntegrator);
        Assert.False(ozelEntegrator.OperasyonelMi);
    }

    [IntegrationFact]
    public async Task DogrudanGibMethodOperasyonelDegildir()
    {
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        var controller = CreateController(dbContext, identityDbContext, isSuperAdmin: true, isKurumAdmin: false, currentKurumId: null);

        var readiness = AsOk(await controller.GetReadiness(_kurumId, CancellationToken.None))!;

        var dogrudanGib = readiness.Yontemler.Single(y => y.Yontem == EBelgeEntegrasyonYontemi.DogrudanGib);
        Assert.False(dogrudanGib.OperasyonelMi);
    }

    [IntegrationFact]
    public async Task GlobalAktivasyonTarihiOncesindeGibPortalReadyDegildir()
    {
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        await SetKurumSellerFieldsAsync();

        // Politika, GEÇMİŞTEKİ bir global tarihle (yazma anında SERBEST) GibPortal + Aktif olarak kaydedilir.
        var writeController = CreateController(dbContext, identityDbContext, isSuperAdmin: true, isKurumAdmin: false, currentKurumId: null);
        await writeController.Update(_kurumId, Dto(EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1)), CancellationToken.None);

        // Readiness, GELECEKTEKİ bir global minimum tarihiyle DEĞERLENDİRİLİR (henüz gelmemiş).
        await using var readinessDbContext = CreateDbContext();
        var readinessController = CreateController(
            readinessDbContext, identityDbContext, isSuperAdmin: true, isKurumAdmin: false, currentKurumId: null,
            processingOptions: new EBelgeProcessingOptions { Enabled = true, NotBeforeLocalDate = "2099-01-01" });

        var readiness = AsOk(await readinessController.GetReadiness(_kurumId, CancellationToken.None))!;

        Assert.False(readiness.GlobalProcessingAktifMi);
        Assert.False(readiness.IslemeHazirMi);
        Assert.Contains(EBelgeKurumReadinessKodlari.GlobalIslemKapisiKapali, readiness.BlokajNedenleri);
    }

    [IntegrationFact]
    public async Task GlobalAktivasyonTarihiSonrasindaGecerliPolitikaVeVeriIslemeHazirOlabilir()
    {
        await using var dbContext = CreateDbContext();
        await using var identityDbContext = CreateIdentityDbContext();
        await SetKurumSellerFieldsAsync();
        var controller = CreateController(dbContext, identityDbContext, isSuperAdmin: true, isKurumAdmin: false, currentKurumId: null,
            processingOptions: new EBelgeProcessingOptions { Enabled = true, NotBeforeLocalDate = "2020-01-01" },
            ublOptions: new EBelgeUblOptions { Enabled = true });
        await controller.Update(_kurumId, Dto(EBelgeEntegrasyonYontemi.GibPortal, true, new DateTime(2020, 1, 1)), CancellationToken.None);

        var readiness = AsOk(await controller.GetReadiness(_kurumId, CancellationToken.None))!;

        Assert.True(readiness.GlobalProcessingAktifMi);
        Assert.True(readiness.IslemeHazirMi);
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        private readonly Guid _userId;
        public FakeCurrentUserAccessor(Guid userId) => _userId = userId;
        public string? GetCurrentUserName() => "integration-test";
        public Guid? GetCurrentUserId() => _userId;
    }

    private sealed class FakeCurrentTenantAccessor : ICurrentTenantAccessor
    {
        private readonly int? _currentKurumId;
        private readonly bool _isSuperAdmin;
        private readonly bool _isKurumAdmin;

        public FakeCurrentTenantAccessor(int? currentKurumId, bool isSuperAdmin, bool isKurumAdmin)
        {
            _currentKurumId = currentKurumId;
            _isSuperAdmin = isSuperAdmin;
            _isKurumAdmin = isKurumAdmin;
        }

        public int? GetCurrentKurumId() => _currentKurumId;
        public IReadOnlyList<int> GetAccessibleKurumIds() => _currentKurumId.HasValue ? [_currentKurumId.Value] : [];
        public bool IsSuperAdmin() => _isSuperAdmin;
        public bool IsKurumAdmin() => _isKurumAdmin;
    }
}
