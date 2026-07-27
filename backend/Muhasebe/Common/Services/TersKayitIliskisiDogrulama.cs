namespace STYS.Muhasebe.Common.Services;

/// <summary>Ters kayit iliskisinin NEDEN dogrulanamadigini aciklayan kodlar.</summary>
public static class TersKayitIliskisiNedenKodlari
{
    /// <summary>Veri modeli/veri durumu ters kayit ile asil fis arasindaki iliskiyi KANITLAMIYOR.
    /// "Dogrulandi" uretilmez.</summary>
    public const string TersKayitIliskisiDogrulanamadi = "TERS_KAYIT_ILISKISI_DOGRULANAMADI";

    /// <summary>Asil fis ID'si BILINMIYOR (null) - ters fiste herhangi bir IptalEdilenFisId bulunmasi
    /// TEK BASINA GECERLI ILISKI SAYILMAZ; hangi fisi terslediginin dogrulanmasi icin karsilastirilacak
    /// bir asil fis kimligi gerekir.</summary>
    public const string AsilFisBilinmiyor = "ASIL_FIS_BILINMIYOR";

    public const string AsilFisIliskisiYok = "AsilFisIliskisiYok";
    public const string TersKayitTesisUyusmazligi = "TERS_KAYIT_TESIS_UYUSMAZLIGI";
    public const string TersKayitKurumUyusmazligi = "TERS_KAYIT_KURUM_UYUSMAZLIGI";
    public const string TersKayitTutarUyusmazligi = "TERS_KAYIT_TUTAR_UYUSMAZLIGI";
    public const string TersKayitParaBirimiUyusmazligi = "TERS_KAYIT_PARA_BIRIMI_UYUSMAZLIGI";
    public const string TersYonluHesapEtkisiDogrulanamadi = "TERS_YONLU_HESAP_ETKISI_DOGRULANAMADI";
    public const string BirdenFazlaTersKayit = "BirdenFazlaTersKayit";

    // Geriye donuk uyumluluk icin eski isimler (kod tabaninda hala referans edilebilir).
    public const string FarkliTesisVeyaKurum = TersKayitTesisUyusmazligi;
    public const string TutarUyumsuz = TersKayitTutarUyusmazligi;
    public const string ParaBirimiUyumsuz = TersKayitParaBirimiUyusmazligi;
}

/// <summary>
/// Ters kayit fisi ile ASIL fis arasindaki iliskinin, sorgu katmaninda toplanmis GERCEK verisi.
/// </summary>
/// <param name="TersKayitFisId">Ters kayit fisinin id'si.</param>
/// <param name="AsilFisId">Kaydin asil (terslenmesi beklenen) fis id'si - BILINMIYORSA (null) iliski
/// KANITLANAMAZ, IptalEdilenFisId'nin doluluğu TEK BASINA yeterli sayilmaz.</param>
/// <param name="TersKayitIptalEdilenFisId">Ters kayit fisinin <c>IptalEdilenFisId</c> degeri -
/// otoriter iliski budur.</param>
/// <param name="TersKayitTesisId">Ters kayit fisinin tesisi.</param>
/// <param name="AsilFisTesisId">Asil fisin tesisi.</param>
/// <param name="TersKayitKurumId">Ters kayit fisinin (tesisi uzerinden) kurumu.</param>
/// <param name="AsilFisKurumId">Asil fisin (tesisi uzerinden) kurumu.</param>
/// <param name="TersKayitToplamBorc">Ters kayit fisinin toplam borcu.</param>
/// <param name="AsilFisToplamBorc">Asil fisin toplam borcu.</param>
/// <param name="TersKayitParaBirimi">Ters kayit fis satirlarinin (temsili) para birimi.</param>
/// <param name="AsilFisParaBirimi">Asil fis satirlarinin (temsili) para birimi.</param>
/// <param name="TersYonluHesapEtkisiUyumluMu">Asil fiste etkilenen HER hesabin, ters kayitta TAM
/// TERS yonde (borc/alacak yer degistirerek) etkilendigi fiş satiri seviyesinde DOGRULANABILDIYSE
/// true/false; bu kontrol YAPILAMADIYSA (veri toplanamadi) null. null VEYA false, iliskiyi
/// KANITLANMIŞ SAYDIRMAZ.</param>
/// <param name="AyniAsilFiseBagliTersKayitSayisi">Ayni asil fise isaret eden ters kayit adedi -
/// 1'den buyukse mukerrer ters kayit vardir.</param>
public sealed record TersKayitIliskisi(
    int TersKayitFisId,
    int? AsilFisId,
    int? TersKayitIptalEdilenFisId,
    int? TersKayitTesisId,
    int? AsilFisTesisId,
    int? TersKayitKurumId,
    int? AsilFisKurumId,
    decimal? TersKayitToplamBorc,
    decimal? AsilFisToplamBorc,
    string? TersKayitParaBirimi,
    string? AsilFisParaBirimi,
    bool? TersYonluHesapEtkisiUyumluMu,
    int AyniAsilFiseBagliTersKayitSayisi);

