using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using STYS.Kbs.Constants;
using STYS.Kbs.Dtos;
using STYS.Kbs.Options;

namespace STYS.Kbs.Connectors;

public class JandarmaKbsConnector(IOptions<KbsOptions> options, IHostEnvironment environment) : IKbsConnector
{
    public string Saglayici => KbsEntegrasyonTipleri.Soap;
    public Task<KbsSonuc> GirisBildirAsync(KbsGirisTalebi talep, CancellationToken cancellationToken) => Task.FromResult(Blocked());
    public Task<KbsSonuc> CikisBildirAsync(KbsCikisTalebi talep, CancellationToken cancellationToken) => Task.FromResult(Blocked());
    public Task<KbsSonuc> OdaGuncelleAsync(KbsOdaGuncellemeTalebi talep, CancellationToken cancellationToken) => Task.FromResult(Blocked());
    public Task<KbsBaglantiTestSonucu> BaglantiKontrolAsync(int tesisId, CancellationToken cancellationToken) => Task.FromResult(new KbsBaglantiTestSonucu(false, "Konfigurasyon dogrulandi fakat resmi WSDL adapteri saglanmadigi icin canli cagri yapilmadi.", false));
    private KbsSonuc Blocked() => !options.Value.LiveConnectorsEnabled || !environment.IsProduction()
        ? new(false, "LIVE-DISABLED", "Canli Jandarma connector'u bu ortamda kapali.", KbsHataSiniflari.Configuration)
        : new(false, "WSDL-ADAPTER-MISSING", "Resmi WSDL'den uretilmis adapter saglanmadi.", KbsHataSiniflari.Configuration);
}
