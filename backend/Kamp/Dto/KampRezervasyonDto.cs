namespace STYS.Kamp.Dto;

public class KampRezervasyonListeDto
{
    public int Id { get; set; }
    public string RezervasyonNo { get; set; } = string.Empty;
    public int KampBasvuruId { get; set; }
    public int KampDonemiId { get; set; }
    public string KampDonemiAd { get; set; } = string.Empty;
    public int TesisId { get; set; }
    public string TesisAd { get; set; } = string.Empty;
    public string BasvuruSahibiAdiSoyadi { get; set; } = string.Empty;
    public string BasvuruSahibiTipi { get; set; } = string.Empty;
    public string KonaklamaBirimiTipi { get; set; } = string.Empty;
    public int KatilimciSayisi { get; set; }
    public decimal DonemToplamTutar { get; set; }
    public decimal AvansToplamTutar { get; set; }
    public string Durum { get; set; } = string.Empty;
    public string? IptalNedeni { get; set; }
    public DateTime? IptalTarihi { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class KampRezervasyonFilterDto
{
    public int? KampDonemiId { get; set; }
    public int? TesisId { get; set; }
    public string? Durum { get; set; }
}

public class KampRezervasyonBaglamDto
{
    public List<KampRezervasyonDonemSecenekDto> Donemler { get; set; } = [];
    public List<KampRezervasyonTesisSecenekDto> Tesisler { get; set; } = [];
    public List<string> Durumlar { get; set; } = [];
}

public class KampRezervasyonDonemSecenekDto
{
    public int Id { get; set; }
    public string? KampProgramiAd { get; set; }
    public string Ad { get; set; } = string.Empty;
}

public class KampRezervasyonTesisSecenekDto
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
}

public class KampRezervasyonUretRequestDto
{
    public int KampBasvuruId { get; set; }
}

public class KampRezervasyonUretSonucDto
{
    public int Id { get; set; }
    public string RezervasyonNo { get; set; } = string.Empty;
}

public class KampRezervasyonIptalRequestDto
{
    public string IptalNedeni { get; set; } = string.Empty;
}
