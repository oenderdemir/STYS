-- ============================================================================
-- find-orphaned-pos-valor-test-data.sql
--
-- tests/STYS.Tests/PosTahsilatValorIntegrationTests.cs (TestMarker = "PVI-970")
-- tarafindan olusturulan, ancak DisposeAsync'in basarisiz/kesintiye ugramis bir
-- calismasi sonucu SILINEMEDEN kalmis yetim test verilerini tespit eder ve
-- (yalnizca ACIKCA istenirse) FK sirasiyla, transaction icinde siler.
--
-- VARSAYILAN DAVRANIS: DRY-RUN. Hicbir INSERT/UPDATE/DELETE calismaz - yalnizca
-- silinecek adaylarin adetleri VE kimlik bilgileri (Id + Kod/Ad) SELECT/PRINT edilir.
--
-- Kullanim (sqlcmd, dosyayi HIC DEGISTIRMEDEN):
--   Dry-run (varsayilan):
--     sqlcmd -S localhost,14333 -d STYSDB -U sa -P "Strong!Pass1" -C -i find-orphaned-pos-valor-test-data.sql
--   Silme (komut satirindan ACIKCA onaylanarak):
--     sqlcmd -S localhost,14333 -d STYSDB -U sa -P "Strong!Pass1" -C -v ExecuteDelete=1 -i find-orphaned-pos-valor-test-data.sql
--
-- NOT (sqlcmd scripting-variable davranisi): `-v ExecuteDelete=1` VERILMEZSE sqlcmd
-- "'ExecuteDelete' scripting variable not defined." seklinde bir BILGI mesaji basar
-- (bu bir T-SQL SYNTAX hatasi DEGILDIR, script normal sekilde calismaya devam eder,
-- exit code 0 doner) ve `$(ExecuteDelete)` metni asagidaki karsilastirmada OLDUGU GIBI
-- kalir - bu metin '1'e ESIT OLMADIGI icin karsilastirma DOGAL olarak dry-run'a duser.
-- Script kasitli olarak `:setvar ExecuteDelete ...` KULLANMAZ, cunku sqlcmd'de bir
-- :setvar komutu HER ZAMAN -v ile verilen degeri EZER (test edilip dogrulanmistir) -
-- bu, "-v ExecuteDelete=1 verilsin ama script yine de dry-run'a dussun" gibi sessiz bir
-- hataya yol acardi.
--
-- SILME MODUNDA (@ExecuteDelete=1) script:
--   - Yalnizca Kurumlar.Kod / Tesisler.Ad / MuhasebeHesapPlanlari.Kod / Iller.Ad
--     alaninda @TestMarker (varsayilan 'PVI-970') GECEN kayitlardan TURETILEN
--     (bu kurum/tesislere FK ile baglanan) satirlari hedefler - marker disindaki
--     hicbir veriye DOKUNMAZ.
--   - PosValorFisNoSayaclari gibi PAYLASILAN sayac tablolarinda YALNIZCA hedef
--     tesislere ait satirlari siler - baska (gercek/paylasilan) tesislerin
--     sayaclarina KESINLIKLE dokunmaz.
--   - FK sirasiyla (once referans temizligi/cocuk kayitlar, sonra ebeveyn
--     kayitlar) TEK BIR transaction icinde, TRY/CATCH ile calisir; herhangi bir
--     adim basarisiz olursa TUMU (ROLLBACK) geri alinir, hata mesaji PRINT edilir.
--   - Silme ONCESI (aday) ve SONRASI (fiilen silinen + kalan) adetleri tablo
--     bazinda rapor eder.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT OFF; -- TRY/CATCH icinde kendi ROLLBACK'imizi kontrollu yapabilmek icin.
SET QUOTED_IDENTIFIER ON; -- Bazi sqlcmd/oturum varsayilanlarinda kapali gelebilir; DELETE/UPDATE
                          -- ifadeleri (filtreli/indeksli nesnelerle ayni oturumda) bunu gerektirir.

DECLARE @TestMarker nvarchar(32) = N'PVI-970';
-- Bkz. yukaridaki NOT: $(ExecuteDelete) tanimsizsa bu metin OLDUGU GIBI kalir ('1'e esit
-- olmadigi icin @ExecuteDelete=0 olur - guvenli varsayilan).
DECLARE @ExecuteDelete bit = CASE WHEN N'$(ExecuteDelete)' = N'1' THEN 1 ELSE 0 END;

