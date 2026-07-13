using STYS.Muhasebe.TahsilatOdemeBelgeleri.Dtos;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.TahsilatOdemeBelgeleri.Services;

public interface ITahsilatOdemeBelgesiService : IBaseRdbmsService<TahsilatOdemeBelgesiDto, TahsilatOdemeBelgesi, int>
{
    Task<TahsilatOdemeOzetDto> GetGunlukOzetAsync(DateTime gun, int? tesisId, CancellationToken cancellationToken = default);
    Task IptalEtAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>requireCariMuhasebeHesabi: Rezervasyon gibi "AlinanAvans" moduna gecebilen
    /// kaynaklar icin false gecirilebilir — bkz. implementasyondaki XML dokumantasyonu.</summary>
    Task ValidateOlusturmaAsync(
        int cariKartId,
        string belgeTipi,
        string odemeYontemi,
        string durum,
        DateTime belgeTarihi,
        int? kapatilacakCariHareketId,
        bool requireCariMuhasebeHesabi,
        CancellationToken cancellationToken = default);
}
