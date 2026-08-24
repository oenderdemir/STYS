using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.SarfFisleri.Dtos;

public class SarfFisiDto : BaseRdbmsDto<int>
{
    public int TesisId { get; set; }
    public int DepoId { get; set; }
    public DateTime SarfTarihi { get; set; }
    public int? IsletmeAlaniId { get; set; }
    public string? IsletmeAlaniAd { get; set; }
    public string? BirimAd { get; set; }
    public int? OdaId { get; set; }
    public string? OdaAd { get; set; }
    public string? SarfNedeni { get; set; }
    public string Durum { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public Guid? OlusturanKullaniciId { get; set; }
    public DateTime? IptalTarihi { get; set; }
    public Guid? IptalEdenKullaniciId { get; set; }
    public string? IptalAciklamasi { get; set; }
    public List<SarfFisiSatirDto> Satirlar { get; set; } = [];
}

public class SarfFisiSatirDto : BaseRdbmsDto<int>
{
    public int SarfFisiId { get; set; }
    public int TasinirKartId { get; set; }
    public int? StokLotId { get; set; }
    public int? StokSeriId { get; set; }
    public int? StokHareketId { get; set; }
    public int? IptalStokHareketId { get; set; }
    public string TakipTipi { get; set; } = string.Empty;
    public string StokKodu { get; set; } = string.Empty;
    public string TasinirKartAd { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public string? LotNo { get; set; }
    public DateTime? SonKullanmaTarihi { get; set; }
    public string? SeriNo { get; set; }
    public decimal Miktar { get; set; }
    public string? Aciklama { get; set; }
}

public class CreateSarfFisiRequest
{
    public int DepoId { get; set; }
    public DateTime SarfTarihi { get; set; }
    public int? IsletmeAlaniId { get; set; }
    public int? OdaId { get; set; }
    public string? SarfNedeni { get; set; }
    public string? Aciklama { get; set; }
}

public class UpdateSarfFisiSatirlarRequest
{
    public List<UpdateSarfFisiSatirRequest> Satirlar { get; set; } = [];
}

public class UpdateSarfFisiSatirRequest
{
    public int Id { get; set; }
    public decimal Miktar { get; set; }
    public int? StokLotId { get; set; }
    public int? StokSeriId { get; set; }
    public string? Aciklama { get; set; }
}

public class AddSarfFisiSatirRequest
{
    public int TasinirKartId { get; set; }
    public decimal Miktar { get; set; }
    public int? StokLotId { get; set; }
    public int? StokSeriId { get; set; }
    public string? Aciklama { get; set; }
}

public class IptalSarfFisiRequest
{
    public string? IptalAciklamasi { get; set; }
}

public class SarfBirimSecenekDto
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
}

public class SarfOdaSecenekDto
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
}
