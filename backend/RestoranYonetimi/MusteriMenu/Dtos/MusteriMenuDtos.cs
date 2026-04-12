namespace STYS.MusteriMenu.Dtos;

public class MusteriMenuDto
{
    public MusteriRestoranOzetDto Restoran { get; set; } = new();
    public List<MusteriMenuKategoriDto> Kategoriler { get; set; } = [];
}

public class MusteriRestoranOzetDto
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
}

public class MusteriMenuKategoriDto
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public int SiraNo { get; set; }
    public List<MusteriMenuUrunDto> Urunler { get; set; } = [];
}

public class MusteriMenuUrunDto
{
    public int Id { get; set; }
    public int KategoriId { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public decimal Fiyat { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public int HazirlamaSuresiDakika { get; set; }
}
