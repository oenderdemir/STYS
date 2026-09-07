-- Run diagnostic first; retain preview/output with the maintenance record.
-- Use a maintenance window with application writers and posting jobs stopped.
-- Only documents and their exact reservation payments are repaired.
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @ExecuteRepair bit = 0;
IF @@TRANCOUNT <> 0 THROW 51000, 'Run outside an existing transaction.', 1;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRY
BEGIN TRANSACTION;
-- Serializable reads retain account, target and history/range locks until commit.
;WITH Hesaplar AS (
    SELECT k.*, m.TamKod AS MuhasebeTamKod, m.AktifMi AS MuhasebeAktifMi,
        m.HareketGorebilirMi, m.DetayHesapMi,
        CAST(CASE WHEN k.IsDeleted = 0 AND k.AktifMi = 1 AND m.Id IS NOT NULL
            AND m.IsDeleted = 0 AND m.AktifMi = 1 AND m.HareketGorebilirMi = 1 AND m.DetayHesapMi = 1
            THEN 1 ELSE 0 END AS bit) AS YeniIslemIcinGecerliMi,
        CONCAT(CASE WHEN k.IsDeleted = 1 THEN N'Finansal hesap silinmis; ' ELSE N'' END,
            CASE WHEN k.AktifMi = 0 THEN N'Finansal hesap pasif; ' ELSE N'' END,
            CASE WHEN m.Id IS NULL THEN N'Muhasebe hesabi yok; ' ELSE N'' END,
            CASE WHEN m.IsDeleted = 1 THEN N'Muhasebe hesabi silinmis; ' ELSE N'' END,
            CASE WHEN m.AktifMi = 0 THEN N'Muhasebe hesabi pasif; ' ELSE N'' END,
            CASE WHEN m.HareketGorebilirMi = 0 THEN N'Hareket goremez; ' ELSE N'' END,
            CASE WHEN m.DetayHesapMi = 0 THEN N'Detay hesap degil; ' ELSE N'' END) AS Sorun
    FROM muhasebe.KasaBankaHesaplari k
    LEFT JOIN muhasebe.MuhasebeHesapPlanlari m ON m.Id = k.MuhasebeHesapPlaniId
), Belgeler AS (
    SELECT b.*, k.Tip AS HesapTipi, k.Sorun, COALESCE(r.TesisId, c.TesisId) AS TesisId,
        ro.Id AS RezervasyonOdemeId,
        CAST(CASE WHEN b.MuhasebeFisId IS NOT NULL OR b.MuhasebeFisOlusturmaTarihi IS NOT NULL
            OR EXISTS (SELECT 1 FROM muhasebe.MuhasebeFisler f
                WHERE (f.KaynakModul = N'TahsilatOdemeBelgesi' AND f.KaynakId = b.Id)
                   OR (b.KaynakId IS NOT NULL AND f.KaynakModul = b.KaynakModul AND f.KaynakId = b.KaynakId)
                   OR (f.KaynakModul = N'KantinSatis' AND EXISTS (
                       SELECT 1 FROM kantin.KantinSatisOdemeleri ko WHERE ko.TahsilatOdemeBelgesiId = b.Id AND ko.KantinSatisId = f.KaynakId))
                   OR (f.KaynakModul = N'KasaHareket' AND EXISTS (
                       SELECT 1 FROM muhasebe.KasaHareketleri h WHERE ((h.KaynakModul = N'TahsilatOdemeBelgesi' AND h.KaynakId = b.Id)
                           OR (h.KaynakModul = b.KaynakModul AND h.KaynakId = b.KaynakId)) AND h.Id = f.KaynakId))
                   OR (f.KaynakModul = N'BankaHareket' AND EXISTS (
                       SELECT 1 FROM muhasebe.BankaHareketleri h WHERE ((h.KaynakModul = N'TahsilatOdemeBelgesi' AND h.KaynakId = b.Id)
                           OR (h.KaynakModul = b.KaynakModul AND h.KaynakId = b.KaynakId)) AND h.Id = f.KaynakId)))
            OR EXISTS (SELECT 1 FROM muhasebe.CariHareketler h JOIN muhasebe.MuhasebeFisler f
                ON f.KaynakModul = N'CariHareket' AND f.KaynakId = h.Id
                WHERE h.KaynakModul = N'TahsilatOdemeBelgesi' AND h.KaynakId = b.Id)
            OR EXISTS (SELECT 1 FROM muhasebe.PosTahsilatValorleri v WHERE v.TahsilatOdemeBelgesiId = b.Id AND
                (v.MuhasebeFisId IS NOT NULL OR EXISTS (SELECT 1 FROM muhasebe.MuhasebeFisler f
                    WHERE f.KaynakModul = N'PosTahsilatValorTransferi' AND f.KaynakId = v.Id)))
            OR EXISTS (SELECT 1 FROM kantin.KantinSatisOdemeleri ko JOIN kantin.KantinSatislar ks ON ks.Id = ko.KantinSatisId
                WHERE ko.TahsilatOdemeBelgesiId = b.Id AND ks.MuhasebeFisId IS NOT NULL)
            THEN 1 ELSE 0 END AS bit) AS MuhasebeGecmisiVarMi,
        CAST(CASE WHEN b.IsDeleted = 0 AND b.Durum = N'Aktif' AND b.Tutar > 0
            AND c.Id IS NOT NULL AND c.IsDeleted = 0
            AND (k.TesisId IS NULL OR k.TesisId = COALESCE(r.TesisId, c.TesisId))
            AND (c.TesisId IS NULL OR r.TesisId IS NULL OR c.TesisId = r.TesisId)
            AND ((k.Tip = N'NakitKasa' AND b.OdemeYontemi = N'Nakit')
                OR (k.Tip = N'Banka' AND b.OdemeYontemi = N'HavaleEft'))
            AND ((NULLIF(b.KaynakModul, N'') IS NULL AND b.KaynakId IS NULL AND ro.Id IS NULL)
                OR (b.KaynakModul = N'Manuel' AND b.KaynakId IS NULL AND ro.Id IS NULL)
                OR (b.KaynakModul = N'Rezervasyon' AND b.KaynakId = ro.Id
                    AND ro.IsDeleted = 0 AND ro.Durum = N'Aktif' AND r.IsDeleted = 0
                    AND ro.KasaBankaHesapId = b.KasaBankaHesapId AND ro.PosOdemeIslemiId IS NULL
                    AND ro.ParaBirimi = b.ParaBirimi AND ro.OdemeTutari = b.Tutar AND ro.OdemeTipi = b.OdemeYontemi))
            AND NOT EXISTS (SELECT 1 FROM entegrasyon.PosOdemeIslemleri p WHERE p.RezervasyonOdemeId = ro.Id)
            AND NOT EXISTS (SELECT 1 FROM kantin.KantinSatisOdemeleri ko WHERE ko.TahsilatOdemeBelgesiId = b.Id
                OR (b.KaynakModul = N'KantinSatisOdeme' AND b.KaynakId = ko.Id))
            AND NOT EXISTS (SELECT 1 FROM muhasebe.PosTahsilatValorleri v WHERE v.TahsilatOdemeBelgesiId = b.Id)
            AND NOT EXISTS (SELECT 1 FROM muhasebe.KasaHareketleri h WHERE
                (h.KaynakModul = N'TahsilatOdemeBelgesi' AND h.KaynakId = b.Id)
                OR (h.KaynakModul = b.KaynakModul AND h.KaynakId = b.KaynakId))
            AND NOT EXISTS (SELECT 1 FROM muhasebe.BankaHareketleri h WHERE
                (h.KaynakModul = N'TahsilatOdemeBelgesi' AND h.KaynakId = b.Id)
                OR (h.KaynakModul = b.KaynakModul AND h.KaynakId = b.KaynakId))
            AND NOT EXISTS (SELECT 1 FROM muhasebe.CariHareketler h WHERE h.KaynakModul = N'TahsilatOdemeBelgesi' AND h.KaynakId = b.Id)
            AND b.KapatilacakCariHareketId IS NULL
            THEN 1 ELSE 0 END AS bit) AS BaglantilarGuvenliMi
    FROM muhasebe.TahsilatOdemeBelgeleri b
    JOIN Hesaplar k ON k.Id = b.KasaBankaHesapId AND k.YeniIslemIcinGecerliMi = 0
    LEFT JOIN muhasebe.CariKartlar c ON c.Id = b.CariKartId
    LEFT JOIN dbo.RezervasyonOdemeler ro ON ro.TahsilatOdemeBelgesiId = b.Id
    LEFT JOIN dbo.Rezervasyonlar r ON r.Id = ro.RezervasyonId
), Planlar AS (
    SELECT b.*, hedef.HedefSayisi, hedef.HedefHesapId,
        CASE WHEN b.MuhasebeGecmisiVarMi = 1 THEN N'B - Muhasebe gecmisi korunacak; manuel inceleme'
            WHEN b.BaglantilarGuvenliMi = 0 THEN N'A - Baglantilar/durum/tesis belirsiz; manuel inceleme'
            WHEN hedef.HedefSayisi = 0 THEN N'A - Uygun tesis hesabi yok; yonetici Finansal Hesaplar ekranindan olusturmali'
            WHEN hedef.HedefSayisi > 1 THEN N'A - Birden fazla hedef hesap; manuel secim gerekli'
            ELSE N'A - Guvenli repair adayi' END AS OnerilenAksiyon
    FROM Belgeler b
    OUTER APPLY (SELECT COUNT(*) AS HedefSayisi, MIN(k.Id) AS HedefHesapId FROM Hesaplar k
        JOIN muhasebe.MuhasebeHesapPlanlari m ON m.Id = k.MuhasebeHesapPlaniId
        WHERE k.YeniIslemIcinGecerliMi = 1 AND k.TesisId = b.TesisId AND m.TesisId = b.TesisId
            AND k.Tip = b.HesapTipi AND k.ParaBirimi = b.ParaBirimi) hedef
)
SELECT * INTO #LegacyRepairPlan FROM Planlar;
SELECT Id AS TahsilatOdemeBelgesiId, RezervasyonOdemeId, KasaBankaHesapId AS EskiHesapId,
    HedefHesapId, HedefSayisi, MuhasebeGecmisiVarMi, BaglantilarGuvenliMi, OnerilenAksiyon
