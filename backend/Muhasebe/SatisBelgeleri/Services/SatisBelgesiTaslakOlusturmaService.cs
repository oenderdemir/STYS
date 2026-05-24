using Microsoft.Extensions.Logging;
using STYS.AccessScope;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Repositories;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// Operasyon modülleri (otel, restoran, kamp vb.) için ortak satış belgesi taslağı oluşturma servisi.
/// </summary>
public class SatisBelgesiTaslakOlusturmaService : ISatisBelgesiTaslakOlusturmaService
{
    private readonly ISatisBelgesiService _satisBelgesiService;
    private readonly ISatisBelgesiRepository _satisBelgesiRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly ILogger<SatisBelgesiTaslakOlusturmaService> _logger;

    public SatisBelgesiTaslakOlusturmaService(
        ISatisBelgesiService satisBelgesiService,
        ISatisBelgesiRepository satisBelgesiRepository,
        IUserAccessScopeService userAccessScopeService,
        ILogger<SatisBelgesiTaslakOlusturmaService> logger)
    {
        _satisBelgesiService = satisBelgesiService;
        _satisBelgesiRepository = satisBelgesiRepository;
        _userAccessScopeService = userAccessScopeService;
        _logger = logger;
    }

    // ──────────────────────────────────────────────
    //  KaynaktanTaslakOlusturAsync
    // ──────────────────────────────────────────────

    public async Task<SatisBelgesiDto> KaynaktanTaslakOlusturAsync(
        SatisBelgesiTaslakOlusturRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Erken validasyon (operasyon modülüne anlaşılır hata)
        ValidateRequest(request);

        // 2. Tesis access scope çözümleme ve kontrol
        var resolvedTesisId = await ResolveTesisIdAsync(request.TesisId, cancellationToken);

        // 3. Duplicate kaynak kontrolü
        await ThrowIfKaynakDuplicateAsync(
            request.KaynakModul, request.KaynakTipi, request.KaynakId, cancellationToken);

        // 4. CreateSatisBelgesiRequest oluştur
        var createRequest = MapToCreateRequest(request, resolvedTesisId);

        // 5. ISatisBelgesiService.CreateAsync çağır (nihai validasyon orada)
        _logger.LogInformation(
            "Kaynaktan taslak oluşturuluyor: Modül={KaynakModul}, Tip={KaynakTipi}, KaynakId={KaynakId}, TesisId={TesisId}",
            request.KaynakModul, request.KaynakTipi, request.KaynakId, resolvedTesisId);

        var result = await _satisBelgesiService.CreateAsync(createRequest, cancellationToken);

        _logger.LogInformation(
            "Kaynaktan taslak oluşturuldu: BelgeId={BelgeId}, BelgeNo={BelgeNo}",
            result.Id, result.BelgeNo);

        return result;
    }

    // ──────────────────────────────────────────────
    //  Private — Validasyon
    // ──────────────────────────────────────────────

    /// <summary>
    /// Operasyon modülüne erken ve anlaşılır hata üretir.
    /// Nihai validasyon ISatisBelgesiService.CreateAsync içinde tekrarlanır.
    /// </summary>
    private static void ValidateRequest(SatisBelgesiTaslakOlusturRequest request)
    {
        // KaynakModul geçerli olmalı
        if (!Enum.IsDefined(request.KaynakModul))
            throw new BaseException("Kaynak modül geçerli değil.", errorCode: 400);

        // KaynakTipi boş olamaz
        if (string.IsNullOrWhiteSpace(request.KaynakTipi))
            throw new BaseException("Kaynak tipi zorunludur.", errorCode: 400);

        // KaynakId boş olamaz
        if (string.IsNullOrWhiteSpace(request.KaynakId))
            throw new BaseException("Kaynak kimliği zorunludur.", errorCode: 400);

        // BelgeTarihi default olamaz
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
            if (string.IsNullOrWhiteSpace(request.MusteriAdSoyad))
                throw new BaseException("Bireysel müşteri için ad soyad zorunludur.", errorCode: 400);
        }

