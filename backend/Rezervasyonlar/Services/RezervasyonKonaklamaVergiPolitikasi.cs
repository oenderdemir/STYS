namespace STYS.Rezervasyonlar.Services;

public sealed class RezervasyonKonaklamaVergiPolitikasi : IRezervasyonKonaklamaVergiPolitikasi
{
    private static readonly DateTime IndirimliKonaklamaVergisiBaslangic = new(2026, 5, 1);
    private static readonly DateTime IndirimliKonaklamaVergisiBitis = new(2026, 12, 31);

    public KonaklamaVergiKarari Resolve(DateTime hizmetTarihi)
    {
        var tarih = hizmetTarihi.Date;
        var konaklamaVergisiOrani =
            tarih >= IndirimliKonaklamaVergisiBaslangic && tarih <= IndirimliKonaklamaVergisiBitis
                ? 1m
                : 2m;

        return new KonaklamaVergiKarari(KdvOrani: 10m, KonaklamaVergisiOrani: konaklamaVergisiOrani);
    }
}

