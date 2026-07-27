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

    /// <summary>YALNIZCA veri kalitesi kapisindan gecmis, Durum=ValorBekliyor kayitlarin net
    /// toplami. Rapor tarihi GECMISTE ise bu deger HER ZAMAN 0'dir - gecmis tarihli POS pozisyonu
    /// desteklenmez (bkz. PosPozisyonuHesaplandiMi).</summary>
    public decimal ToplamBekleyenNetPos { get; set; }

    /// <summary>ToplamBankaMuhasebeBakiyesi + ToplamBekleyenNetPos. Gecmis tarihte POS bileseni
    /// 0 oldugu icin bu deger yalnizca muhasebe bakiyesine esittir (uydurma POS tahmini yapilmaz).</summary>
    public decimal TahminiToplamBankaPozisyonu { get; set; }

    public decimal MutabakatBekleyenToplam { get; set; }
    public int MutabakatBekleyenAdet { get; set; }
    public decimal HataliToplam { get; set; }
    public int HataliAdet { get; set; }

    /// <summary>true ise secilen rapor tarihi bugunden ONCESIDIR.</summary>
    public bool GecmisTarihRaporuMu { get; set; }

    /// <summary>false ise POS/valor pozisyonu HIC hesaplanmamistir (gecmis tarih secildigi icin) -
    /// tum POS alanlari 0'dir ve tahmini bakiye yalnizca muhasebe bakiyesini yansitir. Frontend bu
    /// durumda POS kartlarini gostermemeli veya "hesaplanmadi" olarak isaretlemelidir.</summary>
    public bool PosPozisyonuHesaplandiMi { get; set; } = true;

    /// <summary>PosPozisyonuHesaplandiMi=false ise nedenini aciklayan, kullaniciya gosterilebilir metin.</summary>
    public string? PosPozisyonuHesaplanmamaNedeni { get; set; }

    public int UyariSayisi { get; set; }

    /// <summary>Normal toplamin DISINDA tutulan tutarlarin neden+para birimi bazinda ozeti.</summary>
    public List<UyariliTutarOzetiDto> UyariliTutarlar { get; set; } = [];

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

    /// <summary>Bu hesaba ait, veri kalitesi kapisindan gecmis normal bekleyen kayitlarin net
    /// toplami. Gecmis tarihli raporda HER ZAMAN 0'dir.</summary>
    public decimal ToplamBekleyenNet { get; set; }

    /// <summary>StysMuhasebeBakiyesi + ToplamBekleyenNet. MuhasebeBakiyesiGecerliMi=false ise
    /// (hesabin gecerli bir muhasebe baglantisi yoksa) bu alan null'dir - yalnizca POS tutarindan
    /// olusan sahte bir "bakiye" URETILMEZ.</summary>
    public decimal? TahminiBakiye { get; set; }

    /// <summary>false ise hesabin gecerli (mevcut+aktif+silinmemis) bir muhasebe hesabi baglantisi
    /// yoktur; StysMuhasebeBakiyesi anlamsizdir ve TahminiBakiye uretilmez.</summary>
    public bool MuhasebeBakiyesiGecerliMi { get; set; } = true;

    public decimal MutabakatBekleyenNet { get; set; }
    public int MutabakatBekleyenAdet { get; set; }
    public decimal HataliNet { get; set; }
    public int HataliAdet { get; set; }

    /// <summary>Bu hesaba ait, normal toplama dahil EDILMEYEN tutarlarin neden bazinda ozeti.</summary>
    public List<UyariliTutarOzetiDto> UyariliTutarlar { get; set; } = [];

    public DateTime? SonMuhasebeHareketTarihi { get; set; }
}

/// <summary>GetPozisyonAsync'in tek, birlesik sonucu - ozet + hesap listeleri + uyarilar TEK
/// sorgu calistirmasindan uretilir (bkz. servis - /ozet ve /hesaplar'in ayri ayri, birbirini
/// tekrar eden iki sorgu calistirmasi sorunu boylece ortadan kalkar).</summary>
public class NakitBankaPozisyonuDto
{
    public DateOnly RaporTarihi { get; set; }
    public bool GecmisTarihRaporuMu { get; set; }

    /// <summary>Bkz. NakitBankaPozisyonuOzetDto.PosPozisyonuHesaplandiMi.</summary>
    public bool PosPozisyonuHesaplandiMi { get; set; } = true;
    public string? PosPozisyonuHesaplanmamaNedeni { get; set; }
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

    /// <summary>Tutarin para birimi - farkli para birimlerindeki uyarilar AYRI satirlarda toplanir,
    /// birlestirilmez.</summary>
    public string? ParaBirimi { get; set; }

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

