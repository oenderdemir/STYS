using STYS.Agent.Entities;
using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Entities;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Entegrasyonlar.Pos.Services;

internal static class PosOperationalReadinessEvaluator
{
    private static readonly TimeSpan AgentOfflineThreshold = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan DeviceOfflineThreshold = TimeSpan.FromMinutes(5);

    public static PosOperationalReadinessDto Evaluate(PosCihazi cihaz, AgentEntity? agent, IReadOnlyCollection<PosTerminal> terminals, DateTime utcNow)
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

        var deviceOnline = cihaz.AktifMi
            && cihaz.SonBaglantiTarihi.HasValue
            && utcNow - cihaz.SonBaglantiTarihi.Value <= DeviceOfflineThreshold;

        var provisioned = cihaz.AgentId.HasValue
            && !string.IsNullOrWhiteSpace(cihaz.AgentLocalDeviceId)
            && cihaz.EslesmeOnayliMi;

        var pairingValid = provisioned
            && !string.IsNullOrWhiteSpace(cihaz.Fingerprint)
            && !string.IsNullOrWhiteSpace(cihaz.TargetFingerprint);

        var fingerprintsMatch = !string.IsNullOrWhiteSpace(cihaz.Fingerprint)
            && !string.IsNullOrWhiteSpace(cihaz.TargetFingerprint)
            && string.Equals(Normalize(cihaz.Fingerprint), Normalize(cihaz.TargetFingerprint), StringComparison.OrdinalIgnoreCase);

        var reasons = new List<string>();
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
        else if (!cihaz.EslesmeOnayliMi || string.IsNullOrWhiteSpace(cihaz.Fingerprint) || string.IsNullOrWhiteSpace(cihaz.TargetFingerprint))
        {
            status = PavoOperationalReadiness.PairingInvalid;
            reasons.Add("Pairing geçersiz.");
        }
        else if (!fingerprintsMatch)
        {
            status = PavoOperationalReadiness.ReProvisionRequired;
            reasons.Add("Cihaz yeniden eşitlenmeli.");
        }
        else if (!deviceOnline)
        {
            status = PavoOperationalReadiness.DeviceOffline;
            reasons.Add("PAVO cihazı çevrimdışı.");
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
            var paymentReady = status == PavoOperationalReadiness.Ready && accountMapped;
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
            AgentOnline = agentOnline,
            DeviceOnline = deviceOnline,
            Provisioned = provisioned,
            InSync = status == PavoOperationalReadiness.Ready,
            PairingValid = pairingValid && fingerprintsMatch,
            HasActiveTerminal = hasActiveTerminal,
            HasAccountMapping = hasAccountMapping,
            Disabled = status == PavoOperationalReadiness.Disabled,
            OwnershipConflict = status == PavoOperationalReadiness.OwnershipConflict,
            AgentLastHeartbeatAt = agent?.LastHeartbeatAt,
            DeviceLastConnectionAt = cihaz.SonBaglantiTarihi,
            LastError = reasons.FirstOrDefault(),
            ActiveTerminalCount = activeTerminals.Count,
            AccountMappedTerminalCount = readyTerminals.Count,
            Terminals = terminalReadiness,
            Reasons = reasons
        };
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
