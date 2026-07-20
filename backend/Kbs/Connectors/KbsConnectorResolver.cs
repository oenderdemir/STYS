using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Kbs.Connectors;

public class KbsConnectorResolver(IEnumerable<IKbsConnector> connectors) : IKbsConnectorResolver
{
    public IKbsConnector Resolve(string entegrasyonTipi) => connectors.FirstOrDefault(x => string.Equals(x.Saglayici, entegrasyonTipi, StringComparison.OrdinalIgnoreCase))
        ?? throw new BaseException("KBS connector konfigurasyonu bulunamadi.", 400);
}
