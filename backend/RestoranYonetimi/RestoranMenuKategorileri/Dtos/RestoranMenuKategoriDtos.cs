using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.RestoranMenuKategorileri.Dtos;

public class RestoranMenuKategoriDto : BaseRdbmsDto<int>
{
    public int RestoranId { get; set; }
    public string Ad { get; set; } = string.Empty;
    public int SiraNo { get; set; }
    public bool AktifMi { get; set; } = true;
}

public class CreateRestoranMenuKategoriRequest
{
    [Required]
    public int RestoranId { get; set; }

    [Required]
    public string Ad { get; set; } = string.Empty;

    public int SiraNo { get; set; }

    public bool AktifMi { get; set; } = true;
}

public class UpdateRestoranMenuKategoriRequest
{
    [Required]
    public int RestoranId { get; set; }

    [Required]
    public string Ad { get; set; } = string.Empty;

    public int SiraNo { get; set; }

    public bool AktifMi { get; set; } = true;
}

public class RestoranMenuDto
{
    public int RestoranId { get; set; }
    public List<RestoranMenuKategoriDetayDto> Kategoriler { get; set; } = [];
}

public class RestoranMenuKategoriDetayDto
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public int SiraNo { get; set; }
    public List<RestoranMenuUrunDetayDto> Urunler { get; set; } = [];
}

public class RestoranMenuUrunDetayDto
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public decimal Fiyat { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public int HazirlamaSuresiDakika { get; set; }
}

public class RestoranGlobalMenuKategoriDto
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public int SiraNo { get; set; }
    public bool AktifMi { get; set; } = true;
    public int RestoranSayisi { get; set; }
}

public class CreateRestoranGlobalMenuKategoriRequest
{
    [Required]
    public string Ad { get; set; } = string.Empty;
    public int SiraNo { get; set; }
    public bool AktifMi { get; set; } = true;
}

public class UpdateRestoranGlobalMenuKategoriRequest
{
    [Required]
    public string Ad { get; set; } = string.Empty;
    public int SiraNo { get; set; }
    public bool AktifMi { get; set; } = true;
}

public class RestoranKategoriAtamaBaglamDto
{
    public int RestoranId { get; set; }
    public List<RestoranGlobalMenuKategoriDto> GlobalKategoriler { get; set; } = [];
    public List<int> SeciliGlobalKategoriIdleri { get; set; } = [];
}

public class SaveRestoranKategoriAtamaRequest
{
    public int RestoranId { get; set; }
    public List<int> SeciliGlobalKategoriIdleri { get; set; } = [];
}
