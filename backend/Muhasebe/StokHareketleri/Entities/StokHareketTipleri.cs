namespace STYS.Muhasebe.StokHareketleri.Entities;

public static class StokHareketTipleri
{
    public const string Giris = "Giris";
    public const string Cikis = "Cikis";
    public const string Transfer = "Transfer";
    public const string Iade = "Iade";
    public const string Sarf = "Sarf";
    public const string SayimFarki = "SayimFarki";
    public const string Zimmet = "Zimmet";

    public static readonly string[] Hepsi = [Giris, Cikis, Transfer, Iade, Sarf, SayimFarki, Zimmet];
    public static readonly string[] GirisEtkisi = [Giris, Iade, SayimFarki];
    public static readonly string[] CikisEtkisi = [Cikis, Transfer, Sarf, Zimmet];
}
