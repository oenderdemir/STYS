namespace STYS.Muhasebe.OdemeIzleme.Dtos;

/// <summary>Guven seviyesi - eslesme ASLA yalnizca tutara dayanmaz, birden fazla sinyalin (banka/
/// POS referansi, tahsilat no, fis no, cari, rezervasyon iliskisi, tarih/saat, tutar, PB, banka/
/// IBAN/kasa, aciklama, kullanici, iptal/ters kayit iliskisi) birlikte degerlendirilmesiyle belirlenir.</summary>
public static class OdemeGuvenSeviyeleri
{
    /// <summary>Yalnizca GUCLU ve benzersiz bir referansin (belge no / fis no) BIREBIR (normalize
    /// edilmis tam esitlik) eslesmesiyle uretilir. Kismi metin (Contains), yalnizca tutar veya
    /// yalnizca tarih+tutar eslesmesi ASLA bu seviyeyi uretmez.</summary>
    public const string Kesin = "Kesin";

    /// <summary>Birden fazla kuvvetli alan (tutar + para birimi + dar tarih araligi + yontem/hesap
    /// veya cari) birlikte ortusuyor, ancak benzersiz referans dogrulanmadi.</summary>
    public const string YuksekOlasilik = "YuksekOlasilik";

    /// <summary>Yalnizca zayif sinyaller (ör. yalnizca tarih+tutar) - incelenmesi onerilir, kesin sunulmaz.</summary>
    public const string IncelenmesiGereken = "IncelenmesiGereken";

    public const string EslesmeYok = "EslesmeYok";
}

/// <summary>Yetki kapsami disindaki bir kayda baglanildiginda donen GUVENLI neden kodlari.
/// Hicbiri hedef kaydin ayrintisini (ad, IBAN, fis no, tesis adi vb.) ifsa etmez.</summary>
public static class OdemeErisimKisitiNedenKodlari
{
    public const string YetkiKapsamiDisindaHesapBaglantisi = "YETKI_KAPSAMI_DISINDA_HESAP_BAGLANTISI";
    public const string YetkiKapsamiDisindaFisBaglantisi = "YETKI_KAPSAMI_DISINDA_FIS_BAGLANTISI";
    public const string TesisUyusmazligi = "TESIS_UYUSMAZLIGI";
    public const string KurumUyusmazligi = "KURUM_UYUSMAZLIGI";
}

/// <summary>Bir odemenin bakiyeye dahil olup olmadigini aciklayan neden kodlari.</summary>
public static class BakiyeyeDahilEdilmemeNedenKodlari
{
    public const string OdemeIptalEdilmis = "OdemeIptalEdilmis";
    public const string CariHareketiYok = "CariHareketiYok";
    public const string CariHareketiIptalEdilmis = "CariHareketiIptalEdilmis";
    public const string ZorunluMuhasebeFisiYok = "ZorunluMuhasebeFisiYok";
    public const string MuhasebeFisiIptalEdilmis = "MuhasebeFisiIptalEdilmis";
    public const string PosValorKaydiYok = "PosValorKaydiYok";
    public const string PosValorHenuzAktarilmamis = "PosValorHenuzAktarilmamis";
}

/// <summary>Bakiyeye dahil olma durumunun UST duzey ozeti.</summary>
public static class BakiyeyeDahilEdilmeDurumlari
{
    /// <summary>Tum gerekli kayitlar mevcut - odeme bakiyeyi gercekten etkiliyor.</summary>
    public const string TamamenDahil = "TamamenDahil";

    /// <summary>Cari bakiyeyi etkiliyor ancak muhasebe/POS tarafinda eksik iliski var.</summary>
    public const string KismenDahil = "KismenDahil";

    /// <summary>Bakiyeyi etkilemiyor.</summary>
    public const string DahilDegil = "DahilDegil";
}

