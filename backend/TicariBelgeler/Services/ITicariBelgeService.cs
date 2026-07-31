using STYS.TicariBelgeler.Dtos;

namespace STYS.TicariBelgeler.Services;

/// <summary>
/// Operasyon modülleri (resepsiyon, rezervasyon, restoran, kamp vb.) için TicariBelge uygulama
/// sınırı — mevcut SatisBelgesi muhasebe altyapısını (ISatisBelgesiService,
/// ISatisBelgesiTaslakOlusturmaService) orkestre eden bir façade'dir, iş kurallarını KOPYALAMAZ.
///
/// Bilinçli olarak SUNULMAYAN işlemler (bkz. görev D): MuhasebeOnaylaAsync, ReddetAsync,
/// MuhasebeFisiOlusturAsync, FaturaKesAsync, muhasebe fişi iptal/ters kayıt işlemleri — bunlar
/// yalnızca Muhasebe > Satış Belgeleri ekranı (SatisBelgeleriController) üzerinden yapılabilir.
/// </summary>
public interface ITicariBelgeService
{
    Task<TicariBelgeDetayDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<TicariBelgeDto>> FilterAsync(TicariBelgeFilterDto filter, CancellationToken cancellationToken = default);
    Task<TicariBelgeDetayDto> KaynaktanTaslakOlusturAsync(TicariBelgeTaslakOlusturRequest request, CancellationToken cancellationToken = default);
    Task<TicariBelgeDetayDto> UpdateAsync(int id, TicariBelgeGuncelleRequest request, CancellationToken cancellationToken = default);
    Task MuhasebeOnayinaGonderAsync(int id, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task IptalEtAsync(int id, CancellationToken cancellationToken = default);
}
