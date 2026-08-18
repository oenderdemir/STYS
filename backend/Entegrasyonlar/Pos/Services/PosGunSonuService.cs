using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Entities;
using STYS.Agent.Services;
using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Services;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Entegrasyonlar.Pos.Services;

public interface IPosGunSonuService
{
    Task<PosGunSonuIslemiDto> PerformAsync(int cihazId, PosGunSonuBaslatRequest request, string requestedBy, CancellationToken ct);
    Task<IReadOnlyCollection<PosGunSonuIslemiDto>> GetRecentAsync(int cihazId, int take, CancellationToken ct);
    Task<PosGunSonuIslemiDetayDto> GetByIdAsync(int eodId, CancellationToken ct);
    Task<IReadOnlyCollection<PosGunSonuSlipiDto>> GetSliplerAsync(int eodId, CancellationToken ct);
    Task<PosGunSonuSlipContent> OpenSlipContentAsync(int eodId, int receiptId, CancellationToken ct);
}

public sealed class PosGunSonuService : IPosGunSonuService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly AgentCommandStatus[] ActiveCommandStatuses =
    [
        AgentCommandStatus.Pending, AgentCommandStatus.Delivered, AgentCommandStatus.Accepted, AgentCommandStatus.Running
    ];

    private readonly StysAppDbContext _db;
    private readonly AgentCommandService _agentCommandService;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IMuhasebeTesisScopeService _tesisScopeService;
    private readonly IPosGunSonuSlipStorage _slipStorage;

    public PosGunSonuService(
        StysAppDbContext db,
        AgentCommandService agentCommandService,
        ICurrentTenantAccessor tenantAccessor,
        IMuhasebeTesisScopeService tesisScopeService,
        IPosGunSonuSlipStorage slipStorage)
    {
        _db = db;
        _agentCommandService = agentCommandService;
        _tenantAccessor = tenantAccessor;
        _tesisScopeService = tesisScopeService;
        _slipStorage = slipStorage;
    }

    public async Task<PosGunSonuIslemiDto> PerformAsync(int cihazId, PosGunSonuBaslatRequest request, string requestedBy, CancellationToken ct)
    {
        var cihaz = await GetDeviceAsync(cihazId, ct);
        var agent = await GetValidatedAgentAsync(cihaz, ct);
        await _tesisScopeService.EnsureCanAccessTesisAsync(cihaz.TesisId, ct);

        if (cihaz.Saglayici != PosSaglayici.Pavo)
        {
            throw new BaseException("Bu cihaz PAVO sağlayıcısı değil.", 400);
        }

        if (!cihaz.EslesmeOnayliMi)
        {
            throw new BaseException("Cihaz eşleşmesi onaylanmamış; gün sonu başlatılamaz.", 400);
        }

        if (await HasUnresolvedPaymentAsync(cihaz.Id, ct))
        {
            throw new BaseException("Bu POS cihazında sonucu kesinleşmemiş ödeme işlemleri bulunmaktadır. Önce ödeme işlemlerini sonuçlandırın.", 409);
        }

        if (await HasActiveApplyUpgradeAsync(agent.Id, ct))
        {
            throw new BaseException("Agent üzerinde devam eden bir yükseltme işlemi bulunmaktadır.", 409);
        }

        var now = DateTime.UtcNow;
        PosGunSonuIslemi eod;
        await using (var tx = await BeginTransactionAsync(ct))
        {
            await AcquireEodLockAsync(_db, cihaz.Id, ct);
            if (await HasActiveEodAsync(cihaz.Id, agent.Id, ct))
            {
                throw new BaseException("Bu cihazda zaten aktif bir gün sonu işlemi var.", 409);
            }

            eod = new PosGunSonuIslemi
            {
                KurumId = cihaz.KurumId,
                TesisId = cihaz.TesisId,
                PosCihaziId = cihaz.Id,
                UseSummary = request.UseSummary,
                Print = request.Print,
                Durum = PosGunSonuDurumu.Pending,
                BaslatilmaTarihi = now,
                RequestedBy = requestedBy,
                CreatedBy = requestedBy,
                CreatedAt = now
            };
            _db.PosGunSonuIslemleri.Add(eod);
            await _db.SaveChangesAsync(ct);
            if (tx is not null)
            {
                await tx.CommitAsync(ct);
            }
        }

        try
        {
            var payload = BuildCommandPayload(cihaz, request, eod.Id, now);
            var sentCommand = await _agentCommandService.SendAsync(new AgentCommandSendRequest
            {
                AgentId = agent.Id,
                CommandType = "PavoPerformEOD",
                Payload = payload,
                // No automatic retry: PerformEOD is state-changing; the agent durable store already
                // guards against duplicate physical execution.
                Priority = 1,
                ExpirationMinutes = 10,
                MaxRetryCount = 0
            }, requestedBy, ct);

            eod.AgentCommandId = sentCommand.Id;
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            eod.Durum = PosGunSonuDurumu.Failed;
            eod.PavoErrorCode = "EOD_COMMAND_SEND_FAILED";
            eod.PavoMessage = "Gün sonu komutu gönderilemedi.";
            eod.TamamlanmaTarihi = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw;
        }

        return ToDto(eod, cihaz.Ad);
    }

    public async Task<IReadOnlyCollection<PosGunSonuIslemiDto>> GetRecentAsync(int cihazId, int take, CancellationToken ct)
    {
        var cihaz = await GetDeviceAsync(cihazId, ct);

        var rows = await _db.PosGunSonuIslemleri
            .AsNoTracking()
            .Where(x => x.PosCihaziId == cihaz.Id && !x.IsDeleted)
            .OrderByDescending(x => x.BaslatilmaTarihi)
            .Take(Math.Clamp(take, 1, 50))
            .ToListAsync(ct);

        var slipCounts = await _db.PosGunSonuSlipleri
            .AsNoTracking()
            .Where(x => rows.Select(r => r.Id).Contains(x.PosGunSonuIslemiId) && !x.IsDeleted)
            .GroupBy(x => x.PosGunSonuIslemiId)
            .Select(g => new { PosGunSonuIslemiId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var countMap = slipCounts.ToDictionary(x => x.PosGunSonuIslemiId, x => x.Count);
        return rows.Select(x => ToDto(x, cihaz.Ad, countMap.GetValueOrDefault(x.Id))).ToList();
    }

    public async Task<PosGunSonuIslemiDetayDto> GetByIdAsync(int eodId, CancellationToken ct)
    {
        var eod = await LoadValidatedEodAsync(eodId, ct);
        var slipler = await _db.PosGunSonuSlipleri.AsNoTracking().Where(x => x.PosGunSonuIslemiId == eod.Id && !x.IsDeleted).OrderBy(x => x.SlipTipi).ToListAsync(ct);
        var dto = ToDto(eod, eod.PosCihazi?.Ad ?? string.Empty, slipler.Count);
        return new PosGunSonuIslemiDetayDto
        {
            Id = dto.Id,
            PosCihaziId = dto.PosCihaziId,
            PosCihaziAd = dto.PosCihaziAd,
            UseSummary = dto.UseSummary,
            Print = dto.Print,
            Durum = dto.Durum,
            DurumText = dto.DurumText,
            GunSonuMesaji = dto.GunSonuMesaji,
            BatchNo = dto.BatchNo,
            EodDateTime = dto.EodDateTime,
            PavoErrorCode = dto.PavoErrorCode,
            PavoMessage = dto.PavoMessage,
            BaslatilmaTarihi = dto.BaslatilmaTarihi,
            TamamlanmaTarihi = dto.TamamlanmaTarihi,
            RequestedBy = dto.RequestedBy,
            SlipSayisi = dto.SlipSayisi,
            Slipler = slipler.Select(ToSlipDto).ToList()
        };
    }

    public async Task<IReadOnlyCollection<PosGunSonuSlipiDto>> GetSliplerAsync(int eodId, CancellationToken ct)
    {
        var eod = await LoadValidatedEodAsync(eodId, ct);
        return await _db.PosGunSonuSlipleri.AsNoTracking()
            .Where(x => x.PosGunSonuIslemiId == eod.Id && !x.IsDeleted)
            .OrderBy(x => x.SlipTipi)
            .Select(x => new PosGunSonuSlipiDto
            {
                Id = x.Id,
                PosGunSonuIslemiId = x.PosGunSonuIslemiId,
                PosCihaziId = x.PosCihaziId,
                SlipTipi = (int)x.SlipTipi,
                SlipTipiText = SlipTipiText(x.SlipTipi),
                ContentType = x.ContentType,
                DosyaBoyutu = x.DosyaBoyutu,
                Sha256 = x.Sha256,
                OlusturulmaTarihi = x.OlusturulmaTarihi
            })
            .ToListAsync(ct);
    }

    public async Task<PosGunSonuSlipContent> OpenSlipContentAsync(int eodId, int receiptId, CancellationToken ct)
    {
        var eod = await LoadValidatedEodAsync(eodId, ct);
        var slip = await _db.PosGunSonuSlipleri.AsNoTracking().FirstOrDefaultAsync(x => x.Id == receiptId && !x.IsDeleted, ct)
            ?? throw new BaseException("Slip kaydı bulunamadı.", 404);

        if (slip.PosGunSonuIslemiId != eod.Id || slip.KurumId != eod.KurumId || slip.TesisId != eod.TesisId || slip.PosCihaziId != eod.PosCihaziId)
        {
            throw new BaseException("Slip bu gün sonu işlemine ait değil.", 404);
        }

        var stream = _slipStorage.OpenRead(slip.StoragePath);
        return new PosGunSonuSlipContent(stream, string.IsNullOrWhiteSpace(slip.ContentType) ? "image/png" : slip.ContentType);
    }

    // --------------------------------------- helpers ---------------------------------------

    private async Task<PosCihazi> GetDeviceAsync(int cihazId, CancellationToken ct)
    {
        var cihaz = await _db.PosCihazlari.FirstOrDefaultAsync(x => x.Id == cihazId && !x.IsDeleted, ct)
            ?? throw new BaseException("POS cihazı bulunamadı.", 404);

        if (!_tenantAccessor.IsSuperAdmin() && !_tenantAccessor.GetAccessibleKurumIds().Contains(cihaz.KurumId))
        {
            throw new BaseException("Bu kuruma erişim yetkiniz yok.", 403);
        }

        if (!cihaz.AktifMi)
        {
            throw new BaseException("POS cihazı pasif olduğu için gün sonu başlatılamaz.", 400);
        }

        if (!cihaz.AgentId.HasValue)
        {
            throw new BaseException("Bu POS cihazına atanmış agent bulunamadı.", 400);
        }

        return cihaz;
    }

    private async Task<AgentEntity> GetValidatedAgentAsync(PosCihazi cihaz, CancellationToken ct)
    {
        var agent = await _db.Set<AgentEntity>().FirstOrDefaultAsync(x => x.Id == cihaz.AgentId && !x.IsDeleted, ct)
            ?? throw new BaseException("POS cihazına bağlı agent bulunamadı.", 404);

        if (agent.KurumId != cihaz.KurumId)
        {
            throw new BaseException("POS cihazı ile agent aynı kurumda değil.", 400);
        }

        if (agent.Durum != AgentDurum.Active)
        {
            throw new BaseException("POS cihazına bağlı agent aktif değil.", 400);
        }

        return agent;
    }

    private async Task<bool> HasUnresolvedPaymentAsync(int cihazId, CancellationToken ct) =>
        await _db.PosOdemeIslemleri.AnyAsync(x =>
            x.PosCihaziId == cihazId && !x.IsDeleted
            && (x.Durum == PosOdemeDurumlari.Pending
                || x.Durum == PosOdemeDurumlari.SentToAgent
                || x.Durum == PosOdemeDurumlari.Processing
                || x.Durum == PosOdemeDurumlari.Unknown), ct);

    private async Task<bool> HasActiveEodAsync(int cihazId, int agentId, CancellationToken ct)
    {
        if (await _db.Set<AgentCommand>().AnyAsync(x =>
                x.AgentId == agentId && !x.IsDeleted && x.CommandType == "PavoPerformEOD" && ActiveCommandStatuses.Contains(x.Status), ct))
        {
            return true;
        }

        return await _db.PosGunSonuIslemleri.AnyAsync(x =>
            x.PosCihaziId == cihazId && !x.IsDeleted && x.Durum == PosGunSonuDurumu.Pending, ct);
    }

    private async Task<bool> HasActiveApplyUpgradeAsync(int agentId, CancellationToken ct) =>
        await _db.Set<AgentCommand>().AnyAsync(x =>
            x.AgentId == agentId && !x.IsDeleted && x.CommandType == "AgentApplyUpgrade" && ActiveCommandStatuses.Contains(x.Status), ct);

    private async Task<PosGunSonuIslemi> LoadValidatedEodAsync(int eodId, CancellationToken ct)
    {
        var eod = await _db.PosGunSonuIslemleri
            .AsNoTracking()
            .Include(x => x.PosCihazi)
            .FirstOrDefaultAsync(x => x.Id == eodId && !x.IsDeleted, ct)
            ?? throw new BaseException("Gün sonu işlemi bulunamadı.", 404);

        if (!_tenantAccessor.IsSuperAdmin() && !_tenantAccessor.GetAccessibleKurumIds().Contains(eod.KurumId))
        {
            throw new BaseException("Bu kuruma erişim yetkiniz yok.", 403);
        }

        await _tesisScopeService.EnsureCanAccessTesisAsync(eod.TesisId, ct);
        return eod;
    }

    private static string BuildCommandPayload(PosCihazi cihaz, PosGunSonuBaslatRequest request, int eodId, DateTime now)
    {
        var req = new PavoPerformEodRequest
        {
            PosCihaziId = cihaz.Id,
            IpAddress = cihaz.IpAdresi ?? string.Empty,
            HttpPort = cihaz.HttpPort,
            HttpsPort = cihaz.HttpsPort,
            UseHttps = cihaz.HttpsPort.HasValue,
            UseSummary = request.UseSummary,
            Print = request.Print,
            ReceiptImage = true,
            TransactionHandle = new PavoTransactionHandle
            {
                SerialNumber = cihaz.SeriNo,
                Fingerprint = cihaz.Fingerprint ?? string.Empty,
                TransactionSequence = 0,
                TransactionDate = now
            }
        };

        var node = JsonSerializer.SerializeToNode(req, JsonOptions)!.AsObject();
        node["posGunSonuIslemiId"] = eodId;
        return node.ToJsonString(JsonOptions);
    }

    private static async Task AcquireEodLockAsync(StysAppDbContext db, int cihazId, CancellationToken ct)
    {
        if (!db.Database.IsRelational())
        {
            return;
        }

        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                DECLARE @lockResult int;
                EXEC @lockResult = sp_getapplock
                    @Resource = @resource,
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = 10000;
                SELECT @lockResult;
                """;
            var resource = command.CreateParameter();
            resource.ParameterName = "@resource";
            resource.Value = $"pavo-eod:{cihazId}";
            command.Parameters.Add(resource);

            var result = await command.ExecuteScalarAsync(ct);
            if (result is null)
            {
                throw new InvalidOperationException("Gün sonu lock alınamadı.");
            }

            var code = Convert.ToInt32(result);
            if (code < 0)
            {
                throw new InvalidOperationException($"Gün sonu lock alınamadı. Code={code}");
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken ct) =>
        _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(ct) : null;

    private static PosGunSonuIslemiDto ToDto(PosGunSonuIslemi x, string cihazAd, int slipSayisi = 0) => new()
    {
        Id = x.Id,
        PosCihaziId = x.PosCihaziId,
        PosCihaziAd = cihazAd,
        UseSummary = x.UseSummary,
        Print = x.Print,
        Durum = (int)x.Durum,
        DurumText = DurumText(x.Durum),
        GunSonuMesaji = x.GunSonuMesaji,
        BatchNo = x.BatchNo,
        EodDateTime = x.EodDateTime,
        PavoErrorCode = x.PavoErrorCode,
        PavoMessage = x.PavoMessage,
        BaslatilmaTarihi = x.BaslatilmaTarihi,
        TamamlanmaTarihi = x.TamamlanmaTarihi,
        RequestedBy = x.RequestedBy,
        SlipSayisi = slipSayisi
    };

    private static PosGunSonuSlipiDto ToSlipDto(PosGunSonuSlipi x) => new()
    {
        Id = x.Id,
        PosGunSonuIslemiId = x.PosGunSonuIslemiId,
        PosCihaziId = x.PosCihaziId,
        SlipTipi = (int)x.SlipTipi,
        SlipTipiText = SlipTipiText(x.SlipTipi),
        ContentType = x.ContentType,
        DosyaBoyutu = x.DosyaBoyutu,
        Sha256 = x.Sha256,
        OlusturulmaTarihi = x.OlusturulmaTarihi
    };

    private static string DurumText(PosGunSonuDurumu durum) => durum switch
    {
        PosGunSonuDurumu.Pending => "Bekliyor",
        PosGunSonuDurumu.Successful => "Başarılı",
        PosGunSonuDurumu.Failed => "Başarısız",
        PosGunSonuDurumu.Unknown => "Doğrulanamadı",
        _ => "Bilinmiyor"
    };

    private static string SlipTipiText(PosGunSonuSlipTipi tip) => tip switch
    {
        PosGunSonuSlipTipi.EodImage => "Gün Sonu Görseli",
        _ => "Slip"
    };
}
