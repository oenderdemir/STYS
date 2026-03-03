using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Fiyatlandirma.Dto;

public class IndirimKuraliDto : BaseRdbmsDto<int>
{
    public string Kod { get; set; } = string.Empty;

    public string Ad { get; set; } = string.Empty;

    public string IndirimTipi { get; set; } = IndirimTipleri.Yuzde;

    public decimal Deger { get; set; }

    public string KapsamTipi { get; set; } = IndirimKapsamTipleri.Sistem;

    public int? TesisId { get; set; }

    public DateTime BaslangicTarihi { get; set; }

    public DateTime BitisTarihi { get; set; }

    public int Oncelik { get; set; }

    public bool BirlesebilirMi { get; set; } = true;

    public bool AktifMi { get; set; } = true;

    public List<int> MisafirTipiIds { get; set; } = [];

    public List<int> KonaklamaTipiIds { get; set; } = [];
}
