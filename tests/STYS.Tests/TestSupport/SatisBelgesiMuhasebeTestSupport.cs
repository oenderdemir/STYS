using System.Linq.Expressions;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.AccessScope;
using STYS.Iller.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.Kdv.Entities;
using STYS.Muhasebe.MuhasebeDonemleri.Dtos;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.MuhasebeDonemleri.Mapping;
using STYS.Muhasebe.MuhasebeDonemleri.Repositories;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Mapping;
using STYS.Muhasebe.MuhasebeFisleri.Repositories;
using STYS.Muhasebe.MuhasebeFisleri.Services;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Services;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Mapping;
using STYS.Muhasebe.SatisBelgeleri.Repositories;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.Muhasebe.SatisBelgeleri.Services.MuhasebeFisStratejileri;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Dtos;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Entities;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Services;
using STYS.Muhasebe.TevkifatHesapEslemeleri.Dtos;
using STYS.Muhasebe.TevkifatHesapEslemeleri.Entities;
using STYS.Muhasebe.TevkifatHesapEslemeleri.Services;
using STYS.Tesisler.Entities;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// [Theory] için IntegrationFactAttribute karşılığı: STYS_INTEGRATION_TEST_CONNECTION_STRING
/// ortam değişkeni tanımlı değilse TÜM Theory örneklerini "Skipped" olarak işaretler — böylece
/// (IntegrationFactAttribute'un [Fact] için yaptığı gibi) atlanma durumu test çıktısında açıkça
/// görünür, sessizce "geçti" gibi raporlanmaz.
/// </summary>
public sealed class IntegrationTheoryAttribute : TheoryAttribute
{
    public IntegrationTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar)))
        {
            Skip = $"{IntegrationFactAttribute.ConnectionStringEnvVar} ortam degiskeni tanimli degil — entegrasyon testi atlandi.";
        }
    }
}

