namespace STYS.Kamp.Dto;

public class KampTahsisOtomatikKararSonucDto
{
    public int KampDonemiId { get; set; }

    public int TesisId { get; set; }

    public int ToplamKontenjan { get; set; }

    public int DegerlendirilenBasvuruSayisi { get; set; }

    public int TahsisEdilenSayisi { get; set; }

    public int TahsisEdilemeyenSayisi { get; set; }

    public int GuncellenenKayitSayisi { get; set; }
}
