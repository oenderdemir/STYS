namespace STYS.Muhasebe.NakitBankaPozisyonu.Dtos;

/// <summary>Nakit ve Banka Pozisyonu ekraninin tum sorgulari icin ortak filtre. RaporTarihi
/// verilmezse servis Europe/Istanbul saat dilimine gore bugunu kullanir. MaliYil/Donem yalnizca
/// bir muhasebe donemi secildiginde (rapor tarihi yerine) o donemin bitis tarihini rapor tarihi
/// olarak kullanmak icin cozumlenir - bkz. NakitBankaPozisyonuService.ResolveRaporTarihiAsync.</summary>
public class NakitBankaPozisyonuFilterDto
{
    public int? TesisId { get; set; }
    public DateOnly? RaporTarihi { get; set; }
    public int? MaliYil { get; set; }
    public int? Donem { get; set; }

    /// <summary>"Tumu" | "Kasa" | "Banka" - bos/null ise Tumu.</summary>
    public string? HesapTuru { get; set; }
    public int? BankaHesapId { get; set; }
    public string? ParaBirimi { get; set; }

    /// <summary>PosTahsilatValorDurumlari degerlerinden biri - yalnizca hesap listesi/valor
    /// takvimindeki DETAY kayitlarini filtrelemek icin kullanilir, ozet toplamlarini ETKILEMEZ.</summary>
    public string? ValorDurumu { get; set; }
}

/// <summary>Genel ozet karti verisi - tum tesis/hesap kapsamindaki TEK bir toplam settir.</summary>
public class NakitBankaPozisyonuOzetDto
{
    public DateOnly RaporTarihi { get; set; }

    public decimal ToplamNakit { get; set; }
    public decimal ToplamBankaMuhasebeBakiyesi { get; set; }

    public decimal ValoruGecmisBekleyenNet { get; set; }
    public decimal BugunGelecekNet { get; set; }
    public decimal YarinGelecekNet { get; set; }
    public decimal Takip2_7GunGelecekNet { get; set; }
    public decimal Sonraki7GundenSonraNet { get; set; }

    /// <summary>Yalnizca normal bekleyen (Durum=ValorBekliyor) kayitlarin net tutar toplami -
    /// MutabakatBekliyor/Hata kayitlar buna DAHIL EDILMEZ (ayri uyari tutari olarak raporlanir).</summary>
    public decimal ToplamBekleyenNetPos { get; set; }

    /// <summary>ToplamBankaMuhasebeBakiyesi + ToplamBekleyenNetPos.</summary>
    public decimal TahminiToplamBankaPozisyonu { get; set; }

    public decimal MutabakatBekleyenToplam { get; set; }
    public int MutabakatBekleyenAdet { get; set; }
    public decimal HataliToplam { get; set; }
    public int HataliAdet { get; set; }

    public int UyariSayisi { get; set; }

    /// <summary>Para birimine gore ayri toplamlar - farkli para birimleri DOGRUDAN TOPLANMAZ
    /// (kur donusum altyapisi projede yok).</summary>
    public List<ParaBirimiOzetDto> ParaBirimiOzetleri { get; set; } = [];
}

public class ParaBirimiOzetDto
{
    public string ParaBirimi { get; set; } = "TRY";
    public decimal ToplamNakit { get; set; }
    public decimal ToplamBankaMuhasebeBakiyesi { get; set; }
    public decimal ToplamBekleyenNetPos { get; set; }
    public decimal TahminiToplamBankaPozisyonu { get; set; }
}

