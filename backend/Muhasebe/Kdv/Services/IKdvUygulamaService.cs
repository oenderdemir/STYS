using STYS.Muhasebe.Kdv.Enums;

namespace STYS.Muhasebe.Kdv.Services;

/// <summary>
/// StokHareket gibi işlem satırlarında KDV uygulama tipi ve istisna tanımı doğrulama yardımcısı.
/// </summary>
public interface IKdvUygulamaService
{
    /// <summary>
    /// KDV uygulama tipine göre gerekli validasyonları yapar ve snapshot alanlarını doldurur.
    /// </summary>
    /// <param name="kdvUygulamaTipi">KDV uygulama tipi enum değeri.</param>
    /// <param name="kdvIstisnaTanimId">İstisna tanımı ID (KDV'li değilse zorunlu olabilir).</param>
    /// <param name="kdvOrani">KDV oranı (yüzde).</param>
    /// <param name="tutar">Satır tutarı (KDV hariç).</param>
    /// <param name="islemTarihi">İşlem tarihi (istisna geçerlilik kontrolü için).</param>
    /// <param name="islemYonu">İşlem yönü (satış/alış kontrolü için).</param>
    /// <param name="cancellationToken">İptal belirteci.</param>
    /// <returns>Snapshot alanlarıyla birlikte doğrulanmış KDV bilgisi.</returns>
    Task<KdvUygulamaResult> ValidateAndSnapshotAsync(
        int kdvUygulamaTipi,
        int? kdvIstisnaTanimId,
        decimal kdvOrani,
        decimal tutar,
        DateTime islemTarihi,
        KdvIslemYonu islemYonu,
        CancellationToken cancellationToken = default);
}

public class KdvUygulamaResult
{
    public int KdvUygulamaTipi { get; set; }
    public int? KdvIstisnaTanimId { get; set; }
    public string? KdvIstisnaKodu { get; set; }
    public string? KdvIstisnaAciklamasi { get; set; }
    public decimal KdvOrani { get; set; }
    public decimal KdvTutari { get; set; }
}
