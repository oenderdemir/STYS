using System.Collections.Generic;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;

namespace STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;

public static class OdemeYontemleri
{
    public const string Nakit = "Nakit";
    public const string KrediKarti = "KrediKarti";
    public const string HavaleEft = "HavaleEft";
    public const string OdayaEkle = "OdayaEkle";
    public const string Mahsup = "Mahsup";

    public static readonly string[] Hepsi =
    [
        Nakit,
        KrediKarti,
        HavaleEft,
        OdayaEkle,
        Mahsup
    ];

    /// <summary>Nakit/banka/POS hareketi doguran, dolayisiyla KasaBankaHesap secimi zorunlu olan tipler.
    /// OdayaEkle (oda hesabina yansitma) ve Mahsup icin para hareketi olmadigindan hesap istenmez.</summary>
    public static readonly string[] NakitHareketiGerektirenler = [Nakit, KrediKarti, HavaleEft];

    /// <summary>Odeme tipine gore secilebilecek KasaBankaHesap.Tip degerleri (odeme dialogundaki filtreleme
    /// ve sunucu tarafi dogrulama icin ortak kaynak).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> UygunKasaBankaHesapTipleri = new Dictionary<string, string[]>
    {
        [Nakit] = [KasaBankaHesapTipleri.NakitKasa],
        [KrediKarti] = [KasaBankaHesapTipleri.KrediKarti],
        [HavaleEft] = [KasaBankaHesapTipleri.Banka, KasaBankaHesapTipleri.DovizHesabi]
    };
}
