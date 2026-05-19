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

public class MuavinDefterFilterDto
{
    public int TesisId { get; set; }
    public int MuhasebeHesapPlaniId { get; set; }

    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }

    public int? MaliYil { get; set; }
    public int? Donem { get; set; }

    public bool AltHesaplariDahilEt { get; set; } = false;

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100;

    public void Normalize()
    {
        if (Page < 1) Page = 1;
        if (PageSize < 1) PageSize = 100;
        if (PageSize > 1000) PageSize = 1000;
    }
}

public class MuavinDefterSatirDto
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

    public decimal Bakiye { get; set; }
    public string BakiyeTipi { get; set; } = string.Empty; // Borc / Alacak / Sifir

    public string? SatirAciklama { get; set; }
    public string? FisAciklama { get; set; }

    public string KaynakModul { get; set; } = string.Empty;
    public int? KaynakId { get; set; }
}

public class MuavinDefterDto
{
    public int TesisId { get; set; }
    public int MuhasebeHesapPlaniId { get; set; }
    public string? MuhasebeHesapKodu { get; set; }
    public string? MuhasebeHesapAdi { get; set; }

    public decimal ToplamBorc { get; set; }
    public decimal ToplamAlacak { get; set; }
    public decimal Bakiye { get; set; }
    public string BakiyeTipi { get; set; } = string.Empty; // Borc / Alacak / Sifir

    public List<MuavinDefterSatirDto> Satirlar { get; set; } = [];
}

public class MizanFilterDto
{
    public int TesisId { get; set; }

    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }

    public int? MaliYil { get; set; }
    public int? Donem { get; set; }

    public bool SadeceHareketGorenHesaplar { get; set; } = true;
    public bool AltHesaplariDahilEt { get; set; } = true;

    public string? HesapKoduBaslangic { get; set; }
    public string? HesapKoduBitis { get; set; }

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
    }
}

public class MizanSatirDto
{
    public int MuhasebeHesapPlaniId { get; set; }

    public string HesapKodu { get; set; } = string.Empty;
    public string HesapAdi { get; set; } = string.Empty;

    public bool DetayHesapMi { get; set; }
    public bool HareketGorebilirMi { get; set; }

    public decimal ToplamBorc { get; set; }
    public decimal ToplamAlacak { get; set; }

    public decimal BorcBakiye { get; set; }
    public decimal AlacakBakiye { get; set; }

    public decimal Bakiye { get; set; }
    public string BakiyeTipi { get; set; } = string.Empty; // Borc / Alacak / Sifir

    /// <summary>
    /// Sadece alt hesapların konsolidasyonu ile oluşan üst hesap satırı ise true.
    /// Doğrudan hareket gören hesap satırları için false.
    /// </summary>
    public bool KonsolideSatirMi { get; set; }

    /// <summary>
    /// Hesap kodundaki segment sayısı (nokta ile ayrılmış parça sayısı).
    /// Örn: "150" → 1, "150.01" → 2, "150.01.001" → 3
    /// </summary>
    public int Seviye { get; set; }
}

public class MizanDto
{
    public int TesisId { get; set; }

    public decimal GenelToplamBorc { get; set; }
    public decimal GenelToplamAlacak { get; set; }

    public decimal GenelBorcBakiye { get; set; }
    public decimal GenelAlacakBakiye { get; set; }

    public List<MizanSatirDto> Satirlar { get; set; } = [];
}

/// <summary>
/// Eski mizan (MuhasebeFisSatir tabanlı) ile hızlı mizan (MuhasebeHesapBakiye tabanlı)
/// sonuçlarını karşılaştıran doğrulama/denetim DTO'su.
/// </summary>
public class MizanKarsilastirmaDto
{
    public int TesisId { get; set; }
    public int? MaliYil { get; set; }
    public int? Donem { get; set; }

    public decimal EskiGenelToplamBorc { get; set; }
    public decimal HizliGenelToplamBorc { get; set; }
    public decimal GenelToplamBorcFark { get; set; }

    public decimal EskiGenelToplamAlacak { get; set; }
    public decimal HizliGenelToplamAlacak { get; set; }
    public decimal GenelToplamAlacakFark { get; set; }

    public decimal EskiGenelBorcBakiye { get; set; }
    public decimal HizliGenelBorcBakiye { get; set; }
    public decimal GenelBorcBakiyeFark { get; set; }

    public decimal EskiGenelAlacakBakiye { get; set; }
    public decimal HizliGenelAlacakBakiye { get; set; }
    public decimal GenelAlacakBakiyeFark { get; set; }

    public int EskiSatirSayisi { get; set; }
    public int HizliSatirSayisi { get; set; }
    public int FarkliSatirSayisi { get; set; }

    public bool EslesiyorMu { get; set; }

    public List<MizanKarsilastirmaSatirDto> Farklar { get; set; } = new();
}

public class MizanKarsilastirmaSatirDto
{
    public string HesapKodu { get; set; } = string.Empty;
    public string HesapAdi { get; set; } = string.Empty;

    public bool EskiMizandaVarMi { get; set; }
    public bool HizliMizandaVarMi { get; set; }

    public decimal EskiToplamBorc { get; set; }
    public decimal HizliToplamBorc { get; set; }
    public decimal ToplamBorcFark { get; set; }

    public decimal EskiToplamAlacak { get; set; }
    public decimal HizliToplamAlacak { get; set; }
    public decimal ToplamAlacakFark { get; set; }

    public decimal EskiBorcBakiye { get; set; }
    public decimal HizliBorcBakiye { get; set; }
    public decimal BorcBakiyeFark { get; set; }

    public decimal EskiAlacakBakiye { get; set; }
    public decimal HizliAlacakBakiye { get; set; }
    public decimal AlacakBakiyeFark { get; set; }

    /// <summary>
    /// SadeceEskiMizandaVar / SadeceHizliMizandaVar / TutarFarki / HesapAdiFarki
    /// </summary>
    public string FarkTipi { get; set; } = string.Empty;
}