FROM #LegacyRepairPlan ORDER BY Id;

IF @ExecuteRepair = 1
BEGIN
    DECLARE @Updated TABLE (BelgeId int, EskiHesapId int, YeniHesapId int);
    UPDATE b SET KasaBankaHesapId = p.HedefHesapId
    OUTPUT inserted.Id, deleted.KasaBankaHesapId, inserted.KasaBankaHesapId INTO @Updated
    FROM muhasebe.TahsilatOdemeBelgeleri b
    JOIN #LegacyRepairPlan p ON p.Id = b.Id AND p.KasaBankaHesapId = b.KasaBankaHesapId
    WHERE p.MuhasebeGecmisiVarMi = 0 AND p.BaglantilarGuvenliMi = 1 AND p.HedefSayisi = 1;

    UPDATE ro SET KasaBankaHesapId = u.YeniHesapId
    FROM dbo.RezervasyonOdemeler ro
    JOIN @Updated u ON u.BelgeId = ro.TahsilatOdemeBelgesiId
    JOIN #LegacyRepairPlan p ON p.Id = u.BelgeId AND p.RezervasyonOdemeId = ro.Id
    WHERE ro.KasaBankaHesapId = u.EskiHesapId;
    SELECT * FROM @Updated ORDER BY BelgeId;
END;
DROP TABLE #LegacyRepairPlan;
COMMIT TRANSACTION;
SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    THROW;
END CATCH;
