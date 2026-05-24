using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.RestoranSiparisleri.Dtos;

namespace STYS.RestoranYonetimi.Services;

/// <summary>
/// Restoran sipariş verisinden ortak satış belgesi taslağı oluşturma servisi.
/// Restoran modülü doğrudan SatisBelgesi entity'si oluşturmaz;
/// bunun yerine ISatisBelgesiTaslakOlusturmaService üzerinden fatura altyapısına
/// sipariş verisini iletir.
/// </summary>
public interface IRestoranSatisBelgesiService
{
    /// <summary>
    /// Tamamlanmış bir restoran siparişinden satış belgesi taslağı oluşturur.
    /// </summary>
    /// <param name="siparisId">Kaynak restoran sipariş Id'si.</param>
    /// <param name="request">Fatura bilgileri ve KDV override'ları içeren request.</param>
    /// <param name="cancellationToken">İptal token'ı.</param>
    /// <returns>Oluşturulan satış belgesi taslağı DTO'su.</returns>
    Task<SatisBelgesiDto> SatisBelgesiTaslagiOlusturAsync(
        int siparisId,
        RestoranSatisBelgesiTaslakRequest request,
        CancellationToken cancellationToken = default);
}