/// <summary>Capraz-kaynak arastirmada bir adayin HANGI kaynaktan uretildigi.</summary>
public static class OdemeAdayKaynaklari
{
    public const string TahsilatOdemeBelgesi = "TahsilatOdemeBelgesi";
    public const string CariHareket = "CariHareket";
    public const string PosTahsilatValor = "PosTahsilatValor";
    public const string KasaHareket = "KasaHareket";
    public const string BankaHareket = "BankaHareket";
    public const string MuhasebeFis = "MuhasebeFis";
}

/// <summary>Capraz-kaynak arastirmada tespit edilen KOPUKLUK turleri.</summary>
public static class OdemeKopuklukTipleri
{
    public const string OdemeBaglantisiOlmayanMuhasebeFisi = "OdemeBaglantisiOlmayanMuhasebeFisi";
    public const string OdemeBelgesiOlmayanCariHareket = "OdemeBelgesiOlmayanCariHareket";
    public const string MuhasebeFisiOlmayanOdemeBelgesi = "MuhasebeFisiOlmayanOdemeBelgesi";
    public const string CariHareketEtkisiOlmayanOdemeBelgesi = "CariHareketEtkisiOlmayanOdemeBelgesi";
    public const string ValorKaydiOlmayanPosTahsilati = "ValorKaydiOlmayanPosTahsilati";
    public const string HedefBankaHesabiOlmayanValor = "HedefBankaHesabiOlmayanValor";
    public const string OdemeBelgesiOlmayanKasaHareketi = "OdemeBelgesiOlmayanKasaHareketi";
    public const string OdemeBelgesiOlmayanBankaHareketi = "OdemeBelgesiOlmayanBankaHareketi";
    public const string SoftDeleteIliskiNedeniyleGorunmeyen = "SoftDeleteIliskiNedeniyleGorunmeyen";
}

/// <summary>Capraz-kaynak arastirma sonucundaki TEK bir aday. Ayni mali islem birden fazla kaynakta
/// bulunabildiginden adaylar tekillestirilir (bkz. TekillestirmeAnahtari).</summary>
public class OdemeAdayiDto
{
    /// <summary>Adayin uretildigi birincil kaynak (bkz. OdemeAdayKaynaklari).</summary>
    public string Kaynak { get; set; } = string.Empty;

    /// <summary>Kaynak kaydin kendi id'si.</summary>
    public int KaynakId { get; set; }

    /// <summary>Ayni mali islemi temsil eden kayitlarin BIRLESTIRILDIGI anahtar - tekillestirme
    /// bunun uzerinden yapilir, boylece ayni odeme belge/cari hareket/valor/fis kayitlarinda
    /// bulundugu icin birden fazla kez SAYILMAZ.</summary>
    public string TekillestirmeAnahtari { get; set; } = string.Empty;

    /// <summary>Bu mali islemin bulundugu TUM kaynaklar (tekillestirme sonrasi birlesik liste).</summary>
    public List<string> BulunduguKaynaklar { get; set; } = [];

    public int? TahsilatOdemeBelgesiId { get; set; }
    public int? CariHareketId { get; set; }
    public int? PosTahsilatValorId { get; set; }
    public int? MuhasebeFisId { get; set; }
    public int? KasaBankaHesapId { get; set; }

    public string? BelgeNo { get; set; }
    public DateTime? Tarih { get; set; }
    public decimal? Tutar { get; set; }
    public string? ParaBirimi { get; set; }
    public int? CariKartId { get; set; }
    public string? CariUnvan { get; set; }
    public int? TesisId { get; set; }
    public string? Aciklama { get; set; }

    /// <summary>Bu adayda tespit edilen kopukluk kodlari (bkz. OdemeKopuklukTipleri).</summary>
    public List<string> KopuklukKodlari { get; set; } = [];
    public List<string> KopuklukAciklamalari { get; set; } = [];

