using STYS.KantinYonetimi.Kantinler.Dtos;
using STYS.KantinYonetimi.Kantinler.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.KantinYonetimi.Kantinler.Services;

public interface IKantinService : IBaseRdbmsService<KantinDto, Kantin, int>
{
    Task<List<KantinDto>> GetListAsync(int? tesisId, CancellationToken cancellationToken = default);
    Task<KantinDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<KantinDto> AddAsync(KantinDto dto, CancellationToken cancellationToken = default);
    Task<KantinDto> UpdateAsync(KantinDto dto, CancellationToken cancellationToken = default);
    Task<List<KantinUrunDto>> GetUrunlerAsync(int kantinId, CancellationToken cancellationToken = default);
    Task<KantinUrunDto> AddUrunAsync(int kantinId, KantinUrunDto dto, CancellationToken cancellationToken = default);
    Task<KantinUrunDto> UpdateUrunAsync(int kantinId, KantinUrunDto dto, CancellationToken cancellationToken = default);
    Task<List<KantinDepoSecenekDto>> GetDepolarAsync(int tesisId, CancellationToken cancellationToken = default);
    Task<List<KantinKasaSecenekDto>> GetNakitKasalarAsync(int tesisId, CancellationToken cancellationToken = default);
    Task<List<KantinTasinirKartSecenekDto>> GetTasinirKartlarAsync(int tesisId, CancellationToken cancellationToken = default);
}
