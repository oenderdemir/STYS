using System.Diagnostics;
using System.Globalization;
using AutoMapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STYS.AccessScope;
using TOD.Platform.AspNetCore.Logging;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Services;
using STYS.Muhasebe.MuhasebeFisleri.Repositories;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Repositories;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.StokHareketleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

public class SatisBelgesiService : BaseRdbmsService<SatisBelgesiDto, SatisBelgesi, int>, ISatisBelgesiService
{
    private readonly StysAppDbContext _db;
    private readonly ISatisBelgesiRepository _satisBelgesiRepository;
    private readonly IMuhasebeFisRepository _muhasebeFisRepository;
    private readonly IMuhasebeFisService _muhasebeFisService;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly ILogger<SatisBelgesiService> _logger;
    private readonly IDomainOperationLogger _domainLogger;

    /// <summary>Satış belgesi satırlarında desteklenen KDV uygulama tipleri.</summary>
    private static readonly HashSet<int> DesteklenenKdvUygulamaTipleri =
    [
        (int)KdvUygulamaTipi.Kdvli,
        (int)KdvUygulamaTipi.TamIstisna,
        (int)KdvUygulamaTipi.KismiIstisna,
        (int)KdvUygulamaTipi.KdvKapsamDisi,
        (int)KdvUygulamaTipi.Tevkifatli
    ];

    /// <summary>KDV hesaplaması yapılmayan uygulama tipleri.</summary>
    private static readonly HashSet<int> KdvHesaplanmayanTipler =
    [
        (int)KdvUygulamaTipi.TamIstisna,
        (int)KdvUygulamaTipi.KismiIstisna,
        (int)KdvUygulamaTipi.KdvKapsamDisi
    ];

    /// <summary>Desteklenen tevkifat oranları.</summary>
    private static readonly HashSet<(int Pay, int Payda)> DesteklenenTevkifatOranlari =
    [
        (2, 10),
        (3, 10),
        (4, 10),
        (5, 10),
        (7, 10),
        (9, 10),
        (10, 10)
    ];

    /// <summary>Satır güncellenebilir durumlar.</summary>
    private static readonly HashSet<int> GuncellenebilirDurumlar =
    [
        (int)SatisBelgesiDurumu.Taslak,
        (int)SatisBelgesiDurumu.Reddedildi
    ];

    /// <summary>Silinebilir durumlar.</summary>
    private static readonly HashSet<int> SilinebilirDurumlar =
    [
        (int)SatisBelgesiDurumu.Taslak
    ];

    public SatisBelgesiService(
        ISatisBelgesiRepository satisBelgesiRepository,
        StysAppDbContext db,
        IMapper mapper,
        IMuhasebeFisRepository muhasebeFisRepository,
        IMuhasebeFisService muhasebeFisService,
        IUserAccessScopeService userAccessScopeService,
        ILogger<SatisBelgesiService> logger,
        IDomainOperationLogger domainLogger)
        : base(satisBelgesiRepository, mapper)
    {
        _satisBelgesiRepository = satisBelgesiRepository;
        _db = db;
        _muhasebeFisRepository = muhasebeFisRepository;
        _muhasebeFisService = muhasebeFisService;
        _userAccessScopeService = userAccessScopeService;
        _logger = logger;
        _domainLogger = domainLogger;
    }

    // ── Satirları include eden yardımcı ──

    // ──────────────────────────────────────────────
    //  Private — Muhasebe Fişi Koruması (Faz 68 — Durum-Bazlı)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Bağlı muhasebe fişinin durumuna göre satış belgesi mutasyon işlemlerini
    /// engeller veya serbest bırakır.
    ///
    /// Karar tablosu:
    /// | Bağlı Fiş Durumu        | Karar                          |
    /// |-------------------------|--------------------------------|
    /// | MuhasebeFisId null      | Serbest                        |
    /// | Fiş bulunamadı          | Hata + log warning              |
    /// | IsDeleted = true        | Hata + log warning              |
    /// | Taslak                  | Hata (önce fiş silinmeli)      |
    /// | Onayli                  | Hata (önce iptal/ters kayıt)   |
    /// | Iptal                   | Serbest                        |
    /// | TersKayit               | Hata + log warning (tutarsızlık)|
    /// | Bilinmeyen durum        | Hata                           |
    /// </summary>
    private async Task ThrowIfMuhasebeFisiIslemiEngellerAsync(
        SatisBelgesi belge,
        string islemAdi,
        CancellationToken cancellationToken)
    {
        if (!belge.MuhasebeFisId.HasValue)
            return; // Fiş yok → her şey serbest

        // Bağlı fişi repository üzerinden oku (base metot kullanımı)
        var fis = await _muhasebeFisRepository.FirstOrDefaultAsync(
            x => x.Id == belge.MuhasebeFisId.Value);

        // Durum 2: Fiş bulunamadı (veri tutarsızlığı)
        if (fis is null)
        {
            _logger.LogWarning(
                "SatisBelgesi {BelgeId} için MuhasebeFisId={FisId} referansı var ancak fiş bulunamadı",
                belge.Id, belge.MuhasebeFisId.Value);

            throw new BaseException(
                "Satış belgesine bağlı muhasebe fişi bulunamadı. Sistem yöneticinize başvurun.",
                errorCode: 400);
        }

        // Durum 3: Fiş soft-delete edilmiş (veri tutarsızlığı)
        if (fis.IsDeleted)
        {
            _logger.LogWarning(
                "SatisBelgesi {BelgeId} için MuhasebeFisId={FisId} referansı var ancak fiş silinmiş",
                belge.Id, belge.MuhasebeFisId.Value);

            throw new BaseException(
                "Satış belgesine bağlı muhasebe fişi silinmiş görünüyor. Sistem yöneticinize başvurun.",
                errorCode: 400);
        }

        // Durum bazlı karar
        switch (fis.Durum)
        {
            case MuhasebeFisDurumlari.Taslak:
                throw new BaseException(
                    $"Bu satış belgesine bağlı muhasebe fişi taslak durumunda. Önce bağlı fişi silmeniz gerekir.",
                    errorCode: 400);

            case MuhasebeFisDurumlari.Onayli:
                throw new BaseException(
                    $"Bu satış belgesine bağlı muhasebe fişi onaylı durumdadır. " +
                    "Önce bağlı fiş için iptal/ters kayıt süreci işletilmelidir.",
                    errorCode: 400);

            case MuhasebeFisDurumlari.Iptal:
                // Ters kayıt oluşturulmuş, muhasebe etkisi sıfırlanmış → serbest
                return;

            case MuhasebeFisDurumlari.TersKayit:
                // Bu durumda MuhasebeFisId normalde Iptal fişi göstermeli,
                // TersKayit fişi göstermemeli. Veri tutarsızlığı kabul et.
                _logger.LogWarning(
                    "SatisBelgesi {BelgeId} MuhasebeFisId={FisId} bir TersKayit fişine işaret ediyor",
                    belge.Id, belge.MuhasebeFisId.Value);

                throw new BaseException(
                    "Satış belgesi ters kayıt fişine bağlı görünüyor. Sistem yöneticinize başvurun.",
                    errorCode: 400);

            default:
                throw new BaseException(
                    $"Bağlı muhasebe fişinin durumu nedeniyle {islemAdi} işlemi yapılamaz: {fis.Durum}",
                    errorCode: 400);
        }
    }

    private static Func<IQueryable<SatisBelgesi>, IQueryable<SatisBelgesi>> IncludeSatirlar =>
        q => q.Include(x => x.Satirlar);

    private static Func<IQueryable<SatisBelgesi>, IQueryable<SatisBelgesi>> IncludeSatirlarVeCariKart =>
        q => q.Include(x => x.Satirlar).Include(x => x.CariKart);

    // ──────────────────────────────────────────────
    //  GetByIdAsync (ISatisBelgesiService) — nullable olmayan dönüş
    // ──────────────────────────────────────────────

    public async Task<SatisBelgesiDto> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var entity = await Repository.GetByIdAsync(id, IncludeSatirlarVeCariKart);
        if (entity is null)
            throw new BaseException($"Satış belgesi bulunamadı. (Id: {id})", errorCode: 404);

