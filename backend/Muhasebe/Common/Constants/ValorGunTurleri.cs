namespace STYS.Muhasebe.Common.Constants;

/// <summary>
/// Kredi karti/POS hesaplarinda valor tarihi hesaplama yontemi.
/// </summary>
public static class ValorGunTurleri
{
    public const string TakvimGunu = "TakvimGunu";

    /// <summary>
    /// Yalnizca hafta sonlarini (Cumartesi/Pazar) haric tutar. Resmi tatilleri SU AN dikkate ALMAZ
    /// (resmi tatil takvimi altyapisi projede yok) - bkz. IResmiTatilGunuProvider.
    /// </summary>
    public const string IsGunu = "IsGunu";

    public static readonly string[] Hepsi = [TakvimGunu, IsGunu];
}
