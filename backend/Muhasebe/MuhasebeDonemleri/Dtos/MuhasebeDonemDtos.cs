using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.MuhasebeDonemleri.Dtos;

public class MuhasebeDonemDto : BaseRdbmsDto<int>
{
    public int TesisId { get; set; }
    public string? TesisAdi { get; set; }

    public int MaliYil { get; set; }
    public int DonemNo { get; set; }

    public DateTime BaslangicTarihi { get; set; }
    public DateTime BitisTarihi { get; set; }

    public bool KapaliMi { get; set; }
    public DateTime? KapanisTarihi { get; set; }

    public string? Aciklama { get; set; }
}

public class CreateMuhasebeDonemRequest
{
    public int TesisId { get; set; }
    public int MaliYil { get; set; }
    public int DonemNo { get; set; }

    public DateTime BaslangicTarihi { get; set; }
    public DateTime BitisTarihi { get; set; }

    public string? Aciklama { get; set; }
}

public class UpdateMuhasebeDonemRequest
{
    public int TesisId { get; set; }
    public int MaliYil { get; set; }
    public int DonemNo { get; set; }

    public DateTime BaslangicTarihi { get; set; }
    public DateTime BitisTarihi { get; set; }

    public bool KapaliMi { get; set; }

    public string? Aciklama { get; set; }
}
