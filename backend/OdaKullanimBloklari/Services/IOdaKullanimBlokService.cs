using STYS.OdaKullanimBloklari.Dto;
using STYS.OdaKullanimBloklari.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.OdaKullanimBloklari.Services;

public interface IOdaKullanimBlokService : IBaseRdbmsService<OdaKullanimBlokDto, OdaKullanimBlok, int>
{
    Task<List<OdaKullanimBlokTesisDto>> GetErisilebilirTesislerAsync(CancellationToken cancellationToken = default);

    Task<List<OdaKullanimBlokOdaSecenekDto>> GetOdaSecenekleriAsync(int tesisId, CancellationToken cancellationToken = default);
}

