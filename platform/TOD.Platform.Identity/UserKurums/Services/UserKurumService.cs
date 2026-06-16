using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TOD.Platform.Identity.UserKurums.Dto;
using TOD.Platform.Identity.UserKurums.Entities;
using TOD.Platform.Identity.UserKurums.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace TOD.Platform.Identity.UserKurums.Services;

public class UserKurumService : BaseRdbmsService<UserKurumDto, UserKurum, Guid>, IUserKurumService
{
    private readonly IUserKurumRepository _userKurumRepository;

    public UserKurumService(IUserKurumRepository userKurumRepository, IMapper mapper)
        : base(userKurumRepository, mapper)
    {
        _userKurumRepository = userKurumRepository;
    }

    public async Task<List<UserKurumDto>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var items = await _userKurumRepository
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.VarsayilanMi)
            .ThenBy(x => x.KurumId)
            .ToListAsync(cancellationToken);

        return Mapper.Map<List<UserKurumDto>>(items);
    }

    public async Task<List<UserKurumDto>> GetByKurumIdAsync(int kurumId, CancellationToken cancellationToken = default)
    {
        var items = await _userKurumRepository
            .Where(x => x.KurumId == kurumId)
            .OrderBy(x => x.UserId)
            .ToListAsync(cancellationToken);

        return Mapper.Map<List<UserKurumDto>>(items);
    }

    public async Task<UserKurumDto> AssignAsync(AssignUserKurumRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BaseException("Istek bos olamaz.", 400);
        }

        ValidateRequest(request.UserId, request.KurumId);

        var exists = await _userKurumRepository.AnyAsync(x => x.UserId == request.UserId && x.KurumId == request.KurumId);
        if (exists)
        {
            throw new BaseException("Ayni user-kurum atamasi zaten mevcut.", 400);
        }

        if (request.VarsayilanMi)
        {
            await ClearDefaultAssignmentsAsync(request.UserId, cancellationToken);
        }

        var entity = Mapper.Map<UserKurum>(request);
        entity.Id = Guid.NewGuid();
        entity.VarsayilanMi = request.VarsayilanMi;
        entity.AktifMi = request.AktifMi;
        entity.IsKurumAdmin = request.IsKurumAdmin;

        await _userKurumRepository.AddAsync(entity);
        await _userKurumRepository.SaveChangesAsync(cancellationToken);

        return Mapper.Map<UserKurumDto>(entity);
    }

    public async Task<UserKurumDto> UpdateAsync(Guid id, UpdateUserKurumRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new BaseException("Istek bos olamaz.", 400);
        }

        var entity = await _userKurumRepository.GetByIdAsync(id);
        if (entity is null)
        {
            throw new BaseException("UserKurum kaydi bulunamadi.", 404);
        }

        if (request.VarsayilanMi)
        {
            await ClearDefaultAssignmentsAsync(entity.UserId, cancellationToken, id);
        }

        entity.VarsayilanMi = request.VarsayilanMi && request.AktifMi;
        entity.AktifMi = request.AktifMi;
        entity.IsKurumAdmin = request.IsKurumAdmin;

        if (!entity.AktifMi)
        {
            entity.VarsayilanMi = false;
        }

        _userKurumRepository.Update(entity);
        await _userKurumRepository.SaveChangesAsync(cancellationToken);

        return Mapper.Map<UserKurumDto>(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _userKurumRepository.GetByIdAsync(id);
        if (entity is null)
        {
            throw new BaseException("UserKurum kaydi bulunamadi.", 404);
        }

        _userKurumRepository.Delete(entity);
        await _userKurumRepository.SaveChangesAsync(cancellationToken);
    }

    public override async Task<UserKurumDto> AddAsync(UserKurumDto dto)
    {
        throw new NotSupportedException("Use AssignAsync.");
    }

    public override async Task<UserKurumDto> UpdateAsync(UserKurumDto dto)
    {
        throw new NotSupportedException("Use UpdateAsync(Guid, request).");
    }

    public override async Task DeleteAsync(Guid id)
    {
        await DeleteAsync(id, CancellationToken.None);
    }

    private async Task ClearDefaultAssignmentsAsync(Guid userId, CancellationToken cancellationToken, Guid? excludedId = null)
    {
        var defaults = await _userKurumRepository
            .Where(x => x.UserId == userId && x.VarsayilanMi && (!excludedId.HasValue || x.Id != excludedId.Value))
            .ToListAsync(cancellationToken);

        if (defaults.Count == 0)
        {
            return;
        }

        foreach (var item in defaults)
        {
            item.VarsayilanMi = false;
        }

        foreach (var item in defaults)
        {
            _userKurumRepository.Update(item);
        }
        await _userKurumRepository.SaveChangesAsync(cancellationToken);
    }

    // TODO Tenant Faz 2/3: Kurum varlik kontrolu backend orchestration tarafinda yapilacak.
    private static void ValidateRequest(Guid userId, int kurumId)
    {
        if (userId == Guid.Empty)
        {
            throw new BaseException("UserId zorunludur.", 400);
        }

        if (kurumId <= 0)
        {
            throw new BaseException("KurumId zorunludur.", 400);
        }
    }
}
