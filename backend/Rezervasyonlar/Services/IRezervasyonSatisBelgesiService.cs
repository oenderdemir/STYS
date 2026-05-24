using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Rezervasyonlar.Dto;

namespace STYS.Rezervasyonlar.Services;

/// <summary>
/// Rezervasyon check-out verisinden ortak satış belgesi taslağı oluşturma servisi.
/// Otel modülü doğrudan SatisBelgesi entity'si oluşturmaz;
/// bunun yerine ISatisBelgesiTaslakOlusturmaService üzerinden fatura altyapısına
/// rezervasyon verisini iletir.
/// </summary>
public interface IRezervasyonSatisBelgesiService
{
    /// <summary>
    /// Belirtilen rezervasyonun check-out verisinden satış belgesi taslağı oluşturur.
    /// </summary>
    /// <param name="rezervasyonId">Rezervasyon Id (route)</param>
    /// <param name="request">Request body (RezervasyonId route ile eşleşmeli)</param>
    /// <param name="cancellationToken">İptal token'ı</param>
    /// <returns>Oluşturulan satış belgesi DTO'su</returns>
    Task<SatisBelgesiDto> SatisBelgesiTaslagiOlusturAsync(
        int rezervasyonId,
        RezervasyonSatisBelgesiTaslakRequest request,
        CancellationToken cancellationToken = default);
}
