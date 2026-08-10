using Microsoft.EntityFrameworkCore;
using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Entegrasyonlar.Pos.Services;

public sealed class PosTerminalService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IReadOnlyDictionary<string, IPosOdemeSaglayicisi> _saglayicilar;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public PosTerminalService(
        StysAppDbContext dbContext,
        IEnumerable<IPosOdemeSaglayicisi> saglayicilar,
        ICurrentTenantAccessor tenantAccessor)
    {
        _dbContext = dbContext;
        _saglayicilar = saglayicilar.ToDictionary(x => x.Kod, StringComparer.OrdinalIgnoreCase);
        _tenantAccessor = tenantAccessor;
    }

    public async Task<List<PosTerminalDto>> GetByCihazAsync(int cihazId, CancellationToken cancellationToken)
    {
        var cihaz = await GetCihazAsync(cihazId, cancellationToken);
        return await BuildTerminalQuery(_dbContext.PosTerminaller.AsNoTracking().Where(x => !x.IsDeleted && x.PosCihaziId == cihaz.Id))
            .OrderBy(x => x.Ad)
            .ToListAsync(cancellationToken);
    }

    public async Task<PosTerminalDto> GetByIdAsync(int cihazId, int id, CancellationToken cancellationToken)
    {
        var terminal = await GetTerminalAsync(cihazId, id, cancellationToken);
        return await MapAsync(terminal, cancellationToken);
    }

    public async Task<PosTerminalDto> KaydetAsync(int cihazId, int? id, PosTerminalKaydetRequest request, CancellationToken cancellationToken)
    {
        var cihaz = await GetCihazAsync(cihazId, cancellationToken);
        ValidateRouteDevice(request.PosCihaziId, cihaz.Id);

        if (string.IsNullOrWhiteSpace(request.Ad))
        {
            throw new BaseException("Terminal adı gereklidir.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.SaglayiciKodu))
        {
            throw new BaseException("Sağlayıcı kodu gereklidir.", 400);
        }

        var terminalId = NormalizeTerminalIdentity(request);
        if (string.IsNullOrWhiteSpace(terminalId))
        {
            throw new BaseException("TerminalId gereklidir.", 400);
        }

        var merchantId = Normalize(request.MerchantId ?? request.SourceTerminalReference);
        var sourceFingerprint = Normalize(request.SourceFingerprint);
        var saglayiciKodu = request.SaglayiciKodu.Trim().ToUpperInvariant();
        var saglayici = GetSaglayici(saglayiciKodu);

        var hesap = await ResolveKrediKartiHesabiAsync(request.KasaBankaHesapId, cihaz, cancellationToken);

        PosTerminal terminal;
        if (id.HasValue)
        {
            terminal = await GetTerminalAsync(cihaz.Id, id.Value, cancellationToken);
        }
        else
        {
            terminal = new PosTerminal
            {
                KurumId = cihaz.KurumId,
                TesisId = cihaz.TesisId,
                PosCihaziId = cihaz.Id,
                CreatedBy = "system",
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.PosTerminaller.Add(terminal);
        }

        var duplicate = await _dbContext.PosTerminaller.AnyAsync(x =>
            x.PosCihaziId == cihaz.Id
            && x.SerialNumber == terminalId
            && x.Id != terminal.Id
            && !x.IsDeleted, cancellationToken);
        if (duplicate)
        {
            throw new BaseException("Bu cihaz ve terminal ID ile bir terminal zaten tanımlı.", 409);
        }

        var pairingIdentityChanged = terminal.SaglayiciKodu != saglayiciKodu
            || terminal.SerialNumber != terminalId
            || terminal.SourceFingerprint != sourceFingerprint
            || terminal.SourceTerminalReference != merchantId;

        terminal.KurumId = cihaz.KurumId;
        terminal.TesisId = cihaz.TesisId;
        terminal.PosCihaziId = cihaz.Id;
        terminal.KasaBankaHesapId = hesap?.Id;
        terminal.AcquirerId = hesap?.Kod;
        terminal.AcquirerName = hesap is null ? null : (hesap.BankaAdi ?? hesap.Ad);
        terminal.SaglayiciKodu = saglayiciKodu;
        terminal.Ad = request.Ad.Trim();
        terminal.SerialNumber = terminalId;
        terminal.SourceFingerprint = sourceFingerprint;
        terminal.SourceTerminalReference = merchantId;
        terminal.AktifMi = request.AktifMi;

        saglayici.TerminalBilgileriniDogrula(terminal);

        if (pairingIdentityChanged)
        {
            terminal.PairingId = null;
            terminal.PairingCode = null;
            terminal.TargetFingerprint = null;
            terminal.EslesmeOnayliMi = !saglayici.EslesmeDestekliyorMu;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await MapAsync(terminal, cancellationToken);
    }

    public async Task<PosTerminalDto> DeleteAsync(int cihazId, int id, CancellationToken cancellationToken)
    {
        var terminal = await GetTerminalAsync(cihazId, id, cancellationToken);
        terminal.AktifMi = false;
        terminal.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await MapAsync(terminal, cancellationToken);
    }

    public async Task<PosTerminalDto> EslesmeBaslatAsync(int cihazId, int id, CancellationToken cancellationToken)
    {
        var terminal = await GetTerminalAsync(cihazId, id, cancellationToken);
        var saglayici = GetSaglayici(terminal.SaglayiciKodu);
        EnsurePairingSupported(saglayici);
        var result = await saglayici.EslesmeBaslatAsync(terminal, cancellationToken);
        ApplyPairingResult(terminal, result);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await MapAsync(terminal, cancellationToken);
    }

    public async Task<PosTerminalDto> EslesmeKontrolAsync(int cihazId, int id, CancellationToken cancellationToken)
    {
        var terminal = await GetTerminalAsync(cihazId, id, cancellationToken);
        var saglayici = GetSaglayici(terminal.SaglayiciKodu);
        EnsurePairingSupported(saglayici);
        var result = await saglayici.EslesmeKontrolAsync(terminal, cancellationToken);
        ApplyPairingResult(terminal, result);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await MapAsync(terminal, cancellationToken);
    }

    private static void ValidateRouteDevice(int? requestDeviceId, int routeDeviceId)
    {
        if (requestDeviceId.HasValue && requestDeviceId.Value != routeDeviceId)
        {
            throw new BaseException("Request içindeki PosCihaziId route ile aynı olmalıdır.", 400);
        }
    }

    private IPosOdemeSaglayicisi GetSaglayici(string kod)
    {
        if (_saglayicilar.TryGetValue(kod, out var saglayici))
        {
            return saglayici;
        }

        throw new BaseException($"'{kod}' kodlu POS sağlayıcısı desteklenmiyor.", 400);
    }

    private static void EnsurePairingSupported(IPosOdemeSaglayicisi saglayici)
    {
        if (!saglayici.EslesmeDestekliyorMu)
        {
            throw new BaseException($"{saglayici.Ad} sağlayıcısı cihaz eşleştirmeyi desteklemiyor.", 409);
        }
    }

    private static void ApplyPairingResult(PosTerminal terminal, PosEslesmeSonucu result)
    {
        terminal.PairingId = result.PairingId;
        terminal.PairingCode = result.PairingCode ?? terminal.PairingCode;
        terminal.TargetFingerprint = result.TargetFingerprint;
        terminal.EslesmeOnayliMi = result.OnayliMi;
    }

    private async Task<PosCihazi> GetCihazAsync(int cihazId, CancellationToken cancellationToken)
    {
        var cihaz = await _dbContext.PosCihazlari.FirstOrDefaultAsync(x => x.Id == cihazId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("POS cihazı bulunamadı.", 404);

        if (!_tenantAccessor.IsSuperAdmin() && !_tenantAccessor.GetAccessibleKurumIds().Contains(cihaz.KurumId))
        {
            throw new BaseException("Bu kuruma erişim yetkiniz yok.", 403);
        }

        return cihaz;
    }

    private async Task<PosTerminal> GetTerminalAsync(int cihazId, int id, CancellationToken cancellationToken)
    {
        await GetCihazAsync(cihazId, cancellationToken);
        return await _dbContext.PosTerminaller.FirstOrDefaultAsync(x => x.Id == id && x.PosCihaziId == cihazId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("POS terminali bulunamadı.", 404);
    }

    private async Task<KasaBankaHesap?> ResolveKrediKartiHesabiAsync(int? kasaBankaHesapId, PosCihazi cihaz, CancellationToken cancellationToken)
    {
        if (!kasaBankaHesapId.HasValue)
        {
            return null;
        }

        var hesap = await _dbContext.KasaBankaHesaplari
            .Include(x => x.Tesis)
            .FirstOrDefaultAsync(x => x.Id == kasaBankaHesapId.Value && !x.IsDeleted && x.AktifMi && x.Tip == KasaBankaHesapTipleri.KrediKarti, cancellationToken)
            ?? throw new BaseException("Aktif kredi kartı/POS hesabı bulunamadı.", 404);

        if (hesap.TesisId != cihaz.TesisId)
        {
            throw new BaseException("Terminalin bağlı olduğu kredi kartı hesabı aynı tesis kapsamında olmalıdır.", 400);
        }

        if (hesap.Tesis is null || hesap.Tesis.KurumId != cihaz.KurumId)
        {
            throw new BaseException("Terminalin bağlı olduğu kredi kartı hesabı aynı kurum kapsamında olmalıdır.", 400);
        }

        return hesap;
    }

    private async Task<PosTerminalDto> MapAsync(PosTerminal terminal, CancellationToken cancellationToken)
    {
        var projection = await BuildTerminalQuery(_dbContext.PosTerminaller
                .AsNoTracking()
                .Where(x => x.Id == terminal.Id))
            .SingleAsync(cancellationToken);
        return projection;
    }

    private IQueryable<PosTerminalDto> BuildTerminalQuery(IQueryable<PosTerminal> baseQuery)
    {
        return from terminal in baseQuery
               join cihaz in _dbContext.PosCihazlari on terminal.PosCihaziId equals cihaz.Id into cihazJoin
               from cihaz in cihazJoin.DefaultIfEmpty()
               join tesis in _dbContext.Tesisler on terminal.TesisId equals tesis.Id into tesisJoin
               from tesis in tesisJoin.DefaultIfEmpty()
               join hesap in _dbContext.KasaBankaHesaplari on terminal.KasaBankaHesapId equals hesap.Id into hesapJoin
               from hesap in hesapJoin.DefaultIfEmpty()
               select new PosTerminalDto
               {
                   Id = terminal.Id,
                   KurumId = terminal.KurumId,
                   TesisId = terminal.TesisId,
                   TesisAd = tesis != null ? tesis.Ad : null,
                   PosCihaziId = terminal.PosCihaziId,
                   PosCihaziAd = cihaz != null ? cihaz.Ad : null,
                   KasaBankaHesapId = terminal.KasaBankaHesapId,
                   KasaBankaHesapAd = hesap != null ? hesap.Ad : null,
                   SaglayiciKodu = terminal.SaglayiciKodu,
                   AcquirerId = terminal.AcquirerId ?? (hesap != null ? hesap.Kod : null),
                   AcquirerName = terminal.AcquirerName ?? (hesap != null ? (hesap.BankaAdi ?? hesap.Ad) : null),
                   Ad = terminal.Ad,
                   TerminalId = terminal.SerialNumber,
                   MerchantId = terminal.SourceTerminalReference,
                   SerialNumber = terminal.SerialNumber,
                   SourceFingerprint = terminal.SourceFingerprint,
                   SourceTerminalReference = terminal.SourceTerminalReference,
                   EslesmeOnayliMi = terminal.EslesmeOnayliMi,
                   AktifMi = terminal.AktifMi,
                   PairingId = terminal.PairingId,
                   PairingCode = terminal.PairingCode
               };
    }

    private static string Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string NormalizeTerminalIdentity(PosTerminalKaydetRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.TerminalId))
        {
            return request.TerminalId.Trim();
        }

        return Normalize(request.SerialNumber);
    }
}
