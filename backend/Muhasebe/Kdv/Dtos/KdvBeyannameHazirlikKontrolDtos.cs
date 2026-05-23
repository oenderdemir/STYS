namespace STYS.Muhasebe.Kdv.Dtos;

public class KdvBeyannameHazirlikKontrolFilterDto
{
    public int? TesisId { get; set; }
    public int? DepoId { get; set; }

    public int MaliYil { get; set; }
    public int Donem { get; set; }

    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
}

public class KdvBeyannameHazirlikKontrolDto
{
    public int? TesisId { get; set; }
    public int? DepoId { get; set; }

    public int MaliYil { get; set; }
    public int Donem { get; set; }

    public DateTime BaslangicTarihi { get; set; }
    public DateTime BitisTarihi { get; set; }

    public bool BeyanaHazirMi { get; set; }

    public int ToplamKontrolSayisi { get; set; }
    public int BasariliKontrolSayisi { get; set; }
    public int UyariliKontrolSayisi { get; set; }
    public int BloklayiciKontrolSayisi { get; set; }

    public decimal HesaplananKdvTutari { get; set; }
    public decimal IndirilecekKdvTutari { get; set; }
    public decimal NetKdv { get; set; }

    public List<KdvBeyannameHazirlikKontrolMaddesiDto> Kontroller { get; set; } = [];
}

public class KdvBeyannameHazirlikKontrolMaddesiDto
{
    public string Kod { get; set; } = string.Empty;
    public string Baslik { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;

    /// <summary>Basarili / Uyari / Bloklayici</summary>
    public string Durum { get; set; } = "Basarili";

    /// <summary>success / info / warn / error</summary>
    public string Severity { get; set; } = "success";

    public bool BloklayiciMi { get; set; }

    public int? EtkilenenKayitSayisi { get; set; }
    public string? Route { get; set; }
    public object? RouteQueryParams { get; set; }
}
