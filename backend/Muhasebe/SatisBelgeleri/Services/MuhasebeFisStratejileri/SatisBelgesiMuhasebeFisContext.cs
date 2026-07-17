namespace STYS.Muhasebe.SatisBelgeleri.Services.MuhasebeFisStratejileri;

public sealed class SatisBelgesiMuhasebeFisContext
{
    public int TesisId { get; init; }
    public int MaliYil { get; init; }
    public int Donem { get; init; }
    public DateTime FisTarihi { get; init; }
    public string FisNo { get; init; } = string.Empty;
    public string BelgeNo { get; init; } = string.Empty;
    public int CariHesapPlaniId { get; init; }
    public int? CariKartId { get; init; }
    public int GelirHesapPlaniId { get; init; }
    public int? KdvHesapPlaniId { get; init; }
    public int? StokHesapPlaniId { get; init; }
    public int? HizmetGiderHesapPlaniId { get; init; }
}
