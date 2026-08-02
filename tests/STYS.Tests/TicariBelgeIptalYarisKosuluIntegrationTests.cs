using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariHareketler.Mapping;
using STYS.Muhasebe.CariHareketler.Repositories;
using STYS.Muhasebe.CariHareketler.Services;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.CariKartlar.Mapping;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeDonemleri.Mapping;
using STYS.Muhasebe.MuhasebeDonemleri.Repositories;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Mapping;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Mapping;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Mapping;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Repositories;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// c799337 sonrası görev: "ticari belge iptalindeki yarış koşullarını kapat" - ISatisBelgesiService.
/// OperasyonelIptalEtAsync'in artık transaction-dışı bir GetByIdAsync ön kontrolüne DEĞİL, ortak
/// iptal transaction'ı içinde WITH (UPDLOCK, ROWLOCK) ile alınan GÜNCEL bir DB okumasına dayandığını,
/// ve cari hareket kapaması ile belge iptalinin AYNI satır üzerinde uyumlu kilitleme disipliniyle
/// (SatisBelgesiService.IptalEtCariHareketleriAsync ↔ CariHareketKapamaService.
/// TahsilatOdemeIcinCariHareketOlusturVeKapatAsync) birbirini dışladığını GERÇEK SQL Server'a karşı,
/// İKİ AYRI DbContext ile GERÇEKTEN eşzamanlı çalıştırarak kanıtlayan hedefli entegrasyon testleri.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class TicariBelgeIptalYarisKosuluIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "TBIYARIS-944";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _gelirHesapId;
    private int _kdvSatisHesapId;
    private int _musteriKartId;
    private int _musteriHesapId;

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

        var gelirHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GelirSatis, "GELIR", _tesisId);
        var kdvSatisHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVHesaplanan, "KDVS", _tesisId);
        var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "MUS", _tesisId);
        dbContext.MuhasebeHesapPlanlari.AddRange(gelirHesap, kdvSatisHesap, musteriHesap);
        await dbContext.SaveChangesAsync();
        _gelirHesapId = gelirHesap.Id;
        _kdvSatisHesapId = kdvSatisHesap.Id;
        _musteriHesapId = musteriHesap.Id;

        var musteri = SatisBelgesiMuhasebeTestSupport.BuildCariKart(_uniqueSuffix, "MUS", CariKartTipleri.Musteri, _tesisId, musteriHesap.Id);
        dbContext.CariKartlar.Add(musteri);
        await dbContext.SaveChangesAsync();
        _musteriKartId = musteri.Id;
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString) || _kurumId <= 0)
        {
            return;
        }

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await dbContext.MuhasebeHesapBakiyeleri.Where(x => x.TesisId == _tesisId).ExecuteDeleteAsync();
        await dbContext.KurumFaturaNumaraSayaclari.Where(x => x.KurumId == _kurumId).ExecuteDeleteAsync();

        // TahsilatOdemeBelgeleri.KapatilacakCariHareketId -> CariHareketler Restrict FK ile bağlı -
        // bu yüzden cari hareketlerden (SatisBelgesiMuhasebeTestSupport.CleanupAsync içinde
        // silinir) ÖNCE tahsilat/ödeme belgeleri silinmelidir.
        await dbContext.TahsilatOdemeBelgeleri
            .Where(x => x.BelgeNo.Contains(_uniqueSuffix))
            .ExecuteDeleteAsync();

        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix, _tesisId, _kurumId, _ilId);
    }

    // ─────────────────────────────────────────────────────────────
    // Yardımcılar
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// SatisBelgesiLockluOkumaHelper'ın ürettiği kilitli SELECT ([muhasebe].[SatisBelgeleri]
    /// WITH (UPDLOCK, ROWLOCK)) SQL Server'a GÖNDERİLMEDEN HEMEN ÖNCE (yalnızca İLK eşleşen
    /// komutta) iki tarafı bariyerde durdurup AYNI ANDA serbest bırakan bir DbCommandInterceptor -
    /// PosTahsilatValorIntegrationTests.PosValorSelectBarrierInterceptor ile AYNI, mevcut kod
    /// tabanı kalıbı. Bu, yarışın Task.WhenAll/Task.Run'ın rastgele zamanlamasına değil, GERÇEK
    /// SQL Server satır kilidi rekabetine dayanmasını sağlar (Task.Delay/sleep KULLANILMAZ).
    /// </summary>
    private sealed class SatisBelgesiSelectBarrierInterceptor : DbCommandInterceptor
    {
        private readonly SemaphoreSlim _gate;
        private readonly CountdownEvent _hazir;
        private bool _tetiklendi;

        public SatisBelgesiSelectBarrierInterceptor(SemaphoreSlim gate, CountdownEvent hazir)
        {
            _gate = gate;
            _hazir = hazir;
        }

        public override async ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command,
            CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (!_tetiklendi
                && command.CommandText.Contains("SatisBelgeleri", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("UPDLOCK", StringComparison.OrdinalIgnoreCase))
            {
                _tetiklendi = true;
                _hazir.Signal();
                await _gate.WaitAsync(cancellationToken);
            }

            return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private static StysAppDbContext CreateDbContextWithInterceptor(IInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseSqlServer(SatisBelgesiMuhasebeTestSupport.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;
        return new StysAppDbContext(
            options,
            new SatisBelgesiMuhasebeTestSupport.FakeCurrentUserAccessor(),
            new SatisBelgesiMuhasebeTestSupport.FakeCurrentTenantAccessor());
    }

    private CreateSatisBelgesiRequest YeniHizmetBelgesiRequest() => new()
    {
        BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
        BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
        TesisId = _tesisId,
        CariKartId = _musteriKartId,
        BelgeTarihi = new DateTime(2026, 3, 1),
        Satirlar =
        [
            new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1, Aciklama = "Hizmet satiri", SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                Miktar = 1, BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)STYS.Muhasebe.Kdv.Enums.KdvUygulamaTipi.Kdvli, KdvOrani = 20m
            }
        ]
    };

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<SatisBelgesiProfile>();
            cfg.AddProfile<MuhasebeDonemProfile>();
            cfg.AddProfile<MuhasebeFisProfile>();
            cfg.AddProfile<TahsilatOdemeBelgesiProfile>();
            cfg.AddProfile<CariKartProfile>();
            cfg.AddProfile<CariHareketProfile>();
        }, NullLoggerFactory.Instance);
        return config.CreateMapper();
    }

    private static ICariHareketKapamaService CreateCariHareketKapamaService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var tahsilatRepo = new TahsilatOdemeBelgesiRepository(dbContext, mapper);
        var cariHareketRepo = new CariHareketRepository(dbContext, mapper);
        var muhasebeDonemService = CreateRealMuhasebeDonemService(dbContext);
        return new CariHareketKapamaService(
            dbContext, tahsilatRepo, cariHareketRepo, muhasebeDonemService,
            new SatisBelgesiMuhasebeTestSupport.FakeUserAccessScopeService(), mapper);
    }

    private static IMuhasebeDonemService CreateRealMuhasebeDonemService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var repo = new MuhasebeDonemRepository(dbContext, mapper);
        return new MuhasebeDonemService(repo, mapper, dbContext, new SatisBelgesiMuhasebeTestSupport.FakeMuhasebeTesisScopeService());
    }

    /// <summary>
    /// Yalnızca BEKLENEN bir reddi (BaseException) yakalar ve (false, mesaj) döner; başarıda
    /// (true, null) döner. Başka bir istisna tipi (ör. NullReferenceException, veri tutarsızlığı)
    /// KOŞULSUZ YUTULMAZ - yukarı fırlatılır, test gerçek bir hatayı "beklenen yarış kaybı" sanıp
    /// gizlemez.
    /// </summary>
    private static async Task<(bool Basarili, string? HataMesaji)> SafeCallAsync(Func<Task> action)
    {
        try
        {
            await action();
            return (true, null);
        }
        catch (BaseException ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task<(int IadeBelgesiId, int AsilSatirId)> SeedSatisIadeBelgesiAsync()
    {
        await using var setupCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(setupCtx);

        var asilCreated = await service.CreateAsync(YeniHizmetBelgesiRequest());
        await service.MuhasebeOnayinaGonderAsync(asilCreated.Id!.Value, CancellationToken.None);
        await service.MuhasebeOnaylaAsync(asilCreated.Id.Value, CancellationToken.None);
        if (!await setupCtx.KurumFaturaNumaraSayaclari.AnyAsync(x => x.KurumId == _kurumId && x.MaliYil == 2026 && x.SeriKodu == "YAR"))
        {
            setupCtx.KurumFaturaNumaraSayaclari.Add(new KurumFaturaNumaraSayaci
            {
                KurumId = _kurumId,
                MaliYil = 2026,
                SeriKodu = "YAR",
                SonNumara = 0,
                AktifMi = true
            });
            await setupCtx.SaveChangesAsync();
        }
        await service.FaturaKesAsync(asilCreated.Id.Value, new FaturaKesRequest { SeriKodu = "YAR" }, CancellationToken.None);

        var asil = await setupCtx.SatisBelgeleri
            .AsNoTracking()
            .Include(x => x.Satirlar)
            .SingleAsync(x => x.Id == asilCreated.Id.Value);
        var asilSatirId = asil.Satirlar.Single().Id;

        var iadeCreated = await service.CreateAsync(new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-IAD-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisIadeFaturasi,
            TesisId = _tesisId,
            CariKartId = _musteriKartId,
            BelgeTarihi = new DateTime(2026, 3, 12),
            KarsiTarafFaturaNo = $"MUS-{Guid.NewGuid():N}"[..20],
            IadeEdilenBelgeId = asil.Id,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1,
                    Aciklama = "Iade",
                    Miktar = 1,
                    BirimFiyat = 1000m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
                    KdvOrani = 20m,
                    KaynakSatirId = asilSatirId.ToString()
                }
            ]
        });

        return (iadeCreated.Id!.Value, asilSatirId);
    }

    private static UpdateSatisBelgesiRequest CreateIadeReferansiKaldirmaRequest()
        => new()
        {
            IadeEdilenBelgeReferansiKaldir = true
        };

    // ─────────────────────────────────────────────────────────────
    // 1: Operasyonel iptal ile MuhasebeOnaylaAsync GERÇEKTEN eşzamanlı yarışır - yalnızca biri
    // başarılı olur, karışık/otoriter-olmayan bir durum asla oluşmaz
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task OperasyonelIptal_OnaylamaIleGercektenEszamanliYarisirsa_YalnizcaBiriBasariliVeKarisikDurumOlusmaz()
    {
        // Belge MuhasebeDurumu=Onayda durumunda başlar (Onaylandi DEĞİL) - hem operasyonel iptal
        // (mali etkisi HENÜZ doğmamış, Onayda bunu tetiklemez) hem MuhasebeOnaylaAsync bu belgeyi
        // AYNI ANDA hedefleyebilir.
        await using var setupCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var setupSatisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(setupCtx);
        var created = await setupSatisService.CreateAsync(YeniHizmetBelgesiRequest());
        await setupSatisService.MuhasebeOnayinaGonderAsync(created.Id!.Value);

        var oncekiBelge = await setupCtx.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == created.Id);
        Assert.Equal(TicariBelgeMuhasebeDurumu.Onayda, oncekiBelge.MuhasebeDurumu);

        // Deterministik bariyer: her iki taraf da kilitli SELECT'i SQL Server'a göndermeden HEMEN
        // önce senkronize olur - yarış Task.Run'ın rastgele zamanlamasına DEĞİL, GERÇEK satır
        // kilidi rekabetine dayanır (Task.Delay/sleep KULLANILMAZ).
        using var gate = new SemaphoreSlim(0, 2);
        using var hazirSayaci = new CountdownEvent(2);
        var interceptorA = new SatisBelgesiSelectBarrierInterceptor(gate, hazirSayaci);
        var interceptorB = new SatisBelgesiSelectBarrierInterceptor(gate, hazirSayaci);

        await using var ctx1 = CreateDbContextWithInterceptor(interceptorA);
        await using var ctx2 = CreateDbContextWithInterceptor(interceptorB);
        var satisServiceA = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(ctx1);
        var satisServiceB = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(ctx2);

        var taskA = Task.Run(() => SafeCallAsync(() => satisServiceA.OperasyonelIptalEtAsync(created.Id!.Value, CancellationToken.None)));
        var taskB = Task.Run(() => SafeCallAsync(() => satisServiceB.MuhasebeOnaylaAsync(created.Id!.Value, CancellationToken.None)));

        var ikisiDeHazir = hazirSayaci.Wait(TimeSpan.FromSeconds(10));
        if (!ikisiDeHazir)
        {
            gate.Release(2);
            Assert.Fail("Ön-koşul ihlali: iki taraf da beklenen sürede kilitli SELECT'e ulaşmadı.");
        }
        gate.Release(2);

        var (aBasarili, aHata) = await taskA;
        var (bBasarili, bHata) = await taskB;

        // İki işlem aynı anda başarılı OLAMAZ, ama en az biri başarılı OLMALIDIR (satır kilidi
        // ikisini de reddetmez - yalnızca sırayla işler).
        Assert.False(aBasarili && bBasarili, $"İkisi de başarılı olamaz. A={aBasarili} ({aHata}), B={bBasarili} ({bHata})");
        Assert.True(aBasarili || bBasarili, $"En az biri başarılı olmalı. A={aHata}, B={bHata}");

        await using var verifyCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var belgeDb = await verifyCtx.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == created.Id);
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(verifyCtx);

        if (bBasarili)
        {
            // Onay kazandı: operasyonel iptal (kilitli/güncel okuması artık MuhasebeDurumu=
            // Onaylandi'yi GÖRÜR) reddedilmiş olmalı - belge NET şekilde onaylanmış durumda kalır,
            // İptal ile karışık bir kombinasyon oluşmaz.
            Assert.False(aBasarili);
            Assert.Contains("operasyon ekranından iptal edilemez", aHata);

            Assert.Equal(TicariBelgeDurumu.Hazir, belgeDb.TicariDurum);
            Assert.Equal(TicariBelgeMuhasebeDurumu.Onaylandi, belgeDb.MuhasebeDurumu);

            // Onay kazandığı için fiş oluşturma da (ardından, ayrı bir adım olarak) SERBEST
            // bırakılmalıdır - belge iptal edilmemiş, mali etkisi tam olarak ilerleyebilmelidir.
            var fisDto = await fisService.MuhasebeFisiOlusturAsync(created.Id!.Value, CancellationToken.None);
            Assert.True(fisDto.MuhasebeFisId.HasValue);
        }
        else
        {
            // İptal kazandı: onay (kilitli/güncel okuması artık TicariDurum=IptalEdildi'yi GÖRÜR)
            // reddedilmiş olmalı - belge NET şekilde iptal edilmiş durumda kalır, üç otoriter alan
            // (TicariDurum/MuhasebeDurumu/FaturalamaDurumu) TUTARLI şekilde IptalEdildi'dir.
            Assert.True(aBasarili);
            Assert.False(bBasarili);
            Assert.Contains("Sadece Muhasebe Onayında durumundaki belgeler onaylanabilir", bHata);

            Assert.Equal(TicariBelgeDurumu.IptalEdildi, belgeDb.TicariDurum);
            Assert.Equal(TicariBelgeMuhasebeDurumu.IptalEdildi, belgeDb.MuhasebeDurumu);
            Assert.Equal(TicariBelgeFaturalamaDurumu.IptalEdildi, belgeDb.FaturalamaDurumu);

            // Onay zaten reddedildiği için MuhasebeDurumu hiçbir zaman Onaylandi'ya ulaşmamıştır -
            // bu yüzden ardından denenen fiş oluşturma da (farklı bir gerekçeyle) reddedilmelidir.
            await Assert.ThrowsAsync<BaseException>(
                () => fisService.MuhasebeFisiOlusturAsync(created.Id!.Value, CancellationToken.None));
        }
    }

    [IntegrationFact]
    public async Task SatisIadeBelgesiGuncellemeVeOnayaGonderme_EszamanliCalisirsa_YalnizcaBiriBasarili()
    {
        var (iadeBelgesiId, _) = await SeedSatisIadeBelgesiAsync();

        using var gate = new SemaphoreSlim(0, 2);
        using var hazirSayaci = new CountdownEvent(2);
        var interceptorA = new SatisBelgesiSelectBarrierInterceptor(gate, hazirSayaci);
        var interceptorB = new SatisBelgesiSelectBarrierInterceptor(gate, hazirSayaci);

        await using var ctx1 = CreateDbContextWithInterceptor(interceptorA);
        await using var ctx2 = CreateDbContextWithInterceptor(interceptorB);
        var satisServiceA = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(ctx1);
        var satisServiceB = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(ctx2);

        var taskA = Task.Run(() => SafeCallAsync(() => satisServiceA.UpdateAsync(iadeBelgesiId, CreateIadeReferansiKaldirmaRequest(), CancellationToken.None)));
        var taskB = Task.Run(() => SafeCallAsync(() => satisServiceB.MuhasebeOnayinaGonderAsync(iadeBelgesiId, CancellationToken.None)));

        var ikisiDeHazir = hazirSayaci.Wait(TimeSpan.FromSeconds(10));
        if (!ikisiDeHazir)
        {
            gate.Release(2);
            Assert.Fail("Ön-koşul ihlali: iki taraf da beklenen sürede kilitli SELECT'e ulaşmadı.");
        }
        gate.Release(2);

        var (aBasarili, aHata) = await taskA;
        var (bBasarili, bHata) = await taskB;

        Assert.True(aBasarili ^ bBasarili, $"Tam olarak biri başarılı olmalı. A={aBasarili} ({aHata}), B={bBasarili} ({bHata})");

        await using var verifyCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var belgeDb = await verifyCtx.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == iadeBelgesiId);

        if (aBasarili)
        {
            Assert.False(bBasarili);
            Assert.Contains("iade edilen belge referansı zorunludur", bHata, StringComparison.OrdinalIgnoreCase);
            Assert.Null(belgeDb.IadeEdilenBelgeId);
            Assert.Equal(TicariBelgeDurumu.Taslak, belgeDb.TicariDurum);
            Assert.Equal(TicariBelgeMuhasebeDurumu.Bekliyor, belgeDb.MuhasebeDurumu);
            Assert.False(belgeDb.IsDeleted);
        }
        else
        {
            Assert.False(aBasarili);
            Assert.Contains("Muhasebe Onayında durumundaki belgeler", aHata, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(belgeDb.IadeEdilenBelgeId);
            Assert.Equal(TicariBelgeDurumu.Hazir, belgeDb.TicariDurum);
            Assert.Equal(TicariBelgeMuhasebeDurumu.Onayda, belgeDb.MuhasebeDurumu);
            Assert.False(belgeDb.IsDeleted);
        }
    }

    [IntegrationFact]
    public async Task TaslakBelgeSilmeVeOnayaGonderme_EszamanliCalisirsa_YalnizcaBiriBasarili()
    {
        await using var setupCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(setupCtx);
        var created = await service.CreateAsync(YeniHizmetBelgesiRequest());

        using var gate = new SemaphoreSlim(0, 2);
        using var hazirSayaci = new CountdownEvent(2);
        var interceptorA = new SatisBelgesiSelectBarrierInterceptor(gate, hazirSayaci);
        var interceptorB = new SatisBelgesiSelectBarrierInterceptor(gate, hazirSayaci);

        await using var ctx1 = CreateDbContextWithInterceptor(interceptorA);
        await using var ctx2 = CreateDbContextWithInterceptor(interceptorB);
        var satisServiceA = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(ctx1);
        var satisServiceB = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(ctx2);

        var taskA = Task.Run(() => SafeCallAsync(() => satisServiceA.DeleteAsync(created.Id!.Value, CancellationToken.None)));
        var taskB = Task.Run(() => SafeCallAsync(() => satisServiceB.MuhasebeOnayinaGonderAsync(created.Id!.Value, CancellationToken.None)));

        var ikisiDeHazir = hazirSayaci.Wait(TimeSpan.FromSeconds(10));
        if (!ikisiDeHazir)
        {
            gate.Release(2);
            Assert.Fail("Ön-koşul ihlali: iki taraf da beklenen sürede kilitli SELECT'e ulaşmadı.");
        }
        gate.Release(2);

        var (aBasarili, aHata) = await taskA;
        var (bBasarili, bHata) = await taskB;

        Assert.True(aBasarili ^ bBasarili, $"Tam olarak biri başarılı olmalı. A={aBasarili} ({aHata}), B={bBasarili} ({bHata})");

        await using var verifyCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var belgeDb = await verifyCtx.SatisBelgeleri.IgnoreQueryFilters().FirstAsync(x => x.Id == created.Id);

        if (aBasarili)
        {
            Assert.False(bBasarili);
            Assert.Contains("Satış belgesi bulunamadı", bHata, StringComparison.OrdinalIgnoreCase);
            Assert.True(belgeDb.IsDeleted);
        }
        else
        {
            Assert.False(aBasarili);
            Assert.Contains("silinemez", aHata, StringComparison.OrdinalIgnoreCase);
            Assert.False(belgeDb.IsDeleted);
            Assert.Equal(TicariBelgeDurumu.Hazir, belgeDb.TicariDurum);
            Assert.Equal(TicariBelgeMuhasebeDurumu.Onayda, belgeDb.MuhasebeDurumu);
        }
    }

    [IntegrationFact]
    public async Task BaskaDbContextBelgeyiDegistirdiktenSonra_TrackedBelgeIleMuhasebeOnayaGondermeEskiVeriyiKullanamaz()
    {
        var (iadeBelgesiId, _) = await SeedSatisIadeBelgesiAsync();

        await using var ctx1 = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var tracked = await ctx1.SatisBelgeleri.Include(x => x.Satirlar).SingleAsync(x => x.Id == iadeBelgesiId);
        Assert.NotNull(tracked.IadeEdilenBelgeId);

        await using (var ctx2 = SatisBelgesiMuhasebeTestSupport.CreateDbContext())
        {
            var service2 = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(ctx2);
            await service2.UpdateAsync(iadeBelgesiId, CreateIadeReferansiKaldirmaRequest(), CancellationToken.None);
        }

        var service1 = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(ctx1);
        var ex = await Assert.ThrowsAsync<BaseException>(() => service1.MuhasebeOnayinaGonderAsync(iadeBelgesiId, CancellationToken.None));
        Assert.Contains("iade edilen belge referansı zorunludur", ex.Message, StringComparison.OrdinalIgnoreCase);

        var belgeDb = await ctx1.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == iadeBelgesiId);
        Assert.Null(belgeDb.IadeEdilenBelgeId);
        Assert.Equal(TicariBelgeDurumu.Taslak, belgeDb.TicariDurum);
        Assert.Equal(TicariBelgeMuhasebeDurumu.Bekliyor, belgeDb.MuhasebeDurumu);
        Assert.False(belgeDb.IsDeleted);
    }

    // ─────────────────────────────────────────────────────────────
    // 2: Cari kapama ile belge iptali eşzamanlı çalışırsa yalnızca biri başarılı olur
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task CariKapamaIleBelgeIptali_EszamanliCalisirsaYalnizcaBiriBasarili()
    {
        await using var setupCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (satisServiceSetup, muhasebeFisServiceSetup) = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiServiceWithMuhasebeFisIptal(setupCtx);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisServiceSetup, YeniHizmetBelgesiRequest());

        var fisServiceSetup = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(setupCtx);
        var fisDto = await fisServiceSetup.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);
        await muhasebeFisServiceSetup.OnaylaAsync(fisDto.MuhasebeFisId!.Value, CancellationToken.None);

        var cariHareketId = (await setupCtx.CariHareketler.AsNoTracking()
            .SingleAsync(x => x.KaynakId == onaylanmis.Id && x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi)).Id;

        var tahsilatBelge = new TahsilatOdemeBelgesi
        {
            BelgeNo = $"THS-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTarihi = new DateTime(2026, 3, 10),
            BelgeTipi = TahsilatOdemeBelgeTipleri.Tahsilat,
            CariKartId = _musteriKartId,
            Tutar = onaylanmis.GenelToplam,
            ParaBirimi = "TRY",
            OdemeYontemi = OdemeYontemleri.Nakit,
            KapatilacakCariHareketId = cariHareketId,
            Durum = TahsilatOdemeBelgeDurumlari.Aktif
        };
        setupCtx.TahsilatOdemeBelgeleri.Add(tahsilatBelge);
        await setupCtx.SaveChangesAsync();

        await using var ctx1 = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await using var ctx2 = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (satisServiceA, _) = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiServiceWithMuhasebeFisIptal(ctx1);
        var kapamaServiceB = CreateCariHareketKapamaService(ctx2);

        var taskA = SafeCallAsync(() => satisServiceA.IptalEtAsync(onaylanmis.Id!.Value, CancellationToken.None));
        var taskB = SafeCallAsync(async () =>
            await kapamaServiceB.TahsilatOdemeIcinCariHareketOlusturVeKapatAsync(tahsilatBelge.Id, CancellationToken.None));

        var (aBasarili, aHata) = await taskA;
        var (bBasarili, bHata) = await taskB;

        Assert.True(aBasarili ^ bBasarili, $"Tam olarak biri başarılı olmalı. A={aBasarili} ({aHata}), B={bBasarili} ({bHata})");

        await using var verifyCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var cariHareketDb = await verifyCtx.CariHareketler.AsNoTracking().SingleAsync(x => x.Id == cariHareketId);
        var yetimKapamaVarMi = await verifyCtx.CariHareketler.AsNoTracking()
            .AnyAsync(x => x.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && x.KaynakId == tahsilatBelge.Id);
        var fisDb = await verifyCtx.MuhasebeFisler.AsNoTracking().FirstAsync(x => x.Id == fisDto.MuhasebeFisId!.Value);
        var belgeDb = await verifyCtx.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == onaylanmis.Id);

        if (aBasarili)
        {
            // İptal kazandı: cari hareket İptal olur, kapama YETİM olarak oluşmamıştır (B'nin
            // kendi kilitli okuması, iptal commit olduktan SONRA GÜNCEL - artık Aktif olmayan -
            // durumu görüp reddetmiştir).
            Assert.Equal(CariHareketDurumlari.Iptal, cariHareketDb.Durum);
            Assert.False(yetimKapamaVarMi);
            Assert.Contains("aktif", bHata, StringComparison.OrdinalIgnoreCase);

            Assert.Equal(MuhasebeFisDurumlari.Iptal, fisDb.Durum);
            Assert.True(fisDb.TersKayitFisId.HasValue);
            Assert.Equal(TicariBelgeDurumu.IptalEdildi, belgeDb.TicariDurum);
        }
        else
        {
            // Kapama kazandı: cari hareket kapalı/kısmi kapalı kalır, iptal reddedilmiştir -
            // fiş/belge/stok TAMAMEN tutarlı (iptal transaction'ı tam geri alınmış) kalır.
            Assert.True(cariHareketDb.KapandiMi || cariHareketDb.KapananTutar > 0m);
            Assert.True(yetimKapamaVarMi);
            Assert.Contains("kapat", aHata, StringComparison.OrdinalIgnoreCase);

            Assert.Equal(MuhasebeFisDurumlari.Onayli, fisDb.Durum);
            Assert.False(fisDb.TersKayitFisId.HasValue);
            Assert.False(await verifyCtx.MuhasebeFisler.AsNoTracking().AnyAsync(x => x.IptalEdilenFisId == fisDb.Id));
            Assert.Equal(TicariBelgeDurumu.Hazir, belgeDb.TicariDurum);
            Assert.Equal(TicariBelgeMuhasebeDurumu.Onaylandi, belgeDb.MuhasebeDurumu);
        }

        // İkinci bir "temizlik" denemesi olarak: iptal edilmiş bir hareketin sonradan
        // kapatılması VEYA kapatılmış bir hareketin iptal edilmesi mümkün OLMAMALI - hangi taraf
        // kazanmış olursa olsun, KAYBEDEN tarafın işlemi TEKRAR denendiğinde de aynı şekilde
        // reddedildiğini doğrula (idempotent ret, veri tutarsızlığı yaratmaz). YENİ, taze bir
        // DbContext kullanılır - ctx1/ctx2'nin EF change tracker'ı, İLK (rollback ile geri alınmış)
        // denemeden kalma bayat izlenen örnekler barındırabilir; production'da da her istek YENİ
        // bir scoped DbContext ile gelir, aynı (başarısız) DbContext asla tekrar kullanılmaz.
        await using var retryCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        if (aBasarili)
        {
            var kapamaServiceRetry = CreateCariHareketKapamaService(retryCtx);
            var tekrarKapama = await SafeCallAsync(async () =>
                await kapamaServiceRetry.TahsilatOdemeIcinCariHareketOlusturVeKapatAsync(tahsilatBelge.Id, CancellationToken.None));
            Assert.False(tekrarKapama.Basarili);
        }
        else
        {
            var (satisServiceRetry, _) = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiServiceWithMuhasebeFisIptal(retryCtx);
            var tekrarIptal = await SafeCallAsync(() => satisServiceRetry.IptalEtAsync(onaylanmis.Id!.Value, CancellationToken.None));
            Assert.False(tekrarIptal.Basarili);
            Assert.Contains("kapat", tekrarIptal.HataMesaji!, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 3: Aynı tahsilat belgesi için eşzamanlı İKİ çağrı - kısmi kapamada yalnızca TEK kapama
    // hareketi oluşur, KalanTutar yalnızca BİR kez azalır, yetim/mükerrer kapama oluşmaz
    // ─────────────────────────────────────────────────────────────

    [IntegrationFact]
    public async Task IkiEsZamanliAyniTahsilatCagrisi_KismiKapamadaYalnizcaTekKapamaHareketiOlusturur()
    {
        await using var setupCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (satisServiceSetup, muhasebeFisServiceSetup) = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiServiceWithMuhasebeFisIptal(setupCtx);
        var onaylanmis = await SatisBelgesiMuhasebeTestSupport.OlusturVeMuhasebeOnaylaAsync(satisServiceSetup, YeniHizmetBelgesiRequest());

        var fisServiceSetup = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(setupCtx);
        var fisDto = await fisServiceSetup.MuhasebeFisiOlusturAsync(onaylanmis.Id!.Value, CancellationToken.None);
        await muhasebeFisServiceSetup.OnaylaAsync(fisDto.MuhasebeFisId!.Value, CancellationToken.None);

        var cariHareketId = (await setupCtx.CariHareketler.AsNoTracking()
            .SingleAsync(x => x.KaynakId == onaylanmis.Id && x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi)).Id;

        // Kısmi kapama - hareketin tamamını KAPATMAYACAK bir tutar seçilir; iki eşzamanlı çağrının
        // İKİSİ DE başarılı olsaydı KalanTutar İKİ kez düşerdi (mükerrer kapama) - bu test tam
        // olarak bunu engeller.
        var kismiTutar = Math.Round(onaylanmis.GenelToplam / 3m, 2);
        var tahsilatBelge = new TahsilatOdemeBelgesi
        {
            BelgeNo = $"THS-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTarihi = new DateTime(2026, 3, 10),
            BelgeTipi = TahsilatOdemeBelgeTipleri.Tahsilat,
            CariKartId = _musteriKartId,
            Tutar = kismiTutar,
            ParaBirimi = "TRY",
            OdemeYontemi = OdemeYontemleri.Nakit,
            KapatilacakCariHareketId = cariHareketId,
            Durum = TahsilatOdemeBelgeDurumlari.Aktif
        };
        setupCtx.TahsilatOdemeBelgeleri.Add(tahsilatBelge);
        await setupCtx.SaveChangesAsync();

        // AYNI tahsilat belgesi için İKİ AYRI DbContext üzerinden, dışarıdan ambient transaction
        // VERİLMEDEN (doğrudan) eşzamanlı çağrı - bir çift-tıklama/yeniden-deneme senaryosunu
        // simüle eder (bkz. görev 1: CariHareketKapamaService artık kendi transaction'ını açar).
        await using var ctx1 = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await using var ctx2 = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var kapamaServiceA = CreateCariHareketKapamaService(ctx1);
        var kapamaServiceB = CreateCariHareketKapamaService(ctx2);

        var taskA = Task.Run(() => SafeCallAsync(async () =>
            await kapamaServiceA.TahsilatOdemeIcinCariHareketOlusturVeKapatAsync(tahsilatBelge.Id, CancellationToken.None)));
        var taskB = Task.Run(() => SafeCallAsync(async () =>
            await kapamaServiceB.TahsilatOdemeIcinCariHareketOlusturVeKapatAsync(tahsilatBelge.Id, CancellationToken.None)));

        var (aBasarili, aHata) = await taskA;
        var (bBasarili, bHata) = await taskB;

        Assert.True(aBasarili ^ bBasarili, $"Tam olarak biri başarılı olmalı. A={aBasarili} ({aHata}), B={bBasarili} ({bHata})");
        Assert.Contains("daha önce oluşturulmuş", (aBasarili ? bHata : aHata)!, StringComparison.OrdinalIgnoreCase);

        await using var verifyCtx = SatisBelgesiMuhasebeTestSupport.CreateDbContext();

        // Yalnızca TEK bir kapama hareketi oluşmuş olmalı - yetim/mükerrer kapama YOK.
        var kapamaHareketleri = await verifyCtx.CariHareketler.AsNoTracking()
            .Where(x => x.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && x.KaynakId == tahsilatBelge.Id)
            .ToListAsync();
        Assert.Single(kapamaHareketleri);

        // KalanTutar yalnızca BİR kez azalmış olmalı (iki başarılı çağrı olsaydı iki kat düşerdi).
        var hedefHareket = await verifyCtx.CariHareketler.AsNoTracking().SingleAsync(x => x.Id == cariHareketId);
        Assert.Equal(onaylanmis.GenelToplam - kismiTutar, hedefHareket.KalanTutar);
        Assert.Equal(kismiTutar, hedefHareket.KapananTutar);
        Assert.False(hedefHareket.KapandiMi);
    }
}
