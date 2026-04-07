using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Kamp.Dto;
using STYS.Kamp.Entities;
using STYS.Kamp.Repositories;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Entities;
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
            .Where(x => x.KampProgrami != null && x.KampProgrami.AktifMi)
            .OrderByDescending(x => x.Yil)
            .ThenBy(x => x.KonaklamaBaslangicTarihi)
            .Select(x => new KampRezervasyonDonemSecenekDto
            {
                Id = x.Id,
                KampProgramiAd = x.KampProgrami != null ? x.KampProgrami.Ad : null,
                Ad = x.Ad
            })
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
            .Where(x => x.KampDonemi != null && x.KampDonemi.KampProgrami != null && x.KampDonemi.KampProgrami.AktifMi)
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
            .Include(x => x.KampBasvuruSahibi)
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
        var girisTarihi = donem.KonaklamaBaslangicTarihi.Date;
        var cikisTarihi = donem.KonaklamaBitisTarihi.Date.AddDays(1);
        var odaAtamalari = await BuildAutoRoomAssignmentsAsync(
            basvuru.TesisId,
            basvuru.KonaklamaBirimiTipi,
            basvuru.KatilimciSayisi,
            girisTarihi,
            cikisTarihi,
            cancellationToken);

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

        var normalRezervasyon = new Rezervasyon
        {
            ReferansNo = rezervasyonNo,
            TesisId = basvuru.TesisId,
            KisiSayisi = basvuru.KatilimciSayisi,
            GirisTarihi = girisTarihi,
            CikisTarihi = cikisTarihi,
            TekKisilikFiyatUygulandiMi = false,
            ToplamBazUcret = basvuru.DonemToplamTutar,
            ToplamUcret = basvuru.DonemToplamTutar,
            ParaBirimi = "TRY",
            MisafirAdiSoyadi = basvuru.BasvuruSahibiAdiSoyadiSnapshot,
            MisafirTelefon = "0000000000",
            TcKimlikNo = basvuru.KampBasvuruSahibi?.TcKimlikNo,
            Notlar = $"Kamp rezervasyonundan otomatik olustu. KampBasvuruId={basvuru.Id}",
            RezervasyonDurumu = RezervasyonDurumlari.Onayli,
            AktifMi = true,
            Segmentler =
            [
                new RezervasyonSegment
                {
                    SegmentSirasi = 1,
                    BaslangicTarihi = girisTarihi,
                    BitisTarihi = cikisTarihi,
                    OdaAtamalari = odaAtamalari
                }
            ]
        };

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _dbContext.KampRezervasyonlari.AddAsync(rezervasyon, cancellationToken);
        await _dbContext.Rezervasyonlar.AddAsync(normalRezervasyon, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

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

        var normalRezervasyon = await _dbContext.Rezervasyonlar
            .FirstOrDefaultAsync(x => x.ReferansNo == rezervasyon.RezervasyonNo, cancellationToken);
        if (normalRezervasyon is not null && normalRezervasyon.RezervasyonDurumu != RezervasyonDurumlari.Iptal)
        {
            normalRezervasyon.RezervasyonDurumu = RezervasyonDurumlari.Iptal;
        }

        _rezervasyonRepository.Update(rezervasyon);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> GenerateRezervasyonNoAsync(int yil, CancellationToken cancellationToken)
    {
        var prefix = $"KAMP-{yil}-";
        var mevcutSayac = await _dbContext.KampRezervasyonlari
            .AsNoTracking()
            .CountAsync(x => x.RezervasyonNo.StartsWith(prefix), cancellationToken);
        var sayac = mevcutSayac + 1;

        while (true)
        {
            var aday = $"{prefix}{sayac:D4}";
            var kampVar = await _dbContext.KampRezervasyonlari.AsNoTracking().AnyAsync(x => x.RezervasyonNo == aday, cancellationToken);
            var normalVar = await _dbContext.Rezervasyonlar.AsNoTracking().AnyAsync(x => x.ReferansNo == aday, cancellationToken);
            if (!kampVar && !normalVar)
            {
                return aday;
            }

            sayac++;
        }
    }

    private async Task<ICollection<RezervasyonSegmentOdaAtama>> BuildAutoRoomAssignmentsAsync(
        int tesisId,
        string konaklamaBirimiTipi,
        int kisiSayisi,
        DateTime girisTarihi,
        DateTime cikisTarihi,
        CancellationToken cancellationToken)
    {
        var normalizedBinaAd = string.IsNullOrWhiteSpace(konaklamaBirimiTipi)
            ? null
            : konaklamaBirimiTipi.Trim();

        var binaBazliOdalar = await QueryAdayOdalarAsync(tesisId, normalizedBinaAd, cancellationToken);
        var adayOdalar = binaBazliOdalar.Count > 0
            ? binaBazliOdalar
            : await QueryAdayOdalarAsync(tesisId, null, cancellationToken);

        if (adayOdalar.Count == 0)
        {
            throw new BaseException("Kamp rezervasyonu icin uygun oda bulunamadi.", 400);
        }

        var odaIdler = adayOdalar.Select(x => x.OdaId).ToList();
        var occupancyByRoom = await (
                from atama in _dbContext.RezervasyonSegmentOdaAtamalari
                join segment in _dbContext.RezervasyonSegmentleri on atama.RezervasyonSegmentId equals segment.Id
                join rezervasyon in _dbContext.Rezervasyonlar on segment.RezervasyonId equals rezervasyon.Id
                where odaIdler.Contains(atama.OdaId)
                      && rezervasyon.RezervasyonDurumu != RezervasyonDurumlari.Iptal
                      && segment.BaslangicTarihi < cikisTarihi
                      && segment.BitisTarihi > girisTarihi
                group atama by atama.OdaId into grp
                select new
                {
                    OdaId = grp.Key,
                    Kisi = grp.Sum(x => x.AyrilanKisiSayisi)
                })
            .ToDictionaryAsync(x => x.OdaId, x => x.Kisi, cancellationToken);

        var sorted = adayOdalar
            .Select(x =>
            {
                var dolu = occupancyByRoom.GetValueOrDefault(x.OdaId);
                var kalan = x.PaylasimliMi
                    ? Math.Max(0, x.Kapasite - dolu)
                    : dolu > 0 ? 0 : x.Kapasite;
                return new
                {
                    x.OdaId,
                    x.OdaNo,
                    x.BinaAdi,
                    x.OdaTipiAdi,
                    x.Kapasite,
                    x.PaylasimliMi,
                    Kalan = kalan
                };
            })
            .Where(x => x.Kalan > 0)
            .OrderBy(x => x.PaylasimliMi)
            .ThenByDescending(x => x.Kalan)
            .ThenBy(x => x.OdaId)
            .ToList();

        var kalanKisi = kisiSayisi;
        var result = new List<RezervasyonSegmentOdaAtama>();
        foreach (var oda in sorted)
        {
            if (kalanKisi <= 0)
            {
                break;
            }

            var atanacak = Math.Min(oda.Kalan, kalanKisi);
            if (atanacak <= 0)
            {
                continue;
            }

            result.Add(new RezervasyonSegmentOdaAtama
            {
                OdaId = oda.OdaId,
                AyrilanKisiSayisi = atanacak,
                OdaNoSnapshot = oda.OdaNo,
                BinaAdiSnapshot = oda.BinaAdi,
                OdaTipiAdiSnapshot = oda.OdaTipiAdi,
                PaylasimliMiSnapshot = oda.PaylasimliMi,
                KapasiteSnapshot = oda.Kapasite
            });

            kalanKisi -= atanacak;
        }

        if (kalanKisi > 0)
        {
            throw new BaseException("Kamp rezervasyonu icin yeterli bos oda kapasitesi yok.", 400);
        }

        return result;
    }

    private async Task<List<AdayOdaDto>> QueryAdayOdalarAsync(int tesisId, string? binaAd, CancellationToken cancellationToken)
    {
        var query = _dbContext.Odalar
            .AsNoTracking()
            .Where(x => x.AktifMi
                && x.Bina != null
                && x.Bina.AktifMi
                && x.Bina.TesisId == tesisId
                && x.TesisOdaTipi != null
                && x.TesisOdaTipi.AktifMi);

        if (!string.IsNullOrWhiteSpace(binaAd))
        {
            var normalized = binaAd.Trim().ToLower();
            query = query.Where(x => x.Bina!.Ad.ToLower() == normalized);
        }

        return await query
            .Select(x => new AdayOdaDto
            {
                OdaId = x.Id,
                OdaNo = x.OdaNo,
                BinaAdi = x.Bina!.Ad,
                OdaTipiAdi = x.TesisOdaTipi!.Ad,
                Kapasite = x.TesisOdaTipi.Kapasite,
                PaylasimliMi = x.TesisOdaTipi.PaylasimliMi
            })
            .OrderBy(x => x.BinaAdi)
            .ThenBy(x => x.OdaNo)
            .ToListAsync(cancellationToken);
    }

    private sealed class AdayOdaDto
    {
        public int OdaId { get; set; }

        public string OdaNo { get; set; } = string.Empty;

        public string BinaAdi { get; set; } = string.Empty;

        public string OdaTipiAdi { get; set; } = string.Empty;

        public int Kapasite { get; set; }

        public bool PaylasimliMi { get; set; }
    }
}