    /// <summary>Secilen rapor tarihi GECMISTE oldugu icin POS/valor pozisyonu HIC hesaplanmadi.
    /// PosTahsilatValor'da iptal zamani ve durum gecis tarihcesi TUTULMADIGINDAN (yalnizca
    /// CreatedAt/AktarimTarihi/DeletedAt mevcuttur) bir kaydin gecmis bir tarihteki gercek durumu
    /// deterministik olarak kurulamaz; bu yuzden tahmini bir POS tutari URETILMEZ (bkz. teslim
    /// raporu - "gecmis tarihli POS pozisyonu desteklenmiyor").</summary>
    public const string GecmisTarihPosPozisyonuHesaplanmadi = "GecmisTarihPosPozisyonuHesaplanmadi";

    /// <summary>Kaydin Durum degeri bu ekran tarafindan taninmiyor (projeye sonradan eklenmis
    /// olabilir) - guvenli varsayilan olarak finansal toplamlarin DISINDA tutuldu.</summary>
    public const string TaninmayanValorDurumu = "TaninmayanValorDurumu";

    /// <summary>Bagli banka hesabinin gecerli (mevcut + aktif + silinmemis) bir muhasebe hesabi
    /// baglantisi yok - muhasebe bakiyesi hesaplanamadigi icin bu hesap icin tahmini bakiye
    /// URETILMEZ (yalnizca POS tutarindan olusan sahte bir "bakiye" gosterilmez).</summary>
    public const string BankaHesabininMuhasebeBaglantisiGecersiz = "BankaHesabininMuhasebeBaglantisiGecersiz";

    /// <summary>Bagli muhasebe hesabi PASIF (AktifMi=false) - normal pozisyona dahil edilmez.</summary>
    public const string PasifBaglantiliMuhasebeHesabi = "PasifBaglantiliMuhasebeHesabi";

    /// <summary>Mutabakat bekleyen tutar - normal toplamin disinda, ayri izlenir.</summary>
    public const string MutabakatBekleyen = "MutabakatBekleyen";

    /// <summary>Aktarimi hata ile sonuclanmis tutar - normal toplamin disinda, ayri izlenir.</summary>
    public const string HataliValor = "HataliValor";

    /// <summary>Aktarim/ters kayit ara durumundaki (sonucu kesinlesmemis) tutar.</summary>
    public const string AktarimSurecindeValor = "AktarimSurecindeValor";

    /// <summary>AYNI banka/IBAN hesabinin birden fazla aktif muhasebe hesabina baglanmasi.
    /// KasaBankaHesap.MuhasebeHesapPlaniId TEKIL bir FK oldugu icin bu yon semayla yapisal olarak
    /// IMKANSIZDIR; sabit yalnizca iki yonun karistirilmamasi icin ayrica tanimlanmistir.</summary>
    public const string AyniBankaHesabiBirdenFazlaMuhasebeHesabinaBagli = "AyniBankaHesabiBirdenFazlaMuhasebeHesabinaBagli";

    /// <summary>Kayit bir muhasebe fisine bagli gorunuyor ancak fis GERCEKTEN dogrulanamadi
    /// (bulunamadi / soft-delete / mali etki olusturmayan durum / farkli tesis / fis satirinda
    /// beklenen kasa-banka hesabi etkilenmemis). Yalnizca ID'nin dolu olmasi yeterli DEGILDIR.</summary>
    public const string AktarimFisiDogrulanamadi = "AktarimFisiDogrulanamadi";

    /// <summary>POS valor kaydinin tesisi ile hedef banka hesabinin tesisi FARKLI - kayit o hesabin
    /// toplamina dahil edilemez.</summary>
    public const string ValorBankaHesabiTesisUyumsuz = "ValorBankaHesabiTesisUyumsuz";

    /// <summary>Para birimi tanimsiz/bos - TRY VARSAYILMAZ, hicbir toplama dahil edilmez.</summary>
    public const string ParaBirimiTanimsiz = "ParaBirimiTanimsiz";

    /// <summary>Ters kayit fisi gecerli olsa bile ASIL fisi gercekten tersledigi
    /// KANITLANAMADI (IptalEdilenFisId iliskisi yok/farkli, tesis veya tutar uyumsuz, ya da ayni
    /// asil fise birden fazla ters kayit bagli).</summary>
    public const string TersKayitIliskisiDogrulanamadi = "TersKayitIliskisiDogrulanamadi";
}

/// <summary>Normal finansal toplamin DISINDA tutulan tutarlarin, DAHIL EDILMEME NEDENINE ve PARA
/// BIRIMINE gore ayristirilmis ozeti. Bu tutarlar tahmini bakiyeye HICBIR ZAMAN eklenmez.</summary>
public class UyariliTutarOzetiDto
{
    public string UyariTipi { get; set; } = string.Empty;
    public string ParaBirimi { get; set; } = "TRY";
    public int Adet { get; set; }
    public decimal ToplamNetTutar { get; set; }
    public string Aciklama { get; set; } = string.Empty;
}
