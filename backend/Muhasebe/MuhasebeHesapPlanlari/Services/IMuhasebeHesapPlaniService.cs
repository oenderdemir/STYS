using STYS.Muhasebe.MuhasebeHesapPlanlari.Dtos;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.MuhasebeHesapPlanlari.Services;

public interface IMuhasebeHesapPlaniService : IBaseRdbmsService<MuhasebeHesapPlaniDto, MuhasebeHesapPlani, int>
{
    Task<List<MuhasebeHesapPlaniDto>> GetTreeAsync(CancellationToken cancellationToken = default);
    Task<List<MuhasebeHesapPlaniDto>> GetTreeRootsAsync(CancellationToken cancellationToken = default);
    Task<List<MuhasebeHesapPlaniDto>> GetTreeChildrenAsync(int? parentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ana hesap altında, çalışma tesisi kapsamında güvenli bir DETAY hesap oluşturur.
    /// Kod/TamKod/SeviyeNo/UstHesapId/DetayHesapMi/HareketGorebilirMi backend tarafından belirlenir;
    /// tesisId istek gövdesinden DEĞİL, çalışma tesisi resolution'ından (query/effective scope) gelir.
    /// </summary>
    Task<MuhasebeHesapPlaniDto> CreateDetayHesapAsync(
        int anaHesapId,
        string ad,
        int? tesisId,
        CancellationToken cancellationToken = default);
}
