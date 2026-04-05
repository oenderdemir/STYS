namespace STYS.Kamp.Dto;

public class KampBasvuruRequestDto
{
    public int KampDonemiId { get; set; }
    public int TesisId { get; set; }
    public string KonaklamaBirimiTipi { get; set; } = string.Empty;
    public string BasvuruSahibiTipi { get; set; } = string.Empty;
    public int HizmetYili { get; set; }
    public bool Kamp2023tenFaydalandiMi { get; set; }
    public bool Kamp2024tenFaydalandiMi { get; set; }
    public bool EvcilHayvanGetirecekMi { get; set; }
    public bool BuzdolabiTalepEdildiMi { get; set; }
    public bool TelevizyonTalepEdildiMi { get; set; }
    public bool KlimaTalepEdildiMi { get; set; }
    public List<KampBasvuruKatilimciDto> Katilimcilar { get; set; } = [];
}
