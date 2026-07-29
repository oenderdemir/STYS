using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Entegrasyonlar.Pos.Options;
using STYS.Infrastructure.EntityFramework;

namespace STYS.Entegrasyonlar.Pos.Services;

/// <summary>
/// Bekleyen fiziksel POS islemlerini tarayicidan bagimsiz takip eder. Her islem PosService
/// tarafindan atomik bir lease ile claim edildigi icin birden fazla cloud instance ayni
/// saglayici islemini eszamanli sorgulayamaz.
/// </summary>
public sealed class PosOdemeDurumTakipHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PosOdemeTakipOptions _options;
    private readonly ILogger<PosOdemeDurumTakipHostedService> _logger;

    public PosOdemeDurumTakipHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<PosOdemeTakipOptions> options,
        ILogger<PosOdemeDurumTakipHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("POS odeme durum takip servisi devre disi.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Clamp(_options.WorkerIntervalSeconds, 1, 300));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await BirTurCalistirAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "POS odeme durum takip turunda beklenmeyen hata.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task BirTurCalistirAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        List<TakipAdayi> adaylar;
        using (var discoveryScope = _scopeFactory.CreateScope())
        {
            var dbContext = discoveryScope.ServiceProvider.GetRequiredService<StysAppDbContext>();
            adaylar = await dbContext.PosOdemeIslemleri
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => !x.IsDeleted
                            && (x.Durum == PosOdemeDurumlari.Olusturuldu
                                || x.Durum == PosOdemeDurumlari.PosIslemiBekleniyor
                                || x.Durum == PosOdemeDurumlari.Basarili)
                            && (!x.SonrakiSorgulamaTarihi.HasValue || x.SonrakiSorgulamaTarihi <= now)
                            && (!x.TakipKilitBitisTarihi.HasValue || x.TakipKilitBitisTarihi < now))
                .OrderBy(x => x.SonrakiSorgulamaTarihi)
                .ThenBy(x => x.Id)
                .Take(Math.Clamp(_options.BatchSize, 1, 500))
                .Select(x => new TakipAdayi(x.Id, x.KurumId))
                .ToListAsync(cancellationToken);
        }

        foreach (var aday in adaylar)
        {
            await TakipEtAsync(aday, cancellationToken);
        }
    }

    private async Task TakipEtAsync(TakipAdayi aday, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var oncekiContext = httpContextAccessor.HttpContext;
        httpContextAccessor.HttpContext = CreateWorkerHttpContext(aday.KurumId);
        try
        {
            var service = scope.ServiceProvider.GetRequiredService<IPosService>();
            await service.OdemeDurumuAsync(aday.Id, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "POS odeme islemi takip edilemedi. PosOdemeIslemiId={PosOdemeIslemiId}, KurumId={KurumId}",
                aday.Id,
                aday.KurumId);
        }
        finally
        {
            httpContextAccessor.HttpContext = oncekiContext;
        }
    }

    private static DefaultHttpContext CreateWorkerHttpContext(int kurumId)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("kurumId", kurumId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.NameIdentifier, "pos-odeme-worker")
        ], "PosOdemeWorker");
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }

    private sealed record TakipAdayi(int Id, int KurumId);
}
