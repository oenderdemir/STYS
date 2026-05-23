using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

public class SatisBelgesiService : BaseRdbmsService<SatisBelgesiDto, SatisBelgesi, int>, ISatisBelgesiService
{
    private readonly StysAppDbContext _db;
    private readonly ISatisBelgesiRepository _satisBelgesiRepository;

    /// <summary>Tevkifatlı satış satırları bu fazda desteklenmez.</summary>
    private static readonly HashSet<int> DesteklenenKdvUygulamaTipleri =
    [
        (int)KdvUygulamaTipi.Kdvli,
        (int)KdvUygulamaTipi.TamIstisna,
        (int)KdvUygulamaTipi.KismiIstisna,
        (int)KdvUygulamaTipi.KdvKapsamDisi
    ];

    /// <summary>KDV hesaplaması yapılmayan uygulama tipleri.</summary>
    private static readonly HashSet<int> KdvHesaplanmayanTipler =
    [
        (int)KdvUygulamaTipi.TamIstisna,
        (int)KdvUygulamaTipi.KismiIstisna,
        (int)KdvUygulamaTipi.KdvKapsamDisi
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
        IMapper mapper)
        : base(satisBelgesiRepository, mapper)
    {
        _satisBelgesiRepository = satisBelgesiRepository;
        _db = db;
    }

    // ── Satirları include eden yardımcı ──
    private static Func<IQueryable<SatisBelgesi>, IQueryable<SatisBelgesi>> IncludeSatirlar =>
        q => q.Include(x => x.Satirlar);

    // ──────────────────────────────────────────────
    //  GetByIdAsync (ISatisBelgesiService) — nullable olmayan dönüş
    // ──────────────────────────────────────────────

    public async Task<SatisBelgesiDto> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var entity = await Repository.GetByIdAsync(id, IncludeSatirlar);
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
            ? CombineIncludes(IncludeSatirlar, include)
            : IncludeSatirlar;
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
            .Where(x => !x.IsDeleted);

        if (filter.TesisId.HasValue)
            query = query.Where(x => x.TesisId == filter.TesisId.Value);

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
        // 1. Validasyonlar
        await ValidateCreateRequestAsync(request, cancellationToken);

        // 2. Belge no üret (isteğe bağlı override)
        var belgeNo = request.BelgeNo ?? await GenerateBelgeNoAsync(request.BelgeTarihi, cancellationToken);

        // 3. Ana belge entity'sini oluştur
        var belge = new SatisBelgesi
        {
            BelgeNo = belgeNo,
            BelgeTipi = request.BelgeTipi,
            Durum = SatisBelgesiDurumu.Taslak,
            KaynakModul = request.KaynakModul,
            KaynakTipi = request.KaynakTipi,
            KaynakId = request.KaynakId,
            TesisId = request.TesisId,
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
        foreach (var satirRequest in request.Satirlar)
        {
            await ValidateSatirRequestAsync(satirRequest, belge, cancellationToken);
            var satir = CreateSatirFromRequest(satirRequest);
            belge.Satirlar.Add(satir);
        }

        // 5. Belge toplamlarını hesapla
        HesaplaBelgeToplamlari(belge);

        await Repository.AddAsync(belge);
        await Repository.SaveChangesAsync(cancellationToken);

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

        if (belge.Durum != SatisBelgesiDurumu.Taslak)
        {
            throw new BaseException(
                $"Sadece Taslak durumundaki belgeler muhasebe onayına gönderilebilir. Mevcut durum: {belge.Durum}",
                errorCode: 400);
        }

        if (belge.Satirlar.Count(s => !s.IsDeleted) == 0)
        {
            throw new BaseException("Satır içermeyen belge muhasebe onayına gönderilemez.", errorCode: 400);
        }

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
            x => x.Id == id && !x.IsDeleted)
            ?? throw new BaseException($"Satış belgesi bulunamadı. (Id: {id})", errorCode: 404);

        if (belge.Durum != SatisBelgesiDurumu.MuhasebeOnayinda)
        {
            throw new BaseException(
                $"Sadece Muhasebe Onayında durumundaki belgeler onaylanabilir. Mevcut durum: {belge.Durum}",
                errorCode: 400);
        }

        belge.Durum = SatisBelgesiDurumu.MuhasebeOnaylandi;
        belge.MuhasebeOnayTarihi = DateTime.UtcNow;

