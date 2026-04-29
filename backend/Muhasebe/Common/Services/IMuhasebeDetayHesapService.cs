namespace STYS.Muhasebe.Common.Services;

public interface IMuhasebeDetayHesapService
{
    Task<(int HesapPlaniId, string Kod, int SiraNo)> CreateAsync(
        int tesisId,
        string anaHesapKodu,
        string kaynakAd,
        string kaynakTipi,
        CancellationToken cancellationToken = default);
}

