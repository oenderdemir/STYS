using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.StokTalepleri.Dtos;

public class StokTalepDto : BaseRdbmsDto<int>
{
    public int TesisId { get; set; }
    public int TalepEdenDepoId { get; set; }
    public int KarsilayanDepoId { get; set; }
    public DateTime TalepTarihi { get; set; }
    public string Durum { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public Guid? TalepEdenKullaniciId { get; set; }
    public List<StokTalepSatirDto> Satirlar { get; set; } = [];
}

public class StokTalepSatirDto : BaseRdbmsDto<int>
{
    public int StokTalepId { get; set; }
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
    public decimal TalepMiktari { get; set; }
    public decimal OnaylananMiktar { get; set; }
    public decimal TeslimEdilenMiktar { get; set; }
    public string? Aciklama { get; set; }
    public Guid? TransferGrupId { get; set; }
}

public class CreateStokTalepRequest
{
    public int TalepEdenDepoId { get; set; }
    public int KarsilayanDepoId { get; set; }
    public DateTime TalepTarihi { get; set; }
    public string? Aciklama { get; set; }
}

public class AddStokTalepSatirRequest
{
    public int TasinirKartId { get; set; }
    public decimal TalepMiktari { get; set; }
    public string? Aciklama { get; set; }
}

public class UpdateStokTalepSatirlarRequest
{
    public List<UpdateStokTalepSatirRequest> Satirlar { get; set; } = [];
}

public class UpdateStokTalepSatirRequest
{
    public int Id { get; set; }
    public decimal TalepMiktari { get; set; }
    public decimal OnaylananMiktar { get; set; }
    public string? Aciklama { get; set; }
}

public class TeslimEtStokTalepRequest
{
    public List<TeslimEtStokTalepSatirRequest> Satirlar { get; set; } = [];
}

public class TeslimEtStokTalepSatirRequest
{
    public int Id { get; set; }
    public int? StokLotId { get; set; }
    public int? StokSeriId { get; set; }
}