        await Repository.SaveChangesAsync(cancellationToken);
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
        var belge = await Repository.FirstOrDefaultAsync(
            x => x.Id == id && !x.IsDeleted)
            ?? throw new BaseException($"Satış belgesi bulunamadı. (Id: {id})", errorCode: 404);

        if (belge.Durum == SatisBelgesiDurumu.IptalEdildi)
        {
            throw new BaseException("Belge zaten iptal edilmiş durumda.", errorCode: 400);
        }

        // İptal edilemez durumlar
        if (belge.Durum == SatisBelgesiDurumu.FaturaKesildi ||
            belge.Durum == SatisBelgesiDurumu.MusteriyeGonderildi)
        {
            throw new BaseException(
                $"'{belge.Durum}' durumundaki bir belge iptal edilemez. " +
                "Fatura kesilmiş veya müşteriye gönderilmiş belgeler iptal edilemez.",
                errorCode: 400);
        }

        belge.Durum = SatisBelgesiDurumu.IptalEdildi;

        await Repository.SaveChangesAsync(cancellationToken);
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

        // Tevkifatlı desteklenmez
        if (request.KdvUygulamaTipi == (int)KdvUygulamaTipi.Tevkifatli)
            throw new BaseException("Tevkifatlı satış satırları bu aşamada desteklenmemektedir.", errorCode: 400);

        // Bilinmeyen KDV uygulama tipi
        if (!DesteklenenKdvUygulamaTipleri.Contains(request.KdvUygulamaTipi))
            throw new BaseException($"Geçersiz KDV uygulama tipi: {request.KdvUygulamaTipi}", errorCode: 400);

        // KDV'li satırda KdvOrani > 0
        if (request.KdvUygulamaTipi == (int)KdvUygulamaTipi.Kdvli && request.KdvOrani <= 0)
            throw new BaseException($"KDV'li satırda KDV oranı sıfırdan büyük olmalıdır. (SıraNo: {request.SiraNo})", errorCode: 400);

        // KDV'li olmayan tüm satırlarda KdvIstisnaTanimId zorunlu (KdvKapsamDisi dahil — bug fix)
        if (request.KdvUygulamaTipi != (int)KdvUygulamaTipi.Kdvli)
        {
            if (!request.KdvIstisnaTanimId.HasValue)
                throw new BaseException(
                    $"KDV'li olmayan satırda KDV istisna tanımı zorunludur. (SıraNo: {request.SiraNo})",
                    errorCode: 400);

            await ValidateKdvIstisnaTanimAsync(
                request.KdvIstisnaTanimId.Value,
                request.KdvUygulamaTipi,
                belge.BelgeTarihi,
                cancellationToken);
        }
    }

    private async Task ValidateKdvIstisnaTanimAsync(
        int kdvIstisnaTanimId,
        int kdvUygulamaTipi,
        DateTime belgeTarihi,
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

        if (!tanim.SatisIslemlerindeKullanilirMi)
            throw new BaseException(
                $"KDV istisna tanımı satış işlemlerinde kullanılamaz: '{tanim.Kod} — {tanim.Ad}'",
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
    //  Private — Satır Oluşturma ve Hesaplama
    // ──────────────────────────────────────────────

    private static SatisBelgesiSatiri CreateSatirFromRequest(CreateSatisBelgesiSatiriRequest request)
    {
        var matrah = request.Miktar * request.BirimFiyat;
        var kdvOrani = request.KdvOrani;

        // İstisna / kapsam dışı → KDV hesaplanmaz
        var kdvTutari = KdvHesaplanmayanTipler.Contains(request.KdvUygulamaTipi)
            ? 0m
            : matrah * kdvOrani / 100m;

        var satirToplami = matrah + kdvTutari;

        return new SatisBelgesiSatiri
        {
            SiraNo = request.SiraNo,
            SatirTipi = request.SatirTipi,
            Aciklama = request.Aciklama,
            Miktar = request.Miktar,
            BirimFiyat = request.BirimFiyat,
            Matrah = matrah,
            KdvUygulamaTipi = (KdvUygulamaTipi)request.KdvUygulamaTipi,
            KdvIstisnaTanimId = request.KdvIstisnaTanimId,
            KdvOrani = kdvOrani,
            KdvTutari = kdvTutari,
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
