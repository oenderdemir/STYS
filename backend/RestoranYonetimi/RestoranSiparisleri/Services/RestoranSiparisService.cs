using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using STYS.Infrastructure.EntityFramework;
using STYS.RestoranMasalari.Entities;
using STYS.RestoranMasalari.Repositories;
using STYS.RestoranMenuUrunleri.Entities;
using STYS.RestoranMenuUrunleri.Repositories;
using STYS.Restoranlar.Repositories;
using STYS.RestoranSiparisleri.Dtos;
using STYS.RestoranSiparisleri.Entities;
using STYS.RestoranSiparisleri.Repositories;
using STYS.RestoranYonetimi.Services;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.RestoranSiparisleri.Services;

public class RestoranSiparisService : BaseRdbmsService<RestoranSiparisDto, RestoranSiparis, int>, IRestoranSiparisService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IRestoranSiparisRepository _siparisRepository;
    private readonly IRestoranRepository _restoranRepository;
    private readonly IRestoranMasaRepository _masaRepository;
    private readonly IRestoranMenuUrunRepository _menuUrunRepository;
    private readonly IMapper _mapper;
    private readonly IRestoranErisimService _restoranErisimService;

    public RestoranSiparisService(
        StysAppDbContext dbContext,
        IRestoranSiparisRepository siparisRepository,
        IRestoranRepository restoranRepository,
        IRestoranMasaRepository masaRepository,
        IRestoranMenuUrunRepository menuUrunRepository,
        IMapper mapper,
        IRestoranErisimService restoranErisimService)
        : base(siparisRepository, mapper)
    {
        _dbContext = dbContext;
        _siparisRepository = siparisRepository;
        _restoranRepository = restoranRepository;
        _masaRepository = masaRepository;
        _menuUrunRepository = menuUrunRepository;
        _mapper = mapper;
        _restoranErisimService = restoranErisimService;
    }

    public override async Task<IEnumerable<RestoranSiparisDto>> GetAllAsync(Func<IQueryable<RestoranSiparis>, IQueryable<RestoranSiparis>>? include = null)
    {
        IQueryable<RestoranSiparis> query = _siparisRepository.Where(x => true)
            .Include(x => x.Kalemler)
            .Include(x => x.Odemeler);

        var yetkiliRestoranlar = await _restoranErisimService.GetYetkiliRestoranIdleriAsync();
        if (yetkiliRestoranlar is not null)
        {
            query = query.Where(x => yetkiliRestoranlar.Contains(x.RestoranId));
        }

        if (include is not null)
        {
            query = include(query);
        }

        var items = await query
            .OrderByDescending(x => x.SiparisTarihi)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

        return _mapper.Map<List<RestoranSiparisDto>>(items);
    }

    public override async Task<IEnumerable<RestoranSiparisDto>> WhereAsync(
        Expression<Func<RestoranSiparis, bool>> predicate,
        Func<IQueryable<RestoranSiparis>, IQueryable<RestoranSiparis>>? include = null)
    {
        IQueryable<RestoranSiparis> query = _siparisRepository.Where(predicate)
            .Include(x => x.Kalemler)
            .Include(x => x.Odemeler);

        var yetkiliRestoranlar = await _restoranErisimService.GetYetkiliRestoranIdleriAsync();
        if (yetkiliRestoranlar is not null)
        {
            query = query.Where(x => yetkiliRestoranlar.Contains(x.RestoranId));
        }

        if (include is not null)
        {
            query = include(query);
        }

        var items = await query
            .OrderByDescending(x => x.SiparisTarihi)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

        return _mapper.Map<List<RestoranSiparisDto>>(items);
    }

    public async Task<List<RestoranSiparisDto>> GetByRestoranIdAsync(int restoranId, CancellationToken cancellationToken = default)
    {
        await _restoranErisimService.EnsureRestoranErisimiAsync(restoranId, cancellationToken);
        return _mapper.Map<List<RestoranSiparisDto>>(await _siparisRepository.GetByRestoranIdAsync(restoranId, cancellationToken));
    }

    public async Task<List<RestoranSiparisDto>> GetAcikSiparislerAsync(int? masaId, CancellationToken cancellationToken = default)
    {
        var items = await _siparisRepository.GetAcikSiparislerAsync(masaId, cancellationToken);
        var yetkiliRestoranlar = await _restoranErisimService.GetYetkiliRestoranIdleriAsync(cancellationToken);
        if (yetkiliRestoranlar is not null)
        {
            items = items.Where(x => yetkiliRestoranlar.Contains(x.RestoranId)).ToList();
        }

        return _mapper.Map<List<RestoranSiparisDto>>(items);
    }

    public override async Task<RestoranSiparisDto?> GetByIdAsync(int id, Func<IQueryable<RestoranSiparis>, IQueryable<RestoranSiparis>>? include = null)
    {
        var entity = include is null
            ? await _siparisRepository.GetDetayByIdAsync(id)
            : await _siparisRepository.GetByIdAsync(id, include);

        if (entity is not null)
        {
            await _restoranErisimService.EnsureRestoranErisimiAsync(entity.RestoranId);
        }

        return entity is null ? null : _mapper.Map<RestoranSiparisDto>(entity);
    }

    public override async Task<RestoranSiparisDto> AddAsync(RestoranSiparisDto request)
    {
        ValidateCreateRequest(request.RestoranId, request.ParaBirimi, request.Kalemler);

        var restoran = await _restoranRepository.GetByIdAsync(request.RestoranId)
            ?? throw new BaseException("Restoran bulunamadi.", 400);
        await _restoranErisimService.EnsureRestoranErisimiAsync(request.RestoranId);

        if (!restoran.AktifMi)
        {
            throw new BaseException("Pasif restorana siparis acilamaz.", 400);
        }

        RestoranMasa? masa = null;
        if (request.RestoranMasaId.HasValue)
        {
            masa = await ValidateMasaForOrderAsync(restoran.Id, request.RestoranMasaId.Value, CancellationToken.None);
        }

        var urunler = await ResolveAndValidateUrunlerAsync(request.Kalemler.Select(x => x.RestoranMenuUrunId).ToList(), request.ParaBirimi, CancellationToken.None);

        var entity = new RestoranSiparis
        {
            RestoranId = request.RestoranId,
            RestoranMasaId = request.RestoranMasaId,
            SiparisNo = await GenerateSiparisNoAsync(CancellationToken.None),
            SiparisDurumu = RestoranSiparisDurumlari.Taslak,
            ParaBirimi = request.ParaBirimi.Trim().ToUpperInvariant(),
            Notlar = NormalizeOptional(request.Notlar, 1024),
            SiparisTarihi = DateTime.UtcNow,
            ToplamTutar = 0,
            OdenenTutar = 0,
            KalanTutar = 0,
            OdemeDurumu = RestoranSiparisOdemeDurumlari.Odenmedi
        };

        entity.Kalemler = BuildKalemler(request.Kalemler.Select(x => new CreateRestoranSiparisKalemiRequest
        {
            RestoranMenuUrunId = x.RestoranMenuUrunId,
            Miktar = x.Miktar,
            Notlar = x.Notlar
        }).ToList(), urunler);
        RecalculateSiparisTotals(entity);

        await _siparisRepository.AddAsync(entity);

        if (masa is not null)
        {
            masa.Durum = RestoranMasaDurumlari.Dolu;
        }

        await _siparisRepository.SaveChangesAsync();
        return _mapper.Map<RestoranSiparisDto>(entity);
    }

    public override async Task<RestoranSiparisDto> UpdateAsync(RestoranSiparisDto request)
    {
        if (!request.Id.HasValue)
        {
            throw new BaseException("Siparis id zorunludur.", 400);
        }

        var entity = await _siparisRepository.GetDetayByIdAsync(request.Id.Value)
            ?? throw new BaseException("Siparis bulunamadi.", 404);
        await _restoranErisimService.EnsureRestoranErisimiAsync(entity.RestoranId);

        EnsureMutable(entity);

        if (request.RestoranMasaId.HasValue)
        {
            await ValidateMasaForOrderAsync(entity.RestoranId, request.RestoranMasaId.Value, CancellationToken.None, entity.Id);
        }

        if (request.Kalemler.Count == 0)
        {
            throw new BaseException("Sipariste en az bir kalem olmalidir.", 400);
        }

        var urunler = await ResolveAndValidateUrunlerAsync(request.Kalemler.Select(x => x.RestoranMenuUrunId).ToList(), entity.ParaBirimi, CancellationToken.None);

        entity.RestoranMasaId = request.RestoranMasaId;
        entity.Notlar = NormalizeOptional(request.Notlar, 1024);

        _dbContext.RestoranSiparisKalemleri.RemoveRange(entity.Kalemler);
        entity.Kalemler = BuildKalemler(request.Kalemler.Select(x => new CreateRestoranSiparisKalemiRequest
        {
            RestoranMenuUrunId = x.RestoranMenuUrunId,
            Miktar = x.Miktar,
            Notlar = x.Notlar
        }).ToList(), urunler);

        RecalculateSiparisTotals(entity);
        await _siparisRepository.SaveChangesAsync();
        return _mapper.Map<RestoranSiparisDto>(entity);
    }

    public async Task<RestoranSiparisDto> UpdateDurumAsync(int id, UpdateRestoranSiparisDurumRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SiparisDurumu))
        {
            throw new BaseException("Siparis durumu zorunludur.", 400);
        }

        var entity = await _siparisRepository.GetDetayByIdAsync(id, cancellationToken)
            ?? throw new BaseException("Siparis bulunamadi.", 404);
        await _restoranErisimService.EnsureRestoranErisimiAsync(entity.RestoranId, cancellationToken);

        var newStatus = request.SiparisDurumu.Trim();
        ValidateStatusTransition(entity, newStatus);

        if (newStatus == RestoranSiparisDurumlari.Iptal)
        {
            var hasCompletedPayment = entity.Odemeler.Any(x => x.Durum == STYS.RestoranOdemeleri.Entities.RestoranOdemeDurumlari.Tamamlandi);
            if (hasCompletedPayment)
            {
                throw new BaseException("Tamamlanmis odemesi olan siparis iptal edilemez.", 400);
            }
        }

        entity.SiparisDurumu = newStatus;

        if (entity.RestoranMasaId.HasValue)
        {
            var masa = await _masaRepository.GetByIdAsync(entity.RestoranMasaId.Value);
            if (masa is not null)
            {
                if (newStatus is RestoranSiparisDurumlari.Serviste)
                {
                    masa.Durum = RestoranMasaDurumlari.Serviste;
                }
                else if (newStatus is RestoranSiparisDurumlari.Iptal or RestoranSiparisDurumlari.Tamamlandi)
                {
                    masa.Durum = RestoranMasaDurumlari.Musait;
                }
                else
                {
                    masa.Durum = RestoranMasaDurumlari.Dolu;
                }
            }
        }

        await _siparisRepository.SaveChangesAsync();
        return _mapper.Map<RestoranSiparisDto>(entity);
    }

    private static void EnsureMutable(RestoranSiparis siparis)
    {
        if (siparis.SiparisDurumu is RestoranSiparisDurumlari.Tamamlandi or RestoranSiparisDurumlari.Iptal)
        {
            throw new BaseException("Tamamlanan veya iptal edilen siparis degistirilemez.", 400);
        }
    }

    private async Task<RestoranMasa> ValidateMasaForOrderAsync(int restoranId, int masaId, CancellationToken cancellationToken, int? currentSiparisId = null)
    {
        var masa = await _masaRepository.GetByIdAsync(masaId)
            ?? throw new BaseException("Masa bulunamadi.", 400);

        if (masa.RestoranId != restoranId)
        {
            throw new BaseException("Masa secilen restoran ile uyumlu degil.", 400);
        }

        if (!masa.AktifMi)
        {
            throw new BaseException("Aktif olmayan masa siparise baglanamaz.", 400);
        }

        if (masa.Durum == RestoranMasaDurumlari.Kapali)
        {
            throw new BaseException("Kapali masa siparise baglanamaz.", 400);
        }

        var acikSiparis = await _siparisRepository.GetMasaAcikSiparisAsync(masaId, cancellationToken);
        if (acikSiparis is not null && (!currentSiparisId.HasValue || acikSiparis.Id != currentSiparisId.Value))
        {
            throw new BaseException("Bu masa icin acik siparis zaten mevcut.", 400);
        }

        return masa;
    }

    private async Task<Dictionary<int, RestoranMenuUrun>> ResolveAndValidateUrunlerAsync(IReadOnlyCollection<int> urunIds, string paraBirimi, CancellationToken cancellationToken)
    {
        var normalizedCurrency = paraBirimi.Trim().ToUpperInvariant();
        var uniqueIds = urunIds.Distinct().ToList();

        var urunler = await _menuUrunRepository.Where(x => uniqueIds.Contains(x.Id))
            .Include(x => x.RestoranMenuKategori)
            .ThenInclude(x => x!.Restoran)
            .ToListAsync(cancellationToken);

        if (urunler.Count != uniqueIds.Count)
        {
            throw new BaseException("Secilen urunlerden biri bulunamadi.", 400);
        }

        foreach (var urun in urunler)
        {
            if (!urun.AktifMi)
            {
                throw new BaseException($"Pasif urun sipariste kullanilamaz: {urun.Ad}", 400);
            }

            if (urun.RestoranMenuKategori is null || !urun.RestoranMenuKategori.AktifMi)
            {
                throw new BaseException($"Pasif kategori urunu sipariste kullanilamaz: {urun.Ad}", 400);
            }

            if (urun.ParaBirimi != normalizedCurrency)
            {
                throw new BaseException("Siparis para birimi ile urun para birimi uyusmuyor.", 400);
            }
        }

        return urunler.ToDictionary(x => x.Id, x => x);
    }

    private static List<RestoranSiparisKalemi> BuildKalemler(IReadOnlyCollection<CreateRestoranSiparisKalemiRequest> requests, IReadOnlyDictionary<int, RestoranMenuUrun> urunMap)
    {
        var kalemler = new List<RestoranSiparisKalemi>();

        foreach (var request in requests)
        {
            if (!urunMap.TryGetValue(request.RestoranMenuUrunId, out var urun))
            {
                throw new BaseException("Secilen urun bulunamadi.", 400);
            }

            if (request.Miktar <= 0)
            {
                throw new BaseException("Kalem miktari sifirdan buyuk olmalidir.", 400);
            }

            var satirToplam = Math.Round(request.Miktar * urun.Fiyat, 2, MidpointRounding.AwayFromZero);
            kalemler.Add(new RestoranSiparisKalemi
            {
                RestoranMenuUrunId = urun.Id,
                UrunAdiSnapshot = urun.Ad,
                BirimFiyat = urun.Fiyat,
                Miktar = request.Miktar,
                SatirToplam = satirToplam,
                Durum = RestoranSiparisKalemDurumlari.Beklemede,
                Notlar = NormalizeOptional(request.Notlar, 512)
            });
        }

        return kalemler;
    }

    private static void RecalculateSiparisTotals(RestoranSiparis siparis)
    {
        siparis.ToplamTutar = siparis.Kalemler.Sum(x => x.SatirToplam);
        var completedPayments = siparis.Odemeler
            .Where(x => x.Durum == STYS.RestoranOdemeleri.Entities.RestoranOdemeDurumlari.Tamamlandi)
            .Sum(x => x.Tutar);

        siparis.OdenenTutar = completedPayments;
        siparis.KalanTutar = Math.Max(0m, siparis.ToplamTutar - siparis.OdenenTutar);
        siparis.OdemeDurumu = siparis.OdenenTutar <= 0m
            ? RestoranSiparisOdemeDurumlari.Odenmedi
            : siparis.OdenenTutar < siparis.ToplamTutar
                ? RestoranSiparisOdemeDurumlari.KismiOdendi
                : RestoranSiparisOdemeDurumlari.Odendi;
    }

    private static void ValidateCreateRequest(int restoranId, string paraBirimi, IReadOnlyCollection<RestoranSiparisKalemiDto> kalemler)
    {
        if (restoranId <= 0)
        {
            throw new BaseException("Restoran secimi zorunludur.", 400);
        }

        if (kalemler.Count == 0)
        {
            throw new BaseException("Sipariste en az bir kalem olmalidir.", 400);
        }

        if (string.IsNullOrWhiteSpace(paraBirimi) || paraBirimi.Trim().Length != 3)
        {
            throw new BaseException("Siparis para birimi 3 haneli olmalidir.", 400);
        }
    }

    private static void ValidateStatusTransition(RestoranSiparis siparis, string newStatus)
    {
        if (siparis.SiparisDurumu is RestoranSiparisDurumlari.Tamamlandi or RestoranSiparisDurumlari.Iptal)
        {
            throw new BaseException("Tamamlanmis veya iptal edilmis siparisin durumu degistirilemez.", 400);
        }

        var allowed = siparis.SiparisDurumu switch
        {
            RestoranSiparisDurumlari.Taslak => new[] { RestoranSiparisDurumlari.Hazirlaniyor, RestoranSiparisDurumlari.Iptal },
            RestoranSiparisDurumlari.Hazirlaniyor => new[] { RestoranSiparisDurumlari.Hazir, RestoranSiparisDurumlari.Iptal },
            RestoranSiparisDurumlari.Hazir => new[] { RestoranSiparisDurumlari.Serviste, RestoranSiparisDurumlari.Iptal },
            RestoranSiparisDurumlari.Serviste => new[] { RestoranSiparisDurumlari.Tamamlandi, RestoranSiparisDurumlari.Iptal },
            _ => Array.Empty<string>()
        };

        if (!allowed.Contains(newStatus))
        {
            throw new BaseException($"Gecersiz durum gecisi: {siparis.SiparisDurumu} -> {newStatus}", 400);
        }
    }

    private async Task<string> GenerateSiparisNoAsync(CancellationToken cancellationToken)
    {
        var datePrefix = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = $"RSP-{datePrefix}-";

        var last = await _siparisRepository.Where(x => x.SiparisNo.StartsWith(prefix))
            .OrderByDescending(x => x.Id)
            .Select(x => x.SiparisNo)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (!string.IsNullOrWhiteSpace(last))
        {
            var parts = last.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 3 && int.TryParse(parts[2], out var current))
            {
                next = current + 1;
            }
        }

        return $"{prefix}{next:D4}";
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
