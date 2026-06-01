using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariHareketler.Dtos;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariHareketler.Repositories;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Repositories;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.CariHareketler.Services;

public class CariHareketKapamaService : ICariHareketKapamaService
{
    private readonly StysAppDbContext _dbContext;
    private readonly ITahsilatOdemeBelgesiRepository _tahsilatOdemeBelgesiRepository;
    private readonly ICariHareketRepository _cariHareketRepository;
    private readonly IMuhasebeDonemService _muhasebeDonemService;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly IMapper _mapper;

    public CariHareketKapamaService(
        StysAppDbContext dbContext,
        ITahsilatOdemeBelgesiRepository tahsilatOdemeBelgesiRepository,
        ICariHareketRepository cariHareketRepository,
        IMuhasebeDonemService muhasebeDonemService,
        IUserAccessScopeService userAccessScopeService,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _tahsilatOdemeBelgesiRepository = tahsilatOdemeBelgesiRepository;
        _cariHareketRepository = cariHareketRepository;
        _muhasebeDonemService = muhasebeDonemService;
        _userAccessScopeService = userAccessScopeService;
        _mapper = mapper;
    }

    public async Task<CariHareketDto?> TahsilatOdemeIcinCariHareketOlusturVeKapatAsync(
        int tahsilatOdemeBelgesiId,
        CancellationToken cancellationToken = default)
    {
        var belge = await _tahsilatOdemeBelgesiRepository.GetByIdAsync(
            tahsilatOdemeBelgesiId,
            q => q.Include(x => x.CariKart));

        if (belge is null)
        {
            throw new BaseException("Tahsilat/ödeme belgesi bulunamadı.", 404);
        }

        if (belge.CariKartId <= 0)
        {
            throw new BaseException("Cari kart secimi zorunludur.", 400);
        }

        if (belge.Tutar <= 0m)
        {
            throw new BaseException("Tutar sifirdan buyuk olmali.", 400);
        }

        if (!belge.KapatilacakCariHareketId.HasValue)
        {
            return null;
        }

        var duplicate = await _dbContext.CariHareketler.AnyAsync(x =>
            !x.IsDeleted
            && x.Durum == CariHareketDurumlari.Aktif
            && x.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi
            && x.KaynakId == belge.Id,
            cancellationToken);

        if (duplicate)
        {
            throw new BaseException("Bu tahsilat/ödeme belgesi için cari hareket daha önce oluşturulmuş.", 400);
        }

        var kapatilacak = await _cariHareketRepository.GetByIdAsync(
            belge.KapatilacakCariHareketId.Value,
            q => q.Include(x => x.CariKart));

        if (kapatilacak is null)
        {
            throw new BaseException("Kapama ilişkisi bulunamadı.", 400);
        }

        if (kapatilacak.IsDeleted || kapatilacak.Durum != CariHareketDurumlari.Aktif)
        {
            throw new BaseException("Kapatilacak cari hareket aktif degil.", 400);
        }

        if (kapatilacak.KapandiMi || kapatilacak.KalanTutar <= 0m)
        {
            throw new BaseException("Kapatilacak cari hareket kapali.", 400);
        }

        if (kapatilacak.CariKartId != belge.CariKartId)
        {
            throw new BaseException("Kapatilacak cari hareket secilen cari kart ile uyumlu degil.", 400);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped)
        {
            var cariTesisId = kapatilacak.CariKart?.TesisId;
            if (!cariTesisId.HasValue || !scope.TesisIds.Contains(cariTesisId.Value))
            {
                throw new BaseException("Kapatilacak cari hareket icin yetkiniz bulunmuyor.", 403);
            }
        }

        if (belge.Tutar > kapatilacak.KalanTutar + 0.01m)
        {
            throw new BaseException("Kapama geri alma tutarı cari hareketle uyumsuz.", 400);
        }

        var kapamaTutari = Math.Min(belge.Tutar, kapatilacak.KalanTutar);
        var borcMu = kapatilacak.BorcTutari > 0m;

        var yeniHareket = new CariHareket
        {
            CariKartId = belge.CariKartId,
            HareketTarihi = belge.BelgeTarihi,
            BelgeTuru = belge.BelgeTipi,
            BelgeNo = belge.BelgeNo,
            Aciklama = $"Tahsilat/ödeme cari kapama - {belge.BelgeNo}",
            BorcTutari = borcMu ? 0m : kapamaTutari,
            AlacakTutari = borcMu ? kapamaTutari : 0m,
            KapananTutar = 0m,
            KalanTutar = 0m,
            ParaBirimi = belge.ParaBirimi,
            VadeTarihi = null,
            Durum = CariHareketDurumlari.Aktif,
            KaynakModul = MuhasebeKaynakModulleri.TahsilatOdemeBelgesi,
            KaynakId = belge.Id,
            IliskiliCariHareketId = kapatilacak.Id,
            KapandiMi = true
        };

        kapatilacak.KapananTutar += kapamaTutari;
        kapatilacak.KalanTutar -= kapamaTutari;
        if (kapatilacak.KalanTutar <= 0.01m)
        {
            kapatilacak.KalanTutar = 0m;
            kapatilacak.KapandiMi = true;
        }
        else
        {
            kapatilacak.KapandiMi = false;
        }

        _dbContext.CariHareketler.Add(yeniHareket);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return _mapper.Map<CariHareketDto>(yeniHareket);
    }

