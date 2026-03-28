namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonKonaklamaHakkiDto
{
    public int Id { get; set; }

    public string HizmetKodu { get; set; } = string.Empty;

    public string HizmetAdi { get; set; } = string.Empty;

    public int Miktar { get; set; } = 1;

    public string Periyot { get; set; } = string.Empty;

    public string PeriyotAdi { get; set; } = string.Empty;

    public string KullanimTipi { get; set; } = string.Empty;

    public string KullanimTipiAdi { get; set; } = string.Empty;

    public string KullanimNoktasi { get; set; } = string.Empty;

    public string KullanimNoktasiAdi { get; set; } = string.Empty;

    public string? KullanimBaslangicSaati { get; set; }

    public string? KullanimBitisSaati { get; set; }

    public bool CheckInGunuGecerliMi { get; set; } = true;

    public bool CheckOutGunuGecerliMi { get; set; } = true;

    public DateTime? HakTarihi { get; set; }

    public string? Aciklama { get; set; }

    public string Durum { get; set; } = string.Empty;

    public int TuketilenMiktar { get; set; }

    public int? KalanMiktar { get; set; }

    public DateTime? SonTuketimTarihi { get; set; }

    public List<RezervasyonKonaklamaHakkiTuketimNoktasiDto> TuketimNoktalari { get; set; } = [];

    public List<RezervasyonKonaklamaHakkiTuketimKaydiDto> TuketimKayitlari { get; set; } = [];
}
