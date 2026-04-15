using STYS.Muhasebe.CariKartlar.Dtos;
using STYS.Muhasebe.CariKartlar.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.CariKartlar.Services;

public interface ICariKartService : IBaseRdbmsService<CariKartDto, CariKart, int>
{
    Task<CariBakiyeDto> GetBakiyeAsync(int cariKartId, CancellationToken cancellationToken = default);
}
