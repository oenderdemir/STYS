using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using STYS.Agent.Contracts.Dtos;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Entegrasyonlar.Pos.Options;
using STYS.Infrastructure.EntityFramework;

namespace STYS.Entegrasyonlar.Pos.Services;

public interface IPosReceiptPersistenceService
{
    /// <summary>
    /// Extracts receipt images from a PAVO payment response and persists them (decode/validate/hash/
    /// store + PosOdemeSlip metadata). Changes are tracked on <paramref name="db"/> but not saved; the
    /// caller flushes them with its own SaveChanges. Best-effort: never throws.
    /// </summary>
    Task PersistAsync(
        StysAppDbContext db,
        string commandType,
        PosOdemeIslemi payment,
        PavoPaymentResponseBase? response,
        CancellationToken ct);
}

/// <summary>
/// Extracts PAVO receipt images from a payment response and persists them as files + PosOdemeSlip
/// metadata. Persistence is strictly best-effort: any validation/storage/DB failure is logged and
/// swallowed so it can never flip a payment's business state (section 32). Deduplication is by
/// SHA-256; re-persisting the same image is a no-op, a different image is a controlled in-place
/// replacement so one logical record per (payment, type) is maintained.
/// </summary>
public sealed class PosReceiptPersistenceService : IPosReceiptPersistenceService
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly IPosReceiptStorage _storage;
    private readonly PosReceiptStorageOptions _options;
    private readonly ILogger<PosReceiptPersistenceService> _logger;

    public PosReceiptPersistenceService(
        IPosReceiptStorage storage,
        IOptions<PosReceiptStorageOptions> options,
        ILogger<PosReceiptPersistenceService> logger)
    {
        _storage = storage;
        _options = options.Value ?? new PosReceiptStorageOptions();
        _logger = logger;
    }

    /// <summary>
    /// Persists any receipt images present on <paramref name="response"/> for the given payment.
    /// Changes are tracked on <paramref name="db"/> but not saved; the caller flushes them with its
    /// own SaveChanges so slip rows commit in the same unit of work as the payment update.
    /// </summary>
    public async Task PersistAsync(
        StysAppDbContext db,
        string commandType,
        PosOdemeIslemi payment,
        PavoPaymentResponseBase? response,
        CancellationToken ct)
    {
        if (response?.Data is null)
        {
            return;
        }

        await PersistOneAsync(db, commandType, payment, PosOdemeSlipTipi.Customer, response.Data.CustomerReceiptImage, ct);
        await PersistOneAsync(db, commandType, payment, PosOdemeSlipTipi.Merchant, response.Data.MerchantReceiptImage, ct);
        await PersistOneAsync(db, commandType, payment, PosOdemeSlipTipi.Error, response.Data.ErrorReceiptImage, ct);
    }

    private async Task PersistOneAsync(
        StysAppDbContext db,
        string commandType,
        PosOdemeIslemi payment,
        PosOdemeSlipTipi tip,
        string? base64Image,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(base64Image))
        {
            return;
        }

        try
        {
            var bytes = DecodeBase64(base64Image);
            if (bytes is null || bytes.Length == 0)
            {
                _logger.LogWarning("POS receipt geçersiz Base64. PosOdemeIslemiId={PaymentId}, Tip={Tip}", payment.Id, tip);
                return;
            }

            if (!IsPng(bytes))
            {
                _logger.LogWarning("POS receipt PNG imzası doğrulanamadı. PosOdemeIslemiId={PaymentId}, Tip={Tip}", payment.Id, tip);
                return;
            }

            if (bytes.Length > _options.MaxImageBytes)
            {
                _logger.LogWarning("POS receipt boyut limitini aşıyor. PosOdemeIslemiId={PaymentId}, Tip={Tip}, Boyut={Size}", payment.Id, tip, bytes.Length);
                return;
            }

            var sha = ComputeSha256(bytes);
            var existing = await db.PosOdemeSlipleri
                .FirstOrDefaultAsync(x => x.PosOdemeIslemiId == payment.Id && x.Tip == tip && !x.IsDeleted, ct);

            if (existing is not null && string.Equals(existing.Sha256, sha, StringComparison.OrdinalIgnoreCase))
            {
                return; // same image already stored → no-op
            }

            var relativePath = await _storage.StoreAsync(payment.KurumId, payment.Id, FileNameFor(tip), bytes, ct);
            var oldPath = existing?.StoragePath;

            if (existing is null)
            {
                db.PosOdemeSlipleri.Add(new PosOdemeSlip
                {
                    KurumId = payment.KurumId,
                    TesisId = payment.TesisId,
                    PosOdemeIslemiId = payment.Id,
                    Tip = tip,
                    ContentType = "image/png",
                    StoragePath = relativePath,
                    DosyaBoyutu = bytes.Length,
                    Sha256 = sha,
                    KaydedilmeTarihi = DateTime.UtcNow,
                    KaynakKomutTipi = commandType,
                    CreatedBy = "agent",
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.StoragePath = relativePath;
                existing.DosyaBoyutu = bytes.Length;
                existing.Sha256 = sha;
                existing.KaydedilmeTarihi = DateTime.UtcNow;
                existing.KaynakKomutTipi = commandType;
            }

            if (!string.IsNullOrWhiteSpace(oldPath))
            {
                _storage.Delete(oldPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "POS receipt kalıcılaştırma başarısız (best-effort). PosOdemeIslemiId={PaymentId}, Tip={Tip}", payment.Id, tip);
        }
    }

    private static string FileNameFor(PosOdemeSlipTipi tip) => tip switch
    {
        PosOdemeSlipTipi.Customer => "customer.png",
        PosOdemeSlipTipi.Merchant => "merchant.png",
        PosOdemeSlipTipi.Error => "error.png",
        _ => "unknown.png"
    };

    // --------------------------- pure, testable helpers ---------------------------

    /// <summary>Decodes plain Base64 or a defensive <c>data:image/png;base64,...</c> data URI.</summary>
    public static byte[]? DecodeBase64(string value)
    {
        var trimmed = value.Trim();
        var commaIndex = trimmed.IndexOf(',');
        var payload = commaIndex >= 0 ? trimmed[(commaIndex + 1)..] : trimmed;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static bool IsPng(byte[] bytes) =>
        bytes.Length >= PngSignature.Length && bytes.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature);

    /// <summary>Uppercase hex SHA-256, matching the repo's release-package storage convention.</summary>
    public static string ComputeSha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
