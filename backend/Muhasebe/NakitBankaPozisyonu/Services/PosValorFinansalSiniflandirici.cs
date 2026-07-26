using STYS.Muhasebe.NakitBankaPozisyonu.Dtos;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;

namespace STYS.Muhasebe.NakitBankaPozisyonu.Services;

/// <summary>Bir POS valor kaydinin finansal toplamlar acisindan girdigi TEK kategori.</summary>
public enum PosValorKategori
{
    /// <summary>Normal bekleyen - tahmini bakiyeye DAHIL edilebilir (tek kategori budur).</summary>
    NormalBekleyen,

    /// <summary>Rapor tarihi itibariyla bankaya aktarilmis - muhasebe bakiyesi zaten icerir,
    /// bekleyen tutara EKLENMEZ (mukerrer sayim olurdu).</summary>
    Aktarilmis,

    MutabakatBekliyor,
    Hatali,
    IptalEdilmis,
    TersKayitSurecinde,
    AktarimSurecinde,

    /// <summary>Projede tanimli olmayan/yeni eklenmis bir Durum degeri - GUVENLI VARSAYILAN olarak
    /// finansal toplamlarin DISINDA tutulur ve veri kalitesi uyarisi uretir.</summary>
    TaninmayanDurum,

    /// <summary>Durumdan bagimsiz olarak veri kalitesi kontrolunden gecemedi (eksik/tutarsiz veri).</summary>
    VeriKalitesiUyarisi
}

/// <summary>Siniflandiriciya verilen, DB'den bagimsiz saf girdi.</summary>
public sealed record PosValorSiniflandirmaGirdisi(
    string Durum,
    DateOnly BeklenenValorTarihi,
    decimal BrutTutar,
    decimal KomisyonTutari,
    decimal NetTutar,
    string? ValorParaBirimi,
    string? BankaHesabiParaBirimi,
    int? MuhasebeFisId,
    int? TersKayitMuhasebeFisId,
    bool BankaHesabiGecerliMi,
    bool MuhasebeHesabiGecerliMi);

public sealed record PosValorSiniflandirmaSonucu(
    PosValorKategori Kategori,
    string? UyariTipi,
    string? Aciklama)
{
    /// <summary>Yalnizca NormalBekleyen kategorisi tahmini bakiyeye/normal bekleyen toplama girer.
    /// Diger TUM kategoriler (tanimli olsun olmasin) finansal toplamin DISINDADIR.</summary>
    public bool NormalToplamaDahilMi => Kategori == PosValorKategori.NormalBekleyen;
}

