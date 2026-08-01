using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.TicariBelgeler.Dtos;

namespace STYS.TicariBelgeler.Services;

/// <summary>
/// TicariBelgeYonetimi.View yetkisiyle erişilen, operasyonel/minimal lookup sınırı (bkz. görev A).
/// Bu servis kendisi MUHASEBE/YÖNETİM yetkisi (TesisYonetimi.View, CariKartYonetimi.View,
/// MuhasebeKdvIstisnaTanimlariYonetimi.View) İSTEMEZ - mevcut domain servislerini İÇERİDE
/// yeniden kullanır, controller'a sızdırmaz.
/// </summary>
public interface ITicariBelgeLookupService
{
    Task<List<TicariBelgeTesisLookupDto>> GetTesislerAsync(CancellationToken cancellationToken = default);

    Task<List<TicariBelgeCariKartLookupDto>> GetCariKartlarAsync(
        int tesisId, SatisBelgesiTipi belgeTipi, CancellationToken cancellationToken = default);

    Task<List<TicariBelgeKdvIstisnaLookupDto>> GetKdvIstisnalarAsync(
        TicariBelgeKdvIstisnaLookupFilterDto filter, CancellationToken cancellationToken = default);

    Task<List<TicariBelgeIadeAdayiDto>> GetIadeAdaylariAsync(
        TicariBelgeIadeAdayiFilterDto filter, CancellationToken cancellationToken = default);

    Task<List<TicariBelgeKaynakSatirDto>> GetKaynakSatirlarAsync(
        int kaynakBelgeId, int? mevcutBelgeId, CancellationToken cancellationToken = default);
}
