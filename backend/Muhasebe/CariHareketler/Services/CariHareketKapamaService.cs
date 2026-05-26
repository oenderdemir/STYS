using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariHareketler.Dtos;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariHareketler.Repositories;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Repositories;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.CariHareketler.Services;

public class CariHareketKapamaService : ICariHareketKapamaService
{
    private const string KaynakModulAdi = "TahsilatOdemeBelgesi";

    private readonly StysAppDbContext _dbContext;
    private readonly ITahsilatOdemeBelgesiRepository _tahsilatOdemeBelgesiRepository;
    private readonly ICariHareketRepository _cariHareketRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly IMapper _mapper;

    public CariHareketKapamaService(
        StysAppDbContext dbContext,
        ITahsilatOdemeBelgesiRepository tahsilatOdemeBelgesiRepository,
        ICariHareketRepository cariHareketRepository,
        IUserAccessScopeService userAccessScopeService,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _tahsilatOdemeBelgesiRepository = tahsilatOdemeBelgesiRepository;
        _cariHareketRepository = cariHareketRepository;
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
            throw new BaseException("Tahsilat/odeme belgesi bulunamadi.", 404);
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
            && x.KaynakModul == KaynakModulAdi
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
            throw new BaseException("Kapatilacak cari hareket bulunamadi.", 400);
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
            throw new BaseException("Kapama tutarı kalan tutardan büyük olamaz.", 400);
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
            KaynakModul = KaynakModulAdi,
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
}
