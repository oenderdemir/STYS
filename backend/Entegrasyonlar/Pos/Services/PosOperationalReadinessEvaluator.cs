using STYS.Agent.Entities;
using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Entegrasyonlar.Pos.Options;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Entegrasyonlar.Pos.Services;

internal static class PosOperationalReadinessEvaluator
{
    private static readonly TimeSpan AgentOfflineThreshold = TimeSpan.FromSeconds(90);

    public static PosOperationalReadinessDto Evaluate(
        PosCihazi cihaz,
        AgentEntity? agent,
        IReadOnlyCollection<PosTerminal> terminals,
        PosOperationalHealthOptions healthOptions,
        DateTime utcNow)
    {
        var activeTerminals = (terminals ?? [])
            .Where(x => !x.IsDeleted && x.AktifMi)
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .ToList();

        var readyTerminals = activeTerminals.Where(x => x.KasaBankaHesapId.HasValue).ToList();
        var hasActiveTerminal = activeTerminals.Count > 0;
        var hasAccountMapping = readyTerminals.Count > 0;

        var agentOnline = agent is not null
            && agent.Durum == STYS.Agent.Contracts.Enums.AgentDurum.Active
            && agent.LastHeartbeatAt.HasValue
            && utcNow - agent.LastHeartbeatAt.Value <= AgentOfflineThreshold;

        var deviceHealth = ResolveDeviceHealth(cihaz, healthOptions.FreshnessThreshold, utcNow, out var lastHealthCheckAt, out var lastHealthSuccessAt, out var healthReason);
        var deviceOnline = cihaz.AktifMi && deviceHealth == PavoDeviceHealthStatus.Healthy;

        var provisioned = cihaz.AgentId.HasValue
            && !string.IsNullOrWhiteSpace(cihaz.AgentLocalDeviceId)
            && cihaz.EslesmeOnayliMi;

        var pairingValid = provisioned
            && cihaz.EslesmeOnayliMi
            && !string.IsNullOrWhiteSpace(cihaz.Fingerprint);

        var reasons = new List<string>();
        if (!string.IsNullOrWhiteSpace(healthReason))
        {
            reasons.Add(healthReason);
        }

        var status = PavoOperationalReadiness.Ready;

        if (!cihaz.AktifMi)
        {
            status = PavoOperationalReadiness.Disabled;
            reasons.Add("POS cihazı devre dışı.");
        }
        else if (cihaz.AgentId.HasValue && agent is null)
        {
            status = PavoOperationalReadiness.OwnershipConflict;
            reasons.Add("Cihaza bağlı agent bulunamadı.");
        }
        else if (!agentOnline)
        {
            status = PavoOperationalReadiness.AgentOffline;
            reasons.Add(agent is null
                ? "Agent bilgisi bulunamadı."
                : "Agent çevrimdışı.");
        }
        else if (!cihaz.AgentId.HasValue || string.IsNullOrWhiteSpace(cihaz.AgentLocalDeviceId))
        {
            status = PavoOperationalReadiness.NotProvisioned;
            reasons.Add("Cihaz henüz provision edilmemiş.");
        }
        else if (!cihaz.EslesmeOnayliMi || string.IsNullOrWhiteSpace(cihaz.Fingerprint))
        {
            status = PavoOperationalReadiness.PairingInvalid;
            reasons.Add("Pairing geçersiz.");
        }
        else if (deviceHealth is not PavoDeviceHealthStatus.Healthy)
        {
            status = PavoOperationalReadiness.DeviceOffline;
            reasons.Add(GetDeviceHealthReason(deviceHealth));
        }
        else if (!hasActiveTerminal)
        {
            status = PavoOperationalReadiness.NoActiveTerminal;
            reasons.Add("Aktif terminal bulunamadı.");
        }
        else if (!hasAccountMapping)
        {
            status = PavoOperationalReadiness.NoAccountMapping;
            reasons.Add("Aktif terminal için kredi kartı hesabı eşleştirilmemiş.");
        }

        var terminalReadiness = activeTerminals.Select(terminal =>
        {
            var accountMapped = terminal.KasaBankaHesapId.HasValue;
            var paymentReady = status == PavoOperationalReadiness.Ready && accountMapped && deviceHealth == PavoDeviceHealthStatus.Healthy;
            return new PosTerminalOperationalReadinessDto
            {
                Id = terminal.Id,
                TerminalId = terminal.SerialNumber,
                AcquirerId = terminal.AcquirerId,
                AcquirerName = terminal.AcquirerName,
                Active = terminal.AktifMi,
                KasaBankaHesapId = terminal.KasaBankaHesapId,
                AccountMapped = accountMapped,
                PaymentReady = paymentReady,
                StatusMessage = paymentReady
                    ? null
                    : status switch
                    {
                        PavoOperationalReadiness.Ready => null,
                        PavoOperationalReadiness.NoAccountMapping when !accountMapped => "Hesap eşleştirilmedi.",
                        PavoOperationalReadiness.NoActiveTerminal => "Aktif terminal bulunamadı.",
                        PavoOperationalReadiness.AgentOffline => "Agent çevrimdışı.",
                        PavoOperationalReadiness.DeviceOffline => "PAVO cihazı çevrimdışı.",
                        PavoOperationalReadiness.Disabled => "Cihaz devre dışı.",
                        PavoOperationalReadiness.NotProvisioned => "Cihaz provision edilmemiş.",
                        PavoOperationalReadiness.PairingInvalid => "Pairing geçersiz.",
                        PavoOperationalReadiness.ReProvisionRequired => "Cihaz yeniden eşitlenmeli.",
                        PavoOperationalReadiness.OwnershipConflict => "Sahiplik çakışması.",
                        _ => "PAVO cihazı ödeme için hazır değil."
                    }
            };
        }).ToList();

        return new PosOperationalReadinessDto
        {
            PosCihaziId = cihaz.Id,
            Status = status,
            DeviceHealthStatus = deviceHealth,
            AgentOnline = agentOnline,
            DeviceOnline = deviceOnline,
            Provisioned = provisioned,
            InSync = status == PavoOperationalReadiness.Ready,
            PairingValid = pairingValid,
            HasActiveTerminal = hasActiveTerminal,
            HasAccountMapping = hasAccountMapping,
            Disabled = status == PavoOperationalReadiness.Disabled,
            OwnershipConflict = status == PavoOperationalReadiness.OwnershipConflict,
            AgentLastHeartbeatAt = agent?.LastHeartbeatAt,
            DeviceLastConnectionAt = lastHealthSuccessAt ?? cihaz.SonBaglantiTarihi,
            LastHealthCheckAt = lastHealthCheckAt,
            LastHealthSuccessAt = lastHealthSuccessAt,
            LastHealthStatus = deviceHealth.ToString(),
            LastError = reasons.FirstOrDefault(),
            ActiveTerminalCount = activeTerminals.Count,
            AccountMappedTerminalCount = readyTerminals.Count,
            Terminals = terminalReadiness,
            Reasons = reasons
        };
    }

