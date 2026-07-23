namespace STYS.Muhasebe.PosTahsilatValorleri.Entities;

public static class PosTahsilatValorDurumlari
{
    public const string ValorBekliyor = "ValorBekliyor";

    /// <summary>Komisyon orani belirsiz (KomisyonOrani=null) ve hesap otomatik aktarima acikken
    /// snapshot anindaki durum. Job bu durumu asla secmez; yalnizca manuel aktarim, komisyon/net
    /// bilgisi acikca verilerek bu kaydi ilerletebilir.</summary>
    public const string MutabakatBekliyor = "MutabakatBekliyor";

    public const string Aktariliyor = "Aktariliyor";
    public const string Aktarildi = "Aktarildi";
    public const string Hata = "Hata";
    public const string Iptal = "Iptal";

    /// <summary>Aktarilmis bir kaydin transfer fisi "duzeltme-ters-kayit" ile ters kayitla
    /// iptal edildiginde ulasilan kalici terminal durum. Otomatik/manuel hicbir akis bu
    /// durumdaki kaydi yeniden aktarilabilir hale getirmez.</summary>
    public const string AktarimFisiIptalEdildi = "AktarimFisiIptalEdildi";

    /// <summary>"duzeltme-ters-kayit" isleminin kendi kisa ara-durumu. Normal aktarim claim'i
    /// (HesabaAktarAsync) ve arka plan job'u bu durumu asla gormez/secmez/kurtarmaz - kendi ayri
    /// kurtarma kurali vardir (bkz. PosTahsilatValorAktarimService.DuzeltmeTersKayitAsync).</summary>
    public const string TersKayitOlusturuluyor = "TersKayitOlusturuluyor";

    public static readonly string[] Hepsi =
    [
        ValorBekliyor, MutabakatBekliyor, Aktariliyor, Aktarildi, Hata, Iptal,
        AktarimFisiIptalEdildi, TersKayitOlusturuluyor
    ];
}