    /// <summary>true ise bu kayit HICBIR odeme belgesine bagli degildir - aranan odemeyle iliskisi
    /// KANITLANMAMISTIR, yalnizca filtre kriterleriyle daraltilmis bir adaydir.</summary>
    public bool BagimsizKayitMi { get; set; }

    public string GuvenSeviyesi { get; set; } = string.Empty;
    public string GuvenGerekcesi { get; set; } = string.Empty;
}

/// <summary>Capraz-kaynak arastirma filtresi. Bagimsiz kaynak taramasi genis olabildiginden
/// backend EN AZ BIR daraltici alan ister (cari, belge no, fis no, kasa/banka hesabi, tarih
/// araligi veya tutar araligi) - aksi halde 400 doner.</summary>
public class OdemeCaprazAramaFilterDto
{
    public int? TesisId { get; set; }
    public DateOnly? TarihBaslangic { get; set; }
    public DateOnly? TarihBitis { get; set; }
    public decimal? TutarMin { get; set; }
    public decimal? TutarMax { get; set; }
    public int? CariKartId { get; set; }
    public string? ParaBirimi { get; set; }
    public string? BelgeNo { get; set; }
    public string? MuhasebeFisNo { get; set; }
    public int? KasaBankaHesapId { get; set; }
    public int? MaliYil { get; set; }
    public int? Donem { get; set; }
    public bool? SadeceIptalEdilmisOlanlar { get; set; }

    /// <summary>Yalnizca belirtilen kopukluk turunu tasiyan adaylari dondurur.</summary>
    public string? KopuklukTipi { get; set; }

    /// <summary>true ise yalnizca en az bir kopukluk tespit edilen adaylar dondurulur.</summary>
    public bool SadeceKopukOlanlar { get; set; } = true;
}

public static class OdemeUyariTipleri
{
    public const string OdemeVarFisYok = "OdemeVarFisYok";
    public const string PosVarValorYok = "PosVarValorYok";
    public const string KapatmaHedefiVarAmaKapanmamis = "KapatmaHedefiVarAmaKapanmamis";
    public const string IptalAmaKapamaGeriAlinmamis = "IptalAmaKapamaGeriAlinmamis";
    public const string AyniTutarAyniTarihFarkliCari = "AyniTutarAyniTarihFarkliCari";
    public const string MukerrerBelgeNo = "MukerrerBelgeNo";
    public const string ParaBirimiTutarsizligi = "ParaBirimiTutarsizligi";
    public const string FarkliMuhasebeDonemineDusme = "FarkliMuhasebeDonemineDusme";
}

/// <summary>Ekranin arama filtresi - tesis secimi mevcut global MuhasebeTesisContextService/
/// IMuhasebeTesisScopeService mekanizmasindan gelir, ikinci bir tesis mekanizmasi KURULMAZ.</summary>
public class OdemeAramaFilterDto
{
    public int? TesisId { get; set; }
    public string? BelgeNo { get; set; }
    public int? CariKartId { get; set; }
    /// <summary>Cari unvan/kod icinde kismi arama (LIKE %deger%).</summary>
    public string? CariAramaMetni { get; set; }
    public DateOnly? TarihBaslangic { get; set; }
    public DateOnly? TarihBitis { get; set; }
    public decimal? TutarMin { get; set; }
    public decimal? TutarMax { get; set; }
    public string? ParaBirimi { get; set; }
    public string? OdemeYontemi { get; set; }
    public string? BelgeTipi { get; set; }
    public int? KasaBankaHesapId { get; set; }
    /// <summary>TahsilatOdemeBelgeDurumlari.Aktif/Iptal.</summary>
    public string? Durum { get; set; }
    /// <summary>Yalnizca KrediKarti odeme yonteminde anlamlidir - bagli PosTahsilatValor.Durum'una gore filtreler.</summary>
    public string? ValorDurumu { get; set; }
    /// <summary>true ise yalnizca MuhasebeFisId'si BOS olan (fis uretilmemis) kayitlar listelenir.</summary>
    public bool? SadeceFissizOlanlar { get; set; }

