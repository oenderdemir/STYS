using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.SarfRaporlari.Dtos;

public sealed class SarfTuketimRaporFilterDto
{
    public int TesisId { get; set; }
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public int? DepoId { get; set; }
    public int? TasinirKartId { get; set; }
    public int? IsletmeAlaniId { get; set; }
    public int? OdaId { get; set; }
    public string? SarfNedeni { get; set; }
    public string? Durum { get; set; }
}

public sealed class SarfTuketimDetayRaporSatirDto
{
    public DateTime Tarih { get; set; }
    public int SarfFisiId { get; set; }
    public string FisNo => SarfFisiId.ToString();
    public int SarfFisiSatirId { get; set; }
    public int DepoId { get; set; }
    public string DepoKod { get; set; } = string.Empty;
    public string DepoAd { get; set; } = string.Empty;
    public int? IsletmeAlaniId { get; set; }
    public string? IsletmeAlaniAd { get; set; }
    public int? OdaId { get; set; }
    public string? OdaAd { get; set; }
    public string? SarfNedeni { get; set; }
    public int TasinirKartId { get; set; }
    public string StokKodu { get; set; } = string.Empty;
    public string MalzemeAd { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public decimal Miktar { get; set; }
    public string? LotNo { get; set; }
    public string? SeriNo { get; set; }
    public string Durum { get; set; } = string.Empty;
    public decimal? MaliyetBirimFiyat { get; set; }
    public decimal? ToplamMaliyet { get; set; }
}

public sealed class SarfTuketimMalzemeOzetDto
{
    public int TasinirKartId { get; set; }
    public string StokKodu { get; set; } = string.Empty;
    public string MalzemeAd { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public decimal ToplamTuketimMiktari { get; set; }
    public int SarfFisiSayisi { get; set; }
    public decimal ToplamTuketimMaliyeti { get; set; }
}

public sealed class SarfTuketimKullanimYeriOzetDto
{
    public int? IsletmeAlaniId { get; set; }
    public string? IsletmeAlaniAd { get; set; }
    public int? OdaId { get; set; }
    public string? OdaAd { get; set; }
    public int FarkliMalzemeSayisi { get; set; }
    public int ToplamSarfSatiriSayisi { get; set; }
    public string ToplamMiktarOzeti { get; set; } = string.Empty;
    public decimal ToplamTuketimMaliyeti { get; set; }
}

public sealed class SarfTuketimDetayRaporResponseDto
{
    public PagedResult<SarfTuketimDetayRaporSatirDto> Sonuclar { get; set; } = new([], 1, 20, 0);
}
