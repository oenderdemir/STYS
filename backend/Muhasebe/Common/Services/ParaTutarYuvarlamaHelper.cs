namespace STYS.Muhasebe.Common.Services;

/// <summary>
/// Proje genelinde ayrik bir yuvarlama standardi tanimli olmadigindan (mevcut servislerde
/// carpim/bolum iceren tutar hesaplamasi bulunmuyor), POS komisyon hesaplamasi icin standart
/// ticari/kurus yuvarlamasi burada merkezilestirilir.
/// </summary>
public static class ParaTutarYuvarlamaHelper
{
    public static decimal Yuvarla(decimal tutar) => Math.Round(tutar, 2, MidpointRounding.AwayFromZero);
}
