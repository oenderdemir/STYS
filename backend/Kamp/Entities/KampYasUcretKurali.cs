using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kamp.Entities;

public class KampYasUcretKurali : BaseEntity<int>
{
    public int UcretsizCocukMaxYas { get; set; } = 2;

    public int YarimUcretliCocukMaxYas { get; set; } = 6;

    public decimal YemekOrani { get; set; } = 0.50m;

    public bool AktifMi { get; set; } = true;
}
