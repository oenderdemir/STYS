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

    /// <summary>Muhasebe fişi kimliğine değil, aktif SatisBelgesi kaynaklı CariHareket'in
    /// varlığına göre belirlenir (bkz. RezervasyonGelirTahakkukService.MuhasebelestirildiMiAsync).
    /// Frontend'e muhasebe fişi kimliği/route'u TAŞINMAZ.</summary>
    public bool MuhasebelestirildiMi { get; set; }

    /// <summary>true ise KapatOncekiTahsilatlariAsync çağrılabilir.</summary>
    public bool TahsilatlarKapatilabilirMi { get; set; }

    /// <summary>TahsilatlarKapatilabilirMi=false iken nedeni açıklayan, kullanıcıya gösterilebilir metin (ör. buton disable tooltip'i).</summary>
    public string? TahsilatlarKapatilamazNedeni { get; set; }

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
