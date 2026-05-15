using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.MuhasebeDonemleri.Dtos;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.MuhasebeDonemleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.MuhasebeDonemleri.Services;

public class MuhasebeDonemService
    : BaseRdbmsService<MuhasebeDonemDto, MuhasebeDonem, int>,
      IMuhasebeDonemService
{
    private readonly IMuhasebeDonemRepository _repository;
    private readonly StysAppDbContext _dbContext;

    public MuhasebeDonemService(
        IMuhasebeDonemRepository repository,
        IMapper mapper,
        StysAppDbContext dbContext)
        : base(repository, mapper)
    {
        _repository = repository;
        _dbContext = dbContext;
    }

    public override async Task<MuhasebeDonemDto?> GetByIdAsync(int id, System.Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null)
    {
        var entity = await _repository.GetByIdAsync(id, q => q.Include(x => x.Tesis));
        return Mapper.Map<MuhasebeDonemDto?>(entity);
    }

    public override async Task<IEnumerable<MuhasebeDonemDto>> GetAllAsync(System.Func<IQueryable<MuhasebeDonem>, IQueryable<MuhasebeDonem>>? include = null)
    {
        var entities = await _repository.GetAllAsync(q => q.Include(x => x.Tesis));
        return Mapper.Map<IEnumerable<MuhasebeDonemDto>>(entities);
    }

    public async Task<MuhasebeDonemDto?> GetAktifDonemAsync(
        int tesisId,
        DateTime tarih,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetAktifDonemAsync(tesisId, tarih, cancellationToken);
        return Mapper.Map<MuhasebeDonemDto?>(entity);
    }

    public override async Task<MuhasebeDonemDto> AddAsync(MuhasebeDonemDto dto)
    {
        await ValidateCreateAsync(dto, CancellationToken.None);

        dto.KapaliMi = false;
        dto.KapanisTarihi = null;

        var entity = Mapper.Map<MuhasebeDonem>(dto);
        await _dbContext.MuhasebeDonemler.AddAsync(entity);
        await _dbContext.SaveChangesAsync();

        // Reload with Tesis include for TesisAdi
        var created = await _repository.GetByIdAsync(entity.Id,
            q => q.Include(x => x.Tesis))
            ?? throw new BaseException("Dönem oluşturulamadı.", 500);
        return Mapper.Map<MuhasebeDonemDto>(created);
    }

    public override async Task<MuhasebeDonemDto> UpdateAsync(MuhasebeDonemDto dto)
    {
        var existing = await _dbContext.MuhasebeDonemler
            .FirstOrDefaultAsync(x => x.Id == dto.Id && !x.IsDeleted);

        if (existing is null)
            throw new BaseException("Dönem bulunamadı.", 404);

        // Kapalı dönemde sadece Aciklama değiştirilebilir
        if (existing.KapaliMi)
        {
            if (dto.TesisId != existing.TesisId
                || dto.MaliYil != existing.MaliYil
                || dto.DonemNo != existing.DonemNo
                || dto.BaslangicTarihi != existing.BaslangicTarihi
                || dto.BitisTarihi != existing.BitisTarihi
                || dto.KapaliMi != existing.KapaliMi)
            {
                throw new BaseException("Kapalı dönemin tarih veya dönem bilgileri değiştirilemez.", 400);
            }

            existing.Aciklama = dto.Aciklama;
            await _dbContext.SaveChangesAsync();

            await _dbContext.Entry(existing).Reference(x => x.Tesis).LoadAsync();
            return Mapper.Map<MuhasebeDonemDto>(existing);
        }

        // Açık dönemde KapaliMi değişikliğine izin verme (DonemKapatAsync/DonemAcAsync kullanılmalı)
        if (dto.KapaliMi != existing.KapaliMi)
            throw new BaseException("Dönem kapatma/açma işlemi için ilgili endpoint'i kullanınız.", 400);

        // Açık dönemde tüm alanlar validasyonla güncellenebilir
        await ValidateUpdateAsync(dto, existing, CancellationToken.None);

        existing.TesisId = dto.TesisId;
        existing.MaliYil = dto.MaliYil;
        existing.DonemNo = dto.DonemNo;
        existing.BaslangicTarihi = dto.BaslangicTarihi;
        existing.BitisTarihi = dto.BitisTarihi;
        existing.Aciklama = dto.Aciklama;

        await _dbContext.SaveChangesAsync();

        await _dbContext.Entry(existing).Reference(x => x.Tesis).LoadAsync();
        return Mapper.Map<MuhasebeDonemDto>(existing);
    }

    public override async Task DeleteAsync(int id)
    {
        var existing = await _dbContext.MuhasebeDonemler
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (existing is null)
            throw new BaseException("Dönem bulunamadı.", 404);

        if (existing.KapaliMi)
            throw new BaseException("Kapalı dönem silinemez.", 400);

        // Platform BaseEntity silme davranışı üzerinden dönemi sil
        _dbContext.Entry(existing).State = EntityState.Deleted;
        await _dbContext.SaveChangesAsync();
    }

    public async Task DonemKapatAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.MuhasebeDonemler
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (existing is null)
            throw new BaseException("Dönem bulunamadı.", 404);

        if (existing.KapaliMi)
            throw new BaseException("Dönem zaten kapalıdır.", 400);

        existing.KapaliMi = true;
        existing.KapanisTarihi = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DonemAcAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.MuhasebeDonemler
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (existing is null)
            throw new BaseException("Dönem bulunamadı.", 404);

        if (!existing.KapaliMi)
            throw new BaseException("Dönem zaten açıktır.", 400);

        existing.KapaliMi = false;
        existing.KapanisTarihi = null;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateCreateAsync(MuhasebeDonemDto dto, CancellationToken cancellationToken)
    {
        // 1. TesisId > 0
        if (dto.TesisId <= 0)
            throw new BaseException("Geçerli bir tesis seçilmelidir.", 400);

        // 2. Tesis var mı
        var tesis = await _dbContext.Tesisler
            .FirstOrDefaultAsync(x => x.Id == dto.TesisId && !x.IsDeleted, cancellationToken);
        if (tesis is null)
            throw new BaseException("Seçilen tesis bulunamadı.", 400);

        // 3. MaliYil 2000-2100
        if (dto.MaliYil < 2000 || dto.MaliYil > 2100)
            throw new BaseException("Mali yıl 2000-2100 aralığında olmalıdır.", 400);

        // 4. DonemNo 1-12
        if (dto.DonemNo < 1 || dto.DonemNo > 12)
            throw new BaseException("Dönem numarası 1-12 aralığında olmalıdır.", 400);

        // 5. BaslangicTarihi < BitisTarihi
        if (dto.BaslangicTarihi >= dto.BitisTarihi)
            throw new BaseException("Başlangıç tarihi bitiş tarihinden küçük olmalıdır.", 400);

        // 6. Aynı TesisId + MaliYil + DonemNo için ikinci kayıt kontrolü
        var mevcut = await _repository.GetByTesisYilDonemAsync(
            dto.TesisId, dto.MaliYil, dto.DonemNo, cancellationToken);
        if (mevcut is not null)
            throw new BaseException("Aynı tesis, mali yıl ve dönem için kayıt zaten mevcut.", 400);

        // 7. Tarih aralığı çakışma kontrolü
        var cakisiyor = await _repository.TarihAraligiCakisiyorMuAsync(
            dto.TesisId, dto.BaslangicTarihi, dto.BitisTarihi, null, cancellationToken);
        if (cakisiyor)
            throw new BaseException("Seçilen tarih aralığı aynı tesis için başka bir dönemle çakışıyor.", 400);
    }

    private async Task ValidateUpdateAsync(MuhasebeDonemDto dto, MuhasebeDonem existing, CancellationToken cancellationToken)
    {
        // 1. TesisId > 0
        if (dto.TesisId <= 0)
            throw new BaseException("Geçerli bir tesis seçilmelidir.", 400);

        // 2. Tesis var mı
        var tesis = await _dbContext.Tesisler
            .FirstOrDefaultAsync(x => x.Id == dto.TesisId && !x.IsDeleted, cancellationToken);
        if (tesis is null)
            throw new BaseException("Seçilen tesis bulunamadı.", 400);

        // 3. MaliYil 2000-2100
        if (dto.MaliYil < 2000 || dto.MaliYil > 2100)
            throw new BaseException("Mali yıl 2000-2100 aralığında olmalıdır.", 400);

        // 4. DonemNo 1-12
        if (dto.DonemNo < 1 || dto.DonemNo > 12)
            throw new BaseException("Dönem numarası 1-12 aralığında olmalıdır.", 400);

        // 5. BaslangicTarihi < BitisTarihi
        if (dto.BaslangicTarihi >= dto.BitisTarihi)
            throw new BaseException("Başlangıç tarihi bitiş tarihinden küçük olmalıdır.", 400);

        // 6. Aynı TesisId + MaliYil + DonemNo için başka kayıt kontrolü (kendisi hariç)
        var mevcut = await _repository.GetByTesisYilDonemAsync(
            dto.TesisId, dto.MaliYil, dto.DonemNo, cancellationToken);
        if (mevcut is not null && mevcut.Id != existing.Id)
            throw new BaseException("Aynı tesis, mali yıl ve dönem için kayıt zaten mevcut.", 400);

        // 7. Tarih aralığı çakışma kontrolü (kendisi hariç)
        var cakisiyor = await _repository.TarihAraligiCakisiyorMuAsync(
            dto.TesisId, dto.BaslangicTarihi, dto.BitisTarihi, existing.Id, cancellationToken);
        if (cakisiyor)
            throw new BaseException("Seçilen tarih aralığı aynı tesis için başka bir dönemle çakışıyor.", 400);
    }
}
