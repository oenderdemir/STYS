using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.MuhasebeFisleri.Dtos;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.MuhasebeFisleri.Services;

public class MuhasebeFisService
    : BaseRdbmsService<MuhasebeFisDto, MuhasebeFis, int>,
      IMuhasebeFisService
{
    private readonly IMuhasebeFisRepository _repository;
    private readonly StysAppDbContext _dbContext;

    public MuhasebeFisService(
        IMuhasebeFisRepository repository,
        IMapper mapper,
        StysAppDbContext dbContext)
        : base(repository, mapper)
    {
        _repository = repository;
        _dbContext = dbContext;
    }

    public async Task<MuhasebeFisDto?> GetByIdWithSatirlarAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdWithSatirlarAsync(id, cancellationToken);
        return Mapper.Map<MuhasebeFisDto?>(entity);
    }

    public async Task<List<MuhasebeFisDto>> GetByKaynakAsync(
        string kaynakModul, int kaynakId, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByKaynakAsync(kaynakModul, kaynakId, cancellationToken);
        return Mapper.Map<List<MuhasebeFisDto>>(entities);
    }

    public override async Task<MuhasebeFisDto> AddAsync(MuhasebeFisDto dto)
    {
        await NormalizeAndValidateCreateAsync(dto, CancellationToken.None);
        var entity = Mapper.Map<MuhasebeFis>(dto);
        entity.ToplamBorc = dto.ToplamBorc;
        entity.ToplamAlacak = dto.ToplamAlacak;
        entity.Durum = MuhasebeFisDurumlari.Taslak;
        entity.FisNo = string.IsNullOrWhiteSpace(dto.FisNo)
            ? $"TASLAK-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
            : dto.FisNo;
        entity.Satirlar = Mapper.Map<List<MuhasebeFisSatir>>(dto.Satirlar);
        await _dbContext.MuhasebeFisler.AddAsync(entity);
        await _dbContext.SaveChangesAsync();
        var created = await _repository.GetByIdWithSatirlarAsync(entity.Id)
            ?? throw new BaseException("Fiş oluşturulamadı.", 500);
        return Mapper.Map<MuhasebeFisDto>(created);
    }

    public override async Task<MuhasebeFisDto> UpdateAsync(MuhasebeFisDto dto)
    {
        var existing = await _dbContext.MuhasebeFisler
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == dto.Id && !x.IsDeleted);

        if (existing is null)
            throw new BaseException("Fiş bulunamadı.", 404);

        if (existing.Durum != MuhasebeFisDurumlari.Taslak)
            throw new BaseException("Yalnızca taslak durumundaki fişler güncellenebilir.", 400);

        // Normalize ve validate
        await NormalizeAndValidateCreateAsync(dto, CancellationToken.None);

        // Header alanlarını güncelle
        existing.TesisId = dto.TesisId;
        existing.MaliYil = dto.MaliYil;
        existing.Donem = dto.Donem;
        existing.FisTarihi = dto.FisTarihi;
        existing.FisTipi = dto.FisTipi;
        existing.Aciklama = dto.Aciklama;
        existing.ToplamBorc = dto.ToplamBorc;
        existing.ToplamAlacak = dto.ToplamAlacak;

        // Eski satırları sil (sadece silinmemiş olanları)
        foreach (var oldSatir in existing.Satirlar.Where(s => !s.IsDeleted))
        {
            _dbContext.Entry(oldSatir).State = EntityState.Deleted;
        }
        existing.Satirlar.Clear();

        foreach (var satirDto in dto.Satirlar)
        {
            var satir = Mapper.Map<MuhasebeFisSatir>(satirDto);
            satir.MuhasebeFisId = existing.Id;
            existing.Satirlar.Add(satir);
        }

        await _dbContext.SaveChangesAsync();

        // Reload complete entity with satırlar
        var reloaded = await _repository.GetByIdWithSatirlarAsync(existing.Id)
            ?? throw new BaseException("Güncellenen fiş okunamadı.", 500);
        return Mapper.Map<MuhasebeFisDto>(reloaded);
    }

    public override async Task DeleteAsync(int id)
    {
        var existing = await _dbContext.MuhasebeFisler
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (existing is null)
            throw new BaseException("Fiş bulunamadı.", 404);

        if (existing.Durum != MuhasebeFisDurumlari.Taslak)
            throw new BaseException("Yalnızca taslak durumundaki fişler silinebilir.", 400);

        // Platform BaseEntity silme davranışı üzerinden fiş ve satırları sil
        foreach (var satir in existing.Satirlar.Where(s => !s.IsDeleted))
        {
            _dbContext.Entry(satir).State = EntityState.Deleted;
        }

        // Platform BaseEntity silme davranışı üzerinden fişi sil
        _dbContext.Entry(existing).State = EntityState.Deleted;
        await _dbContext.SaveChangesAsync();
    }

    private async Task NormalizeAndValidateCreateAsync(MuhasebeFisDto dto, CancellationToken cancellationToken)
    {
        // 1. TesisId > 0
        if (dto.TesisId <= 0)
            throw new BaseException("Geçerli bir tesis seçilmelidir.", 400);

        // 2. MaliYil geçerli (2000-2100)
        if (dto.MaliYil < 2000 || dto.MaliYil > 2100)
            throw new BaseException("Mali yıl 2000-2100 aralığında olmalıdır.", 400);

        // 3. Donem 1-12
        if (dto.Donem < 1 || dto.Donem > 12)
            throw new BaseException("Dönem 1-12 aralığında olmalıdır.", 400);

        // 4. FisTarihi zorunlu
        if (dto.FisTarihi == default)
            throw new BaseException("Fiş tarihi zorunludur.", 400);

        // 5. FisTipi desteklenen
        if (string.IsNullOrWhiteSpace(dto.FisTipi))
            throw new BaseException("Fiş tipi boş olamaz.", 400);
        if (!MuhasebeFisTipleri.Hepsi.Contains(dto.FisTipi))
            throw new BaseException($"Desteklenmeyen fiş tipi: {dto.FisTipi}.", 400);

        // 6-7. KaynakModul
        if (string.IsNullOrWhiteSpace(dto.KaynakModul))
            dto.KaynakModul = MuhasebeKaynakModulleri.Manuel;
        if (!MuhasebeKaynakModulleri.Hepsi.Contains(dto.KaynakModul))
            throw new BaseException($"Desteklenmeyen kaynak modül: {dto.KaynakModul}.", 400);

        // 8. Durum create sırasında Taslak olmalı (controller'dan gelmez, service set eder)
        dto.Durum = MuhasebeFisDurumlari.Taslak;

        // 9. En az iki satır olmalı
        if (dto.Satirlar is null || dto.Satirlar.Count < 2)
            throw new BaseException("En az iki fiş satırı gereklidir.", 400);

        decimal toplamBorc = 0;
        decimal toplamAlacak = 0;

        // Satır validasyonları
        for (int i = 0; i < dto.Satirlar.Count; i++)
        {
            var satir = dto.Satirlar[i];

            // 10. MuhasebeHesapPlaniId > 0
            if (satir.MuhasebeHesapPlaniId <= 0)
                throw new BaseException($"{i + 1}. satırda geçerli bir muhasebe hesabı seçilmelidir.", 400);

            // 11. Borc ve Alacak aynı anda > 0 olamaz
            if (satir.Borc > 0 && satir.Alacak > 0)
                throw new BaseException($"{i + 1}. satırda hem borç hem alacak girilemez.", 400);

            // 12. Borc ve Alacak ikisi de 0 olamaz
            if (satir.Borc == 0 && satir.Alacak == 0)
                throw new BaseException($"{i + 1}. satırda borç veya alacak girilmelidir.", 400);

            // 13. Negatif olamaz
            if (satir.Borc < 0 || satir.Alacak < 0)
                throw new BaseException($"{i + 1}. satırda borç veya alacak negatif olamaz.", 400);

            // 15. Sadece DetayHesapMi=true ve HareketGorebilirMi=true hesaplara yazılabilir
            var hesap = await _dbContext.MuhasebeHesapPlanlari
                .FirstOrDefaultAsync(x => x.Id == satir.MuhasebeHesapPlaniId, cancellationToken);

            if (hesap is null)
                throw new BaseException($"{i + 1}. satırda seçilen muhasebe hesabı bulunamadı.", 400);
            if (hesap.IsDeleted)
                throw new BaseException($"{i + 1}. satırda seçilen muhasebe hesabı silinmiştir.", 400);
            if (!hesap.AktifMi)
                throw new BaseException($"{i + 1}. satırda seçilen muhasebe hesabı aktif değildir.", 400);
            if (!hesap.DetayHesapMi)
                throw new BaseException($"{i + 1}. satırda ana hesap seçilemez. Detay hesap seçilmelidir.", 400);
            if (!hesap.HareketGorebilirMi)
                throw new BaseException($"{i + 1}. satırda hareket görebilir detay hesap seçilmelidir.", 400);

            // 16. ParaBirimi boşsa TRY
            if (string.IsNullOrWhiteSpace(satir.ParaBirimi))
                satir.ParaBirimi = "TRY";

            // 17. Kur <= 0 ise 1
            if (satir.Kur <= 0)
                satir.Kur = 1;

            // 18. SiraNo boş/0 ise otomatik sırala (i+1)
            if (satir.SiraNo <= 0)
                satir.SiraNo = i + 1;

            toplamBorc += satir.Borc;
            toplamAlacak += satir.Alacak;
        }

        // 14. Toplam Borc = Toplam Alacak
        if (toplamBorc != toplamAlacak)
            throw new BaseException($"Toplam borç ({toplamBorc:N2}) ile toplam alacak ({toplamAlacak:N2}) eşit olmalıdır.", 400);

        dto.ToplamBorc = toplamBorc;
        dto.ToplamAlacak = toplamAlacak;
    }
}
