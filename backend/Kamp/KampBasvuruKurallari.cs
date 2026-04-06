using STYS.Tesisler.Entities;

namespace STYS.Kamp;

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
    public static readonly DateTime UcretsizCocukSiniri = new(2023, 1, 1);
    public static readonly DateTime YarimUcretliCocukSiniri = new(2020, 1, 1);
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
