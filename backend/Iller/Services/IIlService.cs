using STYS.Iller.Dto;
using STYS.Iller.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Iller.Services;

public interface IIlService : IBaseRdbmsService<IlDto, Il, int>
{
}