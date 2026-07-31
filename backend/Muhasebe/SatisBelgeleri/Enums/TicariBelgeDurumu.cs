namespace STYS.Muhasebe.SatisBelgeleri.Enums;

/// <summary>
/// Ticari belgenin (fatura/iade/proforma vb.) HAZIRLIK sürecindeki durumu - muhasebeleştirme ve
/// faturalama/gönderim süreçlerinden BAĞIMSIZDIR (bkz. TicariBelgeMuhasebeDurumu,
/// TicariBelgeFaturalamaDurumu). Muhasebe reddi burada bir "Reddedildi" değeri OLUŞTURMAZ -
/// ticari belgenin kendisi, muhasebe onayı reddedilse dahi hâlâ "Hazir" (düzenlenmiş/tamamlanmış)
/// kabul edilir; ret yalnızca TicariBelgeMuhasebeDurumu.Reddedildi ile ifade edilir.
///
/// Bu enum, mevcut (otoriter) SatisBelgesiDurumu'nun yerini henüz ALMAZ - SatisBelgesiService
/// tarafından SatisBelgesiDurumProjection üzerinden salt türetilerek yazılan, henüz OKUNMAYAN/karar
/// vermede kullanılmayan bir projeksiyon alanıdır (bkz. SatisBelgesi.TicariDurum).
/// </summary>
public enum TicariBelgeDurumu
{
    Taslak = 1,
    Hazir = 2,
    IptalEdildi = 3
}
