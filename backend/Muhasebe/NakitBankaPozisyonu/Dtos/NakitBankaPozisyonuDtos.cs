namespace STYS.Muhasebe.NakitBankaPozisyonu.Dtos;

/// <summary>Nakit ve Banka Pozisyonu ekraninin tum sorgulari icin ortak filtre. RaporTarihi
/// verilmezse servis Europe/Istanbul saat dilimine gore bugunu kullanir; gelecek bir tarih
/// verilmesi reddedilir (400). MaliYil/Donem yalnizca bir muhasebe donemi secildiginde (rapor
/// tarihi yerine) o donemin bitis tarihini rapor tarihi olarak kullanmak icin cozumlenir - bkz.
/// NakitBankaPozisyonuService.ResolveRaporTarihiAsync. Donem verilirse ayni zamanda o donemin
/// gercek muhasebe filtresi (FisTarihi'nin donem araligina ait olmasi) olarak da uygulanir; rapor
/// tarihi ile donem uyumsuzsa (donem araligi disinda bir RaporTarihi de ayrica verildiyse) 400
/// dondurulur.</summary>
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

    /// <summary>PosTahsilatValorDurumlari degerlerinden biri - YALNIZCA valor takvimi/gun detay
    /// sorgularinda kullanilir. Genel ozet (GetOzetAsync) ve hesap listesi toplamlarini
    /// (GetHesaplarAsync) HICBIR SEKILDE etkilemez - bu iki sorgu her zaman tam kapsamli
    /// (ValorDurumu'ndan bagimsiz) toplam pozisyonu yansitir.</summary>
    public string? ValorDurumu { get; set; }
}

/// <summary>Genel ozet karti verisi - tum tesis/hesap kapsamindaki TEK bir toplam settir. Bu
/// deger her zaman filter.ValorDurumu'ndan BAGIMSIZDIR.</summary>
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

    /// <summary>Rapor tarihi itibariyla henuz bankaya aktarilmamis (bekleyen) net POS tutarlarinin
    /// toplami. Rapor tarihi BUGUN ise bu yalnizca Durum=ValorBekliyor kayitlari icerir (Mutabakat/
    /// Hata ayri raporlanir). Rapor tarihi GECMISTE ise (bkz. GecmisTarihRaporuMu), bu deger o
    /// tarihte henuz aktarilmamis TUM kayitlarin toplamidir - gecmis tarihte hangi alt durumda
    /// (bekliyor/mutabakat/hata) olduklari guvenilir sekilde yeniden olusturulamadigi icin alt
    /// kirilim uygulanmaz, yalnizca "aktarilmamisti" bilgisi (fis tarihinden turetilir) kullanilir.</summary>
    public decimal ToplamBekleyenNetPos { get; set; }

    /// <summary>ToplamBankaMuhasebeBakiyesi + ToplamBekleyenNetPos - AYNI zaman esasini (rapor
    /// tarihi itibariyla FisTarihi &lt;= rapor tarihi) kullanir, bu yuzden iki bilesen tutarlidir.</summary>
    public decimal TahminiToplamBankaPozisyonu { get; set; }

    /// <summary>Yalnizca rapor tarihi BUGUN oldugunda anlamlidir (gecmis tarihte guvenilir sekilde
    /// ayirt edilemez, bkz. ToplamBekleyenNetPos aciklamasi).</summary>
    public decimal MutabakatBekleyenToplam { get; set; }
    public int MutabakatBekleyenAdet { get; set; }
    public decimal HataliToplam { get; set; }
    public int HataliAdet { get; set; }

    /// <summary>true ise secilen rapor tarihi bugunden ONCESIDIR; bu durumda Mutabakat/Hata alt
    /// kirilimlari 0 gelir (guvenilir sekilde reconstruct edilemedigi icin) ve bunun yerine ilgili
    /// kayitlar ToplamBekleyenNetPos icinde, GecmisTarihIcinDurumBelirsiz uyarisiyla birlikte yer
    /// alir. Frontend bu alani gorunur bir bilgi notu olarak kullanmalidir.</summary>
    public bool GecmisTarihRaporuMu { get; set; }

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
    public int TesisId { get; set; }
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
    public int TesisId { get; set; }
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

    /// <summary>Bu hesaba ait, rapor tarihi itibariyla henuz aktarilmamis tum kayitlarin net
    /// toplami (tarih gruplarinin toplami ile ayni). Bkz. NakitBankaPozisyonuOzetDto.ToplamBekleyenNetPos.</summary>
    public decimal ToplamBekleyenNet { get; set; }

    /// <summary>StysMuhasebeBakiyesi + ToplamBekleyenNet.</summary>
    public decimal TahminiBakiye { get; set; }

    public decimal MutabakatBekleyenNet { get; set; }
    public int MutabakatBekleyenAdet { get; set; }
    public decimal HataliNet { get; set; }
    public int HataliAdet { get; set; }

    public DateTime? SonMuhasebeHareketTarihi { get; set; }
}

