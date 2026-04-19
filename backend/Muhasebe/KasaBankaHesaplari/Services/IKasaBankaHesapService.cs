using STYS.Muhasebe.KasaBankaHesaplari.Dtos;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.KasaBankaHesaplari.Services;

public interface IKasaBankaHesapService : IBaseRdbmsService<KasaBankaHesapDto, KasaBankaHesap, int>
{
    Task<List<KasaBankaHesapDto>> GetByTipAsync(string tip, bool onlyActive, CancellationToken cancellationToken = default);
    Task<List<MuhasebeHesapSecimDto>> GetMuhasebeHesapSecimleriAsync(string tip, CancellationToken cancellationToken = default);
}