        return Mapper.Map<SatisBelgesiDto>(entity);
    }

    // ──────────────────────────────────────────────
    //  GetByIdAsync (base override) — nullable dönüş
    // ──────────────────────────────────────────────

    public override Task<SatisBelgesiDto?> GetByIdAsync(
        int id,
        Func<IQueryable<SatisBelgesi>, IQueryable<SatisBelgesi>>? include)
    {
        var effectiveInclude = include is not null
            ? CombineIncludes(IncludeSatirlarVeCariKart, include)
            : IncludeSatirlarVeCariKart;
        return base.GetByIdAsync(id, effectiveInclude);
    }

    private static Func<IQueryable<T>, IQueryable<T>> CombineIncludes<T>(
        Func<IQueryable<T>, IQueryable<T>> first,
        Func<IQueryable<T>, IQueryable<T>> second)
    {
        return q => second(first(q));
    }

    // ──────────────────────────────────────────────
    //  FilterAsync
    // ──────────────────────────────────────────────

    public async Task<List<SatisBelgesiDto>> FilterAsync(
        SatisBelgesiFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var query = _db.SatisBelgeleri
            .AsNoTracking()
            .Include(x => x.Satirlar)
            .Include(x => x.CariKart)
            .Where(x => !x.IsDeleted);

        if (filter.TesisId.HasValue)
            query = query.Where(x => x.TesisId == filter.TesisId.Value);

        if (filter.BelgeTipleri is { Count: > 0 })
            query = query.Where(x => filter.BelgeTipleri.Contains(x.BelgeTipi));

        if (filter.Durum.HasValue)
            query = query.Where(x => x.Durum == filter.Durum.Value);

        if (filter.KaynakModul.HasValue)
            query = query.Where(x => x.KaynakModul == filter.KaynakModul.Value);

        if (!string.IsNullOrWhiteSpace(filter.KaynakTipi))
            query = query.Where(x => x.KaynakTipi == filter.KaynakTipi);

        if (!string.IsNullOrWhiteSpace(filter.KaynakId))
            query = query.Where(x => x.KaynakId == filter.KaynakId);

        if (!string.IsNullOrWhiteSpace(filter.BelgeNo))
            query = query.Where(x => x.BelgeNo.Contains(filter.BelgeNo));

        if (!string.IsNullOrWhiteSpace(filter.Musteri))
            query = query.Where(x =>
                (x.MusteriUnvan != null && x.MusteriUnvan.Contains(filter.Musteri)) ||
                (x.MusteriAdSoyad != null && x.MusteriAdSoyad.Contains(filter.Musteri)));

        if (filter.BaslangicTarihi.HasValue)
            query = query.Where(x => x.BelgeTarihi >= filter.BaslangicTarihi.Value);

        if (filter.BitisTarihi.HasValue)
            query = query.Where(x => x.BelgeTarihi <= filter.BitisTarihi.Value);

        var belgeler = await query
            .OrderByDescending(x => x.BelgeTarihi)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return Mapper.Map<List<SatisBelgesiDto>>(belgeler);
    }

    // ──────────────────────────────────────────────
    //  CreateAsync
    // ──────────────────────────────────────────────

    public async Task<SatisBelgesiDto> CreateAsync(
        CreateSatisBelgesiRequest request,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _domainLogger.Started("Accounting.SalesDocument.Create.Started", new
        {
            BelgeTipi = request.BelgeTipi,
            KaynakTipi = request.KaynakTipi,
            KaynakId = request.KaynakId,
            TesisId = request.TesisId,
            SatirSayisi = request.Satirlar?.Count ?? 0
        });

        request.TesisId = await ResolveWriteTesisIdAsync(request.TesisId, cancellationToken);
        if (!request.TesisId.HasValue)
        {
            throw new BaseException(
                "Tesis seçimi zorunludur; belgenin kurum sahipliği tesis üzerinden belirlenir.",
                errorCode: 400);
        }

        // Otoriter kurum sahipliği zinciri: SatisBelgesi.TesisId -> Tesis.KurumId -> SatisBelgesi.KurumId.
        // İstemciden KurumId ALINMAZ (CreateSatisBelgesiRequest'te böyle bir alan yok) - Tesis
        // sorgusunun kendisi zaten aktif kurum bağlamına göre filtrelendiğinden (Tesis de
        // ITenantEntity'dir), scoped bir kullanıcı başka kuruma ait bir tesisi ASLA seçemez
        // (sorgu onu görmez, "Tesis bulunamadı" döner) - SuperAdmin ise tüm tesisleri görür ve
        // seçtiği tesisin kurumuna belge oluşturabilir.
        var kurumId = await ResolveKurumIdFromTesisAsync(request.TesisId.Value, cancellationToken);

        if (request.CariKartId.HasValue)
        {
            var cari = await ResolveAndValidateCariKartAsync(
                request.CariKartId.Value,
                request.TesisId,
                request.BelgeTipi,
                cancellationToken);
            ApplyCariSnapshotToCreateRequest(request, cari);
        }

        // 1. Validasyonlar
        await ValidateCreateRequestAsync(request, cancellationToken);

        // 2. Belge no üret (isteğe bağlı override)
        var belgeNo = request.BelgeNo ?? await GenerateBelgeNoAsync(request.BelgeTarihi, cancellationToken);

        // 3. Ana belge entity'sini oluştur
        var belge = new SatisBelgesi
        {
            KurumId = kurumId,
            BelgeNo = belgeNo,
            BelgeTipi = request.BelgeTipi,
            Durum = SatisBelgesiDurumu.Taslak,
            KaynakModul = request.KaynakModul,
            KaynakTipi = request.KaynakTipi,
            KaynakId = request.KaynakId,
            TesisId = request.TesisId,
            CariKartId = request.CariKartId,
            BelgeTarihi = request.BelgeTarihi,
            VadeTarihi = request.VadeTarihi,
            MusteriUnvan = request.MusteriUnvan,
            MusteriAdSoyad = request.MusteriAdSoyad,
            MusteriVergiNo = request.MusteriVergiNo,
            MusteriTcKimlikNo = request.MusteriTcKimlikNo,
            MusteriVergiDairesi = request.MusteriVergiDairesi,
            MusteriAdres = request.MusteriAdres,
            MusteriEposta = request.MusteriEposta,
            MusteriTelefon = request.MusteriTelefon,
            KurumsalMi = request.KurumsalMi,
            Aciklama = request.Aciklama
        };

        // 4. Satırları oluştur ve KDV hesapla
        foreach (var satirRequest in request.Satirlar ?? [])
        {
            await ValidateSatirRequestAsync(satirRequest, belge, cancellationToken);
            var satir = CreateSatirFromRequest(satirRequest);
            belge.Satirlar.Add(satir);
        }

        // 5. Belge toplamlarını hesapla
        HesaplaBelgeToplamlari(belge);

        await Repository.AddAsync(belge);
        await Repository.SaveChangesAsync(cancellationToken);
        if (belge.CariKartId.HasValue)
        {
            await _db.Entry(belge).Reference(x => x.CariKart).LoadAsync(cancellationToken);
        }

        sw.Stop();
        _domainLogger.Completed("Accounting.SalesDocument.Create.Completed", new
        {
            BelgeId = belge.Id,
            BelgeNo = belge.BelgeNo,
            BelgeTipi = belge.BelgeTipi,
            KaynakTipi = belge.KaynakTipi,
            KaynakId = belge.KaynakId,
            TesisId = belge.TesisId,
            CariId = belge.CariKartId,
            GenelToplam = belge.GenelToplam,
            ToplamMatrah = belge.ToplamMatrah,
            ToplamKdv = belge.ToplamKdv,
            SatirSayisi = belge.Satirlar.Count,
            DurationMs = sw.ElapsedMilliseconds
        });

        return Mapper.Map<SatisBelgesiDto>(belge);
    }

    // ──────────────────────────────────────────────
    //  UpdateAsync
    // ──────────────────────────────────────────────

    public async Task<SatisBelgesiDto> UpdateAsync(
        int id,
        UpdateSatisBelgesiRequest request,
        CancellationToken cancellationToken = default)
    {
        var belge = await Repository.FirstOrDefaultAsync(
            x => x.Id == id && !x.IsDeleted,
            q => q.Include(x => x.Satirlar))
            ?? throw new BaseException($"Satış belgesi bulunamadı. (Id: {id})", errorCode: 404);

        await ThrowIfMuhasebeFisiIslemiEngellerAsync(belge, "güncelleme", cancellationToken);

        // Durum kontrolü
        if (!GuncellenebilirDurumlar.Contains((int)belge.Durum))
        {
            throw new BaseException(
                $"'{belge.Durum}' durumundaki bir satış belgesi güncellenemez. " +
                "Sadece Taslak veya Reddedildi durumundaki belgeler güncellenebilir.",
                errorCode: 400);
        }

        // Reddedildi → Taslak durumuna döndür
        if (belge.Durum == SatisBelgesiDurumu.Reddedildi)
        {
            belge.Durum = SatisBelgesiDurumu.Taslak;
            belge.RedNedeni = null;
        }

        // Belge no değiştiyse duplicate kontrolü
        if (!string.IsNullOrWhiteSpace(request.BelgeNo) && request.BelgeNo != belge.BelgeNo)
        {
            await ThrowIfBelgeNoDuplicateAsync(request.BelgeNo, excludeId: id, cancellationToken);
        }

        request.TesisId = await ResolveWriteTesisIdAsync(request.TesisId, cancellationToken, belge.TesisId);

        // Tesis değiştiriliyorsa (mevcut TesisId'den farklıysa), yeni tesisin AYNI kuruma ait
        // olduğu doğrulanır - belge başka bir kuruma taşınamaz. KurumId'nin kendisi (belge.KurumId)
        // burada asla değiştirilmez; yalnızca tutarlılık kontrolü yapılır (gerçek değişmezlik
        // garantisi StysAppDbContext.ApplyTenantRules'ta SaveChanges sırasında ayrıca uygulanır).
        if (request.TesisId.HasValue && request.TesisId.Value != belge.TesisId)
        {
            var yeniTesisKurumId = await ResolveKurumIdFromTesisAsync(request.TesisId.Value, cancellationToken);
            if (yeniTesisKurumId != belge.KurumId)
            {
                throw new BaseException(
                    "Belge başka bir kuruma ait tesise taşınamaz.",
                    errorCode: 400);
            }
        }

        if (request.CariKartId.HasValue)
        {
            var cari = await ResolveAndValidateCariKartAsync(
                request.CariKartId.Value,
                request.TesisId,
                request.BelgeTipi ?? belge.BelgeTipi,
                cancellationToken);
            ApplyCariSnapshotToUpdateRequest(request, cari);
        }

        // Ana alanları güncelle
        await ApplyBelgeUpdatesAsync(belge, request, cancellationToken);

        // Satırlar gönderildiyse güncelle
        if (request.Satirlar is { Count: > 0 })
        {
            await UpdateSatirlarAsync(belge, request.Satirlar, cancellationToken);
        }

        HesaplaBelgeToplamlari(belge);
        _satisBelgesiRepository.Update(belge);
        await Repository.SaveChangesAsync(cancellationToken);
        if (belge.CariKartId.HasValue)
        {
            await _db.Entry(belge).Reference(x => x.CariKart).LoadAsync(cancellationToken);
        }

        return Mapper.Map<SatisBelgesiDto>(belge);
    }

    // ──────────────────────────────────────────────
    //  DeleteAsync — soft-delete belge + satırlar
    // ──────────────────────────────────────────────

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var belge = await Repository.FirstOrDefaultAsync(
            x => x.Id == id && !x.IsDeleted,
            q => q.Include(x => x.Satirlar))
            ?? throw new BaseException($"Satış belgesi bulunamadı. (Id: {id})", errorCode: 404);

        await ThrowIfMuhasebeFisiIslemiEngellerAsync(belge, "silme", cancellationToken);

        if (!SilinebilirDurumlar.Contains((int)belge.Durum))
        {
            throw new BaseException(
                $"'{belge.Durum}' durumundaki bir satış belgesi silinemez. " +
                "Sadece Taslak durumundaki belgeler silinebilir.",
                errorCode: 400);
        }

        // Soft-delete: satırları da sil
        foreach (var satir in belge.Satirlar.Where(s => !s.IsDeleted))
        {
            satir.IsDeleted = true;
        }

        belge.IsDeleted = true;

        _satisBelgesiRepository.Update(belge);
        await Repository.SaveChangesAsync(cancellationToken);
    }

    // ──────────────────────────────────────────────
    //  MuhasebeOnayinaGonderAsync
    // ──────────────────────────────────────────────

    public async Task MuhasebeOnayinaGonderAsync(int id, CancellationToken cancellationToken = default)
    {
        var belge = await Repository.FirstOrDefaultAsync(
            x => x.Id == id && !x.IsDeleted,
            q => q.Include(x => x.Satirlar))
            ?? throw new BaseException($"Satış belgesi bulunamadı. (Id: {id})", errorCode: 404);

        await ThrowIfMuhasebeFisiIslemiEngellerAsync(belge, "muhasebe onayına gönderme", cancellationToken);

        if (belge.Durum != SatisBelgesiDurumu.Taslak)
        {
            throw new BaseException(
                $"Sadece Taslak durumundaki belgeler muhasebe onayına gönderilebilir. Mevcut durum: {belge.Durum}",
                errorCode: 400);
        }

        // Kapsamlı ön-kontrol (satır, müşteri, KDV, toplam, kaynak duplicate)
        await ValidateBelgeOnayaGonderilebilir(belge, cancellationToken);

        belge.Durum = SatisBelgesiDurumu.MuhasebeOnayinda;
        belge.MuhasebeOnayinaGonderilmeTarihi = DateTime.UtcNow;

        await Repository.SaveChangesAsync(cancellationToken);
    }

    // ──────────────────────────────────────────────
    //  MuhasebeOnaylaAsync
    // ──────────────────────────────────────────────

    public async Task MuhasebeOnaylaAsync(int id, CancellationToken cancellationToken = default)
    {
        var belge = await Repository.FirstOrDefaultAsync(
            x => x.Id == id && !x.IsDeleted,
            q => q.Include(x => x.Satirlar))
            ?? throw new BaseException($"Satış belgesi bulunamadı. (Id: {id})", errorCode: 404);

        await ThrowIfMuhasebeFisiIslemiEngellerAsync(belge, "muhasebe onaylama", cancellationToken);

        if (belge.Durum != SatisBelgesiDurumu.MuhasebeOnayinda)
        {
            throw new BaseException(
                $"Sadece Muhasebe Onayında durumundaki belgeler onaylanabilir. Mevcut durum: {belge.Durum}",
                errorCode: 400);
        }

        // Onay anında içerik tekrar doğrulanır (arada değişiklik olmadığından emin olmak için)
        await ValidateBelgeMuhasebeOnaylanabilir(belge, cancellationToken);

        belge.Durum = SatisBelgesiDurumu.MuhasebeOnaylandi;
        belge.MuhasebeOnayTarihi = DateTime.UtcNow;

        await Repository.SaveChangesAsync(cancellationToken);

        _domainLogger.Completed("Accounting.SalesDocument.Create.Completed", new
        {
            BelgeId = belge.Id,
            BelgeNo = belge.BelgeNo,
            BelgeTipi = belge.BelgeTipi,
            KaynakTipi = belge.KaynakTipi,
            KaynakId = belge.KaynakId,
            TesisId = belge.TesisId,
            GenelToplam = belge.GenelToplam,
            ToplamKdv = belge.ToplamKdv,
            YeniDurum = belge.Durum.ToString(),
            BusinessResult = "MuhasebeOnaylandi"
        });
    }

    // ──────────────────────────────────────────────
    //  FaturaKesAsync
    // ──────────────────────────────────────────────

    /// <summary>
    /// Standart satış faturasına, kurum + mali yıl + seri bazlı, eşzamanlılığa güvenli bir
    /// resmî fatura numarası atar ve belgeyi FaturaKesildi durumuna geçirir.
    ///
    /// İLK SÜRÜM KAPSAMI: yalnızca SatisBelgesiTipi.SatisFaturasi için otomatik numara üretilir.
    /// AlisFaturasi, Proforma ve FaturaTaslagi için bu metot reddeder — alış faturasında
    /// tedarikçinin düzenlediği harici fatura numarası ile STYS'nin ürettiği bu numara
    /// KARIŞTIRILMAMALIDIR. SatisIadeFaturasi/AlisIadeFaturasi/IadeFaturasi için numara yönü
    /// (iade faturasının kendi resmî numarası mı, orijinal faturanınki mi referans alınacak)
    /// bu iş kapsamında TAHMİN EDİLEREK açılmamıştır — bkz. görev sonuç raporu.
    /// </summary>
    public async Task<SatisBelgesiDto> FaturaKesAsync(
        int id,
        FaturaKesRequest request,
        CancellationToken cancellationToken = default)
    {
        var seriKodu = NormalizeSeriKodu(request.SeriKodu);

        // Ön kontrol (transaction dışı, no-tracking) — asıl otoriter kontrol aşağıda, belge
        // satırı kilitlendikten SONRA tekrar yapılır; bu yalnızca ucuz/erken bir ret sağlar.
        var belgeOnOkuma = await _db.SatisBelgeleri
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BaseException($"Satış belgesi bulunamadı. (Id: {id})", errorCode: 404);

        if (belgeOnOkuma.IsDeleted)
            throw new BaseException("Satış belgesi silinmiş.", errorCode: 400);

        if (belgeOnOkuma.BelgeTipi != SatisBelgesiTipi.SatisFaturasi)
        {
            throw new BaseException(
                "Otomatik resmî fatura numarası yalnızca standart satış faturaları (SatisFaturasi) için üretilebilir.",
                errorCode: 400);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Belgeyi transaction içinde KİLİTLEYEREK yeniden oku - aynı belgeye iki eşzamanlı
            // FaturaKesAsync isteği, ikisi de sırayla bu satırı bekler; ikincisi birincinin
            // commit ettiği (artık FaturaKesildi olan) durumu görür ve aşağıdaki idempotent
            // dalından döner - ikinci bir numara TÜKETİLMEZ.
            var belge = await _db.SatisBelgeleri
                .FromSqlInterpolated($@"
SELECT * FROM [muhasebe].[SatisBelgeleri] WITH (UPDLOCK, ROWLOCK)
WHERE [Id] = {id} AND [IsDeleted] = 0")
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new BaseException($"Satış belgesi bulunamadı. (Id: {id})", errorCode: 404);

            if (belge.BelgeTipi != SatisBelgesiTipi.SatisFaturasi)
            {
                throw new BaseException(
                    "Otomatik resmî fatura numarası yalnızca standart satış faturaları (SatisFaturasi) için üretilebilir.",
                    errorCode: 400);
            }

            // ── Değişmezlik/tutarlılık invariantları (kilitli okuma sonrası, sayaç/yazımdan ÖNCE) ──
            // ResmiFaturaNo ve Durum=FaturaKesildi HER ZAMAN BİRLİKTE bulunmalıdır; biri diğeri
            // olmadan asla "sessizce devam edilerek" kabul edilmez - ikisi arasında bir
            // tutarsızlık varsa (ör. elle veri düzeltmesi, eksik migration, vb.) açık bir hata
            // verilir ve mevcut numara/durum ASLA üzerine yazılmaz.
            var resmiNumaraDoluMu = !string.IsNullOrWhiteSpace(belge.ResmiFaturaNo);
            var durumFaturaKesildiMi = belge.Durum == SatisBelgesiDurumu.FaturaKesildi;

            if (resmiNumaraDoluMu && !durumFaturaKesildiMi)
            {
                throw new BaseException(
                    $"Belgede resmî fatura numarası ({belge.ResmiFaturaNo}) var ancak durum 'FaturaKesildi' değil (Durum: {belge.Durum}); " +
                    $"veri tutarsızlığı, sistem yöneticisine başvurun. (Id: {id})",
                    errorCode: 500);
            }

            if (durumFaturaKesildiMi && !resmiNumaraDoluMu)
            {
                throw new BaseException(
                    $"Belge 'FaturaKesildi' durumunda ancak resmî fatura numarası bulunamadı; veri tutarsızlığı, sistem yöneticisine başvurun. (Id: {id})",
                    errorCode: 500);
            }

            if (durumFaturaKesildiMi && !belge.FaturaKesimTarihi.HasValue)
            {
                throw new BaseException(
                    $"Belge 'FaturaKesildi' durumunda ancak fatura kesim tarihi bulunamadı; veri tutarsızlığı, sistem yöneticisine başvurun. (Id: {id})",
                    errorCode: 500);
            }

            // İdempotency: belge zaten kesilmişse yeni numara TÜKETİLMEZ, mevcut sonuç
            // döndürülür — ama önce mevcut numaranın gerçekten tutarlı olduğu (format, yıl,
            // seri, çakışma, sayaç durumu) doğrulanır; sessizce devam edilmez, herhangi bir
            // tutarsızlıkta açık hata verilir.
            if (durumFaturaKesildiMi)
            {
                if (!TryParseResmiFaturaNo(belge.ResmiFaturaNo!, out var mevcutSeriKodu, out var mevcutYil, out var mevcutSiraNo))
                {
                    throw new BaseException(
                        $"Belgenin mevcut resmî fatura numarası ({belge.ResmiFaturaNo}) beklenen formatta değil; " +
                        $"veri tutarsızlığı, sistem yöneticisine başvurun. (Id: {id})",
                        errorCode: 500);
                }

                if (mevcutYil != belge.BelgeTarihi.Year)
                {
                    throw new BaseException(
                        $"Belgenin mevcut resmî fatura numarasının yıl bölümü ({mevcutYil}) belge tarihinin yılıyla " +
                        $"({belge.BelgeTarihi.Year}) uyuşmuyor; veri tutarsızlığı, sistem yöneticisine başvurun. (Id: {id})",
                        errorCode: 500);
                }

                if (!string.Equals(mevcutSeriKodu, seriKodu, StringComparison.Ordinal))
                {
                    throw new BaseException(
                        $"Belge zaten '{mevcutSeriKodu}' serisiyle kesilmiş; '{seriKodu}' serisiyle tekrar fatura kesilemez. (Id: {id})",
                        errorCode: 409);
                }

                var cakisanBelgeVarMi = await _db.SatisBelgeleri
                    .AsNoTracking()
                    .AnyAsync(x => x.Id != id && x.KurumId == belge.KurumId && x.ResmiFaturaNo == belge.ResmiFaturaNo, cancellationToken);

                if (cakisanBelgeVarMi)
                {
                    throw new BaseException(
                        $"Resmî fatura numarası ({belge.ResmiFaturaNo}) başka bir belgeyle çakışıyor; veri tutarsızlığı, sistem yöneticisine başvurun.",
                        errorCode: 500);
                }

                var ilgiliSayac = await _db.KurumFaturaNumaraSayaclari
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => !x.IsDeleted && x.KurumId == belge.KurumId && x.MaliYil == mevcutYil && x.SeriKodu == mevcutSeriKodu, cancellationToken);

                if (ilgiliSayac is null)
                {
                    throw new BaseException(
                        $"Belgenin resmî fatura numarasına ({belge.ResmiFaturaNo}) karşılık gelen kurum/yıl/seri sayacı bulunamadı; " +
                        $"veri tutarsızlığı, sistem yöneticisine başvurun. (Id: {id})",
                        errorCode: 500);
                }

                if (ilgiliSayac.SonNumara < mevcutSiraNo)
                {
                    throw new BaseException(
                        $"'{mevcutSeriKodu}' serisinin sayacı ({ilgiliSayac.SonNumara}), belgenin mevcut resmî numarasının sıra değerinden " +
                        $"({mevcutSiraNo}) küçük; veri tutarsızlığı, sistem yöneticisine başvurun. (Id: {id})",
                        errorCode: 500);
                }

                await transaction.CommitAsync(cancellationToken);
                return Mapper.Map<SatisBelgesiDto>(belge);
            }

            if (belge.Durum != SatisBelgesiDurumu.MuhasebeOnaylandi)
            {
                throw new BaseException(
                    $"Yalnızca 'MuhasebeOnaylandı' durumundaki belgeler için fatura kesilebilir. Mevcut durum: {belge.Durum}",
                    errorCode: 400);
            }

            if (!belge.MuhasebeFisId.HasValue)
                throw new BaseException("Belgeye bağlı muhasebe fişi bulunamadı; fatura kesilemez.", errorCode: 400);

            var fis = await _db.MuhasebeFisler
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == belge.MuhasebeFisId.Value, cancellationToken);

            if (fis is null)
                throw new BaseException("Belgeye bağlı muhasebe fişi bulunamadı; fatura kesilemez.", errorCode: 400);

            if (fis.IsDeleted)
                throw new BaseException("Belgeye bağlı muhasebe fişi silinmiş; fatura kesilemez.", errorCode: 400);

            // Muhasebe fişi durum matrisi: Taslak/Onaylı geçerli kabul edilir (mevcut fiş üretim
            // akışı Taslak fiş üretir - bkz. SatisBelgesiMuhasebeFisService); İptal ve TersKayit
            // reddedilir; bilinmeyen/beklenmeyen herhangi bir durum değeri de reddedilir - "belki
            // geçerlidir" diye SESSİZCE kabul edilmez.
            switch (fis.Durum)
            {
                case MuhasebeFisDurumlari.Taslak:
                case MuhasebeFisDurumlari.Onayli:
                    break;

                case MuhasebeFisDurumlari.Iptal:
                    throw new BaseException("Belgeye bağlı muhasebe fişi iptal edilmiş; fatura kesilemez.", errorCode: 400);

                case MuhasebeFisDurumlari.TersKayit:
                    throw new BaseException("Belgeye bağlı muhasebe fişi bir ters kayıt fişidir; fatura kesilemez.", errorCode: 400);

                default:
                    throw new BaseException(
                        $"Belgeye bağlı muhasebe fişi bilinmeyen bir durumda ({fis.Durum}); fatura kesilemez.",
                        errorCode: 400);
            }

            if (!belge.TesisId.HasValue)
                throw new BaseException("Belgede tesis bilgisi bulunamadı; fatura kesilemez.", errorCode: 400);

            // Tesis/kurum tutarlılığı — normalde ApplyTenantRules'un KurumId'yi değiştirilemez
            // kıldığı ve CreateAsync'in bunu tesisten aldığı göz önüne alındığında bu her zaman
            // tutarlı olmalıdır; savunma amaçlı ayrıca doğrulanır.
            var tesisKurumId = await ResolveKurumIdFromTesisAsync(belge.TesisId.Value, cancellationToken);
            if (tesisKurumId != belge.KurumId)
            {
                throw new BaseException(
                    "Belgenin tesis ve kurum bilgileri tutarsız; fatura kesilemez.",
                    errorCode: 500);
            }

            var maliYil = belge.BelgeTarihi.Year;

            // Sayaç satırını WITH (UPDLOCK, ROWLOCK, HOLDLOCK) ile kilitle — aynı kurum/yıl/seri
            // için eşzamanlı çalışan başka bir fatura kesme işlemi bu satırı serbest kalana
            // kadar bekler; Max(ResmiFaturaNo)+1 yarışı YOKTUR.
            var sayac = await _db.KurumFaturaNumaraSayaclari
                .FromSqlInterpolated($@"
SELECT * FROM [muhasebe].[KurumFaturaNumaraSayaclari] WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
WHERE [IsDeleted] = 0 AND [KurumId] = {belge.KurumId} AND [MaliYil] = {maliYil} AND [SeriKodu] = {seriKodu}")
                .FirstOrDefaultAsync(cancellationToken);

            if (sayac is null)
            {
                throw new BaseException(
                    $"'{seriKodu}' serisi için {maliYil} mali yılında tanımlı bir fatura numarası sayacı bulunamadı.",
                    errorCode: 400);
            }

            if (!sayac.AktifMi)
                throw new BaseException($"'{seriKodu}' serisi pasif durumda.", errorCode: 400);

            // 9 haneli sıra bölümünün üst sınırı: {SiraNo:000000000} formatı en fazla 999999999
            // değerini taşıyabilir. Sayaç zaten bu değerdeyse ne sayaç ne belge değiştirilir -
            // açık bir hata verilir (taşma sonrası format bozulur/yanlış bir numara üretilir).
            if (sayac.SonNumara >= 999999999)
            {
                throw new BaseException(
                    $"'{seriKodu}' serisi için {maliYil} mali yılındaki sıra numarası sınırına (999999999) ulaşıldı; " +
                    "yeni resmî fatura numarası üretilemez. Yeni bir seri tanımlanmalıdır.",
                    errorCode: 409);
            }

            var siraNo = sayac.SonNumara + 1;
            sayac.SonNumara = siraNo;

            // {SeriKodu}{MaliYil:0000}{SiraNo:000000000} -> 3+4+9 = 16 karakter (ör. ABC2026000000001)
            var resmiFaturaNo = $"{seriKodu}{maliYil:0000}{siraNo:000000000}";

            belge.ResmiFaturaNo = resmiFaturaNo;
            belge.FaturaKesimTarihi = DateTime.UtcNow;
            belge.Durum = SatisBelgesiDurumu.FaturaKesildi;

            try
            {
                // Sayaç artışı VE belge güncellemesi AYNI SaveChangesAsync/transaction'da -
                // biri başarısız olursa (ör. unique index çakışması) ikisi de rollback olur.
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConflict(ex))
            {
                throw new BaseException(
                    "Resmî fatura numarası üretilirken bir çakışma oluştu. Lütfen tekrar deneyin.",
                    errorCode: 409);
            }

            await transaction.CommitAsync(cancellationToken);

            return Mapper.Map<SatisBelgesiDto>(belge);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string NormalizeSeriKodu(string? seriKodu)
    {
        var normalized = (seriKodu ?? string.Empty).Trim().ToUpperInvariant();

        if (normalized.Length != 3 || !normalized.All(c => (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
        {
            throw new BaseException(
                "Seri kodu, A-Z ve 0-9 karakterlerinden oluşan tam 3 karakter uzunluğunda olmalıdır.",
                errorCode: 400);
        }

        return normalized;
    }

    private static bool IsUniqueConflict(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sqlEx &&
               (sqlEx.Number == 2601 || sqlEx.Number == 2627);
    }

    /// <summary>
    /// {SeriKodu(3)}{MaliYil(4)}{SiraNo(9)} = 16 karakterlik resmî fatura numarası formatını KESİN
    /// olarak doğrular. int.TryParse'a doğrudan güvenilmez - "+00000001", "-00000001", " 00000001"
    /// gibi değerleri de (işaret/boşluk NumberStyles.Integer ile kabul edilebildiği için, veya
    /// bazı kültürlerde Unicode rakamlar kabul edilebildiği için) sessizce geçerli sayabilir. Bu
    /// yüzden her karakter ÖNCE tek tek ASCII 'A'-'Z'/'0'-'9' aralığında olduğu doğrulanır; ancak
    /// bu doğrulamadan geçen, saf ASCII rakamlardan oluşan bir dilim üzerinde int.TryParse
    /// (CultureInfo.InvariantCulture, NumberStyles.None ile) çağrılır - NumberStyles.None işaret,
    /// boşluk, binlik ayıracı gibi hiçbir ek karaktere izin vermez.
    /// </summary>
    private static bool TryParseResmiFaturaNo(string resmiFaturaNo, out string seriKodu, out int maliYil, out int siraNo)
    {
        seriKodu = string.Empty;
        maliYil = 0;
        siraNo = 0;

        if (resmiFaturaNo.Length != 16)
            return false;

        var seriKismi = resmiFaturaNo.AsSpan(0, 3);
        var yilKismi = resmiFaturaNo.AsSpan(3, 4);
        var siraKismi = resmiFaturaNo.AsSpan(7, 9);

        foreach (var c in seriKismi)
        {
            if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
                return false;
        }

        foreach (var c in yilKismi)
        {
            if (c < '0' || c > '9')
                return false;
        }

        foreach (var c in siraKismi)
        {
            if (c < '0' || c > '9')
                return false;
        }

        if (!int.TryParse(yilKismi, NumberStyles.None, CultureInfo.InvariantCulture, out maliYil))
            return false;

        if (!int.TryParse(siraKismi, NumberStyles.None, CultureInfo.InvariantCulture, out siraNo))
            return false;

        // Sıra numarası 1'den başlar (bkz. FaturaKesAsync: sayac.SonNumara + 1) - "000000000"
        // hiçbir zaman üretilmemiş, dolayısıyla geçersiz bir değerdir.
        if (siraNo is < 1 or > 999999999)
            return false;

        seriKodu = resmiFaturaNo[..3];
        return true;
    }

    // ──────────────────────────────────────────────
    //  ReddetAsync
    // ──────────────────────────────────────────────

    public async Task ReddetAsync(
        int id,
        string redNedeni,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(redNedeni))
            throw new BaseException("Ret nedeni zorunludur.", errorCode: 400);

        var belge = await Repository.FirstOrDefaultAsync(
            x => x.Id == id && !x.IsDeleted)
            ?? throw new BaseException($"Satış belgesi bulunamadı. (Id: {id})", errorCode: 404);

        await ThrowIfMuhasebeFisiIslemiEngellerAsync(belge, "reddetme", cancellationToken);

        if (belge.Durum != SatisBelgesiDurumu.MuhasebeOnayinda)
        {
            throw new BaseException(
                $"Sadece Muhasebe Onayında durumundaki belgeler reddedilebilir. Mevcut durum: {belge.Durum}",
                errorCode: 400);
        }

        belge.Durum = SatisBelgesiDurumu.Reddedildi;
        belge.RedNedeni = redNedeni.Trim();

        await Repository.SaveChangesAsync(cancellationToken);
    }

    // ──────────────────────────────────────────────
    //  IptalEtAsync
    // ──────────────────────────────────────────────

    public async Task IptalEtAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var belge = await _db.SatisBelgeleri
                .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
                .Include(x => x.CariKart)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
                ?? throw new BaseException($"Satış belgesi bulunamadı. (Id: {id})", errorCode: 404);

            await ValidateTicariBelgeIptalAsync(belge, cancellationToken);

            if (belge.MuhasebeFisId.HasValue)
            {
                await ValidateVeIptalEtMuhasebeFisiAsync(belge, cancellationToken);
            }

            await IptalEtStokHareketleriAsync(belge, cancellationToken);
            await IptalEtCariHareketleriAsync(belge, cancellationToken);

            belge.Durum = SatisBelgesiDurumu.IptalEdildi;

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _domainLogger.Completed("Accounting.SalesDocument.Create.Completed", new
            {
                BelgeId = belge.Id,
                BelgeNo = belge.BelgeNo,
                BelgeTipi = belge.BelgeTipi,
                KaynakTipi = belge.KaynakTipi,
                KaynakId = belge.KaynakId,
                TesisId = belge.TesisId,
                GenelToplam = belge.GenelToplam,
                ToplamKdv = belge.ToplamKdv,
                YeniDurum = belge.Durum.ToString(),
                BusinessResult = "IptalEdildi"
            });
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task ValidateTicariBelgeIptalAsync(SatisBelgesi belge, CancellationToken cancellationToken)
    {
        if (belge.Durum == SatisBelgesiDurumu.IptalEdildi)
        {
            throw new BaseException("Belge zaten iptal edilmiş.", 400);
        }

        if (belge.Durum == SatisBelgesiDurumu.FaturaKesildi ||
            belge.Durum == SatisBelgesiDurumu.MusteriyeGonderildi)
        {
            throw new BaseException(
                $"'{belge.Durum}' durumundaki bir belge iptal edilemez. " +
                "Fatura kesilmiş veya müşteriye gönderilmiş belgeler iptal edilemez.",
                400);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && (!belge.TesisId.HasValue || !scope.TesisIds.Contains(belge.TesisId.Value)))
        {
            throw new BaseException("Bu belge için yetkiniz bulunmuyor.", 403);
        }
    }

    private async Task ValidateVeIptalEtMuhasebeFisiAsync(SatisBelgesi belge, CancellationToken cancellationToken)
    {
        var muhasebeFisId = belge.MuhasebeFisId
            ?? throw new BaseException("Bağlı muhasebe fişi bulunamadı.", 404);

        var fis = await _db.MuhasebeFisler
            .FirstOrDefaultAsync(x => x.Id == muhasebeFisId && !x.IsDeleted, cancellationToken);

        if (fis is null)
        {
            throw new BaseException("Bağlı muhasebe fişi bulunamadı.", 404);
        }

        if (fis.Durum == MuhasebeFisDurumlari.Iptal)
        {
            return;
        }

        if (fis.Durum == MuhasebeFisDurumlari.TersKayit)
        {
            throw new BaseException("Ters kayıt fişi üzerinde iptal/ters kayıt yapılamaz.", 400);
        }

        if (fis.Durum != MuhasebeFisDurumlari.Onayli)
        {
            throw new BaseException("Bağlı taslak muhasebe fişi önce silinmelidir.", 400);
        }

        await _muhasebeFisService.IptalEtAsync(fis.Id, cancellationToken: cancellationToken);
    }

    private async Task IptalEtCariHareketleriAsync(SatisBelgesi belge, CancellationToken cancellationToken)
    {
        var hareketler = await _db.CariHareketler
            .Where(x =>
                !x.IsDeleted
                && x.Durum == CariHareketDurumlari.Aktif
                && x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi
                && x.KaynakId == belge.Id)
            .ToListAsync(cancellationToken);

        var kapatilmisHareketVar = hareketler.Any(x =>
        {
            var toplam = x.BorcTutari > 0m ? x.BorcTutari : x.AlacakTutari;
            return x.KapandiMi || x.KapananTutar > 0m || x.KalanTutar + 0.01m < toplam;
        });

        if (kapatilmisHareketVar)
        {
            throw new BaseException("Bu belgeye ait cari hareket kapatılmış/kısmi kapatılmış. Önce tahsilat/ödeme kapaması geri alınmalıdır.", 400);
        }

        foreach (var hareket in hareketler)
        {
            hareket.Durum = CariHareketDurumlari.Iptal;
        }
    }

    private async Task IptalEtStokHareketleriAsync(SatisBelgesi belge, CancellationToken cancellationToken)
    {
        var hareketler = await _db.StokHareketleri
            .Where(x =>
                !x.IsDeleted
                && x.Durum == StokHareketDurumlari.Aktif
                && x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi
                && x.KaynakId == belge.Id)
            .ToListAsync(cancellationToken);

        foreach (var hareket in hareketler)
        {
            hareket.Durum = StokHareketDurumlari.Iptal;
        }
    }

    // ──────────────────────────────────────────────
    //  Private — Muhasebe Onay Validasyonları
    // ──────────────────────────────────────────────

    /// <summary>
    /// Belgeyi muhasebe onayına göndermeden önce tüm zorunlu kontrolleri yapar.
    /// Aşağıdaki kontrolleri içerir:
    /// 1. En az 1 aktif satır
    /// 2. ToplamMatrah > 0
    /// 3. GenelToplam > 0
    /// 4. Kurumsal müşteri → MusteriUnvan + MusteriVergiNo dolu
    /// 5. Bireysel müşteri → MusteriAdSoyad dolu
    /// 6. KDV uygulama tipi geçerlilik kontrolü (her satırda)
    /// 7. KDV'li satırda KdvOrani > 0
    /// 8. KDV istisna / tevkifat ayrımı kontrolü
    /// 9. Satır toplamları = Belge toplamları (tutarlılık)
    /// 10. Kaynak duplicate kontrolü
    /// 11. KDV istisna tanımı geçerlilik kontrolü
    /// </summary>
    private async Task ValidateBelgeOnayaGonderilebilir(
        SatisBelgesi belge,
        CancellationToken cancellationToken)
    {
        var aktifSatirlar = belge.Satirlar.Where(s => !s.IsDeleted).ToList();

        // 1. En az 1 aktif satır
        if (aktifSatirlar.Count == 0)
            throw new BaseException("Satır içermeyen belge muhasebe onayına gönderilemez.", errorCode: 400);

        // 2. ToplamMatrah > 0
        if (belge.ToplamMatrah <= 0)
            throw new BaseException("Belge toplam matrahı sıfırdan büyük olmalıdır.", errorCode: 400);

        // 3. GenelToplam > 0
        if (belge.GenelToplam <= 0)
            throw new BaseException("Belge genel toplamı sıfırdan büyük olmalıdır.", errorCode: 400);

        // 4-5. Kurumsal/bireysel müşteri alanları
        if (belge.KurumsalMi)
        {
            if (string.IsNullOrWhiteSpace(belge.MusteriUnvan))
                throw new BaseException("Kurumsal müşteri için ünvan zorunludur.", errorCode: 400);
            if (string.IsNullOrWhiteSpace(belge.MusteriVergiNo))
                throw new BaseException("Kurumsal müşteri için vergi numarası zorunludur.", errorCode: 400);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(belge.MusteriAdSoyad))
                throw new BaseException("Bireysel müşteri için ad soyad zorunludur.", errorCode: 400);
        }

        // 6-9. Her satır için KDV kontrolleri
        foreach (var satir in aktifSatirlar)
        {
            // 6. Desteklenen KDV uygulama tipi kontrolü
            if (!DesteklenenKdvUygulamaTipleri.Contains((int)satir.KdvUygulamaTipi))
                throw new BaseException(
                    $"Geçersiz KDV uygulama tipi: {satir.KdvUygulamaTipi}. (SıraNo: {satir.SiraNo})",
                    errorCode: 400);

            // 7. KDV'li satırda KdvOrani > 0
            if (satir.KdvUygulamaTipi == KdvUygulamaTipi.Kdvli && satir.KdvOrani <= 0)
                throw new BaseException(
                    $"KDV'li satırda KDV oranı sıfırdan büyük olmalıdır. (SıraNo: {satir.SiraNo})",
                    errorCode: 400);

            // 8. KDV istisna / tevkifat ayrımı kontrolü
            if (satir.KdvUygulamaTipi == KdvUygulamaTipi.Tevkifatli)
            {
                if (!satir.TevkifatPay.HasValue || !satir.TevkifatPayda.HasValue)
                    throw new BaseException(
                        $"Tevkifatlı satırda tevkifat oranı zorunludur. (SıraNo: {satir.SiraNo})",
                        errorCode: 400);

                if (!DesteklenenTevkifatOranlari.Contains((satir.TevkifatPay.Value, satir.TevkifatPayda.Value)))
                    throw new BaseException(
                        $"Geçersiz tevkifat oranı: {satir.TevkifatPay}/{satir.TevkifatPayda}. (SıraNo: {satir.SiraNo})",
                        errorCode: 400);

                if (satir.KdvOrani <= 0)
                    throw new BaseException(
                        $"Tevkifatlı satırda KDV oranı sıfırdan büyük olmalıdır. (SıraNo: {satir.SiraNo})",
                        errorCode: 400);

                if (satir.KdvIstisnaTanimId.HasValue)
                    throw new BaseException(
                        $"Tevkifatlı satırda KDV istisna tanımı seçilemez. (SıraNo: {satir.SiraNo})",
                        errorCode: 400);
            }
            else if (satir.KdvUygulamaTipi != KdvUygulamaTipi.Kdvli && !satir.KdvIstisnaTanimId.HasValue)
            {
                throw new BaseException(
                    $"KDV'li olmayan satırda KDV istisna tanımı zorunludur. (SıraNo: {satir.SiraNo})",
                    errorCode: 400);
            }
            else if (satir.KdvUygulamaTipi == KdvUygulamaTipi.Kdvli && satir.KdvIstisnaTanimId.HasValue)
            {
                throw new BaseException(
                    $"KDV'li satırda KDV istisna tanımı seçilemez. (SıraNo: {satir.SiraNo})",
                    errorCode: 400);
            }

            // 11. KDV istisna tanımı geçerlilik kontrolü
            if (satir.KdvIstisnaTanimId.HasValue)
            {
                await ValidateKdvIstisnaTanimAsync(
                    satir.KdvIstisnaTanimId.Value,
                    (int)satir.KdvUygulamaTipi,
                    belge.BelgeTarihi,
                    ResolveKdvIslemYonu(belge.BelgeTipi),
                    cancellationToken);
            }
        }

        // 10. Satır toplamları = Belge toplamları (tutarlılık)
        var hesaplananMatrah = aktifSatirlar.Sum(s => s.Matrah);
        var hesaplananKdv = aktifSatirlar.Sum(s => s.KdvTutari);
        var hesaplananGenelToplam = aktifSatirlar.Sum(s => s.SatirToplami);

        if (belge.ToplamMatrah != hesaplananMatrah)
            throw new BaseException(
                $"Belge toplam matrahı ({belge.ToplamMatrah}) satır toplamlarıyla ({hesaplananMatrah}) uyuşmuyor. " +
                "Belgeyi güncelleyip tekrar deneyin.",
                errorCode: 400);

        if (belge.ToplamKdv != hesaplananKdv)
            throw new BaseException(
                $"Belge toplam KDV'si ({belge.ToplamKdv}) satır KDV toplamlarıyla ({hesaplananKdv}) uyuşmuyor. " +
                "Belgeyi güncelleyip tekrar deneyin.",
                errorCode: 400);

        if (belge.GenelToplam != hesaplananGenelToplam)
            throw new BaseException(
                $"Belge genel toplamı ({belge.GenelToplam}) satır genel toplamlarıyla ({hesaplananGenelToplam}) uyuşmuyor. " +
                "Belgeyi güncelleyip tekrar deneyin.",
                errorCode: 400);

        // 11. Kaynak duplicate kontrolü (sadece manuel olmayan belgeler için)
        if (belge.KaynakId is not null)
        {
            await ThrowIfKaynakDuplicateAsync(
                belge.KaynakModul,
                belge.KaynakTipi,
                belge.KaynakId,
                excludeId: belge.Id,
                cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Muhasebe onayı anında belge içeriğini tekrar doğrular.
    /// <see cref="ValidateBelgeOnayaGonderilebilir"/> ile aynı kontrolleri yapar.
    /// Onaya gönderme ile onaylama arasında belge içeriğinin değişmediğinden emin olur.
    /// </summary>
    private async Task ValidateBelgeMuhasebeOnaylanabilir(
        SatisBelgesi belge,
        CancellationToken cancellationToken)
    {
        await ValidateBelgeOnayaGonderilebilir(belge, cancellationToken);
    }

    // ──────────────────────────────────────────────
    //  Private — Validasyon
    // ──────────────────────────────────────────────

    private async Task ValidateCreateRequestAsync(
        CreateSatisBelgesiRequest request,
        CancellationToken cancellationToken)
    {
        // Belge tarihi zorunlu
        if (request.BelgeTarihi == default)
            throw new BaseException("Belge tarihi zorunludur.", errorCode: 400);

        // En az 1 satır
        if (request.Satirlar.Count == 0)
            throw new BaseException("En az bir satır eklenmelidir.", errorCode: 400);

        // Kurumsal → MusteriUnvan + MusteriVergiNo zorunlu
        if (request.KurumsalMi)
        {
            if (string.IsNullOrWhiteSpace(request.MusteriUnvan))
                throw new BaseException("Kurumsal müşteri için ünvan zorunludur.", errorCode: 400);
            if (string.IsNullOrWhiteSpace(request.MusteriVergiNo))
                throw new BaseException("Kurumsal müşteri için vergi numarası zorunludur.", errorCode: 400);
        }
        else
        {
            // Bireysel → MusteriAdSoyad zorunlu
            if (string.IsNullOrWhiteSpace(request.MusteriAdSoyad))
                throw new BaseException("Bireysel müşteri için ad soyad zorunludur.", errorCode: 400);
        }

        // Belge no varsa duplicate kontrolü
        if (!string.IsNullOrWhiteSpace(request.BelgeNo))
        {
            await ThrowIfBelgeNoDuplicateAsync(request.BelgeNo, cancellationToken: cancellationToken);
        }

        // Kaynak duplicate kontrolü
        if (request.KaynakId is not null)
        {
            await ThrowIfKaynakDuplicateAsync(
                request.KaynakModul, request.KaynakTipi, request.KaynakId, cancellationToken: cancellationToken);
        }
    }

    private async Task<CariKart> ResolveAndValidateCariKartAsync(
        int cariKartId,
        int? tesisId,
        SatisBelgesiTipi belgeTipi,
        CancellationToken cancellationToken)
    {
        var cari = await _db.CariKartlar
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == cariKartId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Cari kart bulunamadı.", 404);

        if (!cari.AktifMi)
            throw new BaseException("Cari kart pasif durumda.", 400);

        if (tesisId.HasValue && cari.TesisId.HasValue && cari.TesisId != tesisId)
            throw new BaseException("Seçilen cari kart belge tesisiyle uyumlu değil.", 400);

        if (belgeTipi.IsAlisBelgesi())
        {
            if (!string.Equals(cari.CariTipi, CariKartTipleri.Tedarikci, StringComparison.OrdinalIgnoreCase))
                throw new BaseException("Alış belgelerinde tedarikçi cari kart seçilmelidir.", 400);
        }
        else
        {
            var uygunMusteriTipi =
                string.Equals(cari.CariTipi, CariKartTipleri.Musteri, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cari.CariTipi, CariKartTipleri.KurumsalMusteri, StringComparison.OrdinalIgnoreCase);

            if (!uygunMusteriTipi)
                throw new BaseException("Satış belgelerinde müşteri tipli cari kart seçilmelidir.", 400);
        }

        return cari;
    }

    private static void ApplyCariSnapshotToCreateRequest(
        CreateSatisBelgesiRequest request,
        CariKart cari)
    {
        ApplyCariSnapshot(
            () => request.CariKartId = cari.Id,
            value => request.KurumsalMi = value,
            value => request.MusteriUnvan = value,
            value => request.MusteriAdSoyad = value,
            value => request.MusteriVergiNo = value,
            value => request.MusteriTcKimlikNo = value,
            value => request.MusteriVergiDairesi = value,
            value => request.MusteriAdres = value,
            value => request.MusteriEposta = value,
            value => request.MusteriTelefon = value,
            cari);
    }

    private static void ApplyCariSnapshotToUpdateRequest(
        UpdateSatisBelgesiRequest request,
        CariKart cari)
    {
        ApplyCariSnapshot(
            () => request.CariKartId = cari.Id,
            value => request.KurumsalMi = value,
            value => request.MusteriUnvan = value,
            value => request.MusteriAdSoyad = value,
            value => request.MusteriVergiNo = value,
            value => request.MusteriTcKimlikNo = value,
            value => request.MusteriVergiDairesi = value,
            value => request.MusteriAdres = value,
            value => request.MusteriEposta = value,
            value => request.MusteriTelefon = value,
            cari);
    }

    private static void ApplyCariSnapshot(
        Action setCariKartId,
        Action<bool> setKurumsalMi,
        Action<string?> setMusteriUnvan,
        Action<string?> setMusteriAdSoyad,
        Action<string?> setMusteriVergiNo,
        Action<string?> setMusteriTcKimlikNo,
        Action<string?> setMusteriVergiDairesi,
        Action<string?> setMusteriAdres,
        Action<string?> setMusteriEposta,
        Action<string?> setMusteriTelefon,
        CariKart cari)
    {
        setCariKartId();

        var kurumsalMi = !string.Equals(cari.CariTipi, CariKartTipleri.Musteri, StringComparison.OrdinalIgnoreCase);
        setKurumsalMi(kurumsalMi);

        if (kurumsalMi)
        {
            setMusteriUnvan(cari.UnvanAdSoyad);
            setMusteriAdSoyad(null);
            setMusteriVergiNo(cari.VergiNoTckn);
            setMusteriTcKimlikNo(null);
        }
        else
        {
            setMusteriUnvan(null);
            setMusteriAdSoyad(cari.UnvanAdSoyad);
            setMusteriVergiNo(null);
            setMusteriTcKimlikNo(cari.VergiNoTckn);
        }

        setMusteriVergiDairesi(cari.VergiDairesi);
        setMusteriAdres(cari.Adres);
        setMusteriEposta(cari.Eposta);
        setMusteriTelefon(cari.Telefon);
    }

    private async Task ValidateSatirRequestAsync(
        CreateSatisBelgesiSatiriRequest request,
        SatisBelgesi belge,
        CancellationToken cancellationToken)
    {
        // Miktar > 0
        if (request.Miktar <= 0)
            throw new BaseException($"Satır miktarı sıfırdan büyük olmalıdır. (SıraNo: {request.SiraNo})", errorCode: 400);

        // BirimFiyat >= 0
        if (request.BirimFiyat < 0)
            throw new BaseException($"Birim fiyat negatif olamaz. (SıraNo: {request.SiraNo})", errorCode: 400);

        if (request.IndirimTutari < 0)
            throw new BaseException($"İndirim tutarı negatif olamaz. (SıraNo: {request.SiraNo})", errorCode: 400);

        if (request.IndirimOrani < 0)
            throw new BaseException($"İndirim oranı negatif olamaz. (SıraNo: {request.SiraNo})", errorCode: 400);

        if (request.KdvOrani < 0)
            throw new BaseException($"KDV oranı negatif olamaz. (SıraNo: {request.SiraNo})", errorCode: 400);

        if (request.OtvOrani < 0)
            throw new BaseException($"ÖTV oranı negatif olamaz. (SıraNo: {request.SiraNo})", errorCode: 400);

        if (request.OtvTutari < 0)
            throw new BaseException($"ÖTV tutarı negatif olamaz. (SıraNo: {request.SiraNo})", errorCode: 400);

        if (request.OivOrani < 0)
            throw new BaseException($"ÖİV oranı negatif olamaz. (SıraNo: {request.SiraNo})", errorCode: 400);

        if (request.OivTutari < 0)
            throw new BaseException($"ÖİV tutarı negatif olamaz. (SıraNo: {request.SiraNo})", errorCode: 400);

        if (request.KonaklamaVergisiOrani < 0)
            throw new BaseException($"Konaklama vergisi oranı negatif olamaz. (SıraNo: {request.SiraNo})", errorCode: 400);

        if (request.KonaklamaVergisiTutari < 0)
            throw new BaseException($"Konaklama vergisi tutarı negatif olamaz. (SıraNo: {request.SiraNo})", errorCode: 400);

        // Bilinmeyen KDV uygulama tipi
        if (!DesteklenenKdvUygulamaTipleri.Contains(request.KdvUygulamaTipi))
            throw new BaseException($"Geçersiz KDV uygulama tipi: {request.KdvUygulamaTipi}", errorCode: 400);

        // KDV'li satırda KdvOrani > 0
        if (request.KdvUygulamaTipi == (int)KdvUygulamaTipi.Kdvli && request.KdvOrani <= 0)
            throw new BaseException($"KDV'li satırda KDV oranı sıfırdan büyük olmalıdır. (SıraNo: {request.SiraNo})", errorCode: 400);

        if (request.KdvUygulamaTipi == (int)KdvUygulamaTipi.Kdvli && request.KdvIstisnaTanimId.HasValue)
            throw new BaseException($"KDV'li satırda KDV istisna tanımı seçilemez. (SıraNo: {request.SiraNo})", errorCode: 400);

        if (request.TevkifatPay.HasValue || request.TevkifatPayda.HasValue)
        {
            if (request.KdvUygulamaTipi != (int)KdvUygulamaTipi.Tevkifatli)
                throw new BaseException($"Tevkifat oranı yalnızca tevkifatlı satırlarda kullanılabilir. (SıraNo: {request.SiraNo})", errorCode: 400);
        }

        if (request.KdvUygulamaTipi == (int)KdvUygulamaTipi.Tevkifatli)
        {
            if (!request.TevkifatPay.HasValue || !request.TevkifatPayda.HasValue)
                throw new BaseException($"Tevkifatlı satırda tevkifat oranı zorunludur. (SıraNo: {request.SiraNo})", errorCode: 400);

            if (!DesteklenenTevkifatOranlari.Contains((request.TevkifatPay.Value, request.TevkifatPayda.Value)))
                throw new BaseException($"Geçersiz tevkifat oranı: {request.TevkifatPay}/{request.TevkifatPayda}. (SıraNo: {request.SiraNo})", errorCode: 400);

            if (request.KdvIstisnaTanimId.HasValue)
                throw new BaseException($"Tevkifatlı satırda KDV istisna tanımı seçilemez. (SıraNo: {request.SiraNo})", errorCode: 400);
        }
        else if (request.KdvUygulamaTipi != (int)KdvUygulamaTipi.Kdvli)
        {
            if (!request.KdvIstisnaTanimId.HasValue)
                throw new BaseException(
                    $"KDV'li olmayan satırda KDV istisna tanımı zorunludur. (SıraNo: {request.SiraNo})",
                    errorCode: 400);

            await ValidateKdvIstisnaTanimAsync(
                request.KdvIstisnaTanimId.Value,
                request.KdvUygulamaTipi,
                belge.BelgeTarihi,
                ResolveKdvIslemYonu(belge.BelgeTipi),
                cancellationToken);
        }

        if ((request.SatirTipi == SatisBelgesiSatirTipi.Urun || request.TasinirKartId.HasValue) && !request.DepoId.HasValue)
        {
            throw new BaseException($"Stok/ürün satırlarında depo seçimi zorunludur. (SıraNo: {request.SiraNo})", errorCode: 400);
        }

        if (request.TasinirKartId.HasValue)
        {
            await ValidateTasinirKartAsync(request.TasinirKartId.Value, belge.TesisId, cancellationToken);
        }

        if (request.DepoId.HasValue)
        {
            await ValidateDepoAsync(request.DepoId.Value, belge.TesisId, cancellationToken);
        }
    }

    private async Task ValidateTasinirKartAsync(
        int tasinirKartId,
        int? tesisId,
        CancellationToken cancellationToken)
    {
        var kart = await _db.TasinirKartlar
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tasinirKartId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException($"Taşınır kart bulunamadı. (Id: {tasinirKartId})", errorCode: 400);

        if (!kart.AktifMi)
            throw new BaseException($"Taşınır kart pasif durumda: '{kart.StokKodu} — {kart.Ad}'", errorCode: 400);

        if (tesisId.HasValue && kart.TesisId.HasValue && kart.TesisId != tesisId)
            throw new BaseException($"Taşınır kart seçili çalışma tesisiyle uyumlu değil: '{kart.StokKodu} — {kart.Ad}'", errorCode: 400);
    }

    private async Task ValidateDepoAsync(
        int depoId,
        int? tesisId,
        CancellationToken cancellationToken)
    {
        var depo = await _db.Depolar
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == depoId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException($"Depo bulunamadı. (Id: {depoId})", errorCode: 400);

        if (!depo.AktifMi)
            throw new BaseException($"Depo pasif durumda: '{depo.Kod} — {depo.Ad}'", errorCode: 400);

        if (tesisId.HasValue && depo.TesisId.HasValue && depo.TesisId != tesisId)
            throw new BaseException($"Depo seçili çalışma tesisiyle uyumlu değil: '{depo.Kod} — {depo.Ad}'", errorCode: 400);
    }

    /// <summary>
    /// Belgenin işlem yönünü (satış/alış) otoriter kaynak olan <see cref="SatisBelgesi.BelgeTipi"/>
    /// üzerinden, mevcut <see cref="SatisBelgesiTipiExtensions"/> sınıflandırmasıyla belirler.
    /// Bir belge tipi ne satış ne alış olarak sınıflandırılamıyorsa (yeni eklenen, sınıflandırılmamış
    /// bir tip vb.) sessizce satış varsayılmaz — açık bir doğrulama hatası fırlatılır.
    /// </summary>
    private static KdvIslemYonu ResolveKdvIslemYonu(SatisBelgesiTipi belgeTipi)
    {
        if (belgeTipi.IsSatisBelgesi())
            return KdvIslemYonu.Satis;

        if (belgeTipi.IsAlisBelgesi())
            return KdvIslemYonu.Alis;

        throw new BaseException(
            $"Belge tipi ({belgeTipi}) satış veya alış işlemi olarak sınıflandırılamadı; KDV istisna tanımı doğrulanamaz.",
            errorCode: 400);
    }

    private async Task ValidateKdvIstisnaTanimAsync(
        int kdvIstisnaTanimId,
        int kdvUygulamaTipi,
        DateTime belgeTarihi,
        KdvIslemYonu islemYonu,
        CancellationToken cancellationToken)
    {
        var tanim = await _db.KdvIstisnaTanimlari
            .FirstOrDefaultAsync(x => x.Id == kdvIstisnaTanimId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException(
                $"KDV istisna tanımı bulunamadı. (Id: {kdvIstisnaTanimId})",
                errorCode: 400);

        if (!tanim.AktifMi)
            throw new BaseException(
                $"KDV istisna tanımı pasif durumda: '{tanim.Kod} — {tanim.Ad}'",
                errorCode: 400);

        if (tanim.UygulamaTipi != (KdvUygulamaTipi)kdvUygulamaTipi)
            throw new BaseException(
                $"KDV istisna tanımının uygulama tipi ({tanim.UygulamaTipi}) " +
                $"satırın uygulama tipiyle ({(KdvUygulamaTipi)kdvUygulamaTipi}) uyuşmuyor. " +
                $"Tanım: '{tanim.Kod} — {tanim.Ad}'",
                errorCode: 400);

        if (islemYonu == KdvIslemYonu.Satis && !tanim.SatisIslemlerindeKullanilirMi)
            throw new BaseException(
                $"KDV istisna tanımı satış işlemlerinde kullanılamaz: '{tanim.Kod} — {tanim.Ad}'",
                errorCode: 400);

        if (islemYonu == KdvIslemYonu.Alis && !tanim.AlisIslemlerindeKullanilirMi)
            throw new BaseException(
                $"KDV istisna tanımı alış işlemlerinde kullanılamaz: '{tanim.Kod} — {tanim.Ad}'",
                errorCode: 400);

        // Geçerlilik tarih aralığı
        if (tanim.GecerlilikBaslangicTarihi.HasValue && belgeTarihi < tanim.GecerlilikBaslangicTarihi.Value)
            throw new BaseException(
                $"KDV istisna tanımı belge tarihi itibarıyla henüz geçerli değil: " +
                $"'{tanim.Kod} — {tanim.Ad}' (Başlangıç: {tanim.GecerlilikBaslangicTarihi:dd.MM.yyyy})",
                errorCode: 400);

        if (tanim.GecerlilikBitisTarihi.HasValue && belgeTarihi > tanim.GecerlilikBitisTarihi.Value)
            throw new BaseException(
                $"KDV istisna tanımının geçerlilik süresi belge tarihi itibarıyla dolmuş: " +
                $"'{tanim.Kod} — {tanim.Ad}' (Bitiş: {tanim.GecerlilikBitisTarihi:dd.MM.yyyy})",
                errorCode: 400);
    }

    // ──────────────────────────────────────────────
    //  Private — Belge No Üretimi
    // ──────────────────────────────────────────────

    private async Task<string> GenerateBelgeNoAsync(
        DateTime belgeTarihi,
        CancellationToken cancellationToken)
    {
        var tarihPrefiksi = belgeTarihi.ToString("yyyyMMdd");
        var prefix = $"ST-{tarihPrefiksi}-";

        // Aynı güne ait en büyük belge numarasını bul
        var maxBelgeNo = await _db.SatisBelgeleri
            .Where(x => x.BelgeNo.StartsWith(prefix))
            .Select(x => x.BelgeNo)
            .OrderByDescending(x => x)
            .FirstOrDefaultAsync(cancellationToken);

        int sequence = 1;
        if (maxBelgeNo is not null)
        {
            // ST-20260525-000001 → "000001" → 1 + 1
            var seqPart = maxBelgeNo[(prefix.Length)..];
            if (int.TryParse(seqPart, out var lastSeq))
            {
                sequence = lastSeq + 1;
            }
        }

        return $"{prefix}{sequence:D6}";
    }

    // ──────────────────────────────────────────────
    //  Private — Duplicate Kontrolleri
    // ──────────────────────────────────────────────

    private async Task ThrowIfBelgeNoDuplicateAsync(
        string belgeNo,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.SatisBelgeleri
            .Where(x => x.BelgeNo == belgeNo && !x.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(x => x.Id != excludeId.Value);

        var exists = await query.AnyAsync(cancellationToken);
        if (exists)
            throw new BaseException($"'{belgeNo}' belge numarası zaten kullanılıyor.", errorCode: 400);
    }

    private async Task ThrowIfKaynakDuplicateAsync(
        SatisKaynakModulu kaynakModul,
        string? kaynakTipi,
        string? kaynakId,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        if (kaynakId is null) return;

        var query = _db.SatisBelgeleri
            .Where(x => !x.IsDeleted
                        && x.KaynakModul == kaynakModul
                        && x.KaynakTipi == kaynakTipi
                        && x.KaynakId == kaynakId);

        if (excludeId.HasValue)
            query = query.Where(x => x.Id != excludeId.Value);

        var exists = await query.AnyAsync(cancellationToken);
        if (exists)
        {
            throw new BaseException(
                $"Bu kaynaktan zaten bir satış belgesi oluşturulmuş. " +
                $"(Modül: {kaynakModul}, Tip: {kaynakTipi}, KaynakId: {kaynakId})",
                errorCode: 400);
        }
    }

    private async Task<int?> ResolveWriteTesisIdAsync(
        int? requestedTesisId,
        CancellationToken cancellationToken,
        int? existingTesisId = null)
    {
        _ = cancellationToken;

        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var resolved = requestedTesisId ?? existingTesisId;

        if (scope.IsScoped)
        {
            if (!resolved.HasValue)
            {
                if (scope.TesisIds.Count == 1)
                {
                    resolved = scope.TesisIds.First();
                }
                else
                {
                    throw new BaseException("Tesis seçimi zorunludur.", errorCode: 400);
                }
            }

            if (!scope.TesisIds.Contains(resolved!.Value))
            {
                throw new BaseException("Seçilen tesis için yetkiniz bulunmuyor.", errorCode: 403);
            }
        }

        return resolved is > 0 ? resolved : null;
    }

    /// <summary>
    /// Otoriter kurum sahipliği zinciri: TesisId -> Tesis.KurumId. Tesis de ITenantEntity
    /// olduğundan bu sorgu ZATEN aktif kurum bağlamına göre filtrelenir (StysAppDbContext'in
    /// global query filter'ı) - scoped bir kullanıcı başka kuruma ait bir tesisi burada
    /// GÖREMEZ (404 alır), SuperAdmin tüm tesisleri görür. Bu yüzden ayrıca elle bir
    /// "aktif kurum == tesis kurumu" kontrolü yapmaya gerek yoktur; sorgunun kendisi bunu
    /// zaten garanti eder.
    /// </summary>
    private async Task<int> ResolveKurumIdFromTesisAsync(int tesisId, CancellationToken cancellationToken)
    {
        var kurumId = await _db.Tesisler
            .AsNoTracking()
            .Where(x => x.Id == tesisId && !x.IsDeleted)
            .Select(x => (int?)x.KurumId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!kurumId.HasValue)
            throw new BaseException($"Tesis bulunamadı. (Id: {tesisId})", errorCode: 404);

        return kurumId.Value;
    }

    // ──────────────────────────────────────────────
    //  Private — Satır Oluşturma ve Hesaplama
    // ──────────────────────────────────────────────

    private static decimal ResolveRateBasedAmount(decimal baseAmount, decimal rate, decimal fallbackAmount)
    {
        if (rate > 0)
        {
            return Math.Round(baseAmount * rate / 100m, 2, MidpointRounding.AwayFromZero);
        }

        // fallbackAmount, kullanıcı tarafından doğrudan girilen bir tutar olabilir (oran
        // verilmediğinde) - oran bazlı dala eşit şekilde 2 ondalık/AwayFromZero'ya
        // yuvarlanmazsa, doğrudan tutar girilen satırlarda kuruş farkı oluşabilir.
        return SatisBelgesiTutarHesaplayici.Yuvarla(Math.Max(0m, fallbackAmount));
    }

    private static decimal ResolveLineRate(decimal amount, decimal baseAmount)
    {
        if (amount <= 0 || baseAmount <= 0)
        {
            return 0m;
        }

        return Math.Round(amount * 100m / baseAmount, 4, MidpointRounding.AwayFromZero);
    }

    private static SatisBelgesiSatiri CreateSatirFromRequest(CreateSatisBelgesiSatiriRequest request)
    {
        var brutMatrah = request.Miktar * request.BirimFiyat;
        var indirimOrani = request.IndirimOrani > 0
            ? request.IndirimOrani
            : ResolveLineRate(request.IndirimTutari, brutMatrah);
        var indirimTutari = ResolveRateBasedAmount(brutMatrah, indirimOrani, request.IndirimTutari);
        if (indirimTutari > brutMatrah)
        {
            throw new BaseException("İndirim tutarı satır matrahını aşamaz.", errorCode: 400);
        }

        // Matrah kolonu decimal(18,2)'dir (bkz. StysAppDbContext) - Miktar*BirimFiyat (ikisi de
        // decimal(18,2)) 4 ondalık basamağa kadar üretebilir; KDV/ÖTV/ÖİV/konaklama vergisi
        // hesaplamaları ve SatirToplami'nin, veritabanına yazılacak (2 ondalık) Matrah ile
        // TUTARLI kalması için matrah burada, kullanılmadan önce yuvarlanır.
        var matrah = SatisBelgesiTutarHesaplayici.Yuvarla(brutMatrah - indirimTutari);
        var kdvOrani = request.KdvOrani;

        // İstisna / kapsam dışı → KDV hesaplanmaz
        var kdvTutari = KdvHesaplanmayanTipler.Contains(request.KdvUygulamaTipi)
            ? 0m
            : SatisBelgesiTutarHesaplayici.Yuvarla(matrah * kdvOrani / 100m);

        var tevkifatTutari = 0m;
        if (request.KdvUygulamaTipi == (int)KdvUygulamaTipi.Tevkifatli && request.TevkifatPay.HasValue && request.TevkifatPayda.HasValue && request.TevkifatPayda.Value > 0)
        {
            tevkifatTutari = SatisBelgesiTutarHesaplayici.Yuvarla(kdvTutari * request.TevkifatPay.Value / request.TevkifatPayda.Value);
        }

        var otvOrani = request.OtvOrani > 0
            ? request.OtvOrani
            : ResolveLineRate(request.OtvTutari, matrah);
        var otvTutari = ResolveRateBasedAmount(matrah, otvOrani, request.OtvTutari);

        var oivOrani = request.OivOrani > 0
            ? request.OivOrani
            : ResolveLineRate(request.OivTutari, matrah);
        var oivTutari = ResolveRateBasedAmount(matrah, oivOrani, request.OivTutari);

        var konaklamaVergisiOrani = request.KonaklamaVergisiOrani > 0
            ? request.KonaklamaVergisiOrani
            : ResolveLineRate(request.KonaklamaVergisiTutari, matrah);
        var konaklamaVergisiTutari = ResolveRateBasedAmount(matrah, konaklamaVergisiOrani, request.KonaklamaVergisiTutari);

        // SatirToplami = Matrah + Kdv - Tevkifat + Otv + Oiv + KonaklamaVergisi (bkz.
        // SatisBelgesiTutarHesaplayici) - ÖTV/ÖİV/konaklama vergisi ÖNCEDEN satıra
        // yazılıyordu ama bu toplama hiç DAHIL EDİLMİYORDU; bu formül tek, paylaşılan
        // hesaplayıcıdan gelir ki muhasebe fişi stratejileriyle TUTARSIZLAŞMASIN.
        var satirToplami = SatisBelgesiTutarHesaplayici.HesaplaSatirToplami(
            matrah, kdvTutari, tevkifatTutari, otvTutari, oivTutari, konaklamaVergisiTutari);

        return new SatisBelgesiSatiri
        {
            SiraNo = request.SiraNo,
            SatirTipi = request.SatirTipi,
            Aciklama = request.Aciklama,
            TasinirKartId = request.TasinirKartId,
            DepoId = request.DepoId,
            Birim = string.IsNullOrWhiteSpace(request.Birim) ? "Adet" : request.Birim.Trim(),
            Miktar = request.Miktar,
            BirimFiyat = request.BirimFiyat,
            IndirimOrani = indirimOrani,
            IndirimTutari = indirimTutari,
            Matrah = matrah,
            KdvUygulamaTipi = (KdvUygulamaTipi)request.KdvUygulamaTipi,
            KdvIstisnaTanimId = request.KdvIstisnaTanimId,
            KdvOrani = kdvOrani,
            KdvTutari = kdvTutari,
            TevkifatPay = request.TevkifatPay,
            TevkifatPayda = request.TevkifatPayda,
            TevkifatTutari = tevkifatTutari,
            OtvOrani = otvOrani,
            OtvTutari = otvTutari,
            OivOrani = oivOrani,
            OivTutari = oivTutari,
            KonaklamaVergisiOrani = konaklamaVergisiOrani,
            KonaklamaVergisiTutari = konaklamaVergisiTutari,
            SatirToplami = satirToplami,
            KaynakSatirId = request.KaynakSatirId
        };
    }

    private static void HesaplaBelgeToplamlari(SatisBelgesi belge)
    {
        belge.ToplamMatrah = belge.Satirlar.Where(s => !s.IsDeleted).Sum(s => s.Matrah);
        belge.ToplamKdv = belge.Satirlar.Where(s => !s.IsDeleted).Sum(s => s.KdvTutari);
        belge.GenelToplam = belge.Satirlar.Where(s => !s.IsDeleted).Sum(s => s.SatirToplami);
    }

    // ──────────────────────────────────────────────
    //  Private — Belge Güncelleme
    // ──────────────────────────────────────────────

    private async Task ApplyBelgeUpdatesAsync(
        SatisBelgesi belge,
        UpdateSatisBelgesiRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.BelgeNo))
            belge.BelgeNo = request.BelgeNo;

        if (request.BelgeTipi.HasValue)
            belge.BelgeTipi = request.BelgeTipi.Value;

        if (request.TesisId.HasValue)
            belge.TesisId = request.TesisId;

        belge.CariKartId = request.CariKartId;

        if (request.BelgeTarihi.HasValue)
            belge.BelgeTarihi = request.BelgeTarihi.Value;

        if (request.VadeTarihi.HasValue)
            belge.VadeTarihi = request.VadeTarihi;

        if (request.MusteriUnvan is not null)
            belge.MusteriUnvan = request.MusteriUnvan;

        if (request.MusteriAdSoyad is not null)
            belge.MusteriAdSoyad = request.MusteriAdSoyad;

        if (request.MusteriVergiNo is not null)
            belge.MusteriVergiNo = request.MusteriVergiNo;

        if (request.MusteriTcKimlikNo is not null)
            belge.MusteriTcKimlikNo = request.MusteriTcKimlikNo;

        if (request.MusteriVergiDairesi is not null)
            belge.MusteriVergiDairesi = request.MusteriVergiDairesi;

        if (request.MusteriAdres is not null)
            belge.MusteriAdres = request.MusteriAdres;

        if (request.MusteriEposta is not null)
            belge.MusteriEposta = request.MusteriEposta;

        if (request.MusteriTelefon is not null)
            belge.MusteriTelefon = request.MusteriTelefon;

        if (request.KurumsalMi.HasValue)
            belge.KurumsalMi = request.KurumsalMi.Value;

        if (request.Aciklama is not null)
            belge.Aciklama = request.Aciklama;

        // Kurumsal/bireysel validasyonları
        if (belge.KurumsalMi)
        {
            if (string.IsNullOrWhiteSpace(belge.MusteriUnvan))
                throw new BaseException("Kurumsal müşteri için ünvan zorunludur.", errorCode: 400);
            if (string.IsNullOrWhiteSpace(belge.MusteriVergiNo))
                throw new BaseException("Kurumsal müşteri için vergi numarası zorunludur.", errorCode: 400);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(belge.MusteriAdSoyad))
                throw new BaseException("Bireysel müşteri için ad soyad zorunludur.", errorCode: 400);
        }
        _ = cancellationToken;
    }

    private async Task UpdateSatirlarAsync(
        SatisBelgesi belge,
        List<CreateSatisBelgesiSatiriRequest> yeniSatirlar,
        CancellationToken cancellationToken)
    {
        // Mevcut satırları soft-delete
        foreach (var mevcutSatir in belge.Satirlar)
        {
            mevcutSatir.IsDeleted = true;
        }

        // Yeni satırları ekle
        belge.Satirlar.Clear();
        foreach (var satirRequest in yeniSatirlar)
        {
            await ValidateSatirRequestAsync(satirRequest, belge, cancellationToken);
            var satir = CreateSatirFromRequest(satirRequest);
            belge.Satirlar.Add(satir);
        }
    }
}
