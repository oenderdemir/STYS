using System.Security.Cryptography;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.CariKartlar.Mapping;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeDonemleri.Mapping;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Mapping;
using STYS.Muhasebe.SatisBelgeleri.Repositories;
using STYS.Muhasebe.SatisBelgeleri.Services;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.4.2: kesim öncesi UBL kapısını ve gerçek V2 snapshot üretimini, gerçek SQL Server'a
/// karşı, gerçek FaturaKesAsync akışıyla uçtan uca doğrular. Saf kural matrisi için bkz.
/// EBelgeUblPreCutValidatorTests (DB'siz, izole).
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
[Trait("Domain", "EBelge")]
[Trait("TestLevel", "SqlIntegration")]
[Trait("Dependency", "SqlServer")]
public class EBelgeUblPreCutIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "EBF-PCV";
    private const string SeriKodu = "PCV";

    /// <summary>14.09.2026 go-live sonrası, saf kanal/kapsam/mali/otoriter-alan reddi senaryolarında tarih kapısının hiç devreye girmemesi için kullanılır.</summary>
    private static readonly DateTimeOffset AfterGoLiveUtc = new(2026, 9, 20, 9, 0, 0, TimeSpan.Zero);

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _kurumsalTamId;
    private int _gercekKisiEksikSoyadId;
    private int _eFaturaId;

    public async Task InitializeAsync()
    {
        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (kurum, il, tesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(dbContext, _uniqueSuffix);
        _kurumId = kurum.Id;
        _ilId = il.Id;
        _tesisId = tesis.Id;

        kurum.Ilce = "Kadıköy";
        kurum.Il = "İstanbul";
        await dbContext.SaveChangesAsync();

        var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "MUS", _tesisId);
        var gelirHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GelirSatis, "GELIR", _tesisId);
        var kdvHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVHesaplanan, "KDV", _tesisId);
        dbContext.MuhasebeHesapPlanlari.AddRange(musteriHesap, gelirHesap, kdvHesap);
        await dbContext.SaveChangesAsync();

        var kurumsalTam = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "KRT", CariKartTipleri.KurumsalMusteri, _tesisId, musteriHesap.Id);
        kurumsalTam.EArsivKapsamindaMi = true;
        kurumsalTam.VergiNoTckn = "1234567890";
        kurumsalTam.Ilce = "Beşiktaş";
        kurumsalTam.Il = "İstanbul";

        var gercekKisiEksikSoyad = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "GKS", CariKartTipleri.Musteri, _tesisId, musteriHesap.Id);
        gercekKisiEksikSoyad.EArsivKapsamindaMi = true;
        gercekKisiEksikSoyad.VergiNoTckn = "11111111110";
        gercekKisiEksikSoyad.Ad = "Ayşe";
        gercekKisiEksikSoyad.Soyad = null;
        gercekKisiEksikSoyad.Ilce = "Beşiktaş";
        gercekKisiEksikSoyad.Il = "İstanbul";

        var eFatura = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "EFT", CariKartTipleri.KurumsalMusteri, _tesisId, musteriHesap.Id);
        eFatura.EFaturaMukellefiMi = true;
        eFatura.VergiNoTckn = "1234567890";
        eFatura.Ilce = "Beşiktaş";
        eFatura.Il = "İstanbul";

        dbContext.CariKartlar.AddRange(kurumsalTam, gercekKisiEksikSoyad, eFatura);
        await dbContext.SaveChangesAsync();
        _kurumsalTamId = kurumsalTam.Id;
        _gercekKisiEksikSoyadId = gercekKisiEksikSoyad.Id;
        _eFaturaId = eFatura.Id;

        dbContext.MuhasebeDonemler.Add(new STYS.Muhasebe.MuhasebeDonemleri.Entities.MuhasebeDonem
        {
            TesisId = _tesisId,
            MaliYil = 2026,
            DonemNo = 1,
            BaslangicTarihi = new DateTime(2026, 1, 1),
            BitisTarihi = new DateTime(2026, 12, 31),
            KapaliMi = false
        });

        dbContext.KurumFaturaNumaraSayaclari.Add(new KurumFaturaNumaraSayaci
        {
            KurumId = _kurumId,
            MaliYil = 2026,
            SeriKodu = SeriKodu,
            SonNumara = 0,
            AktifMi = true
        });

        await dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await dbContext.KurumFaturaNumaraSayaclari.Where(x => x.KurumId == _kurumId).ExecuteDeleteAsync();
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix, _tesisId, _kurumId, _ilId);
    }

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<SatisBelgesiProfile>();
            cfg.AddProfile<MuhasebeDonemProfile>();
            cfg.AddProfile<CariKartProfile>();
        }, NullLoggerFactory.Instance);

        return config.CreateMapper();
    }

    private static ISatisBelgesiService CreateService(StysAppDbContext dbContext, TimeProvider timeProvider, bool ubloptionsEnabled = true)
    {
        var mapper = CreateMapper();
        var satisBelgesiRepository = new SatisBelgesiRepository(dbContext, mapper);
        var muhasebeFisRepository = new STYS.Muhasebe.MuhasebeFisleri.Repositories.MuhasebeFisRepository(dbContext, mapper);
        return new SatisBelgesiService(
            satisBelgesiRepository,
            dbContext,
            mapper,
            muhasebeFisRepository,
            null!,
            new SatisBelgesiMuhasebeTestSupport.FakeUserAccessScopeService(),
            NullLogger<SatisBelgesiService>.Instance,
            new SatisBelgesiMuhasebeTestSupport.NoOpDomainOperationLogger(),
            timeProvider,
            Options.Create(new EBelgeUblOptions { Enabled = ubloptionsEnabled }),
            kurumPolitikaServisi: EBelgeKurumPolitikaTestSupport.CreateAlwaysAktifServisi(dbContext, timeProvider));
    }

    private static StysAppDbContext CreateDbContext(int? currentKurumId = null, bool isSuperAdmin = true)
    {
        var optionsBuilder = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseSqlServer(SatisBelgesiMuhasebeTestSupport.ConnectionString);

        return new StysAppDbContext(
            optionsBuilder.Options,
            new SatisBelgesiMuhasebeTestSupport.FakeCurrentUserAccessor(),
            new TestTenantAccessor(currentKurumId, isSuperAdmin));
    }

    private CreateSatisBelgesiRequest BuildRequest(int cariKartId, CreateSatisBelgesiSatiriRequest? satir = null)
        => new()
        {
            BelgeNo = TruncateToMax($"{_uniqueSuffix}-{Guid.NewGuid():N}", 40),
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            TesisId = _tesisId,
            CariKartId = cariKartId,
            BelgeTarihi = new DateTime(2026, 9, 15),
            Satirlar =
            [
                satir ?? new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1,
                    Aciklama = "Dar kapsam satırı",
                    SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                    Miktar = 1,
                    BirimFiyat = 1000m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
                    KdvOrani = 20m
                }
            ]
        };

    private async Task<int> PrepareReadyInvoiceAsync(CreateSatisBelgesiRequest request)
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, TimeProvider.System, ubloptionsEnabled: false);

        var created = await service.CreateAsync(request, CancellationToken.None);
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value, CancellationToken.None);
        await service.MuhasebeOnaylaAsync(created.Id.Value, CancellationToken.None);

        var donemService = SatisBelgesiMuhasebeTestSupport.CreateRealMuhasebeDonemService(dbContext);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(dbContext, donemService);
        await fisService.MuhasebeFisiOlusturAsync(created.Id.Value, CancellationToken.None);

        return created.Id.Value;
    }

    private async Task<int> GetSayacOnceAsync()
    {
        await using var verifyCtx = CreateDbContext();
        return await verifyCtx.KurumFaturaNumaraSayaclari
            .AsNoTracking()
            .Where(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == SeriKodu)
            .Select(x => x.SonNumara)
            .SingleAsync();
    }

    private async Task AssertNoCutArtifactsAsync(int satisBelgesiId, int expectedSayac)
    {
        await using var verifyCtx = CreateDbContext();

        var belge = await verifyCtx.SatisBelgeleri.AsNoTracking().SingleAsync(x => x.Id == satisBelgesiId);

        Assert.Null(belge.ResmiFaturaNo);
        Assert.Null(belge.FaturaKesimTarihi);
        Assert.Equal(TicariBelgeFaturalamaDurumu.KesimBekliyor, belge.FaturalamaDurumu);
        Assert.False(await verifyCtx.EBelgeKayitlari.IgnoreQueryFilters().AnyAsync(x => x.SatisBelgesiId == satisBelgesiId));

        var sayacSonra = await verifyCtx.KurumFaturaNumaraSayaclari
            .AsNoTracking()
            .SingleAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == SeriKodu);

        Assert.Equal(expectedSayac, sayacSonra.SonNumara);
    }

    // 3 + 5. e-Fatura kanalı desteklenmeyen kapsam olarak reddedilir; sayaç sorgulanmaz/değişmez.
    [IntegrationFact]
    public async Task EFaturaKanaliDesteklenmeyenKapsamOlarakReddedilirVeSayacDegismez()
    {
        var belgeId = await PrepareReadyInvoiceAsync(BuildRequest(_eFaturaId));
        var sayacOnce = await GetSayacOnceAsync();

        await using var cutCtx = CreateDbContext();
        var cutService = CreateService(cutCtx, new FakeTimeProvider(AfterGoLiveUtc));

        var ex = await Assert.ThrowsAsync<EBelgeUblScopeUnsupportedException>(
            () => cutService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None));

        Assert.Equal(EBelgeUblScopeUnsupportedException.HttpStatusCode, ex.ErrorCode);
        await AssertNoCutArtifactsAsync(belgeId, sayacOnce);
    }

    // 4 + 17 + 18 + 19 + 20 + 21 + 22. e-Arşiv kanalı kabul edilir; V2 snapshot doğru üretilir.
    [IntegrationFact]
    public async Task EArsivKanaliKabulEdilirVeV2SnapshotDogruUretilir()
    {
        var belgeId = await PrepareReadyInvoiceAsync(BuildRequest(_kurumsalTamId));

        var kesimUtc = new DateTimeOffset(2026, 9, 20, 9, 15, 30, TimeSpan.Zero);
        await using var cutCtx = CreateDbContext();
        var cutService = CreateService(cutCtx, new FakeTimeProvider(kesimUtc));

        var cut = await cutService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None);
        Assert.NotNull(cut.ResmiFaturaNo);

        await using var verifyCtx = CreateDbContext();
        var snapshot = await verifyCtx.EBelgeSnapshots
            .AsNoTracking()
            .Include(x => x.EBelgeKaydi)
            .SingleAsync(x => x.EBelgeKaydi.SatisBelgesiId == belgeId);

        Assert.Equal("2", snapshot.SnapshotSchemaVersion);

        // V1 reader'ın kabul ETMEYECEĞİ, V2 reader'ın kabul EDECEĞİ payload (bkz. görev kısıt md.6).
        var payloadBytes = System.Text.Encoding.UTF8.GetBytes(snapshot.CanonicalJson);
        var v2 = new EBelgeCanonicalSnapshotV2Reader().Read(payloadBytes, snapshot.CanonicalSha256);

        Assert.Equal("EARSIVFATURA", v2.Belge.ProfileID);
        Assert.Equal("SATIS", v2.Belge.InvoiceTypeCode);
        Assert.Equal(new DateOnly(2026, 9, 20), v2.Belge.FaturaTarihiTrt);
        Assert.Equal(new TimeOnly(12, 15, 30), v2.Belge.FaturaSaatiTrt); // UTC+3
        Assert.Equal("C62", v2.Satirlar[0].BirimKodu);
        Assert.Equal("Adet", v2.Satirlar[0].Birim);
        Assert.Equal("İstanbul", v2.Kurum.Il);
        Assert.Equal("Kadıköy", v2.Kurum.Ilce);
        Assert.Equal("İstanbul", v2.Alici.Il);

        // 22. Saklanan JSON, saklanan exact UTF-8 byte dizisi ve SHA-256 birbiriyle eşleşir.
        var yenidenHesaplananHash = Convert.ToHexString(SHA256.HashData(payloadBytes));
        Assert.Equal(yenidenHesaplananHash, snapshot.CanonicalSha256.ToUpperInvariant());
    }

    // 6. SatisFaturasi dışındaki belge tipi reddedilir - bkz. EBelgeUblPreCutValidatorTests
    // (SatisFaturasiDisindaBelgeTipiReddedilir), gerçek AlisIadeFaturasi kurulumu ayrı bir asıl
    // AlisFaturasi gerektirdiğinden burada tekrarlanmaz.

    // 7 + 8. TRY dışındaki para birimi / kur 1 dışındaki değer, EnsureUblHazirlikKaynaklari
    // tarafından (feature flag'den bağımsız, dar kapsam kapısından ÖNCE) zaten reddedilir.
    [IntegrationFact]
    public async Task TryDisindaParaBirimiReddedilir()
    {
        var belgeId = await PrepareReadyInvoiceAsync(BuildRequest(_kurumsalTamId));
        await using (var mutateCtx = CreateDbContext())
        {
            await mutateCtx.SatisBelgeleri.Where(x => x.Id == belgeId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ParaBirimi, "USD"));
        }
        var sayacOnce = await GetSayacOnceAsync();

        await using var cutCtx = CreateDbContext();
        var cutService = CreateService(cutCtx, new FakeTimeProvider(AfterGoLiveUtc));

        await Assert.ThrowsAsync<BaseException>(
            () => cutService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None));

        await AssertNoCutArtifactsAsync(belgeId, sayacOnce);
    }

    // 9. Birimi Adet olmayan satır reddedilir.
    [IntegrationFact]
    public async Task AdetDisindaBirimliSatirReddedilir()
    {
        var satir = new CreateSatisBelgesiSatiriRequest
        {
            SiraNo = 1,
            Aciklama = "Kutu satırı",
            SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
            Miktar = 1,
            Birim = "Kutu",
            BirimFiyat = 1000m,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
            KdvOrani = 20m
        };
        var belgeId = await PrepareReadyInvoiceAsync(BuildRequest(_kurumsalTamId, satir));
        var sayacOnce = await GetSayacOnceAsync();

        await using var cutCtx = CreateDbContext();
        var cutService = CreateService(cutCtx, new FakeTimeProvider(AfterGoLiveUtc));

        var ex = await Assert.ThrowsAsync<EBelgeUblScopeUnsupportedException>(
            () => cutService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None));

        Assert.Equal(EBelgeUblScopeUnsupportedException.HttpStatusCode, ex.ErrorCode);
        await AssertNoCutArtifactsAsync(belgeId, sayacOnce);
    }

    // 11. Tevkifatlı satır reddedilir. Tevkifatlı bir satırı MuhasebeFisiOlusturAsync'ten (ki
    // gerçek tevkifat hesap eşlemesi seed ister) GEÇİRMEDEN test etmek için: belge standart
    // KDV'li bir satırla dar kapsam kesim öncesi hazır hale getirilir, ardından satırın
    // tevkifat alanları DOĞRUDAN veritabanında (fiş oluşturma adımından SONRA) değiştirilir -
    // yalnız kesim öncesi UBL kapısının reddi test edilir, muhasebe fişi pipeline'ı değil.
    [IntegrationFact]
    public async Task TevkifatliSatirReddedilir()
    {
        var belgeId = await PrepareReadyInvoiceAsync(BuildRequest(_kurumsalTamId));
        await using (var mutateCtx = CreateDbContext())
        {
            await mutateCtx.SatisBelgesiSatirlari.Where(x => x.SatisBelgesiId == belgeId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.TevkifatPay, 9)
                    .SetProperty(x => x.TevkifatPayda, 10)
                    .SetProperty(x => x.TevkifatTutari, 16.2m));
        }
        var sayacOnce = await GetSayacOnceAsync();

        await using var cutCtx = CreateDbContext();
        var cutService = CreateService(cutCtx, new FakeTimeProvider(AfterGoLiveUtc));

        await Assert.ThrowsAsync<EBelgeUblScopeUnsupportedException>(
            () => cutService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None));

        await AssertNoCutArtifactsAsync(belgeId, sayacOnce);
    }

    // 12. Ek vergi (konaklama vergisi) içeren satır reddedilir - bkz. TevkifatliSatirReddedilir
    // yorumu: muhasebe fişi pipeline'ından (ÖTV/ÖİV/konaklama hesap eşlemesi ister) bağımsız
    // olması için satır DOĞRUDAN veritabanında, fiş oluşturulduktan SONRA değiştirilir.
    [IntegrationFact]
    public async Task KonaklamaVergisiIcerenSatirReddedilir()
    {
        var belgeId = await PrepareReadyInvoiceAsync(BuildRequest(_kurumsalTamId));
        await using (var mutateCtx = CreateDbContext())
        {
            await mutateCtx.SatisBelgesiSatirlari.Where(x => x.SatisBelgesiId == belgeId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.KonaklamaVergisiOrani, 2m)
                    .SetProperty(x => x.KonaklamaVergisiTutari, 20m));
        }
        var sayacOnce = await GetSayacOnceAsync();

        await using var cutCtx = CreateDbContext();
        var cutService = CreateService(cutCtx, new FakeTimeProvider(AfterGoLiveUtc));

        await Assert.ThrowsAsync<EBelgeUblScopeUnsupportedException>(
            () => cutService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None));

        await AssertNoCutArtifactsAsync(belgeId, sayacOnce);
    }

    // 13. Mali toplam uyuşmazlığı HTTP 422 ile reddedilir.
    [IntegrationFact]
    public async Task MaliToplamUyusmazligi422IleReddedilir()
    {
        var belgeId = await PrepareReadyInvoiceAsync(BuildRequest(_kurumsalTamId));
        await using (var mutateCtx = CreateDbContext())
        {
            await mutateCtx.SatisBelgeleri.Where(x => x.Id == belgeId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ToplamMatrah, 999999m));
        }
        var sayacOnce = await GetSayacOnceAsync();

        await using var cutCtx = CreateDbContext();
        var cutService = CreateService(cutCtx, new FakeTimeProvider(AfterGoLiveUtc));

        var ex = await Assert.ThrowsAsync<EBelgeUblMonetaryTotalMismatchException>(
            () => cutService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None));

        Assert.Equal(EBelgeUblMonetaryTotalMismatchException.HttpStatusCode, ex.ErrorCode);
        await AssertNoCutArtifactsAsync(belgeId, sayacOnce);
    }

    // 14. Kurumsal alıcıda eksik VKN reddedilir.
    [IntegrationFact]
    public async Task KurumsalAlicidaEksikVknReddedilir()
    {
        // Kurumsal müşteri için VKN, ValidateCreateRequestAsync tarafından oluşturma anında ZATEN
        // zorunlu kılınıyor (bkz. _kurumsalEksikVknId ile CreateAsync denemesi "Kurumsal müşteri
        // için vergi numarası zorunludur" ile başarısız olur - önden savunma). E-belge kapısının
        // KENDİ kontrolünü izole test etmek için belge önce VKN'li kartla oluşturulur, sonra VKN
        // doğrudan veritabanında (kesim öncesi) temizlenir.
        var belgeId = await PrepareReadyInvoiceAsync(BuildRequest(_kurumsalTamId));
        await using (var mutateCtx = CreateDbContext())
        {
            await mutateCtx.SatisBelgeleri.Where(x => x.Id == belgeId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.MusteriVergiNo, (string?)null));
        }
        var sayacOnce = await GetSayacOnceAsync();

        await using var cutCtx = CreateDbContext();
        var cutService = CreateService(cutCtx, new FakeTimeProvider(AfterGoLiveUtc));

        var ex = await Assert.ThrowsAsync<EBelgeUblAuthoritativeFieldMissingException>(
            () => cutService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None));

        Assert.Equal(EBelgeUblAuthoritativeFieldMissingException.HttpStatusCode, ex.ErrorCode);
        await AssertNoCutArtifactsAsync(belgeId, sayacOnce);
    }

    // 15. Gerçek kişi alıcıda ayrı ad veya soyad eksikse reddedilir.
    [IntegrationFact]
    public async Task GercekKisiAlicidaEksikSoyadReddedilir()
    {
        var belgeId = await PrepareReadyInvoiceAsync(BuildRequest(_gercekKisiEksikSoyadId));
        var sayacOnce = await GetSayacOnceAsync();

        await using var cutCtx = CreateDbContext();
        var cutService = CreateService(cutCtx, new FakeTimeProvider(AfterGoLiveUtc));

        await Assert.ThrowsAsync<EBelgeUblAuthoritativeFieldMissingException>(
            () => cutService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None));

        await AssertNoCutArtifactsAsync(belgeId, sayacOnce);
    }

    // 16. Zorunlu yapısal adres alanı (satıcı ilçe/il) eksikse reddedilir.
    [IntegrationFact]
    public async Task SaticiYapisalAdresiEksikseReddedilir()
    {
        var belgeId = await PrepareReadyInvoiceAsync(BuildRequest(_kurumsalTamId));
        await using (var mutateCtx = CreateDbContext())
        {
            await mutateCtx.Kurumlar.Where(x => x.Id == _kurumId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Ilce, (string?)null));
        }
        var sayacOnce = await GetSayacOnceAsync();

        await using var cutCtx = CreateDbContext();
        var cutService = CreateService(cutCtx, new FakeTimeProvider(AfterGoLiveUtc));

        var ex = await Assert.ThrowsAsync<EBelgeUblAuthoritativeFieldMissingException>(
            () => cutService.FaturaKesAsync(belgeId, new FaturaKesRequest { SeriKodu = SeriKodu }, CancellationToken.None));

        Assert.Equal(EBelgeUblAuthoritativeFieldMissingException.HttpStatusCode, ex.ErrorCode);
        await AssertNoCutArtifactsAsync(belgeId, sayacOnce);
    }

    private static string TruncateToMax(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _zaman;

        public FakeTimeProvider(DateTimeOffset zaman) => _zaman = zaman;

        public override DateTimeOffset GetUtcNow() => _zaman;
    }

    private sealed class TestTenantAccessor : ICurrentTenantAccessor
    {
        private readonly int? _currentKurumId;
        private readonly bool _isSuperAdmin;

        public TestTenantAccessor(int? currentKurumId, bool isSuperAdmin)
        {
            _currentKurumId = currentKurumId;
            _isSuperAdmin = isSuperAdmin;
        }

        public int? GetCurrentKurumId() => _currentKurumId;

        public IReadOnlyList<int> GetAccessibleKurumIds() => _currentKurumId.HasValue ? [_currentKurumId.Value] : [];

        public bool IsSuperAdmin() => _isSuperAdmin;

        public bool IsKurumAdmin() => false;
    }
}
