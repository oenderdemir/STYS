using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Restoranlar.Dtos;

public class RestoranDto : BaseRdbmsDto<int>
{
    public int TesisId { get; set; }
    public int? IsletmeAlaniId { get; set; }
    public string? IsletmeAlaniAdi { get; set; }
    public ICollection<Guid>? YoneticiUserIds { get; set; }
    public ICollection<Guid>? GarsonUserIds { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public bool AktifMi { get; set; } = true;
}

public class RestoranIsletmeAlaniSecenekDto
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
}

public class CreateRestoranRequest
{
    [Required]
    public int TesisId { get; set; }
    public int? IsletmeAlaniId { get; set; }
    public ICollection<Guid>? YoneticiUserIds { get; set; }
    public ICollection<Guid>? GarsonUserIds { get; set; }

    [Required]
    public string Ad { get; set; } = string.Empty;

    public string? Aciklama { get; set; }

    public bool AktifMi { get; set; } = true;
}

public class UpdateRestoranRequest
{
    [Required]
    public int TesisId { get; set; }
    public int? IsletmeAlaniId { get; set; }
    public ICollection<Guid>? YoneticiUserIds { get; set; }
    public ICollection<Guid>? GarsonUserIds { get; set; }

    [Required]
    public string Ad { get; set; } = string.Empty;

    public string? Aciklama { get; set; }

    public bool AktifMi { get; set; } = true;
}
