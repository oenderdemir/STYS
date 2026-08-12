using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Entities;
using STYS.Agent.Services;
using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Rezervasyonlar.Entities;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Tesisler.Entities;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Entegrasyonlar.Pos.Services;

public sealed class PosPaymentTestService : IPosPaymentTestService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly StysAppDbContext _db;
    private readonly AgentCommandService _agentCommandService;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public PosPaymentTestService(
        StysAppDbContext db,
        AgentCommandService agentCommandService,
        ICurrentTenantAccessor tenantAccessor)
    {
        _db = db;
        _agentCommandService = agentCommandService;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<IReadOnlyCollection<PosOdemeIslemiDto>> GetRecentAsync(int cihazId, int take, CancellationToken cancellationToken)
    {
        var cihaz = await GetDeviceAsync(cihazId, cancellationToken);
        var query = _db.PosOdemeIslemleri
            .AsNoTracking()
            .Include(x => x.PosTerminal)
            .Where(x => x.PosCihaziId == cihaz.Id && x.SaleReference != null)
            .OrderByDescending(x => x.BaslatilmaTarihi ?? x.CreatedAt ?? DateTime.MinValue)
            .Take(Math.Clamp(take, 1, 20));

        return await query
            .Select(x => ToDto(x, x.PosTerminal == null ? null : x.PosTerminal.SaglayiciKodu))
            .ToListAsync(cancellationToken);
    }

    public async Task<PosOdemeIslemiDto> StartAsync(int cihazId, PosPaymentBaslatRequest request, string requestedBy, CancellationToken cancellationToken)
    {
        if (request.Tutar <= 0)
        {
            throw new BaseException("Ödeme tutarı sıfırdan büyük olmalıdır.", 400);
        }

        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        if (idempotencyKey is null)
        {
            throw new BaseException("IdempotencyKey zorunludur.", 400);
        }

        var cihaz = await GetDeviceAsync(cihazId, cancellationToken);
        var terminal = await GetTerminalAsync(request.PosTerminalId, cancellationToken);
        EnsureTerminalBelongsToDevice(cihaz, terminal);
        _ = await GetValidatedAgentAsync(cihaz, cancellationToken);
        var readiness = await GetReadinessAsync(cihaz, cancellationToken);
        if (!readiness.Ready)
        {
            throw new BaseException($"PAVO cihazı ödeme için hazır değil: {readiness.LastError ?? "hazırlık doğrulanamadı."}", 409);
        }
        var hesap = await GetValidatedAccountAsync(terminal, cancellationToken);
        var rezervasyonId = await ResolveReservationIdAsync(cihaz.TesisId, cancellationToken);

        var now = DateTime.UtcNow;
        PosOdemeIslemi? islem;
        var loadedFromConflict = false;

        try
        {
            await using (var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
            {
                islem = await LoadExistingPaymentForStartAsync(request, idempotencyKey, cancellationToken);

                if (islem is null)
                {
                    var saleReference = GenerateSaleReference(cihaz.Id, terminal.Id, now);
                    islem = new PosOdemeIslemi
                    {
                        KurumId = cihaz.KurumId,
                        TesisId = cihaz.TesisId,
                        PosCihaziId = cihaz.Id,
                        PosTerminalId = terminal.Id,
                        KasaBankaHesapId = hesap.Id,
                        SaleReference = saleReference,
                        IdempotencyKey = idempotencyKey,
                        RezervasyonId = rezervasyonId,
                        Tutar = request.Tutar,
                        ParaBirimi = NormalizeCurrency(request.ParaBirimi),
                        Durum = PosOdemeDurumlari.Pending,
                        Aciklama = Normalize(request.Aciklama),
                        BaslatilmaTarihi = now,
                        AcquirerId = terminal.AcquirerId,
                        TerminalId = terminal.SerialNumber,
                        MerchantId = terminal.SourceTerminalReference,
                        CreatedBy = requestedBy,
                        CreatedAt = now
                    };
                    _db.PosOdemeIslemleri.Add(islem);
                    await _db.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);
                }
                else
                {
                    ValidateExistingPaymentForStart(islem, cihaz, terminal, hesap, request, idempotencyKey);
                    if (string.IsNullOrWhiteSpace(islem.IdempotencyKey))
                    {
                        islem.IdempotencyKey = idempotencyKey;
                        await _db.SaveChangesAsync(cancellationToken);
                    }
                    await tx.CommitAsync(cancellationToken);
                }
            }
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            loadedFromConflict = true;
            islem = await LoadExistingPaymentForStartAsync(request, idempotencyKey, cancellationToken);
        }

        if (islem is null)
        {
            throw new BaseException("Ödeme işlemi oluşturulamadı.", 500);
        }

        if (loadedFromConflict)
        {
            ValidateExistingPaymentForStart(islem, cihaz, terminal, hesap, request, idempotencyKey);
        }

        if (string.Equals(islem.Durum, PosOdemeDurumlari.Unknown, StringComparison.OrdinalIgnoreCase))
        {
            return await GetResultAsync(cihazId, islem.Id, requestedBy, cancellationToken);
        }

        if (IsFinalPaymentState(islem.Durum) || IsInFlightPaymentState(islem.Durum))
        {
            await EnsureTerminalLoadedAsync(islem, cancellationToken);
            return ToDto(islem, islem.PosTerminal?.SaglayiciKodu);
        }

        var command = BuildStartCommand(cihaz, terminal, islem, request, now);
        var payload = JsonSerializer.Serialize(command, JsonOptions);

        AgentCommandDto sentCommand;

        try
        {
            sentCommand = await _agentCommandService.SendAsync(new STYS.Agent.Contracts.Dtos.AgentCommandSendRequest
            {
                AgentId = cihaz.AgentId!.Value,
                CommandType = "PavoStartPayment",
                Payload = payload,
                Priority = 1,
                ExpirationMinutes = 10,
                MaxRetryCount = 3
            }, requestedBy, cancellationToken);
        }
        catch (Exception ex)
        {
            islem.Durum = PosOdemeDurumlari.Unknown;
            islem.HataMesaji = Truncate(ex.Message, 1024);
            islem.PavoMessage = Truncate(ex.Message, 1024);
            await _db.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        islem.AgentCommandId = sentCommand.Id;
        islem.Durum = PosOdemeDurumlari.SentToAgent;
        await _db.SaveChangesAsync(cancellationToken);
        await EnsureTerminalLoadedAsync(islem, cancellationToken);
        return ToDto(islem, islem.PosTerminal?.SaglayiciKodu);
    }

    public async Task<PosOdemeIslemiDto> GetResultAsync(int cihazId, int posOdemeIslemiId, string requestedBy, CancellationToken cancellationToken)
    {
        var cihaz = await GetDeviceAsync(cihazId, cancellationToken);
        _ = await GetValidatedAgentAsync(cihaz, cancellationToken);
        var payment = await _db.PosOdemeIslemleri
            .Include(x => x.PosTerminal)
            .FirstOrDefaultAsync(x => x.Id == posOdemeIslemiId, cancellationToken)
            ?? throw new BaseException("POS ödeme işlemi bulunamadı.", 404);

        if (payment.PosCihaziId != cihaz.Id || payment.KurumId != cihaz.KurumId || payment.TesisId != cihaz.TesisId)
        {
            throw new BaseException("Ödeme işlemi seçilen cihaz kapsamıyla eşleşmiyor.", 400);
        }

        if (payment.PosTerminalId == 0)
        {
            throw new BaseException("Ödeme işlemi terminal bilgisi eksik.", 400);
        }

        if (string.IsNullOrWhiteSpace(payment.SaleReference))
        {
            throw new BaseException("SaleReference bulunamadı.", 400);
        }

        if (IsFinalPaymentState(payment.Durum))
        {
            await EnsureTerminalLoadedAsync(payment, cancellationToken);
            return ToDto(payment, payment.PosTerminal?.SaglayiciKodu);
        }

        var command = BuildResultCommand(cihaz, payment);
        var payload = JsonSerializer.Serialize(command, JsonOptions);
        var sentCommand = await _agentCommandService.SendAsync(new STYS.Agent.Contracts.Dtos.AgentCommandSendRequest
        {
            AgentId = cihaz.AgentId!.Value,
            CommandType = "PavoGetPaymentResult",
            Payload = payload,
            Priority = 1,
            ExpirationMinutes = 10,
            MaxRetryCount = 3
        }, requestedBy, cancellationToken);

        payment.Durum = PosOdemeDurumlari.Processing;
        await _db.SaveChangesAsync(cancellationToken);
        await EnsureTerminalLoadedAsync(payment, cancellationToken);
        return ToDto(payment, payment.PosTerminal?.SaglayiciKodu);
    }

    private async Task<PosOperationalReadinessDto> GetReadinessAsync(PosCihazi cihaz, CancellationToken cancellationToken)
    {
        var agent = cihaz.AgentId.HasValue
            ? await _db.Set<AgentEntity>().FirstOrDefaultAsync(x => x.Id == cihaz.AgentId.Value && !x.IsDeleted, cancellationToken)
            : null;

        var terminals = await _db.PosTerminaller
            .AsNoTracking()
            .Where(x => x.PosCihaziId == cihaz.Id && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        return PosOperationalReadinessEvaluator.Evaluate(cihaz, agent, terminals, DateTime.UtcNow);
    }

    private async Task<PosOdemeIslemi?> LoadExistingPaymentForStartAsync(PosPaymentBaslatRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (request.PosOdemeIslemiId.HasValue)
        {
            var byId = await _db.PosOdemeIslemleri
                .Include(x => x.PosTerminal)
                .FirstOrDefaultAsync(x => x.Id == request.PosOdemeIslemiId.Value, cancellationToken)
                ?? throw new BaseException("POS ödeme işlemi bulunamadı.", 404);

            if (!string.IsNullOrWhiteSpace(byId.IdempotencyKey) && !string.Equals(byId.IdempotencyKey, idempotencyKey, StringComparison.OrdinalIgnoreCase))
            {
                throw new BaseException("Idempotency key başka bir ödeme işlemiyle eşleşiyor.", 409);
            }

            return byId;
        }

        return await _db.PosOdemeIslemleri
            .Include(x => x.PosTerminal)
            .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey && !x.IsDeleted, cancellationToken);
    }

    private static void ValidateExistingPaymentForStart(
        PosOdemeIslemi payment,
        PosCihazi cihaz,
        PosTerminal terminal,
        KasaBankaHesap hesap,
        PosPaymentBaslatRequest request,
        string idempotencyKey)
    {
        if (!string.IsNullOrWhiteSpace(payment.IdempotencyKey) && !string.Equals(payment.IdempotencyKey, idempotencyKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new BaseException("Idempotency key başka bir ödeme işlemiyle eşleşiyor.", 409);
        }

        if (payment.PosCihaziId != cihaz.Id || payment.PosTerminalId != terminal.Id || payment.KurumId != cihaz.KurumId || payment.TesisId != cihaz.TesisId)
        {
            throw new BaseException("Ödeme işlemi seçilen cihaz/terminal kapsamıyla eşleşmiyor.", 400);
        }

        if (payment.KasaBankaHesapId != hesap.Id)
        {
            throw new BaseException("Ödeme işlemi farklı bir kredi kartı hesabına bağlı.", 400);
        }

        if (!string.Equals(payment.ParaBirimi, NormalizeCurrency(request.ParaBirimi), StringComparison.OrdinalIgnoreCase))
        {
            throw new BaseException("Aynı ödeme için para birimi değiştirilemez.", 400);
        }

        if (payment.Tutar != request.Tutar)
        {
            throw new BaseException("Aynı ödeme için tutar değiştirilemez.", 400);
        }
    }

    private async Task EnsureTerminalLoadedAsync(PosOdemeIslemi payment, CancellationToken cancellationToken)
    {
        if (payment.PosTerminal is null)
        {
            await _db.Entry(payment).Reference(x => x.PosTerminal).LoadAsync(cancellationToken);
        }
    }

    private async Task<PosCihazi> GetDeviceAsync(int cihazId, CancellationToken cancellationToken)
    {
        var cihaz = await _db.PosCihazlari.FirstOrDefaultAsync(x => x.Id == cihazId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("POS cihazı bulunamadı.", 404);

        EnforceKurum(cihaz.KurumId);
        if (!cihaz.AktifMi)
        {
            throw new BaseException("POS cihazı pasif olduğu için ödeme başlatılamaz.", 400);
        }

        if (!cihaz.AgentId.HasValue)
        {
            throw new BaseException("Bu POS cihazına atanmış agent bulunamadı.", 400);
        }

        return cihaz;
    }

    private async Task<PosTerminal> GetTerminalAsync(int terminalId, CancellationToken cancellationToken)
    {
        var terminal = await _db.PosTerminaller.FirstOrDefaultAsync(x => x.Id == terminalId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("POS terminali bulunamadı.", 404);

        if (!terminal.AktifMi)
        {
            throw new BaseException("POS terminali pasif.", 400);
        }

        return terminal;
    }

    private async Task<AgentEntity> GetValidatedAgentAsync(PosCihazi cihaz, CancellationToken cancellationToken)
    {
        var agent = await _db.Set<AgentEntity>().FirstOrDefaultAsync(x => x.Id == cihaz.AgentId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("POS cihazına bağlı agent bulunamadı.", 404);

        if (agent.KurumId != cihaz.KurumId)
        {
            throw new BaseException("POS cihazı ile agent aynı kurumda değil.", 400);
        }

        if (agent.Durum != STYS.Agent.Contracts.Enums.AgentDurum.Active)
        {
            throw new BaseException("POS cihazına bağlı agent aktif değil.", 400);
        }

        var agentTesisBaglantisiVarMi = await _db.Set<STYS.Agent.Entities.AgentTesis>().AnyAsync(x =>
            x.AgentId == agent.Id
            && x.KurumId == cihaz.KurumId
            && x.TesisId == cihaz.TesisId
            && x.AktifMi
            && !x.IsDeleted, cancellationToken);

        if (!agentTesisBaglantisiVarMi)
        {
            throw new BaseException("Agent seçilen tesis kapsamında değil.", 400);
        }

        return agent;
    }

    private async Task<KasaBankaHesap> GetValidatedAccountAsync(PosTerminal terminal, CancellationToken cancellationToken)
    {
        if (!terminal.KasaBankaHesapId.HasValue)
        {
            throw new BaseException("Bu terminal için kredi kartı hesabı eşleştirilmemiş.", 400);
        }

        var hesap = await _db.Set<KasaBankaHesap>().FirstOrDefaultAsync(x =>
            x.Id == terminal.KasaBankaHesapId.Value
            && x.AktifMi
            && x.Tip == KasaBankaHesapTipleri.KrediKarti, cancellationToken)
            ?? throw new BaseException("Aktif kredi kartı/POS hesabı bulunamadı.", 404);

        if (!hesap.TesisId.HasValue || hesap.TesisId.Value != terminal.TesisId)
        {
            throw new BaseException("POS terminali ile kredi kartı hesabı aynı tesise ait olmalıdır.", 400);
        }

        return hesap;
    }

    private async Task<int> ResolveReservationIdAsync(int tesisId, CancellationToken cancellationToken)
    {
        var rezervasyonId = await _db.Set<Rezervasyon>()
            .AsNoTracking()
            .Where(x => x.TesisId == tesisId && x.AktifMi && !x.IsDeleted)
            .OrderByDescending(x => x.Id)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (rezervasyonId <= 0)
        {
            throw new BaseException("POS ödeme testi için uygun rezervasyon bulunamadı.", 400);
        }

        return rezervasyonId;
    }

    private void EnsureTerminalBelongsToDevice(PosCihazi cihaz, PosTerminal terminal)
    {
        if (terminal.KurumId != cihaz.KurumId || terminal.TesisId != cihaz.TesisId)
        {
            throw new BaseException("POS terminali cihazın kurum ve tesis kapsamıyla eşleşmiyor.", 400);
        }

        if (!terminal.PosCihaziId.HasValue || terminal.PosCihaziId.Value != cihaz.Id)
        {
            throw new BaseException("POS terminali başka bir cihaza bağlı.", 400);
        }
    }

    private static PavoStartPaymentRequest BuildStartCommand(
        PosCihazi cihaz,
        PosTerminal terminal,
        PosOdemeIslemi islem,
        PosPaymentBaslatRequest request,
        DateTime now)
    {
        return new PavoStartPaymentRequest
        {
            PosCihaziId = cihaz.Id,
            PosOdemeIslemiId = islem.Id,
            PosTerminalId = terminal.Id,
            SaleReference = islem.SaleReference ?? GenerateSaleReference(cihaz.Id, terminal.Id, now),
            Amount = request.Tutar,
            CurrencyCode = NormalizeCurrency(request.ParaBirimi),
            Description = Normalize(request.Aciklama),
            IpAddress = cihaz.IpAdresi ?? string.Empty,
            HttpPort = cihaz.HttpPort,
            HttpsPort = cihaz.HttpsPort,
            UseHttps = cihaz.HttpsPort.HasValue,
            TransactionHandle = new PavoTransactionHandle
            {
                SerialNumber = cihaz.SeriNo,
                Fingerprint = cihaz.Fingerprint ?? string.Empty,
                TransactionSequence = 0,
                TransactionDate = now
            }
        };
    }

    private static PavoGetPaymentResultRequest BuildResultCommand(PosCihazi cihaz, PosOdemeIslemi payment)
    {
        return new PavoGetPaymentResultRequest
        {
            PosCihaziId = cihaz.Id,
            PosOdemeIslemiId = payment.Id,
            PosTerminalId = payment.PosTerminalId,
            SaleReference = payment.SaleReference ?? string.Empty,
            IpAddress = cihaz.IpAdresi ?? string.Empty,
            HttpPort = cihaz.HttpPort,
            HttpsPort = cihaz.HttpsPort,
            UseHttps = cihaz.HttpsPort.HasValue,
            TransactionHandle = new PavoTransactionHandle
            {
                SerialNumber = cihaz.SeriNo,
                Fingerprint = cihaz.Fingerprint ?? string.Empty,
                TransactionSequence = 0,
                TransactionDate = DateTime.UtcNow
            }
        };
    }

    private void EnforceKurum(int kurumId)
    {
        if (!_tenantAccessor.IsSuperAdmin() && !_tenantAccessor.GetAccessibleKurumIds().Contains(kurumId))
        {
            throw new BaseException("Bu kuruma erişim yetkiniz yok.", 403);
        }
    }

    private static string GenerateSaleReference(int cihazId, int terminalId, DateTime now)
    {
        var value = $"STYS-PAY-{now:yyyyMMddHHmmssfff}-{cihazId}-{terminalId}-{Guid.NewGuid():N}";
        return value.Length <= 96 ? value : value[..96];
    }

    private static bool IsFinalPaymentState(string? durum) =>
        string.Equals(durum, PosOdemeDurumlari.Successful, StringComparison.OrdinalIgnoreCase)
        || string.Equals(durum, PosOdemeDurumlari.Failed, StringComparison.OrdinalIgnoreCase)
        || string.Equals(durum, PosOdemeDurumlari.Unknown, StringComparison.OrdinalIgnoreCase)
        || string.Equals(durum, PosOdemeDurumlari.Cancelled, StringComparison.OrdinalIgnoreCase);

    private static bool IsInFlightPaymentState(string? durum) =>
        string.Equals(durum, PosOdemeDurumlari.Pending, StringComparison.OrdinalIgnoreCase)
        || string.Equals(durum, PosOdemeDurumlari.SentToAgent, StringComparison.OrdinalIgnoreCase)
        || string.Equals(durum, PosOdemeDurumlari.Processing, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? "TRY" : currency.Trim().ToUpperInvariant();

    private static string? NormalizeIdempotencyKey(string? value)
    {
        var normalized = Normalize(value);
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("Cannot insert duplicate key", StringComparison.OrdinalIgnoreCase) == true;

    private static PosOdemeIslemiDto ToDto(PosOdemeIslemi x, string? saglayiciKodu) => new()
    {
        Id = x.Id,
        PosCihaziId = x.PosCihaziId,
        RezervasyonId = x.RezervasyonId,
        PosTerminalId = x.PosTerminalId,
        KasaBankaHesapId = x.KasaBankaHesapId,
        AgentCommandId = x.AgentCommandId,
        SaglayiciKodu = saglayiciKodu ?? string.Empty,
        SaglayiciIslemId = x.SaglayiciIslemId,
        SaglayiciDurumKodu = x.SaglayiciDurumKodu,
        IslemReferansi = x.IslemReferansi,
        SaleReference = x.SaleReference,
        Tutar = x.Tutar,
        ParaBirimi = x.ParaBirimi,
        Durum = x.Durum,
        PavoResultCode = x.PavoResultCode,
        PavoMessage = x.PavoMessage,
        HataMesaji = x.HataMesaji,
        AcquirerId = x.AcquirerId,
        TerminalId = x.TerminalId,
        MerchantId = x.MerchantId,
        RetrievalReferenceNo = x.RetrievalReferenceNo,
        AcquirerReference = x.AcquirerReference,
        AuthorizationCode = x.AuthorizationCode,
        BaslatilmaTarihi = x.BaslatilmaTarihi,
        TamamlanmaTarihi = x.TamamlanmaTarihi,
        SonSorgulamaTarihi = x.SonSorgulamaTarihi,
        SorgulamaDenemeSayisi = x.SorgulamaDenemeSayisi,
        RezervasyonOdemeId = x.RezervasyonOdemeId,
        TamamlandiMi = string.Equals(x.Durum, PosOdemeDurumlari.Muhasebelestirildi, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Durum, PosOdemeDurumlari.Basarili, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Durum, PosOdemeDurumlari.Basarisiz, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Durum, PosOdemeDurumlari.MutabakatGerekli, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Durum, PosOdemeDurumlari.Successful, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Durum, PosOdemeDurumlari.Failed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Durum, PosOdemeDurumlari.Unknown, StringComparison.OrdinalIgnoreCase)
    };
}
