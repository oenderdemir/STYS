using STYS.RestoranSiparisleri.Dtos;
using STYS.TicariBelgeler.Dtos;

namespace STYS.RestoranYonetimi.Services;

/// <summary>
/// Restoran sipariş verisinden ortak ticari belge taslağı oluşturma servisi.
/// Restoran modülü doğrudan SatisBelgesi entity'si oluşturmaz VE muhasebe servislerini
/// (ISatisBelgesiTaslakOlusturmaService) doğrudan inject ETMEZ; bunun yerine ITicariBelgeService
/// üzerinden fatura altyapısına sipariş verisini iletir.
/// </summary>
public interface IRestoranSatisBelgesiService
{
    /// <summary>
    /// Tamamlanmış bir restoran siparişinden ticari belge taslağı oluşturur.
    /// </summary>
    /// <param name="siparisId">Kaynak restoran sipariş Id'si.</param>
    /// <param name="request">Fatura bilgileri ve KDV override'ları içeren request.</param>
    /// <param name="cancellationToken">İptal token'ı.</param>
    /// <returns>Oluşturulan ticari belge taslağı DTO'su.</returns>
    Task<TicariBelgeDetayDto> SatisBelgesiTaslagiOlusturAsync(
        int siparisId,
        RestoranSatisBelgesiTaslakRequest request,
        CancellationToken cancellationToken = default);
}
