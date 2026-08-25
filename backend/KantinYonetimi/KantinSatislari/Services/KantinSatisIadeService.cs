using System.Data;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.KantinYonetimi.KantinSatislari.Dtos;
using STYS.KantinYonetimi.KantinSatislari.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokHareketleri.Services;
using STYS.Muhasebe.StokMaliyetPolitikalari.Services;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.KantinYonetimi.KantinSatislari.Services;

public class KantinSatisIadeService : IKantinSatisIadeService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IStokHareketService _stokHareketService;
    private readonly IStokMaliyetKatmaniRestoreService _stokMaliyetKatmaniRestoreService;

    public KantinSatisIadeService(
        StysAppDbContext dbContext,
        IUserAccessScopeService userAccessScopeService,
        ICurrentUserAccessor currentUserAccessor,
        IStokHareketService stokHareketService,
        IStokMaliyetKatmaniRestoreService stokMaliyetKatmaniRestoreService)
    {
        _dbContext = dbContext;
        _userAccessScopeService = userAccessScopeService;
        _currentUserAccessor = currentUserAccessor;
        _stokHareketService = stokHareketService;
        _stokMaliyetKatmaniRestoreService = stokMaliyetKatmaniRestoreService;
    }

    public async Task<KantinSatisIadeDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.KantinSatisIadeleri
            .AsNoTracking()
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        await EnsureTesisAccessAsync(entity.TesisId, cancellationToken);
        return await MapDtoAsync(entity, cancellationToken);
    }

    public async Task<KantinSatisIadeDto> CreateAsync(CreateKantinSatisIadeRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Satirlar.Count == 0)
        {
            throw new BaseException("İade için en az bir satır seçilmelidir.", 400);
        }

        var satis = await _dbContext.KantinSatislar
            .AsNoTracking()
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
                .ThenInclude(x => x.StokHareket)
            .FirstOrDefaultAsync(x => x.Id == request.KantinSatisId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Kantin satışı bulunamadı.", 404);

        await EnsureTesisAccessAsync(satis.TesisId, cancellationToken);

        if (!string.Equals(satis.Durum, KantinSatisDurumlari.Kesinlesti, StringComparison.Ordinal))
        {
            throw new BaseException("Yalnızca kesinleşmiş satışlardan iade yapılabilir.", 400);
        }

        var iade = new KantinSatisIade
        {
            TesisId = satis.TesisId,
            KantinSatisId = satis.Id,
            IadeTarihi = DateTime.UtcNow,
            Durum = KantinSatisIadeDurumlari.Taslak,
            Aciklama = NormalizeOptional(request.Aciklama, 1024),
            OlusturanKullaniciId = _currentUserAccessor.GetCurrentUserId()?.ToString(),
            FinansalIadeDurumu = KantinSatisIadeFinansalDurumlari.Bekliyor,
            Satirlar = []
        };

        foreach (var satirRequest in request.Satirlar)
        {
            var originalSatir = satis.Satirlar.FirstOrDefault(x => x.Id == satirRequest.KantinSatisSatirId)
                ?? throw new BaseException("İade için seçilen satış satırı bu satışa ait değil.", 400);

            if (satirRequest.Miktar <= 0)
            {
                throw new BaseException("İade miktarı 0'dan büyük olmalıdır.", 400);
            }

            if (satirRequest.Miktar > originalSatir.Miktar)
            {
                throw new BaseException("İade miktarı satılan miktarı aşamaz.", 400);
            }

            var originalMovement = originalSatir.StokHareket
                ?? throw new BaseException("Satış satırına bağlı stok hareketi bulunamadı.", 400);

            var maliyetBirimFiyat = originalMovement.MaliyetBirimFiyat;
            var maliyetTutari = maliyetBirimFiyat.HasValue
                ? ParaTutarYuvarlamaHelper.Yuvarla(satirRequest.Miktar * maliyetBirimFiyat.Value)
                : (decimal?)null;

            iade.Satirlar.Add(new KantinSatisIadeSatir
            {
                KantinSatisSatirId = originalSatir.Id,
                Miktar = satirRequest.Miktar,
                TasinirKartId = originalSatir.TasinirKartId,
                StokKodu = originalSatir.StokKodu,
                UrunAdi = originalSatir.UrunAdi,
                Birim = originalSatir.Birim,
                TakipTipi = originalSatir.TakipTipi,
                LotNo = originalSatir.LotNo,
                SeriNo = originalSatir.SeriNo,
                BirimSatisFiyati = originalSatir.BirimSatisFiyati,
                KdvOrani = originalSatir.KdvOrani,
                MaliyetBirimFiyat = maliyetBirimFiyat,
                MaliyetTutari = maliyetTutari
            });
        }

        _dbContext.KantinSatisIadeleri.Add(iade);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(iade.Id, cancellationToken)
            ?? throw new BaseException("İade oluşturulamadı.", 500);
    }

    public async Task<List<KantinSatisIadeOzetDto>> GetSatisIadeOzetiAsync(int kantinSatisId, CancellationToken cancellationToken = default)
    {
        var satis = await _dbContext.KantinSatislar
            .AsNoTracking()
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == kantinSatisId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Kantin satışı bulunamadı.", 404);

        await EnsureTesisAccessAsync(satis.TesisId, cancellationToken);

        var sonuc = new List<KantinSatisIadeOzetDto>();
        foreach (var satir in satis.Satirlar)
        {
            var oncekiIade = await GetKesinlesmisIadeToplamiAsync(satir.Id, mevcutIadeId: null, cancellationToken);
            sonuc.Add(new KantinSatisIadeOzetDto
            {
                KantinSatisSatirId = satir.Id,
                SatilanMiktar = satir.Miktar,
                OncekiIadeMiktari = oncekiIade,
                KalanMiktar = satir.Miktar - oncekiIade
            });
        }

        return sonuc;
    }

    public async Task<KantinSatisIadeDto> KesinlestirAsync(int id, CancellationToken cancellationToken = default)
    {
        var iade = await _dbContext.KantinSatisIadeleri
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("İade bulunamadı.", 404);

        await EnsureTesisAccessAsync(iade.TesisId, cancellationToken);

        if (string.Equals(iade.Durum, KantinSatisIadeDurumlari.Kesinlesti, StringComparison.Ordinal))
        {
            return await MapDtoAsync(iade, cancellationToken);
        }

        if (!string.Equals(iade.Durum, KantinSatisIadeDurumlari.Taslak, StringComparison.Ordinal))
        {
            throw new BaseException("Yalnızca taslak iadeler kesinleştirilebilir.", 400);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var satis = await _dbContext.KantinSatislar
                .AsNoTracking()
                .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
                    .ThenInclude(x => x.StokHareket)
                .FirstOrDefaultAsync(x => x.Id == iade.KantinSatisId && !x.IsDeleted, cancellationToken)
                ?? throw new BaseException("Kantin satışı bulunamadı.", 404);

            if (!string.Equals(satis.Durum, KantinSatisDurumlari.Kesinlesti, StringComparison.Ordinal))
            {
                throw new BaseException("Yalnızca kesinleşmiş satışlardan iade yapılabilir.", 400);
            }

            var iadeZamani = DateTime.UtcNow;

            foreach (var iadeSatir in iade.Satirlar.OrderBy(x => x.Id))
            {
                var originalSatir = satis.Satirlar.FirstOrDefault(x => x.Id == iadeSatir.KantinSatisSatirId)
                    ?? throw new BaseException("İade satırına karşılık gelen satış satırı bulunamadı.", 400);

                var originalMovement = originalSatir.StokHareket
                    ?? throw new BaseException("Satış satırına bağlı stok hareketi bulunamadı.", 400);

                if (originalSatir.StokSeriId.HasValue)
                {
                    var oncekiSeriIade = await GetKesinlesmisIadeToplamiAsync(iadeSatir.KantinSatisSatirId, iade.Id, cancellationToken);
                    if (oncekiSeriIade > 0)
                    {
                        throw new BaseException("Seri takipli ürün yalnızca bir kez iade edilebilir.", 400);
                    }
                }

                var oncekiIadeToplami = await GetKesinlesmisIadeToplamiAsync(iadeSatir.KantinSatisSatirId, iade.Id, cancellationToken);
                if (oncekiIadeToplami + iadeSatir.Miktar > originalSatir.Miktar)
                {
                    throw new BaseException("Kümülatif iade miktarı satış miktarını aşamaz.", 400);
                }

                var iadeMovement = await _stokHareketService.AddWithinCurrentTransactionAsync(
                    BuildIadeStokHareketDto(iade, iadeSatir, originalMovement, iadeZamani),
                    cancellationToken);

                await _stokMaliyetKatmaniRestoreService.RestorePartialLayeredCostIfNeededAsync(originalMovement, iadeMovement, iadeSatir.Miktar, cancellationToken);
                iadeSatir.StokHareketId = iadeMovement.Id;
            }

            iade.Durum = KantinSatisIadeDurumlari.Kesinlesti;
            iade.KesinlesmeTarihi = iadeZamani;
            iade.FinansalIadeDurumu = KantinSatisIadeFinansalDurumlari.Bekliyor;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return await GetByIdAsync(iade.Id, cancellationToken)
                ?? throw new BaseException("İade kesinleştirilemedi.", 500);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<decimal> GetKesinlesmisIadeToplamiAsync(int kantinSatisSatirId, int? mevcutIadeId, CancellationToken cancellationToken)
    {
        var query = _dbContext.KantinSatisIadeSatirlari
            .AsNoTracking()
            .Where(x =>
                x.KantinSatisSatirId == kantinSatisSatirId
                && x.KantinSatisIade != null
                && x.KantinSatisIade.Durum == KantinSatisIadeDurumlari.Kesinlesti);

        if (mevcutIadeId.HasValue)
        {
            query = query.Where(x => x.KantinSatisIadeId != mevcutIadeId.Value);
        }

        var toplam = await query.SumAsync(x => (decimal?)x.Miktar, cancellationToken);
        return toplam ?? 0m;
    }

    private static StokHareketDto BuildIadeStokHareketDto(KantinSatisIade iade, KantinSatisIadeSatir iadeSatir, StokHareket originalMovement, DateTime iadeZamani)
        => new()
        {
            DepoId = originalMovement.DepoId,
            TasinirKartId = originalMovement.TasinirKartId,
            HareketTarihi = iadeZamani,
            HareketTipi = StokHareketTipleri.Iade,
            Miktar = iadeSatir.Miktar,
            BirimFiyat = originalMovement.BirimFiyat,
            Tutar = 0,
            BelgeTarihi = iadeZamani,
            Aciklama = $"Kantin Satış İadesi #{iade.Id} - {iadeSatir.StokKodu} {iadeSatir.UrunAdi}",
            KaynakModul = MuhasebeKaynakModulleri.KantinSatisIadeSatir,
            KaynakId = iadeSatir.Id,
            Durum = StokHareketDurumlari.Aktif,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.KdvKapsamDisi,
            KdvOrani = 0,
            KdvTutari = 0,
            MaliyetBirimFiyat = iadeSatir.MaliyetBirimFiyat,
            MaliyetTutari = iadeSatir.MaliyetTutari,
            StokLotId = originalMovement.StokLotId,
            StokSeriId = originalMovement.StokSeriId
        };

    private async Task<KantinSatisIadeDto> MapDtoAsync(KantinSatisIade entity, CancellationToken cancellationToken)
    {
        var satirDtos = new List<KantinSatisIadeSatirDto>();
        foreach (var satir in entity.Satirlar.OrderBy(x => x.Id))
        {
            var originalSatir = await _dbContext.KantinSatisSatirlari
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == satir.KantinSatisSatirId, cancellationToken);

            var oncekiIadeToplami = await GetKesinlesmisIadeToplamiAsync(satir.KantinSatisSatirId, entity.Id, cancellationToken);
            var satilanMiktar = originalSatir?.Miktar ?? 0m;

            satirDtos.Add(new KantinSatisIadeSatirDto
            {
                Id = satir.Id,
                KantinSatisIadeId = satir.KantinSatisIadeId,
                KantinSatisSatirId = satir.KantinSatisSatirId,
                Miktar = satir.Miktar,
                TasinirKartId = satir.TasinirKartId,
                StokKodu = satir.StokKodu,
                UrunAdi = satir.UrunAdi,
                Birim = satir.Birim,
                TakipTipi = satir.TakipTipi,
                LotNo = satir.LotNo,
                SeriNo = satir.SeriNo,
                BirimSatisFiyati = satir.BirimSatisFiyati,
                KdvOrani = satir.KdvOrani,
                MaliyetBirimFiyat = satir.MaliyetBirimFiyat,
                MaliyetTutari = satir.MaliyetTutari,
                StokHareketId = satir.StokHareketId,
                SatilanMiktar = satilanMiktar,
                OncekiIadeMiktari = oncekiIadeToplami,
                KalanMiktar = satilanMiktar - oncekiIadeToplami
            });
        }

        return new KantinSatisIadeDto
        {
            Id = entity.Id,
            TesisId = entity.TesisId,
            KantinSatisId = entity.KantinSatisId,
            IadeTarihi = entity.IadeTarihi,
            Durum = entity.Durum,
            Aciklama = entity.Aciklama,
            OlusturanKullaniciId = entity.OlusturanKullaniciId,
            KesinlesmeTarihi = entity.KesinlesmeTarihi,
            FinansalIadeDurumu = entity.FinansalIadeDurumu,
            Satirlar = satirDtos
        };
    }

    private async Task EnsureTesisAccessAsync(int tesisId, CancellationToken cancellationToken)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && !scope.TesisIds.Contains(tesisId))
        {
            throw new BaseException("Bu tesis için yetkiniz bulunmuyor.", 403);
        }
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized is null)
        {
            return null;
        }

        return normalized.Length > maxLength ? normalized[..maxLength] : normalized;
    }
}
