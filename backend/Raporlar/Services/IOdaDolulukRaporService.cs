using STYS.Raporlar.Dto;

namespace STYS.Raporlar.Services;

public interface IOdaDolulukRaporService
{
    Task<AylikOdaDolulukRaporDto> GetAylikOdaDolulukRaporuAsync(
        int tesisId,
        int yil,
        int ay,
        bool maskele = false,
        CancellationToken cancellationToken = default);
}
