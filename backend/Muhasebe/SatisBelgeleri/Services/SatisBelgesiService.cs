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

    /// <summary>
    /// Güncellenebilirlik/silinebilirlik/onaya-gönderilebilirlik/iptal-edilebilirlik kararları
    /// ARTIK ortak, saf TicariBelgeIslemYetkisi policy'sinden alınır (bkz. görev A/C.1-C.3/C.7) -
    /// bu kurallar SatisBelgesiService VE TicariBelgeService (operasyon uygulama katmanı) içinde
    /// AYRI AYRI yeniden uygulanmaz.
    /// </summary>
    private static bool BelgeGuncellenebilirMi(SatisBelgesi belge)
        => TicariBelgeIslemYetkisi.GuncellenebilirMi(belge.TicariDurum, belge.MuhasebeDurumu);

    private static bool BelgeSilinebilirMi(SatisBelgesi belge)
        => TicariBelgeIslemYetkisi.SilinebilirMi(belge.TicariDurum, belge.MuhasebeDurumu, belge.FaturalamaDurumu);

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
        q => q.Include(x => x.Satirlar).Include(x => x.CariKart).Include(x => x.IadeEdilenBelge);

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
            .Include(x => x.IadeEdilenBelge)
            .Where(x => !x.IsDeleted);

        // DAHİLİ erişim kapsamı filtresi (bkz. SatisBelgesiFilterDto.ErisimKapsamiTesisIdleri) -
        // null ise (mevcut muhasebe ekranı) UYGULANMAZ; boş (ama null olmayan) bir koleksiyon ise
        // sonuç KESİNLİKLE boş döner - IEnumerable.Contains() üzerinde boş liste bunu doğal olarak sağlar.
        if (filter.ErisimKapsamiTesisIdleri is not null)
        {
            var kapsamTesisIdleri = filter.ErisimKapsamiTesisIdleri;
            query = query.Where(x => x.TesisId.HasValue && kapsamTesisIdleri.Contains(x.TesisId.Value));
        }

        if (filter.TesisId.HasValue)
            query = query.Where(x => x.TesisId == filter.TesisId.Value);

        if (filter.BelgeTipleri is { Count: > 0 })
            query = query.Where(x => filter.BelgeTipleri.Contains(x.BelgeTipi));

        if (filter.Durum.HasValue)
            query = query.Where(x => x.Durum == filter.Durum.Value);

        if (filter.TicariDurum.HasValue)
            query = query.Where(x => x.TicariDurum == filter.TicariDurum.Value);

        if (filter.MuhasebeDurumu.HasValue)
            query = query.Where(x => x.MuhasebeDurumu == filter.MuhasebeDurumu.Value);

        if (filter.FaturalamaDurumu.HasValue)
            query = query.Where(x => x.FaturalamaDurumu == filter.FaturalamaDurumu.Value);

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

        if (!string.IsNullOrWhiteSpace(filter.KarsiTarafFaturaNo))
        {
            var normalizedFilter = filter.KarsiTarafFaturaNo.Trim();
            query = query.Where(x => x.KarsiTarafFaturaNo != null && x.KarsiTarafFaturaNo.Contains(normalizedFilter));
        }

        if (filter.IadeEdilenBelgeId.HasValue)
            query = query.Where(x => x.IadeEdilenBelgeId == filter.IadeEdilenBelgeId.Value);

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

        // 1b. Karşı taraf fatura numarası / iade edilen belge referansı — TASLAK aşamasında
        // henüz verilmeyebilir (geriye uyumluluk), ama VERİLDİYSE hemen tam olarak doğrulanır.
        var karsiTarafFaturaNo = NormalizeKarsiTarafFaturaNoOrNull(request.BelgeTipi, request.KarsiTarafFaturaNo);
        if (karsiTarafFaturaNo is not null)
        {
            await ThrowIfKarsiTarafFaturaNoDuplicateAsync(
                kurumId, request.CariKartId, karsiTarafFaturaNo, excludeId: null, cancellationToken);
        }

        if (request.IadeEdilenBelgeId.HasValue)
        {
            await ValidateVeGetIadeEdilenBelgeAsync(
                request.BelgeTipi, request.IadeEdilenBelgeId, kurumId, request.CariKartId, request.BelgeTarihi,
                selfId: null, cancellationToken);
        }

        // 2. Belge no üret (isteğe bağlı override)
        var belgeNo = request.BelgeNo ?? await GenerateBelgeNoAsync(request.BelgeTarihi, cancellationToken);

        // 3. Ana belge entity'sini oluştur
        var belge = new SatisBelgesi
        {
            KurumId = kurumId,
            BelgeNo = belgeNo,
            BelgeTipi = request.BelgeTipi,
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
            Aciklama = request.Aciklama,
            KarsiTarafFaturaNo = karsiTarafFaturaNo,
            IadeEdilenBelgeId = request.IadeEdilenBelgeId
        };

        // Otoriter durum ataması (bkz. SatisBelgesiDurumProjection.OtoriterDurumlariAta) - üç
        // yeni alan burada OLUŞTURULUR; eski Durum bu üçünden TÜRETİLİR (geriye uyumluluk).
        SatisBelgesiDurumProjection.OtoriterDurumlariAta(
            belge,
            TicariBelgeDurumu.Taslak,
            TicariBelgeMuhasebeDurumu.Bekliyor,
            SatisBelgesiDurumProjection.ProjeBaslangicFaturalamaDurumu(belge.BelgeTipi));

        // 4. Satırları oluştur ve KDV hesapla
        foreach (var satirRequest in request.Satirlar ?? [])
        {
            await ValidateSatirRequestAsync(satirRequest, belge, cancellationToken);
            var satir = CreateSatirFromRequest(satirRequest);
            belge.Satirlar.Add(satir);
        }

        // 4b. İade satırı kaynak bağlantısı/miktar — kilitsiz, yalnızca BU belgenin kendi
        // satırları için erken kontrol (bkz. ValidateIadeSatirlariAsync). Diğer belgelerle
        // kümülatif/kilitli nihai kontrol en geç muhasebe onayına gönderme aşamasında yapılır.
        await ValidateIadeSatirlariAsync(belge, kilitliKumulatifKontrol: false, cancellationToken);

        // 5. Belge toplamlarını hesapla
        HesaplaBelgeToplamlari(belge);

        await Repository.AddAsync(belge);
        await SaveChangesTranslatingKarsiTarafFaturaNoConflictAsync(cancellationToken);
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

        // Durum kontrolü — OTORİTER: TicariDurum=Taslak YA DA MuhasebeDurumu=Reddedildi (bkz. C.1).
        if (!BelgeGuncellenebilirMi(belge))
        {
            throw new BaseException(
                $"'{belge.Durum}' durumundaki bir satış belgesi güncellenemez. " +
                "Sadece Taslak veya Reddedildi durumundaki belgeler güncellenebilir.",
                errorCode: 400);
        }

        // Reddedildi → Taslak durumuna döndürülecekse RedNedeni temizlenir - GERÇEK OTORİTER durum
        // ataması (TicariDurum/MuhasebeDurumu/FaturalamaDurumu + türetilen legacy Durum) burada
        // YAPILMAZ, aşağıda (ApplyBelgeUpdatesAsync sonrası, kaydetmeden hemen önce) NİHAİ BelgeTipi
        // ile TEK SEFERDE yapılır - iki giriş yolu (Taslak'ta kalma / Reddedildi'den dönme) HER
        // ZAMAN aynı (Taslak, Bekliyor, ...) hedef kombinasyonunda birleştiğinden erken/geçici bir
        // hesaplamaya gerek YOKTUR.
        if (belge.MuhasebeDurumu == TicariBelgeMuhasebeDurumu.Reddedildi)
        {
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
        else if ((request.BelgeTipi.HasValue || request.TesisId.HasValue) && belge.CariKartId.HasValue)
        {
            // CariKartId bu istekte YENİDEN gönderilmemiş olsa bile, belge YÖNÜ (BelgeTipi) veya
            // TESİSİ değişiyorsa mevcut cari NİHAİ değerlerle yeniden doğrulanır (bkz. görev 4) -
            // aksi halde ör. bir tedarikçi carisi belge satış yönüne çevrilirken sessizce asılı
            // kalabilir, ya da cari başka bir tesise ait olduğu halde fark edilmeyebilirdi.
            // Uyumsuzsa güncelleme AÇIKÇA reddedilir (sessizce temizlenmez/değiştirilmez).
            var mevcutCari = await ResolveAndValidateCariKartAsync(
                belge.CariKartId.Value,
                request.TesisId,
                request.BelgeTipi ?? belge.BelgeTipi,
                cancellationToken);
            ApplyCariSnapshotToUpdateRequest(request, mevcutCari);
        }

        // NİHAİ belge tipi - ApplyBelgeUpdatesAsync HENÜZ çalışmadığından belge.BelgeTipi burada
        // hâlâ ESKİ değeri taşır; request.BelgeTipi verilmişse o, verilmemişse mevcut değer NİHAİ
        // tiptir (ApplyBelgeUpdatesAsync'in belge.BelgeTipi'yi ayarlarken kullandığı AYNI kural).
        var nihaiBelgeTipi = request.BelgeTipi ?? belge.BelgeTipi;
        var nihaiBelgeIadeTipiMi = nihaiBelgeTipi is SatisBelgesiTipi.SatisIadeFaturasi or SatisBelgesiTipi.AlisIadeFaturasi;

        // İADE REFERANSI KALDIRMA + SATIRLARI TEMİZLEME — TEK, ATOMİK İSTİSNA (bkz. görev 2/3):
        // istemci AÇIKÇA IadeEdilenBelgeReferansiKaldir=true VE Satirlar=[] gönderiyorsa (belgenin
        // GERÇEKTEN kaldırılacak bir referansı varken VE NİHAİ belge tipi HÂLÂ bir iade tipiyse),
        // bu "referansı kaldır ve artık kaynaksız kalacak mevcut satırları da temizle" isteği
        // olarak kabul edilir - genel "Satirlar=[] her zaman 400" kuralının TEK istisnasıdır.
        // Belgenin kaldırılacak bir referansı YOKSA (zaten null'sa) YA DA bu istekle NİHAİ tip
        // artık iade tipi DEĞİLSE (iadeden normale geçiş) bu istisna UYGULANMAZ - normal kural
        // (400) geçerli kalır; aksi halde bu istisna, boş satırlı bir NORMAL belge oluşturmak
        // için istismar edilebilirdi (bkz. görev 3). İadeden normale geçişte satırlar zaten BOŞ
        // DEĞİL (istemci kaynak satırları normal satıra dönüştürüp gönderir), bu yüzden bu
        // kısıtlama meşru "normalden iadeye" senaryosunu ENGELLEMEZ.
        var referansKaldirVeSatirlariTemizle =
            request.IadeEdilenBelgeReferansiKaldir
            && belge.IadeEdilenBelgeId.HasValue
            && nihaiBelgeIadeTipiMi
            && request.Satirlar is { Count: 0 };

        // Satirlar alanı AÇIKÇA (null DEĞİL) gönderilmiş ama BOŞSA - yukarıdaki TEK istisna
        // dışında - reddedilir. Bu, "satırları dokunmadan bırak" (null) ile "tüm satırları sil"
        // (client'ın muhtemelen KASTETMEDİĞİ, ör. bir önceki adımda satırların yanlışlıkla
        // boşaltıldığı) durumlarını AYIRT eder; Update uç noktası (bu tek istisna hariç) satır
        // SİLME için tasarlanmamıştır (bkz. görev 4a).
        if (request.Satirlar is { Count: 0 } && !referansKaldirVeSatirlariTemizle)
        {
            throw new BaseException(
                "Satirlar alanı gönderildiyse en az bir satır içermelidir; satırları değiştirmeden " +
                "bırakmak için bu alanı hiç göndermeyin (null).",
                errorCode: 400);
        }

        // Ana alanları güncelle
        await ApplyBelgeUpdatesAsync(belge, request, cancellationToken);

        // Satırlar gönderildiyse güncelle
        if (request.Satirlar is { Count: > 0 })
        {
            await UpdateSatirlarAsync(belge, request.Satirlar, cancellationToken);
        }
        else if (referansKaldirVeSatirlariTemizle)
        {
            // Referans kaldırıldı (ApplyBelgeUpdatesAsync belge.IadeEdilenBelgeId'yi zaten null
            // yaptı) - mevcut TÜM satırlar soft-delete edilir, böylece eski KaynakSatirId'ler
            // hiçbir AKTİF satırda kalmaz (bkz. görev 2).
            foreach (var mevcutSatir in belge.Satirlar)
            {
                mevcutSatir.IsDeleted = true;
            }
            belge.Satirlar.Clear();
        }

        // İade satırlarının kaynak bağlantısı HER ZAMAN yeniden doğrulanır - satırlar bu istekte
        // GÖNDERİLMEMİŞ olsa (dokunulmadan taşınsa) bile, IadeEdilenBelgeId bu istekle DEĞİŞMİŞ
        // olabilir (bkz. ApplyBelgeUpdatesAsync); bu durumda mevcut (değişmemiş) satırların
        // KaynakSatirId'lerinin YENİ IadeEdilenBelgeId'ye ait olup olmadığı buradan geçerken
        // yakalanır (bkz. görev 4b) - normal SatisFaturasi/AlisFaturasi için bu çağrı no-op'tur.
        await ValidateIadeSatirlariAsync(belge, kilitliKumulatifKontrol: false, cancellationToken);

        HesaplaBelgeToplamlari(belge);

        // OTORİTER durum ataması - UpdateAsync'in GEÇERLİ giriş kombinasyonlarının (TicariDurum=
        // Taslak YA DA MuhasebeDurumu=Reddedildi - bkz. BelgeGuncellenebilirMi) HER İKİSİ de bu
        // metodun sonunda AYNI (Taslak, Bekliyor, ...) hedef kombinasyonuna ulaşır - bu yüzden
        // koşulsuz TEK bir atama yeterlidir. FaturalamaDurumu, ApplyBelgeUpdatesAsync'in olası
        // şekilde değiştirmiş olabileceği NİHAİ BelgeTipi ile hesaplanır (bkz. görev D).
        SatisBelgesiDurumProjection.OtoriterDurumlariAta(
            belge,
            TicariBelgeDurumu.Taslak,
            TicariBelgeMuhasebeDurumu.Bekliyor,
            SatisBelgesiDurumProjection.ProjeBaslangicFaturalamaDurumu(belge.BelgeTipi));

        _satisBelgesiRepository.Update(belge);
        await SaveChangesTranslatingKarsiTarafFaturaNoConflictAsync(cancellationToken);
        if (belge.CariKartId.HasValue)
        {
            await _db.Entry(belge).Reference(x => x.CariKart).LoadAsync(cancellationToken);
        }

        return Mapper.Map<SatisBelgesiDto>(belge);
    }

    /// <summary>
    /// KurumId+CariKartId+KarsiTarafFaturaNo tekilliği için uygulama seviyesi kontrol
    /// (ThrowIfKarsiTarafFaturaNoDuplicateAsync) yarış koşuluna karşı yeterli DEĞİLDİR - gerçek
    /// güvence DB unique index'idir. Bu yardımcı, o index'in SaveChanges sırasında tetiklediği ham
    /// SQL Server 2601/2627 hatasını, kullanıcıya anlaşılır bir conflict/duplicate hatasına çevirir.
    /// </summary>
    private async Task SaveChangesTranslatingKarsiTarafFaturaNoConflictAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsKarsiTarafFaturaNoUniqueConflict(ex))
        {
            throw new BaseException(
                "Bu karşı taraf fatura numarası, aynı kurum ve cari kart için eşzamanlı bir istekle az önce kaydedilmiş. Lütfen tekrar deneyin.",
                errorCode: 409);
        }
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

        // OTORİTER: yalnızca GERÇEK taslak kombinasyonu silinebilir (bkz. BelgeSilinebilirMi / C.2).
        if (!BelgeSilinebilirMi(belge))
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
        // İade belgelerinde kaynak satırların kümülatif miktar sınırının eşzamanlılığa güvenli
        // şekilde doğrulanabilmesi için (bkz. ValidateIadeSatirlariAsync,
        // ValidateKarsiTarafVeIadeAlanlariAsync üzerinden çağrılır) TÜM metot gövdesi tek bir
        // transaction içinde çalışır - WITH (UPDLOCK, ROWLOCK, HOLDLOCK) ile alınan kaynak satır
        // kilidi ancak bu transaction commit/rollback olana kadar tutulursa anlamlıdır. Normal
        // (iade olmayan) belgeler için davranış değişmez - yalnızca önceden zaten örtük olan
        // transaction artık AÇIK/uzun ömürlüdür.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var belge = await Repository.FirstOrDefaultAsync(
                x => x.Id == id && !x.IsDeleted,
                q => q.Include(x => x.Satirlar))
                ?? throw new BaseException($"Satış belgesi bulunamadı. (Id: {id})", errorCode: 404);

            await ThrowIfMuhasebeFisiIslemiEngellerAsync(belge, "muhasebe onayına gönderme", cancellationToken);

            // OTORİTER giriş kontrolü (bkz. C.3, TicariBelgeIslemYetkisi.MuhasebeOnayinaGonderilebilirMi).
            if (!TicariBelgeIslemYetkisi.MuhasebeOnayinaGonderilebilirMi(belge.TicariDurum, belge.MuhasebeDurumu))
            {
                throw new BaseException(
                    $"Sadece Taslak durumundaki belgeler muhasebe onayına gönderilebilir. Mevcut durum: {belge.Durum}",
                    errorCode: 400);
            }

            // Kapsamlı ön-kontrol (satır, müşteri, KDV, toplam, kaynak duplicate)
            await ValidateBelgeOnayaGonderilebilir(belge, cancellationToken);

            belge.MuhasebeOnayinaGonderilmeTarihi = DateTime.UtcNow;
            SatisBelgesiDurumProjection.OtoriterDurumlariAta(
                belge,
                TicariBelgeDurumu.Hazir,
                TicariBelgeMuhasebeDurumu.Onayda,
                SatisBelgesiDurumProjection.ProjeBaslangicFaturalamaDurumu(belge.BelgeTipi));

            await Repository.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // ──────────────────────────────────────────────
    //  MuhasebeOnaylaAsync
    // ──────────────────────────────────────────────

    public async Task MuhasebeOnaylaAsync(int id, CancellationToken cancellationToken = default)
    {
        // Bkz. MuhasebeOnayinaGonderAsync'teki açıklama - aynı gerekçeyle TÜM metot gövdesi tek
        // bir transaction içinde çalışır.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var belge = await Repository.FirstOrDefaultAsync(
                x => x.Id == id && !x.IsDeleted,
                q => q.Include(x => x.Satirlar))
                ?? throw new BaseException($"Satış belgesi bulunamadı. (Id: {id})", errorCode: 404);

            await ThrowIfMuhasebeFisiIslemiEngellerAsync(belge, "muhasebe onaylama", cancellationToken);

            // OTORİTER giriş kontrolü — TicariBelgeIslemYetkisi.MuhasebeOnaylanabilirMi TEK merkezi
            // kaynaktır; UI (SatisBelgesiDto.MuhasebeOnaylanabilirMi) ve bu endpoint AYNI kuralı
            // kullanır, farklı karar üretemez.
            if (!TicariBelgeIslemYetkisi.MuhasebeOnaylanabilirMi(belge.TicariDurum, belge.MuhasebeDurumu))
            {
                throw new BaseException(
                    $"Sadece Muhasebe Onayında durumundaki belgeler onaylanabilir. Mevcut durum: {belge.Durum}",
                    errorCode: 400);
            }

            // Onay anında içerik tekrar doğrulanır (arada değişiklik olmadığından emin olmak için)
            await ValidateBelgeMuhasebeOnaylanabilir(belge, cancellationToken);

            belge.MuhasebeOnayTarihi = DateTime.UtcNow;
            // STYS tarafından düzenlenen SatisFaturasi/AlisIadeFaturasi -> KesimBekliyor; diğer
            // belge tiplerinde -> Uygulanamaz (bkz. C.4).
            SatisBelgesiDurumProjection.OtoriterDurumlariAta(
                belge,
                TicariBelgeDurumu.Hazir,
                TicariBelgeMuhasebeDurumu.Onaylandi,
                SatisBelgesiDurumProjection.ProjeOnaylandiFaturalamaDurumu(belge.BelgeTipi));

            await Repository.SaveChangesAsync(cancellationToken);
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
                BusinessResult = "MuhasebeOnaylandi"
            });
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // ──────────────────────────────────────────────
    //  FaturaKesAsync
    // ──────────────────────────────────────────────

    /// <summary>
    /// STYS tarafından düzenlenen giden belgelere (SatisFaturasi, AlisIadeFaturasi), kurum +
    /// mali yıl + seri bazlı, eşzamanlılığa güvenli bir resmî fatura numarası atar ve belgeyi
    /// FaturaKesildi durumuna geçirir. Sıra numarası bu iki belge tipi arasında PAYLAŞILIR
    /// (aynı sayaç anahtarı: KurumId+MaliYil+SeriKodu, BelgeTipi anahtara dahil DEĞİLDİR) - aynı
    /// seri seçilirse resmî numara STYS'nin bu seride düzenlediği TÜM giden belgeler arasında
    /// benzersiz ve ardışık ilerler.
    ///
    /// Yön sınıflandırması <see cref="SatisBelgesiTipiExtensions.OtomatikResmiNumaraUretilebilirMi"/>
    /// üzerinden MERKEZİ olarak yapılır (ön kontrol ve kilitli kontrol AYNI metodu kullanır - iki
    /// ayrı hard-coded liste zamanla farklılaşamaz). AlisFaturasi ve SatisIadeFaturasi karşı taraf
    /// tarafından düzenlenen GELEN belgelerdir; bu metot bunları reddeder - tedarikçinin/müşterinin
    /// kendi harici fatura numarası ile STYS'nin ürettiği bu numara KARIŞTIRILMAMALIDIR. Legacy
    /// IadeFaturasi hangi yönü temsil ettiği belirsiz olduğundan TAHMİN EDİLEREK açılmamıştır -
    /// bkz. görev sonuç raporu.
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

        if (!belgeOnOkuma.BelgeTipi.OtomatikResmiNumaraUretilebilirMi())
        {
            throw new BaseException(
                "Otomatik resmî fatura numarası yalnızca STYS tarafından düzenlenen giden belgeler " +
                "(SatisFaturasi, AlisIadeFaturasi) için üretilebilir.",
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

            if (!belge.BelgeTipi.OtomatikResmiNumaraUretilebilirMi())
            {
                throw new BaseException(
                    "Otomatik resmî fatura numarası yalnızca STYS tarafından düzenlenen giden belgeler " +
                    "(SatisFaturasi, AlisIadeFaturasi) için üretilebilir.",
                    errorCode: 400);
            }

            // ── Değişmezlik/tutarlılık invariantları (kilitli okuma sonrası, sayaç/yazımdan ÖNCE) ──
            // ResmiFaturaNo ve FaturalamaDurumu=Kesildi (OTORİTER alan - eski Durum=FaturaKesildi
            // ARTIK yalnızca BUNDAN türetilir) HER ZAMAN BİRLİKTE bulunmalıdır; biri diğeri olmadan
            // asla "sessizce devam edilerek" kabul edilmez - ikisi arasında bir tutarsızlık varsa
            // (ör. elle veri düzeltmesi, eksik migration, vb.) açık bir hata verilir ve mevcut
            // numara/durum ASLA üzerine yazılmaz.
            var resmiNumaraDoluMu = !string.IsNullOrWhiteSpace(belge.ResmiFaturaNo);
            var faturalamaKesildiMi = belge.FaturalamaDurumu == TicariBelgeFaturalamaDurumu.Kesildi;

            if (resmiNumaraDoluMu && !faturalamaKesildiMi)
            {
                throw new BaseException(
                    $"Belgede resmî fatura numarası ({belge.ResmiFaturaNo}) var ancak FaturalamaDurumu 'Kesildi' değil " +
                    $"(FaturalamaDurumu: {belge.FaturalamaDurumu}, legacy Durum: {belge.Durum}); " +
                    $"veri tutarsızlığı, sistem yöneticisine başvurun. (Id: {id})",
                    errorCode: 500);
            }

            if (faturalamaKesildiMi && !resmiNumaraDoluMu)
            {
                throw new BaseException(
                    $"Belge FaturalamaDurumu 'Kesildi' ancak resmî fatura numarası bulunamadı; veri tutarsızlığı, sistem yöneticisine başvurun. (Id: {id})",
                    errorCode: 500);
            }

            if (faturalamaKesildiMi && !belge.FaturaKesimTarihi.HasValue)
            {
                throw new BaseException(
                    $"Belge FaturalamaDurumu 'Kesildi' ancak fatura kesim tarihi bulunamadı; veri tutarsızlığı, sistem yöneticisine başvurun. (Id: {id})",
                    errorCode: 500);
            }

            // İdempotency: belge zaten kesilmişse yeni numara TÜKETİLMEZ, mevcut sonuç
            // döndürülür — ama önce mevcut numaranın gerçekten tutarlı olduğu (format, yıl,
            // seri, çakışma, sayaç durumu) doğrulanır; sessizce devam edilmez, herhangi bir
            // tutarsızlıkta açık hata verilir.
            if (faturalamaKesildiMi)
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

            // OTORİTER giriş kontrolü (bkz. C.6): TicariDurum=Hazir + MuhasebeDurumu=Onaylandi +
            // FaturalamaDurumu=KesimBekliyor - yalnızca "Onaylandi" YETERLİ DEĞİLDİR, belge zaten
            // KesimBekliyor aşamasında (henüz Kesildi/MusteriyeGonderildi'ye geçmemiş) olmalıdır.
            if (belge.TicariDurum != TicariBelgeDurumu.Hazir
                || belge.MuhasebeDurumu != TicariBelgeMuhasebeDurumu.Onaylandi
                || belge.FaturalamaDurumu != TicariBelgeFaturalamaDurumu.KesimBekliyor)
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

            // AlisIadeFaturasi için: numara üretilmeden ÖNCE iade edilen asıl AlisFaturasi'nin
            // (ve bağlı muhasebe fişinin) HÂLÂ geçerli olduğu yeniden doğrulanır - aynı merkezi
            // yardımcı (ValidateVeGetIadeEdilenBelgeAsync) kullanılır. Doğrulama başarısızsa bu
            // noktada sayaç HENÜZ hiç sorgulanmamış/kilitlenmemiştir.
            if (belge.BelgeTipi == SatisBelgesiTipi.AlisIadeFaturasi)
            {
                if (!belge.IadeEdilenBelgeId.HasValue)
                {
                    throw new BaseException(
                        "Alış iade faturasında iade edilen belge referansı bulunamadı; fatura kesilemez.",
                        errorCode: 400);
                }

                await ValidateVeGetIadeEdilenBelgeAsync(
                    belge.BelgeTipi, belge.IadeEdilenBelgeId, belge.KurumId, belge.CariKartId, belge.BelgeTarihi,
                    selfId: belge.Id, cancellationToken);
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
            SatisBelgesiDurumProjection.OtoriterDurumlariAta(
                belge,
                TicariBelgeDurumu.Hazir,
                TicariBelgeMuhasebeDurumu.Onaylandi,
                TicariBelgeFaturalamaDurumu.Kesildi);

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
    /// IsUniqueConflict'in aksine, yalnızca BELİRLİ index'i (KurumId+CariKartId+KarsiTarafFaturaNo)
    /// ihlal eden 2601/2627 hatalarını tanır - SQL Server bu hataların mesajına ihlal edilen
    /// index'in adını gömer. Başka bir unique index (ör. BelgeNo, KaynakModul/KaynakTipi/KaynakId)
    /// ihlal edilirse bu metot false döner ve orijinal exception, karşı taraf fatura numarası
    /// mesajıyla MASKELENMEDEN olduğu gibi yukarı fırlatılmaya devam eder.
    /// </summary>
    private static bool IsKarsiTarafFaturaNoUniqueConflict(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sqlEx &&
               (sqlEx.Number == 2601 || sqlEx.Number == 2627) &&
               sqlEx.Message.Contains(
                   "IX_SatisBelgeleri_KurumId_CariKartId_KarsiTarafFaturaNo",
                   StringComparison.OrdinalIgnoreCase);
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

        // OTORİTER giriş kontrolü — TicariBelgeIslemYetkisi.ReddedilebilirMi TEK merkezi kaynaktır;
        // UI (SatisBelgesiDto.ReddedilebilirMi) ve bu endpoint AYNI kuralı kullanır, farklı karar
        // üretemez.
        if (!TicariBelgeIslemYetkisi.ReddedilebilirMi(belge.TicariDurum, belge.MuhasebeDurumu))
        {
            throw new BaseException(
                $"Sadece Muhasebe Onayında durumundaki belgeler reddedilebilir. Mevcut durum: {belge.Durum}",
                errorCode: 400);
        }

        belge.RedNedeni = redNedeni.Trim();
        SatisBelgesiDurumProjection.OtoriterDurumlariAta(
            belge,
            TicariBelgeDurumu.Hazir,
            TicariBelgeMuhasebeDurumu.Reddedildi,
            SatisBelgesiDurumProjection.ProjeBaslangicFaturalamaDurumu(belge.BelgeTipi));

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

            // İptalde ÜÇ otoriter alan da IptalEdildi olur (bkz. C.7).
            SatisBelgesiDurumProjection.OtoriterDurumlariAta(
                belge,
                TicariBelgeDurumu.IptalEdildi,
                TicariBelgeMuhasebeDurumu.IptalEdildi,
                TicariBelgeFaturalamaDurumu.IptalEdildi);

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
        // OTORİTER (bkz. görev A/C.7): TicariDurum=IptalEdildi zaten iptal edilmiş demektir.
        if (belge.TicariDurum == TicariBelgeDurumu.IptalEdildi)
        {
            throw new BaseException("Belge zaten iptal edilmiş.", 400);
        }

        // OTORİTER: FaturalamaDurumu=Kesildi/MusteriyeGonderildi olan belgeler iptal edilemez -
        // mevcut izin kapsamı GENİŞLETİLMEZ, yalnızca karar kaynağı değişir.
        if (belge.FaturalamaDurumu == TicariBelgeFaturalamaDurumu.Kesildi ||
            belge.FaturalamaDurumu == TicariBelgeFaturalamaDurumu.MusteriyeGonderildi)
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

        // 12. Karşı taraf fatura numarası / iade edilen belge referansı zorunluluk + yeniden
        // doğrulama - bu metot hem MuhasebeOnayinaGonderAsync hem MuhasebeOnaylaAsync tarafından
        // (ValidateBelgeMuhasebeOnaylanabilir üzerinden) çağrıldığından, onaya gönderme ile
        // onaylama ARASINDA asıl belgenin iptal edilmesi/bozulması SESSİZCE kabul edilmez.
        await ValidateKarsiTarafVeIadeAlanlariAsync(belge, cancellationToken);
    }

    /// <summary>
    /// Karşı taraf fatura numarası ve iade edilen belge referansı için onay-aşaması zorunluluk
    /// kontrolleri: gelen belgelerde (AlisFaturasi/SatisIadeFaturasi) KarsiTarafFaturaNo zorunlu,
    /// giden belgelerde (SatisFaturasi/AlisIadeFaturasi) bulunamaz; iade belgelerinde
    /// (SatisIadeFaturasi/AlisIadeFaturasi) IadeEdilenBelgeId zorunlu ve MERKEZİ yardımcı ile
    /// (ValidateVeGetIadeEdilenBelgeAsync) yeniden doğrulanır, iade olmayan belgelerde bulunamaz.
    /// </summary>
    private async Task ValidateKarsiTarafVeIadeAlanlariAsync(SatisBelgesi belge, CancellationToken cancellationToken)
    {
        var gidenBelgeMi = belge.BelgeTipi.StysTarafindanDuzenlenirMi();
        var gelenBelgeMi = belge.BelgeTipi.KarsiTarafTarafindanDuzenlenirMi();
        var iadeBelgeMi = belge.BelgeTipi is SatisBelgesiTipi.SatisIadeFaturasi or SatisBelgesiTipi.AlisIadeFaturasi;

        if (gidenBelgeMi && !string.IsNullOrWhiteSpace(belge.KarsiTarafFaturaNo))
        {
            throw new BaseException(
                "STYS tarafından düzenlenen giden belgelerde karşı taraf fatura numarası bulunamaz.",
                errorCode: 400);
        }

        if (gelenBelgeMi && string.IsNullOrWhiteSpace(belge.KarsiTarafFaturaNo))
        {
            throw new BaseException(
                "Karşı taraf tarafından düzenlenen belgeler için karşı taraf fatura numarası zorunludur.",
                errorCode: 400);
        }

        if (!iadeBelgeMi && belge.IadeEdilenBelgeId.HasValue)
        {
            throw new BaseException(
                "İade faturası olmayan belgelerde iade edilen belge referansı bulunamaz.",
                errorCode: 400);
        }

        if (iadeBelgeMi)
        {
            if (!belge.IadeEdilenBelgeId.HasValue)
            {
                throw new BaseException(
                    "İade faturaları için iade edilen belge referansı zorunludur.",
                    errorCode: 400);
            }

            await ValidateVeGetIadeEdilenBelgeAsync(
                belge.BelgeTipi, belge.IadeEdilenBelgeId, belge.KurumId, belge.CariKartId, belge.BelgeTarihi,
                selfId: belge.Id, cancellationToken);

            // Kaynak satır bağlantısı ve KÜMÜLATİF miktar sınırı — bu noktada IadeEdilenBelgeId
            // zaten zorunlu kılınmış ve doğrulanmıştır. Bu metot (ValidateBelgeOnayaGonderilebilir
            // üzerinden) YALNIZCA MuhasebeOnayinaGonderAsync/MuhasebeOnaylaAsync tarafından, HER
            // ZAMAN açık bir transaction içinde çağrılır - bkz. o metotların gövdesi - bu yüzden
            // kilitliKumulatifKontrol=true burada GÜVENLİDİR.
            await ValidateIadeSatirlariAsync(belge, kilitliKumulatifKontrol: true, cancellationToken);
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

    // ──────────────────────────────────────────────
    //  Private — Karşı Taraf Fatura Numarası / İade Edilen Belge Referansı (merkezi doğrulama)
    // ──────────────────────────────────────────────

    /// <summary>
    /// raw null ise (alan hiç verilmemiş) null döner. Doluysa ÖNCE kontrol karakteri kontrolü
    /// yapılır (bkz. ThrowIfKarsiTarafFaturaNoIcerirKontrolKarakteri) - string.IsNullOrWhiteSpace
    /// KESİNLİKLE kullanılmaz, çünkü .NET'te char.IsWhiteSpace bazı kontrol karakterlerini de
    /// (ör. U+0085 NEL, U+2028) whitespace sayar; bu yüzden "yalnızca whitespace ise null say" kısa
    /// devresi IsNullOrWhiteSpace ile yapılsaydı, TEK BAŞINA bir U+0085 karakterinden oluşan bir
    /// değer sessizce "boş/verilmemiş" sayılıp kontrol karakteri kontrolünü hiç görmeden geçerdi.
    /// Yalnızca normal ASCII boşluktan (' ') oluşan bir değer, kontrol karakteri kontrolünden
    /// GEÇTİKTEN SONRA, Create sırasında null (opsiyonel alan) kabul edilir.
    /// </summary>
    private static string? NormalizeKarsiTarafFaturaNoOrNull(SatisBelgesiTipi belgeTipi, string? raw)
    {
        if (raw is null)
            return null;

        ThrowIfKarsiTarafFaturaNoIcerirKontrolKarakteri(raw);

        if (raw.Trim(' ').Length == 0)
            return null;

        return NormalizeKarsiTarafFaturaNo(belgeTipi, raw);
    }

    /// <summary>
    /// Kontrol karakteri doğrulaması: HAM (raw) değer üzerinde, herhangi bir boş/whitespace kısa
    /// devresinden (string.IsNullOrWhiteSpace, Trim() vb.) ÖNCE çağrılmalıdır - aksi halde bazı
    /// kontrol karakterleri (ör. U+0085 NEL) .NET'in whitespace sınıflandırmasına takılıp
    /// SESSİZCE "boş" sayılabilir. Konumdan (baş/orta/son) BAĞIMSIZ olarak reddeder.
    /// </summary>
    private static void ThrowIfKarsiTarafFaturaNoIcerirKontrolKarakteri(string raw)
    {
        if (raw.Any(char.IsControl))
        {
            throw new BaseException(
                "Karşı taraf fatura numarası satır sonu, tab veya başka bir kontrol karakteri içeremez.",
                errorCode: 400);
        }
    }

    /// <summary>
    /// Karşı taraf fatura numarasını (yalnızca AlisFaturasi/SatisIadeFaturasi için geçerli) trim
    /// eder ve doğrular: 1..50 karakter, kontrol karakteri (satır sonu, tab dahil) içermez. Yazılış
    /// biçimi (büyük/küçük harf vb.) GEREKSİZ YERE DEĞİŞTİRİLMEZ - yalnızca baş/son boşluk temizlenir.
    /// </summary>
    private static string NormalizeKarsiTarafFaturaNo(SatisBelgesiTipi belgeTipi, string raw)
    {
        if (belgeTipi != SatisBelgesiTipi.AlisFaturasi && belgeTipi != SatisBelgesiTipi.SatisIadeFaturasi)
        {
            throw new BaseException(
                "Karşı taraf fatura numarası yalnızca alış faturası veya satış iade faturası için kullanılabilir.",
                errorCode: 400);
        }

        // Kontrol karakteri doğrulaması TRIM'DEN ÖNCE, HAM (raw) değer üzerinde yapılır. Aksi
        // halde String.Trim() tab/satır sonu gibi Unicode boşluk sayılan kontrol karakterlerini
        // (\t, \n, \r vb.) baştan/sondan SESSİZCE siler ve "\tTED-1" gibi bir değer, kontrol
        // karakteri hiç yokmuş gibi kabul edilirdi. Yalnızca normal ASCII boşluk (' ') karakteri
        // trim edilebilir - kontrol karakterleri konumdan (baş/orta/son) BAĞIMSIZ reddedilir.
        ThrowIfKarsiTarafFaturaNoIcerirKontrolKarakteri(raw);

        var trimmed = raw.Trim(' ');

        if (trimmed.Length == 0)
            throw new BaseException("Karşı taraf fatura numarası boş olamaz.", errorCode: 400);

        if (trimmed.Length > 50)
            throw new BaseException("Karşı taraf fatura numarası en fazla 50 karakter olabilir.", errorCode: 400);

        return trimmed;
    }

    /// <summary>
    /// Tekillik anahtarı KurumId+CariKartId+KarsiTarafFaturaNo'dur (global unique DEĞİLDİR - bkz.
    /// görev raporu). Bu, gerçek yarış koşuluna karşı OTORİTER güvence olan DB unique index'inden
    /// ÖNCE, kullanıcıya anlaşılır bir erken ret sağlayan uygulama seviyesi kontroldür.
    /// </summary>
    private async Task ThrowIfKarsiTarafFaturaNoDuplicateAsync(
        int kurumId, int? cariKartId, string karsiTarafFaturaNo, int? excludeId, CancellationToken cancellationToken)
    {
        var query = _db.SatisBelgeleri.Where(x =>
            !x.IsDeleted &&
            x.KurumId == kurumId &&
            x.CariKartId == cariKartId &&
            x.KarsiTarafFaturaNo == karsiTarafFaturaNo);

        if (excludeId.HasValue)
            query = query.Where(x => x.Id != excludeId.Value);

        if (await query.AnyAsync(cancellationToken))
        {
            throw new BaseException(
                $"'{karsiTarafFaturaNo}' numaralı belge bu cari kart için zaten kayıtlı.",
                errorCode: 409);
        }
    }

    /// <summary>
    /// İade edilen belge referansını (SatisIadeFaturasi -> SatisFaturasi, AlisIadeFaturasi ->
    /// AlisFaturasi) MERKEZİ ve tam olarak doğrular: yön uygunluğu, kendine referans, kurum/cari
    /// eşleşmesi, tarih sıralaması, asıl belgenin durumu ve bağlı muhasebe fişinin geçerliliği.
    /// iadeEdilenBelgeId null ise hiçbir şey yapmadan null döner (opsiyonel alan). Hem Create/Update
    /// sırasında (verildiyse) hem MuhasebeOnayina* akışlarında hem FaturaKesAsync'te (AlisIadeFaturasi
    /// için) AYNI bu metot çağrılır - dağınık, birbirinden farklılaşabilecek kontroller OLUŞTURULMAZ.
    /// </summary>
    private async Task<SatisBelgesi?> ValidateVeGetIadeEdilenBelgeAsync(
        SatisBelgesiTipi belgeTipi,
        int? iadeEdilenBelgeId,
        int kurumId,
        int? cariKartId,
        DateTime belgeTarihi,
        int? selfId,
        CancellationToken cancellationToken)
    {
        if (!iadeEdilenBelgeId.HasValue)
            return null;

        if (belgeTipi != SatisBelgesiTipi.SatisIadeFaturasi && belgeTipi != SatisBelgesiTipi.AlisIadeFaturasi)
        {
            throw new BaseException(
                "İade edilen belge referansı yalnızca satış iade faturası veya alış iade faturası için kullanılabilir.",
                errorCode: 400);
        }

        if (selfId.HasValue && iadeEdilenBelgeId.Value == selfId.Value)
            throw new BaseException("Belge kendisini iade edilen belge olarak gösteremez.", errorCode: 400);

        // Doğrudan _db üzerinden (repository değil) - SuperAdmin için bile aşağıda AÇIKÇA kurum
        // eşitliği kontrol edilir; global sorgu filtresine güvenilmez.
        var asilBelge = await _db.SatisBelgeleri
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == iadeEdilenBelgeId.Value, cancellationToken)
            ?? throw new BaseException($"İade edilen belge bulunamadı. (Id: {iadeEdilenBelgeId})", errorCode: 404);

        if (asilBelge.IsDeleted)
            throw new BaseException("İade edilen belge silinmiş.", errorCode: 400);

        // Tenant güvenliği: SuperAdmin dahil, KurumId eşitliği HER ZAMAN zorunludur - başka
        // kurumun belgesi "uygun asıl belge" olarak ASLA kabul edilmez.
        if (asilBelge.KurumId != kurumId)
            throw new BaseException($"İade edilen belge bulunamadı. (Id: {iadeEdilenBelgeId})", errorCode: 404);

        // OTORİTER kaynak: IadeEdilenBelgeEligibility.BeklenenAsilTip - lookup sorgusuyla AYNI eşleme.
        var beklenenAsilTip = IadeEdilenBelgeEligibility.BeklenenAsilTip(belgeTipi);

        if (asilBelge.BelgeTipi != beklenenAsilTip)
        {
            throw new BaseException(
                $"İade edilen belge '{beklenenAsilTip}' tipinde olmalıdır. (Mevcut tip: {asilBelge.BelgeTipi})",
                errorCode: 400);
        }

        // Karşı taraf karışıklığını engelle: serbest metin (müşteri unvanı vb.) DEĞİL, yalnızca
        // CariKartId eşleşmesine bakılır.
        if (!cariKartId.HasValue || !asilBelge.CariKartId.HasValue || cariKartId.Value != asilBelge.CariKartId.Value)
        {
            throw new BaseException(
                "İade edilen belgenin cari kartı, iade faturasınınkiyle eşleşmiyor.",
                errorCode: 400);
        }

        if (belgeTarihi < asilBelge.BelgeTarihi)
        {
            throw new BaseException(
                "İade faturasının tarihi, iade edilen asıl faturanın tarihinden eski olamaz.",
                errorCode: 400);
        }

        if (belgeTipi == SatisBelgesiTipi.SatisIadeFaturasi)
        {
            // OTORİTER: eski Durum=FaturaKesildi karşılığı FaturalamaDurumu=Kesildi'dir.
            if (asilBelge.FaturalamaDurumu != TicariBelgeFaturalamaDurumu.Kesildi)
            {
                throw new BaseException(
                    $"İade edilen satış faturası 'FaturaKesildi' durumunda olmalıdır. (Mevcut durum: {asilBelge.Durum})",
                    errorCode: 400);
            }

            if (string.IsNullOrWhiteSpace(asilBelge.ResmiFaturaNo))
                throw new BaseException("İade edilen satış faturasında geçerli bir resmî fatura numarası bulunamadı.", errorCode: 400);

            if (!asilBelge.FaturaKesimTarihi.HasValue)
                throw new BaseException("İade edilen satış faturasında fatura kesim tarihi bulunamadı.", errorCode: 400);
        }
        else
        {
            // AlisFaturasi hiçbir zaman FaturaKesAsync ile FaturaKesildi durumuna geçemez (bkz.
            // OtomatikResmiNumaraUretilebilirMi) - bu yüzden "en az MuhasebeOnaylandı" pratikte
            // yalnızca MuhasebeOnaylandi (OTORİTER: MuhasebeDurumu=Onaylandi) durumuyla karşılanabilir.
            if (asilBelge.MuhasebeDurumu != TicariBelgeMuhasebeDurumu.Onaylandi)
            {
                throw new BaseException(
                    $"İade edilen alış faturası en az 'MuhasebeOnaylandı' durumunda olmalıdır. (Mevcut durum: {asilBelge.Durum})",
                    errorCode: 400);
            }

            if (string.IsNullOrWhiteSpace(asilBelge.KarsiTarafFaturaNo))
                throw new BaseException("İade edilen alış faturasında tedarikçi fatura numarası bulunamadı.", errorCode: 400);
        }

        if (!asilBelge.MuhasebeFisId.HasValue)
            throw new BaseException("İade edilen belgeye bağlı muhasebe fişi bulunamadı.", errorCode: 400);

        var asilFis = await _db.MuhasebeFisler
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == asilBelge.MuhasebeFisId.Value, cancellationToken)
            ?? throw new BaseException("İade edilen belgeye bağlı muhasebe fişi bulunamadı.", errorCode: 400);

        ValidateMuhasebeFisDurumu(asilFis);

        return asilBelge;
    }

    /// <summary>
    /// Muhasebe fişi durum matrisini MERKEZİ olarak doğrular: Taslak/Onaylı geçerli; İptal, Ters
    /// Kayıt ve bilinmeyen herhangi bir durum reddedilir. FaturaKesAsync ve
    /// ValidateVeGetIadeEdilenBelgeAsync AYNI bu metodu kullanır.
    /// </summary>
    private static void ValidateMuhasebeFisDurumu(MuhasebeFis fis)
    {
        if (fis.IsDeleted)
            throw new BaseException("Bağlı muhasebe fişi silinmiş.", errorCode: 400);

        // OTORİTER kaynak: IadeEdilenBelgeEligibility.FisDurumuGecerliMi - lookup sorgusuyla
        // (TicariBelgeLookupService) AYNI kriter, iki yerde ayrı ayrı yazılmaz.
        if (IadeEdilenBelgeEligibility.FisDurumuGecerliMi(fis.Durum))
        {
            return;
        }

        switch (fis.Durum)
        {
            case MuhasebeFisDurumlari.Iptal:
                throw new BaseException("Bağlı muhasebe fişi iptal edilmiş.", errorCode: 400);

            case MuhasebeFisDurumlari.TersKayit:
                throw new BaseException("Bağlı muhasebe fişi bir ters kayıt fişidir.", errorCode: 400);

            default:
                throw new BaseException($"Bağlı muhasebe fişi bilinmeyen bir durumda ({fis.Durum}).", errorCode: 400);
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
        var eskiCariKartId = belge.CariKartId;

        if (!string.IsNullOrWhiteSpace(request.BelgeNo))
            belge.BelgeNo = request.BelgeNo;

        if (request.BelgeTipi.HasValue)
            belge.BelgeTipi = request.BelgeTipi.Value;

        if (request.TesisId.HasValue)
            belge.TesisId = request.TesisId;

        // Diğer nullable alanlarla AYNI "yalnızca verilmişse uygula" yarı-kısmi güncelleme
        // semantiği: request.CariKartId null ise mevcut CariKartId KORUNUR. Bu görev kapsamında
        // CariKartId'yi AÇIKÇA temizlemek için ayrı bir semantik (ör. bir "kaldır" bayrağı)
        // EKLENMEZ - CariKartId zorunlu bir ilişkidir ve iş akışlarında temizlenmesi beklenmez.
        if (request.CariKartId.HasValue)
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

        var cariKartIdDegisiyorMu = belge.CariKartId != eskiCariKartId;

        // ── Karşı taraf fatura numarası ──
        if (request.KarsiTarafFaturaNo is not null)
        {
            // Kontrol karakteri kontrolü, "boş mu/temizleme talebi mi" kısa devresinden ÖNCE, HAM
            // değer üzerinde çalışır - string.IsNullOrWhiteSpace/Trim() (parametresiz) KULLANILMAZ,
            // çünkü bazı kontrol karakterleri (ör. U+0085 NEL) .NET'te whitespace sayılır ve
            // yalnızca bu karakterden oluşan bir değer, kontrol karakteri hiç yokmuş gibi sessizce
            // "temizleme talebi" sayılabilirdi. Yalnızca normal ASCII boşluk (' ') temizlenebilir.
            ThrowIfKarsiTarafFaturaNoIcerirKontrolKarakteri(request.KarsiTarafFaturaNo);

            var trimmed = request.KarsiTarafFaturaNo.Trim(' ');
            if (trimmed.Length == 0)
            {
                // Yalnızca normal ASCII boşluklardan oluşuyor = Update sırasında AÇIKÇA temizle.
                belge.KarsiTarafFaturaNo = null;
            }
            else
            {
                var normalized = NormalizeKarsiTarafFaturaNo(belge.BelgeTipi, request.KarsiTarafFaturaNo);
                await ThrowIfKarsiTarafFaturaNoDuplicateAsync(
                    belge.KurumId, belge.CariKartId, normalized, excludeId: belge.Id, cancellationToken);
                belge.KarsiTarafFaturaNo = normalized;
            }
        }
        else if (!string.IsNullOrWhiteSpace(belge.KarsiTarafFaturaNo))
        {
            // KarsiTarafFaturaNo dokunulmadan taşınıyor - BelgeTipi değişmişse yeni tipe uygun
            // değilse SESSİZCE temizlenmez, güncelleme AÇIKÇA reddedilir; CariKartId değişmişse
            // duplicate anahtarı (KurumId+CariKartId+KarsiTarafFaturaNo) NİHAİ cari ile tekrar
            // kontrol edilir - aksi halde yeni cari altında fark edilmeden bir çakışma oluşabilir.
            NormalizeKarsiTarafFaturaNo(belge.BelgeTipi, belge.KarsiTarafFaturaNo);

            if (cariKartIdDegisiyorMu)
            {
                await ThrowIfKarsiTarafFaturaNoDuplicateAsync(
                    belge.KurumId, belge.CariKartId, belge.KarsiTarafFaturaNo, excludeId: belge.Id, cancellationToken);
            }
        }

        // ── İade edilen belge referansı ──
        if (request.IadeEdilenBelgeId.HasValue && request.IadeEdilenBelgeReferansiKaldir)
        {
            throw new BaseException(
                "IadeEdilenBelgeId ve IadeEdilenBelgeReferansiKaldir birlikte gönderilemez.",
                errorCode: 400);
        }

        if (request.IadeEdilenBelgeReferansiKaldir)
        {
            belge.IadeEdilenBelgeId = null;
        }
        else if (request.IadeEdilenBelgeId.HasValue)
        {
            belge.IadeEdilenBelgeId = request.IadeEdilenBelgeId;
        }

        // Belge üzerinde İadeEdilenBelgeId HÂLÂ varsa (yeni verildi, dokunulmadan taşındı, ya da
        // BelgeTipi/KurumId/CariKartId/BelgeTarihi bu istekle değişmiş olabilir) - NİHAİ
        // değerlerle HER ZAMAN yeniden doğrulanır. Yalnızca "IadeEdilenBelgeId yeni verildi" veya
        // "BelgeTipi değişti" koşullarına bağlı kalınmaz - CariKartId/BelgeTarihi değişse bile
        // (IadeEdilenBelgeId ve BelgeTipi dokunulmasa dahi) asıl belgeyle uyumsuzluk yakalanmalıdır.
        if (belge.IadeEdilenBelgeId.HasValue)
        {
            await ValidateVeGetIadeEdilenBelgeAsync(
                belge.BelgeTipi, belge.IadeEdilenBelgeId, belge.KurumId, belge.CariKartId, belge.BelgeTarihi,
                selfId: belge.Id, cancellationToken);
        }
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

        // ValidateIadeSatirlariAsync burada AYRICA çağrılmaz - UpdateAsync, satırlar gönderilsin
        // ya da gönderilmesin HER durumda bu doğrulamayı TEK SEFERDE (bu metottan hemen sonra)
        // çalıştırır (bkz. görev 4b) - dağınık/farklılaşabilecek kopya çağrılar OLUŞTURULMAZ.
    }

    // ──────────────────────────────────────────────
    //  Private — İade Satırı Kaynak Bağlantısı ve Kümülatif Miktar
    // ──────────────────────────────────────────────

    /// <summary>
    /// İade satırlarının (SatisIadeFaturasi/AlisIadeFaturasi) kaynak satır bağlantısını,
    /// finansal alan tutarlılığını ve miktar sınırlarını doğrular. NİHAİ belge ve satır
    /// değerleriyle çalışan MERKEZİ metottur — Create/Update (kilitsiz, yalnızca bu belgenin
    /// kendi satırları için erken kontrol) ve muhasebe onayına gönderme/onaylama (kilitli,
    /// diğer belgelerle birlikte kümülatif nihai kontrol) AYNI bu metodu çağırır; dağınık,
    /// birbirinden farklılaşabilecek kopya kurallar OLUŞTURULMAZ.
    ///
    /// Normal SatisFaturasi/AlisFaturasi belgelerinde (belge.BelgeTipi iade tipi değilse) hiçbir
    /// şey yapmadan döner — bu belgelerde KaynakSatirId hâlâ diğer modüllerin (Kamp, Restoran,
    /// Rezervasyon) kullandığı serbest biçimli harici kaynak kimliği olarak KALIR, davranışı
    /// DEĞİŞTİRİLMEZ.
    ///
    /// belge.IadeEdilenBelgeId henüz seçilmemişse (Taslak aşamasında KarsiTarafFaturaNo/
    /// IadeEdilenBelgeId gibi geriye uyumlu olarak ertelenebilir — bkz. NormalizeKarsiTarafFaturaNoOrNull)
    /// yalnızca KaynakSatirId'nin VAR OLDUĞU ve ayrıştırılabilir olduğu kontrol edilir; asıl
    /// fatura henüz bilinmediğinden sahiplik/mali alan/miktar kontrolleri ERTELENİR — onay
    /// aşamasında IadeEdilenBelgeId zaten zorunlu kılındığından (bkz.
    /// ValidateKarsiTarafVeIadeAlanlariAsync, bu metottan ÖNCE çalışır) o noktada MUTLAKA ve TAM
    /// olarak tekrar çalışır; hiçbir satır bu kontrolden kaçarak onaylanamaz.
    /// </summary>
    private async Task ValidateIadeSatirlariAsync(
        SatisBelgesi belge,
        bool kilitliKumulatifKontrol,
        CancellationToken cancellationToken)
    {
        if (belge.BelgeTipi is not (SatisBelgesiTipi.SatisIadeFaturasi or SatisBelgesiTipi.AlisIadeFaturasi))
            return;

        var aktifSatirlar = belge.Satirlar.Where(s => !s.IsDeleted).ToList();
        if (aktifSatirlar.Count == 0)
            return;

        // 1. KaynakSatirId her iade satırında ZORUNLUDUR ve pozitif bir tam sayı olarak
        // ayrıştırılabilir olmalıdır (NumberStyles.None: işaret/boşluk/binlik ayıracı YOK).
        //
        // Kanonikleştirme: ayrıştırılan değer, satıra HER ZAMAN kaynakSatirId.ToString(InvariantCulture)
        // (baştaki sıfırlar/farklı biçimler olmadan) olarak GERİ YAZILIR - "00123" ve "123" AYNI
        // kaynak satırı gösterse de, aşağıdaki kümülatif sorgu (x.KaynakSatirId == ...) SAF METİN
        // eşitliği kullanır; satır DB'ye kanonik olmayan bir biçimde yazılırsa, o kaynak satıra
        // yapılan BAŞKA bir iadenin metinsel eşleşmesi SESSİZCE KAÇAR ve kümülatif sınır delinebilir.
        // Bu satır, bu metodun HER çağrıldığı yerde (Create, satırlı Update, onay akışları) çalıştığı
        // için önceden (bu düzeltmeden ÖNCE) kanonik olmayan biçimde kaydedilmiş satırlar da, bu
        // metodun bir sonraki çalışmasında (ör. bir sonraki onay adımında) kendiliğinden düzelir;
        // geçmişte zaten var olan kayıtlar için AYRICA bir migration ile geriye dönük düzeltilir
        // (bkz. HardenIadeSatirKaynagiCanonicalFormat migration'ı).
        var parsed = new List<(SatisBelgesiSatiri Satir, int KaynakSatirId)>();
        foreach (var satir in aktifSatirlar)
        {
            if (string.IsNullOrWhiteSpace(satir.KaynakSatirId) ||
                !int.TryParse(satir.KaynakSatirId, NumberStyles.None, CultureInfo.InvariantCulture, out var kaynakSatirId) ||
                kaynakSatirId <= 0)
            {
                throw new BaseException(
                    $"İade satırlarında kaynak satır referansı (KaynakSatirId) zorunludur. (SıraNo: {satir.SiraNo})",
                    errorCode: 400);
            }

            satir.KaynakSatirId = kaynakSatirId.ToString(CultureInfo.InvariantCulture);
            parsed.Add((satir, kaynakSatirId));
        }

        if (!belge.IadeEdilenBelgeId.HasValue)
            return;

        var kaynakSatirIdler = parsed.Select(x => x.KaynakSatirId).Distinct().OrderBy(x => x).ToList();

        if (kilitliKumulatifKontrol)
        {
            // Eşzamanlılık: her kaynak satır, SABİT (artan Id) sırayla WITH (UPDLOCK, ROWLOCK,
            // HOLDLOCK) kilitlenir - çağıran (MuhasebeOnayinaGonderAsync/MuhasebeOnaylaAsync) bu
            // metodu her zaman açık bir transaction içinde çağırır, bu yüzden kilit COMMIT/ROLLBACK
            // olana kadar TUTULUR. Aynı kaynak satıra eşzamanlı onaya gönderilen/onaylanan iki
            // farklı iade belgesi bu satırda birbirini bekler; ikincisi, birincinin transaction'ı
            // sonuçlanınca (commit ile) NİHAİ/güncel toplamı görür - yalnızca "önce oku sonra yaz"
            // (uygulama seviyesi, kilitsiz) kontrolüyle YETİNİLMEZ. Sabit artan sıra, iki
            // transaction'ın farklı sırayla kilit almasından doğabilecek deadlock'ları önler.
            foreach (var kaynakSatirId in kaynakSatirIdler)
            {
                await _db.SatisBelgesiSatirlari
                    .FromSqlInterpolated($@"
SELECT * FROM [muhasebe].[SatisBelgesiSatirlari] WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
WHERE [Id] = {kaynakSatirId} AND [IsDeleted] = 0")
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);
            }
        }

        var kaynakSatirlar = await _db.SatisBelgesiSatirlari
            .AsNoTracking()
            .Where(x => kaynakSatirIdler.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var kaynakSatirId in kaynakSatirIdler)
        {
            if (!kaynakSatirlar.ContainsKey(kaynakSatirId))
            {
                throw new BaseException(
                    $"Kaynak satır bulunamadı veya silinmiş. (KaynakSatirId: {kaynakSatirId})",
                    errorCode: 400);
            }
        }

        foreach (var (satir, kaynakSatirId) in parsed)
        {
            var kaynakSatir = kaynakSatirlar[kaynakSatirId];

            // 2. Sahiplik: kaynak satır, SEÇİLEN asıl faturaya (belge.IadeEdilenBelgeId) ait
            // olmalıdır - başka bir faturaya (hatta başka geçerli bir asıl faturaya) ait bir satır
            // KABUL EDİLMEZ. Kaynak satırın kendisi silinmemiş olduğundan (yukarıdaki sözlükte
            // bulunduğundan) ve global sorgu filtresi zaten IsDeleted=0 uyguladığından, burada
            // ayrıca bir kurum kontrolüne gerek YOKTUR - IadeEdilenBelgeId'nin kendisi zaten
            // ValidateVeGetIadeEdilenBelgeAsync ile aynı kuruma ait olduğu doğrulanmış bir belgedir.
            if (kaynakSatir.SatisBelgesiId != belge.IadeEdilenBelgeId.Value)
            {
                throw new BaseException(
                    $"Kaynak satır, seçilen iade edilen belgeye ait değil. (SıraNo: {satir.SiraNo}, KaynakSatirId: {kaynakSatirId})",
                    errorCode: 400);
            }

            // 3. Finansal alan tutarlılığı: iade satırı kaynak satırın birim fiyatını, indirim
            // oranını, KDV uygulama tipi/oranını ve tevkifat oranını BİREBİR yansıtmalıdır -
            // yalnızca Miktar farklı olabilir. Bu, paralel bir tutar hesaplayıcı OLUŞTURMADAN
            // (SatisBelgesiTutarHesaplayici tek hesaplama noktası olarak KALIR), iade üzerinden
            // asıl faturada bulunmayan veya asıl birim değerini aşan bir mali değer üretilmesini
            // yapısal olarak engeller - toplam mali maruziyet, aşağıdaki miktar sınırıyla birlikte
            // kaynak satırın kendi SatirToplami'nı ASLA aşamaz.
            //
            // ÖTV/ÖİV/konaklama vergisi oranları BİLİNÇLİ OLARAK bu eşleşme kontrolüne DAHİL
            // EDİLMEMİŞTİR (kapsam kararı, bkz. görev sonuç raporu): SatisBelgesiEkVergiEngelIntegrationTests
            // ile kanıtlandığı üzere, bu üç alandan herhangi biri > 0 olan HERHANGİ bir belge
            // (iade dahil), tipi ne olursa olsun, SatisBelgesiMuhasebeFisService.
            // MuhasebeFisiOlusturAsync tarafından ZATEN koşulsuz reddedilir (hesap eşlemesi
            // tanımlanmamıştır) - böyle bir belge hiçbir zaman gerçek bir muhasebe fişine/mali
            // etkiye dönüşemez, dolayısıyla bu alanlardaki bir kaynak/iade uyumsuzluğu gerçek bir
            // mali risk oluşturmaz. Bu üç alanı da zorunlu eşleşmeye dahil etmek, kapsamı
            // gerektiğinden fazla büyütür ve mevcut ek-vergi-engeli testleriyle ÇAKIŞIR.
            if (satir.BirimFiyat != kaynakSatir.BirimFiyat ||
                satir.IndirimOrani != kaynakSatir.IndirimOrani ||
                satir.KdvUygulamaTipi != kaynakSatir.KdvUygulamaTipi ||
                satir.KdvOrani != kaynakSatir.KdvOrani ||
                satir.TevkifatPay != kaynakSatir.TevkifatPay ||
                satir.TevkifatPayda != kaynakSatir.TevkifatPayda)
            {
                throw new BaseException(
                    "İade satırının birim fiyatı, indirim oranı, KDV uygulama tipi/oranı ve tevkifat " +
                    $"oranı kaynak satırla birebir eşleşmelidir. (SıraNo: {satir.SiraNo})",
                    errorCode: 400);
            }
        }

        // 4. Miktar sınırı: bu belgenin KENDİ satırlarının kaynak satır bazında toplamı (aynı
        // kaynak satıra birden fazla satırla iade TEORİK olarak mümkündür) hesaplanır.
        // kilitliKumulatifKontrol=false ise (Create/Update, kilitsiz) yalnızca BU belgenin kendi
        // toplamı kaynak miktarı aşıyor mu kontrol edilir - "tek belgenin kendi başına asıl
        // miktarı aşması" erken reddi budur. kilitliKumulatifKontrol=true ise (onay aşaması,
        // kilitli) DİĞER geçerli (IadeKumulatifSayilanMuhasebeDurumlari) belgelerin toplamı da
        // eklenir - NİHAİ, kümülatif ve eşzamanlılığa güvenli kontrol budur.
        var buBelgeninKendiToplamlari = parsed
            .GroupBy(x => x.KaynakSatirId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Satir.Miktar));

        foreach (var (kaynakSatirId, buBelgeninToplami) in buBelgeninKendiToplamlari)
        {
            var kaynakSatir = kaynakSatirlar[kaynakSatirId];
            var digerBelgelerToplami = 0m;

            if (kilitliKumulatifKontrol)
            {
                // SAF METİN eşitliği (x.KaynakSatirId == kaynakSatirId.ToString(...)) KULLANILMAZ:
                // bu belgenin kendi satırları yukarıda kanonik biçime (baştaki sıfırlar OLMADAN)
                // yazılmış olsa da, DİĞER belgelerin satırları - ör. bu düzeltmeden ÖNCE oluşturulmuş
                // ve migration'ın (HardenIadeSatirKaynagiCanonicalFormat) kapsamadığı, veya doğrudan
                // veri aktarımıyla eklenmiş - kanonik OLMAYAN bir biçimde ("00123" gibi) kalmış
                // olabilir. "123" ve "00123" AYNI kaynak satırı gösterir; TRY_CAST(...) = @kaynakSatirId
                // ile SAYISAL eşitlik kullanılarak bu farklı gösterimler GÜVENİLİR şekilde aynı kaynak
                // satır olarak sayılır - metinsel eşitliğin gözden kaçırabileceği önceki bir iade,
                // kümülatif toplamdan ASLA düşürülmez.
                //
                // Bu sorguya gömülen TÜM değerler (kaynakSatirId, belge.Id, belge.IadeEdilenBelgeId,
                // BelgeTipi, MuhasebeDurumu listesi) sıkı tipli INT/ENUM değerleridir - hiçbiri
                // kullanıcıdan gelen serbest metin DEĞİLDİR; bu yüzden FromSqlRaw ile doğrudan
                // gömülmeleri SQL enjeksiyonuna açık DEĞİLDİR (FromSqlInterpolated'in IN (...)
                // listesi gibi çoklu değerleri TEK bir parametreye bağlayıp bozacağı için burada
                // tercih edilmemiştir).
                //
                // OTORİTER: filtre artık eski Durum yerine MuhasebeDurumu üzerinden yapılır (bkz.
                // C.8) - yalnızca Onayda/Onaylandi dahil edilir; Bekliyor, Reddedildi, IptalEdildi
                // HARİÇ tutulur.
                var durumListesi = string.Join(",", IadeEdilenBelgeEligibility.IadeKumulatifSayilanMuhasebeDurumlari.Select(d => (int)d));
                var sql = $"""
                    SELECT ssb.* FROM [muhasebe].[SatisBelgesiSatirlari] ssb
                    INNER JOIN [muhasebe].[SatisBelgeleri] sb ON sb.[Id] = ssb.[SatisBelgesiId]
                    WHERE ssb.[IsDeleted] = 0
                      AND TRY_CAST(ssb.[KaynakSatirId] AS BIGINT) = {kaynakSatirId}
                      AND sb.[Id] <> {belge.Id}
                      AND sb.[IsDeleted] = 0
                      AND sb.[IadeEdilenBelgeId] = {belge.IadeEdilenBelgeId!.Value}
                      AND sb.[BelgeTipi] = {(int)belge.BelgeTipi}
                      AND sb.[MuhasebeDurumu] IN ({durumListesi})
                    """;

                var digerSatirlar = await _db.SatisBelgesiSatirlari
                    .FromSqlRaw(sql)
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                digerBelgelerToplami = digerSatirlar.Sum(x => x.Miktar);
            }

            var toplamIadeMiktari = digerBelgelerToplami + buBelgeninToplami;

            if (toplamIadeMiktari > kaynakSatir.Miktar)
            {
                throw new BaseException(
                    $"Kaynak satırın toplam iade miktarı ({toplamIadeMiktari}) asıl satır miktarını " +
                    $"({kaynakSatir.Miktar}) aşamaz. (KaynakSatirId: {kaynakSatirId})",
                    errorCode: 400);
            }
        }
    }
}
