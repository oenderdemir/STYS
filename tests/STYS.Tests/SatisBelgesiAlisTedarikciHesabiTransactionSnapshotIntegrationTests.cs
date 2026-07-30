using System.Reflection;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Infrastructure.EntityFramework;
using STYS.Iller.Entities;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeDonemleri.Dtos;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
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
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// SatisBelgesiMuhasebeFisService.MuhasebeFisiOlusturAsync'in transaction içi belge okumasının
/// GERÇEKTEN güncel olduğunu — DbContext'in change tracker'ında bu belge için ÖNCEDEN (örneğin
/// aynı istek kapsamında daha önceki bir okumadan) izlenen bir örnek bulunsa bile — doğrulayan
/// entegrasyon testi.
///
/// EF Core InMemory sağlayıcısı ile de aynı "stale tracked entity" identity-resolution davranışı
/// prensipte gözlemlenebilir, ancak bu senaryo GERÇEK SQL Server'a karşı, gerçek transaction
/// içinde doğrulanır — InMemory testleri (SatisBelgesiAlisTedarikciHesabiTests) bu senaryonun
/// yerine geçmez, tamamlayıcısıdır.
///
/// Çalıştırma: STYS_INTEGRATION_TEST_CONNECTION_STRING tanımlı değilse otomatik atlanır.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class SatisBelgesiAlisTedarikciHesabiTransactionSnapshotIntegrationTests
{
    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);

    private const string TestMarker = "TXSNAP-641";

    [IntegrationFact]
    public async Task MuhasebeFisiOlusturAsync_DbContextteEskiTedarikciOncedenTrackliyken_TransactionIcindeGuncelTedarikciKullanilir()
    {
        var uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..28];
        var kurumId = 0;
        var tesisId = 0;

        try
        {
            await using var seedContext = CreateDbContext();

            var kurum = new Kurum { Kod = uniqueSuffix, Ad = "Test Kurum " + uniqueSuffix, AktifMi = true };
            seedContext.Kurumlar.Add(kurum);
            var il = new Il { Ad = "Test Il " + uniqueSuffix, AktifMi = true };
            seedContext.Iller.Add(il);
            await seedContext.SaveChangesAsync();
            kurumId = kurum.Id;

            var tesis = new Tesis
            {
                KurumId = kurum.Id,
                IlId = il.Id,
                Ad = "Test Tesis " + uniqueSuffix,
                Telefon = "0000",
                Adres = "Test Adres",
                AktifMi = true
            };
            seedContext.Tesisler.Add(tesis);
            await seedContext.SaveChangesAsync();
            tesisId = tesis.Id;

            // hesap1/hesap2: tedarikci karta DOGRUDAN Id ile baglanir (ana kod eslesmesi
            // gerekmez), ama TesisId'nin belge tesisiyle eslesmesi ARANIR (bkz.
            // GetCariMuhasebeHesabiAsync'in son hesapPlani sorgusu).
            var hesap1 = SeedHesap(uniqueSuffix, "TED1", tesis.Id);
            var hesap2 = SeedHesap(uniqueSuffix, "TED2", tesis.Id);
            // gider/kdv/stok hesaplari ANA KOD ile aranir (GetHesapPlaniAsync/GetKdvHesabiAsync);
            // TamKod GLOBAL UNIQUE oldugundan (paylasilan test DB'sinde ana kodun kendisiyle
            // CAKISMAMASI icin) "{anaKod}.{benzersizEk}" seklinde, StartsWith(anaKod + ".")
            // dalina uyacak sekilde olusturulur.
            var giderHesap = SeedAnaKodHesap(uniqueSuffix, MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, "GIDER", tesis.Id);
            var kdvHesap = SeedAnaKodHesap(uniqueSuffix, MuhasebeAnaHesapKodlari.KDVIndirilecek, "KDV", tesis.Id);
            var stokHesap = SeedAnaKodHesap(uniqueSuffix, MuhasebeAnaHesapKodlari.StokTicariMal, "STOK", tesis.Id);
            seedContext.MuhasebeHesapPlanlari.AddRange(hesap1, hesap2, giderHesap, kdvHesap, stokHesap);
            await seedContext.SaveChangesAsync();

            var tedarikci1 = new CariKart
            {
                CariTipi = CariKartTipleri.Tedarikci,
                CariKodu = $"T1-{uniqueSuffix}",
                UnvanAdSoyad = "Tedarikci Bir " + uniqueSuffix,
                AktifMi = true,
                TesisId = tesis.Id,
                MuhasebeHesapPlaniId = hesap1.Id
            };
            var tedarikci2 = new CariKart
            {
                CariTipi = CariKartTipleri.Tedarikci,
                CariKodu = $"T2-{uniqueSuffix}",
                UnvanAdSoyad = "Tedarikci Iki " + uniqueSuffix,
                AktifMi = true,
                TesisId = tesis.Id,
                MuhasebeHesapPlaniId = hesap2.Id
            };
            seedContext.CariKartlar.AddRange(tedarikci1, tedarikci2);
            await seedContext.SaveChangesAsync();

            var belge = new SatisBelgesi
            {
                BelgeNo = $"BLG-{uniqueSuffix}",
                BelgeTipi = SatisBelgesiTipi.AlisFaturasi,
                Durum = SatisBelgesiDurumu.MuhasebeOnaylandi,
                TesisId = tesis.Id,
                CariKartId = tedarikci1.Id,
                BelgeTarihi = new DateTime(2026, 1, 15)
            };
            belge.Satirlar.Add(InvokeCreateSatirFromRequest(new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = 1,
                Aciklama = "Hizmet alimi",
                Miktar = 1,
                BirimFiyat = 1000m,
                KdvUygulamaTipi = (int)KdvUygulamaTipi.Kdvli,
                KdvOrani = 20m
            }));
            InvokeHesaplaBelgeToplamlari(belge);
            seedContext.SatisBelgeleri.Add(belge);
            await seedContext.SaveChangesAsync();
            var belgeId = belge.Id;

            // ── 1. Servisin kullanacağı DbContext içinde belgeyi BİRİNCİ tedarikçiyle önceden track et ──
            await using var serviceDbContext = CreateDbContext();
            var oncedenTrackliBelge = await serviceDbContext.SatisBelgeleri.FirstAsync(x => x.Id == belgeId);
            Assert.Equal(tedarikci1.Id, oncedenTrackliBelge.CariKartId);

            // ── 2. Ayrı bir DbContext üzerinden CariKartId'yi İKİNCİ tedarikçiyle güncelle ──
            await using (var updaterContext = CreateDbContext())
            {
                var guncellenecek = await updaterContext.SatisBelgeleri.FirstAsync(x => x.Id == belgeId);
                guncellenecek.CariKartId = tedarikci2.Id;
                await updaterContext.SaveChangesAsync();
            }

            // ── 3. MuhasebeFisiOlusturAsync'i, ÖNCEDEN (birinci tedarikçiyle) track edilmiş
            //      serviceDbContext üzerinden çalıştır ──
            var service = CreateMuhasebeFisService(serviceDbContext);
            var dto = await service.MuhasebeFisiOlusturAsync(belgeId, CancellationToken.None);

            // ── 4. Fiş satırı ve cari hareket İKİSİ DE ikinci tedarikçiyi/hesabını kullanmalı ──
            var fis = await serviceDbContext.MuhasebeFisler
                .Include(x => x.Satirlar)
                .FirstAsync(x => x.Id == dto.MuhasebeFisId);

            var tedarikciSatiri = Assert.Single(fis.Satirlar, s => s.CariKartId == tedarikci2.Id);
            Assert.Equal(hesap2.Id, tedarikciSatiri.MuhasebeHesapPlaniId);

            var cariHareket = await serviceDbContext.CariHareketler.FirstAsync(x => x.KaynakId == belgeId);
            Assert.Equal(tedarikci2.Id, cariHareket.CariKartId);

            // ── 5. Birinci tedarikçinin hesabı/kartı HİÇBİR şekilde kullanılmamalı ──
            Assert.DoesNotContain(fis.Satirlar, s => s.MuhasebeHesapPlaniId == hesap1.Id);
            Assert.DoesNotContain(fis.Satirlar, s => s.CariKartId == tedarikci1.Id);

            var toplamBorc = fis.Satirlar.Sum(s => s.Borc);
            var toplamAlacak = fis.Satirlar.Sum(s => s.Alacak);
            Assert.Equal(toplamBorc, toplamAlacak);
        }
        finally
        {
            if (kurumId > 0)
            {
                await using var cleanupContext = CreateDbContext();
                await CleanupAsync(cleanupContext, tesisId, kurumId, uniqueSuffix);
            }
        }
    }

    private static MuhasebeHesapPlani SeedHesap(string uniqueSuffix, string etiket, int tesisId) => new()
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

    private static MuhasebeHesapPlani SeedAnaKodHesap(string uniqueSuffix, string anaKod, string etiket, int tesisId) => new()
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

    private static async Task CleanupAsync(StysAppDbContext dbContext, int tesisId, int kurumId, string uniqueSuffix)
    {
        // FK bağımlılığına göre TERSTEN sıralı silme (bkz. RezervasyonOdemeMuhasebeIntegrationTests
        // ile aynı yaklaşım). SatisBelgeleri.MuhasebeFisId -> MuhasebeFisler Restrict FK ile bağlı
        // olduğundan, SatisBelgeleri fiş satırlarından/fişlerden ÖNCE silinmeli (aksi halde "hâlâ
        // bir SatisBelgesi'nin işaret ettiği fiş siliniyor" hatası alınır).
        await dbContext.CariHareketler
            .Where(x => x.CariKart != null && x.CariKart.TesisId == tesisId)
            .ExecuteDeleteAsync();
        await dbContext.SatisBelgeleri
            .Where(x => x.TesisId == tesisId)
            .ExecuteDeleteAsync();

        var fisIds = await dbContext.MuhasebeFisler
            .Where(x => x.TesisId == tesisId)
            .Select(x => x.Id)
            .ToListAsync();
        if (fisIds.Count > 0)
        {
            await dbContext.MuhasebeFisSatirlari.Where(x => fisIds.Contains(x.MuhasebeFisId)).ExecuteDeleteAsync();
            await dbContext.MuhasebeFisler.Where(x => fisIds.Contains(x.Id)).ExecuteDeleteAsync();
        }

        await dbContext.CariKartlar
            .Where(x => x.TesisId == tesisId)
            .ExecuteDeleteAsync();
        // Global TestMarker DEĞİL, örneğe özel uniqueSuffix ile filtrelenir — eş zamanlı çalışan
        // başka bir IntegrationFact örneğinin hâlâ canlı hesap planı kaydını silmeye çalışıp FK
        // Restrict hatası almaması için (bkz. SqlServerIntegrationCollection notu).
        await dbContext.MuhasebeHesapPlanlari
            .Where(x => x.Kod != null && x.Kod.Contains(uniqueSuffix))
            .ExecuteDeleteAsync();
        await dbContext.Tesisler
            .Where(x => x.Id == tesisId)
            .ExecuteDeleteAsync();
        await dbContext.Iller
            .Where(x => x.Ad != null && x.Ad.Contains(uniqueSuffix))
            .ExecuteDeleteAsync();
        await dbContext.Kurumlar
            .Where(x => x.Id == kurumId)
            .ExecuteDeleteAsync();
    }

    private static StysAppDbContext CreateDbContext()
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

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<SatisBelgesiProfile>();
        }, NullLoggerFactory.Instance);
        return config.CreateMapper();
    }

    private static ISatisBelgesiMuhasebeFisService CreateMuhasebeFisService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var repository = new SatisBelgesiRepository(dbContext, mapper);

        var stratejiler = new List<ISatisBelgesiMuhasebeFisStratejisi>
        {
            new SatisFaturasiMuhasebeFisStratejisi(),
            new SatisIadeFaturasiMuhasebeFisStratejisi(dbContext),
            new SatisTevkifatliFaturaMuhasebeFisStratejisi(new FakeTevkifatHesapEslemeService(hesapPlaniId: 999)),
            new AlisFaturasiMuhasebeFisStratejisi(dbContext, new FakeTasinirKodMuhasebeHesapEslemeService()),
            new AlisIadeFaturasiMuhasebeFisStratejisi(dbContext, new FakeTasinirKodMuhasebeHesapEslemeService()),
            new AlisTevkifatliFaturaMuhasebeFisStratejisi(
                dbContext, new FakeTasinirKodMuhasebeHesapEslemeService(), new FakeTevkifatHesapEslemeService(hesapPlaniId: 999))
        };

        return new SatisBelgesiMuhasebeFisService(
            repository,
            dbContext,
            mapper,
            new FakeMuhasebeDonemService(),
            stratejiler,
            NullLogger<SatisBelgesiMuhasebeFisService>.Instance);
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

    private sealed class FakeCurrentUserAccessor : TOD.Platform.Security.Auth.Services.ICurrentUserAccessor
    {
        public string? GetCurrentUserName() => "integration-test";
        public Guid? GetCurrentUserId() => Guid.NewGuid();
    }

    private sealed class FakeCurrentTenantAccessor : TOD.Platform.Security.Auth.Services.ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => null;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [];
        public bool IsSuperAdmin() => true;
        public bool IsKurumAdmin() => false;
    }

    private sealed class FakeMuhasebeDonemService : IMuhasebeDonemService
    {
        public Task<MuhasebeDonemDto?> GetAktifDonemAsync(int tesisId, DateTime tarih, CancellationToken cancellationToken = default)
            => Task.FromResult<MuhasebeDonemDto?>(new MuhasebeDonemDto
            {
                Id = 1,
                TesisId = tesisId,
                MaliYil = 2026,
                DonemNo = 1,
                BaslangicTarihi = new DateTime(2026, 1, 1),
                BitisTarihi = new DateTime(2026, 1, 31),
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
            System.Linq.Expressions.Expression<Func<MuhasebeDonem, bool>>? predicate = null,
            Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null,
            Func<IQueryable<MuhasebeDonem>, IOrderedQueryable<MuhasebeDonem>>? orderBy = null)
            => throw new NotImplementedException();

        public Task<MuhasebeDonemDto> AddAsync(MuhasebeDonemDto dto) => throw new NotImplementedException();

        public Task<MuhasebeDonemDto> UpdateAsync(MuhasebeDonemDto dto) => throw new NotImplementedException();

        public Task DeleteAsync(int id) => throw new NotImplementedException();

        public Task<IEnumerable<MuhasebeDonemDto>> WhereAsync(
            System.Linq.Expressions.Expression<Func<MuhasebeDonem, bool>> predicate,
            Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null)
            => throw new NotImplementedException();

        public Task<bool> AnyAsync(
            System.Linq.Expressions.Expression<Func<MuhasebeDonem, bool>> predicate,
            Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null)
            => throw new NotImplementedException();
    }

    private sealed class FakeTasinirKodMuhasebeHesapEslemeService : ITasinirKodMuhasebeHesapEslemeService
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
            System.Linq.Expressions.Expression<Func<TasinirKodMuhasebeHesapEsleme, bool>>? predicate = null,
            Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null,
            Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IOrderedQueryable<TasinirKodMuhasebeHesapEsleme>>? orderBy = null)
            => throw new NotImplementedException();

        public Task<TasinirKodMuhasebeHesapEslemeDto> AddAsync(TasinirKodMuhasebeHesapEslemeDto dto) => throw new NotImplementedException();

        public Task<TasinirKodMuhasebeHesapEslemeDto> UpdateAsync(TasinirKodMuhasebeHesapEslemeDto dto) => throw new NotImplementedException();

        public Task DeleteAsync(int id) => throw new NotImplementedException();

        public Task<IEnumerable<TasinirKodMuhasebeHesapEslemeDto>> WhereAsync(
            System.Linq.Expressions.Expression<Func<TasinirKodMuhasebeHesapEsleme, bool>> predicate,
            Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null)
            => throw new NotImplementedException();

        public Task<bool> AnyAsync(
            System.Linq.Expressions.Expression<Func<TasinirKodMuhasebeHesapEsleme, bool>> predicate,
            Func<IQueryable<TasinirKodMuhasebeHesapEsleme>, IQueryable<TasinirKodMuhasebeHesapEsleme>>? include = null)
            => throw new NotImplementedException();
    }

    private sealed class FakeTevkifatHesapEslemeService(int hesapPlaniId) : ITevkifatHesapEslemeService
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

        public Task<PagedResult<TevkifatHesapEslemeDto>> GetPagedAsync(PagedRequest request, System.Linq.Expressions.Expression<Func<TevkifatHesapEsleme, bool>>? predicate = null, Func<IQueryable<TevkifatHesapEsleme>, IQueryable<TevkifatHesapEsleme>>? include = null, Func<IQueryable<TevkifatHesapEsleme>, IOrderedQueryable<TevkifatHesapEsleme>>? orderBy = null)
            => throw new NotImplementedException();

        public Task<TevkifatHesapEslemeDto> AddAsync(TevkifatHesapEslemeDto dto) => throw new NotImplementedException();

        public Task<TevkifatHesapEslemeDto> UpdateAsync(TevkifatHesapEslemeDto dto) => throw new NotImplementedException();

        public Task DeleteAsync(int id) => throw new NotImplementedException();

        public Task<IEnumerable<TevkifatHesapEslemeDto>> WhereAsync(System.Linq.Expressions.Expression<Func<TevkifatHesapEsleme, bool>> predicate, Func<IQueryable<TevkifatHesapEsleme>, IQueryable<TevkifatHesapEsleme>>? include = null)
            => throw new NotImplementedException();

        public Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<TevkifatHesapEsleme, bool>> predicate, Func<IQueryable<TevkifatHesapEsleme>, IQueryable<TevkifatHesapEsleme>>? include = null)
            => throw new NotImplementedException();
    }
}
