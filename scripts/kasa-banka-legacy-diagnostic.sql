-- Read-only system-wide inventory. Includes soft-deleted references for historical usage.
-- Run on the intended STYS database. No business rows are modified.
SET NOCOUNT ON;
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
SELECT k.Id AS KasaBankaHesapId, k.TesisId, k.Tip, k.Kod, k.Ad, k.MuhasebeHesapPlaniId,
    k.MuhasebeTamKod, k.MuhasebeAktifMi, k.HareketGorebilirMi, k.DetayHesapMi,
    (SELECT COUNT_BIG(*) FROM muhasebe.TahsilatOdemeBelgeleri x WHERE x.KasaBankaHesapId = k.Id) AS TahsilatOdemeBelgesiSayisi,
    (SELECT COUNT_BIG(*) FROM dbo.RezervasyonOdemeler x WHERE x.KasaBankaHesapId = k.Id) AS RezervasyonOdemeSayisi,
    (SELECT COUNT_BIG(*) FROM muhasebe.KasaHareketleri x WHERE x.KasaBankaHesapId = k.Id) AS KasaHareketSayisi,
    (SELECT COUNT_BIG(*) FROM muhasebe.BankaHareketleri x WHERE x.KasaBankaHesapId = k.Id) AS BankaHareketSayisi,
    (SELECT COUNT_BIG(*) FROM entegrasyon.PosOdemeIslemleri x WHERE x.KasaBankaHesapId = k.Id) AS PosOdemeSayisi,
    (SELECT COUNT_BIG(*) FROM muhasebe.MuhasebeFisSatirlari x WHERE x.KasaBankaHesapId = k.Id) AS MuhasebeFisSatiriSayisi,
    (SELECT COUNT_BIG(*) FROM Belgeler b WHERE b.KasaBankaHesapId = k.Id AND b.MuhasebeGecmisiVarMi = 0) AS FisOlusturulmamisBelgeSayisi,
    k.YeniIslemIcinGecerliMi, k.Sorun,
    CASE WHEN k.YeniIslemIcinGecerliMi = 1 THEN N'Gecerli'
        ELSE N'Yeni islemden disla; belge planini incele, gecmisi koru' END AS OnerilenAksiyon
FROM Hesaplar k ORDER BY k.YeniIslemIcinGecerliMi, k.Id;

-- Result 2: ALL problematic documents, including cancelled/deleted documents.
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
SELECT Id AS TahsilatOdemeBelgesiId, BelgeNo, BelgeTipi, BelgeTarihi, Durum, IsDeleted,
    TesisId, KasaBankaHesapId, KaynakModul, KaynakId, RezervasyonOdemeId, ParaBirimi, Tutar,
    MuhasebeFisId, MuhasebeGecmisiVarMi, BaglantilarGuvenliMi, HedefSayisi, HedefHesapId, Sorun, OnerilenAksiyon
FROM Planlar ORDER BY MuhasebeGecmisiVarMi, Id;

-- Result 3: every actual FK, including differently named columns and future FKs.
DECLARE @sql nvarchar(max);
SELECT @sql = STRING_AGG(CAST(N'SELECT N''' + REPLACE(s.name + N'.' + t.name, N'''', N'''''')
    + N''' AS ReferansTablo, N''' + REPLACE(c.name, N'''', N'''''') + N''' AS ReferansKolon, '
    + QUOTENAME(c.name) + N' AS KasaBankaHesapId, COUNT_BIG(*) AS KullanimSayisi FROM '
    + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' WHERE ' + QUOTENAME(c.name)
    + N' IS NOT NULL GROUP BY ' + QUOTENAME(c.name) AS nvarchar(max)), N' UNION ALL ')
FROM sys.foreign_key_columns fk
JOIN sys.tables t ON t.object_id = fk.parent_object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = fk.parent_column_id
WHERE fk.referenced_object_id = OBJECT_ID(N'muhasebe.KasaBankaHesaplari');
IF @sql IS NOT NULL EXEC sys.sp_executesql @sql;
