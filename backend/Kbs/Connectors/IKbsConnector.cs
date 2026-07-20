using STYS.Kbs.Dtos;

namespace STYS.Kbs.Connectors;

public interface IKbsConnector
{
    string Saglayici { get; }
    Task<KbsSonuc> GirisBildirAsync(KbsGirisTalebi talep, CancellationToken cancellationToken);
    Task<KbsSonuc> CikisBildirAsync(KbsCikisTalebi talep, CancellationToken cancellationToken);
    Task<KbsSonuc> OdaGuncelleAsync(KbsOdaGuncellemeTalebi talep, CancellationToken cancellationToken);
    Task<KbsBaglantiTestSonucu> BaglantiKontrolAsync(int tesisId, CancellationToken cancellationToken);
}

public interface IKbsConnectorResolver { IKbsConnector Resolve(string entegrasyonTipi); }