/// <summary>
/// dc861c7 (ek vergi engeli/yuvarlama), a1f25d6 (KDV istisna yön doğrulaması) ve ff66146/f7a44fe
/// (tedarikçi hesabı/transaction snapshot) düzeltmelerini GERÇEK SQL Server'a karşı, gerçek public
/// servis akışlarıyla (SatisBelgesiService, SatisBelgesiMuhasebeFisService.MuhasebeFisiOlusturAsync)
/// birlikte doğrulayan regresyon test paketinin PAYLAŞILAN altyapısı.
///
/// Bu sınıf yalnızca test kurulumu/temizliği için yardımcı metotlar ve DI'siz servis
/// oluşturma fabrikalarını barındırır — davranışın kendisini test etmez.
/// </summary>
public static class SatisBelgesiMuhasebeTestSupport
{
    public static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);

    public static StysAppDbContext CreateDbContext()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException(
                $"{IntegrationFactAttribute.ConnectionStringEnvVar} ortam degiskeni tanimli degil.");
        }

        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new StysAppDbContext(options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor());
    }

    public static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<SatisBelgesiProfile>();
            cfg.AddProfile<MuhasebeDonemProfile>();
            cfg.AddProfile<MuhasebeFisProfile>();
        }, NullLoggerFactory.Instance);
        return config.CreateMapper();
    }

    // ─────────────────────────────────────────────────────────────
    // Servis fabrikaları
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Gerçek SatisBelgesiService: CreateAsync / MuhasebeOnayinaGonderAsync / MuhasebeOnaylaAsync
    /// gibi PUBLIC akışları çalıştırmak için. IMuhasebeFisService ve IMuhasebeFisRepository bu
    /// akışlarda (belgeye zaten bağlı bir fiş yokken) hiç dereference edilmez; ilki bu yüzden null
    /// bırakılabilir (bkz. ThrowIfMuhasebeFisiIslemiEngellerAsync: MuhasebeFisId boşsa hemen döner).
    /// </summary>
    public static ISatisBelgesiService CreateSatisBelgesiService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var repository = new SatisBelgesiRepository(dbContext, mapper);
        var muhasebeFisRepository = new MuhasebeFisRepository(dbContext, mapper);

        return new SatisBelgesiService(
            repository,
            dbContext,
            mapper,
            muhasebeFisRepository,
            null!, // IMuhasebeFisService: yalnızca fiş iptali akışında kullanılır, bu testlerde hiç çağrılmaz
            new FakeUserAccessScopeService(),
            NullLogger<SatisBelgesiService>.Instance,
            new NoOpDomainOperationLogger());
    }

    /// <summary>
    /// Gerçek SatisBelgesiService - GERÇEK bir IMuhasebeFisService ile birlikte kurulur (bkz.
    /// görev: iptal/ters kayıt akışı). IptalEtAsync, bağlı bir muhasebe fişi varsa
    /// SatisBelgesiFisiIptalEtAsync üzerinden GERÇEK ters kayıt/bakiye güncelleme akışını
    /// çalıştırır - bu yüzden yalnızca-SatisBelgesiService fabrikasının aksine (IMuhasebeFisService
    /// null!), burada tam bir MuhasebeFisService (gerçek repository + gerçek
    /// MuhasebeHesapBakiyeGuncellemeService) kurulur.
    /// </summary>
    public static (ISatisBelgesiService SatisBelgesiService, IMuhasebeFisService MuhasebeFisService) CreateSatisBelgesiServiceWithMuhasebeFisIptal(
        StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var satisBelgesiRepository = new SatisBelgesiRepository(dbContext, mapper);
        var muhasebeFisRepository = new MuhasebeFisRepository(dbContext, mapper);

        var muhasebeFisService = new MuhasebeFisService(
            muhasebeFisRepository,
            mapper,
            dbContext,
            new FakeMuhasebeDonemService(),
            new MuhasebeHesapBakiyeGuncellemeService(dbContext),
            new FakeUserAccessScopeService(),
            new NoOpDomainOperationLogger());

        var satisBelgesiService = new SatisBelgesiService(
            satisBelgesiRepository,
            dbContext,
            mapper,
            muhasebeFisRepository,
            muhasebeFisService,
            new FakeUserAccessScopeService(),
            NullLogger<SatisBelgesiService>.Instance,
            new NoOpDomainOperationLogger());

        return (satisBelgesiService, muhasebeFisService);
    }

    /// <param name="tevkifatKarsiligiHesapPlaniId">
    /// Tevkifatlı stratejilerin "tevkifat karşılığı" satırı için kullanacağı MuhasebeHesapPlani.Id.
    /// GERÇEK SQL Server FK'si (FK_MuhasebeFisSatirlari_MuhasebeHesapPlanlari_MuhasebeHesapPlaniId)
    /// bu Id'nin MuhasebeHesapPlanlari tablosunda GERÇEKTEN var olmasını zorunlu kılar — InMemory
    /// sağlayıcının aksine, üretilmiş/uydurma bir Id burada FK ihlaline yol açar. Bu yüzden
    /// tevkifatlı senaryo testleri, gerçekten seed ettikleri bir hesabın Id'sini buraya vermelidir.
    /// </param>
    public static ISatisBelgesiMuhasebeFisService CreateMuhasebeFisService(
        StysAppDbContext dbContext, IMuhasebeDonemService? donemService = null, int tevkifatKarsiligiHesapPlaniId = 0)
    {
        var mapper = CreateMapper();
        var repository = new SatisBelgesiRepository(dbContext, mapper);

        var stratejiler = new List<ISatisBelgesiMuhasebeFisStratejisi>
        {
            new SatisFaturasiMuhasebeFisStratejisi(),
            new SatisIadeFaturasiMuhasebeFisStratejisi(dbContext),
            new SatisTevkifatliFaturaMuhasebeFisStratejisi(new FakeTevkifatHesapEslemeService(tevkifatKarsiligiHesapPlaniId)),
            new AlisFaturasiMuhasebeFisStratejisi(dbContext, new FakeTasinirKodMuhasebeHesapEslemeService()),
            new AlisIadeFaturasiMuhasebeFisStratejisi(dbContext, new FakeTasinirKodMuhasebeHesapEslemeService()),
            new AlisTevkifatliFaturaMuhasebeFisStratejisi(
                dbContext, new FakeTasinirKodMuhasebeHesapEslemeService(), new FakeTevkifatHesapEslemeService(tevkifatKarsiligiHesapPlaniId))
        };

        return new SatisBelgesiMuhasebeFisService(
            repository,
            dbContext,
            mapper,
            donemService ?? new FakeMuhasebeDonemService(),
            stratejiler,
            NullLogger<SatisBelgesiMuhasebeFisService>.Instance);
    }

    /// <summary>
    /// GERÇEK MuhasebeDonemService (Fake DEĞİL) — repository de gerçek MuhasebeDonemRepository'dir,
    /// gerçek MuhasebeDonemler tablosuna karşı sorgu yapar. Yalnızca IMuhasebeTesisScopeService fake'tir
    /// (EnsureCanAccessTesisAsync no-op) — bu, kullanıcı/rol bazlı erişim kapsamı bu testlerin
    /// kapsamı dışında olduğu için tercih edilmiştir; dönem SEÇME/EŞLEŞTİRME mantığının kendisi
    /// (TesisId + BelgeTarihi'nin gerçek DB'deki hangi MuhasebeDonem satırına denk düştüğü) sahte
    /// DEĞİLDİR.
    /// </summary>
    public static IMuhasebeDonemService CreateRealMuhasebeDonemService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var repo = new MuhasebeDonemRepository(dbContext, mapper);
        return new MuhasebeDonemService(repo, mapper, dbContext, new FakeMuhasebeTesisScopeService());
    }

    // ─────────────────────────────────────────────────────────────
    // Seed yardımcıları
    // ─────────────────────────────────────────────────────────────

    public static async Task<(Kurum kurum, Il il, Tesis tesis)> SeedKurumIlTesisAsync(StysAppDbContext dbContext, string uniqueSuffix)
    {
        var kurum = new Kurum { Kod = uniqueSuffix, Ad = "Test Kurum " + uniqueSuffix, AktifMi = true };
        dbContext.Kurumlar.Add(kurum);
        var il = new Il { Ad = "Test Il " + uniqueSuffix, AktifMi = true };
        dbContext.Iller.Add(il);
        await dbContext.SaveChangesAsync();

        var tesis = new Tesis
        {
            KurumId = kurum.Id,
            IlId = il.Id,
            Ad = "Test Tesis " + uniqueSuffix,
            Telefon = "0000",
            Adres = "Test Adres",
            AktifMi = true
        };
        dbContext.Tesisler.Add(tesis);
        await dbContext.SaveChangesAsync();

        return (kurum, il, tesis);
    }

    /// <summary>ID ile doğrudan eşlenen (ör. tedarikçi/müşteri) hesap — ana kod eşleşmesi aranmaz.</summary>
    public static MuhasebeHesapPlani BuildHesap(string uniqueSuffix, string etiket, int? tesisId) => new()
    {
        Kod = $"{uniqueSuffix}-{etiket}",
        TamKod = $"{uniqueSuffix}-{etiket}",
        Ad = $"Test Hesap {etiket} {uniqueSuffix}",
        HesapTipi = HesapTipi.DetayHesap,
        AktifMi = true,
        DetayHesapMi = true,
        HareketGorebilirMi = true,
        TesisId = tesisId
    };

    /// <summary>
    /// Ana kod ile aranan (gelir/gider/KDV/stok/iade) hesap. TamKod GLOBAL UNIQUE olduğundan
    /// (paylaşılan test DB'sinde ana kodun kendisiyle çakışmaması için) "{anaKod}.{benzersizEk}"
    /// şeklinde, GetHesapPlaniAsync/GetKdvHesabiAsync/ResolveHesapByAnaKodAsync'in
    /// StartsWith(anaKod + ".") dalına uyacak şekilde oluşturulur.
    /// </summary>
    public static MuhasebeHesapPlani BuildAnaKodHesap(string uniqueSuffix, string anaKod, string etiket, int? tesisId) => new()
    {
        Kod = $"{anaKod}.{uniqueSuffix}-{etiket}",
        TamKod = $"{anaKod}.{uniqueSuffix}-{etiket}",
        Ad = $"Test Hesap {etiket} {uniqueSuffix}",
        HesapTipi = HesapTipi.DetayHesap,
        AktifMi = true,
        DetayHesapMi = true,
        HareketGorebilirMi = true,
        TesisId = tesisId
    };

    public static CariKart BuildCariKart(
        string uniqueSuffix, string etiket, string cariTipi, int? tesisId, int? muhasebeHesapPlaniId, bool aktifMi = true) => new()
    {
        CariTipi = cariTipi,
        CariKodu = $"{etiket}-{uniqueSuffix}",
        UnvanAdSoyad = $"Test Cari {etiket} {uniqueSuffix}",
        AktifMi = aktifMi,
        TesisId = tesisId,
        MuhasebeHesapPlaniId = muhasebeHesapPlaniId
    };

    /// <summary>
    /// Belgeyi CreateAsync ile oluşturur, ardından MuhasebeOnayinaGonderAsync ve MuhasebeOnaylaAsync
    /// PUBLIC akışlarıyla MuhasebeOnaylandi durumuna getirir (üçü de gerçek SatisBelgesiService
    /// üzerinden) — MuhasebeFisiOlusturAsync'in gerektirdiği ön koşulu, reflection'a başvurmadan,
    /// gerçek servis metotlarını çalıştırarak sağlar.
    /// </summary>
    public static async Task<SatisBelgesiDto> OlusturVeMuhasebeOnaylaAsync(
        ISatisBelgesiService service, CreateSatisBelgesiRequest request, CancellationToken cancellationToken = default)
    {
        var created = await service.CreateAsync(request, cancellationToken);
        await service.MuhasebeOnayinaGonderAsync(created.Id!.Value, cancellationToken);
        await service.MuhasebeOnaylaAsync(created.Id!.Value, cancellationToken);
        return await service.GetByIdAsync(created.Id.Value, cancellationToken);
    }

    public static async Task AssertHicMuhasebeKaydiOlusmadiAsync(StysAppDbContext dbContext, int belgeId)
    {
        var fisIds = await dbContext.MuhasebeFisler.AsNoTracking()
            .Where(x => x.KaynakId == belgeId)
            .Select(x => x.Id)
            .ToListAsync();
        Assert.Empty(fisIds);

        // MuhasebeFisSatir, KaynakId ile değil MuhasebeFisId ile bağlıdır — üstteki kontrol
        // MuhasebeFis'in hiç oluşmadığını doğrular, ama bunu VARSAYMAK yerine burada AYRICA
        // açıkça doğrulanır: bu belgeye ait (fisIds boş olsa da) hiçbir fiş satırı yoktur.
        Assert.False(await dbContext.MuhasebeFisSatirlari.AsNoTracking().AnyAsync(x => fisIds.Contains(x.MuhasebeFisId)));

        Assert.False(await dbContext.CariHareketler.AsNoTracking().AnyAsync(x => x.KaynakId == belgeId));
        Assert.False(await dbContext.StokHareketleri.AsNoTracking().AnyAsync(x => x.KaynakId == belgeId));

        var guncelBelge = await dbContext.SatisBelgeleri.AsNoTracking().FirstOrDefaultAsync(x => x.Id == belgeId);
        Assert.NotNull(guncelBelge);
        Assert.False(guncelBelge!.MuhasebeFisId.HasValue);
    }

    // ─────────────────────────────────────────────────────────────
    // Temizlik — FK sırasına uygun, örneğe özel uniqueSuffix ile filtreli
    // ─────────────────────────────────────────────────────────────

    public static async Task CleanupAsync(
        StysAppDbContext dbContext, string uniqueSuffix, int? tesisId = null, int? kurumId = null, int? ilId = null)
    {
        // SatisBelgeleri.MuhasebeFisId -> MuhasebeFisler Restrict FK ile bağlı olduğundan,
        // SatisBelgeleri fiş satırlarından/fişlerden ÖNCE silinmeli.
        await dbContext.CariHareketler
            .Where(x => x.CariKart != null && x.CariKart.CariKodu != null && x.CariKart.CariKodu.Contains(uniqueSuffix))
            .ExecuteDeleteAsync();
        await dbContext.StokHareketleri
            .Where(x => x.BelgeNo != null && x.BelgeNo.Contains(uniqueSuffix))
            .ExecuteDeleteAsync();
        var belgeIds = await dbContext.SatisBelgeleri
            .Where(x => x.BelgeNo.Contains(uniqueSuffix))
            .Select(x => x.Id)
            .ToListAsync();

        var eBelgeKaydiIds = await dbContext.EBelgeKayitlari
            .Where(x => belgeIds.Contains(x.SatisBelgesiId))
            .Select(x => x.Id)
            .ToListAsync();

        if (eBelgeKaydiIds.Count > 0)
        {
            await dbContext.EBelgeSnapshots
                .Where(x => eBelgeKaydiIds.Contains(x.EBelgeKaydiId))
                .ExecuteDeleteAsync();

            await dbContext.EBelgeKayitlari
                .Where(x => eBelgeKaydiIds.Contains(x.Id))
                .ExecuteDeleteAsync();
        }

        var fisIds = new List<int>();
        if (belgeIds.Count > 0)
        {
            fisIds = await dbContext.MuhasebeFisler
                .Where(x => x.KaynakId != null && belgeIds.Contains(x.KaynakId.Value))
                .Select(x => x.Id)
                .ToListAsync();
            await dbContext.SatisBelgeleri.Where(x => belgeIds.Contains(x.Id)).ExecuteDeleteAsync();
        }
        if (fisIds.Count > 0)
        {
            await dbContext.MuhasebeFisSatirlari.Where(x => fisIds.Contains(x.MuhasebeFisId)).ExecuteDeleteAsync();
            await dbContext.MuhasebeFisler.Where(x => fisIds.Contains(x.Id)).ExecuteDeleteAsync();
        }

        await dbContext.KdvIstisnaTanimlari
            .Where(x => x.Kod != null && x.Kod.Contains(uniqueSuffix))
            .ExecuteDeleteAsync();

        if (tesisId.HasValue)
        {
            await dbContext.CariKartlar.Where(x => x.TesisId == tesisId.Value).ExecuteDeleteAsync();
            await dbContext.MuhasebeDonemler.Where(x => x.TesisId == tesisId.Value).ExecuteDeleteAsync();
            // Global TestMarker DEĞİL, örneğe özel uniqueSuffix ile filtrelenir — eş zamanlı çalışan
            // başka bir IntegrationFact örneğinin hâlâ canlı hesap planı kaydını silmeye çalışıp FK
            // Restrict hatası almaması için (bkz. SqlServerIntegrationCollection notu).
            await dbContext.MuhasebeHesapPlanlari
                .Where(x => x.Kod != null && x.Kod.Contains(uniqueSuffix))
                .ExecuteDeleteAsync();
            await dbContext.Tesisler.Where(x => x.Id == tesisId.Value).ExecuteDeleteAsync();
        }

        if (ilId.HasValue)
        {
            await dbContext.Iller.Where(x => x.Id == ilId.Value).ExecuteDeleteAsync();
        }

        if (kurumId.HasValue)
        {
            await dbContext.Kurumlar.Where(x => x.Id == kurumId.Value).ExecuteDeleteAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Fake'ler (yalnızca bu testlerde hiç dereference edilmeyen bağımlılıklar için)
    // ─────────────────────────────────────────────────────────────

    public sealed class FakeCurrentUserAccessor : TOD.Platform.Security.Auth.Services.ICurrentUserAccessor
    {
        public string? GetCurrentUserName() => "integration-test";
        public Guid? GetCurrentUserId() => Guid.NewGuid();
    }

    public sealed class FakeCurrentTenantAccessor : TOD.Platform.Security.Auth.Services.ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => null;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [];
        public bool IsSuperAdmin() => true;
        public bool IsKurumAdmin() => false;
    }

    public sealed class FakeUserAccessScopeService : IUserAccessScopeService
    {
        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(DomainAccessScope.Unscoped());
    }

    public sealed class FakeMuhasebeTesisScopeService : IMuhasebeTesisScopeService
    {
        public Task<int[]> GetEffectiveTesisIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<int>());

        public Task<int[]> GetEffectiveTesisIdsAsync(DomainAccessScope scope, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<int>());

        public Task EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    public sealed class NoOpDomainOperationLogger : IDomainOperationLogger
    {
        public void Started(string eventName, object payload) { }
        public void Completed(string eventName, object payload) { }
        public void Warning(string eventName, object payload) { }
        public void Failed(string eventName, Exception exception, object payload) { }
    }

    public sealed class FakeMuhasebeDonemService : IMuhasebeDonemService
    {
        public Task<MuhasebeDonemDto?> GetAktifDonemAsync(int tesisId, DateTime tarih, CancellationToken cancellationToken = default)
            => Task.FromResult<MuhasebeDonemDto?>(new MuhasebeDonemDto
            {
                Id = 1,
                TesisId = tesisId,
                MaliYil = 2026,
                DonemNo = 1,
                BaslangicTarihi = new DateTime(2026, 1, 1),
                BitisTarihi = new DateTime(2026, 12, 31),
                KapaliMi = false
            });

        public Task DonemKapatAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DonemAcAsync(int id, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IEnumerable<MuhasebeDonemDto>> GetAllAsync(Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null)
            => throw new NotImplementedException();

        public Task<MuhasebeDonemDto?> GetByIdAsync(int id, Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null)
            => throw new NotImplementedException();

        public Task<PagedResult<MuhasebeDonemDto>> GetPagedAsync(
            PagedRequest request,
            Expression<Func<MuhasebeDonem, bool>>? predicate = null,
            Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null,
            Func<IQueryable<MuhasebeDonem>, IOrderedQueryable<MuhasebeDonem>>? orderBy = null)
            => throw new NotImplementedException();

        public Task<MuhasebeDonemDto> AddAsync(MuhasebeDonemDto dto) => throw new NotImplementedException();
        public Task<MuhasebeDonemDto> UpdateAsync(MuhasebeDonemDto dto) => throw new NotImplementedException();
        public Task DeleteAsync(int id) => throw new NotImplementedException();

        public Task<IEnumerable<MuhasebeDonemDto>> WhereAsync(
            Expression<Func<MuhasebeDonem, bool>> predicate,
            Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null)
            => throw new NotImplementedException();

        public Task<bool> AnyAsync(
            Expression<Func<MuhasebeDonem, bool>> predicate,
            Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null)
            => throw new NotImplementedException();
    }

    public sealed class FakeTasinirKodMuhasebeHesapEslemeService : ITasinirKodMuhasebeHesapEslemeService
    {
        public Task<List<TasinirKodMuhasebeHesapEslemeDto>> GetByTasinirKodIdAsync(int tasinirKodId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<TasinirKodMuhasebeHesapEslemeDto?> GetVarsayilanAsync(int tasinirKodId, string malzemeTipi, string hareketTipi, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IEnumerable<TasinirKodMuhasebeHesapEslemeDto>> GetAllAsync(Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null)
            => throw new NotImplementedException();

        public Task<TasinirKodMuhasebeHesapEslemeDto?> GetByIdAsync(int id, Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null)
            => throw new NotImplementedException();

        public Task<PagedResult<TasinirKodMuhasebeHesapEslemeDto>> GetPagedAsync(
            PagedRequest request,
            Expression<Func<TasinirKodMuhasebeHesapEsleme, bool>>? predicate = null,
            Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null,
            Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IOrderedQueryable<TasinirKodMuhasebeHesapEsleme>>? orderBy = null)
            => throw new NotImplementedException();

        public Task<TasinirKodMuhasebeHesapEslemeDto> AddAsync(TasinirKodMuhasebeHesapEslemeDto dto) => throw new NotImplementedException();
        public Task<TasinirKodMuhasebeHesapEslemeDto> UpdateAsync(TasinirKodMuhasebeHesapEslemeDto dto) => throw new NotImplementedException();
        public Task DeleteAsync(int id) => throw new NotImplementedException();

        public Task<IEnumerable<TasinirKodMuhasebeHesapEslemeDto>> WhereAsync(
            Expression<Func<TasinirKodMuhasebeHesapEsleme, bool>> predicate,
            Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null)
            => throw new NotImplementedException();

        public Task<bool> AnyAsync(
            Expression<Func<TasinirKodMuhasebeHesapEsleme, bool>> predicate,
            Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null)
            => throw new NotImplementedException();
    }

    public sealed class FakeTevkifatHesapEslemeService(int hesapPlaniId) : ITevkifatHesapEslemeService
    {
        public Task<TevkifatHesapEslemeDto?> GetAktifEslemeAsync(int? tesisId, string islemYonu, int tevkifatPay, int tevkifatPayda, CancellationToken cancellationToken = default)
            => Task.FromResult<TevkifatHesapEslemeDto?>(new TevkifatHesapEslemeDto
            {
                Id = 1,
                TesisId = tesisId,
                IslemYonu = islemYonu,
                TevkifatPay = tevkifatPay,
                TevkifatPayda = tevkifatPayda,
                MuhasebeHesapPlaniId = hesapPlaniId,
                AktifMi = true
            });

        public Task<IEnumerable<TevkifatHesapEslemeDto>> GetAllAsync(int? tesisId = null, string? islemYonu = null, bool? aktifMi = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PagedResult<TevkifatHesapEslemeDto>> GetPagedAsync(PagedRequest request, int? tesisId = null, string? islemYonu = null, bool? aktifMi = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IEnumerable<TevkifatHesapEslemeDto>> GetAllAsync(Func<IQueryable<TevkifatHesapEsleme>, IQueryable<TevkifatHesapEsleme>>? include = null)
            => throw new NotImplementedException();

        public Task<TevkifatHesapEslemeDto?> GetByIdAsync(int id, Func<IQueryable<TevkifatHesapEsleme>, IQueryable<TevkifatHesapEsleme>>? include = null)
            => throw new NotImplementedException();

        public Task<PagedResult<TevkifatHesapEslemeDto>> GetPagedAsync(PagedRequest request, Expression<Func<TevkifatHesapEsleme, bool>>? predicate = null, Func<IQueryable<TevkifatHesapEsleme>, IQueryable<TevkifatHesapEsleme>>? include = null, Func<IQueryable<TevkifatHesapEsleme>, IOrderedQueryable<TevkifatHesapEsleme>>? orderBy = null)
            => throw new NotImplementedException();

        public Task<TevkifatHesapEslemeDto> AddAsync(TevkifatHesapEslemeDto dto) => throw new NotImplementedException();
        public Task<TevkifatHesapEslemeDto> UpdateAsync(TevkifatHesapEslemeDto dto) => throw new NotImplementedException();
        public Task DeleteAsync(int id) => throw new NotImplementedException();

        public Task<IEnumerable<TevkifatHesapEslemeDto>> WhereAsync(Expression<Func<TevkifatHesapEsleme, bool>> predicate, Func<IQueryable<TevkifatHesapEsleme>, IQueryable<TevkifatHesapEsleme>>? include = null)
            => throw new NotImplementedException();

        public Task<bool> AnyAsync(Expression<Func<TevkifatHesapEsleme, bool>> predicate, Func<IQueryable<TevkifatHesapEsleme>, IQueryable<TevkifatHesapEsleme>>? include = null)
            => throw new NotImplementedException();
    }
}