-- ----------------------------------------------------------------------------
-- 1) Hedef kok kayitlar: yalnizca marker'i TASIYAN Kurumlar/Tesisler/
--    MuhasebeHesapPlanlari/Iller (baska HICBIR kritere gore genisletilmez).
-- ----------------------------------------------------------------------------
DECLARE @HedefKurumlar TABLE (Id int PRIMARY KEY);
DECLARE @HedefTesisler TABLE (Id int PRIMARY KEY);
DECLARE @HedefCariKartlar TABLE (Id int PRIMARY KEY);
DECLARE @HedefFisler TABLE (Id int PRIMARY KEY);
DECLARE @HedefHesapPlanlari TABLE (Id int PRIMARY KEY);
DECLARE @HedefIller TABLE (Id int PRIMARY KEY);

INSERT INTO @HedefKurumlar (Id)
SELECT Id FROM dbo.Kurumlar WHERE Kod LIKE @TestMarker + '%';

INSERT INTO @HedefTesisler (Id)
SELECT Id FROM dbo.Tesisler
WHERE Ad LIKE '%' + @TestMarker + '%'
   OR KurumId IN (SELECT Id FROM @HedefKurumlar);

INSERT INTO @HedefCariKartlar (Id)
SELECT Id FROM muhasebe.CariKartlar WHERE TesisId IN (SELECT Id FROM @HedefTesisler);

INSERT INTO @HedefFisler (Id)
SELECT Id FROM muhasebe.MuhasebeFisler WHERE TesisId IN (SELECT Id FROM @HedefTesisler);

INSERT INTO @HedefHesapPlanlari (Id)
SELECT Id FROM muhasebe.MuhasebeHesapPlanlari WHERE Kod LIKE @TestMarker + '%';

INSERT INTO @HedefIller (Id)
SELECT Id FROM dbo.Iller WHERE Ad LIKE '%' + @TestMarker + '%';

-- ----------------------------------------------------------------------------
-- 2) DRY-RUN raporu: hem ADETLER hem de HEDEF Kurum/Tesis/MuhasebeHesapPlani
--    kayitlarinin kimlik bilgileri (Id + Kod/Ad). HER ZAMAN calisir (silme
--    modunda da "silme ONCESI" durumun kaydi olarak islev gorur).
-- ----------------------------------------------------------------------------
PRINT N'=== DRY-RUN (silme ONCESI): PVI-970 marker''li aday kayit adetleri (TestMarker=' + @TestMarker + N', ExecuteDelete=' + CAST(@ExecuteDelete AS nvarchar(1)) + N') ===';

