using AutoMapper;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.UserKurums.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace TOD.Platform.Identity.UserKurums.Repositories;

public class UserKurumRepository : BaseRdbmsRepository<UserKurum, Guid>, IUserKurumRepository
{
    public UserKurumRepository(TodIdentityDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}
