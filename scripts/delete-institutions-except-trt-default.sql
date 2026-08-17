-- ============================================================================
-- delete-institutions-except-trt-default.sql
--
-- TRT (Kod='TRT') ve varsayilan kurum (Kod='DEFAULT') HARICINDEKI TUM kurumlari
-- ve bu kurumlara FK ile bagli TUM kayitlari (tesis, bina, oda, rezervasyon,
-- kamp, restoran, muhasebe, e-belge, entegrasyon/POS, kullanici-tesis sahipligi,
-- kullanici-kurum baglanti kayitlari) tek bir transaction icinde, FK sirasiyla siler.
--
-- AYRICA: adinda 'Test' gecen TUM tesisler - HANGI kuruma bagli olursa olsun
-- (varsayilan kurum ve TRT dahil) - ve bunlara FK ile bagli tum kayitlar silinir.
-- (Test tesisleri genellikle test kurumlarina bagli oldugu icin kurum kurali zaten
-- yakalar; bu ek kural, testlerin dogrudan DEFAULT/TRT altinda yarattigi 'Test'
-- tesislerini de temizlemek icindir.)
--
-- DOKUNULMAYANLAR (global/paylasilan veriler):
--   - TODBase.* (kullanicilar, roller, menuler), dbo.Countries, dbo.Iller
--   - Global lookup tablolari: KonaklamaTipleri, KonaklamaTipiIcerikKalemleri,
--     MisafirTipleri, OdaSiniflari, OdaOzellikleri, IsletmeAlaniSiniflari,
--     GlobalEkHizmetTanimlari, KampBasvuruSahibiTipleri, KampBasvuruSahipleri,
--     KampAkrabalikTipleri, KampKatilimciTipleri, KampParametreleri,
--     KampYasUcretKurallari, KdvIstisnaTanimlari, PaketTurleri, TasinirKodlar,
--     restoran.MenuKategoriTanimlari
--   - Kullanici kayitlari (TODBase.Users) SILINMEZ; yalnizca silinen kurumlara ait
--     identity.UserKurums baglanti satirlari silinir.
--
-- VARSAYILAN DAVRANIS: DRY-RUN. Hicbir DELETE/UPDATE calismaz; yalnizca silinecek
-- aday kayitlarin tablo bazinda ADETLERI ve saklanacak kurumlarin kimlikleri
-- SELECT/PRINT edilir.
--
-- Kullanim (sqlcmd, dosyayi HIC DEGISTIRMEDEN):
--   Dry-run (varsayilan):
--     sqlcmd -S localhost,14333 -d STYSDB -U sa -P "Strong!Pass1" -C -i delete-institutions-except-trt-default.sql
--   Silme (komut satirindan ACIKCA onaylanarak):
--     sqlcmd -S localhost,14333 -d STYSDB -U sa -P "Strong!Pass1" -C -v ExecuteDelete=1 -i delete-institutions-except-trt-default.sql
--
-- NOT (sqlcmd scripting-variable davranisi): `-v ExecuteDelete=1` VERILMEZSE sqlcmd
-- "'ExecuteDelete' scripting variable not defined." seklinde bir BILGI mesaji basar
-- (bu bir T-SQL SYNTAX hatasi DEGILDIR) ve `$(ExecuteDelete)` metni OLDUGU GIBI
-- kalir - bu metin '1'e ESIT OLMADIGI icin karsilastirma dogal olarak dry-run'a duser.
--
-- GUVENLIK:
--   - Saklanacak kurum seti (Kod IN ('DEFAULT','TRT')) BOSSA script HICBIR SEY
--     yapmadan durur (yanlislikla TUM veriyi silmeyi onler).
--   - DONGUSEL FK referanslari (RezervasyonOdemeler <-> PosOdemeIslemleri,
--     Tesisler.RezervasyonMisafirVarsayilanCariKartId <-> CariKartlar.TesisId ve
--     tum self-FK'ler) silme ONCESI hedef kayitlarda NULL'a cekilir.
--   - TEK transaction + TRY/CATCH; herhangi bir adim basarisiz olursa ROLLBACK.
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT OFF; -- TRY/CATCH icinde kendi ROLLBACK'imizi kontrollu yapabilmek icin.
SET QUOTED_IDENTIFIER ON;

DECLARE @ExecuteDelete bit = CASE WHEN N'$(ExecuteDelete)' = N'1' THEN 1 ELSE 0 END;

-- ----------------------------------------------------------------------------
-- 1) Saklanacak kurumlar + hedef (silinecek) Id kumeleri.
-- ----------------------------------------------------------------------------
DECLARE @SaklanacakKurum TABLE (Id int PRIMARY KEY, Kod nvarchar(64), Ad nvarchar(200));
INSERT INTO @SaklanacakKurum (Id, Kod, Ad)
SELECT Id, Kod, Ad FROM dbo.Kurumlar WHERE Kod IN (N'DEFAULT', N'TRT');

IF NOT EXISTS (SELECT 1 FROM @SaklanacakKurum)
BEGIN
    PRINT N'=== DURDURULDU: Kod IN (''DEFAULT'',''TRT'') eslesen HICBIR kurum bulunamadi. ===';
    PRINT N'GUVENLIK nedeniyle hicbir kayit silinmedi. Saklanacak kurum Kodlarini kontrol edin.';
    RETURN;
END;

DECLARE @HedefKurumlar        TABLE (Id int PRIMARY KEY);
DECLARE @HedefTesisler        TABLE (Id int PRIMARY KEY);
DECLARE @HedefBinalar         TABLE (Id int PRIMARY KEY);
DECLARE @HedefOdalar          TABLE (Id int PRIMARY KEY);
DECLARE @HedefRezervasyonlar  TABLE (Id int PRIMARY KEY);
DECLARE @HedefCariKartlar     TABLE (Id int PRIMARY KEY);
DECLARE @HedefDepolar         TABLE (Id int PRIMARY KEY);
DECLARE @HedefHesaplar        TABLE (Id int PRIMARY KEY);
DECLARE @HedefKasaBanka       TABLE (Id int PRIMARY KEY);
DECLARE @HedefHesapPlanlari   TABLE (Id int PRIMARY KEY);
DECLARE @HedefTasinirKartlar  TABLE (Id int PRIMARY KEY);
DECLARE @HedefFisler          TABLE (Id int PRIMARY KEY);
DECLARE @HedefSatisBelgeleri  TABLE (Id int PRIMARY KEY);
DECLARE @HedefEBelgeKayitlari TABLE (Id int PRIMARY KEY);
DECLARE @HedefTahsilatOdeme   TABLE (Id int PRIMARY KEY);
DECLARE @HedefKampProgramlari TABLE (Id int PRIMARY KEY);
DECLARE @HedefKampDonemleri   TABLE (Id int PRIMARY KEY);
DECLARE @HedefKampBasvurulari TABLE (Id int PRIMARY KEY);
DECLARE @HedefRestoranlar     TABLE (Id int PRIMARY KEY);
DECLARE @HedefIsletmeAlanlari TABLE (Id int PRIMARY KEY);
DECLARE @HedefEkHizmetler     TABLE (Id int PRIMARY KEY);
DECLARE @HedefEkHizmetTarifeleri TABLE (Id int PRIMARY KEY);
DECLARE @HedefIndirimKurallari TABLE (Id int PRIMARY KEY);
DECLARE @HedefTesisOdaTipleri TABLE (Id int PRIMARY KEY);

INSERT INTO @HedefKurumlar (Id)
SELECT Id FROM dbo.Kurumlar
WHERE ISNULL(Kod, N'') NOT IN (N'DEFAULT', N'TRT');

INSERT INTO @HedefTesisler (Id)
SELECT Id FROM dbo.Tesisler
WHERE KurumId IN (SELECT Id FROM @HedefKurumlar)
   OR Ad LIKE N'%Test%';

INSERT INTO @HedefBinalar (Id)
SELECT Id FROM dbo.Binalar WHERE TesisId IN (SELECT Id FROM @HedefTesisler);

INSERT INTO @HedefOdalar (Id)
SELECT Id FROM dbo.Odalar WHERE BinaId IN (SELECT Id FROM @HedefBinalar);

