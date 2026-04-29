namespace STYS.Muhasebe.Common.Services;

public sealed class MuhasebeDetayHesapSonuc
{
    public int MuhasebeHesapPlaniId { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string AnaMuhasebeHesapKodu { get; set; } = string.Empty;
    public int SiraNo { get; set; }
}

public interface IMuhasebeDetayHesapService
{
    Task<MuhasebeDetayHesapSonuc> CreateOrResolveDetayHesapAsync(
        int tesisId,
        string anaMuhasebeHesapKodu,
        string kaynakTipi,
        string kaynakAd,
        CancellationToken cancellationToken = default);
}
