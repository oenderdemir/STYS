using STYS.Kamp.Dto;
using STYS.Muhasebe.SatisBelgeleri.Dtos;

namespace STYS.Kamp.Services;

/// <summary>
/// Kamp rezervasyon verisinden ortak satış belgesi taslağı oluşturma servisi arayüzü.
/// Kamp modülü doğrudan SatisBelgesi entity'si oluşturmaz;
/// bunun yerine ISatisBelgesiTaslakOlusturmaService üzerinden fatura altyapısına
/// rezervasyon verisini iletir.
/// </summary>
public interface IKampSatisBelgesiService
{
    /// <summary>
    /// Kamp rezervasyonundan satış belgesi taslağı oluşturur.
    /// </summary>
    /// <param name="rezervasyonId">Kamp rezervasyon Id'si (route'dan).</param>
    /// <param name="request">Müşteri/belge/KDV bilgilerini içeren request modeli.</param>
    /// <param name="cancellationToken">İptal token'ı.</param>
    /// <returns>Oluşturulan satış belgesi DTO'su.</returns>
    Task<SatisBelgesiDto> SatisBelgesiTaslagiOlusturAsync(
        int rezervasyonId,
        KampSatisBelgesiTaslakRequest request,
        CancellationToken cancellationToken = default);
}
