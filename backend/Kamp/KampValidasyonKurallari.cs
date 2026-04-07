namespace STYS.Kamp;

/// <summary>
/// Kamp yönetimi modülü için validasyon sınırlarını merkezi bir yerden yönetmeyi sağlar.
/// Her alan min/max değerlerini sabit tutar.
/// </summary>
public static class KampValidasyonKurallari
{
    public static class YilRange
    {
        public const int Min = 2000;
        public const int Max = 2100;
    }

    public static class OncekiYilSayisi
    {
        public const int Min = 0;
        public const int Max = 10;
    }

    public static class KatilimCezaPuani
    {
        public const int Min = 0;
        public const int Max = 1000;
    }

    public static class KatilimciBasinaPuan
    {
        public const int Min = 0;
        public const int Max = 1000;
    }

    public static class OncelikSirasi
    {
        public const int Min = 0;
        public const int Max = 999;
    }

    public static class TabanPuan
    {
        public const int Min = 0;
        public const int Max = 5000;
    }

    public static class EmekliBonusPuani
    {
        public const int Min = 0;
        public const int Max = 1000;
    }

    public static class UcretsizCocukMaxYas
    {
        public const int Min = 0;
        public const int Max = 18;
    }

    public static class YarimUcretliCocukMaxYas
    {
        public const int Min = 0;
        public const int Max = 18;
    }

    public static class YemekOrani
    {
        public const decimal Min = 0.00m;
        public const decimal Max = 1.00m;
    }

    public static class AvansKisiBasi
    {
        public const decimal Min = 0.00m;
        public const decimal Max = 50000.00m;
    }

    public static class VazgecmeIadeGunSayisi
    {
        public const int Min = 0;
        public const int Max = 60;
    }

    public static class GecBildirimGunlukKesintiOrani
    {
        public const decimal Min = 0.00m;
        public const decimal Max = 1.00m;
    }

    public static class NoShowSuresiGun
    {
        public const int Min = 0;
        public const int Max = 30;
    }
}
