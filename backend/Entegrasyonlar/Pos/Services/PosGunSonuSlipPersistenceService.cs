using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Entegrasyonlar.Pos.Options;
using STYS.Infrastructure.EntityFramework;

namespace STYS.Entegrasyonlar.Pos.Services;

public interface IPosGunSonuSlipPersistenceService
{
    /// <summary>Persists the PerformEOD <c>eodImage</c> as a PosGunSonuSlipi + file. Idempotent by
    /// (PosGunSonuIslemiId, SlipTipi, Sha256). Returns replaced file paths for post-commit cleanup.</summary>
    Task<IReadOnlyCollection<string>> PersistAsync(
        StysAppDbContext db,
        PosGunSonuIslemi eod,
        string? eodImageBase64,
        CancellationToken ct);

    void Cleanup(IReadOnlyCollection<string> relativePaths);
}

/// <summary>
/// Gün sonu slip persistence. Best-effort: an invalid/oversized image is skipped and never flips the
/// EOD business outcome; raw Base64 never persists centrally. Uses the same decode/PNG/SHA semantics
/// as payment receipts but against the separate PosGunSonuSlipi entity/storage.
/// </summary>
public sealed class PosGunSonuSlipPersistenceService : IPosGunSonuSlipPersistenceService
{
    private readonly IPosGunSonuSlipStorage _storage;
    private readonly PosGunSonuSlipStorageOptions _options;
    private readonly ILogger<PosGunSonuSlipPersistenceService> _logger;

    public PosGunSonuSlipPersistenceService(
        IPosGunSonuSlipStorage storage,
        IOptions<PosGunSonuSlipStorageOptions> options,
        ILogger<PosGunSonuSlipPersistenceService> logger)
    {
        _storage = storage;
        _options = options.Value ?? new PosGunSonuSlipStorageOptions();
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<string>> PersistAsync(
        StysAppDbContext db,
        PosGunSonuIslemi eod,
        string? eodImageBase64,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(eodImageBase64))
        {
            return Array.Empty<string>();
        }

        try
        {
            var bytes = PosReceiptPersistenceService.DecodeBase64(eodImageBase64);
            if (bytes is null || bytes.Length == 0)
            {
                _logger.LogWarning("Gün sonu slip geçersiz Base64. PosGunSonuIslemiId={EodId}", eod.Id);
                return Array.Empty<string>();
            }

            if (!PosReceiptPersistenceService.IsPng(bytes))
            {
                _logger.LogWarning("Gün sonu slip PNG imzası doğrulanamadı. PosGunSonuIslemiId={EodId}", eod.Id);
                return Array.Empty<string>();
            }

            if (bytes.Length > _options.MaxImageBytes)
            {
                _logger.LogWarning("Gün sonu slip boyut limitini aşıyor. PosGunSonuIslemiId={EodId}, Boyut={Size}", eod.Id, bytes.Length);
                return Array.Empty<string>();
            }

            var sha = PosReceiptPersistenceService.ComputeSha256(bytes);
            var existing = await db.PosGunSonuSlipleri
                .FirstOrDefaultAsync(x => x.PosGunSonuIslemiId == eod.Id && x.SlipTipi == PosGunSonuSlipTipi.EodImage && !x.IsDeleted, ct);

            if (existing is not null && string.Equals(existing.Sha256, sha, StringComparison.OrdinalIgnoreCase))
            {
                return Array.Empty<string>(); // same image → no-op
            }

            var relativePath = await _storage.StoreAsync(eod.KurumId, eod.PosCihaziId, eod.Id, $"eod-{sha}.png", bytes, ct);
            var oldPath = existing?.StoragePath;

            if (existing is null)
            {
                db.PosGunSonuSlipleri.Add(new PosGunSonuSlipi
                {
                    KurumId = eod.KurumId,
                    TesisId = eod.TesisId,
                    PosGunSonuIslemiId = eod.Id,
                    PosCihaziId = eod.PosCihaziId,
                    SlipTipi = PosGunSonuSlipTipi.EodImage,
                    ContentType = "image/png",
                    StoragePath = relativePath,
                    Sha256 = sha,
                    DosyaBoyutu = bytes.Length,
                    OlusturulmaTarihi = DateTime.UtcNow,
                    CreatedBy = "agent",
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.StoragePath = relativePath;
                existing.Sha256 = sha;
                existing.DosyaBoyutu = bytes.Length;
                existing.OlusturulmaTarihi = DateTime.UtcNow;
            }

            var cleanup = new List<string>();
            if (!string.IsNullOrWhiteSpace(oldPath) && !string.Equals(oldPath, relativePath, StringComparison.OrdinalIgnoreCase))
            {
                cleanup.Add(oldPath);
            }

            return cleanup;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gün sonu slip kalıcılaştırma başarısız (best-effort). PosGunSonuIslemiId={EodId}", eod.Id);
            return Array.Empty<string>();
        }
    }

    public void Cleanup(IReadOnlyCollection<string> relativePaths)
    {
        foreach (var path in relativePaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                _storage.Delete(path);
            }
        }
    }
}
