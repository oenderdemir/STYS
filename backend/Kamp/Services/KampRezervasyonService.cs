using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Kamp.Dto;
using STYS.Kamp.Entities;
using STYS.Kamp.Repositories;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Kamp.Services;

public class KampRezervasyonService : IKampRezervasyonService
{
    private readonly IKampRezervasyonRepository _rezervasyonRepository;
    private readonly StysAppDbContext _dbContext;

    public KampRezervasyonService(IKampRezervasyonRepository rezervasyonRepository, StysAppDbContext dbContext)
    {
        _rezervasyonRepository = rezervasyonRepository;
        _dbContext = dbContext;
    }

    public async Task<KampRezervasyonBaglamDto> GetBaglamAsync(CancellationToken cancellationToken = default)
    {
        var donemler = await _dbContext.KampDonemleri
            .OrderByDescending(x => x.Yil)
            .ThenBy(x => x.KonaklamaBaslangicTarihi)
            .Select(x => new KampRezervasyonDonemSecenekDto { Id = x.Id, Ad = x.Ad })
            .ToListAsync(cancellationToken);

        var tesisler = await _dbContext.Tesisler
            .OrderBy(x => x.Ad)
            .Select(x => new KampRezervasyonTesisSecenekDto { Id = x.Id, Ad = x.Ad })
            .ToListAsync(cancellationToken);

        return new KampRezervasyonBaglamDto
        {
            Donemler = donemler,
            Tesisler = tesisler,
            Durumlar = KampRezervasyonDurumlari.Hepsi.ToList()
        };
    }

    public async Task<List<KampRezervasyonListeDto>> GetListeAsync(KampRezervasyonFilterDto filter, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.KampRezervasyonlari
            .AsNoTracking()
            .Include(x => x.KampDonemi)
            .Include(x => x.Tesis)
            .AsQueryable();

        if (filter.KampDonemiId.HasValue && filter.KampDonemiId.Value > 0)
            query = query.Where(x => x.KampDonemiId == filter.KampDonemiId.Value);

        if (filter.TesisId.HasValue && filter.TesisId.Value > 0)
            query = query.Where(x => x.TesisId == filter.TesisId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Durum))
            query = query.Where(x => x.Durum == filter.Durum);

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Select(x => new KampRezervasyonListeDto
            {
                Id = x.Id,
                RezervasyonNo = x.RezervasyonNo,
                KampBasvuruId = x.KampBasvuruId,
                KampDonemiId = x.KampDonemiId,
                KampDonemiAd = x.KampDonemi != null ? x.KampDonemi.Ad : string.Empty,
                TesisId = x.TesisId,
                TesisAd = x.Tesis != null ? x.Tesis.Ad : string.Empty,
                BasvuruSahibiAdiSoyadi = x.BasvuruSahibiAdiSoyadi,
                BasvuruSahibiTipi = x.BasvuruSahibiTipi,
                KonaklamaBirimiTipi = x.KonaklamaBirimiTipi,
                KatilimciSayisi = x.KatilimciSayisi,
                DonemToplamTutar = x.DonemToplamTutar,
                AvansToplamTutar = x.AvansToplamTutar,
                Durum = x.Durum,
                IptalNedeni = x.IptalNedeni,
                IptalTarihi = x.IptalTarihi,
                CreatedAt = x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<KampRezervasyonUretSonucDto> UretAsync(int kampBasvuruId, CancellationToken cancellationToken = default)
    {
        var basvuru = await _dbContext.KampBasvurulari
            .FirstOrDefaultAsync(x => x.Id == kampBasvuruId, cancellationToken)
            ?? throw new BaseException("Kamp basvurusu bulunamadi.", 404);

        if (basvuru.Durum != KampBasvuruDurumlari.TahsisEdildi)
            throw new BaseException("Rezervasyon yalnizca 'TahsisEdildi' durumundaki basvurulardan uretilebilir.", 400);

        var mevcutVar = await _rezervasyonRepository.AnyAsync(x => x.KampBasvuruId == kampBasvuruId);
        if (mevcutVar)
            throw new BaseException("Bu basvuru icin zaten bir rezervasyon mevcut.", 400);

        var donem = await _dbContext.KampDonemleri
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == basvuru.KampDonemiId, cancellationToken)
            ?? throw new BaseException("Kamp donemi bulunamadi.", 404);

        var rezervasyonNo = await GenerateRezervasyonNoAsync(donem.Yil, cancellationToken);

        var rezervasyon = new KampRezervasyon
        {
            RezervasyonNo = rezervasyonNo,
            KampBasvuruId = basvuru.Id,
            KampDonemiId = basvuru.KampDonemiId,
            TesisId = basvuru.TesisId,
            BasvuruSahibiAdiSoyadi = basvuru.BasvuruSahibiAdiSoyadiSnapshot,
            BasvuruSahibiTipi = basvuru.BasvuruSahibiTipiSnapshot,
            KonaklamaBirimiTipi = basvuru.KonaklamaBirimiTipi,
            KatilimciSayisi = basvuru.KatilimciSayisi,
            DonemToplamTutar = basvuru.DonemToplamTutar,
            AvansToplamTutar = basvuru.AvansToplamTutar,
            Durum = KampRezervasyonDurumlari.Aktif
        };

        await _rezervasyonRepository.AddAsync(rezervasyon);
        await _rezervasyonRepository.SaveChangesAsync();

        return new KampRezervasyonUretSonucDto
        {
            Id = rezervasyon.Id,
            RezervasyonNo = rezervasyon.RezervasyonNo
        };
    }

    public async Task IptalEtAsync(int id, KampRezervasyonIptalRequestDto request, CancellationToken cancellationToken = default)
    {
        var rezervasyon = await _rezervasyonRepository.GetByIdAsync(id)
            ?? throw new BaseException("Rezervasyon bulunamadi.", 404);

        if (rezervasyon.Durum == KampRezervasyonDurumlari.IptalEdildi)
            throw new BaseException("Bu rezervasyon zaten iptal edilmis.", 400);

        rezervasyon.Durum = KampRezervasyonDurumlari.IptalEdildi;
        rezervasyon.IptalNedeni = request.IptalNedeni?.Trim();
        rezervasyon.IptalTarihi = DateTime.UtcNow;

        _rezervasyonRepository.Update(rezervasyon);
        await _rezervasyonRepository.SaveChangesAsync();
    }

    private async Task<string> GenerateRezervasyonNoAsync(int yil, CancellationToken cancellationToken)
    {
        var prefix = $"KAMP-{yil}-";
        var mevcutSayac = await _dbContext.KampRezervasyonlari
            .AsNoTracking()
            .CountAsync(x => x.RezervasyonNo.StartsWith(prefix), cancellationToken);

        return $"{prefix}{(mevcutSayac + 1):D4}";
    }
}
