using STYS.ErisimTeshis.Dto;

namespace STYS.ErisimTeshis.Services;

public interface IErisimTeshisService
{
    Task<ErisimTeshisReferansDto> GetReferanslarAsync(CancellationToken cancellationToken = default);

    Task<ErisimTeshisSonucDto> TeshisEtAsync(ErisimTeshisIstekDto request, CancellationToken cancellationToken = default);
}
