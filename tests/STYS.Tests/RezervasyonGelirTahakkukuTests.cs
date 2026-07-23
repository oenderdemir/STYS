using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using STYS.AccessScope;
using STYS.EkHizmetler.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariHareketler.Mapping;
using STYS.Muhasebe.CariHareketler.Repositories;
using STYS.Muhasebe.CariHareketler.Services;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.CariKartlar.Mapping;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.MuhasebeDonemleri.Mapping;
using STYS.Muhasebe.MuhasebeDonemleri.Repositories;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeFisleri.Dtos;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Repositories;
using STYS.Muhasebe.MuhasebeFisleri.Services;
using STYS.Muhasebe.SatisBelgeleri.Mapping;
using STYS.Muhasebe.SatisBelgeleri.Repositories;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Mapping;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Repositories;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Entities;
using STYS.Rezervasyonlar.Services;
using STYS.Tesisler.Entities;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Persistence.Rdbms.Dto;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Rezervasyon gelir tahakkuku (Faz 2) icin InMemory-tabanli senaryolar. Bu senaryolar unique index
/// enforcement veya gercek transaction (BeginTransactionAsync) gerektirmez — bu ikisine ihtiyac duyan
/// senaryolar (esizamanli olusturma, kapama geri alma) RezervasyonOdemeMuhasebeIntegrationTests'te
/// gercek SQL Server'a karsi calisir.
/// </summary>
public class RezervasyonGelirTahakkukuTests
{
    // Senaryo 1: Check-out, cari kart tesis varsayilanindan cozumlenebiliyor -> taslak otomatik
    // olusur ve Rezervasyon.SatisBelgesiId set edilir.
    [Fact]
    public async Task Senaryo1_CariKartTesisVarsayilanindanCozumlenir_TaslakOlusurVeSatisBelgesiIdSetEdilir()
    {
        await using var dbContext = CreateDbContext();
        var (tesisId, cariKartId) = await SeedTesisVeCariKartAsync(dbContext, tesisVarsayilanCariKart: true);
        var rezervasyonId = await SeedRezervasyonAsync(dbContext, tesisId, toplamUcret: 1000m);

        var service = CreateGelirTahakkukService(dbContext);
        var result = await service.OlusturTaslakAsync(rezervasyonId);

        Assert.True(result.Id > 0);
        var rezervasyon = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == rezervasyonId);
        Assert.Equal(result.Id, rezervasyon.SatisBelgesiId);
        Assert.Equal(cariKartId, result.CariKartId);
    }

    // Senaryo 2: Cari kart ne rezervasyonda ne tesis varsayilaninda mevcut -> 422 firlatilir,
    // taslak olusmaz, Rezervasyon.SatisBelgesiId bos kalir.
    [Fact]
    public async Task Senaryo2_CariKartCozumlenemiyor_422FirlatirVeTaslakOlusmaz()
    {
        await using var dbContext = CreateDbContext();
        var (tesisId, _) = await SeedTesisVeCariKartAsync(dbContext, tesisVarsayilanCariKart: false);
        var rezervasyonId = await SeedRezervasyonAsync(dbContext, tesisId, toplamUcret: 1000m);

        var service = CreateGelirTahakkukService(dbContext);
        var exception = await Assert.ThrowsAsync<BaseException>(() => service.OlusturTaslakAsync(rezervasyonId));

        Assert.Equal(RezervasyonCariKartResolver.CariKartSecimiGerekliStatusCode, exception.ErrorCode);
        var rezervasyon = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == rezervasyonId);
        Assert.Null(rezervasyon.SatisBelgesiId);
    }

    // Senaryo 3: Ek hizmet + restoran (OdayaEkle) kalemi olan rezervasyon -> taslakta uc satir tipi
    // de yer alir ve toplam, tahsil edilen toplamla (konaklama + ek hizmet + restoran) esit olur.
    [Fact]
    public async Task Senaryo3_EkHizmetVeRestoranVarsa_TumSatirlarTaslaktaYerAlirVeToplamTahsilatlaEsit()
    {
        await using var dbContext = CreateDbContext();
        var (tesisId, _) = await SeedTesisVeCariKartAsync(dbContext, tesisVarsayilanCariKart: true);
        var rezervasyonId = await SeedRezervasyonAsync(dbContext, tesisId, toplamUcret: 1000m);

        var konaklayanId = rezervasyonId + 1;
        dbContext.RezervasyonKonaklayanlar.Add(new RezervasyonKonaklayan
        {
            Id = konaklayanId,
            RezervasyonId = rezervasyonId,
            SiraNo = 1,
            AdSoyad = "Test Misafir",
            KatilimDurumu = KonaklayanKatilimDurumlari.Geldi
        });
        dbContext.RezervasyonEkHizmetler.Add(new RezervasyonEkHizmet
        {
            RezervasyonId = rezervasyonId,
            RezervasyonKonaklayanId = konaklayanId,
            EkHizmetId = 1,
            EkHizmetTarifeId = 1,
            RezervasyonSegmentId = 1,
            OdaId = 1,
            TarifeAdiSnapshot = "Spa Girisi",
            BirimAdiSnapshot = "Adet",
            OdaNoSnapshot = "101",
            BinaAdiSnapshot = "A Blok",
            HizmetTarihi = new DateTime(2026, 3, 8),
            Miktar = 1,
            BirimFiyat = 200m,
            ToplamTutar = 200m,
            ParaBirimi = "TRY"
        });
        dbContext.RezervasyonOdemeler.Add(new RezervasyonOdeme
        {
            RezervasyonId = rezervasyonId,
            OdemeTarihi = DateTime.UtcNow,
            OdemeTutari = -150m,
            ParaBirimi = "TRY",
            OdemeTipi = OdemeYontemleri.OdayaEkle,
            Aciklama = "Restoran - Test Siparis"
        });
        await dbContext.SaveChangesAsync();

        var service = CreateGelirTahakkukService(dbContext);
        var result = await service.OlusturTaslakAsync(rezervasyonId);

        var satirTipleri = result.Satirlar.Select(x => x.SatirTipi).ToHashSet();
        Assert.Contains(STYS.Muhasebe.SatisBelgeleri.Enums.SatisBelgesiSatirTipi.Konaklama, satirTipleri);
        Assert.Contains(STYS.Muhasebe.SatisBelgeleri.Enums.SatisBelgesiSatirTipi.EkHizmet, satirTipleri);
        Assert.Contains(STYS.Muhasebe.SatisBelgeleri.Enums.SatisBelgesiSatirTipi.YiyecekIcecek, satirTipleri);

        // Toplam borc: konaklama (1000) + ek hizmet (200) + restoran (150) = 1350 (KDV haric matrah toplami).
        var matrahToplami = result.Satirlar.Sum(x => x.Miktar * x.BirimFiyat);
        Assert.Equal(1350m, matrahToplami);
    }

    // Senaryo 4: "Gelir Belgesi Olustur" iki kez cagrilir -> ikinci cagri yeni belge yaratmaz,
    // ayni SatisBelgesiId'yi doner (idempotency katman 1).
    [Fact]
    public async Task Senaryo4_IkinciCagriYeniBelgeYaratmaz()
    {
        await using var dbContext = CreateDbContext();
        var (tesisId, _) = await SeedTesisVeCariKartAsync(dbContext, tesisVarsayilanCariKart: true);
        var rezervasyonId = await SeedRezervasyonAsync(dbContext, tesisId, toplamUcret: 1000m);

        var service = CreateGelirTahakkukService(dbContext);
        var ilkSonuc = await service.OlusturTaslakAsync(rezervasyonId);
        var ikinciSonuc = await service.OlusturTaslakAsync(rezervasyonId);

        Assert.Equal(ilkSonuc.Id, ikinciSonuc.Id);
        var toplamBelgeSayisi = await dbContext.SatisBelgeleri.CountAsync(x => x.KaynakId == rezervasyonId.ToString());
        Assert.Equal(1, toplamBelgeSayisi);
    }

    // Senaryo 6: Fatura CariHareket'i olustuktan sonra rezervasyona ait iki aktif tahsilat
    // KapatOncekiTahsilatlariAsync ile faturaya karsi kapatilir.
    [Fact]
    public async Task Senaryo6_FaturaCariHareketiVarken_IkiAktifTahsilatDaKapatilir()
    {
        await using var dbContext = CreateDbContext();
        var (tesisId, cariKartId) = await SeedTesisVeCariKartAsync(dbContext, tesisVarsayilanCariKart: true);
        var rezervasyonId = await SeedRezervasyonAsync(dbContext, tesisId, toplamUcret: 1000m);

        var rezervasyon = await dbContext.Rezervasyonlar.SingleAsync(x => x.Id == rezervasyonId);
        rezervasyon.CariKartId = cariKartId;

        // Iki ayri odeme + tahsilat belgesi (Faz 1 akisindaki TahsilatOdemeBelgesi kaydiyla ayni sema).
        var odeme1 = new RezervasyonOdeme { RezervasyonId = rezervasyonId, OdemeTarihi = DateTime.UtcNow, OdemeTutari = 400m, ParaBirimi = "TRY", OdemeTipi = "Nakit" };
        var odeme2 = new RezervasyonOdeme { RezervasyonId = rezervasyonId, OdemeTarihi = DateTime.UtcNow, OdemeTutari = 600m, ParaBirimi = "TRY", OdemeTipi = "Nakit" };
        dbContext.RezervasyonOdemeler.AddRange(odeme1, odeme2);
        await dbContext.SaveChangesAsync();

        var belge1 = new TahsilatOdemeBelgesi { BelgeNo = "TEST-1", BelgeTarihi = DateTime.UtcNow, BelgeTipi = TahsilatOdemeBelgeTipleri.Tahsilat, CariKartId = cariKartId, Tutar = 400m, ParaBirimi = "TRY", OdemeYontemi = "Nakit", KaynakModul = MuhasebeKaynakModulleri.Rezervasyon, KaynakId = odeme1.Id, Durum = TahsilatOdemeBelgeDurumlari.Aktif };
        var belge2 = new TahsilatOdemeBelgesi { BelgeNo = "TEST-2", BelgeTarihi = DateTime.UtcNow, BelgeTipi = TahsilatOdemeBelgeTipleri.Tahsilat, CariKartId = cariKartId, Tutar = 600m, ParaBirimi = "TRY", OdemeYontemi = "Nakit", KaynakModul = MuhasebeKaynakModulleri.Rezervasyon, KaynakId = odeme2.Id, Durum = TahsilatOdemeBelgeDurumlari.Aktif };
        dbContext.TahsilatOdemeBelgeleri.AddRange(belge1, belge2);

        // Fatura satis belgesi + CariHareket'i (SatisBelgesiMuhasebeFisService'in ureteceginin ayni semasi)
        // dogrudan seed edilir — asil test edilen KapatOncekiTahsilatlariAsync'in yeniden kullanimidir,
        // satis belgesi onay/fis zincirinin kendisi degil (o zincir SatisBelgesiService'in kendi testlerinde kapsanir).
        var satisBelgesi = new STYS.Muhasebe.SatisBelgeleri.Entities.SatisBelgesi
        {
            BelgeNo = "FAT-1",
            BelgeTipi = STYS.Muhasebe.SatisBelgeleri.Enums.SatisBelgesiTipi.SatisFaturasi,
            Durum = STYS.Muhasebe.SatisBelgeleri.Enums.SatisBelgesiDurumu.MuhasebeOnaylandi,
            KaynakModul = STYS.Muhasebe.SatisBelgeleri.Enums.SatisKaynakModulu.Otel,
            KaynakId = rezervasyonId.ToString(),
            TesisId = tesisId,
            CariKartId = cariKartId,
            BelgeTarihi = DateTime.UtcNow,
            GenelToplam = 1000m,
            MuhasebeFisId = 1
        };
        dbContext.SatisBelgeleri.Add(satisBelgesi);
        await dbContext.SaveChangesAsync();
        rezervasyon.SatisBelgesiId = satisBelgesi.Id;
        await dbContext.SaveChangesAsync();

        var faturaHareket = new CariHareket
        {
            CariKartId = cariKartId,
            HareketTarihi = DateTime.UtcNow,
            BelgeTuru = "SatisFaturasi",
            BelgeNo = "FAT-1",
            BorcTutari = 1000m,
            AlacakTutari = 0m,
            KapananTutar = 0m,
            KalanTutar = 1000m,
            ParaBirimi = "TRY",
            Durum = CariHareketDurumlari.Aktif,
            KaynakModul = MuhasebeKaynakModulleri.SatisBelgesi,
            KaynakId = satisBelgesi.Id
        };
        dbContext.CariHareketler.Add(faturaHareket);
        await dbContext.SaveChangesAsync();

        var service = CreateGelirTahakkukService(dbContext);
        var sonuc = await service.KapatOncekiTahsilatlariAsync(rezervasyonId);

        Assert.Equal(2, sonuc.BasariliSayisi);
        Assert.Equal(0, sonuc.HataliSayisi);
        Assert.Equal(TahsilatKapamaDurumlari.TamKapatildi, sonuc.Ozet.TahsilatKapamaDurumu);

        var faturaGuncel = await dbContext.CariHareketler.SingleAsync(x => x.Id == faturaHareket.Id);
        Assert.Equal(0m, faturaGuncel.KalanTutar);
        Assert.True(faturaGuncel.KapandiMi);

        var kapamaHareketSayisi = await dbContext.CariHareketler.CountAsync(x =>
            x.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi);
        Assert.Equal(2, kapamaHareketSayisi);
    }

    // ──────────────────────────────────────────────
    //  Seed yardimcilari
    // ──────────────────────────────────────────────

    private static async Task<(int TesisId, int CariKartId)> SeedTesisVeCariKartAsync(StysAppDbContext dbContext, bool tesisVarsayilanCariKart)
    {
        var cariKart = new CariKart
        {
            CariTipi = CariKartTipleri.Musteri,
            CariKodu = $"TEST-{Guid.NewGuid():N}"[..16],
            UnvanAdSoyad = "Test Misafir Cari",
            AktifMi = true
        };
        dbContext.CariKartlar.Add(cariKart);
        await dbContext.SaveChangesAsync();

        var tesis = new Tesis
        {
            KurumId = 1,
            Ad = "Test Tesis " + Guid.NewGuid().ToString("N")[..8],
            Telefon = "0000",
            Adres = "Test Adres",
            AktifMi = true,
            RezervasyonMisafirVarsayilanCariKartId = tesisVarsayilanCariKart ? cariKart.Id : null
        };
        dbContext.Tesisler.Add(tesis);
        await dbContext.SaveChangesAsync();

        cariKart.TesisId = tesis.Id;
        await dbContext.SaveChangesAsync();

        return (tesis.Id, cariKart.Id);
    }

    private static async Task<int> SeedRezervasyonAsync(StysAppDbContext dbContext, int tesisId, decimal toplamUcret)
    {
        var rezervasyon = new Rezervasyon
        {
            ReferansNo = "TEST-" + Guid.NewGuid().ToString("N")[..12],
            TesisId = tesisId,
            KisiSayisi = 1,
            GirisTarihi = new DateTime(2026, 3, 8, 14, 0, 0),
            CikisTarihi = new DateTime(2026, 3, 9, 10, 0, 0),
            MisafirAdiSoyadi = "Test Misafir",
            MisafirTelefon = "0000",
            ToplamBazUcret = toplamUcret,
            ToplamUcret = toplamUcret,
            ParaBirimi = "TRY",
            RezervasyonDurumu = RezervasyonDurumlari.CheckOutTamamlandi,
            AktifMi = true
        };
        dbContext.Rezervasyonlar.Add(rezervasyon);
        await dbContext.SaveChangesAsync();
        return rezervasyon.Id;
    }

    // ──────────────────────────────────────────────
    //  Servis wiring — Faz 1/2'deki gercek servis grafi, InMemory DbContext uzerinde
    // ──────────────────────────────────────────────

    private static StysAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase($"stys-gelir-tahakkuku-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new StysAppDbContext(options, null, new FakeCurrentTenantAccessor());
    }

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<TahsilatOdemeBelgesiProfile>();
            cfg.AddProfile<CariKartProfile>();
            cfg.AddProfile<CariHareketProfile>();
            cfg.AddProfile<MuhasebeDonemProfile>();
            cfg.AddProfile<SatisBelgesiProfile>();
        }, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        return config.CreateMapper();
    }

    private static IMuhasebeDonemService CreateMuhasebeDonemService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var repo = new MuhasebeDonemRepository(dbContext, mapper);
        return new MuhasebeDonemService(repo, mapper, dbContext, new FakeMuhasebeTesisScopeService());
    }

    private static ICariHareketKapamaService CreateCariHareketKapamaService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var tahsilatRepo = new TahsilatOdemeBelgesiRepository(dbContext, mapper);
        var cariHareketRepo = new CariHareketRepository(dbContext, mapper);
        return new CariHareketKapamaService(
            dbContext, tahsilatRepo, cariHareketRepo, CreateMuhasebeDonemService(dbContext), new FakeUserAccessScopeService(), mapper);
    }

    private static ISatisBelgesiService CreateSatisBelgesiService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var satisBelgesiRepo = new SatisBelgesiRepository(dbContext, mapper);
        var muhasebeFisRepo = new MuhasebeFisRepository(dbContext, mapper);
        return new SatisBelgesiService(
            satisBelgesiRepo,
            dbContext,
            mapper,
            muhasebeFisRepo,
            new FakeMuhasebeFisService(),
            new FakeUserAccessScopeService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SatisBelgesiService>.Instance,
            new FakeDomainOperationLogger());
    }

    private static ISatisBelgesiTaslakOlusturmaService CreateSatisBelgesiTaslakOlusturmaService(StysAppDbContext dbContext)
    {
        var mapper = CreateMapper();
        var satisBelgesiRepo = new SatisBelgesiRepository(dbContext, mapper);
        return new SatisBelgesiTaslakOlusturmaService(
            CreateSatisBelgesiService(dbContext),
            satisBelgesiRepo,
            new FakeUserAccessScopeService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SatisBelgesiTaslakOlusturmaService>.Instance);
    }

    private static IRezervasyonSatisBelgesiService CreateRezervasyonSatisBelgesiService(StysAppDbContext dbContext)
    {
        return new RezervasyonSatisBelgesiService(
            dbContext,
            new FakeUserAccessScopeService(),
            CreateSatisBelgesiTaslakOlusturmaService(dbContext),
            new RezervasyonCariKartResolver(dbContext),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RezervasyonSatisBelgesiService>.Instance);
    }

    private static IRezervasyonGelirTahakkukService CreateGelirTahakkukService(StysAppDbContext dbContext)
    {
        return new RezervasyonGelirTahakkukService(
            dbContext,
            new FakeUserAccessScopeService(),
            CreateRezervasyonSatisBelgesiService(dbContext),
            CreateSatisBelgesiService(dbContext),
            CreateCariHareketKapamaService(dbContext));
    }

    // ──────────────────────────────────────────────
    //  Fakes
    // ──────────────────────────────────────────────

    private sealed class FakeCurrentTenantAccessor : TOD.Platform.Security.Auth.Services.ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => null;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [];
        public bool IsSuperAdmin() => true;
        public bool IsKurumAdmin() => false;
    }

    private sealed class FakeUserAccessScopeService : IUserAccessScopeService
    {
        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(DomainAccessScope.Unscoped());
    }

    private sealed class FakeMuhasebeTesisScopeService : IMuhasebeTesisScopeService
    {
        public Task<int[]> GetEffectiveTesisIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<int>());

        public Task<int[]> GetEffectiveTesisIdsAsync(DomainAccessScope scope, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<int>());

        public Task EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeDomainOperationLogger : IDomainOperationLogger
    {
        public void Started(string eventName, object payload) { }
        public void Completed(string eventName, object payload) { }
        public void Warning(string eventName, object payload) { }
        public void Failed(string eventName, Exception exception, object payload) { }
    }

    private sealed class FakeMuhasebeFisService : IMuhasebeFisService
    {
        public Task<IEnumerable<MuhasebeFisDto>> GetAllAsync(Func<IQueryable<MuhasebeFis>, IQueryable<MuhasebeFis>>? include = null) => throw new NotSupportedException();
        public Task<MuhasebeFisDto?> GetByIdAsync(int id, Func<IQueryable<MuhasebeFis>, IQueryable<MuhasebeFis>>? include = null) => throw new NotSupportedException();
        public Task<PagedResult<MuhasebeFisDto>> GetPagedAsync(PagedRequest request, System.Linq.Expressions.Expression<Func<MuhasebeFis, bool>>? predicate = null, Func<IQueryable<MuhasebeFis>, IQueryable<MuhasebeFis>>? include = null, Func<IQueryable<MuhasebeFis>, IOrderedQueryable<MuhasebeFis>>? orderBy = null) => throw new NotSupportedException();
        public Task<MuhasebeFisDto> AddAsync(MuhasebeFisDto dto) => throw new NotSupportedException();
        public Task<MuhasebeFisDto> UpdateAsync(MuhasebeFisDto dto) => throw new NotSupportedException();
        public Task DeleteAsync(int id) => throw new NotSupportedException();
        public Task<IEnumerable<MuhasebeFisDto>> WhereAsync(System.Linq.Expressions.Expression<Func<MuhasebeFis, bool>> predicate, Func<IQueryable<MuhasebeFis>, IQueryable<MuhasebeFis>>? include = null) => throw new NotSupportedException();
        public Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<MuhasebeFis, bool>> predicate, Func<IQueryable<MuhasebeFis>, IQueryable<MuhasebeFis>>? include = null) => throw new NotSupportedException();
        public Task<MuhasebeFisDto?> GetByIdWithSatirlarAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<MuhasebeFisDto>> GetByKaynakAsync(string kaynakModul, int kaynakId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MuhasebeFisDto> OnaylaAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MuhasebeFisDto> IptalEtAsync(int id, string? aciklama = null, CancellationToken cancellationToken = default) => throw new NotSupportedException("Bu senaryolarda fis olusmadigi icin cagrilmamali.");
        public Task<STYS.Muhasebe.MuhasebeFisleri.Dtos.MuhasebeFisIptalSonucDto> PosValorTransferFisiniIptalEtAsync(int muhasebeFisId, int beklenenKaynakId, int beklenenTesisId, string aciklama, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<MuhasebeFisDto>> GetFilteredAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountFilteredAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<YevmiyeDefteriDto> GetYevmiyeDefteriAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]> ExportYevmiyeDefteriExcelAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MuavinDefterDto> GetMuavinDefterAsync(MuavinDefterFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]> ExportMuavinDefterExcelAsync(MuavinDefterFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MizanDto> GetMizanAsync(MizanFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MizanDto> GetMizanBakiyeAsync(MizanFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]> ExportMizanBakiyeExcelAsync(MizanFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MizanKarsilastirmaDto> KarsilastirMizanAsync(MizanFilterDto filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TasinirMuhasebeFisiOlusturResultDto> TasinirMuhasebeFisiTaslagiOlusturAsync(TasinirMuhasebeFisiOlusturRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
