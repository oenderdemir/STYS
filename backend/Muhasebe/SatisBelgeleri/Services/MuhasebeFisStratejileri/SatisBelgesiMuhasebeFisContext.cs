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
    /// <summary>
    /// KDV oranı → o oran için kullanılacak MuhasebeHesapPlani.Id eşlemesi. Belgede KdvTutari>0
    /// olan HER ayrı KdvOrani için (eğer çözümlenebildiyse) bir giriş bulunur - tek, oran'dan
    /// bağımsız bir KDV hesabı YOKTUR (bkz. görev: KDV hesaplarını oran bazında çöz). Boş
    /// sözlük, belgede hiç KDV olmadığı (ToplamKdv=0) anlamına gelir.
    /// </summary>
    public IReadOnlyDictionary<decimal, int> KdvHesaplariByOran { get; init; } = new Dictionary<decimal, int>();
    public int? StokHesapPlaniId { get; init; }
    public int? HizmetGiderHesapPlaniId { get; init; }
}
