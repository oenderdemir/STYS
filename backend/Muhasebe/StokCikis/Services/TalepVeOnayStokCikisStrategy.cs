using STYS.Muhasebe.StokCikis.Dtos;
using STYS.Muhasebe.StokTalepleri.Dtos;
using STYS.Muhasebe.StokTalepleri.Services;
using STYS.Tesisler;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.StokCikis.Services;

public class TalepVeOnayStokCikisStrategy : IStokCikisStrategy
{
    private readonly IStokTalepService _stokTalepService;

    public TalepVeOnayStokCikisStrategy(IStokTalepService stokTalepService)
    {
        _stokTalepService = stokTalepService;
    }

    public string Yontem => StokCikisYontemleri.TalepVeOnay;

    public async Task<StokCikisSonuc> BaslatAsync(StokCikisIstegi istek, CancellationToken cancellationToken = default)
    {
        if (istek.Talep is null)
        {
            throw new BaseException("Bu tesiste stok çıkışı talep ve onay akışıyla yürütülmelidir.", 400);
        }

        var talep = await _stokTalepService.AddAsync(new StokTalepDto
        {
            TesisId = istek.TesisId,
            TalepEdenDepoId = istek.Talep.TalepEdenDepoId,
            KarsilayanDepoId = istek.Talep.KarsilayanDepoId,
            TalepTarihi = istek.Talep.TalepTarihi,
            Aciklama = istek.Talep.Aciklama
        });

        return new StokCikisSonuc
        {
            Talep = talep
        };
    }
}
