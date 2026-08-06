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
        Guid? currentUserId = null) =>
        new(
            new EBelgeKurumPolitikaYonetimServisi(
                dbContext,
                new EBelgeYontemYetenekSaglayici(),
                Options.Create(new EBelgeProcessingOptions { Enabled = true, NotBeforeLocalDate = "2020-01-01" }),
                TimeProvider.System),
            dbContext,
            identityDbContext,
            new FakeCurrentUserAccessor(currentUserId ?? Guid.NewGuid()),
            new FakeCurrentTenantAccessor(currentKurumId, isSuperAdmin, isKurumAdmin));

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
