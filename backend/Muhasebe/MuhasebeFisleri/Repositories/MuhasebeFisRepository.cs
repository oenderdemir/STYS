using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.MuhasebeFisleri.Dtos;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.MuhasebeFisleri.Repositories;

public class MuhasebeFisRepository
    : BaseRdbmsRepository<MuhasebeFis, int>,
      IMuhasebeFisRepository
{
    private readonly StysAppDbContext _dbContext;

    public MuhasebeFisRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
        _dbContext = dbContext;
    }

    public async Task<MuhasebeFis?> GetByIdWithSatirlarAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MuhasebeFisler
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.MuhasebeHesapPlani)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
    }

    public async Task<List<MuhasebeFis>> GetByKaynakAsync(string kaynakModul, int kaynakId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MuhasebeFisler
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.MuhasebeHesapPlani)
            .Where(x => x.KaynakModul == kaynakModul && x.KaynakId == kaynakId && !x.IsDeleted)
            .OrderByDescending(x => x.FisTarihi)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MuhasebeFis>> GetFilteredAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.MuhasebeFisler
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.MuhasebeHesapPlani)
            .AsNoTracking();

        query = ApplyFilter(query, filter);

        query = query
            .OrderByDescending(x => x.FisTarihi)
            .ThenByDescending(x => x.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<int> CountFilteredAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.MuhasebeFisler.AsNoTracking();
        query = ApplyFilter(query, filter);
        return await query.CountAsync(cancellationToken);
    }

    public async Task<List<MuhasebeFis>> GetMuavinDefterAsync(MuavinDefterFilterDto filter, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.MuhasebeFisler
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.MuhasebeHesapPlani)
            .AsNoTracking();

        // Sadece Onayli ve TersKayit durumundaki fişler (Iptal ve Taslak hariç)
        query = query.Where(x =>
            x.Durum == MuhasebeFisDurumlari.Onayli ||
            x.Durum == MuhasebeFisDurumlari.TersKayit);

        query = query.Where(x => !x.IsDeleted);
        query = query.Where(x => x.TesisId == filter.TesisId);

        if (filter.MaliYil.HasValue)
            query = query.Where(x => x.MaliYil == filter.MaliYil.Value);

        if (filter.Donem.HasValue)
            query = query.Where(x => x.Donem == filter.Donem.Value);

        if (filter.BaslangicTarihi.HasValue)
            query = query.Where(x => x.FisTarihi >= filter.BaslangicTarihi.Value);

        if (filter.BitisTarihi.HasValue)
            query = query.Where(x => x.FisTarihi <= filter.BitisTarihi.Value);

        query = query
            .OrderBy(x => x.FisTarihi)
            .ThenBy(x => x.YevmiyeNo)
            .ThenBy(x => x.Id);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<List<MuhasebeFis>> GetMizanFisleriAsync(MizanFilterDto filter, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.MuhasebeFisler
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.MuhasebeHesapPlani)
            .AsNoTracking();

        // Sadece Onayli ve TersKayit durumundaki fişler
        query = query.Where(x =>
            x.Durum == MuhasebeFisDurumlari.Onayli ||
            x.Durum == MuhasebeFisDurumlari.TersKayit);

        query = query.Where(x => !x.IsDeleted);
        query = query.Where(x => x.TesisId == filter.TesisId);

        if (filter.MaliYil.HasValue)
            query = query.Where(x => x.MaliYil == filter.MaliYil.Value);

        if (filter.Donem.HasValue)
            query = query.Where(x => x.Donem == filter.Donem.Value);

        if (filter.BaslangicTarihi.HasValue)
            query = query.Where(x => x.FisTarihi >= filter.BaslangicTarihi.Value);

        if (filter.BitisTarihi.HasValue)
            query = query.Where(x => x.FisTarihi <= filter.BitisTarihi.Value);

        query = query
            .OrderBy(x => x.FisTarihi)
            .ThenBy(x => x.YevmiyeNo)
            .ThenBy(x => x.Id);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<List<MuhasebeFis>> GetYevmiyeDefteriAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.MuhasebeFisler
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.MuhasebeHesapPlani)
            .AsNoTracking();

        // Default: sadece Onayli ve TersKayit durumları
        if (string.IsNullOrWhiteSpace(filter.Durum))
        {
            query = query.Where(x =>
                x.Durum == MuhasebeFisDurumlari.Onayli ||
                x.Durum == MuhasebeFisDurumlari.TersKayit);
        }
        else
        {
            query = query.Where(x => x.Durum == filter.Durum);
        }

        query = ApplyFilter(query, filter);

        query = query
            .OrderBy(x => x.FisTarihi)
            .ThenBy(x => x.YevmiyeNo)
            .ThenBy(x => x.Id);

        return await query.ToListAsync(cancellationToken);
    }

    private IQueryable<MuhasebeFis> ApplyFilter(IQueryable<MuhasebeFis> query, MuhasebeFisFilterDto filter)
    {
        query = query.Where(x => !x.IsDeleted);

        if (filter.TesisId.HasValue)
            query = query.Where(x => x.TesisId == filter.TesisId.Value);

        if (filter.MaliYil.HasValue)
            query = query.Where(x => x.MaliYil == filter.MaliYil.Value);

        if (filter.Donem.HasValue)
            query = query.Where(x => x.Donem == filter.Donem.Value);

        if (filter.BaslangicTarihi.HasValue)
            query = query.Where(x => x.FisTarihi >= filter.BaslangicTarihi.Value);

        if (filter.BitisTarihi.HasValue)
            query = query.Where(x => x.FisTarihi <= filter.BitisTarihi.Value);

        if (!string.IsNullOrWhiteSpace(filter.FisTipi))
            query = query.Where(x => x.FisTipi == filter.FisTipi);

        if (!string.IsNullOrWhiteSpace(filter.Durum))
            query = query.Where(x => x.Durum == filter.Durum);

        if (!string.IsNullOrWhiteSpace(filter.KaynakModul))
            query = query.Where(x => x.KaynakModul == filter.KaynakModul);

        if (filter.KaynakId.HasValue)
            query = query.Where(x => x.KaynakId == filter.KaynakId.Value);

        if (filter.YevmiyeNoBaslangic.HasValue)
            query = query.Where(x => x.YevmiyeNo.HasValue && x.YevmiyeNo.Value >= filter.YevmiyeNoBaslangic.Value);

        if (filter.YevmiyeNoBitis.HasValue)
            query = query.Where(x => x.YevmiyeNo.HasValue && x.YevmiyeNo.Value <= filter.YevmiyeNoBitis.Value);

        if (!string.IsNullOrWhiteSpace(filter.FisNo))
            query = query.Where(x => x.FisNo.Contains(filter.FisNo));

        if (!string.IsNullOrWhiteSpace(filter.Aciklama))
            query = query.Where(x => x.Aciklama != null && x.Aciklama.Contains(filter.Aciklama));

        return query;
    }
}
