-- ============================================================================
-- cari-duplicate-diagnostic.sql
--
-- READ-ONLY diagnostic. Ayni tesiste ayni TCKN/VKN (normalize) ile birden fazla
-- aktif Musteri/KurumsalMusteri cari karti bulunan gruplari ve her karta ait:
--   - finansal kullanim var mi
--   - rezervasyon/is kaydi baglantisi var mi
--   - canonical mi
--   - otomatik temizlenebilir mi
--   - manuel inceleme nedeni
-- bilgisini listeler. Hicbir veri DEGISTIRMEZ.
--
-- Kullanim:
--   sqlcmd -S localhost,14333 -d STYSDB -U sa -P "Strong!Pass1" -C -i cari-duplicate-diagnostic.sql
-- ============================================================================

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

;WITH GroupStats AS (
    SELECT TesisId, VergiNoTcknNormalized, COUNT(*) AS KartSayisi, MIN(Id) AS MinId
    FROM muhasebe.CariKartlar
    WHERE IsDeleted = 0 AND AktifMi = 1 AND TesisId IS NOT NULL AND VergiNoTcknNormalized IS NOT NULL
      AND CariTipi IN ('Musteri','KurumsalMusteri')
    GROUP BY TesisId, VergiNoTcknNormalized
    HAVING COUNT(*) > 1
),
CardFlags AS (
    SELECT
        c.Id, c.TesisId, c.VergiNoTcknNormalized, c.CariKodu, c.UnvanAdSoyad, c.MuhasebeHesapPlaniId,
        CASE WHEN EXISTS (SELECT 1 FROM muhasebe.CariHareketler x WHERE x.CariKartId = c.Id)
              OR EXISTS (SELECT 1 FROM muhasebe.TahsilatOdemeBelgeleri x WHERE x.CariKartId = c.Id)
              OR EXISTS (SELECT 1 FROM muhasebe.SatisBelgeleri x WHERE x.CariKartId = c.Id)
              OR EXISTS (SELECT 1 FROM muhasebe.BankaHareketleri x WHERE x.CariKartId = c.Id)
              OR EXISTS (SELECT 1 FROM muhasebe.KasaHareketleri x WHERE x.CariKartId = c.Id)
              OR EXISTS (SELECT 1 FROM muhasebe.StokHareketleri x WHERE x.CariKartId = c.Id)
              OR EXISTS (SELECT 1 FROM muhasebe.MuhasebeFisSatirlari x WHERE x.CariKartId = c.Id)
              OR EXISTS (SELECT 1 FROM entegrasyon.PosOdemeIslemleri x WHERE x.CariKartId = c.Id)
            THEN 1 ELSE 0 END AS FinansalKullanildi,
        CASE WHEN EXISTS (SELECT 1 FROM dbo.Rezervasyonlar x WHERE x.CariKartId = c.Id)
              OR EXISTS (SELECT 1 FROM dbo.Tesisler x WHERE x.RezervasyonMisafirVarsayilanCariKartId = c.Id)
            THEN 1 ELSE 0 END AS RezervasyonVar
    FROM muhasebe.CariKartlar c
    INNER JOIN GroupStats g ON g.TesisId = c.TesisId AND g.VergiNoTcknNormalized = c.VergiNoTcknNormalized
    WHERE c.IsDeleted = 0 AND c.AktifMi = 1 AND c.CariTipi IN ('Musteri','KurumsalMusteri')
),
Canonical AS (
    SELECT g.TesisId, g.VergiNoTcknNormalized,
           COALESCE(
               (SELECT MIN(f.Id) FROM CardFlags f WHERE f.TesisId = g.TesisId AND f.VergiNoTcknNormalized = g.VergiNoTcknNormalized AND f.FinansalKullanildi = 1),
               (SELECT MIN(f.Id) FROM CardFlags f WHERE f.TesisId = g.TesisId AND f.VergiNoTcknNormalized = g.VergiNoTcknNormalized AND f.RezervasyonVar = 1),
               g.MinId) AS CanonicalId
    FROM GroupStats g
)
SELECT
    f.TesisId,
    f.VergiNoTcknNormalized AS NormalizeTCKN_VKN,
    f.Id AS CariKartId,
    f.CariKodu,
    f.UnvanAdSoyad,
    f.MuhasebeHesapPlaniId,
    f.FinansalKullanildi AS FinansalKullanimVarMi,
    f.RezervasyonVar AS RezervasyonBaglantisiVarMi,
    CASE WHEN f.Id = cn.CanonicalId THEN 1 ELSE 0 END AS CanonicalMi,
    CASE WHEN f.Id <> cn.CanonicalId AND f.FinansalKullanildi = 0 THEN 1 ELSE 0 END AS OtomatikTemizlenebilirMi,
    CASE
        WHEN f.Id = cn.CanonicalId THEN NULL
        WHEN f.FinansalKullanildi = 1 THEN N'Finansal geçmişi var; otomatik merge edilmedi (manuel inceleme)'
        ELSE NULL
    END AS ManuelIncelemeNedeni
FROM CardFlags f
INNER JOIN Canonical cn ON cn.TesisId = f.TesisId AND cn.VergiNoTcknNormalized = f.VergiNoTcknNormalized
ORDER BY f.TesisId, f.VergiNoTcknNormalized, f.Id;
