namespace STYS.Muhasebe.SatisBelgeleri.Enums;

/// <summary>
/// Ticari belgenin MUHASEBELEŞTİRME sürecindeki durumu - ticari belgenin hazırlık (bkz.
/// TicariBelgeDurumu) ve faturalama/gönderim (bkz. TicariBelgeFaturalamaDurumu) süreçlerinden
/// BAĞIMSIZDIR.
///
/// Bu enum, mevcut (otoriter) SatisBelgesiDurumu'nun yerini henüz ALMAZ - SatisBelgesiService
/// tarafından SatisBelgesiDurumProjection üzerinden salt türetilerek yazılan, henüz OKUNMAYAN/karar
/// vermede kullanılmayan bir projeksiyon alanıdır (bkz. SatisBelgesi.MuhasebeDurumu).
/// </summary>
public enum TicariBelgeMuhasebeDurumu
{
    Bekliyor = 1,
    Onayda = 2,
    Onaylandi = 3,
    Reddedildi = 4,
    IptalEdildi = 5
}