SELECT 'Kurumlar' AS Tablo, COUNT(*) AS AdaySayisi FROM @HedefKurumlar
UNION ALL SELECT 'Tesisler', COUNT(*) FROM @HedefTesisler
UNION ALL SELECT 'Iller', COUNT(*) FROM @HedefIller
UNION ALL SELECT 'MuhasebeHesapPlanlari', COUNT(*) FROM @HedefHesapPlanlari
UNION ALL SELECT 'CariKartlar', COUNT(*) FROM @HedefCariKartlar
UNION ALL SELECT 'MuhasebeFisler', COUNT(*) FROM @HedefFisler
UNION ALL SELECT 'MuhasebeFisSatirlari', (SELECT COUNT(*) FROM muhasebe.MuhasebeFisSatirlari WHERE MuhasebeFisId IN (SELECT Id FROM @HedefFisler))
UNION ALL SELECT 'PosTahsilatValorleri', (SELECT COUNT(*) FROM muhasebe.PosTahsilatValorleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT 'PosTahsilatValorDegisiklikGecmisleri', (SELECT COUNT(*) FROM muhasebe.PosTahsilatValorDegisiklikGecmisleri d
    INNER JOIN muhasebe.PosTahsilatValorleri v ON v.Id = d.PosTahsilatValorId WHERE v.TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT 'TahsilatOdemeBelgeleri', (SELECT COUNT(*) FROM muhasebe.TahsilatOdemeBelgeleri WHERE CariKartId IN (SELECT Id FROM @HedefCariKartlar))
UNION ALL SELECT 'PosValorFisNoSayaclari', (SELECT COUNT(*) FROM muhasebe.PosValorFisNoSayaclari WHERE TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT 'MuhasebeHesapBakiyeleri', (SELECT COUNT(*) FROM muhasebe.MuhasebeHesapBakiyeleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT 'CariHareketler', (SELECT COUNT(*) FROM muhasebe.CariHareketler WHERE CariKartId IN (SELECT Id FROM @HedefCariKartlar))
UNION ALL SELECT 'KasaBankaHesaplari', (SELECT COUNT(*) FROM muhasebe.KasaBankaHesaplari WHERE TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT 'MuhasebeDonemler', (SELECT COUNT(*) FROM muhasebe.MuhasebeDonemler WHERE TesisId IN (SELECT Id FROM @HedefTesisler));

PRINT N'=== DRY-RUN: hedef Kurumlar (Id, Kod, Ad) ===';
SELECT k.Id, k.Kod, k.Ad, k.CreatedAt FROM dbo.Kurumlar k WHERE k.Id IN (SELECT Id FROM @HedefKurumlar) ORDER BY k.Id;

PRINT N'=== DRY-RUN: hedef Tesisler (Id, Ad, KurumId) ===';
SELECT t.Id, t.Ad, t.KurumId, t.CreatedAt FROM dbo.Tesisler t WHERE t.Id IN (SELECT Id FROM @HedefTesisler) ORDER BY t.Id;

PRINT N'=== DRY-RUN: hedef MuhasebeHesapPlanlari (Id, Kod, TamKod) ===';
SELECT h.Id, h.Kod, h.TamKod, h.Ad, h.CreatedAt FROM muhasebe.MuhasebeHesapPlanlari h WHERE h.Id IN (SELECT Id FROM @HedefHesapPlanlari) ORDER BY h.Id;

IF @ExecuteDelete = 0
BEGIN
    PRINT N'@ExecuteDelete = 0 (varsayilan/tanimsiz) - HICBIR SATIR SILINMEDI.';
    PRINT N'Silmek icin: sqlcmd ... -v ExecuteDelete=1 -i find-orphaned-pos-valor-test-data.sql';
    RETURN;
END;

-- ----------------------------------------------------------------------------
-- 3) SILME: FK sirasiyla, TEK transaction, TRY/CATCH. Yalnizca @ExecuteDelete=1
--    iken buraya ulasilir.
-- ----------------------------------------------------------------------------
DECLARE @Silinen TABLE (Tablo nvarchar(64) PRIMARY KEY, Adet int NOT NULL);

BEGIN TRY
    BEGIN TRANSACTION;

    -- 3.1) MuhasebeFisler'e referans veren alanlari NULL'a cek (FK Restrict) -
    -- PosTahsilatValorleri.MuhasebeFisId/TersKayitMuhasebeFisId,
    -- TahsilatOdemeBelgeleri.MuhasebeFisId, MuhasebeFisler'in KENDI self-referanslari.
    UPDATE v SET v.MuhasebeFisId = NULL, v.TersKayitMuhasebeFisId = NULL
    FROM muhasebe.PosTahsilatValorleri v WHERE v.TesisId IN (SELECT Id FROM @HedefTesisler);

    UPDATE b SET b.MuhasebeFisId = NULL
    FROM muhasebe.TahsilatOdemeBelgeleri b WHERE b.CariKartId IN (SELECT Id FROM @HedefCariKartlar);

    UPDATE f SET f.TersKayitFisId = NULL, f.IptalEdilenFisId = NULL
    FROM muhasebe.MuhasebeFisler f WHERE f.Id IN (SELECT Id FROM @HedefFisler);

    -- 3.2) Cocuk kayitlar (fis satirlari, degisiklik gecmisi) once silinir.
    DELETE FROM muhasebe.MuhasebeFisSatirlari WHERE MuhasebeFisId IN (SELECT Id FROM @HedefFisler);

    DELETE d FROM muhasebe.PosTahsilatValorDegisiklikGecmisleri d
    INNER JOIN muhasebe.PosTahsilatValorleri v ON v.Id = d.PosTahsilatValorId
    WHERE v.TesisId IN (SELECT Id FROM @HedefTesisler);

    -- 3.3) Fisler silinir (self-referanslar zaten temizlendi).
    DELETE FROM muhasebe.MuhasebeFisler WHERE Id IN (SELECT Id FROM @HedefFisler);
    INSERT INTO @Silinen VALUES ('MuhasebeFisler', @@ROWCOUNT);

    -- 3.4) PosTahsilatValorleri silinir.
    DELETE FROM muhasebe.PosTahsilatValorleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES ('PosTahsilatValorleri', @@ROWCOUNT);

    -- 3.5) TahsilatOdemeBelgeleri silinir (CariKartlar'dan ONCE).
    DELETE FROM muhasebe.TahsilatOdemeBelgeleri WHERE CariKartId IN (SELECT Id FROM @HedefCariKartlar);
    INSERT INTO @Silinen VALUES ('TahsilatOdemeBelgeleri', @@ROWCOUNT);

    -- 3.6) PosValorFisNoSayaclari - YALNIZCA hedef tesislere ait satirlar; paylasilan/gercek
    -- tesislerin sayaclarina KESINLIKLE dokunulmaz (WHERE TesisId IN (@HedefTesisler) disina
    -- ASLA cikilmaz).
    DELETE FROM muhasebe.PosValorFisNoSayaclari WHERE TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES ('PosValorFisNoSayaclari', @@ROWCOUNT);

    -- 3.7) MuhasebeHesapBakiyeleri.
    DELETE FROM muhasebe.MuhasebeHesapBakiyeleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES ('MuhasebeHesapBakiyeleri', @@ROWCOUNT);

    -- 3.8) CariHareketler - once self-FK (IliskiliCariHareketId) temizlenir, sonra silinir.
    UPDATE ch SET ch.IliskiliCariHareketId = NULL
    FROM muhasebe.CariHareketler ch WHERE ch.CariKartId IN (SELECT Id FROM @HedefCariKartlar);

    DELETE FROM muhasebe.CariHareketler WHERE CariKartId IN (SELECT Id FROM @HedefCariKartlar);
    INSERT INTO @Silinen VALUES ('CariHareketler', @@ROWCOUNT);

    -- 3.9) CariKartlar.
    DELETE FROM muhasebe.CariKartlar WHERE Id IN (SELECT Id FROM @HedefCariKartlar);
    INSERT INTO @Silinen VALUES ('CariKartlar', @@ROWCOUNT);

    -- 3.10) KasaBankaHesaplari.
    DELETE FROM muhasebe.KasaBankaHesaplari WHERE TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES ('KasaBankaHesaplari', @@ROWCOUNT);

    -- 3.11) MuhasebeDonemler.
    DELETE FROM muhasebe.MuhasebeDonemler WHERE TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES ('MuhasebeDonemler', @@ROWCOUNT);

    -- 3.12) MuhasebeHesapPlanlari (global, marker'li).
    DELETE FROM muhasebe.MuhasebeHesapPlanlari WHERE Id IN (SELECT Id FROM @HedefHesapPlanlari);
    INSERT INTO @Silinen VALUES ('MuhasebeHesapPlanlari', @@ROWCOUNT);

    -- 3.13) Tesisler.
    DELETE FROM dbo.Tesisler WHERE Id IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES ('Tesisler', @@ROWCOUNT);

    -- 3.14) Iller.
    DELETE FROM dbo.Iller WHERE Id IN (SELECT Id FROM @HedefIller);
    INSERT INTO @Silinen VALUES ('Iller', @@ROWCOUNT);

    -- 3.15) Kurumlar (en son - butun cocuklari silinmis olmali).
    DELETE FROM dbo.Kurumlar WHERE Id IN (SELECT Id FROM @HedefKurumlar);
    INSERT INTO @Silinen VALUES ('Kurumlar', @@ROWCOUNT);

    COMMIT TRANSACTION;

    PRINT N'=== SILME TAMAMLANDI - tablo bazinda silinen kayit sayilari ===';
    SELECT Tablo, Adet FROM @Silinen ORDER BY Tablo;

    -- 4) SONRASI dogrulama: ayni hedef Id kumeleri (silinmis olsalar bile @Hedef* tablo
    -- degiskenlerinde hala tutulur) uzerinden, artik HICBIR fiziksel kaydin kalmadigini
    -- (adet=0) teyit eder - "silinen adet" ile "GERCEKTEN kalan adet"in AYNI seyi kanitlamasi
    -- gerekmez (biri neyin silindigini, digeri neyin KALMADIGINI dogrudan sorgular).
    PRINT N'=== SILME SONRASI dogrulama: hedef kumelerde kalan kayit adetleri (0 olmali) ===';
    SELECT 'Kurumlar' AS Tablo, COUNT(*) AS KalanAdet FROM dbo.Kurumlar WHERE Id IN (SELECT Id FROM @HedefKurumlar)
    UNION ALL SELECT 'Tesisler', COUNT(*) FROM dbo.Tesisler WHERE Id IN (SELECT Id FROM @HedefTesisler)
    UNION ALL SELECT 'Iller', COUNT(*) FROM dbo.Iller WHERE Id IN (SELECT Id FROM @HedefIller)
    UNION ALL SELECT 'MuhasebeHesapPlanlari', COUNT(*) FROM muhasebe.MuhasebeHesapPlanlari WHERE Id IN (SELECT Id FROM @HedefHesapPlanlari)
    UNION ALL SELECT 'CariKartlar', COUNT(*) FROM muhasebe.CariKartlar WHERE Id IN (SELECT Id FROM @HedefCariKartlar)
    UNION ALL SELECT 'MuhasebeFisler', COUNT(*) FROM muhasebe.MuhasebeFisler WHERE Id IN (SELECT Id FROM @HedefFisler);
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
    BEGIN
        ROLLBACK TRANSACTION;
    END;

    DECLARE @HataMesaji nvarchar(4000) = ERROR_MESSAGE();
    DECLARE @HataNo int = ERROR_NUMBER();
    PRINT N'=== SILME BASARISIZ - TUM ISLEMLER GERI ALINDI (ROLLBACK) ===';
    PRINT N'Hata No: ' + CAST(@HataNo AS nvarchar(20)) + N' - ' + @HataMesaji;
    THROW;
END CATCH;
