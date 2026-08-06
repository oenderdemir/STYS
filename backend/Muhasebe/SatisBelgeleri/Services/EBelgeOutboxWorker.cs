using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using STYS.Muhasebe.SatisBelgeleri.Dtos;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// Faz 2B.8 - üretim-güvenli, çoklu instance destekli, feature flag ile kapatılabilir e-belge
/// outbox worker'ı. Bu sınıf MEVCUT `IEBelgeOutboxClaimLeaseService`/`IEBelgeOutboxMesajIslemeService`
/// altyapısını ORKESTRE EDER - kendi claim/lease/retry mantığını YAZMAZ, UBL XML üretmez/imzalamaz/
/// artifact yazmaz/outbox durumunu DOĞRUDAN değiştirmez/handler seçmez/retry süresi hesaplamaz
/// (bkz. görev md.2). Tüm bu sorumluluklar MEVCUT servislerde KALIR:
///
/// - Claim: `IEBelgeOutboxClaimLeaseService.TryClaimNextAsync` (gerçek lease token'ı, mevcut lease
///   süresi/retry/terminal koşulları - bkz. görev md.4).
/// - İşleme: `IEBelgeOutboxMesajIslemeService.IsleAsync` - handler seçimi, atomik complete/fail,
///   retry policy uygulaması TAMAMEN bu servisin İÇİNDEDİR; worker sonucu YALNIZ GÖZLEMLER, İKİNCİ
///   bir complete/fail/retry/lease-release çağrısı YAPMAZ (bkz. görev md.12).
///
/// Çoklu instance güvenliği, process-içi kilit/distributed lock EKLEMEDEN, TAMAMEN mevcut SQL
/// `UPDLOCK/READPAST` claim mekanizmasından GELİR (bkz. görev md.5) - bu worker, "tek pod
/// çalışacak" varsayımı YAPMAZ; N tane instance AYNI ANDA çalışabilir.
/// </summary>
public sealed class EBelgeOutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEBelgeProcessingActivationGate _activationGate;
    private readonly IEBelgeOutboxWorkerMetrics _metrics;
    private readonly IEBelgeOutboxWorkerHealthState _healthState;
    private readonly IEBelgeOutboxWorkerDelay _delay;
    private readonly EBelgeProcessingOptions _options;
    private readonly ILogger<EBelgeOutboxWorker> _logger;

    public EBelgeOutboxWorker(
        IServiceScopeFactory scopeFactory,
        IEBelgeProcessingActivationGate activationGate,
        IEBelgeOutboxWorkerMetrics metrics,
        IEBelgeOutboxWorkerHealthState healthState,
        IEBelgeOutboxWorkerDelay delay,
        IOptions<EBelgeProcessingOptions> options,
        ILogger<EBelgeOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _activationGate = activationGate;
        _metrics = metrics;
        _healthState = healthState;
        _delay = delay;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Faz 2B.8 görev md.8 - `ShutdownGracePeriodSeconds`i, host'un KENDİ (genel, `HostOptions.
    /// ShutdownTimeout`) kapanma süresinden BAĞIMSIZ olarak uygular. Süre AŞILIRSA, çalışan
    /// mesaj(lar) ZORLA iptal EDİLMEZ - yalnız `StopAsync` beklemeyi BIRAKIR; lease'in daha SONRA
    /// süresi dolup BAŞKA bir worker tarafından yeniden claim edilmesine GÜVENİLİR (bkz. görev
    /// md.8 madde 4).
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        using var graceCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(0, _options.ShutdownGracePeriodSeconds)));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, graceCts.Token);
        try
        {
            await base.StopAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (graceCts.IsCancellationRequested)
        {
            _logger.LogWarning("E-belge outbox worker, ShutdownGracePeriodSeconds ({GracePeriod}s) içinde tamamen durmadı - lease'lerin doğal olarak dolmasına güveniliyor.", _options.ShutdownGracePeriodSeconds);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _healthState.RecordLoopStarted();
        _logger.LogInformation("E-belge outbox worker döngüsü başladı.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var herhangiBirMesajIslendi = false;
            try
            {
                herhangiBirMesajIslendi = await BirTurCalistirAsync(stoppingToken);
                _healthState.RecordSuccessfulPoll();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Faz 2B.8 görev md.9: bir polling turu exception ÜRETTİĞİNDE worker TAMAMEN
                // ÖLMEZ - güvenli/PII içermeyen loglama + kontrollü backoff (idle interval) + bir
                // sonraki turda DEVAM. Fatal hatalar (OutOfMemoryException, StackOverflowException
                // - İKİNCİSİ .NET'te ZATEN YAKALANAMAZ) genel bir catch İLE GİZLENMEZ.
                _metrics.RecordPollError();
                _healthState.RecordWorkerError(WorkerLevelSafeErrorCode(ex));
                _logger.LogError(ex, "E-belge outbox worker turu sırasında beklenmeyen hata - kontrollü backoff sonrası devam edilecek.");
            }

            var bekleme = herhangiBirMesajIslendi
                ? TimeSpan.FromSeconds(_options.PollIntervalSeconds)
                : TimeSpan.FromSeconds(_options.IdlePollIntervalSeconds);

            try
            {
                await _delay.DelayAsync(bekleme, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("E-belge outbox worker döngüsü durdu (graceful shutdown).");
    }

    /// <summary>
    /// Tek bir polling turu: aktivasyon kapısı kontrolü (görev md.17) → `BatchSize` kadar bounded
    /// claim (görev md.4) → her claim, bir `MaxParallelism` semaforu İLE sınırlı AYRI bir DI scope
    /// İÇİNDE işlenir (görev md.6-7). `true` döner YALNIZ en az bir mesaj GERÇEKTEN claim
    /// edildiyse - bu, dış döngünün normal `PollIntervalSeconds` mi (muhtemelen daha fazla iş VAR)
    /// yoksa `IdlePollIntervalSeconds` mi (kuyruk boş VEYA gate kapalı) kullanacağını belirler.
    /// </summary>
    private async Task<bool> BirTurCalistirAsync(CancellationToken stoppingToken)
    {
        // Faz 2B.8 görev md.17: gate kapalıyken mesajlar terminal hataya geçirilmez, deneme
        // sayısı artırılmaz, lease alınmaz - claim'e HİÇ GİDİLMEDEN olduğu yerde beklenir.
        if (!_activationGate.ShouldProcess())
        {
            return false;
        }

        // Faz 2B.8 görev md.6: paralellik BOUNDED bir SemaphoreSlim İLE kontrol edilir - semafor,
        // CLAIM denemesinden ÖNCE alınır, böylece MaxParallelism=1 iken bir sonraki claim, ÖNCEKİ
        // mesajın işlenmesi TAMAMLANMADAN denenmez (lease'in, işlenmeyi BEKLERKEN boşa akmasını
        // ÖNLER).
        var maxParalellik = Math.Clamp(_options.MaxParallelism, 1, EBelgeProcessingOptions.MaxParallelismLimit);
        using var semaphore = new SemaphoreSlim(maxParalellik, maxParalellik);
        var tasks = new List<Task>();
        var claimedCount = 0;

        while (claimedCount < _options.BatchSize)
        {
            await semaphore.WaitAsync(stoppingToken);

            EBelgeOutboxClaimLeaseResultDto? claim;
            using (var claimScope = _scopeFactory.CreateScope())
            {
                var claimService = claimScope.ServiceProvider.GetRequiredService<IEBelgeOutboxClaimLeaseService>();
                claim = await claimService.TryClaimNextAsync(TimeSpan.FromSeconds(_options.LeaseDurationSeconds), stoppingToken);
            }

            if (claim is null)
            {
                semaphore.Release();
                break;
            }

            claimedCount++;
            _metrics.RecordClaimed(claim.IsTuru);
            tasks.Add(ProcessClaimAsync(claim, semaphore, stoppingToken));
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }

        return claimedCount > 0;
    }

    /// <summary>
    /// Faz 2B.8 görev md.7: HER claim için YENİ bir DI scope oluşturulur, işlenir, dispose edilir
    /// - aynı `DbContext`/scoped servis birden fazla mesajda VEYA paralel task'ta PAYLAŞILMAZ.
    /// `IEBelgeOutboxMesajIslemeService.IsleAsync`'in döndürdüğü sonuç (Tamamlandi/RetryPlanlandi/
    /// TerminalHata/SahiplikKaybedildi) YALNIZ GÖZLEMLENİR - worker BURADA İKİNCİ bir complete/
    /// fail/retry/lease-release çağrısı YAPMAZ (bkz. görev md.12).
    /// </summary>
    private async Task ProcessClaimAsync(EBelgeOutboxClaimLeaseResultDto claim, SemaphoreSlim semaphore, CancellationToken stoppingToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            _healthState.IncrementInflight();
            _metrics.IncrementInflight();

            using var scope = _scopeFactory.CreateScope();
            var islemeService = scope.ServiceProvider.GetRequiredService<IEBelgeOutboxMesajIslemeService>();
            var sonuc = await islemeService.IsleAsync(claim, stoppingToken);

            _metrics.RecordResult(claim.IsTuru, sonuc.SonucTuru, stopwatch.Elapsed);
            LogSonuc(claim, sonuc, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Faz 2B.8 görev md.8 madde 5: host cancellation nedeniyle oluşan bir iptal, hata VEYA
            // retry olarak KAYDEDİLMEZ - warning/error seviyesinde LOGLANMAZ, metrik ARTIRILMAZ.
            _logger.LogInformation(
                "E-belge outbox mesajı işlenirken graceful shutdown nedeniyle iptal edildi. OutboxMesajiId={OutboxMesajiId}, IsTuru={IsTuru}",
                claim.OutboxMesajiId, claim.IsTuru);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Faz 2B.8 görev md.9/md.12: BURADA yakalanan bir exception, `IsleAsync`'in KENDİ
            // sözleşmesinin (handler exception'larını ZATEN yakalayıp retry/terminal'e ÇEVİRDİĞİ)
            // DIŞINDA, GERÇEKTEN beklenmedik bir worker-seviyesi arızadır (ör. scope/DI çözümleme
            // hatası). Worker BURADA outbox üzerinde İKİNCİ bir complete/fail çağrısı YAPMAZ -
            // lease süresi doğal olarak dolar, mesaj BAŞKA bir worker TARAFINDAN (VEYA aynı worker
            // bir SONRAKİ turda) yeniden claim edilir.
            _metrics.RecordPollError();
            _healthState.RecordWorkerError(WorkerLevelSafeErrorCode(ex));
            _logger.LogError(
                ex,
                "E-belge outbox mesajı işlenirken beklenmeyen worker-seviyesi hata. OutboxMesajiId={OutboxMesajiId}, IsTuru={IsTuru}",
                claim.OutboxMesajiId, claim.IsTuru);
        }
        finally
        {
            _healthState.DecrementInflight();
            _metrics.DecrementInflight();
            semaphore.Release();
        }
    }

    /// <summary>Bkz. görev md.14 - yalnız GÜVENLİ alanlar (Outbox ID, Kurum ID, EBelgeKaydi ID, iş türü, deneme sayısı, sonuç türü, işlem süresi). XML/lease token/sertifika/kişisel veri/tam hash ASLA loglanmaz.</summary>
    private void LogSonuc(EBelgeOutboxClaimLeaseResultDto claim, EBelgeOutboxIslemeSonucu sonuc, TimeSpan sure)
    {
        _logger.LogInformation(
            "E-belge outbox mesajı işlendi. OutboxMesajiId={OutboxMesajiId}, KurumId={KurumId}, EBelgeKaydiId={EBelgeKaydiId}, IsTuru={IsTuru}, DenemeSayisi={DenemeSayisi}, SonucTuru={SonucTuru}, SureMs={SureMs}",
            claim.OutboxMesajiId, claim.KurumId, claim.EBelgeKaydiId, claim.IsTuru, claim.DenemeSayisi, sonuc.SonucTuru, sure.TotalMilliseconds);
    }

    /// <summary>Bkz. görev md.14/md.15 - health state'e/loglara yazılan hata KODU her zaman SABİT, PII/exception-detayı İÇERMEYEN bir string olmalıdır.</summary>
    private static string WorkerLevelSafeErrorCode(Exception ex) => ex switch
    {
        SqlException => "EBELGE_OUTBOX_WORKER_SQL_HATASI",
        TimeoutException => "EBELGE_OUTBOX_WORKER_ZAMAN_ASIMI",
        _ => "EBELGE_OUTBOX_WORKER_BEKLENMEYEN_HATA"
    };
}
