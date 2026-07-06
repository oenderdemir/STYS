using STYS.Raporlar.GunlukGirisCikis.Dto;

namespace STYS.Raporlar.GunlukGirisCikis.Services;

public interface IGunlukGirisCikisRaporService
{
    Task<GunlukGirisCikisRaporDto> GetRaporAsync(
        int tesisId,
        DateTime tarih,
        string? listeTipi = null,
        CancellationToken cancellationToken = default);
}
