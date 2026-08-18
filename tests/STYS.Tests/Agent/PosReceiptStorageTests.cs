using Microsoft.Extensions.Options;
using STYS.Entegrasyonlar.Pos.Options;
using STYS.Entegrasyonlar.Pos.Services;

namespace STYS.Tests.Agent;

public sealed class PosReceiptStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"stys-receipt-{Guid.NewGuid():N}");
    private readonly PosReceiptStorage _storage;

    public PosReceiptStorageTests()
    {
        Directory.CreateDirectory(_root);
        _storage = new PosReceiptStorage(Options.Create(new PosReceiptStorageOptions { RootPath = _root }));
    }

    [Fact]
    public async Task StoreAsync_ImmutableIsimler_FarkliDosyalarOlusturur()
    {
        var bytes = Png(1);

        var pathA = await _storage.StoreAsync(1, 100, "customer-A.png", bytes, CancellationToken.None);
        var pathB = await _storage.StoreAsync(1, 100, "customer-B.png", bytes, CancellationToken.None);

        Assert.NotEqual(pathA, pathB);
        Assert.True(File.Exists(Path.Combine(_root, pathA)));
        Assert.True(File.Exists(Path.Combine(_root, pathB)));
    }

    [Fact]
    public async Task StoreAsync_OkunanByte_Aynidir()
    {
        var bytes = Png(7);
        var path = await _storage.StoreAsync(1, 100, "merchant-X.png", bytes, CancellationToken.None);

        using var stream = _storage.OpenRead(path);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);

        Assert.Equal(bytes, ms.ToArray());
    }

    [Fact]
    public void OpenRead_KokDisiYol_Reddedilir()
    {
        Assert.ThrowsAny<Exception>(() => _storage.OpenRead("../disari.png"));
    }

    [Fact]
    public void Delete_BilinmeyenYol_HataVermez()
    {
        _storage.Delete("yok/boyle.png");
        _storage.Delete(null);
    }

    private static byte[] Png(byte filler) =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, filler, filler, filler];

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
        }
    }
}
