using STYS.Tesisler.Entities;

namespace STYS.Kamp;

public static class KampBasvuruSahibiTipleri
{
    public const string TarimOrmanPersoneli = "TarimOrmanPersoneli";
    public const string TarimOrmanEmeklisi = "TarimOrmanEmeklisi";
    public const string BagliKurulusPersoneli = "BagliKurulusPersoneli";
    public const string BagliKurulusEmeklisi = "BagliKurulusEmeklisi";
    public const string DigerKamuPersoneli = "DigerKamuPersoneli";
    public const string DigerKamuEmeklisi = "DigerKamuEmeklisi";
    public const string Diger = "Diger";

    public static readonly string[] Hepsi =
    [
        TarimOrmanPersoneli,
        TarimOrmanEmeklisi,
        BagliKurulusPersoneli,
        BagliKurulusEmeklisi,
        DigerKamuPersoneli,
        DigerKamuEmeklisi,
        Diger
    ];

    public static bool IsPublic(string tip)
        => tip != Diger;
}

public static class KampKatilimciTipleri
{
    public const string Kamu = "Kamu";
    public const string SehitGaziMalul = "SehitGaziMalul";
    public const string Diger = "Diger";

    public static readonly string[] Hepsi = [Kamu, SehitGaziMalul, Diger];

    /// <summary>
    /// Talimat: "Sehit yakinlari ile gaziler, harp ve vazife malulleri ve yakinlarina,
    /// belgelendirmeleri durumunda, Bakanlik mensuplari icin belirlenen tarife uzerinden
    /// ucretlendirme yapilacaktir."
    /// </summary>
    public static bool KamuTarifesiUygulanirMi(string katilimciTipi)
        => katilimciTipi is Kamu or SehitGaziMalul;
}

public static class KampAkrabalikTipleri
{
    public const string BasvuruSahibi = "BasvuruSahibi";
    public const string Es = "Es";
    public const string Cocuk = "Cocuk";
    public const string Anne = "Anne";
    public const string Baba = "Baba";
    public const string Kardes = "Kardes";
    public const string Diger = "Diger";

    public static readonly string[] Hepsi = [BasvuruSahibi, Es, Cocuk, Anne, Baba, Kardes, Diger];

    public static bool IsYakindanDogrulanabilir(string tip)
        => tip is BasvuruSahibi or Es or Cocuk or Anne or Baba;
}

public static class KampBasvuruDurumlari
{
    public const string Beklemede = "Beklemede";
    public const string TahsisEdildi = "TahsisEdildi";
    public const string TahsisEdilemedi = "TahsisEdilemedi";
    public const string Reddedildi = "Reddedildi";
    public const string IptalEdildi = "IptalEdildi";

    public static readonly string[] Hepsi = [Beklemede, TahsisEdildi, TahsisEdilemedi, Reddedildi, IptalEdildi];

    public static readonly string[] TahsisKararlari = [Beklemede, TahsisEdildi, TahsisEdilemedi];
}

public static class KampKonaklamaBirimiTipleri
{
    public const string AlataStandart = "AlataStandart";
    public const string FocaPrefabrik = "FocaPrefabrik";
    public const string FocaOtel = "FocaOtel";
    public const string FocaBetonarme = "FocaBetonarme";

    public static readonly string[] Hepsi = [AlataStandart, FocaPrefabrik, FocaOtel, FocaBetonarme];
}

public static class KampRezervasyonDurumlari
{
    public const string Aktif = "Aktif";
    public const string IptalEdildi = "IptalEdildi";

    public static readonly string[] Hepsi = [Aktif, IptalEdildi];
}

public static class KampIadeNedenleri
{
    public const string TahsisYapilamadi = "TahsisYapilamadi";
    public const string BirHaftaOncesiVazgecme = "BirHaftaOncesiVazgecme";
    public const string GecBildirimMazeretsiz = "GecBildirimMazeretsiz";
    public const string BildirimYok = "BildirimYok";
    public const string KampBasladi = "KampBasladi";
    public const string ZorunluAyrilis = "ZorunluAyrilis";
}

public static class KampBasvuruKurallari
{
    public static readonly DateTime UcretsizCocukSiniri = new(2022, 1, 1);
    public static readonly DateTime YarimUcretliCocukSiniri = new(2019, 1, 1);
    public const decimal KamuAvansKisiBasi = 1700m;
    public const decimal DigerAvansKisiBasi = 2550m;
    public const decimal YemekOrani = 0.50m;

    public static KampKonaklamaKonfigurasyonu ResolveKonaklama(Tesis tesis, string konaklamaBirimiTipi)
    {
        var tesisAd = (tesis.Ad ?? string.Empty).Trim().ToLowerInvariant();
        var isFoca = tesisAd.Contains("foca") || tesisAd.Contains("foça");
        var isAlata = tesisAd.Contains("alata");

        return konaklamaBirimiTipi switch
        {
            KampKonaklamaBirimiTipleri.AlataStandart when isAlata => new KampKonaklamaKonfigurasyonu(konaklamaBirimiTipi, 3, 4, 1700m, 2550m, 45m, 45m, 60m),
            KampKonaklamaBirimiTipleri.FocaPrefabrik when isFoca => new KampKonaklamaKonfigurasyonu(konaklamaBirimiTipi, 4, 5, 1550m, 2325m, 40m, 40m, 50m),
            KampKonaklamaBirimiTipleri.FocaOtel when isFoca => new KampKonaklamaKonfigurasyonu(konaklamaBirimiTipi, 4, 5, 1550m, 2325m, 40m, 40m, 50m),
            KampKonaklamaBirimiTipleri.FocaBetonarme when isFoca => new KampKonaklamaKonfigurasyonu(konaklamaBirimiTipi, 4, 5, 1700m, 2550m, 40m, 40m, 50m),
            _ => throw new InvalidOperationException($"'{tesis.Ad}' tesisi icin desteklenmeyen konaklama birimi tipi: {konaklamaBirimiTipi}")
        };
    }

    public static int GetOncelik(string basvuruSahibiTipi)
        => basvuruSahibiTipi switch
        {
            KampBasvuruSahibiTipleri.TarimOrmanPersoneli or KampBasvuruSahibiTipleri.TarimOrmanEmeklisi => 1,
            KampBasvuruSahibiTipleri.BagliKurulusPersoneli or KampBasvuruSahibiTipleri.BagliKurulusEmeklisi => 2,
            KampBasvuruSahibiTipleri.DigerKamuPersoneli or KampBasvuruSahibiTipleri.DigerKamuEmeklisi => 3,
            _ => 4
        };

    public static int GetTabanPuan(string basvuruSahibiTipi)
        => basvuruSahibiTipi switch
        {
            KampBasvuruSahibiTipleri.TarimOrmanPersoneli => 40,
            KampBasvuruSahibiTipleri.TarimOrmanEmeklisi => 20,
            KampBasvuruSahibiTipleri.BagliKurulusPersoneli or KampBasvuruSahibiTipleri.BagliKurulusEmeklisi => 15,
            KampBasvuruSahibiTipleri.DigerKamuPersoneli or KampBasvuruSahibiTipleri.DigerKamuEmeklisi => 10,
            _ => 5
        };
}

public sealed record KampKonaklamaKonfigurasyonu(
    string Kod,
    int MinimumKisi,
    int MaksimumKisi,
    decimal KamuGunlukUcret,
    decimal DigerGunlukUcret,
    decimal BuzdolabiGunlukUcret,
    decimal TelevizyonGunlukUcret,
    decimal KlimaGunlukUcret);
