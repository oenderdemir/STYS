namespace STYS.Muhasebe.TasinirKartlari.Entities;

public static class TasinirKartTakipTipleri
{
    public const string Yok = "Yok";
    public const string Lot = "Lot";
    public const string Seri = "Seri";

    public static readonly IReadOnlyCollection<string> Hepsi = [Yok, Lot, Seri];
}
