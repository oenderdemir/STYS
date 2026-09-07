using STYS.Muhasebe.CariHareketler.Dtos;
using STYS.Muhasebe.CariHareketler.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.CariHareketler.Services;

public interface ICariHareketService : IBaseRdbmsService<CariHareketDto, CariHareket, int>
{
    Task<CariBakiyeOzetDto> GetCariBakiyeOzetAsync(int cariKartId, CancellationToken cancellationToken = default);
    Task<List<CariHareketDurumOzetDto>> GetCariAcikHareketlerAsync(int cariKartId, CancellationToken cancellationToken = default);
    Task<List<CariHareketDurumOzetDto>> GetCariKapananHareketlerAsync(int cariKartId, CancellationToken cancellationToken = default);
    Task<List<CariHareketDurumOzetDto>> GetCariHareketEkstreAsync(int cariKartId, DateTime? baslangic, DateTime? bitis, CancellationToken cancellationToken = default);
    Task<CariEkstreDto> GetEkstreAsync(int cariKartId, DateTime? baslangic, DateTime? bitis, CancellationToken cancellationToken = default);

    /// <summary>Dar kapsamlı sunucu-tarafı filtreli paged sorgu (cari arama, belge, tarih, durum,
    /// kaynak modül, kapama durumu + tesis scope).</summary>
    Task<PagedResult<CariHareketDto>> GetPagedWithFilterAsync(
        CariHareketFilterRequest filter,
        PagedRequest request,
        CancellationToken cancellationToken = default);
}