    /// <summary>Bagli muhasebe fisinin numarasi (MuhasebeFis.FisNo) ile arama.</summary>
    public string? MuhasebeFisNo { get; set; }

    /// <summary>Rezervasyon referans numarasi (Rezervasyon.ReferansNo) ile arama - odeme,
    /// RezervasyonOdeme.TahsilatOdemeBelgesiId uzerinden rezervasyona baglanir.</summary>
    public string? RezervasyonReferansNo { get; set; }

    /// <summary>Kayit OLUSTURULMA tarihi araligi (BelgeTarihi'nden farklidir).</summary>
    public DateOnly? OlusturulmaBaslangic { get; set; }
    public DateOnly? OlusturulmaBitis { get; set; }

    /// <summary>Islemi yapan (kaydi olusturan) kullanici - BaseEntity.CreatedBy ile eslesir.</summary>
    public string? OlusturanKullanici { get; set; }

    /// <summary>Muhasebe donemi - bagli fisin MaliYil/Donem degerleriyle eslesir.</summary>
    public int? MaliYil { get; set; }
    public int? Donem { get; set; }

    /// <summary>Bagli banka hesabinin IBAN'i (kismi arama).</summary>
    public string? Iban { get; set; }

    /// <summary>true: yalnizca iptal edilmis; false: yalnizca aktif; null: hepsi. (Durum ile ayni
    /// isi yapar, kullanim kolayligi icin ayri tutulur.)</summary>
    public bool? SadeceIptalEdilmisOlanlar { get; set; }
}

public class OdemeAramaSatiriDto
{
    public int Id { get; set; }
    public string BelgeNo { get; set; } = string.Empty;
    public DateTime BelgeTarihi { get; set; }
    public string BelgeTipi { get; set; } = string.Empty;
    public string Durum { get; set; } = string.Empty;
    public decimal Tutar { get; set; }
    public string ParaBirimi { get; set; } = string.Empty;
    public string OdemeYontemi { get; set; } = string.Empty;
    public int CariKartId { get; set; }
    public string CariKodu { get; set; } = string.Empty;
    public string CariUnvan { get; set; } = string.Empty;
    public string? KasaBankaHesapAdi { get; set; }
    public int? MuhasebeFisId { get; set; }
    /// <summary>Bu satirda tespit edilen en yuksek onemli uyari sayisi - detayda tam liste gorulur.</summary>
    public int UyariSayisi { get; set; }
}

public class OdemeUyariDto
{
    public string UyariTipi { get; set; } = string.Empty;
    public string GuvenSeviyesi { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
    /// <summary>Bu uyariyla iliskili baska bir OdemeOdemeBelgesi/CariHareket id'si varsa (ör. mukerrer
    /// kayit, ayni tutar/tarih baska cari) - yalnizca KULLANICININ YETKILI OLDUGU tesis kapsaminda doldurulur.</summary>
    public int? IliskiliBelgeId { get; set; }
}

/// <summary>Tek bir odemenin tam detayi - genis alan listesi, ilgili kayitlara yonlendirme icin ID'ler.</summary>
public class OdemeDetayDto
{
    public int Id { get; set; }
    public string BelgeNo { get; set; } = string.Empty;
    public DateTime BelgeTarihi { get; set; }
    public string BelgeTipi { get; set; } = string.Empty;
    public string Durum { get; set; } = string.Empty;
    public decimal Tutar { get; set; }
    public string ParaBirimi { get; set; } = string.Empty;
    public string OdemeYontemi { get; set; } = string.Empty;
    public string? Aciklama { get; set; }

    public int CariKartId { get; set; }
    public string CariKodu { get; set; } = string.Empty;
    public string CariUnvan { get; set; } = string.Empty;

    public int? TesisId { get; set; }
    public string? TesisAdi { get; set; }

