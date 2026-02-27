using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Countries.Dto;

public class CountryDto : BaseRdbmsDto<Guid>
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Code { get; set; } = string.Empty;
}
