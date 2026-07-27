using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Infrastructure.EntityFramework.Migrations;
using STYS.Iller.Entities;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Tesisler.Entities;

namespace STYS.Tests;

/// <summary>
/// 20260724141641_BackfillMissingPosTahsilatValorSnapshots migrationinin (bkz.
/// BackfillMissingPosTahsilatValorSnapshots.BackfillSql) dogrulugunu GERCEK SQL Server'a karsi
/// test eder.
///
/// IZOLASYON: her test KENDI Kurum/Il/Tesis/CariKart/KasaBankaHesap/TahsilatOdemeBelgesi
/// satirlarini (benzersiz "BFPV-970-{guid}" isaretiyle) bir transaction ICINDE olusturur,
/// BackfillSql'i AYNI transaction icinde calistirir, sonuclari yalnizca KENDI olusturdugu
/// BelgeId'ler uzerinden dogrular (script TUM veritabanini taradigi icin transaction icinde
/// GERCEK/paylasilan baska eksik kayitlar da islenebilir - bu ZARARSIZDIR, cunku hicbir
/// assertion bunlara bakmaz ve transaction HER ZAMAN rollback edilir, hicbir kalici etkisi
/// olmaz).
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class BackfillMissingPosTahsilatValorSnapshotsMigrationTests
{
    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable(IntegrationFactAttribute.ConnectionStringEnvVar);

    private const string TestMarker = "BFPV-970";

    private static StysAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>().UseSqlServer(ConnectionString).Options;
        // Kurum/Tesis gibi ITenantEntity turlerini eklerken StysAppDbContext.ApplyTenantRules
        // "aktif kurum" bilgisi ister - IsSuperAdmin=true ile bu kisitlama, testin kendi Kurum
        // kaydi icin gecerli bir KurumId>0 sagladigi surece atlanir (bkz.
        // PosTahsilatValorIntegrationTests'teki ayni FakeCurrentTenantAccessor kullanimi).
        return new StysAppDbContext(options, new FakeCurrentUserAccessor(), new FakeCurrentTenantAccessor());
    }

    private sealed class FakeCurrentUserAccessor : TOD.Platform.Security.Auth.Services.ICurrentUserAccessor
    {
        public string? GetCurrentUserName() => "backfill-migration-test";
        public Guid? GetCurrentUserId() => Guid.NewGuid();
    }

    private sealed class FakeCurrentTenantAccessor : TOD.Platform.Security.Auth.Services.ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => null;
        public IReadOnlyList<int> GetAccessibleKurumIds() => [];
        public bool IsSuperAdmin() => true;
        public bool IsKurumAdmin() => false;
    }

    private sealed record Ortam(int TesisId, int CariKartId);

    private static async Task<Ortam> OrtamHazirlaAsync(StysAppDbContext dbContext, string uniqueSuffix)
    {
        var kurum = new Kurum { Kod = uniqueSuffix, Ad = "Test Kurum " + uniqueSuffix, AktifMi = true };
        dbContext.Kurumlar.Add(kurum);
        var il = new Il { Ad = "Test Il " + uniqueSuffix, AktifMi = true };
        dbContext.Iller.Add(il);
        await dbContext.SaveChangesAsync();

        var tesis = new Tesis { KurumId = kurum.Id, IlId = il.Id, Ad = "Test Tesis " + uniqueSuffix, Telefon = "0000", Adres = "Test Adres", AktifMi = true };
        dbContext.Tesisler.Add(tesis);
        await dbContext.SaveChangesAsync();

        var cariKart = new CariKart { TesisId = tesis.Id, CariTipi = CariKartTipleri.Musteri, CariKodu = uniqueSuffix + "-C1", UnvanAdSoyad = "Test Musteri " + uniqueSuffix, AktifMi = true };
        dbContext.CariKartlar.Add(cariKart);
        await dbContext.SaveChangesAsync();

        return new Ortam(tesis.Id, cariKart.Id);
    }

    private static async Task<int> EkleKasaBankaHesabiAsync(
        StysAppDbContext dbContext, int tesisId, string uniqueSuffix, string tip,
        int valorGunSayisi, string valorGunTuru, bool otomatikAktarim, decimal? komisyonOrani, bool isDeleted = false)
    {
        var hesap = new KasaBankaHesap
        {
            TesisId = tesisId,
            Tip = tip,
            Kod = uniqueSuffix + "-" + Guid.NewGuid().ToString("N")[..8],
            Ad = "Test Hesap " + uniqueSuffix,
            ParaBirimi = "TRY",
            AktifMi = true,
            ValorGunSayisi = valorGunSayisi,
            ValorGunTuru = valorGunTuru,
            ValorGunundeOtomatikHesabaAktarMi = otomatikAktarim,
            KomisyonOrani = komisyonOrani
        };
        dbContext.KasaBankaHesaplari.Add(hesap);
        await dbContext.SaveChangesAsync();

        if (isDeleted)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE [muhasebe].[KasaBankaHesaplari] SET [IsDeleted] = 1 WHERE [Id] = {hesap.Id}");
        }

        return hesap.Id;
    }

    private static async Task<int> EkleTahsilatBelgesiAsync(
        StysAppDbContext dbContext, int cariKartId, int? kasaBankaHesapId, string uniqueSuffix,
        string tag, decimal tutar, DateTime belgeTarihi, string odemeYontemi = "KrediKarti", string durum = "Aktif")
    {
        var belge = new TahsilatOdemeBelgesi
        {
            BelgeNo = $"{uniqueSuffix}-{tag}",
            BelgeTarihi = belgeTarihi,
            BelgeTipi = TahsilatOdemeBelgeTipleri.Tahsilat,
            CariKartId = cariKartId,
            Tutar = tutar,
            ParaBirimi = "TRY",
            OdemeYontemi = odemeYontemi,
            KasaBankaHesapId = kasaBankaHesapId,
            Durum = durum
        };
        dbContext.TahsilatOdemeBelgeleri.Add(belge);
        await dbContext.SaveChangesAsync();
        return belge.Id;
    }

    private static async Task<PosTahsilatValor?> OkuValorKaydiAsync(StysAppDbContext dbContext, int belgeId) =>
        await dbContext.PosTahsilatValorleri.AsNoTracking().SingleOrDefaultAsync(x => x.TahsilatOdemeBelgesiId == belgeId);

    // ─────────────────────────────────────────────────────────────
    // Senaryo 1 — Aktif kredi karti odemesi, komisyon orani tanimli: dogru komisyon/net/Durum.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task AktifKrediKartiOdemeKomisyonOraniTanimli_DogruDegerlerleBackfillEdilir()
    {
        await using var dbContext = CreateDbContext();
        var uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var ortam = await OrtamHazirlaAsync(dbContext, uniqueSuffix);
            var hesapId = await EkleKasaBankaHesabiAsync(dbContext, ortam.TesisId, uniqueSuffix, KasaBankaHesapTipleri.KrediKarti,
                valorGunSayisi: 5, valorGunTuru: ValorGunTurleri.TakvimGunu, otomatikAktarim: false, komisyonOrani: 2.5m);
            var belgeTarihi = new DateTime(2026, 1, 10);
            var belgeId = await EkleTahsilatBelgesiAsync(dbContext, ortam.CariKartId, hesapId, uniqueSuffix, "S1", 1000m, belgeTarihi);

            await dbContext.Database.ExecuteSqlRawAsync(BackfillMissingPosTahsilatValorSnapshots.BackfillSql);

            var valor = await OkuValorKaydiAsync(dbContext, belgeId);
            Assert.NotNull(valor);
            Assert.Equal(ortam.TesisId, valor!.TesisId);
            Assert.Equal(hesapId, valor.KrediKartiHesapId);
            Assert.Equal(1000m, valor.BrutTutar);
            Assert.Equal(25.00m, valor.KomisyonTutari); // 1000 * 2.5% = 25
            Assert.Equal(975.00m, valor.NetTutar);
            Assert.Equal(PosTahsilatValorDurumlari.ValorBekliyor, valor.Durum);
            Assert.Equal(DateOnly.FromDateTime(belgeTarihi).AddDays(5), valor.BeklenenValorTarihi);
            Assert.False(valor.IsDeleted);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 2 — Komisyon orani belirsiz (NULL) + otomatik aktarim ACIK -> MutabakatBekliyor.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task KomisyonBelirsizVeOtomatikAktarimAcik_MutabakatBekliyorOlur()
    {
        await using var dbContext = CreateDbContext();
        var uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var ortam = await OrtamHazirlaAsync(dbContext, uniqueSuffix);
            var hesapId = await EkleKasaBankaHesabiAsync(dbContext, ortam.TesisId, uniqueSuffix, KasaBankaHesapTipleri.KrediKarti,
                valorGunSayisi: 0, valorGunTuru: ValorGunTurleri.TakvimGunu, otomatikAktarim: true, komisyonOrani: null);
            var belgeId = await EkleTahsilatBelgesiAsync(dbContext, ortam.CariKartId, hesapId, uniqueSuffix, "S2", 500m, new DateTime(2026, 1, 10));

            await dbContext.Database.ExecuteSqlRawAsync(BackfillMissingPosTahsilatValorSnapshots.BackfillSql);

            var valor = await OkuValorKaydiAsync(dbContext, belgeId);
            Assert.NotNull(valor);
            Assert.Equal(0m, valor!.KomisyonTutari);
            Assert.Equal(500m, valor.NetTutar);
            Assert.Equal(PosTahsilatValorDurumlari.MutabakatBekliyor, valor.Durum);
            Assert.Null(valor.KomisyonOraniSnapshot);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 3 — Komisyon orani belirsiz (NULL) + otomatik aktarim KAPALI -> ValorBekliyor.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task KomisyonBelirsizVeOtomatikAktarimKapali_ValorBekliyorOlur()
    {
        await using var dbContext = CreateDbContext();
        var uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var ortam = await OrtamHazirlaAsync(dbContext, uniqueSuffix);
            var hesapId = await EkleKasaBankaHesabiAsync(dbContext, ortam.TesisId, uniqueSuffix, KasaBankaHesapTipleri.KrediKarti,
                valorGunSayisi: 0, valorGunTuru: ValorGunTurleri.TakvimGunu, otomatikAktarim: false, komisyonOrani: null);
            var belgeId = await EkleTahsilatBelgesiAsync(dbContext, ortam.CariKartId, hesapId, uniqueSuffix, "S3", 300m, new DateTime(2026, 1, 10));

            await dbContext.Database.ExecuteSqlRawAsync(BackfillMissingPosTahsilatValorSnapshots.BackfillSql);

            var valor = await OkuValorKaydiAsync(dbContext, belgeId);
            Assert.NotNull(valor);
            Assert.Equal(PosTahsilatValorDurumlari.ValorBekliyor, valor!.Durum);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 4 — IsGunu valor hesabi: hafta sonu dogru atlanir.
    // 2026-01-09 Cuma + 3 is günü = Pzt(12) atlanmaz, Sal(13) atlanmaz, Çar(14) -> 2026-01-14
    // (09 Cuma -> 12 Pzt (1) -> 13 Sal (2) -> 14 Çar (3), hafta sonu 10-11 atlanir).
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task IsGunuValorHesabi_HaftaSonuAtlanarakDogruHesaplanir()
    {
        await using var dbContext = CreateDbContext();
        var uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var ortam = await OrtamHazirlaAsync(dbContext, uniqueSuffix);
            var hesapId = await EkleKasaBankaHesabiAsync(dbContext, ortam.TesisId, uniqueSuffix, KasaBankaHesapTipleri.KrediKarti,
                valorGunSayisi: 3, valorGunTuru: ValorGunTurleri.IsGunu, otomatikAktarim: false, komisyonOrani: 0m);
            var belgeTarihi = new DateTime(2026, 1, 9); // Cuma
            var belgeId = await EkleTahsilatBelgesiAsync(dbContext, ortam.CariKartId, hesapId, uniqueSuffix, "S4", 200m, belgeTarihi);

            await dbContext.Database.ExecuteSqlRawAsync(BackfillMissingPosTahsilatValorSnapshots.BackfillSql);

            var valor = await OkuValorKaydiAsync(dbContext, belgeId);
            Assert.NotNull(valor);
            Assert.Equal(new DateOnly(2026, 1, 14), valor!.BeklenenValorTarihi);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 5 — Zaten bir valor kaydi olan belge: ikinci kayit URETILMEZ (idempotency).
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task ZatenValorKaydiOlanBelge_IkinciKayitUretilmez()
    {
        await using var dbContext = CreateDbContext();
        var uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var ortam = await OrtamHazirlaAsync(dbContext, uniqueSuffix);
            var hesapId = await EkleKasaBankaHesabiAsync(dbContext, ortam.TesisId, uniqueSuffix, KasaBankaHesapTipleri.KrediKarti,
                valorGunSayisi: 0, valorGunTuru: ValorGunTurleri.TakvimGunu, otomatikAktarim: false, komisyonOrani: 1m);
            var belgeId = await EkleTahsilatBelgesiAsync(dbContext, ortam.CariKartId, hesapId, uniqueSuffix, "S5", 100m, new DateTime(2026, 1, 10));

            var mevcutValor = new PosTahsilatValor
            {
                TesisId = ortam.TesisId,
                TahsilatOdemeBelgesiId = belgeId,
                KrediKartiHesapId = hesapId,
                OdemeTarihi = new DateTime(2026, 1, 10),
                ValorGunSayisi = 0,
                ValorGunTuru = ValorGunTurleri.TakvimGunu,
                BeklenenValorTarihi = new DateOnly(2026, 1, 10),
                OtomatikAktarimMi = false,
                BrutTutar = 100m,
                KomisyonTutari = 0m,
                NetTutar = 100m,
                ParaBirimi = "TRY",
                Durum = PosTahsilatValorDurumlari.Aktarildi
            };
            dbContext.PosTahsilatValorleri.Add(mevcutValor);
            await dbContext.SaveChangesAsync();

            await dbContext.Database.ExecuteSqlRawAsync(BackfillMissingPosTahsilatValorSnapshots.BackfillSql);

            var valorSayisi = await dbContext.PosTahsilatValorleri.CountAsync(x => x.TahsilatOdemeBelgesiId == belgeId);
            Assert.Equal(1, valorSayisi);

            var valor = await OkuValorKaydiAsync(dbContext, belgeId);
            // Mevcut kayit DEGISTIRILMEMIS olmali (Durum hala Aktarildi, backfill UZERINE YAZMAZ).
            Assert.Equal(PosTahsilatValorDurumlari.Aktarildi, valor!.Durum);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 6 — Iptal edilmis belge: backfill edilmez.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task IptalEdilmisBelge_BackfillEdilmez()
    {
        await using var dbContext = CreateDbContext();
        var uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var ortam = await OrtamHazirlaAsync(dbContext, uniqueSuffix);
            var hesapId = await EkleKasaBankaHesabiAsync(dbContext, ortam.TesisId, uniqueSuffix, KasaBankaHesapTipleri.KrediKarti,
                valorGunSayisi: 0, valorGunTuru: ValorGunTurleri.TakvimGunu, otomatikAktarim: false, komisyonOrani: 1m);
            var belgeId = await EkleTahsilatBelgesiAsync(dbContext, ortam.CariKartId, hesapId, uniqueSuffix, "S6", 100m, new DateTime(2026, 1, 10), durum: TahsilatOdemeBelgeDurumlari.Iptal);

            await dbContext.Database.ExecuteSqlRawAsync(BackfillMissingPosTahsilatValorSnapshots.BackfillSql);

            var valor = await OkuValorKaydiAsync(dbContext, belgeId);
            Assert.Null(valor);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 7 — Nakit/Banka odeme yontemi: backfill edilmez.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task KrediKartiDisiOdemeYontemi_BackfillEdilmez()
    {
        await using var dbContext = CreateDbContext();
        var uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var ortam = await OrtamHazirlaAsync(dbContext, uniqueSuffix);
            var hesapId = await EkleKasaBankaHesabiAsync(dbContext, ortam.TesisId, uniqueSuffix, KasaBankaHesapTipleri.Banka,
                valorGunSayisi: 0, valorGunTuru: ValorGunTurleri.TakvimGunu, otomatikAktarim: false, komisyonOrani: null);
            var belgeId = await EkleTahsilatBelgesiAsync(dbContext, ortam.CariKartId, hesapId, uniqueSuffix, "S7", 100m, new DateTime(2026, 1, 10), odemeYontemi: OdemeYontemleri.HavaleEft);

            await dbContext.Database.ExecuteSqlRawAsync(BackfillMissingPosTahsilatValorSnapshots.BackfillSql);

            var valor = await OkuValorKaydiAsync(dbContext, belgeId);
            Assert.Null(valor);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 8 — KasaBankaHesap.Tip = KrediKarti DEGIL (ornegin Banka): backfill edilmez.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task KasaBankaHesapTipiKrediKartiDegil_BackfillEdilmez()
    {
        await using var dbContext = CreateDbContext();
        var uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var ortam = await OrtamHazirlaAsync(dbContext, uniqueSuffix);
            var hesapId = await EkleKasaBankaHesabiAsync(dbContext, ortam.TesisId, uniqueSuffix, KasaBankaHesapTipleri.Banka,
                valorGunSayisi: 0, valorGunTuru: ValorGunTurleri.TakvimGunu, otomatikAktarim: false, komisyonOrani: null);
            // OdemeYontemi=KrediKarti ama hesap KENDISI Banka tipinde - fix bunu KASITLI ATLAMALI.
            var belgeId = await EkleTahsilatBelgesiAsync(dbContext, ortam.CariKartId, hesapId, uniqueSuffix, "S8", 100m, new DateTime(2026, 1, 10));

            await dbContext.Database.ExecuteSqlRawAsync(BackfillMissingPosTahsilatValorSnapshots.BackfillSql);

            var valor = await OkuValorKaydiAsync(dbContext, belgeId);
            Assert.Null(valor);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 9 — KasaBankaHesap soft-delete edilmis: backfill edilmez.
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task KasaBankaHesapSoftDeleteEdilmis_BackfillEdilmez()
    {
        await using var dbContext = CreateDbContext();
        var uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var ortam = await OrtamHazirlaAsync(dbContext, uniqueSuffix);
            var hesapId = await EkleKasaBankaHesabiAsync(dbContext, ortam.TesisId, uniqueSuffix, KasaBankaHesapTipleri.KrediKarti,
                valorGunSayisi: 0, valorGunTuru: ValorGunTurleri.TakvimGunu, otomatikAktarim: false, komisyonOrani: null, isDeleted: true);
            var belgeId = await EkleTahsilatBelgesiAsync(dbContext, ortam.CariKartId, hesapId, uniqueSuffix, "S9", 100m, new DateTime(2026, 1, 10));

            await dbContext.Database.ExecuteSqlRawAsync(BackfillMissingPosTahsilatValorSnapshots.BackfillSql);

            var valor = await OkuValorKaydiAsync(dbContext, belgeId);
            Assert.Null(valor);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Senaryo 10 — Migration ikinci kez calistirildiginda sonuc DEGISMEMELI (idempotency).
    // ─────────────────────────────────────────────────────────────
    [IntegrationFact]
    public async Task IkinciKezCalistirma_SonucDegismez()
    {
        await using var dbContext = CreateDbContext();
        var uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        try
        {
            var ortam = await OrtamHazirlaAsync(dbContext, uniqueSuffix);
            var hesapId = await EkleKasaBankaHesabiAsync(dbContext, ortam.TesisId, uniqueSuffix, KasaBankaHesapTipleri.KrediKarti,
                valorGunSayisi: 2, valorGunTuru: ValorGunTurleri.TakvimGunu, otomatikAktarim: false, komisyonOrani: 3m);
            var belgeId = await EkleTahsilatBelgesiAsync(dbContext, ortam.CariKartId, hesapId, uniqueSuffix, "S10", 400m, new DateTime(2026, 1, 10));

            await dbContext.Database.ExecuteSqlRawAsync(BackfillMissingPosTahsilatValorSnapshots.BackfillSql);
            var valor1 = await OkuValorKaydiAsync(dbContext, belgeId);

            await dbContext.Database.ExecuteSqlRawAsync(BackfillMissingPosTahsilatValorSnapshots.BackfillSql);
            var valor2 = await OkuValorKaydiAsync(dbContext, belgeId);

            var toplamSayisi = await dbContext.PosTahsilatValorleri.CountAsync(x => x.TahsilatOdemeBelgesiId == belgeId);
            Assert.Equal(1, toplamSayisi);
            Assert.Equal(valor1!.Id, valor2!.Id);
            Assert.Equal(valor1.KomisyonTutari, valor2.KomisyonTutari);
        }
        finally
        {
            await tx.RollbackAsync();
        }
    }
}