    public int? KasaBankaHesapId { get; set; }
    public string? KasaBankaHesapAdi { get; set; }
    public string? KasaBankaHesapTipi { get; set; }
    public string? BankaAdi { get; set; }
    /// <summary>Yalnizca maskeli gosterilir (ör. TR33 **** **** **** **26) - tam IBAN bu DTO'da HICBIR ZAMAN yer almaz.</summary>
    public string? IbanMaskeli { get; set; }
    public string? MuhasebeHesapKodu { get; set; }

    public int? MuhasebeFisId { get; set; }
    public string? MuhasebeFisNo { get; set; }
    public DateTime? MuhasebeFisTarihi { get; set; }
    public string? MuhasebeFisDurumu { get; set; }

    public int? PosTahsilatValorId { get; set; }
    public string? PosValorDurumu { get; set; }
    public DateOnly? PosBeklenenValorTarihi { get; set; }
    public decimal? PosNetTutar { get; set; }

    public int? KapatilacakCariHareketId { get; set; }
    public bool KapatildiMi { get; set; }

    public int? RezervasyonId { get; set; }
    public string? RezervasyonReferansNo { get; set; }

    public string? OlusturanKullanici { get; set; }
    public DateTime? OlusturmaTarihi { get; set; }
    public string? DegistirenKullanici { get; set; }
    public DateTime? DegisiklikTarihi { get; set; }

    /// <summary>Odemenin bakiyeyi GERCEKTEN etkileyip etkilemedigi - yalnizca belgenin Durum'una
    /// degil, cari hareket/muhasebe fisi/POS valor iliskilerinin gercek varligina bakilarak
    /// hesaplanir (bkz. BakiyeyeDahilEdilmeDurumu ve neden kodlari).</summary>
    public bool BakiyeyeDahilMi { get; set; }

    /// <summary>TamamenDahil | KismenDahil | DahilDegil (bkz. BakiyeyeDahilEdilmeDurumlari).</summary>
    public string BakiyeyeDahilEdilmeDurumu { get; set; } = string.Empty;

    public List<string> BakiyeyeDahilEdilmemeNedenKodlari { get; set; } = [];
    public List<string> BakiyeyeDahilEdilmemeAciklamalari { get; set; } = [];

    /// <summary>Odemenin fiilen etkiledigi cari hesap/borc (kapama hareketi varsa onun hedefi).</summary>
    public string? EtkiledigiCariVeyaBorc { get; set; }
    public decimal? EtkiledigiTutar { get; set; }

    /// <summary>Etkilenen tutarin para birimi - farkli para birimleri birlestirilmediginden ayrica tasinir.</summary>
    public string? EtkiledigiParaBirimi { get; set; }

    /// <summary>true ise bagli kasa/banka hesabi yetki kapsami disinda; hesap ayrintilari
    /// (ad, banka adi, maskeli IBAN, muhasebe hesap kodu) DOLDURULMAMISTIR.</summary>
    public bool BagliHesapErisimKisitliMi { get; set; }

    /// <summary>true ise bagli muhasebe fisi yetki kapsami disinda; fis no/tarih/durum DOLDURULMAMISTIR.</summary>
    public bool BagliFisErisimKisitliMi { get; set; }

    /// <summary>Guvenli erisim kisiti neden kodlari (bkz. OdemeErisimKisitiNedenKodlari).</summary>
    public List<string> ErisimKisitiNedenKodlari { get; set; } = [];

