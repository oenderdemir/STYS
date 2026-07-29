using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Entegrasyonlar.Pos.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Entegrasyonlar.Pavo.Services;

public sealed class PavoPosOdemeSaglayicisi : IPosOdemeSaglayicisi
{
    private readonly IPavoUniCloudClient _client;

    public PavoPosOdemeSaglayicisi(IPavoUniCloudClient client)
    {
        _client = client;
    }

    public string Kod => "PAVO";
    public string Ad => "PAVO";
    public bool EslesmeDestekliyorMu => true;

    public void TerminalBilgileriniDogrula(PosTerminal terminal)
    {
        if (string.IsNullOrWhiteSpace(terminal.SourceFingerprint))
        {
            throw new BaseException("PAVO terminali icin fingerprint zorunludur.", 400);
        }
    }

    public async Task<PosEslesmeSonucu> EslesmeBaslatAsync(
        PosTerminal terminal,
        CancellationToken cancellationToken)
    {
        var result = await _client.PairingRequestAsync(terminal, cancellationToken);
        return new PosEslesmeSonucu(result.Id, result.PairingCode, result.TargetFingerprint, result.IsApproved);
    }

    public async Task<PosEslesmeSonucu> EslesmeKontrolAsync(
        PosTerminal terminal,
        CancellationToken cancellationToken)
    {
        var result = await _client.CheckPairingAsync(terminal, cancellationToken);
        return new PosEslesmeSonucu(result.Id, result.PairingCode, result.TargetFingerprint, result.IsApproved);
    }

    public async Task<PosOdemeBaslatSonucu> OdemeBaslatAsync(
        PosTerminal terminal,
        string islemReferansi,
        decimal tutar,
        string paraBirimi,
        CancellationToken cancellationToken)
    {
        var result = await _client.CreateLinkAsync(terminal, islemReferansi, tutar, paraBirimi, cancellationToken);
        return new PosOdemeBaslatSonucu(
            result.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            result.StatusId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            result.RawJson);
    }

    public async Task<PosOdemeSorguSonucu> OdemeDurumuAsync(
        PosTerminal terminal,
        string saglayiciIslemId,
        string islemReferansi,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(saglayiciIslemId, out var paymentLinkId))
        {
            throw new BaseException("PAVO odeme baglantisi gecersiz.", 409);
        }

        var result = await _client.CheckLinkAsync(terminal, paymentLinkId, islemReferansi, cancellationToken);
        return new PosOdemeSorguSonucu(
            result.StatusId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            result.Pending,
            result.Successful,
            result.RawJson,
            result.ErrorMessage,
            result.RetrievalReferenceNo,
            result.AcquirerReference,
            result.AuthorizationCode);
    }
}
