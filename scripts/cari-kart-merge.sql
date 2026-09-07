-- ============================================================================
-- cari-kart-merge.sql
--
-- Ayni tesiste ayni TCKN/VKN ile mükerrer olan iki MUSTERI cari kartini tek
-- kartta birlestirir. "Canonical" kart KORUNUR; "NonCanonical" karttaki TUM
-- referanslar (cari hareket, tahsilat, satis belgesi, banka/kasa/stok hareket,
-- POS, rezervasyon, yetkili kisi, banka hesabi) ve MUHASEBE DETAY HESABI
-- (MuhasebeFisSatirlari.MuhasebeHesapPlaniId) canonical karta tasinir.
--
-- Muhasebe bakiyeleri (MuhasebeHesapBakiyeleri) (MaliYil, Donem) bazinda
-- toplanarak birlestirilir (BorcToplam/AlacakToplam toplanir, turev alanlar
-- yeniden hesaplanir). NonCanonical kart SOFT-DELETE edilir ve kullanilmayan
-- detay hesabi pasiflestirilir. Fiziksel hard delete yapilmaz.
--
-- VARSAYILAN: DRY-RUN. Hicbir sey degistirmez; sadece tasinacak kayit sayilarini
-- raporlar. Gercak birlestirme icin `-v ExecuteMerge=1` gec.
--
-- Kullanim:
--   Dry-run:
--     sqlcmd -S <sunucu> -d <db> -U <kullanici> -P "<sifre>" -C -i cari-kart-merge.sql
--   Merge:
--     sqlcmd -S <sunucu> -d <db> -U <kullanici> -P "<sifre>" -C -v ExecuteMerge=1 -i cari-kart-merge.sql
--
-- ONEMLI: Birlestirme ONCESI kartlarin GERCEKTEN ayni kisiye ait oldugunu dogrula.
-- Ayni TCKN ama FARKLI ad-soyad = veri girisi hatasi (TCKN yanlis), birlestirme DEGIL.
-- ============================================================================

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT OFF;

DECLARE @ExecuteMerge bit = CASE WHEN N'$(ExecuteMerge)' = N'1' THEN 1 ELSE 0 END;

-- ============================================================
-- 1) BIRLESTIRILECEK KARTLAR (buradaki Id degerlerini duzenle)
-- ============================================================
DECLARE @CanonicalId    int = 24;   -- KORUNACAK kart (canonical)
DECLARE @NonCanonicalId int = 26;   -- KAPATILACAK kart (buna birlestirilecek)

-- ============================================================
-- 2) VALIDATION
-- ============================================================
DECLARE @CanonicalTesisId int, @CanonicalTipi nvarchar(32), @CanonicalAd nvarchar(256);
SELECT @CanonicalTesisId = TesisId, @CanonicalTipi = CariTipi, @CanonicalAd = UnvanAdSoyad
FROM muhasebe.CariKartlar WHERE Id = @CanonicalId AND IsDeleted = 0;

IF @CanonicalTesisId IS NULL
BEGIN
    RAISERROR(N'Canonical cari kart bulunamadi veya silinmis. Id=%d', 16, 1, @CanonicalId);
    RETURN;
END

DECLARE @NonTesisId int, @NonTipi nvarchar(32), @NonAd nvarchar(256), @NonAktifMi bit;
SELECT @NonTesisId = TesisId, @NonTipi = CariTipi, @NonAd = UnvanAdSoyad, @NonAktifMi = AktifMi
FROM muhasebe.CariKartlar WHERE Id = @NonCanonicalId AND IsDeleted = 0;

IF @NonTesisId IS NULL
BEGIN
    RAISERROR(N'NonCanonical cari kart bulunamadi veya silinmis. Id=%d', 16, 1, @NonCanonicalId);
    RETURN;
END

IF @CanonicalTesisId <> @NonTesisId
BEGIN
    RAISERROR(N'Kartlar farkli tesiste; birlestirilemez. (%d vs %d)', 16, 1, @CanonicalTesisId, @NonTesisId);
    RETURN;
END

IF @CanonicalTipi <> @NonTipi
BEGIN
    RAISERROR(N'Kart tipleri farkli; birlestirilemez. (%s vs %s)', 16, 1, @CanonicalTipi, @NonTipi);
    RETURN;
END

IF @CanonicalId = @NonCanonicalId
BEGIN
    RAISERROR(N'Canonical ve NonCanonical ayni kart olamaz.', 16, 1);
    RETURN;
END

