namespace STYS.Raporlar.Dto;

public class OdaDolulukHucreDto
{
    public int OdaId { get; set; }

    public string OdaNo { get; set; } = "";

    public bool DoluMu { get; set; }

    public int? RezervasyonId { get; set; }

    public string? ReferansNo { get; set; }

    public string? MisafirAdiSoyadi { get; set; }

    public string? KurumUnite { get; set; }

    public int KisiSayisi { get; set; }

    public DateTime? GirisTarihi { get; set; }

    public DateTime? CikisTarihi { get; set; }

    public string? RezervasyonDurumu { get; set; }

    public decimal ToplamUcret { get; set; }

    public decimal OdenenTutar { get; set; }

    public decimal KalanTutar { get; set; }

    public string? ParaBirimi { get; set; }

    public bool OdemesiEksikMi { get; set; }

    public bool OdaDegisimiGerekliMi { get; set; }

    public string? HucreRenkKodu { get; set; }

    public string? TutarAciklamasi { get; set; }

    public bool CakismaVarMi { get; set; }

    public int CakismaSayisi { get; set; }

    public List<OdaDolulukCakismaDto> Cakismalar { get; set; } = [];
}
