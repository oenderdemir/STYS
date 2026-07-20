using STYS.Kbs.Constants;
using STYS.Kbs.Dtos;

namespace STYS.Kbs.Connectors;

public class EgmKbsExcelConnector : IKbsConnector
{
    public string Saglayici => KbsEntegrasyonTipleri.Excel;
    private static KbsSonuc Manual() => new(false, "EXCEL-MANUAL", "EGM bildirimi Excel uretilip kullanici tarafindan yuklenmelidir.", KbsHataSiniflari.Configuration);
    public Task<KbsSonuc> GirisBildirAsync(KbsGirisTalebi talep, CancellationToken cancellationToken) => Task.FromResult(Manual());
    public Task<KbsSonuc> CikisBildirAsync(KbsCikisTalebi talep, CancellationToken cancellationToken) => Task.FromResult(Manual());
    public Task<KbsSonuc> OdaGuncelleAsync(KbsOdaGuncellemeTalebi talep, CancellationToken cancellationToken) => Task.FromResult(Manual());
    public Task<KbsBaglantiTestSonucu> BaglantiKontrolAsync(int tesisId, CancellationToken cancellationToken) => Task.FromResult(new KbsBaglantiTestSonucu(true, "Excel connector konfigurasyonu dogrulanabilir; ag cagrisi yapmaz.", false));
}
