namespace STYS.Muhasebe.Kdv.Enums;

/// <summary>
/// KDV işlem yönü: Satış veya Alış.
/// Stok hareket tarafında HareketTipi'nden türetilir:
///   Giris → Alis, Cikis → Satis
/// </summary>
public enum KdvIslemYonu
{
    Satis = 1,
    Alis = 2
}
