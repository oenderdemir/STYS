using Microsoft.EntityFrameworkCore;
using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Entegrasyonlar.Pos.Services;

public interface IPosReceiptService
{
    Task<IReadOnlyCollection<PosOdemeSlipDto>> GetReceiptsAsync(int paymentId, CancellationToken ct);
    Task<PosReceiptContent> OpenReceiptContentAsync(int paymentId, int receiptId, CancellationToken ct);
}

public sealed record PosReceiptContent(Stream Stream, string ContentType);

/// <summary>
/// Authorized, tenant-scoped access to persisted PAVO receipt slips. The content endpoint never
/// returns the physical storage path; it streams bytes through this service after verifying the
/// payment belongs to the caller's institution/tesis scope.
/// </summary>
public sealed class PosReceiptService : IPosReceiptService
{
    private readonly StysAppDbContext _db;
    private readonly IPosReceiptStorage _storage;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public PosReceiptService(
        StysAppDbContext db,
        IPosReceiptStorage storage,
        ICurrentTenantAccessor tenantAccessor)
    {
        _db = db;
        _storage = storage;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<IReadOnlyCollection<PosOdemeSlipDto>> GetReceiptsAsync(int paymentId, CancellationToken ct)
    {
        var payment = await LoadValidatedPaymentAsync(paymentId, ct);

        return await _db.PosOdemeSlipleri
            .AsNoTracking()
            .Where(x => x.PosOdemeIslemiId == payment.Id && !x.IsDeleted)
            .OrderBy(x => x.Tip)
            .Select(x => new PosOdemeSlipDto
            {
                Id = x.Id,
                Tip = (int)x.Tip,
                ContentType = x.ContentType,
                DosyaBoyutu = x.DosyaBoyutu,
                Sha256 = x.Sha256,
                KaydedilmeTarihi = x.KaydedilmeTarihi,
                KaynakKomutTipi = x.KaynakKomutTipi
            })
            .ToListAsync(ct);
    }

    public async Task<PosReceiptContent> OpenReceiptContentAsync(int paymentId, int receiptId, CancellationToken ct)
    {
        var payment = await LoadValidatedPaymentAsync(paymentId, ct);

        var slip = await _db.PosOdemeSlipleri
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == receiptId && !x.IsDeleted, ct)
            ?? throw new BaseException("Slip kaydı bulunamadı.", 404);

        if (slip.PosOdemeIslemiId != payment.Id)
        {
            throw new BaseException("Slip bu ödemeye ait değil.", 400);
        }

        var stream = _storage.OpenRead(slip.StoragePath);
        return new PosReceiptContent(stream, string.IsNullOrWhiteSpace(slip.ContentType) ? "image/png" : slip.ContentType);
    }

    private async Task<PosOdemeIslemi> LoadValidatedPaymentAsync(int paymentId, CancellationToken ct)
    {
        var payment = await _db.PosOdemeIslemleri
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == paymentId && !x.IsDeleted, ct)
            ?? throw new BaseException("POS ödeme işlemi bulunamadı.", 404);

        if (!_tenantAccessor.IsSuperAdmin() && !_tenantAccessor.GetAccessibleKurumIds().Contains(payment.KurumId))
        {
            throw new BaseException("Bu kuruma erişim yetkiniz yok.", 403);
        }

        return payment;
    }
}
