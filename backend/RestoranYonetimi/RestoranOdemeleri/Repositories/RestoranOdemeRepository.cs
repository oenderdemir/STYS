using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Entities;
using STYS.RestoranOdemeleri.Dtos;
using STYS.RestoranOdemeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.RestoranOdemeleri.Repositories;

public class RestoranOdemeRepository : BaseRdbmsRepository<RestoranOdeme, int>, IRestoranOdemeRepository
{
    private readonly StysAppDbContext _dbContext;

    public RestoranOdemeRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
        _dbContext = dbContext;
    }

    public Task<List<RestoranOdeme>> GetBySiparisIdAsync(int siparisId, CancellationToken cancellationToken = default)
        => _dbContext.RestoranOdemeleri
            .Where(x => x.RestoranSiparisId == siparisId)
            .OrderByDescending(x => x.OdemeTarihi)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<bool> HasCompletedRoomChargeAsync(int siparisId, int rezervasyonId, CancellationToken cancellationToken = default)
        => _dbContext.RestoranOdemeleri.AnyAsync(x =>
            x.RestoranSiparisId == siparisId
            && x.RezervasyonId == rezervasyonId
            && x.OdemeTipi == RestoranOdemeTipleri.OdayaEkle
            && x.Durum == RestoranOdemeDurumlari.Tamamlandi,
            cancellationToken);

    public async Task<List<AktifRezervasyonAramaDto>> SearchAktifRezervasyonlarAsync(int tesisId, string? query, CancellationToken cancellationToken = default)
    {
        var normalized = query?.Trim();

        var baseQuery = _dbContext.Rezervasyonlar
            .Where(x =>
                x.TesisId == tesisId
                && x.AktifMi
                && x.RezervasyonDurumu != RezervasyonDurumlari.Iptal
                && x.RezervasyonDurumu == RezervasyonDurumlari.CheckInTamamlandi);

        if (!string.IsNullOrWhiteSpace(normalized))
        {
            baseQuery = baseQuery.Where(x =>
                x.ReferansNo.Contains(normalized)
                || x.MisafirAdiSoyadi.Contains(normalized)
                || x.Segmentler.Any(s => s.OdaAtamalari.Any(a => a.OdaNoSnapshot != null && a.OdaNoSnapshot.Contains(normalized))));
        }

        return await baseQuery
            .Select(x => new AktifRezervasyonAramaDto
            {
                RezervasyonId = x.Id,
                TesisId = x.TesisId,
                ReferansNo = x.ReferansNo,
                MisafirAdiSoyadi = x.MisafirAdiSoyadi,
                OdaNo = x.Segmentler
                    .SelectMany(s => s.OdaAtamalari)
                    .Select(a => a.OdaNoSnapshot)
                    .FirstOrDefault() ?? string.Empty,
                GirisTarihi = x.GirisTarihi,
                CikisTarihi = x.CikisTarihi
            })
            .OrderBy(x => x.ReferansNo)
            .ThenBy(x => x.RezervasyonId)
            .Take(100)
            .ToListAsync(cancellationToken);
    }
}
