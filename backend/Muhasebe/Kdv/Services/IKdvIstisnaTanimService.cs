using STYS.Muhasebe.Kdv.Dtos;
using STYS.Muhasebe.Kdv.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.Kdv.Services;

public interface IKdvIstisnaTanimService : IBaseRdbmsService<KdvIstisnaTanimDto, KdvIstisnaTanim, int>
{
}
