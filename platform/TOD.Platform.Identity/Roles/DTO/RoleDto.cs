using TOD.Platform.Persistence.Rdbms.Dto;

namespace TOD.Platform.Identity.Roles.DTO;

public class RoleDto : BaseRdbmsDto<Guid>
{
    public string? Name { get; set; }

    public string? Domain { get; set; }
}
