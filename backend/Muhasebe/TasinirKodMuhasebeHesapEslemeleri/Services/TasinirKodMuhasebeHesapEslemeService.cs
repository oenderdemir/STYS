using AutoMapper;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Dtos;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Entities;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Services;

public class TasinirKodMuhasebeHesapEslemeService
    : BaseRdbmsService<TasinirKodMuhasebeHesapEslemeDto, TasinirKodMuhasebeHesapEsleme, int>,
      ITasinirKodMuhasebeHesapEslemeService
{
    private readonly ITasinirKodMuhasebeHesapEslemeRepository _repository;

    public TasinirKodMuhasebeHesapEslemeService(
        ITasinirKodMuhasebeHesapEslemeRepository repository,
        IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
    }

    public async Task<List<TasinirKodMuhasebeHesapEslemeDto>> GetByTasinirKodIdAsync(int tasinirKodId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetByTasinirKodIdAsync(tasinirKodId, cancellationToken);
        return Mapper.Map<List<TasinirKodMuhasebeHesapEslemeDto>>(items);
    }
}