        // Satır validasyonları
        foreach (var satir in request.Satirlar)
        {
            if (string.IsNullOrWhiteSpace(satir.Aciklama))
                throw new BaseException($"Satır açıklaması zorunludur. (Satır indeks: {request.Satirlar.IndexOf(satir) + 1})", errorCode: 400);

            if (satir.Miktar <= 0)
                throw new BaseException($"Satır miktarı sıfırdan büyük olmalıdır. (Satır indeks: {request.Satirlar.IndexOf(satir) + 1})", errorCode: 400);

            if (satir.BirimFiyat < 0)
                throw new BaseException($"Birim fiyat negatif olamaz. (Satır indeks: {request.Satirlar.IndexOf(satir) + 1})", errorCode: 400);

            // Tevkifatlı desteklenmez
            if (satir.KdvUygulamaTipi == KdvUygulamaTipi.Tevkifatli)
                throw new BaseException("Tevkifatlı satış satırları bu aşamada desteklenmemektedir.", errorCode: 400);

            // KDV'li → KdvOrani > 0
            if (satir.KdvUygulamaTipi == KdvUygulamaTipi.Kdvli && satir.KdvOrani <= 0)
                throw new BaseException($"KDV'li satırda KDV oranı sıfırdan büyük olmalıdır. (Satır indeks: {request.Satirlar.IndexOf(satir) + 1})", errorCode: 400);

            // KDV'li değilse KdvIstisnaTanimId zorunlu
            if (satir.KdvUygulamaTipi != KdvUygulamaTipi.Kdvli && !satir.KdvIstisnaTanimId.HasValue)
                throw new BaseException(
                    $"KDV'li olmayan satırda KDV istisna tanımı zorunludur. (Satır indeks: {request.Satirlar.IndexOf(satir) + 1})",
                    errorCode: 400);
        }
    }

    // ──────────────────────────────────────────────
    //  Private — Tesis Access Scope
    // ──────────────────────────────────────────────

    private async Task<int?> ResolveTesisIdAsync(int? tesisId, CancellationToken cancellationToken)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var resolved = tesisId;

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

    // ──────────────────────────────────────────────
    //  Private — Duplicate Kaynak Kontrolü
    // ──────────────────────────────────────────────

    private async Task ThrowIfKaynakDuplicateAsync(
        SatisKaynakModulu kaynakModul,
        string kaynakTipi,
        string kaynakId,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var exists = await _satisBelgesiRepository.AnyAsync(
            x => !x.IsDeleted
                 && x.KaynakModul == kaynakModul
                 && x.KaynakTipi == kaynakTipi
                 && x.KaynakId == kaynakId);

        if (exists)
        {
            throw new BaseException(
                "Bu kaynak için daha önce satış belgesi taslağı oluşturulmuş.",
                errorCode: 400);
        }
    }

    // ──────────────────────────────────────────────
    //  Private — Request Dönüşümü
    // ──────────────────────────────────────────────

    private static CreateSatisBelgesiRequest MapToCreateRequest(
        SatisBelgesiTaslakOlusturRequest request,
        int? resolvedTesisId)
    {
        var satirlar = new List<CreateSatisBelgesiSatiriRequest>(request.Satirlar.Count);
        for (var i = 0; i < request.Satirlar.Count; i++)
        {
            var src = request.Satirlar[i];
            satirlar.Add(new CreateSatisBelgesiSatiriRequest
            {
                SiraNo = i + 1,
                SatirTipi = src.SatirTipi,
                Aciklama = src.Aciklama,
                Miktar = src.Miktar,
                BirimFiyat = src.BirimFiyat,
                KdvUygulamaTipi = (int)src.KdvUygulamaTipi,
                KdvOrani = src.KdvOrani,
                KdvIstisnaTanimId = src.KdvIstisnaTanimId,
                KaynakSatirId = src.KaynakSatirId
            });
        }

        return new CreateSatisBelgesiRequest
        {
            BelgeTipi = SatisBelgesiTipi.FaturaTaslagi,
            KaynakModul = request.KaynakModul,
            KaynakTipi = request.KaynakTipi,
            KaynakId = request.KaynakId,
            TesisId = resolvedTesisId,
            BelgeTarihi = request.BelgeTarihi,
            VadeTarihi = request.VadeTarihi,
            KurumsalMi = request.KurumsalMi,
            MusteriUnvan = request.MusteriUnvan,
            MusteriAdSoyad = request.MusteriAdSoyad,
            MusteriVergiNo = request.MusteriVergiNo,
            MusteriTcKimlikNo = request.MusteriTcKimlikNo,
            MusteriVergiDairesi = request.MusteriVergiDairesi,
            MusteriAdres = request.MusteriAdres,
            MusteriEposta = request.MusteriEposta,
            MusteriTelefon = request.MusteriTelefon,
            Aciklama = request.Aciklama,
            Satirlar = satirlar
        };
    }
}
