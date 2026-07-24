-- ============================================================================
-- find-orphaned-pos-valor-test-data.sql
--
-- DRY-RUN bakim script'i - tests/STYS.Tests/PosTahsilatValorIntegrationTests.cs
-- (TestMarker = "PVI-970") tarafindan olusturulan, ancak DisposeAsync'in
-- basarisiz/kesintiye ugramis bir calismasi sonucu SILINEMEDEN kalmis yetim test
-- verilerini TESPIT EDER. HICBIR SATIRI SILMEZ/DEGISTIRMEZ - yalnizca SELECT
-- calistirir. Bulunan kayitlarin silinip silinmeyecegine (ve hangi esik/yas
-- degerinin "eski" sayilacagina) karar vermek bir INSAN gozden gecirmesi
-- gerektirir; bu script o kararin YERINE GECMEZ.
--
-- Kullanim: bu dosyayi bir sqlcmd/SSMS oturumunda, ilgili veritabanina (STYSDB)
-- karsi calistirin. Sonuclari inceleyip, GERCEKTEN yetim olduklari (asagidaki
-- "Iliskili gercek is verisi var mi?" kontrolleriyle) dogrulandiktan SONRA elle
-- silme islemini yapin - bu script otomatik silme SAGLAMAZ.
-- ============================================================================

SET NOCOUNT ON;

DECLARE @TestMarker nvarchar(32) = N'PVI-970';
DECLARE @EskiEsikGun int = 1; -- Bu gunden eski (CreatedAt) test kayitlari "yetim adayi" sayilir.

PRINT N'=== Yetim adayi Test Kurumlari ===';
SELECT k.Id, k.Kod, k.Ad, k.CreatedAt,
       DATEDIFF(DAY, k.CreatedAt, SYSUTCDATETIME()) AS YasGun
FROM dbo.Kurumlar k
WHERE k.Kod LIKE @TestMarker + '%'
  AND k.CreatedAt < DATEADD(DAY, -@EskiEsikGun, SYSUTCDATETIME())
ORDER BY k.CreatedAt;

PRINT N'=== Yetim adayi Test Tesisleri ===';
SELECT t.Id, t.Ad, t.KurumId, t.CreatedAt,
       DATEDIFF(DAY, t.CreatedAt, SYSUTCDATETIME()) AS YasGun
FROM dbo.Tesisler t
WHERE t.Ad LIKE '%' + @TestMarker + '%'
  AND t.CreatedAt < DATEADD(DAY, -@EskiEsikGun, SYSUTCDATETIME())
ORDER BY t.CreatedAt;

PRINT N'=== Yetim adayi Test MuhasebeHesapPlanlari (global, TesisId IS NULL, bozuk TamKod formati DAHIL) ===';
SELECT h.Id, h.Kod, h.TamKod, h.Ad, h.TesisId, h.CreatedAt,
       DATEDIFF(DAY, h.CreatedAt, SYSUTCDATETIME()) AS YasGun
FROM muhasebe.MuhasebeHesapPlanlari h
WHERE (h.Kod LIKE @TestMarker + '%' OR h.TamKod LIKE '%' + @TestMarker + '%')
  AND h.CreatedAt < DATEADD(DAY, -@EskiEsikGun, SYSUTCDATETIME())
ORDER BY h.CreatedAt;

PRINT N'=== Yetim adayi Test CariKartlari ===';
SELECT ck.Id, ck.CariKodu, ck.UnvanAdSoyad, ck.TesisId, ck.CreatedAt
FROM muhasebe.CariKartlar ck
WHERE ck.CariKodu LIKE '%' + @TestMarker + '%'
  AND ck.CreatedAt < DATEADD(DAY, -@EskiEsikGun, SYSUTCDATETIME());

PRINT N'=== Iliskili gercek is verisi var mi kontrolu (yukaridaki Tesisler icin) ===';
-- Bir "yetim aday" Tesis'in GERCEKTEN yetim oldugunu (yani test disi/gercek hicbir
-- is verisiyle iliskilendirilmedigini) dogrulamak icin - silmeden ONCE bu sorguyu
-- calistirip Adet=0 oldugunu teyit edin.
SELECT t.Id AS TesisId, t.Ad,
    (SELECT COUNT(*) FROM muhasebe.PosTahsilatValorleri v WHERE v.TesisId = t.Id) AS PosValorAdet,
    (SELECT COUNT(*) FROM muhasebe.MuhasebeFisler f WHERE f.TesisId = t.Id) AS MuhasebeFisAdet,
    (SELECT COUNT(*) FROM muhasebe.CariKartlar ck WHERE ck.TesisId = t.Id) AS CariKartAdet,
    (SELECT COUNT(*) FROM muhasebe.KasaBankaHesaplari kb WHERE kb.TesisId = t.Id) AS KasaBankaAdet
FROM dbo.Tesisler t
WHERE t.Ad LIKE '%' + @TestMarker + '%'
  AND t.CreatedAt < DATEADD(DAY, -@EskiEsikGun, SYSUTCDATETIME());
