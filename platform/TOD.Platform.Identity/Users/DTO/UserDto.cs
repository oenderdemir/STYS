using TOD.Platform.Identity.UserGroups.DTO;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace TOD.Platform.Identity.Users.DTO;

public class UserDto : BaseRdbmsDto<Guid>
{
    public string UserName { get; set; } = string.Empty;

    public string? NationalId { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Email { get; set; }

    public string? AvatarPath { get; set; }

    public string Status { get; set; } = string.Empty;

    public List<UserGroupDto> UserGroups { get; set; } = new();
}