    public async Task GeriAlAsync(int tahsilatOdemeBelgesiId, CancellationToken cancellationToken = default)
    {
        var ownsTransaction = _dbContext.Database.CurrentTransaction is null;
        var transaction = ownsTransaction
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            var belge = await _tahsilatOdemeBelgesiRepository.GetByIdAsync(
                tahsilatOdemeBelgesiId,
                q => q.Include(x => x.CariKart));

            if (belge is null)
            {
                throw new BaseException("Tahsilat/ödeme belgesi bulunamadı.", 404);
            }

            if (belge.Durum == TahsilatOdemeBelgeDurumlari.Iptal)
            {
                throw new BaseException("Tahsilat/ödeme belgesi zaten iptal edilmiş.", 400);
            }

            if (belge.CariKartId <= 0)
            {
                throw new BaseException("Cari kart secimi zorunludur.", 400);
            }

            await MuhasebeDonemKontrolHelper.EnsureOpenPeriodAsync(
                _muhasebeDonemService,
                belge.CariKart?.TesisId,
                belge.BelgeTarihi,
                cancellationToken);

            var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
            if (scope.IsScoped)
            {
                var tesisId = belge.CariKart?.TesisId;
                if (!tesisId.HasValue || !scope.TesisIds.Contains(tesisId.Value))
                {
                    throw new BaseException("Tahsilat/ödeme belgesi için yetkiniz bulunmuyor.", 403);
                }
            }

            var kapamaHareket = await _dbContext.CariHareketler
                .Include(x => x.CariKart)
                .Include(x => x.IliskiliCariHareket)
                .FirstOrDefaultAsync(x =>
                    !x.IsDeleted
                    && x.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi
                    && x.KaynakId == belge.Id,
                    cancellationToken);

            if (kapamaHareket is null)
            {
                throw new BaseException("Bu tahsilat/ödeme belgesi için geri alınacak cari hareket bulunamadi.", 404);
            }

            if (kapamaHareket.Durum != CariHareketDurumlari.Aktif)
            {
                throw new BaseException("Kapama hareketi zaten geri alinmis.", 400);
            }

            if (!kapamaHareket.IliskiliCariHareketId.HasValue)
            {
                throw new BaseException("Kapama ilişkisi bulunamadı.", 400);
            }

            var faturaHareket = kapamaHareket.IliskiliCariHareket;
            if (faturaHareket is null)
            {
                faturaHareket = await _dbContext.CariHareketler
                    .Include(x => x.CariKart)
                    .FirstOrDefaultAsync(x => x.Id == kapamaHareket.IliskiliCariHareketId.Value, cancellationToken);
            }

            if (faturaHareket is null)
            {
                throw new BaseException("Kapama ilişkisi bulunamadı.", 404);
            }

            if (faturaHareket.IsDeleted || faturaHareket.Durum != CariHareketDurumlari.Aktif)
            {
                throw new BaseException("Ilişkili cari hareket aktif degil.", 400);
            }

            if (faturaHareket.CariKartId != belge.CariKartId)
            {
                throw new BaseException("Ilişkili cari hareket secilen cari kart ile uyumlu degil.", 400);
            }

            if (scope.IsScoped)
            {
                var faturaTesisId = faturaHareket.CariKart?.TesisId;
                if (!faturaTesisId.HasValue || !scope.TesisIds.Contains(faturaTesisId.Value))
                {
                    throw new BaseException("Ilişkili cari hareket icin yetkiniz bulunmuyor.", 403);
                }
            }

            var kapamaTutari = kapamaHareket.BorcTutari > 0m ? kapamaHareket.BorcTutari : kapamaHareket.AlacakTutari;
            if (kapamaTutari <= 0m)
            {
                throw new BaseException("Kapama tutari gecersiz.", 400);
            }

            if (kapamaTutari > faturaHareket.KapananTutar + 0.01m)
            {
                throw new BaseException("Kapama geri alma tutarı cari hareketle uyumsuz.", 400);
            }

            var orijinalTutar = faturaHareket.BorcTutari > 0m ? faturaHareket.BorcTutari : faturaHareket.AlacakTutari;
            faturaHareket.KapananTutar = Math.Max(0m, faturaHareket.KapananTutar - kapamaTutari);
            faturaHareket.KalanTutar = Math.Min(orijinalTutar, Math.Max(0m, faturaHareket.KalanTutar + kapamaTutari));
            faturaHareket.KapandiMi = faturaHareket.KalanTutar <= 0.01m;

            kapamaHareket.Durum = CariHareketDurumlari.Iptal;
            kapamaHareket.KapananTutar = 0m;
            kapamaHareket.KalanTutar = 0m;
            kapamaHareket.KapandiMi = true;
            kapamaHareket.IliskiliCariHareketId = null;
            kapamaHareket.IliskiliCariHareket = null;
            belge.Durum = TahsilatOdemeBelgeDurumlari.Iptal;
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (ownsTransaction && transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (ownsTransaction && transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
    }
}
