using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Countries.Entities;

public class Country : BaseEntity<Guid>
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Code { get; set; } = string.Empty;
}
