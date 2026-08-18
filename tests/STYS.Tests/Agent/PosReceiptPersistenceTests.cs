using STYS.Entegrasyonlar.Pos.Services;

namespace STYS.Tests.Agent;

public sealed class PosReceiptPersistenceTests
{
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void DecodeBase64_PlainBase64_Cozulur()
    {
        var bytes = PosReceiptPersistenceService.DecodeBase64("iVBORw0KGgo=");

        Assert.NotNull(bytes);
        Assert.Equal(PngSignature, bytes);
    }

    [Fact]
    public void DecodeBase64_DataUriPrefiksi_Cozulur()
    {
        var bytes = PosReceiptPersistenceService.DecodeBase64("data:image/png;base64,iVBORw0KGgo=");

        Assert.NotNull(bytes);
        Assert.Equal(PngSignature, bytes);
    }

    [Fact]
    public void DecodeBase64_GecersizBase64_NullDoner()
    {
        Assert.Null(PosReceiptPersistenceService.DecodeBase64("bu-base64-degil!!!"));
        Assert.Null(PosReceiptPersistenceService.DecodeBase64("   "));
        Assert.Null(PosReceiptPersistenceService.DecodeBase64(string.Empty));
    }

    [Fact]
    public void IsPng_PngImzasiIle_TrueDoner()
    {
        Assert.True(PosReceiptPersistenceService.IsPng(PngHeader));
    }

    [Fact]
    public void IsPng_PngOlmayanIcerik_FalseDoner()
    {
        Assert.False(PosReceiptPersistenceService.IsPng([0x47, 0x49, 0x46, 0x38, 0x39, 0x61])); // GIF
        Assert.False(PosReceiptPersistenceService.IsPng([]));
        Assert.False(PosReceiptPersistenceService.IsPng([0x89, 0x50, 0x4E, 0x47]));
    }

    [Fact]
    public void ComputeSha256_BuyukHarfHexVeDeterministiktir()
    {
        var bytes = PngHeader;
        var first = PosReceiptPersistenceService.ComputeSha256(bytes);
        var second = PosReceiptPersistenceService.ComputeSha256(bytes);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.Matches("^[0-9A-F]{64}$", first);
    }
}
