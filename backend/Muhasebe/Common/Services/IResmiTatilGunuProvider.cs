namespace STYS.Muhasebe.Common.Services;

/// <summary>
/// Resmi tatil takvimi altyapisi icin genisletilebilirlik noktasi. Su an projede resmi tatil
/// takvimi bulunmadigindan varsayilan implementasyon (bkz. NoOpResmiTatilGunuProvider) hep bos
/// donuyor; IsGunu hesaplamasi bu yuzden yalnizca hafta sonlarini atlar.
/// </summary>
public interface IResmiTatilGunuProvider
{
    bool ResmiTatilMi(DateOnly tarih);
}

public sealed class NoOpResmiTatilGunuProvider : IResmiTatilGunuProvider
{
    public bool ResmiTatilMi(DateOnly tarih) => false;
}
