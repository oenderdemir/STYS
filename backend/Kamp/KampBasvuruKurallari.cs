using STYS.Kamp.Services;

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
    public const string ParametrePrefix = "Konaklama.";
    public const string AlanAd = "Ad";
    public const string AlanMinKisi = "MinKisi";
    public const string AlanMaksKisi = "MaksKisi";
    public const string AlanKamuGunluk = "KamuGunluk";
    public const string AlanDigerGunluk = "DigerGunluk";
    public const string AlanBuzdolabiGunluk = "BuzdolabiGunluk";
    public const string AlanTelevizyonGunluk = "TelevizyonGunluk";
    public const string AlanKlimaGunluk = "KlimaGunluk";

    public static readonly string[] ZorunluAlanlar =
    [
        AlanMinKisi,
        AlanMaksKisi,
        AlanKamuGunluk,
        AlanDigerGunluk,
        AlanBuzdolabiGunluk,
        AlanTelevizyonGunluk,
        AlanKlimaGunluk
    ];

    public static string BuildParametreKodu(string birimKodu, string alan)
        => $"{ParametrePrefix}{birimKodu}.{alan}";
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
    public const int UcretsizCocukMaxYas = 2;
    public const int YarimUcretliCocukMaxYas = 6;
    public const decimal KamuAvansKisiBasi = 1700m;
    public const decimal DigerAvansKisiBasi = 2550m;
    public const decimal YemekOrani = 0.50m;

    public static KampKonaklamaKonfigurasyonu ResolveKonaklama(IKampParametreService parametreService, string konaklamaBirimiTipi)
    {
        if (string.IsNullOrWhiteSpace(konaklamaBirimiTipi))
        {
            throw new InvalidOperationException("Konaklama birimi tipi zorunludur.");
        }

        var birimKodu = konaklamaBirimiTipi.Trim();
        var eksikAlanlar = KampKonaklamaBirimiTipleri.ZorunluAlanlar
            .Where(alan => parametreService.GetString(KampKonaklamaBirimiTipleri.BuildParametreKodu(birimKodu, alan)) is null)
            .ToList();

        if (eksikAlanlar.Count > 0)
        {
            throw new InvalidOperationException($"Konaklama birimi tipi icin eksik parametre tanimi: {birimKodu} ({string.Join(", ", eksikAlanlar)})");
        }

        var minimumKisi = parametreService.GetInt(
            KampKonaklamaBirimiTipleri.BuildParametreKodu(birimKodu, KampKonaklamaBirimiTipleri.AlanMinKisi),
            -1);
        var maksimumKisi = parametreService.GetInt(
            KampKonaklamaBirimiTipleri.BuildParametreKodu(birimKodu, KampKonaklamaBirimiTipleri.AlanMaksKisi),
            -1);

        if (minimumKisi <= 0 || maksimumKisi <= 0 || minimumKisi > maksimumKisi)
        {
            throw new InvalidOperationException($"Konaklama birimi tipi icin gecersiz kapasite araligi: {birimKodu} ({minimumKisi}-{maksimumKisi}).");
        }

        return new KampKonaklamaKonfigurasyonu(
            birimKodu,
            minimumKisi,
            maksimumKisi,
            parametreService.GetDecimal(KampKonaklamaBirimiTipleri.BuildParametreKodu(birimKodu, KampKonaklamaBirimiTipleri.AlanKamuGunluk)),
            parametreService.GetDecimal(KampKonaklamaBirimiTipleri.BuildParametreKodu(birimKodu, KampKonaklamaBirimiTipleri.AlanDigerGunluk)),
            parametreService.GetDecimal(KampKonaklamaBirimiTipleri.BuildParametreKodu(birimKodu, KampKonaklamaBirimiTipleri.AlanBuzdolabiGunluk)),
            parametreService.GetDecimal(KampKonaklamaBirimiTipleri.BuildParametreKodu(birimKodu, KampKonaklamaBirimiTipleri.AlanTelevizyonGunluk)),
            parametreService.GetDecimal(KampKonaklamaBirimiTipleri.BuildParametreKodu(birimKodu, KampKonaklamaBirimiTipleri.AlanKlimaGunluk)));
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
