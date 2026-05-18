using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.MuhasebeFisleri.Dtos;

public class MuhasebeFisDto : BaseRdbmsDto<int>
{
    public int TesisId { get; set; }
    public int MaliYil { get; set; }
    public int Donem { get; set; }
    public string FisNo { get; set; } = string.Empty;
    public int? YevmiyeNo { get; set; }
    public DateTime FisTarihi { get; set; }
    public string FisTipi { get; set; } = string.Empty;
    public string KaynakModul { get; set; } = string.Empty;
    public int? KaynakId { get; set; }
    public string Durum { get; set; } = string.Empty;
    public decimal ToplamBorc { get; set; }
    public decimal ToplamAlacak { get; set; }
    public string? Aciklama { get; set; }
    public int? TersKayitFisId { get; set; }
    public int? IptalEdilenFisId { get; set; }
    public List<MuhasebeFisSatirDto> Satirlar { get; set; } = [];
}

public class MuhasebeFisSatirDto : BaseRdbmsDto<int>
{
    public int MuhasebeFisId { get; set; }
    public int MuhasebeHesapPlaniId { get; set; }
    public string? MuhasebeHesapKodu { get; set; }
    public string? MuhasebeHesapAdi { get; set; }
    public int SiraNo { get; set; }
    public decimal Borc { get; set; }
    public decimal Alacak { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public decimal Kur { get; set; } = 1;
    public int? CariKartId { get; set; }
    public int? TasinirKartId { get; set; }
    public int? DepoId { get; set; }
    public int? KasaBankaHesapId { get; set; }
    public string? Aciklama { get; set; }
}

public class CreateMuhasebeFisRequest
{
    public int TesisId { get; set; }
    public int MaliYil { get; set; }
    public int Donem { get; set; }
    public DateTime FisTarihi { get; set; }
    public string FisTipi { get; set; } = string.Empty;
    public string? KaynakModul { get; set; }
    public int? KaynakId { get; set; }
    public string? Aciklama { get; set; }
    public List<CreateMuhasebeFisSatirRequest> Satirlar { get; set; } = [];
}

public class CreateMuhasebeFisSatirRequest
{
    public int MuhasebeHesapPlaniId { get; set; }
    public int SiraNo { get; set; }
    public decimal Borc { get; set; }
    public decimal Alacak { get; set; }
    public string? ParaBirimi { get; set; }
    public decimal Kur { get; set; } = 1;
    public int? CariKartId { get; set; }
    public int? TasinirKartId { get; set; }
    public int? DepoId { get; set; }
    public int? KasaBankaHesapId { get; set; }
    public string? Aciklama { get; set; }
}

public class UpdateMuhasebeFisRequest
{
    public int TesisId { get; set; }
    public int MaliYil { get; set; }
    public int Donem { get; set; }
    public DateTime FisTarihi { get; set; }
    public string FisTipi { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public List<CreateMuhasebeFisSatirRequest> Satirlar { get; set; } = [];
}

public class MuhasebeFisIptalRequest
{
    public string? Aciklama { get; set; }
}

public class MuhasebeFisFilterDto
{
    public int? TesisId { get; set; }
    public int? MaliYil { get; set; }
    public int? Donem { get; set; }

    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }

    public string? FisTipi { get; set; }
    public string? Durum { get; set; }
    public string? KaynakModul { get; set; }
    public int? KaynakId { get; set; }

    public int? YevmiyeNoBaslangic { get; set; }
    public int? YevmiyeNoBitis { get; set; }

    public string? FisNo { get; set; }
    public string? Aciklama { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;

    public void Normalize()
    {
        if (Page < 1) Page = 1;
        if (PageSize < 1) PageSize = 50;
        if (PageSize > 500) PageSize = 500;
    }
}

public class YevmiyeDefteriSatirDto
{
    public int FisId { get; set; }
    public string FisNo { get; set; } = string.Empty;
    public int? YevmiyeNo { get; set; }

    public DateTime FisTarihi { get; set; }
    public string FisTipi { get; set; } = string.Empty;
    public string Durum { get; set; } = string.Empty;

    public int SiraNo { get; set; }

    public int MuhasebeHesapPlaniId { get; set; }
    public string? MuhasebeHesapKodu { get; set; }
    public string? MuhasebeHesapAdi { get; set; }

    public decimal Borc { get; set; }
    public decimal Alacak { get; set; }

    public string? SatirAciklama { get; set; }
    public string? FisAciklama { get; set; }

    public string KaynakModul { get; set; } = string.Empty;
    public int? KaynakId { get; set; }
}

public class YevmiyeDefteriDto
{
    public List<YevmiyeDefteriSatirDto> Satirlar { get; set; } = [];
    public decimal ToplamBorc { get; set; }
    public decimal ToplamAlacak { get; set; }
}
