using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Iller.Entities;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.Tesisler.Entities;
using STYS.TicariBelgeler.Services;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Operasyonel ui/ticari-belgeler ekranı (TicariBelgeService) ile Muhasebe Satış/Alış Belgeleri
/// ekranının (SatisBelgeleriController -> ISatisBelgesiService) AYNI belge üzerinde, GERÇEK SQL
/// Server'a karşı çalışan GERÇEK durum geçişleriyle uyumlu olduğunu kanıtlayan hedefli entegrasyon
/// testleri. Önceki sürümde bu dosya her çağrıyı sessizce kabul eden bir sahte (fake)
/// ISatisBelgesiService kullanıyordu - bu, delegasyonun VAROLDUĞUNU kanıtlıyordu ama gerçek durum
/// makinesinin (Taslak -> Onayda -> Onaylandi) doğru işlediğini KANITLAMIYORDU. Bu sürüm
/// SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService ile gerçek bir SatisBelgesiService
/// örneği kullanır; TicariBelgeService bu örneği SARARAK (facade) operasyon tarafını temsil eder.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class MuhasebeOnayAkisiUyumlulukTests : IAsyncLifetime
{
    private const string TestMarker = "ONAYAKIS-771";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _musteriKartId;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return;
        }

        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (kurum, il, tesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, _uniqueSuffix);
        _kurumId = kurum.Id;
        _ilId = il.Id;
        _tesisId = tesis.Id;

        var musteriKart = SatisBelgesiMuhasebeTestSupport.BuildCariKart(
            _uniqueSuffix, "MUS", CariKartTipleri.Musteri, _tesisId, muhasebeHesapPlaniId: null);
        dbContext.CariKartlar.Add(musteriKart);
        await dbContext.SaveChangesAsync();
        _musteriKartId = musteriKart.Id;
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString) || _kurumId <= 0)
        {
            return;
        }

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix, _tesisId, _kurumId, _ilId);
    }

    private CreateSatisBelgesiRequest BuildRequest(SatisBelgesiTipi belgeTipi) => new()
    {
        BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
        BelgeTipi = belgeTipi,
        TesisId = _tesisId,
        CariKartId = _musteriKartId,
        BelgeTarihi = new DateTime(2026, 3, 1),
        MusteriAdSoyad = "Test Musteri " + _uniqueSuffix,
        Satirlar =
        [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Test satir", Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)STYS.Muhasebe.Kdv.Enums.KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]
    };

    private sealed class UnscopedUserAccessScopeService : IUserAccessScopeService
    {
        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(DomainAccessScope.Unscoped());
    }

    [IntegrationFact]
    public async Task OperasyonEkraniOnayaGonderir_MuhasebeEkraniOnaylar_BelgeOnaylandiOlurVeFisYetenegiDogruHesaplanir()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisBelgesiService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        // Operasyon (ui/ticari-belgeler) ekranını temsil eden gerçek TicariBelgeService — gerçek
        // ISatisBelgesiService'i SARAR, kendi durum makinesini KOPYALAMAZ (bkz. ITicariBelgeService
        // XML doc'u: MuhasebeOnaylaAsync/ReddetAsync/MuhasebeFisiOlusturAsync bilinçli olarak
        // SUNULMAZ).
        var operasyonServisi = new TicariBelgeService(
            satisBelgesiService,
            taslakOlusturmaService: null!,
            new UnscopedUserAccessScopeService(),
            mapper: null!);

        var created = await satisBelgesiService.CreateAsync(BuildRequest(SatisBelgesiTipi.SatisFaturasi));
        var belgeId = created.Id!.Value;

        // Taslak/Bekliyor -> Onayda (operasyon ekranı üzerinden)
        await operasyonServisi.MuhasebeOnayinaGonderAsync(belgeId);

        var onaydakiBelge = await satisBelgesiService.GetByIdAsync(belgeId);
        Assert.Equal(TicariBelgeMuhasebeDurumu.Onayda, onaydakiBelge.MuhasebeDurumu);
        Assert.True(onaydakiBelge.MuhasebeOnaylanabilirMi);
        Assert.True(onaydakiBelge.ReddedilebilirMi);
        Assert.False(onaydakiBelge.MuhasebeFisiOlusturulabilirMi);

        // Onayda -> Onaylandi (muhasebe ekranı üzerinden — AYNI Id, AYNI ISatisBelgesiService)
        await satisBelgesiService.MuhasebeOnaylaAsync(belgeId);

        var onaylananBelge = await satisBelgesiService.GetByIdAsync(belgeId);
        Assert.Equal(TicariBelgeMuhasebeDurumu.Onaylandi, onaylananBelge.MuhasebeDurumu);
        Assert.False(onaylananBelge.MuhasebeOnaylanabilirMi);
        Assert.False(onaylananBelge.ReddedilebilirMi);
        Assert.Null(onaylananBelge.MuhasebeFisId);
        Assert.True(onaylananBelge.MuhasebeFisiOlusturulabilirMi);
    }

    [IntegrationFact]
    public async Task FaturaTaslagi_OnaylandiOlsaBileMuhasebeFisiOlusturmaEndpointiReddeder()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisBelgesiService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);
        var muhasebeFisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext);

        var onaylanan = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(
            satisBelgesiService, BuildRequest(SatisBelgesiTipi.FaturaTaslagi));

        Assert.Equal(TicariBelgeMuhasebeDurumu.Onaylandi, onaylanan.MuhasebeDurumu);

        // FaturaTaslagi allowlist'te DEĞİLDİR - MuhasebeDurumu=Onaylandi olsa dahi
        // MuhasebeFisiOlusturulabilirMi=false olmalı ve doğrudan servis çağrısı REDDEDİLMELİDİR.
        Assert.False(onaylanan.MuhasebeFisiOlusturulabilirMi);

        var ex = await Assert.ThrowsAsync<BaseException>(
            () => muhasebeFisService.MuhasebeFisiOlusturAsync(onaylanan.Id!.Value));
        Assert.Equal(400, ex.ErrorCode);

        await SatisBelgesiMuhasebeTestSupport.AssertHicMuhasebeKaydiOlusmadiAsync(dbContext, onaylanan.Id!.Value);
    }
}