    public List<OdemeUyariDto> Uyarilar { get; set; } = [];
}

public class CariHareketDokumFilterDto
{
    public int CariKartId { get; set; }
    public DateOnly? TarihBaslangic { get; set; }
    public DateOnly? TarihBitis { get; set; }
}

public class CariHareketDokumSatiriDto
{
    public int Id { get; set; }
    public DateTime HareketTarihi { get; set; }
    public string BelgeTuru { get; set; } = string.Empty;
    public string? BelgeNo { get; set; }
    public string? Aciklama { get; set; }
    public decimal BorcTutari { get; set; }
    public decimal AlacakTutari { get; set; }
    public decimal KalanTutar { get; set; }
    public string Durum { get; set; } = string.Empty;
    public string? KaynakModul { get; set; }
    public int? KaynakId { get; set; }
    public bool KapandiMi { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    /// <summary>Bu hareket dahil, acilis bakiyesinden itibaren kumulatif bakiye (Borc - Alacak, yalnizca
    /// Durum=Aktif hareketler dahil edilir).</summary>
    public decimal KumulatifBakiye { get; set; }
    /// <summary>Bu hareket hesaplama disi mi birakildi (ör. Iptal durumunda) - true ise KumulatifBakiye
    /// bir onceki satirla AYNIDIR (bu hareket bakiyeyi degistirmemistir).</summary>
    public bool HesaplamaDisiMi { get; set; }
}

/// <summary>Bir carinin bakiyesini ayristiran, PARA BIRIMI bazinda tutulan toplamlar. Farkli para
/// birimleri ASLA tek bir toplamda birlestirilmez. Her hareket bu gruplardan YALNIZCA BIRINDE
/// sayilir - ayni odeme belge/cari hareket/valor/fis kayitlarinda bulundugu icin mukerrer toplanmaz.</summary>
public class CariBakiyeParaBirimiOzetiDto
{
    public string ParaBirimi { get; set; } = "TRY";

    /// <summary>Acilis bakiyesi + tarih araligi BASLANGICINDAN ONCE bakiyeye gercekten dahil olmus
    /// (Durum=Aktif) hareketlerin net etkisi. Tarih araligi verilmediyse yalnizca acilis bakiyesidir.</summary>
    public decimal DevredenBakiye { get; set; }

    /// <summary>Yalnizca DONEM ICI (tarih araligindaki) aktif hareketlerin borc toplami.</summary>
    public decimal ToplamBorc { get; set; }
    public decimal ToplamAlacak { get; set; }

    /// <summary>Iptal edilmis (Durum != Aktif) cari hareketlerin tutari - bakiyeye DAHIL DEGILDIR.</summary>
    public decimal IptalEdilmisTutar { get; set; }

    /// <summary>Durum=ValorBekliyor olan, normal seyrinde aktarilmayi bekleyen POS tahsilatlari.</summary>
    public decimal NormalAktarilmayiBekleyenPos { get; set; }

    /// <summary>Durum=MutabakatBekliyor - normal bekleyenle BIRLESTIRILMEZ.</summary>
    public decimal MutabakatBekleyenPos { get; set; }

    /// <summary>Durum=Hata - normal bekleyenle BIRLESTIRILMEZ.</summary>
    public decimal HataliPos { get; set; }

    /// <summary>Aktarim/ters kayit ara durumlarindaki (sonucu kesinlesmemis) POS tutari.</summary>
    public decimal AktarimSurecindekiPos { get; set; }

    /// <summary>DevredenBakiye + dönem içi aktif hareketlerin net etkisi = ACIKLANAN kalan bakiye.</summary>
    public decimal AciklananKalanBakiye { get; set; }

    /// <summary>Tarih alani guvenilir olmadigi icin doneme KATILMAYAN POS kayitlarinin tutari
    /// (bkz. CariHareketDokumDto.Uyarilar). Hicbir toplama dahil edilmez.</summary>
    public decimal DonemeKatilmayanBelirsizTarihliPos { get; set; }
}

/// <summary>Bir carinin bakiyesini ACIKLANABILIR sekilde aciklayan tam dokum - "eksik odeme" yalnizca
/// tek bir fark rakami olarak DEGIL, ayristirilmis kalemler halinde sunulur.</summary>
public class CariHareketDokumDto
{
    public int CariKartId { get; set; }
    public string CariUnvan { get; set; } = string.Empty;
    public decimal AcilisBakiyeTutari { get; set; }
    public string? AcilisBakiyeYonu { get; set; }
    public List<CariHareketDokumSatiriDto> Hareketler { get; set; } = [];