/// <summary>
/// POS valor kayitlarinin finansal siniflandirmasini yapan SAF (DB'siz, yan etkisiz) bilesen.
///
/// TASARIM KURALI - ALLOWLIST: yalnizca ACIKCA "normal bekleyen" olarak tanimlanmis tek bir durum
/// (ValorBekliyor) ve tum veri kalitesi kontrollerinden gecen kayitlar finansal toplama girer.
/// Bunun disindaki HER SEY (bilinen diger durumlar, gelecekte eklenebilecek TANINMAYAN durumlar,
/// eksik/tutarsiz verili kayitlar) guvenli varsayilan olarak toplamin DISINDA tutulur. Boylece
/// projeye yeni bir Durum sabiti eklendiginde bu kod guncellenmese bile tutar SESSIZCE bekleyen
/// toplamina sizmaz.
/// </summary>
public static class PosValorFinansalSiniflandirici
{
    public static PosValorSiniflandirmaSonucu Siniflandir(PosValorSiniflandirmaGirdisi girdi)
    {
        // ── 1) Once durum: normal toplama girme IHTIMALI olmayan durumlar hemen ayrilir.
        // (Veri kalitesi kontrolleri yalnizca "aday" kayitlar icin anlamlidir - ornegin zaten
        // iptal edilmis bir kaydin valor tarihinin bos olmasi ayri bir uyari uretmemelidir.)
        switch (girdi.Durum)
        {
            case PosTahsilatValorDurumlari.ValorBekliyor:
                break; // tek aday - asagidaki veri kalitesi kapisina girer.

            case PosTahsilatValorDurumlari.Aktarildi:
                return girdi.MuhasebeFisId.HasValue
                    ? new(PosValorKategori.Aktarilmis, null, null)
                    : new(PosValorKategori.VeriKalitesiUyarisi,
                        NakitBankaPozisyonuUyariTipleri.AktarimDurumuFisIliskisiTutarsiz,
                        "Kayıt 'Aktarıldı' durumunda ancak bağlı bir muhasebe fişi (MuhasebeFisId) bulunamıyor.");

            case PosTahsilatValorDurumlari.MutabakatBekliyor:
                return new(PosValorKategori.MutabakatBekliyor, null, null);

            case PosTahsilatValorDurumlari.Hata:
                return new(PosValorKategori.Hatali, null, null);

            case PosTahsilatValorDurumlari.Iptal:
                return new(PosValorKategori.IptalEdilmis, null, null);

            case PosTahsilatValorDurumlari.AktarimFisiIptalEdildi:
                return girdi.TersKayitMuhasebeFisId.HasValue
                    ? new(PosValorKategori.TersKayitSurecinde, null, null)
                    : new(PosValorKategori.VeriKalitesiUyarisi,
                        NakitBankaPozisyonuUyariTipleri.AktarimDurumuFisIliskisiTutarsiz,
                        "Kayıt 'Aktarım Fişi İptal Edildi' durumunda ancak bağlı bir ters kayıt fişi (TersKayitMuhasebeFisId) bulunamıyor.");

            case PosTahsilatValorDurumlari.TersKayitOlusturuluyor:
                return new(PosValorKategori.TersKayitSurecinde, null, null);

            case PosTahsilatValorDurumlari.Aktariliyor:
                return new(PosValorKategori.AktarimSurecinde, null, null);

            default:
                // GUVENLI VARSAYILAN - tanimadigimiz bir durum ASLA bekleyen toplama girmez.
                return new(PosValorKategori.TaninmayanDurum,
                    NakitBankaPozisyonuUyariTipleri.TaninmayanValorDurumu,
                    $"Kaydın durumu ('{girdi.Durum}') bu ekran tarafından tanınmıyor; güvenli davranış olarak finansal toplamlara dahil edilmedi.");
        }

        // ── 2) Yalnizca ValorBekliyor adaylari icin veri kalitesi kapisi.
        // Bu kontrollerden HERHANGI BIRI basarisizsa kayit normal toplama GIRMEZ.

        if (!girdi.BankaHesabiGecerliMi)
        {
            return new(PosValorKategori.VeriKalitesiUyarisi,
                NakitBankaPozisyonuUyariTipleri.BankaHesabiBulunamadiVeyaPasif,
                "POS valör kaydının bağlı olduğu banka hesabı bulunamadı, pasif veya silinmiş.");
        }

        if (!girdi.MuhasebeHesabiGecerliMi)
        {
            return new(PosValorKategori.VeriKalitesiUyarisi,
                NakitBankaPozisyonuUyariTipleri.BankaHesabininMuhasebeBaglantisiGecersiz,
                "Bağlı banka hesabının geçerli (mevcut, aktif ve silinmemiş) bir muhasebe hesabı bağlantısı yok; muhasebe bakiyesi hesaplanamadığı için tahmini bakiye üretilmedi.");
        }

        if (girdi.MuhasebeFisId.HasValue)
        {
            return new(PosValorKategori.VeriKalitesiUyarisi,
                NakitBankaPozisyonuUyariTipleri.AktarimDurumuFisIliskisiTutarsiz,
                $"Kayıt hâlâ 'Valör Bekliyor' durumunda olduğu hâlde bir muhasebe fişine (Id={girdi.MuhasebeFisId}) bağlı görünüyor.");
        }

        if (girdi.TersKayitMuhasebeFisId.HasValue)
        {
            return new(PosValorKategori.VeriKalitesiUyarisi,
                NakitBankaPozisyonuUyariTipleri.AktarimDurumuFisIliskisiTutarsiz,
                "Kayıt 'Valör Bekliyor' durumunda olduğu hâlde bir ters kayıt fişine bağlı görünüyor; bu kayıt normal bekleyen tutara dahil edilemez.");
        }

        if (girdi.BeklenenValorTarihi == default)
        {
            return new(PosValorKategori.VeriKalitesiUyarisi,
                NakitBankaPozisyonuUyariTipleri.ValorTarihiBos,
                "Beklenen valör tarihi tanımlı değil.");
        }

        if (string.IsNullOrWhiteSpace(girdi.ValorParaBirimi)
            || string.IsNullOrWhiteSpace(girdi.BankaHesabiParaBirimi)
            || !string.Equals(girdi.ValorParaBirimi, girdi.BankaHesabiParaBirimi, StringComparison.OrdinalIgnoreCase))
        {
            return new(PosValorKategori.VeriKalitesiUyarisi,
                NakitBankaPozisyonuUyariTipleri.ParaBirimiUyusmuyor,
                $"Kaydın para birimi ('{girdi.ValorParaBirimi}') bağlı banka hesabının para biriminden ('{girdi.BankaHesabiParaBirimi}') farklı veya tanımsız; kur dönüşümü yapılmadan toplanamaz.");
        }

        // Net tutar iliskisi: NetTutar = BrutTutar - KomisyonTutari. Para tutarlari 2 haneye
        // yuvarlandigi icin 0.01'lik bir tolerans birakilir (mevcut ParaTutarYuvarlamaHelper
        // hassasiyetiyle tutarli).
        if (Math.Abs(girdi.BrutTutar - girdi.KomisyonTutari - girdi.NetTutar) > 0.01m)
        {
            return new(PosValorKategori.VeriKalitesiUyarisi,
                NakitBankaPozisyonuUyariTipleri.NetVeyaKomisyonBilgisiEksik,
                $"Net tutar tutarsız: brüt ({girdi.BrutTutar:N2}) - komisyon ({girdi.KomisyonTutari:N2}) ≠ net ({girdi.NetTutar:N2}).");
        }

        if (girdi.NetTutar <= 0m)
        {
            return new(PosValorKategori.VeriKalitesiUyarisi,
                NakitBankaPozisyonuUyariTipleri.NetVeyaKomisyonBilgisiEksik,
                $"Net tutar sıfır veya negatif ({girdi.NetTutar:N2}); bekleyen tahsilat tutarı olarak toplama dahil edilmedi.");
        }

        return new(PosValorKategori.NormalBekleyen, null, null);
    }
}
