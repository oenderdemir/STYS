using STYS.Muhasebe.StokLotlari.Dtos;

namespace STYS.Muhasebe.StokLotlari.Services;

public interface IStokLotSktUyariService
{
    Task<List<StokLotSktUyariDto>> GetSktUyarilariAsync(int tesisId, int? depoId, int? tasinirKartId, bool sadeceRiskli, CancellationToken cancellationToken = default);
}
