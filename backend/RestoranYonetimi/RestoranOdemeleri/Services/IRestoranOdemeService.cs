using STYS.RestoranOdemeleri.Dtos;
using STYS.RestoranSiparisleri.Dtos;

namespace STYS.RestoranOdemeleri.Services;

public interface IRestoranOdemeService
{
    Task<List<RestoranOdemeDto>> GetBySiparisIdAsync(int siparisId, CancellationToken cancellationToken = default);
    Task<RestoranSiparisOdemeOzetiDto> GetOdemeOzetiAsync(int siparisId, CancellationToken cancellationToken = default);
    Task<List<AktifRezervasyonAramaDto>> SearchAktifRezervasyonlarAsync(int tesisId, string? query, CancellationToken cancellationToken = default);
    Task<RestoranOdemeDto> CreateNakitOdemeAsync(int siparisId, CreateNakitOdemeRequest request, CancellationToken cancellationToken = default);
    Task<RestoranOdemeDto> CreateKrediKartiOdemeAsync(int siparisId, CreateKrediKartiOdemeRequest request, CancellationToken cancellationToken = default);
    Task<RestoranOdemeDto> CreateOdayaEkleOdemeAsync(int siparisId, CreateOdayaEkleOdemeRequest request, CancellationToken cancellationToken = default);
}
