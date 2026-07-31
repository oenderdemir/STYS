namespace STYS.Muhasebe.SatisBelgeleri.Enums;

/// <summary>
/// Ticari belgenin RESMÎ FATURA KESİMİ ve müşteriye/tedarikçiye GÖNDERİM sürecindeki durumu -
/// ticari belgenin hazırlık (bkz. TicariBelgeDurumu) ve muhasebeleştirme (bkz.
/// TicariBelgeMuhasebeDurumu) süreçlerinden BAĞIMSIZDIR. Yalnızca STYS tarafından düzenlenen giden
/// belgeler (SatisFaturasi, AlisIadeFaturasi - bkz. SatisBelgesiTipiExtensions.
/// StysTarafindanDuzenlenirMi) için "uygulanabilir" bir süreçtir; diğer tüm belge tiplerinde
/// (AlisFaturasi, SatisIadeFaturasi, FaturaTaslagi, Proforma, legacy IadeFaturasi) daima
/// Uygulanamaz'dır.
///
/// E-Belge işlemi, entegratör durumu, gönderim hatası veya yeniden deneme (retry) gibi gelecekteki
/// sağlayıcı durumları BİLİNÇLİ OLARAK bu enuma eklenmemiştir - bu, bu turun kapsamı dışındadır.
///
/// Bu enum, mevcut (otoriter) SatisBelgesiDurumu'nun yerini henüz ALMAZ - SatisBelgesiService
/// tarafından SatisBelgesiDurumProjection üzerinden salt türetilerek yazılan, henüz OKUNMAYAN/karar
/// vermede kullanılmayan bir projeksiyon alanıdır (bkz. SatisBelgesi.FaturalamaDurumu).
/// </summary>
public enum TicariBelgeFaturalamaDurumu
{
    Uygulanamaz = 1,
    Baslatilmadi = 2,
    KesimBekliyor = 3,
    Kesildi = 4,
    MusteriyeGonderildi = 5,
    IptalEdildi = 6
}