    private static PavoDeviceHealthStatus ResolveDeviceHealth(
        PosCihazi cihaz,
        TimeSpan freshnessThreshold,
        DateTime utcNow,
        out DateTime? lastHealthCheckAt,
        out DateTime? lastHealthSuccessAt,
        out string? reason)
    {
        lastHealthCheckAt = cihaz.LastHealthCheckAt;
        lastHealthSuccessAt = cihaz.LastHealthSuccessAt;
        var lastHealthStatus = cihaz.LastHealthStatus;
        reason = null;

        var hasModernHealthState = lastHealthCheckAt.HasValue || lastHealthSuccessAt.HasValue || lastHealthStatus != PavoDeviceHealthStatus.Unknown;
        if (!hasModernHealthState && cihaz.SonBaglantiTarihi.HasValue)
        {
            lastHealthCheckAt = cihaz.SonBaglantiTarihi;
            lastHealthSuccessAt = cihaz.SonBaglantiTarihi;
            lastHealthStatus = PavoDeviceHealthStatus.Healthy;
        }

        if (!lastHealthCheckAt.HasValue && !lastHealthSuccessAt.HasValue && lastHealthStatus == PavoDeviceHealthStatus.Unknown)
        {
            reason = "PAVO sağlık kontrolü henüz yapılmadı.";
            return PavoDeviceHealthStatus.Unknown;
        }

        if (lastHealthStatus == PavoDeviceHealthStatus.Healthy)
        {
            if (lastHealthSuccessAt.HasValue && utcNow - lastHealthSuccessAt.Value <= freshnessThreshold)
            {
                return PavoDeviceHealthStatus.Healthy;
            }

            reason = "Son başarılı PAVO sağlık kontrolü eski.";
            return PavoDeviceHealthStatus.Stale;
        }

        if (lastHealthStatus == PavoDeviceHealthStatus.Unknown)
        {
            reason = "PAVO sağlık kontrolü sonucu bilinmiyor.";
            return PavoDeviceHealthStatus.Unknown;
        }

        reason = GetDeviceHealthReason(lastHealthStatus);
        return lastHealthStatus;
    }

    private static string GetDeviceHealthReason(PavoDeviceHealthStatus status) =>
        status switch
        {
            PavoDeviceHealthStatus.Healthy => "PAVO cihazı sağlıklı.",
            PavoDeviceHealthStatus.Unreachable => "PAVO cihazına ulaşılamıyor.",
            PavoDeviceHealthStatus.Timeout => "PAVO sağlık kontrolü zaman aşımına uğradı.",
            PavoDeviceHealthStatus.TlsError => "PAVO TLS doğrulaması başarısız oldu.",
            PavoDeviceHealthStatus.ProtocolError => "PAVO protokol yanıtı geçersiz.",
            PavoDeviceHealthStatus.Stale => "Son başarılı PAVO sağlık kontrolü eski.",
            _ => "PAVO sağlık durumu bilinmiyor."
        };
}
