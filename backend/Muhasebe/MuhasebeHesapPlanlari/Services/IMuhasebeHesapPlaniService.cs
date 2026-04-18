using STYS.Muhasebe.MuhasebeHesapPlanlari.Dtos;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.MuhasebeHesapPlanlari.Services;

public interface IMuhasebeHesapPlaniService : IBaseRdbmsService<MuhasebeHesapPlaniDto, MuhasebeHesapPlani, int>
{
    Task<List<MuhasebeHesapPlaniDto>> GetTreeAsync(CancellationToken cancellationToken = default);
}
