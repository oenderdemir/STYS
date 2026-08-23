using STYS.Muhasebe.StokUyarilari.Dtos;

namespace STYS.Muhasebe.StokUyarilari.Services;

public interface IStokUyariService
{
    Task<List<StokUyariDto>> GetStokUyarilariAsync(int tesisId, int? depoId, int? tasinirKartId, bool sadeceRiskli, CancellationToken cancellationToken = default);
}

