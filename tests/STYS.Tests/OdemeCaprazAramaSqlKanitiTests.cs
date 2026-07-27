using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.OdemeIzleme.Dtos;
using STYS.Muhasebe.OdemeIzleme.Services;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

/// <summary>
/// Capraz-kaynak aramanin SAYFALAMAYI GERCEKTEN VERITABANINDA yaptigini KANITLAR.
///
/// Yontem: EF Core'un urettigi SQL yakalanir ve icinde SQL Server sayfalama sozdiziminin
/// (OFFSET ... ROWS FETCH NEXT ... ROWS ONLY) ve UNION ALL'in bulundugu dogrulanir. Ayrica
/// materyalize edilen satir sayisi (Sayfa boyutu) olcuye alinir - kaynak tablolarin tamami
/// belleye ALINMAZ.
/// </summary>
[Trait("Category", "Integration")]
public class OdemeCaprazAramaSqlKanitiTests : IAsyncLifetime
{
    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);

    private const string TestMarker = "SQLK-970";

    private readonly List<int> _tesisIdler = [];
    private readonly List<int> _kurumIdler = [];
    private readonly List<int> _illIdler = [];
    private readonly List<int> _cariKartIdler = [];
    private readonly List<int> _belgeIdler = [];

    /// <summary>Calistirilan tum SQL komutlarini toplayan interceptor.</summary>
    private sealed class SqlYakalayici : DbCommandInterceptor
    {
        public List<string> Komutlar { get; } = [];

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Komutlar.Add(command.CommandText);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Komutlar.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private static StysAppDbContext CreateDbContext(params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<StysAppDbContext>().UseSqlServer(ConnectionString);
        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }
        return new StysAppDbContext(builder.Options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor());
    }

    public Task InitializeAsync() => Task.CompletedTask;

    private List<STYS.Tests.TestSupport.CleanupAdimi> OlusturCleanupAdimlari() =>
    [
        new("TahsilatOdemeBelgeleri silme", async () =>
        {
            if (_belgeIdler.Count == 0) return;
            await using var db = CreateDbContext();
            await db.TahsilatOdemeBelgeleri.IgnoreQueryFilters().Where(x => _belgeIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("CariKartlar silme", async () =>
        {
            if (_cariKartIdler.Count == 0) return;
            await using var db = CreateDbContext();
            await db.CariKartlar.IgnoreQueryFilters().Where(x => _cariKartIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("Tesisler silme", async () =>
        {
            if (_tesisIdler.Count == 0) return;
            await using var db = CreateDbContext();
            await db.Tesisler.IgnoreQueryFilters().Where(x => _tesisIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("Iller silme", async () =>
        {
            if (_illIdler.Count == 0) return;
            await using var db = CreateDbContext();
            await db.Iller.IgnoreQueryFilters().Where(x => _illIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
        new("Kurumlar silme", async () =>
        {
            if (_kurumIdler.Count == 0) return;
            await using var db = CreateDbContext();
            await db.Kurumlar.IgnoreQueryFilters().Where(x => _kurumIdler.Contains(x.Id)).ExecuteDeleteAsync();
        }),
    ];

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return;
        }

        var hatalar = (await STYS.Tests.TestSupport.TwoPhaseCleanupRunner.CalistirAsync(OlusturCleanupAdimlari())).ToList();

        await using var db = CreateDbContext();
        var kalan = await db.Kurumlar.IgnoreQueryFilters().CountAsync(x => _kurumIdler.Contains(x.Id));
        if (kalan > 0)
        {
            hatalar.Add(new InvalidOperationException($"Cleanup sonrasi kalinti: Kurumlar={kalan}"));
        }

        if (hatalar.Count > 0)
        {
            throw new AggregateException($"[OdemeCaprazAramaSqlKanitiTests.DisposeAsync] {hatalar.Count} cleanup hatasi.", hatalar);
        }
    }

    private async Task<(int TesisId, int CariKartId, string Suffix)> SeedAsync(StysAppDbContext db, int belgeSayisi)
    {
        var suffix = $"{TestMarker}-{Guid.NewGuid():N}"[..20];

        var kurum = new STYS.Kurumlar.Entities.Kurum { Kod = suffix, Ad = "K " + suffix, AktifMi = true };
        db.Kurumlar.Add(kurum);
        var il = new STYS.Iller.Entities.Il { Ad = "I " + suffix, AktifMi = true };
        db.Iller.Add(il);
        await db.SaveChangesAsync();
        _kurumIdler.Add(kurum.Id);
        _illIdler.Add(il.Id);

        var tesis = new STYS.Tesisler.Entities.Tesis
        {
            KurumId = kurum.Id, IlId = il.Id, Ad = "T " + suffix, Telefon = "0", Adres = "A", AktifMi = true
        };
        db.Tesisler.Add(tesis);
        await db.SaveChangesAsync();
        _tesisIdler.Add(tesis.Id);

        var cari = new STYS.Muhasebe.CariKartlar.Entities.CariKart
        {
            TesisId = tesis.Id, CariTipi = STYS.Muhasebe.CariKartlar.Entities.CariKartTipleri.Musteri,
            CariKodu = suffix, UnvanAdSoyad = "C " + suffix, AktifMi = true
        };
        db.CariKartlar.Add(cari);
        await db.SaveChangesAsync();
        _cariKartIdler.Add(cari.Id);

        for (var i = 0; i < belgeSayisi; i++)
        {
            var belge = new STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities.TahsilatOdemeBelgesi
            {
                BelgeNo = $"{suffix}-{i:000}",
                BelgeTarihi = DateTime.UtcNow.Date.AddDays(-i),
                BelgeTipi = STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities.TahsilatOdemeBelgeTipleri.Tahsilat,
                CariKartId = cari.Id,
                Tutar = 100m + i,
                ParaBirimi = "TRY",
                OdemeYontemi = STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities.OdemeYontemleri.Nakit,
                Durum = STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities.TahsilatOdemeBelgeDurumlari.Aktif
            };
            db.TahsilatOdemeBelgeleri.Add(belge);
            await db.SaveChangesAsync();
            _belgeIdler.Add(belge.Id);
        }

        return (tesis.Id, cari.Id, suffix);
    }

    [IntegrationFact]
    public async Task Sayfalama_SQL_TARAFINDA_yapilir_OFFSET_FETCH_ve_UNION_ALL_uretilir()
    {
        var yakalayici = new SqlYakalayici();
        await using var db = CreateDbContext(yakalayici);
        var (tesisId, cariId, suffix) = await SeedAsync(db, belgeSayisi: 25);

        yakalayici.Komutlar.Clear();

        var svc = new OdemeCaprazAramaService(db, new FakeMuhasebeTesisScopeService([tesisId]));
        var sonuc = await svc.AraAsync(
            new PagedRequest { PageNumber = 2, PageSize = 5 },
            new OdemeCaprazAramaFilterDto { TesisId = tesisId, BelgeNo = suffix, BeklenenCariKartId = cariId, SadeceKopukOlanlar = true });

        var tumSql = string.Join("\n---\n", yakalayici.Komutlar);

        // Uretilen SQL, teslim raporunda kanit olarak gosterilebilmesi icin diske yazilir.
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "capraz-arama-sql.txt"), tumSql);

        // 1) Sayfalama SQL tarafinda: OFFSET/FETCH uretilmis olmali.
        Assert.Contains("OFFSET", tumSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FETCH NEXT", tumSql, StringComparison.OrdinalIgnoreCase);

        // 2) Kaynaklar SQL'de UNION ALL ile birlesmis olmali (bellekte degil).
        Assert.Contains("UNION ALL", tumSql, StringComparison.OrdinalIgnoreCase);

        // 3) Tekillestirme/sayim SQL tarafinda: COUNT ve GROUP BY uretilmis olmali.
        Assert.Contains("COUNT(", tumSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GROUP BY", tumSql, StringComparison.OrdinalIgnoreCase);

        // 4) Sayfa boyutu kadar sonuc doner; toplam ise TUM adaylari yansitir.
        Assert.True(sonuc.Items.Count <= 5, $"Sayfa boyutu asildi: {sonuc.Items.Count}");
        Assert.Equal(25, sonuc.TotalCount);
        Assert.Equal(2, sonuc.PageNumber);
    }

    [IntegrationFact]
    public async Task Sayfalar_arasinda_tekrar_veya_atlama_olmaz_ve_maksimum_page_size_uygulanir()
    {
        await using var db = CreateDbContext();
        var (tesisId, cariId, suffix) = await SeedAsync(db, belgeSayisi: 12);

        var svc = new OdemeCaprazAramaService(db, new FakeMuhasebeTesisScopeService([tesisId]));
        var filtre = new OdemeCaprazAramaFilterDto { TesisId = tesisId, BelgeNo = suffix, BeklenenCariKartId = cariId, SadeceKopukOlanlar = true };

        var s1 = await svc.AraAsync(new PagedRequest { PageNumber = 1, PageSize = 5 }, filtre);
        var s2 = await svc.AraAsync(new PagedRequest { PageNumber = 2, PageSize = 5 }, filtre);
        var s3 = await svc.AraAsync(new PagedRequest { PageNumber = 3, PageSize = 5 }, filtre);

        var tum = s1.Items.Concat(s2.Items).Concat(s3.Items).Select(x => x.TekillestirmeAnahtari).ToList();
        Assert.Equal(12, s1.TotalCount);
        Assert.Equal(12, tum.Count);
        Assert.Equal(12, tum.Distinct().Count()); // tekrar YOK

        // Maksimum page size kirpilir.
        var buyuk = await svc.AraAsync(new PagedRequest { PageNumber = 1, PageSize = 100_000 }, filtre);
        Assert.True(buyuk.PageSize <= 200);
    }

    [IntegrationFact]
    public async Task Daraltici_alan_verilmezse_asiri_genis_sorgu_REDDEDILIR()
    {
        await using var db = CreateDbContext();
        var (tesisId, _, _) = await SeedAsync(db, belgeSayisi: 1);

        var svc = new OdemeCaprazAramaService(db, new FakeMuhasebeTesisScopeService([tesisId]));

        var ex = await Assert.ThrowsAsync<BaseException>(() =>
            svc.AraAsync(new PagedRequest(), new OdemeCaprazAramaFilterDto { TesisId = tesisId }));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Contains("referans", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Fake'ler ──

    private sealed class FakeCurrentUserAccessor : TOD.Platform.Security.Auth.Services.ICurrentUserAccessor
    {
        public string? GetCurrentUserName() => "sql-kanit-test";
        public Guid? GetCurrentUserId() => Guid.NewGuid();
    }

    private sealed class FakeCurrentTenantAccessor : TOD.Platform.Security.Auth.Services.ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => null;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [];
        public bool IsSuperAdmin() => true;
        public bool IsKurumAdmin() => false;
    }

    private sealed class FakeMuhasebeTesisScopeService(IEnumerable<int> tesisIds) : IMuhasebeTesisScopeService
    {
        private readonly HashSet<int> _ids = tesisIds.ToHashSet();

        public Task<int[]> GetEffectiveTesisIdsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_ids.ToArray());
        public Task<int[]> GetEffectiveTesisIdsAsync(DomainAccessScope scope, CancellationToken cancellationToken = default) => Task.FromResult(_ids.ToArray());
        public Task EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken = default)
        {
            if (!_ids.Contains(tesisId)) throw new BaseException("Bu tesis için yetkiniz bulunmuyor.", 403);
            return Task.CompletedTask;
        }
    }
}
