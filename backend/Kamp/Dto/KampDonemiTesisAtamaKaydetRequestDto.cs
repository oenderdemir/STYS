namespace STYS.Kamp.Dto;

public class KampDonemiTesisAtamaKaydetRequestDto
{
    public IReadOnlyCollection<KampDonemiTesisAtamaKayitDto> Kayitlar { get; set; } = [];
}

public class KampDonemiTesisAtamaKayitDto
{
    public int TesisId { get; set; }

    public bool AtamaVarMi { get; set; }

    public bool DonemdeAktifMi { get; set; }

    public bool BasvuruyaAcikMi { get; set; }

    public int ToplamKontenjan { get; set; }

    public string? Aciklama { get; set; }

    public List<string> KonaklamaTarifeKodlari { get; set; } = [];
}