INSERT INTO @HedefRezervasyonlar (Id)
SELECT Id FROM dbo.Rezervasyonlar WHERE TesisId IN (SELECT Id FROM @HedefTesisler);

INSERT INTO @HedefCariKartlar (Id)
SELECT Id FROM muhasebe.CariKartlar WHERE TesisId IN (SELECT Id FROM @HedefTesisler);

INSERT INTO @HedefDepolar (Id)
SELECT Id FROM muhasebe.Depolar WHERE TesisId IN (SELECT Id FROM @HedefTesisler);

INSERT INTO @HedefHesaplar (Id)
SELECT Id FROM muhasebe.Hesaplar WHERE TesisId IN (SELECT Id FROM @HedefTesisler);

INSERT INTO @HedefKasaBanka (Id)
SELECT Id FROM muhasebe.KasaBankaHesaplari WHERE TesisId IN (SELECT Id FROM @HedefTesisler);

INSERT INTO @HedefHesapPlanlari (Id)
SELECT Id FROM muhasebe.MuhasebeHesapPlanlari WHERE TesisId IN (SELECT Id FROM @HedefTesisler);

INSERT INTO @HedefTasinirKartlar (Id)
SELECT Id FROM muhasebe.TasinirKartlar WHERE TesisId IN (SELECT Id FROM @HedefTesisler);

INSERT INTO @HedefFisler (Id)
SELECT Id FROM muhasebe.MuhasebeFisler WHERE TesisId IN (SELECT Id FROM @HedefTesisler);

INSERT INTO @HedefSatisBelgeleri (Id)
SELECT Id FROM muhasebe.SatisBelgeleri
WHERE KurumId IN (SELECT Id FROM @HedefKurumlar)
   OR TesisId IN (SELECT Id FROM @HedefTesisler);

INSERT INTO @HedefEBelgeKayitlari (Id)
SELECT Id FROM muhasebe.EBelgeKayitlari
WHERE KurumId IN (SELECT Id FROM @HedefKurumlar)
   OR SatisBelgesiId IN (SELECT Id FROM @HedefSatisBelgeleri);

INSERT INTO @HedefTahsilatOdeme (Id)
SELECT Id FROM muhasebe.TahsilatOdemeBelgeleri
WHERE CariKartId IN (SELECT Id FROM @HedefCariKartlar)
   OR KasaBankaHesapId IN (SELECT Id FROM @HedefKasaBanka)
   OR MuhasebeFisId IN (SELECT Id FROM @HedefFisler)
   OR KapatilacakCariHareketId IN (SELECT Id FROM muhasebe.CariHareketler WHERE CariKartId IN (SELECT Id FROM @HedefCariKartlar));

INSERT INTO @HedefKampProgramlari (Id)
SELECT Id FROM dbo.KampProgramlari WHERE KurumId IN (SELECT Id FROM @HedefKurumlar);

INSERT INTO @HedefKampDonemleri (Id)
SELECT Id FROM dbo.KampDonemleri WHERE KampProgramiId IN (SELECT Id FROM @HedefKampProgramlari);

INSERT INTO @HedefKampBasvurulari (Id)
SELECT Id FROM dbo.KampBasvurulari
WHERE TesisId IN (SELECT Id FROM @HedefTesisler)
   OR KampDonemiId IN (SELECT Id FROM @HedefKampDonemleri);

INSERT INTO @HedefRestoranlar (Id)
SELECT Id FROM restoran.Restoranlar WHERE TesisId IN (SELECT Id FROM @HedefTesisler);

INSERT INTO @HedefIsletmeAlanlari (Id)
SELECT Id FROM dbo.IsletmeAlanlari WHERE BinaId IN (SELECT Id FROM @HedefBinalar);

INSERT INTO @HedefEkHizmetler (Id)
SELECT Id FROM dbo.EkHizmetler WHERE TesisId IN (SELECT Id FROM @HedefTesisler);

INSERT INTO @HedefEkHizmetTarifeleri (Id)
SELECT Id FROM dbo.EkHizmetTarifeleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler);

INSERT INTO @HedefIndirimKurallari (Id)
SELECT Id FROM dbo.IndirimKurallari WHERE TesisId IN (SELECT Id FROM @HedefTesisler);

INSERT INTO @HedefTesisOdaTipleri (Id)
SELECT Id FROM dbo.TesisOdaTipleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler);

-- ----------------------------------------------------------------------------
-- 2) DRY-RUN raporu: saklanacak kurumlar + tablo bazinda silinecek aday adetleri.
-- ----------------------------------------------------------------------------
PRINT N'=== DRY-RUN: saklanacak kurumlar (SILINMEYECEK) ===';
SELECT Id, Kod, Ad FROM @SaklanacakKurum ORDER BY Id;

