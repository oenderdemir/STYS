using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.StokSayimlari.Dtos;

public class StokSayimDto : BaseRdbmsDto<int>
{
    public int TesisId { get; set; }
    public int DepoId { get; set; }
    public DateTime SayimTarihi { get; set; }
    public string Durum { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public List<StokSayimSatirDto> Satirlar { get; set; } = [];
}

public class StokSayimSatirDto : BaseRdbmsDto<int>
{
    public int StokSayimId { get; set; }
    public int TasinirKartId { get; set; }
    public int? StokLotId { get; set; }
    public int? StokSeriId { get; set; }
    public string TakipTipi { get; set; } = string.Empty;
    public string StokKodu { get; set; } = string.Empty;
    public string TasinirKartAd { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public string? LotNo { get; set; }
    public DateTime? SonKullanmaTarihi { get; set; }
    public string? SeriNo { get; set; }
    public decimal SistemMiktari { get; set; }
    public decimal SayilanMiktar { get; set; }
    public decimal FarkMiktari { get; set; }
}

public class CreateStokSayimRequest
{
    public int DepoId { get; set; }
    public DateTime SayimTarihi { get; set; }
    public string? Aciklama { get; set; }
}

public class UpdateStokSayimSatirlarRequest
{
    public List<UpdateStokSayimSatirRequest> Satirlar { get; set; } = [];
}

public class UpdateStokSayimSatirRequest
{
    public int Id { get; set; }
    public decimal SayilanMiktar { get; set; }
}

public class AddStokSayimSatirRequest
{
    public int TasinirKartId { get; set; }
    public int? StokLotId { get; set; }
    public int? StokSeriId { get; set; }
    public string? LotNo { get; set; }
    public DateTime? SonKullanmaTarihi { get; set; }
    public string? SeriNo { get; set; }
    public decimal SayilanMiktar { get; set; }
}
