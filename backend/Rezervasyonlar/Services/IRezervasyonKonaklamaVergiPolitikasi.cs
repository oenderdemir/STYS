namespace STYS.Rezervasyonlar.Services;

public interface IRezervasyonKonaklamaVergiPolitikasi
{
    KonaklamaVergiKarari Resolve(DateTime hizmetTarihi);
}

public readonly record struct KonaklamaVergiKarari(decimal KdvOrani, decimal KonaklamaVergisiOrani);

