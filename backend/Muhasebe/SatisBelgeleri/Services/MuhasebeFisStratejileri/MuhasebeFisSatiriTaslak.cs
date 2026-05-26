namespace STYS.Muhasebe.SatisBelgeleri.Services.MuhasebeFisStratejileri;

public sealed class MuhasebeFisSatiriTaslak
{
    public int MuhasebeHesapPlaniId { get; init; }
    public int SiraNo { get; init; }
    public decimal Borc { get; init; }
    public decimal Alacak { get; init; }
    public string Aciklama { get; init; } = string.Empty;
    public int? CariKartId { get; init; }
    public int? TasinirKartId { get; init; }
    public int? DepoId { get; init; }
    public int? KasaBankaHesapId { get; init; }
}
