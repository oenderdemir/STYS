using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.PosTahsilatValorleri.Services;

/// <summary>
/// Valor gunu gelen ve otomatik aktarima acik POS tahsilatlarini periyodik olarak bankaya
/// (STYS ici muhasebe kaydiyla) aktarir. BU ISLEM BANKAYA FIZIKSEL PARA TRANSFERI YAPMAZ,
/// yalnizca STYS icindeki POS alacaginin bagli banka hesabina muhasebe kaydiyla aktarildigini
/// temsil eder. LicenseAwareMaintenanceHostedService ile ayni BackgroundService deseni.
/// </summary>
public sealed class PosValorAktarimHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private const int StuckDakika = 15;
    private const int AzamiOtomatikDeneme = 5;
    private const int BackoffDakika = 30;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PosValorAktarimHostedService> _logger;

    public PosValorAktarimHostedService(IServiceScopeFactory scopeFactory, ILogger<PosValorAktarimHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await BirTurCalistirAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PosValorAktarimHostedService beklenmeyen hata.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task BirTurCalistirAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StysAppDbContext>();
        var aktarimService = scope.ServiceProvider.GetRequiredService<IPosTahsilatValorAktarimService>();

        var bugun = ValorTarihHesaplamaService.BugunIstanbul();
        var stuckEsigi = DateTime.UtcNow.AddMinutes(-StuckDakika);
        var backoffEsigi = DateTime.UtcNow.AddMinutes(-BackoffDakika);

        // (i) ValorBekliyor + otomatik aktarima acik + valor tarihi gelmis + belge aktif
        var valorBekleyenler = await dbContext.PosTahsilatValorleri.AsNoTracking()
            .Where(x => !x.IsDeleted
                        && x.Durum == PosTahsilatValorDurumlari.ValorBekliyor
                        && x.OtomatikAktarimMi
                        && x.BeklenenValorTarihi <= bugun
                        && x.TahsilatOdemeBelgesi!.Durum == TahsilatOdemeBelgeDurumlari.Aktif)
            .Select(x => x.Id)
            .ToListAsync(stoppingToken);

        // (ii) Hata + yeniden-deneme uygun (limit/backoff dahilinde)
        var hataliUygunlar = await dbContext.PosTahsilatValorleri.AsNoTracking()
            .Where(x => !x.IsDeleted
                        && x.Durum == PosTahsilatValorDurumlari.Hata
                        && x.OtomatikAktarimMi
                        && x.DenemeSayisi < AzamiOtomatikDeneme
                        && (x.SonDenemeTarihi == null || x.SonDenemeTarihi < backoffEsigi))
            .Select(x => x.Id)
            .ToListAsync(stoppingToken);

        // (iii) Takili (Aktariliyor + AktarimBaslamaTarihi < stuckEsigi) - job bu id'leri
        // gercekten sorgular, aksi halde claim'in kurtarma dali hic tetiklenmez.
        var takiliKayitlar = await dbContext.PosTahsilatValorleri.AsNoTracking()
            .Where(x => !x.IsDeleted
                        && x.Durum == PosTahsilatValorDurumlari.Aktariliyor
                        && x.AktarimBaslamaTarihi != null
                        && x.AktarimBaslamaTarihi < stuckEsigi)
            .Select(x => x.Id)
            .ToListAsync(stoppingToken);

        var tumIdler = valorBekleyenler.Concat(hataliUygunlar).Concat(takiliKayitlar).Distinct();

        foreach (var id in tumIdler)
        {
            try
            {
                await aktarimService.HesabaAktarAsync(id, null, stoppingToken);
            }
            catch (BaseException ex)
            {
                _logger.LogInformation(ex, "PosValorAktarimHostedService: kayıt {Id} işlenemedi.", id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PosValorAktarimHostedService: kayıt {Id} işlenirken beklenmeyen hata.", id);
            }
        }
    }
}