public sealed record TersKayitDogrulamaSonucu(bool DogrulandiMi, IReadOnlyList<string> NedenKodlari, IReadOnlyList<string> Aciklamalar);

/// <summary>
/// Ters kayit fisinin ASIL fisi GERCEKTEN terslediğini degerlendiren saf (DB'siz) mantik.
///
/// Yalnizca <c>TersKayitMuhasebeFisId</c> alaninin dolu olmasi KANIT DEGILDIR. Otoriter iliski
/// ters kayit fisinin <c>IptalEdilenFisId</c> degeridir (bkz. MuhasebeFis entity'si ve
/// IX_MuhasebeFisler_IptalEdilenFisId unique index'i). Veri bu iliskiyi kanitlamaya yetmiyorsa
/// "dogrulandi" URETILMEZ.
/// </summary>
public static class TersKayitIliskisiDogrulama
{
    /// <summary>Tutar karsilastirmasinda kabul edilen yuvarlama toleransi.</summary>
    private const decimal TutarToleransi = 0.01m;

    public static TersKayitDogrulamaSonucu Degerlendir(TersKayitIliskisi? iliski)
    {
        var nedenler = new List<string>();
        var aciklamalar = new List<string>();

        if (iliski is null)
        {
            nedenler.Add(TersKayitIliskisiNedenKodlari.TersKayitIliskisiDogrulanamadi);
            aciklamalar.Add("Ters kayıt ilişkisi hakkında veri toplanamadı; ilişki doğrulanamadı.");
            return new(false, nedenler, aciklamalar);
        }

        // 0) Asil fis ID'si bilinmiyorsa, ters fiste IptalEdilenFisId dolu olsa BILE hangi fisi
        // terslediği (dogrulamaya konu olan asil fisle eslestigi) KANITLANAMAZ.
        if (!iliski.AsilFisId.HasValue)
        {
            nedenler.Add(TersKayitIliskisiNedenKodlari.AsilFisBilinmiyor);
            aciklamalar.Add("Doğrulanacak asıl fişin kimliği bilinmiyor; ters kayıt fişinde bir bağlantı bulunması TEK BAŞINA geçerli ilişki sayılmaz.");
        }

        // 1) Otoriter iliski: ters kayit fisi ASIL fisi isaret ediyor mu?
        if (!iliski.TersKayitIptalEdilenFisId.HasValue)
        {
            nedenler.Add(TersKayitIliskisiNedenKodlari.AsilFisIliskisiYok);
            aciklamalar.Add("Ters kayıt fişinde iptal edilen fiş bağlantısı (IptalEdilenFisId) yok; hangi fişi terslediği kanıtlanamıyor.");
        }
        else if (iliski.AsilFisId.HasValue && iliski.TersKayitIptalEdilenFisId.Value != iliski.AsilFisId.Value)
        {
            nedenler.Add(TersKayitIliskisiNedenKodlari.AsilFisIliskisiYok);
            aciklamalar.Add(
                $"Ters kayıt fişi başka bir fişi ({iliski.TersKayitIptalEdilenFisId}) tersliyor; beklenen asıl fiş ({iliski.AsilFisId}) değil.");
        }

        // 2) Ayni tesis kapsaminda mi?
        if (iliski.TersKayitTesisId.HasValue && iliski.AsilFisTesisId.HasValue
            && iliski.TersKayitTesisId.Value != iliski.AsilFisTesisId.Value)
        {
            nedenler.Add(TersKayitIliskisiNedenKodlari.TersKayitTesisUyusmazligi);
            aciklamalar.Add($"Ters kayıt fişi ({iliski.TersKayitTesisId}) ile asıl fiş ({iliski.AsilFisTesisId}) farklı tesislere ait.");
        }

        // 2b) Ayni kurum kapsaminda mi?
        if (iliski.TersKayitKurumId.HasValue && iliski.AsilFisKurumId.HasValue
            && iliski.TersKayitKurumId.Value != iliski.AsilFisKurumId.Value)
        {
            nedenler.Add(TersKayitIliskisiNedenKodlari.TersKayitKurumUyusmazligi);
            aciklamalar.Add($"Ters kayıt fişi ({iliski.TersKayitKurumId}) ile asıl fiş ({iliski.AsilFisKurumId}) farklı kurumlara ait.");
        }

        // 3) Tutar uyumu - ters kayit asil fisin tutarini karsilamali.
        if (iliski.TersKayitToplamBorc.HasValue && iliski.AsilFisToplamBorc.HasValue
            && Math.Abs(iliski.TersKayitToplamBorc.Value - iliski.AsilFisToplamBorc.Value) > TutarToleransi)
        {
            nedenler.Add(TersKayitIliskisiNedenKodlari.TersKayitTutarUyusmazligi);
            aciklamalar.Add(
                $"Ters kayıt tutarı ({iliski.TersKayitToplamBorc:N2}) asıl fiş tutarıyla ({iliski.AsilFisToplamBorc:N2}) uyuşmuyor.");
        }

        // 3b) Para birimi uyumu.
        if (!string.IsNullOrWhiteSpace(iliski.TersKayitParaBirimi) && !string.IsNullOrWhiteSpace(iliski.AsilFisParaBirimi)
            && !string.Equals(iliski.TersKayitParaBirimi, iliski.AsilFisParaBirimi, StringComparison.OrdinalIgnoreCase))
        {
            nedenler.Add(TersKayitIliskisiNedenKodlari.TersKayitParaBirimiUyusmazligi);
            aciklamalar.Add($"Ters kayıt para birimi ({iliski.TersKayitParaBirimi}) asıl fişin para birimiyle ({iliski.AsilFisParaBirimi}) uyuşmuyor.");
        }

        // 3c) Ters yonlu hesap etkisi - dogrulanamadiysa (null) VEYA uyumsuzsa (false) "dogrulandi" URETILMEZ.
        if (iliski.TersYonluHesapEtkisiUyumluMu != true)
        {
            nedenler.Add(TersKayitIliskisiNedenKodlari.TersYonluHesapEtkisiDogrulanamadi);
            aciklamalar.Add(iliski.TersYonluHesapEtkisiUyumluMu is null
                ? "Asıl fişte etkilenen hesapların ters kayıtta gerçekten ters yönde etkilenip etkilenmediği doğrulanamadı."
                : "Asıl fişte etkilenen hesaplardan en az biri ters kayıtta ters yönde (borç/alacak yer değiştirerek) etkilenmemiş.");
        }

        // 4) Ayni asil fise birden fazla ters kayit uygulanmis mi (mukerrer terslenme)?
        if (iliski.AyniAsilFiseBagliTersKayitSayisi > 1)
        {
            nedenler.Add(TersKayitIliskisiNedenKodlari.BirdenFazlaTersKayit);
            aciklamalar.Add($"Aynı asıl fişe {iliski.AyniAsilFiseBagliTersKayitSayisi} adet ters kayıt bağlı; mükerrer terslenme olabilir.");
        }

        if (nedenler.Count > 0)
        {
            // Herhangi bir sorun varsa GENEL "dogrulanamadi" kodu da eklenir - cagiran taraf tek bir
            // kodla da karar verebilsin.
            nedenler.Insert(0, TersKayitIliskisiNedenKodlari.TersKayitIliskisiDogrulanamadi);
            return new(false, nedenler, aciklamalar);
        }

        return new(true, nedenler, aciklamalar);
    }
}
