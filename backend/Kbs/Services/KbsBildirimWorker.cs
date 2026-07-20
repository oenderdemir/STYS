using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using STYS.Bildirimler;
using STYS.Bildirimler.Dto;
using STYS.Bildirimler.Services;
using STYS.Infrastructure.EntityFramework;
using STYS.Kbs.Connectors;
using STYS.Kbs.Constants;
using STYS.Kbs.Dtos;
using STYS.Kbs.Entities;
using STYS.Kbs.Options;
using STYS.Kbs.Payload;

namespace STYS.Kbs.Services;

public class KbsBildirimWorker(IServiceScopeFactory scopeFactory, IOptions<KbsOptions> options, ILogger<KbsBildirimWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await RecoverAsync(stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        catch (Exception) { logger.LogError("KBS worker kurtarma taramasi tamamlanamadi."); }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(2, options.Value.WorkerIntervalSeconds)));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogError("KBS worker batch islemi tamamlanamadi; sonraki dongude yeniden denenecek."); }
            try { if (!await timer.WaitForNextTickAsync(stoppingToken)) break; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    internal async Task RecoverAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StysAppDbContext>();
        var threshold = DateTime.UtcNow.AddMinutes(-Math.Max(1, options.Value.SendingRecoveryMinutes));
        var stuck = await db.KbsBildirimler.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.Durum == KbsBildirimDurumlari.Gonderiliyor && x.GonderimTarihi < threshold).ToListAsync(ct);
        foreach (var item in stuck)
        {
            item.Durum = KbsBildirimDurumlari.SonucuBelirsiz;
            item.SonHataKodu = "WORKER-RECOVERY";
            item.SonHataMesaji = "Onceki gonderimin sonucu belirsiz; manuel mutabakat gerekli.";
        }
        if (stuck.Count == 0) return;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { /* Baska worker kaydi sahiplenmis olabilir. */ }
    }

    internal async Task ProcessBatchAsync(CancellationToken ct)
    {
        List<long> ids;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StysAppDbContext>();
            var now = DateTime.UtcNow;
            ids = await db.KbsBildirimler.IgnoreQueryFilters()
                .Where(x => !x.IsDeleted && (x.Durum == KbsBildirimDurumlari.Hazir || x.Durum == KbsBildirimDurumlari.TekrarBekliyor)
                    && (x.SonrakiDenemeTarihi == null || x.SonrakiDenemeTarihi <= now)
                    && x.Saglayici != KbsEntegrasyonTipleri.Excel)
                .OrderBy(x => x.Id).Select(x => x.Id).Take(20).ToListAsync(ct);
        }

        foreach (var id in ids)
        {
            try { await ProcessOneAsync(id, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception) { logger.LogError("KBS bildirimi izole edilerek atlandi. BildirimId={BildirimId}", id); }
        }
    }

    private async Task ProcessOneAsync(long id, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StysAppDbContext>();
        var item = await db.KbsBildirimler.IgnoreQueryFilters().FirstOrDefaultAsync(
            x => x.Id == id && !x.IsDeleted && (x.Durum == KbsBildirimDurumlari.Hazir || x.Durum == KbsBildirimDurumlari.TekrarBekliyor), ct);
        if (item is null) return;

        var snapshot = TryReadSnapshot(scope.ServiceProvider.GetRequiredService<IKbsPayloadProtector>(), item, out var payloadError);
        if (snapshot is null)
        {
            await CompleteConfigError(db, item, payloadError!, "Korunan KBS payload dogrulanamadi; manuel mudahale gerekli.", ct);
            return;
        }

        var setting = await db.KbsTesisAyarlari.IgnoreQueryFilters().FirstOrDefaultAsync(
            x => x.TesisId == item.TesisId && x.KurumId == item.KurumId && !x.IsDeleted, ct);
        if (setting is null || !setting.AktifMi) { await CompleteConfigError(db, item, "KBS-SETTING", "Aktif KBS tesis ayari bulunamadi.", ct); return; }
        if (item.Saglayici != KbsEntegrasyonTipleri.Fake && !setting.CanliGonderimAktifMi)
        { await CompleteConfigError(db, item, "LIVE-DISABLED", "Canli gonderim tesis icin kapali.", ct); return; }

        item.Durum = KbsBildirimDurumlari.Gonderiliyor;
        item.GonderimTarihi = DateTime.UtcNow;
        item.DenemeSayisi++;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return; }

        var connector = scope.ServiceProvider.GetRequiredService<IKbsConnectorResolver>().Resolve(item.Saglayici);
        KbsSonuc result;
        try
        {
            result = item.BildirimTipi switch
            {
                KbsBildirimTipleri.Giris => await connector.GirisBildirAsync(new(item.TesisId, item.RezervasyonKonaklayanId,
                    snapshot.Ad ?? string.Empty, snapshot.Soyad ?? string.Empty, snapshot.KimlikNo, snapshot.BelgeNo, snapshot.UyrukKodu, snapshot.OlayTarihi), ct),
                KbsBildirimTipleri.Cikis => await connector.CikisBildirAsync(new(item.TesisId, item.RezervasyonKonaklayanId, snapshot.OlayTarihi), ct),
                KbsBildirimTipleri.OdaGuncelleme => await connector.OdaGuncelleAsync(new(item.TesisId, item.RezervasyonKonaklayanId,
                    snapshot.YeniOdaNo ?? string.Empty, snapshot.OlayTarihi), ct),
                _ => new(false, "UNSUPPORTED", "Bildirim tipi connector tarafindan desteklenmiyor.", KbsHataSiniflari.Permanent)
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { result = new(false, "CONNECTOR-EXCEPTION", "Connector isteginin sonucu dogrulanamadi.", KbsHataSiniflari.Uncertain, true); }

        ApplyResult(item, result);
        db.KbsBildirimDenemeleri.Add(new KbsBildirimDenemesi
        {
            KurumId = item.KurumId, KbsBildirimId = item.Id, DenemeTarihi = DateTime.UtcNow, Sonuc = item.Durum,
            HataSinifi = result.HataSinifi, SaglayiciHataKodu = result.Kod, MaskelenmisAciklama = Mask(result.Aciklama)
        });
        try { await db.SaveChangesAsync(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { await MarkUncertainAsync(id, CancellationToken.None); throw; }
        catch (Exception) { await MarkUncertainAsync(id, ct); return; }

        logger.LogInformation("KBS bildirimi islendi. BildirimId={BildirimId} TesisId={TesisId} Durum={Durum} Kod={Kod}", item.Id, item.TesisId, item.Durum, result.Kod);
        try
        {
            var notifications = scope.ServiceProvider.GetRequiredService<IBildirimService>();
            await notifications.PublishToTesisUsersAsync(item.TesisId, new BildirimOlusturRequestDto
            {
                Tip = "Kbs", Baslik = "KBS bildirim sonucu", Mesaj = $"KBS bildirimi #{item.Id}: {item.Durum}",
                Severity = result.Basarili ? BildirimSeverityleri.Success : BildirimSeverityleri.Warn, Link = "/kbs-bildirim-merkezi"
            }, ct);
        }
        catch (Exception) { logger.LogWarning("KBS sonucu uygulama bildirimine donusturulemedi. BildirimId={BildirimId}", item.Id); }
    }

    private static KbsPayloadSnapshot? TryReadSnapshot(IKbsPayloadProtector protector, KbsBildirim item, out string? error)
    {
        error = null;
        string canonical;
        try { canonical = protector.Unprotect(item.ProtectedPayload); }
        catch { error = "PAYLOAD-UNPROTECT"; return null; }
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(KbsCanonicalPayload.Hash(canonical)), Convert.FromHexString(item.PayloadHash)))
            { error = "PAYLOAD-HASH"; return null; }
        }
        catch { error = "PAYLOAD-HASH"; return null; }
        try
        {
            var snapshot = KbsCanonicalPayload.Deserialize(canonical);
            if (snapshot.Version != item.PayloadVersion || snapshot.KurumId != item.KurumId || snapshot.TesisId != item.TesisId
                || snapshot.RezervasyonId != item.RezervasyonId || snapshot.RezervasyonKonaklayanId != item.RezervasyonKonaklayanId
                || snapshot.BildirimTipi != item.BildirimTipi)
            { error = "PAYLOAD-INVARIANT"; return null; }
            return snapshot;
        }
        catch { error = "PAYLOAD-FORMAT"; return null; }
    }

    private async Task MarkUncertainAsync(long id, CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<StysAppDbContext>();
            var item = await db.KbsBildirimler.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
            if (item is null || item.Durum != KbsBildirimDurumlari.Gonderiliyor) return;
            item.Durum = KbsBildirimDurumlari.SonucuBelirsiz;
            item.SonHataKodu = "POST-SEND-PERSIST";
            item.SonHataMesaji = "Dis sistem sonucu alindi ancak yerel sonuc kaydedilemedi; mutabakat gerekli.";
            await db.SaveChangesAsync(ct);
        }
        catch (Exception) { logger.LogError("KBS sonucu belirsiz olarak isaretlenemedi. BildirimId={BildirimId}", id); }
    }

    private void ApplyResult(KbsBildirim item, KbsSonuc result)
    {
        item.SonHataKodu = result.Basarili ? null : result.Kod;
        item.SonHataMesaji = result.Basarili ? null : Mask(result.Aciklama);
        if (result.Basarili) { item.Durum = KbsBildirimDurumlari.Basarili; item.TamamlanmaTarihi = DateTime.UtcNow; return; }
        if (result.SonucuBelirsiz || result.HataSinifi == KbsHataSiniflari.Uncertain) { item.Durum = KbsBildirimDurumlari.SonucuBelirsiz; return; }
        if (result.HataSinifi is KbsHataSiniflari.Configuration or KbsHataSiniflari.Permanent || item.DenemeSayisi >= options.Value.MaxAttempts)
        { item.Durum = KbsBildirimDurumlari.MudahaleGerekli; return; }
        item.Durum = KbsBildirimDurumlari.TekrarBekliyor;
        var seconds = Math.Min(3600, Math.Pow(2, item.DenemeSayisi) * 10) + Random.Shared.Next(1, 10);
        item.SonrakiDenemeTarihi = DateTime.UtcNow.AddSeconds(seconds);
    }

    private static async Task CompleteConfigError(StysAppDbContext db, KbsBildirim item, string code, string message, CancellationToken ct)
    { item.Durum = KbsBildirimDurumlari.MudahaleGerekli; item.SonHataKodu = code; item.SonHataMesaji = message; await db.SaveChangesAsync(ct); }

    internal static string Mask(string text) => Regex.Replace(text, @"\b\d{5,}\b", "***");
}
