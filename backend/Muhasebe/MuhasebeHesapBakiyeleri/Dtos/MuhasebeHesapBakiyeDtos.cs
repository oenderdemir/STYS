using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.MuhasebeHesapBakiyeleri.Dtos;

public class MuhasebeHesapBakiyeDto : BaseRdbmsDto<int>
{
    public int TesisId { get; set; }
    public string? TesisAdi { get; set; }

    public int MaliYil { get; set; }
    public int Donem { get; set; }

    public int MuhasebeHesapPlaniId { get; set; }

    public string HesapKodu { get; set; } = string.Empty;
    public string HesapAdi { get; set; } = string.Empty;

    public bool KonsolideMi { get; set; }

    public decimal BorcToplam { get; set; }
    public decimal AlacakToplam { get; set; }

    public decimal BorcBakiye { get; set; }
    public decimal AlacakBakiye { get; set; }

    public decimal NetBakiye { get; set; }
    public decimal Bakiye { get; set; }
    public string BakiyeTipi { get; set; } = string.Empty;

    public int HesapSeviyesi { get; set; }
    public string? UstHesapKodu { get; set; }

    public DateTime SonGuncellemeTarihi { get; set; }
}

public class CreateMuhasebeHesapBakiyeRequest
{
    public int TesisId { get; set; }
    public int MaliYil { get; set; }
    public int Donem { get; set; }

    public int MuhasebeHesapPlaniId { get; set; }

    public bool KonsolideMi { get; set; }

    public decimal BorcToplam { get; set; }
    public decimal AlacakToplam { get; set; }
}

public class UpdateMuhasebeHesapBakiyeRequest
{
    public int TesisId { get; set; }
    public int MaliYil { get; set; }
    public int Donem { get; set; }

    public int MuhasebeHesapPlaniId { get; set; }

    public bool KonsolideMi { get; set; }

    public decimal BorcToplam { get; set; }
    public decimal AlacakToplam { get; set; }
}

public class MuhasebeHesapBakiyeFilterDto
{
    public int? TesisId { get; set; }
    public int? MaliYil { get; set; }
    public int? Donem { get; set; }

    public int? MuhasebeHesapPlaniId { get; set; }
    public string? HesapKoduBaslangic { get; set; }
    public string? HesapKoduBitis { get; set; }

    public bool? KonsolideMi { get; set; }

    public int? HesapSeviyesi { get; set; }
    public string? UstHesapKodu { get; set; }
    public string? BakiyeTipi { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 500;

    public void Normalize()
    {
        if (Page < 1) Page = 1;
        if (PageSize < 1) PageSize = 500;
        if (PageSize > 2000) PageSize = 2000;

        HesapKoduBaslangic = string.IsNullOrWhiteSpace(HesapKoduBaslangic)
            ? null
            : HesapKoduBaslangic.Trim();

        HesapKoduBitis = string.IsNullOrWhiteSpace(HesapKoduBitis)
            ? null
            : HesapKoduBitis.Trim();

        UstHesapKodu = string.IsNullOrWhiteSpace(UstHesapKodu)
            ? null
            : UstHesapKodu.Trim();

        BakiyeTipi = string.IsNullOrWhiteSpace(BakiyeTipi)
            ? null
            : BakiyeTipi.Trim();
    }
}

public class MuhasebeHesapBakiyeRebuildRequest
{
    public int TesisId { get; set; }
    public int MaliYil { get; set; }

    /// <summary>
    /// Null ise tüm yıl rebuild edilir. Dolu ise sadece ilgili dönem rebuild edilir.
    /// </summary>
    public int? Donem { get; set; }

    /// <summary>
    /// Varsayılan false. Bu fazda true gelse bile soft delete tercih edilir.
    /// </summary>
    public bool ForceHardDelete { get; set; } = false;
}

public class MuhasebeHesapBakiyeRebuildResultDto
{
    public int TesisId { get; set; }
    public int MaliYil { get; set; }
    public int? Donem { get; set; }

    public int IslenenFisSayisi { get; set; }
    public int IslenenSatirSayisi { get; set; }

    public int SilinenBakiyeKaydiSayisi { get; set; }
    public int OlusturulanBakiyeKaydiSayisi { get; set; }

    public DateTime BaslamaZamani { get; set; }
    public DateTime BitisZamani { get; set; }

    public string Mesaj { get; set; } = string.Empty;
}
