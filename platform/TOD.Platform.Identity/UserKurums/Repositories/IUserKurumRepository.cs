using TOD.Platform.Identity.UserKurums.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace TOD.Platform.Identity.UserKurums.Repositories;

public interface IUserKurumRepository : IBaseRdbmsRepository<UserKurum, Guid>
{
}
