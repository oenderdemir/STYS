using Microsoft.Extensions.Logging;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

public interface IEBelgeOutboxMesajIslemeService
{
    Task<EBelgeOutboxIslemeSonucu> IsleAsync(
        EBelgeOutboxClaimLeaseResultDto claim,
        CancellationToken cancellationToken = default);
}

public interface IEBelgeOutboxIsTuruHandler
{
    EBelgeOutboxIsTuru IsTuru { get; }

    Task<EBelgeOutboxHandlerSonucu> HandleAsync(EBelgeOutboxIslemBaglami baglam, CancellationToken cancellationToken = default);
}

public sealed record EBelgeOutboxIslemBaglami(
    int OutboxMesajiId,
    int KurumId,
    int EBelgeKaydiId,
    EBelgeOutboxIsTuru IsTuru,
    int DenemeSayisi);

public sealed record EBelgeOutboxHandlerSonucu
{
    public bool BasariliMi { get; }

    public EBelgeOutboxHataSinifi? HataSinifi { get; }

    public string? HataKodu { get; }

    public string? HataMesaji { get; }

    private EBelgeOutboxHandlerSonucu(bool basariliMi, EBelgeOutboxHataSinifi? hataSinifi, string? hataKodu, string? hataMesaji)
    {
        BasariliMi = basariliMi;
        HataSinifi = hataSinifi;
        HataKodu = hataKodu;
        HataMesaji = hataMesaji;
    }

    public static EBelgeOutboxHandlerSonucu Basarili()
        => new(true, null, null, null);

    public static EBelgeOutboxHandlerSonucu Basarisiz(EBelgeOutboxHataSinifi hataSinifi, string hataKodu, string hataMesaji)
    {
        if (!Enum.IsDefined(typeof(EBelgeOutboxHataSinifi), hataSinifi))
        {
            throw new BaseException("Bilinmeyen hata sınıfı.", 400);
        }

        EBelgeOutboxLeaseValidationHelper.ValidateHataAlanlari(hataKodu, hataMesaji);
        return new(false, hataSinifi, hataKodu, hataMesaji);
    }
}

public enum EBelgeOutboxIslemeSonucuTuru
{
    Tamamlandi = 1,
    RetryPlanlandi = 2,
    TerminalHata = 3,
    SahiplikKaybedildi = 4
}

public sealed record EBelgeOutboxIslemeSonucu
{
    public EBelgeOutboxIslemeSonucuTuru SonucTuru { get; }

    public TimeSpan? RetryGecikmesi { get; }

    private EBelgeOutboxIslemeSonucu(EBelgeOutboxIslemeSonucuTuru sonucTuru, TimeSpan? retryGecikmesi)
    {
        SonucTuru = sonucTuru;
        RetryGecikmesi = retryGecikmesi;
    }

    public static EBelgeOutboxIslemeSonucu Tamamlandi()
        => new(EBelgeOutboxIslemeSonucuTuru.Tamamlandi, null);

    public static EBelgeOutboxIslemeSonucu RetryPlanlandi(TimeSpan retryGecikmesi)
    {
        EBelgeOutboxLeaseValidationHelper.NormalizeAndValidateRetryDelaySeconds(retryGecikmesi);
        return new(EBelgeOutboxIslemeSonucuTuru.RetryPlanlandi, retryGecikmesi);
    }

    public static EBelgeOutboxIslemeSonucu TerminalHata()
        => new(EBelgeOutboxIslemeSonucuTuru.TerminalHata, null);

    public static EBelgeOutboxIslemeSonucu SahiplikKaybedildi()
        => new(EBelgeOutboxIslemeSonucuTuru.SahiplikKaybedildi, null);
}

public sealed class EBelgeOutboxMesajIslemeService : IEBelgeOutboxMesajIslemeService
{
    private const string DesteklenmeyenIsTuruHataKodu = "EBELGE_DESTEKLENMEYEN_IS_TURU";
    private const string DesteklenmeyenIsTuruHataMesaji = "E-belge outbox işi desteklenmeyen bir iş türü nedeniyle tamamlanamadı.";
    private const string BeklenmeyenHataKodu = "EBEKLENMEYENISLEMHATASI";
    private const string BeklenmeyenHataMesaji = "E-belge outbox işi işlenirken beklenmeyen bir hata oluştu.";

