namespace STYS.Muhasebe.OdemeIzleme.Dtos;

/// <summary>Guven seviyesi - eslesme ASLA yalnizca tutara dayanmaz, birden fazla sinyalin (banka/
/// POS referansi, tahsilat no, fis no, cari, rezervasyon iliskisi, tarih/saat, tutar, PB, banka/
/// IBAN/kasa, aciklama, kullanici, iptal/ters kayit iliskisi) birlikte degerlendirilmesiyle belirlenir.</summary>
public static class OdemeGuvenSeviyeleri
{
    /// <summary>Veri butunlugu acisindan KESIN (ornegin mukerrer BelgeNo, eksik zorunlu FK) - tahmin degil.</summary>
    public const string Kesin = "Kesin";
    /// <summary>Birden fazla guclu sinyal (tarih+tutar+banka/yontem) ortusuyor ama kesin degil.</summary>
    public const string YuksekOlasilik = "YuksekOlasilik";
    /// <summary>Yalnizca zayif sinyaller (ör. yalnizca tarih+tutar) - incelenmesi onerilir, kesin sunulmaz.</summary>
    public const string IncelenmesiGereken = "IncelenmesiGereken";
    public const string EslesmeYok = "EslesmeYok";
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

    /// <summary>Bu odeme rapor/bakiye hesaplarina (ör. Nakit ve Banka Pozisyonu, Hizli Mizan) DAHIL mi -
    /// ve degilse NEDEN (ör. "Iptal edilmis").</summary>
    public bool BakiyeyeDahilMi { get; set; }
    public string? BakiyeyeDahilDegilGerekcesi { get; set; }

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
    /// <summary>Bu hareket dahil, acilis bakiyesinden itibaren kumulatif bakiye (Borc - Alacak, yalnizca
    /// Durum=Aktif hareketler dahil edilir).</summary>
    public decimal KumulatifBakiye { get; set; }
    /// <summary>Bu hareket hesaplama disi mi birakildi (ör. Iptal durumunda) - true ise KumulatifBakiye
    /// bir onceki satirla AYNIDIR (bu hareket bakiyeyi degistirmemistir).</summary>
    public bool HesaplamaDisiMi { get; set; }
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

    public decimal ToplamBorc { get; set; }
    public decimal ToplamAlacak { get; set; }
    public decimal ToplamIptalEdilmisTutar { get; set; }
    /// <summary>Bu cariye ait, POS valor'u henuz bankaya aktarilmamis (bekleyen) tahsilatlarin toplami -
    /// bu tutar KalanBakiye'ye NEDEN dahil olmadigini/oldugunu aciklamak icin ayrica gosterilir.</summary>
    public decimal AktarilmayiBekleyenPosTutari { get; set; }
    public decimal KalanBakiye { get; set; }
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
    /// <summary>Kullanicinin hatirladigi/dekonttaki belge no - dosya yukleme YOKTUR, yalnizca metin arama.</summary>
    public string? BelgeNoTahmini { get; set; }
    public int? CariKartId { get; set; }
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
}
