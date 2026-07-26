using STYS.Muhasebe.Common.Constants;

namespace STYS.Muhasebe.Common.Services;

/// <summary>Bir muhasebe fisinin, kaynak kayit acisindan neden GECERLI SAYILMADIGINI aciklayan
/// kodlar. Yalnizca ID'nin dolu olmasi geçerlilik kaniti DEGILDIR.</summary>
public static class FisGecersizlikNedenKodlari
{
    public const string FisBulunamadi = "FisBulunamadi";
    public const string FisSoftDeleteEdilmis = "FisSoftDeleteEdilmis";
    public const string FisDurumuMaliEtkiOlusturmuyor = "FisDurumuMaliEtkiOlusturmuyor";
    public const string FisFarkliTesiseAit = "FisFarkliTesiseAit";
    public const string FisDonemiUyumsuz = "FisDonemiUyumsuz";
    public const string FisSatirindaBeklenenHesapYok = "FisSatirindaBeklenenHesapYok";
}

/// <summary>
/// Bir muhasebe fisinin DOGRULANMIS (yalnizca ID'den ibaret olmayan) durumu. Sorgu katmani bu
/// projeksiyonu doldurur; saf siniflandirma/degerlendirme bilesenleri yalnizca bunu kullanir.
/// </summary>
/// <param name="FisId">Kaynak kaydin isaret ettigi fis id'si.</param>
/// <param name="Bulundu">Fis satiri veritabaninda GERCEKTEN bulundu mu (soft-delete dahil arandi).</param>
/// <param name="SoftDeleteEdilmis">Fis soft-delete edilmis mi.</param>
/// <param name="Durum">MuhasebeFisDurumlari degeri (bulunduysa).</param>
/// <param name="TesisId">Fisin ait oldugu tesis (bulunduysa).</param>
/// <param name="MaliYil">Fisin mali yili.</param>
/// <param name="Donem">Fisin donemi.</param>
/// <param name="FisTarihi">Fisin tarihi.</param>
/// <param name="BeklenenKasaBankaHesabiEtkilenmisMi">Fis SATIRLARINDA beklenen kasa/banka hesabi
/// gercekten etkilenmis mi (MuhasebeFisSatir.KasaBankaHesapId ile dogrulanir). Beklenen hesap
/// verilmediyse null.</param>
public sealed record DogrulanmisFis(
    int FisId,
    bool Bulundu,
    bool SoftDeleteEdilmis,
    string? Durum,
    int? TesisId,
    int? MaliYil,
    int? Donem,
    DateTime? FisTarihi,
    bool? BeklenenKasaBankaHesabiEtkilenmisMi);

public sealed record FisGecerlilikSonucu(bool GecerliMi, IReadOnlyList<string> NedenKodlari, IReadOnlyList<string> Aciklamalar);

/// <summary>
/// Muhasebe fisi gecerliligini DEGERLENDIREN saf (DB'siz) mantik. Sorgu katmanindan gelen
/// <see cref="DogrulanmisFis"/> uzerinde calisir; boylece hem POS valor siniflandiricisi hem de
/// Odeme Izleme "bakiyeye dahil mi" hesabi AYNI kurallari kullanir.
/// </summary>
public static class MuhasebeFisDogrulama
{
    /// <summary>Fisin mali etki olusturabilecek durumda olup olmadigi. Hizli Mizan/bakiye
    /// hesaplamalarinin kullandigi kuralla AYNI: yalnizca Onayli ve TersKayit fisler bakiyeye
    /// yansir (Taslak henuz yansimaz, Iptal artik yansimaz).</summary>
    public static bool DurumMaliEtkiOlusturuyorMu(string? durum) =>
        durum == MuhasebeFisDurumlari.Onayli || durum == MuhasebeFisDurumlari.TersKayit;

    /// <summary>
    /// Fisin, verilen beklentilere gore gercekten gecerli olup olmadigini degerlendirir.
    /// </summary>
    /// <param name="fis">Sorgu katmanindan gelen dogrulanmis fis bilgisi (null ise fis hic aranmamis demektir).</param>
    /// <param name="beklenenTesisId">Fisin ait olmasi gereken tesis; null ise tesis kontrolu yapilmaz.</param>
    /// <param name="donemBaslangic">Fis tarihinin icinde olmasi beklenen donem araligi baslangici; null ise donem kontrolu yapilmaz.</param>
    /// <param name="donemBitis">Donem araligi bitisi (dahil).</param>
    /// <param name="kasaBankaHesabiKontrolEdilsinMi">Fis satirlarinda beklenen kasa/banka hesabinin
    /// etkilenmis olmasi sarti aranacak mi.</param>
    public static FisGecerlilikSonucu Degerlendir(
        DogrulanmisFis? fis,
        int? beklenenTesisId = null,
        DateTime? donemBaslangic = null,
        DateTime? donemBitis = null,
        bool kasaBankaHesabiKontrolEdilsinMi = false)
    {
        var nedenler = new List<string>();
        var aciklamalar = new List<string>();

        if (fis is null || !fis.Bulundu)
        {
            nedenler.Add(FisGecersizlikNedenKodlari.FisBulunamadi);
            aciklamalar.Add("Kayıt bir muhasebe fişine bağlı görünüyor ancak fiş bulunamadı (silinmiş veya erişilemiyor).");
            return new(false, nedenler, aciklamalar);
        }

        if (fis.SoftDeleteEdilmis)
        {
            nedenler.Add(FisGecersizlikNedenKodlari.FisSoftDeleteEdilmis);
            aciklamalar.Add("Bağlı muhasebe fişi silinmiş (soft-delete); mali etki oluşturmaz.");
        }

        if (!DurumMaliEtkiOlusturuyorMu(fis.Durum))
        {
            nedenler.Add(FisGecersizlikNedenKodlari.FisDurumuMaliEtkiOlusturmuyor);
            aciklamalar.Add($"Bağlı muhasebe fişi '{fis.Durum}' durumunda; bakiyeye yansımaz (yalnızca Onaylı/TersKayit fişler yansır).");
        }

        if (beklenenTesisId.HasValue && fis.TesisId.HasValue && fis.TesisId.Value != beklenenTesisId.Value)
        {
            nedenler.Add(FisGecersizlikNedenKodlari.FisFarkliTesiseAit);
            aciklamalar.Add($"Bağlı muhasebe fişi farklı bir tesise ait (fiş tesisi: {fis.TesisId}, beklenen: {beklenenTesisId}).");
        }

        if (donemBaslangic.HasValue && donemBitis.HasValue && fis.FisTarihi.HasValue
            && (fis.FisTarihi.Value.Date < donemBaslangic.Value.Date || fis.FisTarihi.Value.Date > donemBitis.Value.Date))
        {
            nedenler.Add(FisGecersizlikNedenKodlari.FisDonemiUyumsuz);
            aciklamalar.Add($"Fiş tarihi ({fis.FisTarihi:yyyy-MM-dd}) beklenen muhasebe dönemi aralığının ({donemBaslangic:yyyy-MM-dd} - {donemBitis:yyyy-MM-dd}) dışında.");
        }

        if (kasaBankaHesabiKontrolEdilsinMi && fis.BeklenenKasaBankaHesabiEtkilenmisMi == false)
        {
            nedenler.Add(FisGecersizlikNedenKodlari.FisSatirindaBeklenenHesapYok);
            aciklamalar.Add("Muhasebe fişinin hiçbir satırı beklenen kasa/banka hesabını etkilemiyor; fiş başka bir hesaba işlenmiş olabilir.");
        }

        return new(nedenler.Count == 0, nedenler, aciklamalar);
    }
}