    private readonly IReadOnlyDictionary<EBelgeOutboxIsTuru, IEBelgeOutboxIsTuruHandler> _handlers;
    private readonly IEBelgeOutboxRetryPolicy _retryPolicy;
    private readonly IEBelgeOutboxLeaseTransitionService _transitionService;
    private readonly ILogger<EBelgeOutboxMesajIslemeService> _logger;

    public EBelgeOutboxMesajIslemeService(
        IEnumerable<IEBelgeOutboxIsTuruHandler> handlers,
        IEBelgeOutboxRetryPolicy retryPolicy,
        IEBelgeOutboxLeaseTransitionService transitionService,
        ILogger<EBelgeOutboxMesajIslemeService> logger)
    {
        var handlerList = handlers.ToList();
        var tanimsizHandler = handlerList.FirstOrDefault(x => !Enum.IsDefined(typeof(EBelgeOutboxIsTuru), x.IsTuru));
        if (tanimsizHandler is not null)
        {
            throw new InvalidOperationException($"Tanımsız iş türünü desteklediğini bildiren handler reddedildi: {tanimsizHandler.IsTuru}.");
        }

        _handlers = handlerList
            .GroupBy(x => x.IsTuru)
            .ToDictionary(x => x.Key, x => SelectUniqueHandler(x.Key, x.ToList()));
        _retryPolicy = retryPolicy;
        _transitionService = transitionService;
        _logger = logger;
    }

