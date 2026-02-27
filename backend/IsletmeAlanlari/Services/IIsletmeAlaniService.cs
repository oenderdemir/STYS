using STYS.IsletmeAlanlari.Dto;
using STYS.IsletmeAlanlari.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.IsletmeAlanlari.Services;

public interface IIsletmeAlaniService : IBaseRdbmsService<IsletmeAlaniDto, IsletmeAlani, int>
{
}