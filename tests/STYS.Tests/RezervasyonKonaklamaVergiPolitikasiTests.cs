using STYS.Rezervasyonlar.Services;
using Xunit;

namespace STYS.Tests;

public class RezervasyonKonaklamaVergiPolitikasiTests
{
    [Theory]
    [InlineData("2026-12-31", 1)]
    [InlineData("2027-01-01", 2)]
    public void Resolve_KonaklamaVergisiOraniniHizmetTarihineGoreBelirler(string tarih, decimal beklenenKonaklamaVergisiOrani)
    {
        var politika = new RezervasyonKonaklamaVergiPolitikasi();

        var karar = politika.Resolve(DateTime.Parse(tarih));

        Assert.Equal(10m, karar.KdvOrani);
        Assert.Equal(beklenenKonaklamaVergisiOrani, karar.KonaklamaVergisiOrani);
    }
}
