using System.ComponentModel.DataAnnotations;

namespace TOD.Platform.Persistence.Rdbms.Entities;

public abstract class BaseEntity<TKey> where TKey : struct
{
    [Key]
    public TKey Id { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public string? DeletedBy { get; set; }
}

public abstract class BaseEntity : BaseEntity<Guid>
{
}
