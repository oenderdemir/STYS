namespace STYS.Muhasebe.Common.Services;

/// <summary>Ters kayit iliskisinin NEDEN dogrulanamadigini aciklayan kodlar.</summary>
public static class TersKayitIliskisiNedenKodlari
{
    /// <summary>Veri modeli/veri durumu ters kayit ile asil fis arasindaki iliskiyi KANITLAMIYOR.
    /// "Dogrulandi" uretilmez.</summary>
    public const string TersKayitIliskisiDogrulanamadi = "TERS_KAYIT_ILISKISI_DOGRULANAMADI";

    public const string AsilFisIliskisiYok = "AsilFisIliskisiYok";
    public const string FarkliTesisVeyaKurum = "FarkliTesisVeyaKurum";
    public const string TutarUyumsuz = "TutarUyumsuz";
    public const string ParaBirimiUyumsuz = "ParaBirimiUyumsuz";
    public const string BirdenFazlaTersKayit = "BirdenFazlaTersKayit";
}

/// <summary>
/// Ters kayit fisi ile ASIL fis arasindaki iliskinin, sorgu katmaninda toplanmis GERCEK verisi.
/// </summary>
/// <param name="TersKayitFisId">Ters kayit fisinin id'si.</param>
/// <param name="AsilFisId">Kaydin asil (terslenmesi beklenen) fis id'si.</param>
/// <param name="TersKayitIptalEdilenFisId">Ters kayit fisinin <c>IptalEdilenFisId</c> degeri -
/// otoriter iliski budur.</param>
/// <param name="TersKayitTesisId">Ters kayit fisinin tesisi.</param>
/// <param name="AsilFisTesisId">Asil fisin tesisi.</param>
/// <param name="TersKayitToplamBorc">Ters kayit fisinin toplam borcu.</param>
/// <param name="AsilFisToplamBorc">Asil fisin toplam borcu.</param>
/// <param name="AyniAsilFiseBagliTersKayitSayisi">Ayni asil fise isaret eden ters kayit adedi -
/// 1'den buyukse mukerrer ters kayit vardir.</param>
public sealed record TersKayitIliskisi(
    int TersKayitFisId,
    int? AsilFisId,
    int? TersKayitIptalEdilenFisId,
    int? TersKayitTesisId,
    int? AsilFisTesisId,
    decimal? TersKayitToplamBorc,
    decimal? AsilFisToplamBorc,
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

        // 2) Ayni tesis/kurum kapsaminda mi?
        if (iliski.TersKayitTesisId.HasValue && iliski.AsilFisTesisId.HasValue
            && iliski.TersKayitTesisId.Value != iliski.AsilFisTesisId.Value)
        {
            nedenler.Add(TersKayitIliskisiNedenKodlari.FarkliTesisVeyaKurum);
            aciklamalar.Add($"Ters kayıt fişi ({iliski.TersKayitTesisId}) ile asıl fiş ({iliski.AsilFisTesisId}) farklı tesislere ait.");
        }

        // 3) Tutar uyumu - ters kayit asil fisin tutarini karsilamali.
        if (iliski.TersKayitToplamBorc.HasValue && iliski.AsilFisToplamBorc.HasValue
            && Math.Abs(iliski.TersKayitToplamBorc.Value - iliski.AsilFisToplamBorc.Value) > TutarToleransi)
        {
            nedenler.Add(TersKayitIliskisiNedenKodlari.TutarUyumsuz);
            aciklamalar.Add(
                $"Ters kayıt tutarı ({iliski.TersKayitToplamBorc:N2}) asıl fiş tutarıyla ({iliski.AsilFisToplamBorc:N2}) uyuşmuyor.");
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
