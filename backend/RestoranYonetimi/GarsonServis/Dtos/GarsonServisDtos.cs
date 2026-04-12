using System.ComponentModel.DataAnnotations;

namespace STYS.GarsonServis.Dtos;

public class GarsonMasaDto
{
    public int MasaId { get; set; }
    public string MasaNo { get; set; } = string.Empty;
    public string Durum { get; set; } = string.Empty;
    public int? AktifOturumId { get; set; }
    public decimal? AktifOturumToplamTutar { get; set; }
    public int AktifKalemSayisi { get; set; }
    public DateTime? SonIslemZamani { get; set; }
}

public class MasaOturumuKalemiDto
{
    public int Id { get; set; }
    public int UrunId { get; set; }
    public string UrunAdi { get; set; } = string.Empty;
    public decimal BirimFiyat { get; set; }
    public decimal Miktar { get; set; }
    public decimal SatirToplam { get; set; }
    public string Durum { get; set; } = string.Empty;
    public string? Notlar { get; set; }
}

public class MasaOturumuDto
{
    public int OturumId { get; set; }
    public int RestoranId { get; set; }
    public int MasaId { get; set; }
    public string MasaNo { get; set; } = string.Empty;
    public string Durum { get; set; } = string.Empty;
    public string? Notlar { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public decimal ToplamTutar { get; set; }
    public DateTime SiparisTarihi { get; set; }
    public List<MasaOturumuKalemiDto> Kalemler { get; set; } = [];
}

public class CreateMasaOturumuRequest
{
    [StringLength(3, MinimumLength = 3)]
    public string ParaBirimi { get; set; } = "TRY";
}

public class AddMasaOturumuKalemiRequest
{
    [Required]
    public int UrunId { get; set; }

    [Range(0.01, 99999)]
    public decimal Miktar { get; set; } = 1;

    public string? Notlar { get; set; }
}

public class UpdateMasaOturumuKalemiRequest
{
    [Range(0, 99999)]
    public decimal Miktar { get; set; }

    public string? Durum { get; set; }

    public string? Notlar { get; set; }
}

public class UpdateMasaOturumuNotRequest
{
    public string? Notlar { get; set; }
}

public class UpdateMasaOturumuDurumRequest
{
    [Required]
    public string Durum { get; set; } = string.Empty;
}

public class GarsonMenuDto
{
    public int RestoranId { get; set; }
    public List<GarsonMenuKategoriDto> Kategoriler { get; set; } = [];
}

public class GarsonMenuKategoriDto
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public int SiraNo { get; set; }
    public List<GarsonMenuUrunDto> Urunler { get; set; } = [];
}

public class GarsonMenuUrunDto
{
    public int Id { get; set; }
    public int KategoriId { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public decimal Fiyat { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public int HazirlamaSuresiDakika { get; set; }
}
