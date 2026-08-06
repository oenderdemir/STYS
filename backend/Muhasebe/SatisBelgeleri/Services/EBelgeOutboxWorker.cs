using System.Diagnostics;
using System.Runtime.ExceptionServices;
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
///
/// Faz 2B.8.1 görev md.1-4 - bir polling turunda BAŞLATILAN her `ProcessClaimAsync` task'ı, tur
/// başarıyla/hatayla/cancellation İLE bitse BİLE MUTLAKA await edilir (`BirTurCalistirAsync`'in
/// `finally` bloğu) - semaphore, HİÇBİR çalışan task `Release()` çağırmadan dispose EDİLMEZ, permit
/// bir processing task'a DEVREDİLMEDİĞİ HER yolda (null claim/claim exception/claim cancellation/
/// scope hatası) İÇ `finally` bloğu TARAFINDAN geri BIRAKILIR.
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
                var safeErrorCode = WorkerLevelSafeErrorCode(ex);
                _healthState.RecordWorkerError(safeErrorCode);
                LogWorkerLevelHataGuvenli("PollingTuru", safeErrorCode, ex);
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
    ///
    /// Faz 2B.8.1 görev md.1/md.3 - bu turda BAŞLATILAN (semaphore permit'i DEVRALMIŞ) HER
    /// `ProcessClaimAsync` task'ı, claim döngüsü İÇİNDE bir exception OLUŞSA/turu erken
    /// SONLANDIRSA BİLE, `finally` bloğunda `Task.WhenAll` İLE MUTLAKA await edilir - semaphore
    /// ANCAK bundan SONRA dispose edilir. Bu, hem "bir tur claim hatasıyla sonlanırsa ÖNCEKİ turun
    /// task'ları tamamlanmadan YENİ tur başlamaz" (çünkü `ExecuteAsync`'in dış döngüsü BU metodun
    /// dönmesini/fırlatmasını BEKLER) hem de "unobserved task exception KALMAZ" gereksinimlerini
    /// (görev md.2-3) TEK bir mekanizma İLE sağlar.
    /// </summary>
    private async Task<bool> BirTurCalistirAsync(CancellationToken stoppingToken)
    {
        // Faz 2B.8.1 görev md.7/md.9: worker VE health check AYNI değerlendirmeyi paylaşır -
        // burada TEK bir `Evaluate()` çağrısı yapılır, sonucu health state'e YAZILIR.
        var aktivasyonKarari = _activationGate.Evaluate();
        _healthState.RecordActivationDecision(aktivasyonKarari);

        // Faz 2B.8 görev md.17: gate kapalıyken mesajlar terminal hataya geçirilmez, deneme
        // sayısı artırılmaz, lease alınmaz - claim'e HİÇ GİDİLMEDEN olduğu yerde beklenir.
        if (!aktivasyonKarari.CanProcess)
        {
            return false;
        }

        var maxParalellik = Math.Clamp(_options.MaxParallelism, 1, EBelgeProcessingOptions.MaxParallelismLimit);
        var semaphore = new SemaphoreSlim(maxParalellik, maxParalellik);
        var tasks = new List<Task>();
        var claimedCount = 0;
        Exception? turHatasi = null;
        Exception? taskHatasi = null;

        try
        {
            while (claimedCount < _options.BatchSize)
            {
                // BURADA (henüz İÇ try/finally'e girmeden) fırlayan bir cancellation, HİÇ permit
                // ALINMADIĞI için release GEREKTİRMEZ.
                await semaphore.WaitAsync(stoppingToken);
                var izinTaskaDevredildi = false;

                try
                {
                    EBelgeOutboxClaimLeaseResultDto? claim;
                    using (var claimScope = _scopeFactory.CreateScope())
                    {
                        var claimService = claimScope.ServiceProvider.GetRequiredService<IEBelgeOutboxClaimLeaseService>();
                        claim = await claimService.TryClaimNextAsync(TimeSpan.FromSeconds(_options.LeaseDurationSeconds), stoppingToken);
                    }

                    if (claim is null)
                    {
                        break;
                    }

                    claimedCount++;
                    _metrics.RecordClaimed(claim.IsTuru);
                    tasks.Add(ProcessClaimAsync(claim, semaphore, stoppingToken));
                    izinTaskaDevredildi = true;
                }
                finally
                {
                    // Faz 2B.8.1 görev md.1/md.4: claim null/exception/cancellation VEYA scope/DI
                    // çözümleme hatası - permit'in bir processing task'a DEVREDİLMEDİĞİ HER yol -
                    // burada GERİ BIRAKILIR. Permit bir task'a devredildiyse (izinTaskaDevredildi),
                    // artık YALNIZ o task (`ProcessClaimAsync`'in KENDİ finally'i) release eder -
                    // aynı permit İKİ KEZ bırakılmaz.
                    if (!izinTaskaDevredildi)
                    {
                        semaphore.Release();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Faz 2B.8.1 görev md.1: tur BİR exception İLE sonlanıyor OLSA BİLE, BAŞLATILMIŞ
            // task'lar sahipsiz BIRAKILMAZ - exception BURADA yutulmaz, yalnız `finally`
            // TARAFINDAN tüm task'lar await EDİLDİKTEN SONRA yeniden fırlatılmak üzere SAKLANIR.
            turHatasi = ex;
        }
        finally
        {
            // Faz 2B.8.2 görev md.6: `Task.WhenAll(tasks)`'ın KENDİSİ bir exception ÜRETİRSE
            // (worker altyapısından kaynaklanan, ör. bir task'ın KENDİSİ fatal bir hatayla
            // faulted olması), `semaphore.Dispose()` YİNE de MUTLAKA çalışmalıdır - bu yüzden
            // AYRI, İÇ bir try/catch/finally İLE dispose GÜVENCE altına alınır; `Task.WhenAll`'dan
            // gelen hata AYRI bir değişkende (`taskHatasi`) saklanır, turHatasi'nin ÜZERİNE
            // YAZILMAZ.
            try
            {
                if (tasks.Count > 0)
                {
                    await Task.WhenAll(tasks);
                }
            }
            catch (Exception ex)
            {
                taskHatasi = ex;
            }
            finally
            {
                semaphore.Dispose();
            }
        }

        RethrowTurVeTaskHatalariGuvenliSekilde(turHatasi, taskHatasi, stoppingToken);

        return claimedCount > 0;
    }

    /// <summary>
    /// Faz 2B.8.2 görev md.6 - claim/tur hatası (`turHatasi`) İLE `Task.WhenAll` hatası
    /// (`taskHatasi`) AYNI turda OLUŞABİLİR; İKİSİ de KAYBOLMAMALI, biri diğerini SESSİZCE
    /// EZMEMELİ. Açık öncelik politikası:
    ///
    /// 1. **Fatal** (`OutOfMemoryException` - doğrudan VEYA `Task.WhenAll`'ın ürettiği bir
    ///    `AggregateException` İÇİNDE) HER ZAMAN ÖNCELİKLİDİR ve HİÇBİR sarmalayıcı OLMADAN,
    ///    orijinal TİPİYLE yeniden fırlatılır - genel bir catch İLE ASLA gizlenmez.
    /// 2. **Host cancellation** (`OperationCanceledException` VE `stoppingToken.
    ///    IsCancellationRequested`) İKİNCİ önceliktir - `ExecuteAsync`'in ÖZEL cancellation
    ///    filtresinin (normal shutdown olarak ele alması İÇİN) doğru TİPLE eşleşmesi GEREKİR, bu
    ///    yüzden SARILMADAN yeniden fırlatılır.
    /// 3. İKİSİ de VARSA VE İKİSİ de yukarıdaki İKİ kategoriye GİRMİYORSA - HİÇBİRİ sessizce
    ///    EZİLMEZ; GÜVENLİ (SABİT, hiçbir ham exception metni İÇERMEYEN bir mesajla) bir
    ///    `AggregateException` İÇİNDE İKİSİ DE gözlemlenebilir kalacak şekilde BİRLEŞTİRİLİR.
    /// 4. Yalnız BİRİ VARSA, doğrudan (orijinal stack trace KORUNARAK) yeniden fırlatılır.
    /// </summary>
    private static void RethrowTurVeTaskHatalariGuvenliSekilde(Exception? turHatasi, Exception? taskHatasi, CancellationToken stoppingToken)
    {
        if (turHatasi is null && taskHatasi is null)
        {
            return;
        }

        var fatal = FindOutOfMemoryException(turHatasi) ?? FindOutOfMemoryException(taskHatasi);
        if (fatal is not null)
        {
            ExceptionDispatchInfo.Capture(fatal).Throw();
        }

        if (IsHostCancellation(turHatasi, stoppingToken))
        {
            ExceptionDispatchInfo.Capture(turHatasi!).Throw();
        }

        if (IsHostCancellation(taskHatasi, stoppingToken))
        {
            ExceptionDispatchInfo.Capture(taskHatasi!).Throw();
        }

        if (turHatasi is not null && taskHatasi is not null)
        {
            throw new AggregateException(
                "E-belge outbox worker polling turunda birden fazla hata oluştu (claim/tur hatası ve task hatası).",
                turHatasi, taskHatasi);
        }

        ExceptionDispatchInfo.Capture((turHatasi ?? taskHatasi)!).Throw();
    }

    private static OutOfMemoryException? FindOutOfMemoryException(Exception? ex) => ex switch
    {
        null => null,
        OutOfMemoryException oom => oom,
        AggregateException agg => agg.InnerExceptions.OfType<OutOfMemoryException>().FirstOrDefault(),
        _ => null,
    };

    private static bool IsHostCancellation(Exception? ex, CancellationToken stoppingToken) =>
        ex is OperationCanceledException && stoppingToken.IsCancellationRequested;

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
            var safeErrorCode = WorkerLevelSafeErrorCode(ex);
            _healthState.RecordWorkerError(safeErrorCode);
            LogWorkerLevelHataGuvenli("MesajIsleme", safeErrorCode, ex, claim);
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

    /// <summary>
    /// Faz 2B.8.1 görev md.5/md.6 - worker-seviyesi bir exception'ı GÜVENLİ biçimde loglar.
    /// Exception NESNESİ (`ex`) logger'a HİÇBİR ZAMAN GEÇİRİLMEZ - yalnız SABİT/type-safe alanlar:
    /// güvenli hata kodu, exception TİP ADI (`ex.GetType().Name`), iş türü VE güvenli kimlik
    /// alanları (Outbox/Kurum/EBelgeKaydi ID). Exception'ın KENDİ mesajı, inner exception'ı veya
    /// `ToString()`'i - SQL statement/parametre, XML, lease token, sertifika/PFX/PEM,
    /// SignatureValue, VKN/TCKN, müşteri bilgisi, bağlantı parolası, URL query secret'ı TAŞIYABİLİR
    /// - production logger'ına ASLA YAZILMAZ (bkz. görev md.5). Serilog gibi GERÇEK sağlayıcılar,
    /// `ILogger.LogError(ex, ...)` çağrısında exception NESNESİNİN `ToString()`'ini OTOMATİK olarak
    /// render EDER - bu yüzden `ex` PARAMETRE OLARAK BİLE geçirilmez, yalnız türü/kodu okunur.
    /// </summary>
    private void LogWorkerLevelHataGuvenli(string baglam, string safeErrorCode, Exception ex, EBelgeOutboxClaimLeaseResultDto? claim = null)
    {
        if (claim is not null)
        {
            _logger.LogError(
                "E-belge outbox worker hatası. Baglam={Baglam}, OutboxMesajiId={OutboxMesajiId}, IsTuru={IsTuru}, HataKodu={HataKodu}, ExceptionType={ExceptionType}",
                baglam, claim.OutboxMesajiId, claim.IsTuru, safeErrorCode, ex.GetType().Name);
        }
        else
        {
            _logger.LogError(
                "E-belge outbox worker hatası. Baglam={Baglam}, HataKodu={HataKodu}, ExceptionType={ExceptionType}",
                baglam, safeErrorCode, ex.GetType().Name);
        }
    }

    /// <summary>
    /// Bkz. görev md.14/md.15 - health state'e/loglara yazılan hata KODU her zaman SABİT, PII/
    /// exception-detayı İÇERMEYEN bir string olmalıdır. Faz 2B.8.2 görev md.6 - claim/tur hatası
    /// VE task hatası AYNI turda oluşup bir `AggregateException` İÇİNDE BİRLEŞTİRİLDİYSE
    /// (`RethrowTurVeTaskHatalariGuvenliSekilde`), İÇ exception'lar İNCELENEREK YİNE GÜVENLİ/
    /// type-safe bir sınıflandırma yapılır - ham metin OKUNMAZ.
    /// </summary>
    private static string WorkerLevelSafeErrorCode(Exception ex) => ex switch
    {
        SqlException => "EBELGE_OUTBOX_WORKER_SQL_HATASI",
        TimeoutException => "EBELGE_OUTBOX_WORKER_ZAMAN_ASIMI",
        AggregateException agg when agg.InnerExceptions.Any(inner => inner is SqlException) => "EBELGE_OUTBOX_WORKER_SQL_HATASI",
        AggregateException agg when agg.InnerExceptions.Any(inner => inner is TimeoutException) => "EBELGE_OUTBOX_WORKER_ZAMAN_ASIMI",
        _ => "EBELGE_OUTBOX_WORKER_BEKLENMEYEN_HATA"
    };
}
