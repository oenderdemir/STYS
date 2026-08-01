using STYS.Rezervasyonlar.Dto;
using STYS.TicariBelgeler.Dtos;

namespace STYS.Rezervasyonlar.Services;

/// <summary>
/// Rezervasyon check-out verisinden ortak ticari belge taslağı oluşturma servisi.
/// Otel modülü doğrudan SatisBelgesi entity'si oluşturmaz VE muhasebe servislerini
/// (ISatisBelgesiTaslakOlusturmaService) doğrudan inject ETMEZ; bunun yerine operasyon uygulama
/// sınırı olan ITicariBelgeService üzerinden fatura altyapısına rezervasyon verisini iletir.
/// </summary>
public interface IRezervasyonSatisBelgesiService
{
    /// <summary>
    /// Belirtilen rezervasyonun check-out verisinden ticari belge taslağı oluşturur.
    /// </summary>
    /// <param name="rezervasyonId">Rezervasyon Id (route)</param>
    /// <param name="request">Request body (RezervasyonId route ile eşleşmeli)</param>
    /// <param name="cancellationToken">İptal token'ı</param>
    /// <returns>Oluşturulan ticari belge DTO'su</returns>
    Task<TicariBelgeDetayDto> SatisBelgesiTaslagiOlusturAsync(
        int rezervasyonId,
        RezervasyonSatisBelgesiTaslakRequest request,
        CancellationToken cancellationToken = default);
}
