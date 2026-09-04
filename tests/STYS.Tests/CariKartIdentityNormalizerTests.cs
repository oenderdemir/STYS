using STYS.Muhasebe.CariKartlar;

namespace STYS.Tests;

public sealed class CariKartIdentityNormalizerTests
{
    [Theory]
    [InlineData("12345678901", "12345678901")]
    [InlineData("123-456-7890", "1234567890")]
    [InlineData(" 123.456.7890 ", "1234567890")]
    [InlineData("ABC-DEF", "ABCDEF")]
    [InlineData("abc123", "ABC123")]
    [InlineData("   ", null)]
    [InlineData("---", null)]
    [InlineData("", null)]
    public void NormalizeVergiNoTckn_Beklenen(string? input, string? expected)
    {
        Assert.Equal(expected, CariKartIdentityNormalizer.NormalizeVergiNoTckn(input));
    }

    [Fact]
    public void NormalizeVergiNoTckn_Null_ReturnsNull()
    {
        Assert.Null(CariKartIdentityNormalizer.NormalizeVergiNoTckn(null));
    }

    [Fact]
    public void NormalizeVergiNoTckn_UzunDeger_32yeKirpilir()
    {
        var input = new string('1', 40);
        var result = CariKartIdentityNormalizer.NormalizeVergiNoTckn(input);
        Assert.NotNull(result);
        Assert.Equal(32, result!.Length);
    }

    [Theory]
    [InlineData("Musteri", true)]
    [InlineData("KurumsalMusteri", true)]
    [InlineData("Tedarikci", false)]
    [InlineData("Personel", false)]
    [InlineData(null, false)]
    public void IsMusteriGrubu_Beklenen(string? tip, bool expected)
    {
        Assert.Equal(expected, CariKartIdentityNormalizer.IsMusteriGrubu(tip));
    }
}
