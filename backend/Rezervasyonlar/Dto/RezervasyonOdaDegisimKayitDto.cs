namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonOdaDegisimKayitDto
{
    public int RezervasyonSegmentOdaAtamaId { get; set; }

    public int SegmentId { get; set; }

    public int SegmentSirasi { get; set; }

    public DateTime BaslangicTarihi { get; set; }

    public DateTime BitisTarihi { get; set; }

    public int AyrilanKisiSayisi { get; set; }

    public int MevcutOdaId { get; set; }

    public string MevcutOdaNo { get; set; } = string.Empty;

    public string MevcutBinaAdi { get; set; } = string.Empty;

    public string MevcutOdaTipiAdi { get; set; } = string.Empty;

    public bool MevcutOdaPaylasimliMi { get; set; }

    public int MevcutOdaKapasitesi { get; set; }

    public bool ProblemliMi { get; set; }

    public List<RezervasyonOdaDegisimAdayOdaDto> AdayOdalar { get; set; } = [];
}

