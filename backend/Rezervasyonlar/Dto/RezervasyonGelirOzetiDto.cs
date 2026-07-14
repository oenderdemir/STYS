namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonGelirOzetiDto
{
    public int RezervasyonId { get; set; }

    public string ReferansNo { get; set; } = string.Empty;

    public int? SatisBelgesiId { get; set; }

    public string? SatisBelgesiNo { get; set; }

    /// <summary>SatisBelgesiDurumu enum'unun string karsiligi (Taslak/MuhasebeOnayinda/
    /// MuhasebeOnaylandi/Reddedildi/FaturaKesildi/MusteriyeGonderildi/IptalEdildi).</summary>
    public string? SatisBelgesiDurumu { get; set; }

    public decimal? GenelToplam { get; set; }

    public int? MuhasebeFisId { get; set; }

    /// <summary>Rezervasyonun onceki tahsilatlarinin gelir belgesinin cari hareketine karsi
    /// ne kadarinin kapatildigi. Bkz. TahsilatKapamaDurumlari.</summary>
    public string TahsilatKapamaDurumu { get; set; } = string.Empty;

    public int TahsilatToplamSayisi { get; set; }

    public int TahsilatKapaliSayisi { get; set; }

    public int TahsilatHataliSayisi { get; set; }
}

public class RezervasyonTahsilatKapamaSonucuDto
{
    public int BasariliSayisi { get; set; }

    public int HataliSayisi { get; set; }

    public int AtlananSayisi { get; set; }

    public List<string> Hatalar { get; set; } = [];

    public RezervasyonGelirOzetiDto Ozet { get; set; } = new();
}
