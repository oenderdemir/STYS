using STYS.Entegrasyonlar.Pos.Entities;

namespace STYS.Entegrasyonlar.Pos.Services;

public interface IPosOdemeSaglayicisi
{
    string Kod { get; }
    string Ad { get; }
    bool EslesmeDestekliyorMu { get; }

    void TerminalBilgileriniDogrula(PosTerminal terminal);
    Task<PosEslesmeSonucu> EslesmeBaslatAsync(PosTerminal terminal, CancellationToken cancellationToken);
    Task<PosEslesmeSonucu> EslesmeKontrolAsync(PosTerminal terminal, CancellationToken cancellationToken);
    Task<PosOdemeBaslatSonucu> OdemeBaslatAsync(
        PosTerminal terminal,
        string islemReferansi,
        decimal tutar,
        string paraBirimi,
        CancellationToken cancellationToken);
    Task<PosOdemeSorguSonucu> OdemeDurumuAsync(
        PosTerminal terminal,
        string saglayiciIslemId,
        string islemReferansi,
        CancellationToken cancellationToken);
}

public sealed record PosEslesmeSonucu(
    long? PairingId,
    string? PairingCode,
    string? TargetFingerprint,
    bool OnayliMi);

public sealed record PosOdemeBaslatSonucu(
    string SaglayiciIslemId,
    string? SaglayiciDurumKodu,
    string HamYanit);

public sealed record PosOdemeSorguSonucu(
    string? SaglayiciDurumKodu,
    bool Bekliyor,
    bool Basarili,
    string HamYanit,
    string? HataMesaji,
    string? RetrievalReferenceNo,
    string? AcquirerReference,
    string? AuthorizationCode);
