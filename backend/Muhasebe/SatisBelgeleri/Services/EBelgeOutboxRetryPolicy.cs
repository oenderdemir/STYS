using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

public enum EBelgeOutboxHataSinifi
{
    Gecici = 1,
    Kalici = 2
}

public interface IEBelgeOutboxRetryPolicy
{
    EBelgeOutboxRetryKarari Hesapla(int denemeSayisi, EBelgeOutboxHataSinifi hataSinifi);
}

public sealed record EBelgeOutboxRetryKarari
{
    private const int MaxRetryDelaySeconds = 30 * 24 * 60 * 60;

    public bool YenidenDenenecekMi { get; }

    public TimeSpan? RetryGecikmesi { get; }

    private EBelgeOutboxRetryKarari(bool yenidenDenenecekMi, TimeSpan? retryGecikmesi)
    {
        YenidenDenenecekMi = yenidenDenenecekMi;
        RetryGecikmesi = retryGecikmesi;
    }

    public static EBelgeOutboxRetryKarari Retry(TimeSpan retryGecikmesi)
    {
        ValidateRetryDelay(retryGecikmesi);
        return new EBelgeOutboxRetryKarari(true, retryGecikmesi);
    }

    public static EBelgeOutboxRetryKarari Terminal()
        => new(false, null);

    private static void ValidateRetryDelay(TimeSpan retryGecikmesi)
    {
        if (retryGecikmesi <= TimeSpan.Zero)
        {
            throw new BaseException("Retry gecikmesi pozitif olmalıdır.", 400);
        }

        if (retryGecikmesi.Ticks % TimeSpan.TicksPerSecond != 0)
        {
            throw new BaseException("Retry gecikmesi tam saniye cinsinden olmalıdır.", 400);
        }

        var retryDelaySeconds = retryGecikmesi.Ticks / TimeSpan.TicksPerSecond;
        if (retryDelaySeconds > MaxRetryDelaySeconds)
        {
            throw new BaseException("Retry gecikmesi en fazla 30 gün olabilir.", 400);
        }
    }
}

public sealed class EBelgeOutboxRetryPolicy : IEBelgeOutboxRetryPolicy
{
    private static readonly TimeSpan[] GeciciHataRetryCizelgesi =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6)
    ];

    public EBelgeOutboxRetryKarari Hesapla(int denemeSayisi, EBelgeOutboxHataSinifi hataSinifi)
    {
        if (denemeSayisi <= 0)
        {
            throw new BaseException("DenemeSayisi pozitif olmalıdır.", 400);
        }

        return hataSinifi switch
        {
            EBelgeOutboxHataSinifi.Gecici => HesaplaGecici(denemeSayisi),
            EBelgeOutboxHataSinifi.Kalici => EBelgeOutboxRetryKarari.Terminal(),
            _ => throw new BaseException("Bilinmeyen hata sınıfı.", 400)
        };
    }

    private static EBelgeOutboxRetryKarari HesaplaGecici(int denemeSayisi)
        => denemeSayisi switch
        {
            1 => EBelgeOutboxRetryKarari.Retry(GeciciHataRetryCizelgesi[0]),
            2 => EBelgeOutboxRetryKarari.Retry(GeciciHataRetryCizelgesi[1]),
            3 => EBelgeOutboxRetryKarari.Retry(GeciciHataRetryCizelgesi[2]),
            4 => EBelgeOutboxRetryKarari.Retry(GeciciHataRetryCizelgesi[3]),
            5 => EBelgeOutboxRetryKarari.Retry(GeciciHataRetryCizelgesi[4]),
            _ => EBelgeOutboxRetryKarari.Terminal()
        };
}
