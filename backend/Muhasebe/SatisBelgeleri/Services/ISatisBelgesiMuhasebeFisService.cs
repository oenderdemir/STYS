using STYS.Muhasebe.SatisBelgeleri.Dtos;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// Satış belgesinden muhasebe fişi oluşturma servisi.
/// Bu servis doğrudan controller'a bağlanmaz; endpoint Faz 65D'de eklenecektir.
/// </summary>
public interface ISatisBelgesiMuhasebeFisService
{
    /// <summary>
    /// MuhasebeOnaylandi durumundaki satış belgesinden 120 / 600 / 391 hesap kurgusuyla
    /// muhasebe fişi taslağı oluşturur.
    /// </summary>
    /// <param name="satisBelgesiId">Satış belgesi ID</param>
    /// <param name="cancellationToken">İptal token'ı</param>
    /// <returns>MuhasebeFisId ve MuhasebeFisOlusturmaTarihi doldurulmuş SatisBelgesiDto</returns>
    Task<SatisBelgesiDto> MuhasebeFisiOlusturAsync(
        int satisBelgesiId,
        CancellationToken cancellationToken = default);
}
