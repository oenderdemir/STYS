using Microsoft.EntityFrameworkCore;
using STYS.YoneticiAdaylari.Dto;
using TOD.Platform.Identity.Common.Enums;
using TOD.Platform.Identity.Users.Repositories;

namespace STYS.YoneticiAdaylari.Services;

public class YoneticiAdayService : IYoneticiAdayService
{
    private readonly IUserRepository _userRepository;

    public YoneticiAdayService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<List<YoneticiAdayDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository
            .Where(x => x.Status != UserStatus.Blocked)
            .OrderBy(x => x.UserName)
            .Select(x => new YoneticiAdayDto
            {
                Id = x.Id,
                UserName = x.UserName,
                AdSoyad = string.Join(' ', new[] { x.FirstName, x.LastName }.Where(part => !string.IsNullOrWhiteSpace(part)))
            })
            .ToListAsync(cancellationToken);

        return users;
    }
}
