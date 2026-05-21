using AutoMapper;
using STYS.Muhasebe.Kdv.Dtos;
using STYS.Muhasebe.Kdv.Entities;
using STYS.Muhasebe.Kdv.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.Kdv.Services;

public class KdvIstisnaTanimService : BaseRdbmsService<KdvIstisnaTanimDto, KdvIstisnaTanim, int>, IKdvIstisnaTanimService
{
    private readonly IKdvIstisnaTanimRepository _repository;
    private readonly IMapper _mapper;

    public KdvIstisnaTanimService(
        IKdvIstisnaTanimRepository repository,
        IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public override async Task<KdvIstisnaTanimDto> AddAsync(KdvIstisnaTanimDto dto)
    {
        await ValidateCreateAsync(dto);
        return await base.AddAsync(dto);
    }

    public override async Task<KdvIstisnaTanimDto> UpdateAsync(KdvIstisnaTanimDto dto)
    {
        var existing = await _repository.GetByIdAsync(dto.Id.GetValueOrDefault());

        if (existing is null)
            throw new BaseException($"Id={dto.Id} KDV istisna tanımı bulunamadı.", 404);

        await ValidateUpdateAsync(dto, existing);
        return await base.UpdateAsync(dto);
    }

    private async Task ValidateCreateAsync(KdvIstisnaTanimDto dto)
    {
        await ValidateKodUniqueAsync(dto.Kod, null);
        ValidateGecerlilikTarihleri(dto);
    }

    private async Task ValidateUpdateAsync(KdvIstisnaTanimDto dto, KdvIstisnaTanim existing)
    {
        await ValidateKodUniqueAsync(dto.Kod, dto.Id);
        ValidateGecerlilikTarihleri(dto);
    }

    private async Task ValidateKodUniqueAsync(string kod, int? excludeId)
    {
        var exists = await _repository.AnyAsync(
            x => x.Kod == kod && (!excludeId.HasValue || x.Id != excludeId.Value));

        if (exists)
            throw new BaseException($"\"{kod}\" kodlu bir KDV istisna tanımı zaten mevcut.", 409);
    }

    private static void ValidateGecerlilikTarihleri(KdvIstisnaTanimDto dto)
    {
        if (dto.GecerlilikBaslangicTarihi.HasValue
            && dto.GecerlilikBitisTarihi.HasValue
            && dto.GecerlilikBitisTarihi.Value <= dto.GecerlilikBaslangicTarihi.Value)
        {
            throw new BaseException(
                "Geçerlilik bitiş tarihi, başlangıç tarihinden sonra olmalıdır.", 400);
        }
    }
}