PRINT N'=== DRY-RUN: silinecek aday kayit adetleri (ExecuteDelete=' + CAST(@ExecuteDelete AS nvarchar(1)) + N') ===';
SELECT N'Kurumlar' AS Tablo, (SELECT COUNT(*) FROM @HedefKurumlar) AS Adet
UNION ALL SELECT N'Tesisler', (SELECT COUNT(*) FROM @HedefTesisler)
UNION ALL SELECT N'Binalar', (SELECT COUNT(*) FROM @HedefBinalar)
UNION ALL SELECT N'BinaYoneticileri', (SELECT COUNT(*) FROM dbo.BinaYoneticileri WHERE BinaId IN (SELECT Id FROM @HedefBinalar))
UNION ALL SELECT N'IsletmeAlanlari', (SELECT COUNT(*) FROM @HedefIsletmeAlanlari)
UNION ALL SELECT N'Odalar', (SELECT COUNT(*) FROM @HedefOdalar)
UNION ALL SELECT N'OdaOzellikDegerleri', (SELECT COUNT(*) FROM dbo.OdaOzellikDegerleri WHERE OdaId IN (SELECT Id FROM @HedefOdalar))
UNION ALL SELECT N'OdaFiyatlari', (SELECT COUNT(*) FROM dbo.OdaFiyatlari WHERE TesisOdaTipiId IN (SELECT Id FROM @HedefTesisOdaTipleri))
UNION ALL SELECT N'OdaKullanimBloklari', (SELECT COUNT(*) FROM dbo.OdaKullanimBloklari WHERE TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT N'TesisOdaTipleri', (SELECT COUNT(*) FROM @HedefTesisOdaTipleri)
UNION ALL SELECT N'TesisOdaTipiOzellikDegerleri', (SELECT COUNT(*) FROM dbo.TesisOdaTipiOzellikDegerleri WHERE TesisOdaTipiId IN (SELECT Id FROM @HedefTesisOdaTipleri))
UNION ALL SELECT N'TesisKonaklamaTipleri', (SELECT COUNT(*) FROM dbo.TesisKonaklamaTipleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT N'TesisKonaklamaTipiIcerikOverridelari', (SELECT COUNT(*) FROM dbo.TesisKonaklamaTipiIcerikOverridelari WHERE TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT N'TesisMisafirTipleri', (SELECT COUNT(*) FROM dbo.TesisMisafirTipleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT N'TesisYoneticileri', (SELECT COUNT(*) FROM dbo.TesisYoneticileri WHERE TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT N'TesisResepsiyonistleri', (SELECT COUNT(*) FROM dbo.TesisResepsiyonistleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT N'TesisMuhasebecileri', (SELECT COUNT(*) FROM dbo.TesisMuhasebecileri WHERE TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT N'KullaniciTesisSahiplikleri', (SELECT COUNT(*) FROM dbo.KullaniciTesisSahiplikleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT N'EkHizmetler', (SELECT COUNT(*) FROM @HedefEkHizmetler)
UNION ALL SELECT N'EkHizmetTarifeleri', (SELECT COUNT(*) FROM @HedefEkHizmetTarifeleri)
UNION ALL SELECT N'IndirimKurallari', (SELECT COUNT(*) FROM @HedefIndirimKurallari)
UNION ALL SELECT N'IndirimKuraliKonaklamaTipleri', (SELECT COUNT(*) FROM dbo.IndirimKuraliKonaklamaTipleri WHERE IndirimKuraliId IN (SELECT Id FROM @HedefIndirimKurallari))
UNION ALL SELECT N'IndirimKuraliMisafirTipleri', (SELECT COUNT(*) FROM dbo.IndirimKuraliMisafirTipleri WHERE IndirimKuraliId IN (SELECT Id FROM @HedefIndirimKurallari))
UNION ALL SELECT N'SezonKurallari', (SELECT COUNT(*) FROM dbo.SezonKurallari WHERE TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT N'Rezervasyonlar', (SELECT COUNT(*) FROM @HedefRezervasyonlar)
UNION ALL SELECT N'RezervasyonOdemeler', (SELECT COUNT(*) FROM dbo.RezervasyonOdemeler WHERE RezervasyonId IN (SELECT Id FROM @HedefRezervasyonlar))
UNION ALL SELECT N'RezervasyonSegmentleri', (SELECT COUNT(*) FROM dbo.RezervasyonSegmentleri WHERE RezervasyonId IN (SELECT Id FROM @HedefRezervasyonlar))
UNION ALL SELECT N'RezervasyonKonaklayanlar', (SELECT COUNT(*) FROM dbo.RezervasyonKonaklayanlar WHERE RezervasyonId IN (SELECT Id FROM @HedefRezervasyonlar))
UNION ALL SELECT N'RezervasyonEkHizmetler', (SELECT COUNT(*) FROM dbo.RezervasyonEkHizmetler WHERE RezervasyonId IN (SELECT Id FROM @HedefRezervasyonlar))
UNION ALL SELECT N'RezervasyonDegisiklikGecmisleri', (SELECT COUNT(*) FROM dbo.RezervasyonDegisiklikGecmisleri WHERE RezervasyonId IN (SELECT Id FROM @HedefRezervasyonlar))
UNION ALL SELECT N'KampProgramlari', (SELECT COUNT(*) FROM @HedefKampProgramlari)
UNION ALL SELECT N'KampDonemleri', (SELECT COUNT(*) FROM @HedefKampDonemleri)
UNION ALL SELECT N'KampDonemiTesisleri', (SELECT COUNT(*) FROM dbo.KampDonemiTesisleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler) OR KampDonemiId IN (SELECT Id FROM @HedefKampDonemleri))
UNION ALL SELECT N'KampBasvurulari', (SELECT COUNT(*) FROM @HedefKampBasvurulari)
UNION ALL SELECT N'KampBasvuruTercihleri', (SELECT COUNT(*) FROM dbo.KampBasvuruTercihleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler) OR KampDonemiId IN (SELECT Id FROM @HedefKampDonemleri))
UNION ALL SELECT N'KampBasvuruKatilimcilari', (SELECT COUNT(*) FROM dbo.KampBasvuruKatilimcilari WHERE KampBasvuruId IN (SELECT Id FROM @HedefKampBasvurulari))
UNION ALL SELECT N'KampBasvuruGecmisKatilimlari', (SELECT COUNT(*) FROM dbo.KampBasvuruGecmisKatilimlari WHERE KaynakBasvuruId IN (SELECT Id FROM @HedefKampBasvurulari))
UNION ALL SELECT N'KampRezervasyonlari', (SELECT COUNT(*) FROM dbo.KampRezervasyonlari WHERE TesisId IN (SELECT Id FROM @HedefTesisler) OR KampDonemiId IN (SELECT Id FROM @HedefKampDonemleri))
UNION ALL SELECT N'Restoranlar', (SELECT COUNT(*) FROM @HedefRestoranlar)
UNION ALL SELECT N'RestoranMasalari', (SELECT COUNT(*) FROM restoran.RestoranMasalari WHERE RestoranId IN (SELECT Id FROM @HedefRestoranlar))
UNION ALL SELECT N'RestoranMenuKategorileri', (SELECT COUNT(*) FROM restoran.RestoranMenuKategorileri WHERE RestoranId IN (SELECT Id FROM @HedefRestoranlar))
UNION ALL SELECT N'RestoranMenuUrunleri', (SELECT COUNT(*) FROM restoran.RestoranMenuUrunleri WHERE RestoranMenuKategoriId IN (SELECT Id FROM restoran.RestoranMenuKategorileri WHERE RestoranId IN (SELECT Id FROM @HedefRestoranlar)))
UNION ALL SELECT N'RestoranSiparisleri', (SELECT COUNT(*) FROM restoran.RestoranSiparisleri WHERE RestoranId IN (SELECT Id FROM @HedefRestoranlar))
UNION ALL SELECT N'RestoranSiparisKalemleri', (SELECT COUNT(*) FROM restoran.RestoranSiparisKalemleri WHERE RestoranSiparisId IN (SELECT Id FROM restoran.RestoranSiparisleri WHERE RestoranId IN (SELECT Id FROM @HedefRestoranlar)))
UNION ALL SELECT N'RestoranOdemeleri', (SELECT COUNT(*) FROM restoran.RestoranOdemeleri WHERE RestoranSiparisId IN (SELECT Id FROM restoran.RestoranSiparisleri WHERE RestoranId IN (SELECT Id FROM @HedefRestoranlar)))
UNION ALL SELECT N'RestoranGarsonlari', (SELECT COUNT(*) FROM restoran.RestoranGarsonlari WHERE RestoranId IN (SELECT Id FROM @HedefRestoranlar))
UNION ALL SELECT N'RestoranYoneticileri', (SELECT COUNT(*) FROM restoran.RestoranYoneticileri WHERE RestoranId IN (SELECT Id FROM @HedefRestoranlar))
UNION ALL SELECT N'CariKartlar', (SELECT COUNT(*) FROM @HedefCariKartlar)
UNION ALL SELECT N'CariHareketler', (SELECT COUNT(*) FROM muhasebe.CariHareketler WHERE CariKartId IN (SELECT Id FROM @HedefCariKartlar))
UNION ALL SELECT N'CariKartBankaHesaplari', (SELECT COUNT(*) FROM muhasebe.CariKartBankaHesaplari WHERE CariKartId IN (SELECT Id FROM @HedefCariKartlar))
UNION ALL SELECT N'CariKartYetkiliKisileri', (SELECT COUNT(*) FROM muhasebe.CariKartYetkiliKisileri WHERE CariKartId IN (SELECT Id FROM @HedefCariKartlar))
UNION ALL SELECT N'BankaHareketleri', (SELECT COUNT(*) FROM muhasebe.BankaHareketleri WHERE CariKartId IN (SELECT Id FROM @HedefCariKartlar) OR KasaBankaHesapId IN (SELECT Id FROM @HedefKasaBanka))
UNION ALL SELECT N'KasaHareketleri', (SELECT COUNT(*) FROM muhasebe.KasaHareketleri WHERE CariKartId IN (SELECT Id FROM @HedefCariKartlar) OR KasaBankaHesapId IN (SELECT Id FROM @HedefKasaBanka))
UNION ALL SELECT N'Depolar', (SELECT COUNT(*) FROM @HedefDepolar)
UNION ALL SELECT N'DepoCikisGruplari', (SELECT COUNT(*) FROM muhasebe.DepoCikisGruplari WHERE DepoId IN (SELECT Id FROM @HedefDepolar))
UNION ALL SELECT N'Hesaplar', (SELECT COUNT(*) FROM @HedefHesaplar)
UNION ALL SELECT N'HesapDepoBaglantilari', (SELECT COUNT(*) FROM muhasebe.HesapDepoBaglantilari WHERE HesapId IN (SELECT Id FROM @HedefHesaplar))
UNION ALL SELECT N'HesapKasaBankaBaglantilari', (SELECT COUNT(*) FROM muhasebe.HesapKasaBankaBaglantilari WHERE HesapId IN (SELECT Id FROM @HedefHesaplar))
UNION ALL SELECT N'KasaBankaHesaplari', (SELECT COUNT(*) FROM @HedefKasaBanka)
UNION ALL SELECT N'MuhasebeHesapPlanlari', (SELECT COUNT(*) FROM @HedefHesapPlanlari)
UNION ALL SELECT N'MuhasebeHesapBakiyeleri', (SELECT COUNT(*) FROM muhasebe.MuhasebeHesapBakiyeleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT N'MuhasebeHesapKoduSayaclari', (SELECT COUNT(*) FROM muhasebe.MuhasebeHesapKoduSayaclari WHERE TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT N'MuhasebeVergiHesapEslemeleri', (SELECT COUNT(*) FROM muhasebe.MuhasebeVergiHesapEslemeleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT N'MuhasebeYevmiyeNoSayaclari', (SELECT COUNT(*) FROM muhasebe.MuhasebeYevmiyeNoSayaclari WHERE TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT N'MuhasebeDonemler', (SELECT COUNT(*) FROM muhasebe.MuhasebeDonemler WHERE TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT N'TasinirKartlar', (SELECT COUNT(*) FROM @HedefTasinirKartlar)
UNION ALL SELECT N'TasinirKodMuhasebeHesapEslemeleri', (SELECT COUNT(*) FROM muhasebe.TasinirKodMuhasebeHesapEslemeleri WHERE MuhasebeHesapPlaniId IN (SELECT Id FROM @HedefHesapPlanlari))
UNION ALL SELECT N'TevkifatHesapEslemeleri', (SELECT COUNT(*) FROM muhasebe.TevkifatHesapEslemeleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT N'StokHareketleri', (SELECT COUNT(*) FROM muhasebe.StokHareketleri WHERE DepoId IN (SELECT Id FROM @HedefDepolar) OR TasinirKartId IN (SELECT Id FROM @HedefTasinirKartlar) OR CariKartId IN (SELECT Id FROM @HedefCariKartlar))
UNION ALL SELECT N'MuhasebeFisler', (SELECT COUNT(*) FROM @HedefFisler)
UNION ALL SELECT N'MuhasebeFisSatirlari', (SELECT COUNT(*) FROM muhasebe.MuhasebeFisSatirlari WHERE MuhasebeFisId IN (SELECT Id FROM @HedefFisler))
UNION ALL SELECT N'TahsilatOdemeBelgeleri', (SELECT COUNT(*) FROM @HedefTahsilatOdeme)
UNION ALL SELECT N'PosTahsilatValorleri', (SELECT COUNT(*) FROM muhasebe.PosTahsilatValorleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT N'PosTahsilatValorDegisiklikGecmisleri', (SELECT COUNT(*) FROM muhasebe.PosTahsilatValorDegisiklikGecmisleri d INNER JOIN muhasebe.PosTahsilatValorleri v ON v.Id = d.PosTahsilatValorId WHERE v.TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT N'PosValorFisNoSayaclari', (SELECT COUNT(*) FROM muhasebe.PosValorFisNoSayaclari WHERE TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT N'SatisBelgeleri', (SELECT COUNT(*) FROM @HedefSatisBelgeleri)
UNION ALL SELECT N'SatisBelgesiSatirlari', (SELECT COUNT(*) FROM muhasebe.SatisBelgesiSatirlari WHERE SatisBelgesiId IN (SELECT Id FROM @HedefSatisBelgeleri))
UNION ALL SELECT N'KurumEBelgePolitikalari', (SELECT COUNT(*) FROM muhasebe.KurumEBelgePolitikalari WHERE KurumId IN (SELECT Id FROM @HedefKurumlar))
UNION ALL SELECT N'KurumEBelgePolitikaRevizyonlari', (SELECT COUNT(*) FROM muhasebe.KurumEBelgePolitikaRevizyonlari WHERE KurumId IN (SELECT Id FROM @HedefKurumlar))
UNION ALL SELECT N'KurumFaturaNumaraSayaclari', (SELECT COUNT(*) FROM muhasebe.KurumFaturaNumaraSayaclari WHERE KurumId IN (SELECT Id FROM @HedefKurumlar))
UNION ALL SELECT N'EBelgeKayitlari', (SELECT COUNT(*) FROM @HedefEBelgeKayitlari)
UNION ALL SELECT N'EBelgeSnapshots', (SELECT COUNT(*) FROM muhasebe.EBelgeSnapshots WHERE EBelgeKaydiId IN (SELECT Id FROM @HedefEBelgeKayitlari) OR KurumId IN (SELECT Id FROM @HedefKurumlar))
UNION ALL SELECT N'EBelgeOutboxMesajlari', (SELECT COUNT(*) FROM muhasebe.EBelgeOutboxMesajlari WHERE EBelgeKaydiId IN (SELECT Id FROM @HedefEBelgeKayitlari) OR KurumId IN (SELECT Id FROM @HedefKurumlar))
UNION ALL SELECT N'EBelgeArtifactlari', (SELECT COUNT(*) FROM muhasebe.EBelgeArtifactlari WHERE EBelgeKaydiId IN (SELECT Id FROM @HedefEBelgeKayitlari) OR KurumId IN (SELECT Id FROM @HedefKurumlar))
UNION ALL SELECT N'SatisBelgesiEBelgeKararlari', (SELECT COUNT(*) FROM muhasebe.SatisBelgesiEBelgeKararlari WHERE SatisBelgesiId IN (SELECT Id FROM @HedefSatisBelgeleri) OR KurumId IN (SELECT Id FROM @HedefKurumlar))
UNION ALL SELECT N'Agentler', (SELECT COUNT(*) FROM entegrasyon.Agentler WHERE KurumId IN (SELECT Id FROM @HedefKurumlar))
UNION ALL SELECT N'AgentCapabilities', (SELECT COUNT(*) FROM entegrasyon.AgentCapabilities WHERE KurumId IN (SELECT Id FROM @HedefKurumlar))
UNION ALL SELECT N'AgentCommands', (SELECT COUNT(*) FROM entegrasyon.AgentCommands WHERE KurumId IN (SELECT Id FROM @HedefKurumlar))
UNION ALL SELECT N'AgentCommandExecutions', (SELECT COUNT(*) FROM entegrasyon.AgentCommandExecutions WHERE KurumId IN (SELECT Id FROM @HedefKurumlar))
UNION ALL SELECT N'AgentCredentialler', (SELECT COUNT(*) FROM entegrasyon.AgentCredentialler WHERE KurumId IN (SELECT Id FROM @HedefKurumlar))
UNION ALL SELECT N'AgentEnrollments', (SELECT COUNT(*) FROM entegrasyon.AgentEnrollments WHERE KurumId IN (SELECT Id FROM @HedefKurumlar))
UNION ALL SELECT N'AgentInstallationSessions', (SELECT COUNT(*) FROM entegrasyon.AgentInstallationSessions WHERE KurumId IN (SELECT Id FROM @HedefKurumlar))
UNION ALL SELECT N'AgentScopes', (SELECT COUNT(*) FROM entegrasyon.AgentScopes WHERE KurumId IN (SELECT Id FROM @HedefKurumlar))
UNION ALL SELECT N'AgentTesisler', (SELECT COUNT(*) FROM entegrasyon.AgentTesisler WHERE KurumId IN (SELECT Id FROM @HedefKurumlar))
UNION ALL SELECT N'AgentReleases', (SELECT COUNT(*) FROM entegrasyon.AgentReleases WHERE KurumId IN (SELECT Id FROM @HedefKurumlar))
UNION ALL SELECT N'PosCihazlari', (SELECT COUNT(*) FROM entegrasyon.PosCihazlari WHERE KurumId IN (SELECT Id FROM @HedefKurumlar) OR TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT N'PosTerminaller', (SELECT COUNT(*) FROM entegrasyon.PosTerminaller WHERE KurumId IN (SELECT Id FROM @HedefKurumlar) OR TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT N'PosOdemeIslemleri', (SELECT COUNT(*) FROM entegrasyon.PosOdemeIslemleri WHERE KurumId IN (SELECT Id FROM @HedefKurumlar) OR TesisId IN (SELECT Id FROM @HedefTesisler))
UNION ALL SELECT N'UserKurums', (SELECT COUNT(*) FROM [identity].UserKurums WHERE KurumId IN (SELECT Id FROM @HedefKurumlar));

IF @ExecuteDelete = 0
BEGIN
    PRINT N'@ExecuteDelete = 0 (varsayilan/tanimsiz) - HICBIR SATIR SILINMEDI.';
    PRINT N'Silmek icin: sqlcmd ... -v ExecuteDelete=1 -i delete-institutions-except-trt-default.sql';
    RETURN;
END;

-- ----------------------------------------------------------------------------
-- 3) SILME: FK sirasiyla, TEK transaction, TRY/CATCH. Yalnizca @ExecuteDelete=1
--    iken buraya ulasilir.
-- ----------------------------------------------------------------------------
DECLARE @Silinen TABLE (Tablo nvarchar(64) PRIMARY KEY, Adet int NOT NULL);

BEGIN TRY
    BEGIN TRANSACTION;

    -- 3.0) DONGUSEL FK referanslarini NULL'a cek (hedef kayitlarda).
    --      RezervasyonOdemeler <-> PosOdemeIslemleri
    UPDATE p SET p.RezervasyonOdemeId = NULL
    FROM entegrasyon.PosOdemeIslemleri p
    WHERE p.KurumId IN (SELECT Id FROM @HedefKurumlar) OR p.TesisId IN (SELECT Id FROM @HedefTesisler);

    UPDATE r SET r.PosOdemeIslemiId = NULL
    FROM dbo.RezervasyonOdemeler r
    WHERE r.RezervasyonId IN (SELECT Id FROM @HedefRezervasyonlar);

    --      Tesisler.RezervasyonMisafirVarsayilanCariKartId <-> CariKartlar.TesisId
    UPDATE t SET t.RezervasyonMisafirVarsayilanCariKartId = NULL
    FROM dbo.Tesisler t
    WHERE t.Id IN (SELECT Id FROM @HedefTesisler);

    --      Self-FK'ler
    UPDATE s SET s.IadeEdilenBelgeId = NULL
    FROM muhasebe.SatisBelgeleri s
    WHERE s.Id IN (SELECT Id FROM @HedefSatisBelgeleri);

    UPDATE f SET f.IptalEdilenFisId = NULL, f.TersKayitFisId = NULL
    FROM muhasebe.MuhasebeFisler f
    WHERE f.Id IN (SELECT Id FROM @HedefFisler);

    UPDATE c SET c.IliskiliCariHareketId = NULL
    FROM muhasebe.CariHareketler c
    WHERE c.CariKartId IN (SELECT Id FROM @HedefCariKartlar);

    UPDATE d SET d.UstDepoId = NULL
    FROM muhasebe.Depolar d
    WHERE d.Id IN (SELECT Id FROM @HedefDepolar);

    UPDATE k SET k.BagliBankaHesapId = NULL
    FROM muhasebe.KasaBankaHesaplari k
    WHERE k.Id IN (SELECT Id FROM @HedefKasaBanka);

    UPDATE h SET h.UstHesapId = NULL
    FROM muhasebe.MuhasebeHesapPlanlari h
    WHERE h.Id IN (SELECT Id FROM @HedefHesapPlanlari);

    UPDATE a SET a.KaynakArtifactId = NULL
    FROM muhasebe.EBelgeArtifactlari a
    WHERE a.KurumId IN (SELECT Id FROM @HedefKurumlar);

    -- 3.1) restoran alanindaki cocuk kayitlar (Restoranlar'dan once).
    DELETE FROM restoran.RestoranSiparisKalemleri WHERE RestoranSiparisId IN (SELECT Id FROM restoran.RestoranSiparisleri WHERE RestoranId IN (SELECT Id FROM @HedefRestoranlar));
    INSERT INTO @Silinen VALUES (N'RestoranSiparisKalemleri', @@ROWCOUNT);

    DELETE FROM restoran.RestoranOdemeleri WHERE RestoranSiparisId IN (SELECT Id FROM restoran.RestoranSiparisleri WHERE RestoranId IN (SELECT Id FROM @HedefRestoranlar));
    INSERT INTO @Silinen VALUES (N'RestoranOdemeleri', @@ROWCOUNT);

    DELETE FROM restoran.RestoranSiparisleri WHERE RestoranId IN (SELECT Id FROM @HedefRestoranlar);
    INSERT INTO @Silinen VALUES (N'RestoranSiparisleri', @@ROWCOUNT);

    DELETE FROM restoran.RestoranMenuUrunleri WHERE RestoranMenuKategoriId IN (SELECT Id FROM restoran.RestoranMenuKategorileri WHERE RestoranId IN (SELECT Id FROM @HedefRestoranlar));
    INSERT INTO @Silinen VALUES (N'RestoranMenuUrunleri', @@ROWCOUNT);

    DELETE FROM restoran.RestoranMenuKategorileri WHERE RestoranId IN (SELECT Id FROM @HedefRestoranlar);
    INSERT INTO @Silinen VALUES (N'RestoranMenuKategorileri', @@ROWCOUNT);

    DELETE FROM restoran.RestoranMasalari WHERE RestoranId IN (SELECT Id FROM @HedefRestoranlar);
    INSERT INTO @Silinen VALUES (N'RestoranMasalari', @@ROWCOUNT);

    DELETE FROM restoran.RestoranGarsonlari WHERE RestoranId IN (SELECT Id FROM @HedefRestoranlar);
    INSERT INTO @Silinen VALUES (N'RestoranGarsonlari', @@ROWCOUNT);

    DELETE FROM restoran.RestoranYoneticileri WHERE RestoranId IN (SELECT Id FROM @HedefRestoranlar);
    INSERT INTO @Silinen VALUES (N'RestoranYoneticileri', @@ROWCOUNT);

    DELETE FROM restoran.Restoranlar WHERE Id IN (SELECT Id FROM @HedefRestoranlar);
    INSERT INTO @Silinen VALUES (N'Restoranlar', @@ROWCOUNT);

    -- 3.2) entegrasyon/POS: PosOdemeIslemleri en erken silinir (Rezervasyonlar,
    --      CariKartlar, KasaBankaHesaplari, PosTerminaller, AgentCommands'e referans verir).
    DELETE FROM entegrasyon.PosOdemeIslemleri WHERE KurumId IN (SELECT Id FROM @HedefKurumlar) OR TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES (N'PosOdemeIslemleri', @@ROWCOUNT);

    DELETE FROM entegrasyon.AgentCommandExecutions WHERE KurumId IN (SELECT Id FROM @HedefKurumlar);
    INSERT INTO @Silinen VALUES (N'AgentCommandExecutions', @@ROWCOUNT);

    DELETE FROM entegrasyon.PosTerminaller WHERE KurumId IN (SELECT Id FROM @HedefKurumlar) OR TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES (N'PosTerminaller', @@ROWCOUNT);

    DELETE FROM entegrasyon.PosCihazlari WHERE KurumId IN (SELECT Id FROM @HedefKurumlar) OR TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES (N'PosCihazlari', @@ROWCOUNT);

    DELETE FROM entegrasyon.AgentCapabilities WHERE KurumId IN (SELECT Id FROM @HedefKurumlar);
    INSERT INTO @Silinen VALUES (N'AgentCapabilities', @@ROWCOUNT);

    DELETE FROM entegrasyon.AgentScopes WHERE KurumId IN (SELECT Id FROM @HedefKurumlar);
    INSERT INTO @Silinen VALUES (N'AgentScopes', @@ROWCOUNT);

    DELETE FROM entegrasyon.AgentTesisler WHERE KurumId IN (SELECT Id FROM @HedefKurumlar);
    INSERT INTO @Silinen VALUES (N'AgentTesisler', @@ROWCOUNT);

    DELETE FROM entegrasyon.AgentCredentialler WHERE KurumId IN (SELECT Id FROM @HedefKurumlar);
    INSERT INTO @Silinen VALUES (N'AgentCredentialler', @@ROWCOUNT);

    DELETE FROM entegrasyon.AgentEnrollments WHERE KurumId IN (SELECT Id FROM @HedefKurumlar);
    INSERT INTO @Silinen VALUES (N'AgentEnrollments', @@ROWCOUNT);

    DELETE FROM entegrasyon.AgentInstallationSessions WHERE KurumId IN (SELECT Id FROM @HedefKurumlar);
    INSERT INTO @Silinen VALUES (N'AgentInstallationSessions', @@ROWCOUNT);

    DELETE FROM entegrasyon.AgentCommands WHERE KurumId IN (SELECT Id FROM @HedefKurumlar);
    INSERT INTO @Silinen VALUES (N'AgentCommands', @@ROWCOUNT);

    DELETE FROM entegrasyon.AgentReleases WHERE KurumId IN (SELECT Id FROM @HedefKurumlar);
    INSERT INTO @Silinen VALUES (N'AgentReleases', @@ROWCOUNT);

    DELETE FROM entegrasyon.Agentler WHERE KurumId IN (SELECT Id FROM @HedefKurumlar);
    INSERT INTO @Silinen VALUES (N'Agentler', @@ROWCOUNT);

    -- 3.3) Rezervasyon alanindaki cocuk kayitlar (Rezervasyonlar'dan once).
    DELETE FROM dbo.RezervasyonKonaklamaHakkiTuketimKayitlari WHERE RezervasyonId IN (SELECT Id FROM @HedefRezervasyonlar);
    INSERT INTO @Silinen VALUES (N'RezervasyonKonaklamaHakkiTuketimKayitlari', @@ROWCOUNT);

    DELETE FROM dbo.RezervasyonKonaklamaHaklari WHERE RezervasyonId IN (SELECT Id FROM @HedefRezervasyonlar);
    INSERT INTO @Silinen VALUES (N'RezervasyonKonaklamaHaklari', @@ROWCOUNT);

    DELETE FROM dbo.RezervasyonEkHizmetler WHERE RezervasyonId IN (SELECT Id FROM @HedefRezervasyonlar);
    INSERT INTO @Silinen VALUES (N'RezervasyonEkHizmetler', @@ROWCOUNT);

    DELETE FROM dbo.RezervasyonKonaklayanSegmentAtamalari WHERE RezervasyonKonaklayanId IN (SELECT Id FROM dbo.RezervasyonKonaklayanlar WHERE RezervasyonId IN (SELECT Id FROM @HedefRezervasyonlar));
    INSERT INTO @Silinen VALUES (N'RezervasyonKonaklayanSegmentAtamalari', @@ROWCOUNT);

    DELETE FROM dbo.RezervasyonSegmentOdaAtamalari WHERE RezervasyonSegmentId IN (SELECT Id FROM dbo.RezervasyonSegmentleri WHERE RezervasyonId IN (SELECT Id FROM @HedefRezervasyonlar));
    INSERT INTO @Silinen VALUES (N'RezervasyonSegmentOdaAtamalari', @@ROWCOUNT);

    DELETE FROM dbo.RezervasyonDegisiklikGecmisleri WHERE RezervasyonId IN (SELECT Id FROM @HedefRezervasyonlar);
    INSERT INTO @Silinen VALUES (N'RezervasyonDegisiklikGecmisleri', @@ROWCOUNT);

    DELETE FROM dbo.RezervasyonOdemeler WHERE RezervasyonId IN (SELECT Id FROM @HedefRezervasyonlar);
    INSERT INTO @Silinen VALUES (N'RezervasyonOdemeler', @@ROWCOUNT);

    DELETE FROM dbo.RezervasyonKonaklayanlar WHERE RezervasyonId IN (SELECT Id FROM @HedefRezervasyonlar);
    INSERT INTO @Silinen VALUES (N'RezervasyonKonaklayanlar', @@ROWCOUNT);

    DELETE FROM dbo.RezervasyonSegmentleri WHERE RezervasyonId IN (SELECT Id FROM @HedefRezervasyonlar);
    INSERT INTO @Silinen VALUES (N'RezervasyonSegmentleri', @@ROWCOUNT);

    DELETE FROM dbo.Rezervasyonlar WHERE Id IN (SELECT Id FROM @HedefRezervasyonlar);
    INSERT INTO @Silinen VALUES (N'Rezervasyonlar', @@ROWCOUNT);

    -- 3.4) Oda / tesis yapisi (Binalar'dan once; Tesisler'den once).
    DELETE FROM dbo.OdaKullanimBloklari WHERE TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES (N'OdaKullanimBloklari', @@ROWCOUNT);

    DELETE FROM dbo.OdaFiyatlari WHERE TesisOdaTipiId IN (SELECT Id FROM @HedefTesisOdaTipleri);
    INSERT INTO @Silinen VALUES (N'OdaFiyatlari', @@ROWCOUNT);

    DELETE FROM dbo.TesisOdaTipiOzellikDegerleri WHERE TesisOdaTipiId IN (SELECT Id FROM @HedefTesisOdaTipleri);
    INSERT INTO @Silinen VALUES (N'TesisOdaTipiOzellikDegerleri', @@ROWCOUNT);

    DELETE FROM dbo.OdaOzellikDegerleri WHERE OdaId IN (SELECT Id FROM @HedefOdalar);
    INSERT INTO @Silinen VALUES (N'OdaOzellikDegerleri', @@ROWCOUNT);

    DELETE FROM dbo.Odalar WHERE Id IN (SELECT Id FROM @HedefOdalar);
    INSERT INTO @Silinen VALUES (N'Odalar', @@ROWCOUNT);

    DELETE FROM dbo.TesisOdaTipleri WHERE Id IN (SELECT Id FROM @HedefTesisOdaTipleri);
    INSERT INTO @Silinen VALUES (N'TesisOdaTipleri', @@ROWCOUNT);

    DELETE FROM dbo.TesisKonaklamaTipiIcerikOverridelari WHERE TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES (N'TesisKonaklamaTipiIcerikOverridelari', @@ROWCOUNT);

    DELETE FROM dbo.TesisKonaklamaTipleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES (N'TesisKonaklamaTipleri', @@ROWCOUNT);

    DELETE FROM dbo.TesisMisafirTipleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES (N'TesisMisafirTipleri', @@ROWCOUNT);

    DELETE FROM dbo.EkHizmetTarifeleri WHERE Id IN (SELECT Id FROM @HedefEkHizmetTarifeleri);
    INSERT INTO @Silinen VALUES (N'EkHizmetTarifeleri', @@ROWCOUNT);

    DELETE FROM dbo.EkHizmetler WHERE Id IN (SELECT Id FROM @HedefEkHizmetler);
    INSERT INTO @Silinen VALUES (N'EkHizmetler', @@ROWCOUNT);

    DELETE FROM dbo.IndirimKuraliKonaklamaTipleri WHERE IndirimKuraliId IN (SELECT Id FROM @HedefIndirimKurallari);
    INSERT INTO @Silinen VALUES (N'IndirimKuraliKonaklamaTipleri', @@ROWCOUNT);

    DELETE FROM dbo.IndirimKuraliMisafirTipleri WHERE IndirimKuraliId IN (SELECT Id FROM @HedefIndirimKurallari);
    INSERT INTO @Silinen VALUES (N'IndirimKuraliMisafirTipleri', @@ROWCOUNT);

    DELETE FROM dbo.IndirimKurallari WHERE Id IN (SELECT Id FROM @HedefIndirimKurallari);
    INSERT INTO @Silinen VALUES (N'IndirimKurallari', @@ROWCOUNT);

    DELETE FROM dbo.SezonKurallari WHERE TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES (N'SezonKurallari', @@ROWCOUNT);

    DELETE FROM dbo.IsletmeAlanlari WHERE Id IN (SELECT Id FROM @HedefIsletmeAlanlari);
    INSERT INTO @Silinen VALUES (N'IsletmeAlanlari', @@ROWCOUNT);

    DELETE FROM dbo.BinaYoneticileri WHERE BinaId IN (SELECT Id FROM @HedefBinalar);
    INSERT INTO @Silinen VALUES (N'BinaYoneticileri', @@ROWCOUNT);

    DELETE FROM dbo.Binalar WHERE Id IN (SELECT Id FROM @HedefBinalar);
    INSERT INTO @Silinen VALUES (N'Binalar', @@ROWCOUNT);

    DELETE FROM dbo.TesisYoneticileri WHERE TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES (N'TesisYoneticileri', @@ROWCOUNT);

    DELETE FROM dbo.TesisResepsiyonistleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES (N'TesisResepsiyonistleri', @@ROWCOUNT);

    DELETE FROM dbo.TesisMuhasebecileri WHERE TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES (N'TesisMuhasebecileri', @@ROWCOUNT);

    DELETE FROM dbo.KullaniciTesisSahiplikleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES (N'KullaniciTesisSahiplikleri', @@ROWCOUNT);

    -- 3.5) Kamp alani (KampProgramlari'ndan once).
    DELETE FROM dbo.KampBasvuruGecmisKatilimlari WHERE KaynakBasvuruId IN (SELECT Id FROM @HedefKampBasvurulari);
    INSERT INTO @Silinen VALUES (N'KampBasvuruGecmisKatilimlari', @@ROWCOUNT);

    DELETE FROM dbo.KampBasvuruKatilimcilari WHERE KampBasvuruId IN (SELECT Id FROM @HedefKampBasvurulari);
    INSERT INTO @Silinen VALUES (N'KampBasvuruKatilimcilari', @@ROWCOUNT);

    DELETE FROM dbo.KampBasvuruTercihleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler) OR KampDonemiId IN (SELECT Id FROM @HedefKampDonemleri);
    INSERT INTO @Silinen VALUES (N'KampBasvuruTercihleri', @@ROWCOUNT);

    DELETE FROM dbo.KampRezervasyonlari WHERE TesisId IN (SELECT Id FROM @HedefTesisler) OR KampDonemiId IN (SELECT Id FROM @HedefKampDonemleri);
    INSERT INTO @Silinen VALUES (N'KampRezervasyonlari', @@ROWCOUNT);

    DELETE FROM dbo.KampBasvurulari WHERE Id IN (SELECT Id FROM @HedefKampBasvurulari);
    INSERT INTO @Silinen VALUES (N'KampBasvurulari', @@ROWCOUNT);

    DELETE FROM dbo.KampDonemiTesisleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler) OR KampDonemiId IN (SELECT Id FROM @HedefKampDonemleri);
    INSERT INTO @Silinen VALUES (N'KampDonemiTesisleri', @@ROWCOUNT);

    DELETE FROM dbo.KampDonemleri WHERE Id IN (SELECT Id FROM @HedefKampDonemleri);
    INSERT INTO @Silinen VALUES (N'KampDonemleri', @@ROWCOUNT);

    DELETE FROM dbo.KampKonaklamaTarifeleri WHERE KampProgramiId IN (SELECT Id FROM @HedefKampProgramlari);
    INSERT INTO @Silinen VALUES (N'KampKonaklamaTarifeleri', @@ROWCOUNT);

    DELETE FROM dbo.KampKuralSetleri WHERE KampProgramiId IN (SELECT Id FROM @HedefKampProgramlari);
    INSERT INTO @Silinen VALUES (N'KampKuralSetleri', @@ROWCOUNT);

    DELETE FROM dbo.KampProgramiBasvuruSahibiTipKurallari WHERE KampProgramiId IN (SELECT Id FROM @HedefKampProgramlari);
    INSERT INTO @Silinen VALUES (N'KampProgramiBasvuruSahibiTipKurallari', @@ROWCOUNT);

    DELETE FROM dbo.KampProgramiParametreAyarlari WHERE KampProgramiId IN (SELECT Id FROM @HedefKampProgramlari);
    INSERT INTO @Silinen VALUES (N'KampProgramiParametreAyarlari', @@ROWCOUNT);

    DELETE FROM dbo.KampProgramlari WHERE Id IN (SELECT Id FROM @HedefKampProgramlari);
    INSERT INTO @Silinen VALUES (N'KampProgramlari', @@ROWCOUNT);

    -- 3.6) Muhasebe alani (MuhasebeFisler / MuhasebeHesapPlanlari / CariKartlar'dan once).
    DELETE FROM muhasebe.PosTahsilatValorDegisiklikGecmisleri WHERE PosTahsilatValorId IN (SELECT Id FROM muhasebe.PosTahsilatValorleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler));
    INSERT INTO @Silinen VALUES (N'PosTahsilatValorDegisiklikGecmisleri', @@ROWCOUNT);

    DELETE FROM muhasebe.PosTahsilatValorleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES (N'PosTahsilatValorleri', @@ROWCOUNT);

    DELETE FROM muhasebe.MuhasebeFisSatirlari WHERE MuhasebeFisId IN (SELECT Id FROM @HedefFisler);
    INSERT INTO @Silinen VALUES (N'MuhasebeFisSatirlari', @@ROWCOUNT);

    DELETE FROM muhasebe.TahsilatOdemeBelgeleri WHERE Id IN (SELECT Id FROM @HedefTahsilatOdeme);
    INSERT INTO @Silinen VALUES (N'TahsilatOdemeBelgeleri', @@ROWCOUNT);

    DELETE FROM muhasebe.CariHareketler WHERE CariKartId IN (SELECT Id FROM @HedefCariKartlar);
    INSERT INTO @Silinen VALUES (N'CariHareketler', @@ROWCOUNT);

    DELETE FROM muhasebe.CariKartBankaHesaplari WHERE CariKartId IN (SELECT Id FROM @HedefCariKartlar);
    INSERT INTO @Silinen VALUES (N'CariKartBankaHesaplari', @@ROWCOUNT);

    DELETE FROM muhasebe.CariKartYetkiliKisileri WHERE CariKartId IN (SELECT Id FROM @HedefCariKartlar);
    INSERT INTO @Silinen VALUES (N'CariKartYetkiliKisileri', @@ROWCOUNT);

    DELETE FROM muhasebe.BankaHareketleri WHERE CariKartId IN (SELECT Id FROM @HedefCariKartlar) OR KasaBankaHesapId IN (SELECT Id FROM @HedefKasaBanka);
    INSERT INTO @Silinen VALUES (N'BankaHareketleri', @@ROWCOUNT);

    DELETE FROM muhasebe.KasaHareketleri WHERE CariKartId IN (SELECT Id FROM @HedefCariKartlar) OR KasaBankaHesapId IN (SELECT Id FROM @HedefKasaBanka);
    INSERT INTO @Silinen VALUES (N'KasaHareketleri', @@ROWCOUNT);

    DELETE FROM muhasebe.StokHareketleri WHERE DepoId IN (SELECT Id FROM @HedefDepolar) OR TasinirKartId IN (SELECT Id FROM @HedefTasinirKartlar) OR CariKartId IN (SELECT Id FROM @HedefCariKartlar);
    INSERT INTO @Silinen VALUES (N'StokHareketleri', @@ROWCOUNT);

    DELETE FROM muhasebe.SatisBelgesiSatirlari WHERE SatisBelgesiId IN (SELECT Id FROM @HedefSatisBelgeleri);
    INSERT INTO @Silinen VALUES (N'SatisBelgesiSatirlari', @@ROWCOUNT);

    DELETE FROM muhasebe.EBelgeArtifactlari WHERE EBelgeKaydiId IN (SELECT Id FROM @HedefEBelgeKayitlari) OR KurumId IN (SELECT Id FROM @HedefKurumlar);
    INSERT INTO @Silinen VALUES (N'EBelgeArtifactlari', @@ROWCOUNT);

    DELETE FROM muhasebe.EBelgeSnapshots WHERE EBelgeKaydiId IN (SELECT Id FROM @HedefEBelgeKayitlari) OR KurumId IN (SELECT Id FROM @HedefKurumlar);
    INSERT INTO @Silinen VALUES (N'EBelgeSnapshots', @@ROWCOUNT);

    DELETE FROM muhasebe.EBelgeOutboxMesajlari WHERE EBelgeKaydiId IN (SELECT Id FROM @HedefEBelgeKayitlari) OR KurumId IN (SELECT Id FROM @HedefKurumlar);
    INSERT INTO @Silinen VALUES (N'EBelgeOutboxMesajlari', @@ROWCOUNT);

    DELETE FROM muhasebe.SatisBelgesiEBelgeKararlari WHERE SatisBelgesiId IN (SELECT Id FROM @HedefSatisBelgeleri) OR KurumId IN (SELECT Id FROM @HedefKurumlar);
    INSERT INTO @Silinen VALUES (N'SatisBelgesiEBelgeKararlari', @@ROWCOUNT);

    DELETE FROM muhasebe.EBelgeKayitlari WHERE Id IN (SELECT Id FROM @HedefEBelgeKayitlari);
    INSERT INTO @Silinen VALUES (N'EBelgeKayitlari', @@ROWCOUNT);

    DELETE FROM muhasebe.KurumEBelgePolitikaRevizyonlari WHERE KurumId IN (SELECT Id FROM @HedefKurumlar);
    INSERT INTO @Silinen VALUES (N'KurumEBelgePolitikaRevizyonlari', @@ROWCOUNT);

    DELETE FROM muhasebe.KurumEBelgePolitikalari WHERE KurumId IN (SELECT Id FROM @HedefKurumlar);
    INSERT INTO @Silinen VALUES (N'KurumEBelgePolitikalari', @@ROWCOUNT);

    DELETE FROM muhasebe.KurumFaturaNumaraSayaclari WHERE KurumId IN (SELECT Id FROM @HedefKurumlar);
    INSERT INTO @Silinen VALUES (N'KurumFaturaNumaraSayaclari', @@ROWCOUNT);

    DELETE FROM muhasebe.SatisBelgeleri WHERE Id IN (SELECT Id FROM @HedefSatisBelgeleri);
    INSERT INTO @Silinen VALUES (N'SatisBelgeleri', @@ROWCOUNT);

    DELETE FROM muhasebe.DepoCikisGruplari WHERE DepoId IN (SELECT Id FROM @HedefDepolar);
    INSERT INTO @Silinen VALUES (N'DepoCikisGruplari', @@ROWCOUNT);

    DELETE FROM muhasebe.HesapDepoBaglantilari WHERE HesapId IN (SELECT Id FROM @HedefHesaplar) OR DepoId IN (SELECT Id FROM @HedefDepolar);
    INSERT INTO @Silinen VALUES (N'HesapDepoBaglantilari', @@ROWCOUNT);

    DELETE FROM muhasebe.HesapKasaBankaBaglantilari WHERE HesapId IN (SELECT Id FROM @HedefHesaplar) OR KasaBankaHesapId IN (SELECT Id FROM @HedefKasaBanka);
    INSERT INTO @Silinen VALUES (N'HesapKasaBankaBaglantilari', @@ROWCOUNT);

    DELETE FROM muhasebe.TasinirKodMuhasebeHesapEslemeleri WHERE MuhasebeHesapPlaniId IN (SELECT Id FROM @HedefHesapPlanlari);
    INSERT INTO @Silinen VALUES (N'TasinirKodMuhasebeHesapEslemeleri', @@ROWCOUNT);

    DELETE FROM muhasebe.MuhasebeHesapBakiyeleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES (N'MuhasebeHesapBakiyeleri', @@ROWCOUNT);

    DELETE FROM muhasebe.MuhasebeVergiHesapEslemeleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES (N'MuhasebeVergiHesapEslemeleri', @@ROWCOUNT);

    DELETE FROM muhasebe.TevkifatHesapEslemeleri WHERE TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES (N'TevkifatHesapEslemeleri', @@ROWCOUNT);

    DELETE FROM muhasebe.MuhasebeHesapKoduSayaclari WHERE TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES (N'MuhasebeHesapKoduSayaclari', @@ROWCOUNT);

    DELETE FROM muhasebe.MuhasebeYevmiyeNoSayaclari WHERE TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES (N'MuhasebeYevmiyeNoSayaclari', @@ROWCOUNT);

    DELETE FROM muhasebe.MuhasebeDonemler WHERE TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES (N'MuhasebeDonemler', @@ROWCOUNT);

    DELETE FROM muhasebe.PosValorFisNoSayaclari WHERE TesisId IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES (N'PosValorFisNoSayaclari', @@ROWCOUNT);

    DELETE FROM muhasebe.TasinirKartlar WHERE Id IN (SELECT Id FROM @HedefTasinirKartlar);
    INSERT INTO @Silinen VALUES (N'TasinirKartlar', @@ROWCOUNT);

    DELETE FROM muhasebe.Depolar WHERE Id IN (SELECT Id FROM @HedefDepolar);
    INSERT INTO @Silinen VALUES (N'Depolar', @@ROWCOUNT);

    DELETE FROM muhasebe.Hesaplar WHERE Id IN (SELECT Id FROM @HedefHesaplar);
    INSERT INTO @Silinen VALUES (N'Hesaplar', @@ROWCOUNT);

    DELETE FROM muhasebe.KasaBankaHesaplari WHERE Id IN (SELECT Id FROM @HedefKasaBanka);
    INSERT INTO @Silinen VALUES (N'KasaBankaHesaplari', @@ROWCOUNT);

    DELETE FROM muhasebe.CariKartlar WHERE Id IN (SELECT Id FROM @HedefCariKartlar);
    INSERT INTO @Silinen VALUES (N'CariKartlar', @@ROWCOUNT);

    DELETE FROM muhasebe.MuhasebeFisler WHERE Id IN (SELECT Id FROM @HedefFisler);
    INSERT INTO @Silinen VALUES (N'MuhasebeFisler', @@ROWCOUNT);

    DELETE FROM muhasebe.MuhasebeHesapPlanlari WHERE Id IN (SELECT Id FROM @HedefHesapPlanlari);
    INSERT INTO @Silinen VALUES (N'MuhasebeHesapPlanlari', @@ROWCOUNT);

    -- 3.7) Tesisler (butun cocuklari silinmis olmali).
    DELETE FROM dbo.Tesisler WHERE Id IN (SELECT Id FROM @HedefTesisler);
    INSERT INTO @Silinen VALUES (N'Tesisler', @@ROWCOUNT);

    -- 3.8) Kullanici-kurum baglantilari + Kurumlar (en son).
    DELETE FROM [identity].UserKurums WHERE KurumId IN (SELECT Id FROM @HedefKurumlar);
    INSERT INTO @Silinen VALUES (N'UserKurums', @@ROWCOUNT);

    DELETE FROM dbo.Kurumlar WHERE Id IN (SELECT Id FROM @HedefKurumlar);
    INSERT INTO @Silinen VALUES (N'Kurumlar', @@ROWCOUNT);

    COMMIT TRANSACTION;

    PRINT N'=== SILME TAMAMLANDI - tablo bazinda silinen kayit sayilari ===';
    SELECT Tablo, Adet FROM @Silinen ORDER BY Tablo;

    -- 4) SONRASI dogrulama: hedef kume Id'lerinde artik fiziksel kayit kalmamali.
    PRINT N'=== SILME SONRASI dogrulama: hedef kumelerde kalan kayit adetleri (0 olmali) ===';
    SELECT N'Kurumlar' AS Tablo, (SELECT COUNT(*) FROM dbo.Kurumlar WHERE Id IN (SELECT Id FROM @HedefKurumlar)) AS KalanAdet
    UNION ALL SELECT N'Tesisler', (SELECT COUNT(*) FROM dbo.Tesisler WHERE Id IN (SELECT Id FROM @HedefTesisler))
    UNION ALL SELECT N'Binalar', (SELECT COUNT(*) FROM dbo.Binalar WHERE Id IN (SELECT Id FROM @HedefBinalar))
    UNION ALL SELECT N'Odalar', (SELECT COUNT(*) FROM dbo.Odalar WHERE Id IN (SELECT Id FROM @HedefOdalar))
    UNION ALL SELECT N'Rezervasyonlar', (SELECT COUNT(*) FROM dbo.Rezervasyonlar WHERE Id IN (SELECT Id FROM @HedefRezervasyonlar))
    UNION ALL SELECT N'CariKartlar', (SELECT COUNT(*) FROM muhasebe.CariKartlar WHERE Id IN (SELECT Id FROM @HedefCariKartlar))
    UNION ALL SELECT N'MuhasebeFisler', (SELECT COUNT(*) FROM muhasebe.MuhasebeFisler WHERE Id IN (SELECT Id FROM @HedefFisler))
    UNION ALL SELECT N'MuhasebeHesapPlanlari', (SELECT COUNT(*) FROM muhasebe.MuhasebeHesapPlanlari WHERE Id IN (SELECT Id FROM @HedefHesapPlanlari))
    UNION ALL SELECT N'SatisBelgeleri', (SELECT COUNT(*) FROM muhasebe.SatisBelgeleri WHERE Id IN (SELECT Id FROM @HedefSatisBelgeleri))
    UNION ALL SELECT N'Restoranlar', (SELECT COUNT(*) FROM restoran.Restoranlar WHERE Id IN (SELECT Id FROM @HedefRestoranlar));
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
