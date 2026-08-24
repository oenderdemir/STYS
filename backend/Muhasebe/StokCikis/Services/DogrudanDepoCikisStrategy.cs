using STYS.Muhasebe.StokCikis.Dtos;
using STYS.Muhasebe.StokHareketleri.Services;
using STYS.Tesisler;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.StokCikis.Services;

public class DogrudanDepoCikisStrategy : IStokCikisStrategy
{
    private readonly IStokHareketService _stokHareketService;

    public DogrudanDepoCikisStrategy(IStokHareketService stokHareketService)
    {
        _stokHareketService = stokHareketService;
    }

    public string Yontem => StokCikisYontemleri.DogrudanDepoCikisi;

    public async Task<StokCikisSonuc> BaslatAsync(StokCikisIstegi istek, CancellationToken cancellationToken = default)
    {
        if (istek.Transfer is null)
        {
            throw new BaseException("Bu tesiste stok talepleri yerine doğrudan depo çıkışı kullanılmalıdır.", 400);
        }

        var transfer = await _stokHareketService.CreateTransferAsync(istek.Transfer, cancellationToken);
        return new StokCikisSonuc
        {
            TransferHareketleri = transfer
        };
    }
}
