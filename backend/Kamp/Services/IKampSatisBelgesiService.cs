using STYS.Kamp.Dto;
using STYS.TicariBelgeler.Dtos;

namespace STYS.Kamp.Services;

/// <summary>
/// Kamp rezervasyon verisinden ortak ticari belge taslağı oluşturma servisi arayüzü.
/// Kamp modülü doğrudan SatisBelgesi entity'si oluşturmaz VE muhasebe servislerini
/// (ISatisBelgesiTaslakOlusturmaService) doğrudan inject ETMEZ; bunun yerine ITicariBelgeService
/// üzerinden fatura altyapısına rezervasyon verisini iletir.
/// </summary>
public interface IKampSatisBelgesiService
{
    /// <summary>
    /// Kamp rezervasyonundan ticari belge taslağı oluşturur.
    /// </summary>
    /// <param name="rezervasyonId">Kamp rezervasyon Id'si (route'dan).</param>
    /// <param name="request">Müşteri/belge/KDV bilgilerini içeren request modeli.</param>
    /// <param name="cancellationToken">İptal token'ı.</param>
    /// <returns>Oluşturulan ticari belge DTO'su.</returns>
    Task<TicariBelgeDetayDto> SatisBelgesiTaslagiOlusturAsync(
        int rezervasyonId,
        KampSatisBelgesiTaslakRequest request,
        CancellationToken cancellationToken = default);
}
