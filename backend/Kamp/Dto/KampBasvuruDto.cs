namespace STYS.Kamp.Dto;

public class KampBasvuruDto
{
    public int Id { get; set; }
    public string BasvuruNo { get; set; } = string.Empty;
    public int KampDonemiId { get; set; }
    public string KampDonemiAd { get; set; } = string.Empty;
    public DateTime KonaklamaBaslangicTarihi { get; set; }
    public DateTime KonaklamaBitisTarihi { get; set; }
    public int TesisId { get; set; }
    public string TesisAd { get; set; } = string.Empty;
    public string KonaklamaBirimiTipi { get; set; } = string.Empty;
    public string BasvuruSahibiAdiSoyadi { get; set; } = string.Empty;
    public string BasvuruSahibiTipi { get; set; } = string.Empty;
    public int HizmetYili { get; set; }
    public List<int> GecmisKatilimYillari { get; set; } = [];
    public bool EvcilHayvanGetirecekMi { get; set; }
    public string Durum { get; set; } = string.Empty;
    public int KatilimciSayisi { get; set; }
    public int OncelikSirasi { get; set; }
    public int Puan { get; set; }
    public decimal GunlukToplamTutar { get; set; }
    public decimal DonemToplamTutar { get; set; }
    public decimal AvansToplamTutar { get; set; }
    public decimal KalanOdemeTutari { get; set; }
    public List<string> Uyarilar { get; set; } = [];
    public bool BuzdolabiTalepEdildiMi { get; set; }
    public bool TelevizyonTalepEdildiMi { get; set; }
    public bool KlimaTalepEdildiMi { get; set; }
    public DateTime? CreatedAt { get; set; }
    public List<KampBasvuruKatilimciDto> Katilimcilar { get; set; } = [];
    public List<KampBasvuruTercihDto> Tercihler { get; set; } = [];
}

public class KampKatilimciIptalSonucDto
{
    public int KampBasvuruId { get; set; }
    public int KatilimciId { get; set; }
    public int KalanKatilimciSayisi { get; set; }
    public bool TekKisiKaldiMi { get; set; }
    public string? UyariMesaji { get; set; }
}