DECLARE @CanonicalHesapId    int = (SELECT MuhasebeHesapPlaniId FROM muhasebe.CariKartlar WHERE Id = @CanonicalId);
DECLARE @NonCanonicalHesapId int = (SELECT MuhasebeHesapPlaniId FROM muhasebe.CariKartlar WHERE Id = @NonCanonicalId);

-- ============================================================
-- 3) DRY-RUN RAPORU (tasinacak kayit adetleri)
-- ============================================================
PRINT N'=== BIRLESTIRME OZETI ===';
SELECT
    @CanonicalId AS CanonicalId, @CanonicalAd AS CanonicalAd, @CanonicalHesapId AS CanonicalHesapId,
    @NonCanonicalId AS NonCanonicalId, @NonAd AS NonCanonicalAd, @NonCanonicalHesapId AS NonCanonicalHesapId;

PRINT N'=== NonCanonical karta ait tasinacak kayitlar (ExecuteMerge=' + CAST(@ExecuteMerge AS nvarchar(1)) + N') ===';
SELECT N'CariHareketler' AS Tablo, COUNT(*) AS Adet FROM muhasebe.CariHareketler WHERE CariKartId = @NonCanonicalId
UNION ALL SELECT N'TahsilatOdemeBelgeleri', COUNT(*) FROM muhasebe.TahsilatOdemeBelgeleri WHERE CariKartId = @NonCanonicalId
UNION ALL SELECT N'SatisBelgeleri', COUNT(*) FROM muhasebe.SatisBelgeleri WHERE CariKartId = @NonCanonicalId
UNION ALL SELECT N'BankaHareketleri', COUNT(*) FROM muhasebe.BankaHareketleri WHERE CariKartId = @NonCanonicalId
UNION ALL SELECT N'KasaHareketleri', COUNT(*) FROM muhasebe.KasaHareketleri WHERE CariKartId = @NonCanonicalId
UNION ALL SELECT N'StokHareketleri', COUNT(*) FROM muhasebe.StokHareketleri WHERE CariKartId = @NonCanonicalId
UNION ALL SELECT N'PosOdemeIslemleri', COUNT(*) FROM entegrasyon.PosOdemeIslemleri WHERE CariKartId = @NonCanonicalId
UNION ALL SELECT N'Rezervasyonlar', COUNT(*) FROM dbo.Rezervasyonlar WHERE CariKartId = @NonCanonicalId
UNION ALL SELECT N'Tesisler(VarsayilanCari)', COUNT(*) FROM dbo.Tesisler WHERE RezervasyonMisafirVarsayilanCariKartId = @NonCanonicalId
UNION ALL SELECT N'CariKartYetkiliKisileri', COUNT(*) FROM muhasebe.CariKartYetkiliKisileri WHERE CariKartId = @NonCanonicalId
UNION ALL SELECT N'CariKartBankaHesaplari', COUNT(*) FROM muhasebe.CariKartBankaHesaplari WHERE CariKartId = @NonCanonicalId
UNION ALL SELECT N'MuhasebeFisSatirlari(CariKartId)', COUNT(*) FROM muhasebe.MuhasebeFisSatirlari WHERE CariKartId = @NonCanonicalId
UNION ALL SELECT N'MuhasebeFisSatirlari(HesapPlaniId)', COUNT(*) FROM muhasebe.MuhasebeFisSatirlari WHERE MuhasebeHesapPlaniId = @NonCanonicalHesapId AND @NonCanonicalHesapId IS NOT NULL
UNION ALL SELECT N'MuhasebeHesapBakiyeleri', COUNT(*) FROM muhasebe.MuhasebeHesapBakiyeleri WHERE MuhasebeHesapPlaniId = @NonCanonicalHesapId AND @NonCanonicalHesapId IS NOT NULL;

IF @ExecuteMerge = 0
BEGIN
    PRINT N'@ExecuteMerge = 0 (varsayilan) - HICBIR SEY DEGISMEDI.';
    PRINT N'Birlestirmek icin: sqlcmd ... -v ExecuteMerge=1 -i cari-kart-merge.sql';
    RETURN;
END