    /// <summary>Para birimi bazinda ayristirilmis toplamlar - tek bir birlesik toplam URETILMEZ.</summary>
    public List<CariBakiyeParaBirimiOzetiDto> ParaBirimiOzetleri { get; set; } = [];

    /// <summary>Uygulanan tarih araligi (verildiyse) - kullanicinin hangi kapsamin gecerli oldugunu
    /// tahmin etmemesi icin echo edilir.</summary>
    public DateOnly? TarihBaslangic { get; set; }
    public DateOnly? TarihBitis { get; set; }

    /// <summary>Dokum sirasinda olusan veri kalitesi uyarilari (ör. tarihi belirsiz oldugu icin
    /// doneme katilamayan POS kayitlari).</summary>
    public List<string> Uyarilar { get; set; } = [];
}

public class BeyanEdilenOdemeKarsilastirmaFilterDto
{
    public int? TesisId { get; set; }
    public DateOnly Tarih { get; set; }
    /// <summary>Beyan edilen tarihin +/- kac gun toleransla aranacagi (varsayilan 1).</summary>
    public int TarihToleransGun { get; set; } = 1;
    public decimal Tutar { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public string? OdemeYontemi { get; set; }
    public int? KasaBankaHesapId { get; set; }
    /// <summary>Kullanicinin hatirladigi/dekonttaki belge no - dosya yukleme YOKTUR, yalnizca metin
    /// arama. KESIN eslesme icin bu deger normalize edilmis haliyle BIREBIR esitlenmelidir; kismi
    /// metin kesin eslesme uretmez. Cok kisa degerlerle genis/hassas arama yapilmasini engellemek
    /// icin backend'de minimum uzunluk dogrulamasi uygulanir (bkz. MinimumReferansUzunlugu).</summary>
    public string? BelgeNoTahmini { get; set; }
    public int? CariKartId { get; set; }

    /// <summary>Referans aramalarinda kabul edilen en kisa (normalize edilmis) uzunluk.</summary>
    public const int MinimumReferansUzunlugu = 4;
}

public class BeyanEdilenOdemeEslesmeDto
{
    public int OdemeId { get; set; }
    public string BelgeNo { get; set; } = string.Empty;
    public DateTime BelgeTarihi { get; set; }
    public decimal Tutar { get; set; }
    public string ParaBirimi { get; set; } = string.Empty;
    public string OdemeYontemi { get; set; } = string.Empty;
    public string CariUnvan { get; set; } = string.Empty;
    public string GuvenSeviyesi { get; set; } = string.Empty;
    public string Gerekce { get; set; } = string.Empty;

    /// <summary>Hangi alanlarin eslestigi (kullaniciya aciklanabilirlik icin).</summary>
    public List<string> EslesenAlanlar { get; set; } = [];

    /// <summary>Hangi alanlarin UYUSMADIGI veya dogrulanamadigi.</summary>
    public List<string> UyusmayanAlanlar { get; set; } = [];

    /// <summary>Tarih birebir mi yoksa tolerans araligiyla mi eslesti - toleransli eslesme
    /// "birebir tarih" olarak RAPORLANMAZ.</summary>
    public bool TarihBirebirMi { get; set; }
    public int TarihFarkiGun { get; set; }

    /// <summary>Referans karsilastirmasinda uygulanan normalizasyonun kullaniciya aciklamasi.</summary>
    public string KullanilanNormalizasyon { get; set; } = string.Empty;

    /// <summary>Normalize referansin yetki kapsaminda TEKIL olup olmadigi. false ise ayni numaraya
    /// sahip birden fazla kayit vardir ve KESIN eslesme URETILMEZ. Referans verilmediyse null.</summary>
    public bool? ReferansTekilMi { get; set; }
}