    public async Task<EBelgeOutboxIslemeSonucu> IsleAsync(
        EBelgeOutboxClaimLeaseResultDto claim,
        CancellationToken cancellationToken = default)
    {
        var normalizedToken = ValidateClaimAndGetNormalizedToken(claim);

        if (!_handlers.TryGetValue(claim.IsTuru, out var handler))
        {
            return await HandleDesteklenmeyenIsTuruAsync(claim, normalizedToken, cancellationToken);
        }

        var baglam = new EBelgeOutboxIslemBaglami(
            claim.OutboxMesajiId,
            claim.KurumId,
            claim.EBelgeKaydiId,
            claim.IsTuru,
            claim.DenemeSayisi);

        EBelgeOutboxHandlerSonucu handlerSonucu;
        try
        {
            handlerSonucu = await handler.HandleAsync(baglam, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return await HandleBeklenmeyenHandlerExceptionAsync(claim, normalizedToken, cancellationToken, ex);
        }

        if (handlerSonucu.BasariliMi)
        {
            var completeEdildi = await _transitionService.TryCompleteAsync(
                claim.OutboxMesajiId,
                claim.KurumId,
                normalizedToken,
                cancellationToken);

            return completeEdildi
                ? EBelgeOutboxIslemeSonucu.Tamamlandi()
                : EBelgeOutboxIslemeSonucu.SahiplikKaybedildi();
        }

        return await HandleBasarisizHandlerSonucuAsync(claim, normalizedToken, handlerSonucu, cancellationToken);
    }

    private async Task<EBelgeOutboxIslemeSonucu> HandleDesteklenmeyenIsTuruAsync(
        EBelgeOutboxClaimLeaseResultDto claim,
        string normalizedToken,
        CancellationToken cancellationToken)
    {
        var failEdildi = await _transitionService.TryFailAsync(
            claim.OutboxMesajiId,
            claim.KurumId,
            normalizedToken,
            DesteklenmeyenIsTuruHataKodu,
            DesteklenmeyenIsTuruHataMesaji,
            null,
            cancellationToken);

        return failEdildi
            ? EBelgeOutboxIslemeSonucu.TerminalHata()
            : EBelgeOutboxIslemeSonucu.SahiplikKaybedildi();
    }

    private async Task<EBelgeOutboxIslemeSonucu> HandleBasarisizHandlerSonucuAsync(
        EBelgeOutboxClaimLeaseResultDto claim,
        string normalizedToken,
        EBelgeOutboxHandlerSonucu handlerSonucu,
        CancellationToken cancellationToken)
    {
        var retryKarari = _retryPolicy.Hesapla(claim.DenemeSayisi, handlerSonucu.HataSinifi!.Value);
        var failEdildi = await _transitionService.TryFailAsync(
            claim.OutboxMesajiId,
            claim.KurumId,
            normalizedToken,
            handlerSonucu.HataKodu!,
            handlerSonucu.HataMesaji!,
            retryKarari.YenidenDenenecekMi ? retryKarari.RetryGecikmesi : null,
            cancellationToken);

        if (!failEdildi)
        {
            return EBelgeOutboxIslemeSonucu.SahiplikKaybedildi();
        }

        return retryKarari.YenidenDenenecekMi
            ? EBelgeOutboxIslemeSonucu.RetryPlanlandi(retryKarari.RetryGecikmesi!.Value)
            : EBelgeOutboxIslemeSonucu.TerminalHata();
    }

    private async Task<EBelgeOutboxIslemeSonucu> HandleBeklenmeyenHandlerExceptionAsync(
        EBelgeOutboxClaimLeaseResultDto claim,
        string normalizedToken,
        CancellationToken cancellationToken,
        Exception ex)
    {
        _logger.LogWarning(ex, "Beklenmeyen e-belge outbox hatası. Güvenli geçici sınıflandırma uygulanacak.");
        var retryKarari = _retryPolicy.Hesapla(claim.DenemeSayisi, EBelgeOutboxHataSinifi.Gecici);
        var failEdildi = await _transitionService.TryFailAsync(
            claim.OutboxMesajiId,
            claim.KurumId,
            normalizedToken,
            BeklenmeyenHataKodu,
            BeklenmeyenHataMesaji,
            retryKarari.YenidenDenenecekMi ? retryKarari.RetryGecikmesi : null,
            cancellationToken);

        if (!failEdildi)
        {
            return EBelgeOutboxIslemeSonucu.SahiplikKaybedildi();
        }

        return retryKarari.YenidenDenenecekMi
            ? EBelgeOutboxIslemeSonucu.RetryPlanlandi(retryKarari.RetryGecikmesi!.Value)
            : EBelgeOutboxIslemeSonucu.TerminalHata();
    }

    private static IEBelgeOutboxIsTuruHandler SelectUniqueHandler(EBelgeOutboxIsTuru isTuru, IReadOnlyList<IEBelgeOutboxIsTuruHandler> handlers)
    {
        if (handlers.Count == 1)
        {
            return handlers[0];
        }

        if (handlers.Count == 0)
        {
            throw new InvalidOperationException($"{isTuru} için handler tanımlı değil.");
        }

        throw new InvalidOperationException($"{isTuru} için birden fazla handler tanımlı.");
    }

    private static string ValidateClaimAndGetNormalizedToken(EBelgeOutboxClaimLeaseResultDto claim)
    {
        if (claim is null)
        {
            throw new BaseException("Claim boş olamaz.", 400);
        }

        if (claim.OutboxMesajiId <= 0)
        {
            throw new BaseException("OutboxMesajiId pozitif olmalıdır.", 400);
        }

        if (claim.KurumId <= 0)
        {
            throw new BaseException("KurumId pozitif olmalıdır.", 400);
        }

        if (claim.EBelgeKaydiId <= 0)
        {
            throw new BaseException("EBelgeKaydiId pozitif olmalıdır.", 400);
        }

        if (claim.DenemeSayisi <= 0)
        {
            throw new BaseException("DenemeSayisi pozitif olmalıdır.", 400);
        }

        if (claim.Durum != EBelgeOutboxDurumu.Isleniyor)
        {
            throw new BaseException("Claim durumu Isleniyor olmalıdır.", 400);
        }

        return EBelgeOutboxLeaseValidationHelper.NormalizeAndValidateKilitToken(claim.KilitToken);
    }
}
