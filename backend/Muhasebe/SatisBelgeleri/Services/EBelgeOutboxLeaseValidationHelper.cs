using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

internal static class EBelgeOutboxLeaseValidationHelper
{
    private const int MinLeaseSeconds = 1;
    private const int MaxClaimLeaseSeconds = 24 * 60 * 60;
    private const int MaxRetryDelaySeconds = 30 * 24 * 60 * 60;

    internal static int NormalizeAndValidateLeaseSeconds(TimeSpan leaseDuration)
    {
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new BaseException("Lease süresi pozitif olmalıdır.", 400);
        }

        if (leaseDuration.Ticks % TimeSpan.TicksPerSecond != 0)
        {
            throw new BaseException("Lease süresi tam saniye cinsinden olmalıdır.", 400);
        }

        var leaseSeconds = leaseDuration.Ticks / TimeSpan.TicksPerSecond;
        if (leaseSeconds < MinLeaseSeconds || leaseSeconds > MaxClaimLeaseSeconds)
        {
            throw new BaseException(
                $"Lease süresi {MinLeaseSeconds} ile {MaxClaimLeaseSeconds} saniye arasında olmalıdır.",
                400);
        }

        return checked((int)leaseSeconds);
    }

    internal static int? NormalizeAndValidateRetryDelaySeconds(TimeSpan? retryDelay)
    {
        if (retryDelay is null)
        {
            return null;
        }

        if (retryDelay.Value <= TimeSpan.Zero)
        {
            throw new BaseException("Retry gecikmesi pozitif olmalıdır.", 400);
        }

        if (retryDelay.Value.Ticks % TimeSpan.TicksPerSecond != 0)
        {
            throw new BaseException("Retry gecikmesi tam saniye cinsinden olmalıdır.", 400);
        }

        var retryDelaySeconds = retryDelay.Value.Ticks / TimeSpan.TicksPerSecond;
        if (retryDelaySeconds < MinLeaseSeconds || retryDelaySeconds > MaxRetryDelaySeconds)
        {
            throw new BaseException(
                $"Retry gecikmesi {MinLeaseSeconds} ile {MaxRetryDelaySeconds} saniye arasında olmalıdır.",
                400);
        }

        return checked((int)retryDelaySeconds);
    }

    internal static string NormalizeAndValidateKilitToken(string kilitToken)
    {
        if (string.IsNullOrWhiteSpace(kilitToken))
        {
            throw new BaseException("KilitToken boş olamaz.", 400);
        }

        if (!Guid.TryParseExact(kilitToken, "D", out var parsed))
        {
            throw new BaseException("KilitToken geçerli bir GUID (D formatı) olmalıdır.", 400);
        }

        return parsed.ToString("D");
    }

    internal static void ValidateHataAlanlari(string sonHataKodu, string sonHataMesaji)
    {
        if (string.IsNullOrWhiteSpace(sonHataKodu))
        {
            throw new BaseException("SonHataKodu boş olamaz.", 400);
        }

        if (sonHataKodu.Length > 100)
        {
            throw new BaseException("SonHataKodu en fazla 100 karakter olabilir.", 400);
        }

        if (string.IsNullOrWhiteSpace(sonHataMesaji))
        {
            throw new BaseException("SonHataMesaji boş olamaz.", 400);
        }

        if (sonHataMesaji.Length > 2000)
        {
            throw new BaseException("SonHataMesaji en fazla 2000 karakter olabilir.", 400);
        }
    }
}
