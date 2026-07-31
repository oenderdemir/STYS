namespace STYS.Muhasebe.SatisBelgeleri.Enums;

public static class SatisBelgesiTipiExtensions
{
    public static bool IsSatisBelgesi(this SatisBelgesiTipi belgeTipi)
        => belgeTipi is SatisBelgesiTipi.FaturaTaslagi
            or SatisBelgesiTipi.SatisFaturasi
            or SatisBelgesiTipi.SatisIadeFaturasi
            or SatisBelgesiTipi.IadeFaturasi
            or SatisBelgesiTipi.Proforma;

    public static bool IsAlisBelgesi(this SatisBelgesiTipi belgeTipi)
        => belgeTipi is SatisBelgesiTipi.AlisFaturasi
            or SatisBelgesiTipi.AlisIadeFaturasi;

    public static bool IsIadeBelgesi(this SatisBelgesiTipi belgeTipi)
        => belgeTipi is SatisBelgesiTipi.IadeFaturasi
            or SatisBelgesiTipi.SatisIadeFaturasi
            or SatisBelgesiTipi.AlisIadeFaturasi;

    /// <summary>
    /// Belgeyi FİİLEN KİMİN DÜZENLEDİĞİ (STYS mi, karşı taraf mı) sorusuna cevap verir - bu,
    /// <see cref="IsSatisBelgesi"/>/<see cref="IsAlisBelgesi"/>/<see cref="IsIadeBelgesi"/>'nin
    /// belirlediği KDV/cari/stok/muhasebe işlem YÖNÜNDEN (satış mı alış mı) TAMAMEN AYRI bir
    /// kavramdır ve o metotların anlamı/davranışı bu eklemeyle DEĞİŞTİRİLMEZ.
    ///
    /// Otoriter sınıflandırma:
    /// - SatisFaturasi: STYS satıcı konumunda, faturayı STYS düzenler → giden.
    /// - AlisIadeFaturasi: STYS alıcı konumunda olduğu iade işleminde, iade faturasını STYS
    ///   tedarikçiye düzenler → giden.
    /// - AlisFaturasi / SatisIadeFaturasi: karşı taraf düzenler → gelen (bkz.
    ///   <see cref="KarsiTarafTarafindanDuzenlenirMi"/>).
    /// - FaturaTaslagi, Proforma, legacy IadeFaturasi ve bilinmeyen/gelecekte eklenen değerler:
    ///   ne giden ne gelen olarak sınıflandırılır (false) - fail-closed.
    /// </summary>
    public static bool StysTarafindanDuzenlenirMi(this SatisBelgesiTipi belgeTipi)
        => belgeTipi is SatisBelgesiTipi.SatisFaturasi or SatisBelgesiTipi.AlisIadeFaturasi;

    /// <summary>
    /// Belgenin, tedarikçi/müşteri gibi karşı taraf tarafından düzenlenip STYS'ye ulaştırılan
    /// (gelen) bir belge olup olmadığını belirtir. Bkz. <see cref="StysTarafindanDuzenlenirMi"/>.
    /// </summary>
    public static bool KarsiTarafTarafindanDuzenlenirMi(this SatisBelgesiTipi belgeTipi)
        => belgeTipi is SatisBelgesiTipi.AlisFaturasi or SatisBelgesiTipi.SatisIadeFaturasi;

    /// <summary>
    /// Otomatik resmî fatura numarası (SatisBelgesiService.FaturaKesAsync) yalnızca STYS
    /// tarafından düzenlenen giden belgeler için üretilebilir - bkz.
    /// <see cref="StysTarafindanDuzenlenirMi"/>. Bilinmeyen/gelecekte eklenen bir enum değeri
    /// de bu metot false döndürerek fail-closed davranır; sessizce SatisFaturasi VARSAYILMAZ.
    /// </summary>
    public static bool OtomatikResmiNumaraUretilebilirMi(this SatisBelgesiTipi belgeTipi)
        => belgeTipi.StysTarafindanDuzenlenirMi();
}