/// <summary>GetPozisyonAsync'in tek, birlesik sonucu - ozet + hesap listeleri + uyarilar TEK
/// sorgu calistirmasindan uretilir (bkz. servis - /ozet ve /hesaplar'in ayri ayri, birbirini
/// tekrar eden iki sorgu calistirmasi sorunu boylece ortadan kalkar).</summary>
public class NakitBankaPozisyonuDto
{
    public DateOnly RaporTarihi { get; set; }
    public bool GecmisTarihRaporuMu { get; set; }
    public NakitBankaPozisyonuOzetDto Ozet { get; set; } = new();
    public List<NakitHesapPozisyonuDto> KasaHesaplari { get; set; } = [];
    public List<BankaHesapPozisyonuDto> BankaHesaplari { get; set; } = [];
    public List<VeriKalitesiUyariDto> Uyarilar { get; set; } = [];

    /// <summary>Bu sonucu uretmek icin fiilen uygulanan filtre - frontend'in "hangi filtre
    /// gecerli oldu" konusunda tahmine dayanmamasi icin echo edilir.</summary>
    public NakitBankaPozisyonuFilterDto UygulananFiltre { get; set; } = new();
}

public class VeriKalitesiUyariDto
{
    /// <summary>Sabit bir uyari kodu (bkz. NakitBankaPozisyonuUyariTipleri).</summary>
    public string UyariTipi { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
    public int? KasaBankaHesapId { get; set; }
    public int? PosTahsilatValorId { get; set; }
    public decimal? Tutar { get; set; }

    /// <summary>Bu uyariya konu kayit adedi - ayni UyariTipi+KasaBankaHesapId icin birden fazla
    /// PosTahsilatValor kaydi varsa, servis bunlari TEK bir ozet satirinda toplar (adet+tutar) -
    /// yuzlerce ayni-turden uyarinin listeyi bogmasi engellenir.</summary>
    public int Adet { get; set; } = 1;
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
}

/// <summary>Yalnizca gun bazinda OZET listesi - detay satirlari icermez (bkz.
/// GetValorGunDetaylariAsync, ayri, sayfali bir sorgudur). Kullanici bir gunu actiginda yalnizca o
/// gunun sayfali detaylari ayrica yuklenir.</summary>
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

    /// <summary>Bir muhasebe hesabina (tek bir MuhasebeHesapPlaniId'ye) birden fazla aktif banka/
    /// IBAN hesabi baglanmis (muhasebe hesabi -> cok sayida banka hesabi yonu). KasaBankaHesap'in
    /// MuhasebeHesapPlaniId'si tekil (tek) bir FK oldugu icin bunun TERSI (bir banka hesabinin
    /// birden fazla aktif muhasebe hesabina baglanmasi) semayla yapisal olarak imkansizdir - bu
    /// yuzden yalnizca bu tek yonlu kontrol vardir.</summary>
    public const string AyniMuhasebeHesabinaBirdenFazlaAktifBankaHesabiBagli = "AyniMuhasebeHesabinaBirdenFazlaAktifBankaHesabiBagli";
    public const string SoftDeleteEdilmisBaglantiliMuhasebeHesabi = "SoftDeleteEdilmisBaglantiliMuhasebeHesabi";

    /// <summary>PosTahsilatValor.BagliBankaHesapId dolu ama hedef KasaBankaHesap kaydi
    /// bulunamiyor, silinmis (soft-delete) veya pasif - bu kayit hicbir bankaya bucket'lanamaz.</summary>
    public const string BankaHesabiBulunamadiVeyaPasif = "BankaHesabiBulunamadiVeyaPasif";

    /// <summary>PosTahsilatValor.ParaBirimi, bagli oldugu banka hesabinin para biriminden farkli -
    /// kur donusum altyapisi olmadigi icin bu kayit o bankanin toplamina KATILAMAZ.</summary>
    public const string ParaBirimiUyusmuyor = "ParaBirimiUyusmuyor";

    /// <summary>Secilen rapor tarihi GECMISTE ve bu kayit rapor tarihi itibariyla henuz
    /// aktarilmamisti (bekleyen tutara dahil edildi) ancak GUNCEL durumu (Mutabakat Bekliyor / Hata
    /// / Iptal) rapor tarihindeki durumuyla AYNI OLMAYABILIR - PosTahsilatValor'da durum
    /// gecislerinin zaman damgali bir gecmisi tutulmadigindan bu ayrim gecmise donuk guvenilir
    /// sekilde yeniden olusturulamaz; yalnizca "aktarilmamisti" bilgisi (MuhasebeFisId'nin bagli
    /// oldugu fisin FisTarihi'nden turetilir) kullanilmistir.</summary>
    public const string GecmisTarihIcinDurumBelirsiz = "GecmisTarihIcinDurumBelirsiz";

    /// <summary>Kayit su an Iptal durumunda ve iptalin rapor tarihinden ONCE mi SONRA mi
    /// gerceklestigi guvenilir sekilde bilinemiyor (IptalTarihi alani yok) - kayit temkinli
    /// sekilde bekleyen toplamdan HARIC tutulmustur; iptal aslinda rapor tarihinden SONRA
    /// gerceklesmisse bu, o gun icin pozisyonun OLDUGUNDAN DUSUK gorunmesine yol acabilir.</summary>
    public const string GecmisTarihIcinIptalZamanlamasiBelirsiz = "GecmisTarihIcinIptalZamanlamasiBelirsiz";
}
