using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Depolar.Repositories;
using STYS.Muhasebe.StokCikis.Dtos;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokTalepleri.Dtos;
using STYS.Tesisler;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.StokCikis.Services;

public class StokCikisService : IStokCikisService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IDepoRepository _depoRepository;
    private readonly IStokCikisStrategyResolver _strategyResolver;

    public StokCikisService(StysAppDbContext dbContext, IDepoRepository depoRepository, IStokCikisStrategyResolver strategyResolver)
    {
        _dbContext = dbContext;
        _depoRepository = depoRepository;
        _strategyResolver = strategyResolver;
    }

    public async Task<StokTalepDto> TalepBaslatAsync(CreateStokTalepRequest request, CancellationToken cancellationToken = default)
    {
        var istek = new StokCikisIstegi
        {
            TesisId = await ResolveTesisIdAsync(request.TalepEdenDepoId, request.KarsilayanDepoId),
            Talep = request
        };

        var sonuc = await ExecuteAsync(istek, cancellationToken);
        return sonuc.Talep ?? throw new BaseException("Stok talebi oluşturulamadı.", 500);
    }

    public async Task<IReadOnlyList<StokHareketDto>> DogrudanTransferBaslatAsync(StokTransferRequest request, CancellationToken cancellationToken = default)
    {
        var istek = new StokCikisIstegi
        {
            TesisId = await ResolveTesisIdAsync(request.KaynakDepoId, request.HedefDepoId),
            Transfer = request
        };

        var sonuc = await ExecuteAsync(istek, cancellationToken);
        return sonuc.TransferHareketleri ?? throw new BaseException("Doğrudan depo çıkışı oluşturulamadı.", 500);
    }

    private async Task<StokCikisSonuc> ExecuteAsync(StokCikisIstegi istek, CancellationToken cancellationToken)
    {
        var tesis = await _dbContext.Tesisler.FindAsync([istek.TesisId], cancellationToken)
            ?? throw new BaseException("Tesis bulunamadı.", 404);

        if (!StokCikisYontemleri.IsValid(tesis.StokCikisYontemi))
        {
            throw new BaseException($"Bilinmeyen stok çıkış yöntemi: {tesis.StokCikisYontemi}", 400);
        }

        var strategy = _strategyResolver.Resolve(tesis.StokCikisYontemi);
        return await strategy.BaslatAsync(istek, cancellationToken);
    }

    private async Task<int> ResolveTesisIdAsync(int ilkDepoId, int ikinciDepoId)
    {
        var ilkDepo = await _depoRepository.GetByIdAsync(ilkDepoId)
            ?? throw new BaseException("Depo bulunamadı.", 400);
        var ikinciDepo = await _depoRepository.GetByIdAsync(ikinciDepoId)
            ?? throw new BaseException("Depo bulunamadı.", 400);

        if (!ilkDepo.TesisId.HasValue || !ikinciDepo.TesisId.HasValue || ilkDepo.TesisId.Value != ikinciDepo.TesisId.Value)
        {
            throw new BaseException("İşlem yalnızca aynı tesise ait depolar arasında yapılabilir.", 400);
        }

        return ilkDepo.TesisId.Value;
    }
}