public class NakitHesapPozisyonuDto
{
    public int KasaBankaHesapId { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string Kod { get; set; } = string.Empty;
    public string ParaBirimi { get; set; } = "TRY";
    public int? MuhasebeHesapPlaniId { get; set; }
    public string? MuhasebeHesapKodu { get; set; }
    public string? MuhasebeHesapAdi { get; set; }
    public decimal MuhasebeBakiyesi { get; set; }
    public DateTime? SonHareketTarihi { get; set; }
}

public class BankaHesapPozisyonuDto
{
    public int KasaBankaHesapId { get; set; }
    public string BankaAdi { get; set; } = string.Empty;
    public string HesapAdi { get; set; } = string.Empty;
    public string? Iban { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public int? MuhasebeHesapPlaniId { get; set; }
    public string? MuhasebeHesapKodu { get; set; }

    /// <summary>"STYS Muhasebe Bakiyesi" - bankanin gercek kullanilabilir bakiyesi DEGILDIR, banka
    /// ekstresi entegrasyonu yoktur.</summary>
    public decimal StysMuhasebeBakiyesi { get; set; }

    public decimal ValoruGecmisBekleyenNet { get; set; }
    public decimal BugunGelecekNet { get; set; }
    public decimal YarinGelecekNet { get; set; }
    public decimal Takip2_7GunGelecekNet { get; set; }
    public decimal Sonraki7GundenSonraNet { get; set; }

    /// <summary>Yalnizca Durum=ValorBekliyor kayitlarin bu hesaba ait net toplami (tum tarih
    /// gruplarinin toplami ile ayni).</summary>
    public decimal ToplamBekleyenNet { get; set; }

    /// <summary>StysMuhasebeBakiyesi + ToplamBekleyenNet.</summary>
    public decimal TahminiBakiye { get; set; }

    public decimal MutabakatBekleyenNet { get; set; }
    public int MutabakatBekleyenAdet { get; set; }
    public decimal HataliNet { get; set; }
    public int HataliAdet { get; set; }

    public DateTime? SonMuhasebeHareketTarihi { get; set; }
}

public class NakitBankaPozisyonuHesaplarDto
{
    public DateOnly RaporTarihi { get; set; }
    public List<NakitHesapPozisyonuDto> KasaHesaplari { get; set; } = [];
    public List<BankaHesapPozisyonuDto> BankaHesaplari { get; set; } = [];
    public List<VeriKalitesiUyariDto> Uyarilar { get; set; } = [];
}

public class VeriKalitesiUyariDto
{
    /// <summary>Sabit bir uyari kodu (bkz. NakitBankaPozisyonuUyariTipleri).</summary>
    public string UyariTipi { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
    public int? KasaBankaHesapId { get; set; }
    public int? PosTahsilatValorId { get; set; }
    public decimal? Tutar { get; set; }
}

public class ValorDetayDto
{
    public int Id { get; set; }
    public int TahsilatOdemeBelgesiId { get; set; }
    public string? TahsilatBelgeNo { get; set; }
    public string? KrediKartiHesapAdi { get; set; }
    public DateTime OdemeTarihi { get; set; }
    public DateOnly BeklenenValorTarihi { get; set; }
    public decimal BrutTutar { get; set; }
    public decimal KomisyonTutari { get; set; }
    public decimal NetTutar { get; set; }
    public string Durum { get; set; } = string.Empty;
    public int? MuhasebeFisId { get; set; }
    public string? HataMesaji { get; set; }
}

public class GunlukValorOzetiDto
{
    public DateOnly ValorTarihi { get; set; }
    public int IslemSayisi { get; set; }
    public decimal BrutTutar { get; set; }
    public decimal KomisyonTutari { get; set; }
    public decimal NetTutar { get; set; }
    public List<ValorDetayDto> Detaylar { get; set; } = [];
}

public class BankaValorTakvimiDto
{
    public int KasaBankaHesapId { get; set; }
    public DateOnly RaporTarihi { get; set; }
    public List<GunlukValorOzetiDto> Gunler { get; set; } = [];
}

public static class NakitBankaPozisyonuUyariTipleri
{
    public const string IbanVarMuhasebeHesabiYok = "IbanVarMuhasebeHesabiYok";
    public const string MuhasebeHesabiVarIbanYok = "MuhasebeHesabiVarIbanYok";
    public const string PosValorHedefBankaBelirlenemiyor = "PosValorHedefBankaBelirlenemiyor";
    public const string NetVeyaKomisyonBilgisiEksik = "NetVeyaKomisyonBilgisiEksik";
    public const string ValorTarihiBos = "ValorTarihiBos";
    public const string AktarimDurumuFisIliskisiTutarsiz = "AktarimDurumuFisIliskisiTutarsiz";
    public const string AyniBankaHesabinaBirdenFazlaAktifMuhasebeHesabi = "AyniBankaHesabinaBirdenFazlaAktifMuhasebeHesabi";
    public const string SoftDeleteEdilmisBaglantiliMuhasebeHesabi = "SoftDeleteEdilmisBaglantiliMuhasebeHesabi";
}
