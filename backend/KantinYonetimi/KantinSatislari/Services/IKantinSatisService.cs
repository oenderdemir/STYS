using STYS.KantinYonetimi.KantinSatislari.Dtos;
using STYS.KantinYonetimi.KantinSatislari.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.KantinYonetimi.KantinSatislari.Services;

public interface IKantinSatisService : IBaseRdbmsService<KantinSatisDto, KantinSatis, int>
{
    Task<List<KantinSatisDto>> GetListAsync(int? tesisId, int? kantinId, CancellationToken cancellationToken = default);
    Task<KantinSatisDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<KantinSatisDto> AddAsync(KantinSatisDto dto, CancellationToken cancellationToken = default);
    Task<KantinSatisDto> UpdateAsync(KantinSatisDto dto, CancellationToken cancellationToken = default);
    Task<KantinSatisDto> AddSatirAsync(int satisId, AddKantinSatisSatirRequest request, CancellationToken cancellationToken = default);
    Task<KantinSatisDto> UpdateSatirAsync(int satisId, int satirId, UpdateKantinSatisSatirRequest request, CancellationToken cancellationToken = default);
    Task DeleteSatirAsync(int satisId, int satirId, CancellationToken cancellationToken = default);
    Task<KantinSatisDto> AddOdemeAsync(int satisId, AddKantinSatisOdemeRequest request, CancellationToken cancellationToken = default);
    Task<KantinSatisDto> UpdateOdemeAsync(int satisId, int odemeId, UpdateKantinSatisOdemeRequest request, CancellationToken cancellationToken = default);
    Task DeleteOdemeAsync(int satisId, int odemeId, CancellationToken cancellationToken = default);
    Task<KantinSatisDto> KesinlestirAsync(int satisId, CancellationToken cancellationToken = default);
    Task<KantinSatisBarkodUrunDto?> GetAktifUrunByBarkodAsync(int kantinId, string barkod, CancellationToken cancellationToken = default);
}