-- ============================================================
-- 4) BIRLESTIRME
-- ============================================================
BEGIN TRY
    BEGIN TRANSACTION;

    -- 4.1 CariKartId referanslarini tasi
    UPDATE muhasebe.CariHareketler        SET CariKartId = @CanonicalId WHERE CariKartId = @NonCanonicalId;
    UPDATE muhasebe.TahsilatOdemeBelgeleri SET CariKartId = @CanonicalId WHERE CariKartId = @NonCanonicalId;
    UPDATE muhasebe.SatisBelgeleri         SET CariKartId = @CanonicalId WHERE CariKartId = @NonCanonicalId;
    UPDATE muhasebe.BankaHareketleri       SET CariKartId = @CanonicalId WHERE CariKartId = @NonCanonicalId;
    UPDATE muhasebe.KasaHareketleri        SET CariKartId = @CanonicalId WHERE CariKartId = @NonCanonicalId;
    UPDATE muhasebe.StokHareketleri        SET CariKartId = @CanonicalId WHERE CariKartId = @NonCanonicalId;
    UPDATE entegrasyon.PosOdemeIslemleri   SET CariKartId = @CanonicalId WHERE CariKartId = @NonCanonicalId;
    UPDATE dbo.Rezervasyonlar              SET CariKartId = @CanonicalId WHERE CariKartId = @NonCanonicalId;
    UPDATE dbo.Tesisler SET RezervasyonMisafirVarsayilanCariKartId = @CanonicalId WHERE RezervasyonMisafirVarsayilanCariKartId = @NonCanonicalId;
    UPDATE kantin.Kantinler                SET PerakendeCariKartId = @CanonicalId WHERE PerakendeCariKartId = @NonCanonicalId;

    -- Yetkili kisiler / banka hesaplari: canonical'da ayni kayit yoksa tasi.
    UPDATE y SET y.CariKartId = @CanonicalId
    FROM muhasebe.CariKartYetkiliKisileri y
    WHERE y.CariKartId = @NonCanonicalId
      AND NOT EXISTS (SELECT 1 FROM muhasebe.CariKartYetkiliKisileri c
                      WHERE c.CariKartId = @CanonicalId AND c.AdSoyad = y.AdSoyad AND c.Telefon = y.Telefon);

    UPDATE b SET b.CariKartId = @CanonicalId
    FROM muhasebe.CariKartBankaHesaplari b
    WHERE b.CariKartId = @NonCanonicalId
      AND NOT EXISTS (SELECT 1 FROM muhasebe.CariKartBankaHesaplari c
                      WHERE c.CariKartId = @CanonicalId AND c.Iban = b.Iban);

    -- 4.2 Muhasebe fis satirlari (CariKartId + detay hesap)
    UPDATE muhasebe.MuhasebeFisSatirlari SET CariKartId = @CanonicalId WHERE CariKartId = @NonCanonicalId;

    IF @CanonicalHesapId IS NOT NULL AND @NonCanonicalHesapId IS NOT NULL AND @CanonicalHesapId <> @NonCanonicalHesapId
    BEGIN
        -- 4.3 Detay hesap birlestirme: fis satirlarini canonical hesabina tasi
        UPDATE muhasebe.MuhasebeFisSatirlari
        SET MuhasebeHesapPlaniId = @CanonicalHesapId
        WHERE MuhasebeHesapPlaniId = @NonCanonicalHesapId;

        -- 4.4 Bakiyeleri (MaliYil, Donem) bazinda birlestir
        UPDATE canon
        SET canon.BorcToplam   = canon.BorcToplam + non.BorcToplam,
            canon.AlacakToplam = canon.AlacakToplam + non.AlacakToplam,
            canon.SonGuncellemeTarihi = GETUTCDATE()
        FROM muhasebe.MuhasebeHesapBakiyeleri canon
        INNER JOIN muhasebe.MuhasebeHesapBakiyeleri non
            ON non.MuhasebeHesapPlaniId = @NonCanonicalHesapId
           AND canon.MuhasebeHesapPlaniId = @CanonicalHesapId
           AND canon.MaliYil = non.MaliYil
           AND canon.Donem = non.Donem;

        -- Canonical'da olmayan (MaliYil, Donem) bakiyelerini tasi
        INSERT INTO muhasebe.MuhasebeHesapBakiyeleri
            (TesisId, MaliYil, Donem, MuhasebeHesapPlaniId, HesapKodu, HesapAdi, KonsolideMi,
             BorcToplam, AlacakToplam, BorcBakiye, AlacakBakiye, NetBakiye, BakiyeTipi, HesapSeviyesi, UstHesapKodu,
             SonGuncellemeTarihi, IsDeleted, CreatedAt, CreatedBy)
        SELECT non.TesisId, non.MaliYil, non.Donem, @CanonicalHesapId,
               ch.TamKod, ch.Ad, non.KonsolideMi,
               non.BorcToplam, non.AlacakToplam, non.BorcBakiye, non.AlacakBakiye, non.NetBakiye, non.BakiyeTipi,
               LEN(ch.TamKod) - LEN(REPLACE(ch.TamKod, '.', '')) + 1,
               CASE WHEN CHARINDEX('.', ch.TamKod) = 0 THEN NULL
                    ELSE LEFT(ch.TamKod, LEN(ch.TamKod) - CHARINDEX('.', REVERSE(ch.TamKod))) END,
               GETUTCDATE(), 0, GETUTCDATE(), N'cari-merge'
        FROM muhasebe.MuhasebeHesapBakiyeleri non
        INNER JOIN muhasebe.MuhasebeHesapPlanlari ch ON ch.Id = @CanonicalHesapId
        WHERE non.MuhasebeHesapPlaniId = @NonCanonicalHesapId
          AND NOT EXISTS (SELECT 1 FROM muhasebe.MuhasebeHesapBakiyeleri c
                          WHERE c.MuhasebeHesapPlaniId = @CanonicalHesapId AND c.MaliYil = non.MaliYil AND c.Donem = non.Donem);

        -- Turev alanlari canonical hesabin birlesik toplamlarindan yeniden hesapla
        UPDATE canon
        SET canon.NetBakiye    = canon.BorcToplam - canon.AlacakToplam,
            canon.BorcBakiye   = CASE WHEN canon.BorcToplam - canon.AlacakToplam > 0 THEN canon.BorcToplam - canon.AlacakToplam ELSE 0 END,
            canon.AlacakBakiye = CASE WHEN canon.BorcToplam - canon.AlacakToplam < 0 THEN -1 * (canon.BorcToplam - canon.AlacakToplam) ELSE 0 END,
            canon.BakiyeTipi   = CASE WHEN canon.BorcToplam - canon.AlacakToplam > 0 THEN N'Borc'
                                      WHEN canon.BorcToplam - canon.AlacakToplam < 0 THEN N'Alacak' ELSE N'Sifir' END
        FROM muhasebe.MuhasebeHesapBakiyeleri canon
        WHERE canon.MuhasebeHesapPlaniId = @CanonicalHesapId;

        -- NonCanonical hesabin bakiyelerini sil
        DELETE FROM muhasebe.MuhasebeHesapBakiyeleri WHERE MuhasebeHesapPlaniId = @NonCanonicalHesapId;

        -- NonCanonical detay hesabini pasiflestir
        UPDATE muhasebe.MuhasebeHesapPlanlari SET AktifMi = 0 WHERE Id = @NonCanonicalHesapId;
    END

    -- 4.5 Profil tamamlama: canonical'in BOS alanlarini doldur (dolu alan korunur)
    UPDATE c
    SET Ad           = CASE WHEN c.Ad IS NULL OR LTRIM(RTRIM(c.Ad)) = '' THEN n.Ad ELSE c.Ad END,
        Soyad        = CASE WHEN c.Soyad IS NULL OR LTRIM(RTRIM(c.Soyad)) = '' THEN n.Soyad ELSE c.Soyad END,
        VergiDairesi = CASE WHEN c.VergiDairesi IS NULL OR LTRIM(RTRIM(c.VergiDairesi)) = '' THEN n.VergiDairesi ELSE c.VergiDairesi END,
        Telefon      = CASE WHEN c.Telefon IS NULL OR LTRIM(RTRIM(c.Telefon)) = '' THEN n.Telefon ELSE c.Telefon END,
        Eposta       = CASE WHEN c.Eposta IS NULL OR LTRIM(RTRIM(c.Eposta)) = '' THEN n.Eposta ELSE c.Eposta END,
        Adres        = CASE WHEN c.Adres IS NULL OR LTRIM(RTRIM(c.Adres)) = '' THEN n.Adres ELSE c.Adres END,
        Il           = CASE WHEN c.Il IS NULL OR LTRIM(RTRIM(c.Il)) = '' THEN n.Il ELSE c.Il END,
        Ilce         = CASE WHEN c.Ilce IS NULL OR LTRIM(RTRIM(c.Ilce)) = '' THEN n.Ilce ELSE c.Ilce END
    FROM muhasebe.CariKartlar c
    INNER JOIN muhasebe.CariKartlar n ON n.Id = @NonCanonicalId
    WHERE c.Id = @CanonicalId;

    -- 4.6 NonCanonical karti soft-delete et
    UPDATE muhasebe.CariKartlar
    SET IsDeleted = 1, AktifMi = 0, DeletedAt = GETUTCDATE(), DeletedBy = N'cari-merge'
    WHERE Id = @NonCanonicalId;

    COMMIT TRANSACTION;
    PRINT N'=== BIRLESTIRME TAMAMLANDI ===';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    PRINT N'=== BIRLESTIRME BASARISIZ - GERI ALINDI ===';
    THROW;
END CATCH;
