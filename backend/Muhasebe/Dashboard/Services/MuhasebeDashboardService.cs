using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Dashboard.Dtos;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.Dashboard.Services;

public class MuhasebeDashboardService : IMuhasebeDashboardService
{
    private readonly StysAppDbContext _db;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public MuhasebeDashboardService(StysAppDbContext db, IUserAccessScopeService userAccessScopeService)
    {
        _db = db;
        _userAccessScopeService = userAccessScopeService;
    }

    public async Task<MuhasebeDashboardDto> GetDashboardAsync(
        MuhasebeDashboardFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var maliYil = filter.MaliYil ?? DateTime.UtcNow.Year;
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var accessibleTesisIds = scope.IsScoped ? scope.TesisIds.OrderBy(x => x).ToArray() : [];

        if (filter.TesisId.HasValue)
        {
            EnsureCanAccessTesis(scope, filter.TesisId.Value);
        }

        var fisQuery = _db.MuhasebeFisler.AsQueryable();
        if (filter.TesisId.HasValue)
        {
            fisQuery = fisQuery.Where(f => f.TesisId == filter.TesisId.Value);
        }
        else if (scope.IsScoped)
        {
            fisQuery = fisQuery.Where(f => accessibleTesisIds.Contains(f.TesisId));
        }

        fisQuery = fisQuery.Where(f => f.MaliYil == maliYil);
        if (filter.Donem.HasValue)
            fisQuery = fisQuery.Where(f => f.Donem == filter.Donem.Value);

        var donemQuery = _db.MuhasebeDonemler.AsQueryable();
        if (filter.TesisId.HasValue)
        {
            donemQuery = donemQuery.Where(d => d.TesisId == filter.TesisId.Value);
        }
        else if (scope.IsScoped)
        {
            donemQuery = donemQuery.Where(d => accessibleTesisIds.Contains(d.TesisId));
        }

        donemQuery = donemQuery.Where(d => d.MaliYil == maliYil);
        if (filter.Donem.HasValue)
            donemQuery = donemQuery.Where(d => d.DonemNo == filter.Donem.Value);

        // Sayılar
        var acikDonemSayisi = await donemQuery.CountAsync(d => !d.KapaliMi, cancellationToken);
        var kapaliDonemSayisi = await donemQuery.CountAsync(d => d.KapaliMi, cancellationToken);

        var taslakSayisi = await fisQuery.CountAsync(f => f.Durum == MuhasebeFisDurumlari.Taslak, cancellationToken);
        var onayliSayisi = await fisQuery.CountAsync(f => f.Durum == MuhasebeFisDurumlari.Onayli, cancellationToken);
        var iptalSayisi = await fisQuery.CountAsync(f => f.Durum == MuhasebeFisDurumlari.Iptal, cancellationToken);
        var tersKayitSayisi = await fisQuery.CountAsync(f => f.Durum == MuhasebeFisDurumlari.TersKayit, cancellationToken);

        // Dengesiz taslak: Durum=Taslak ve ToplamBorc != ToplamAlacak (0.009 tolerans)
        var dengesizTaslakSayisi = await fisQuery
            .Where(f => f.Durum == MuhasebeFisDurumlari.Taslak
                        && Math.Abs(f.ToplamBorc - f.ToplamAlacak) > 0.009m)
            .CountAsync(cancellationToken);

        // ToplamBorc/ToplamAlacak: sadece Onayli + TersKayit
        var hareketFisleri = await fisQuery
            .Where(f => f.Durum == MuhasebeFisDurumlari.Onayli
                        || f.Durum == MuhasebeFisDurumlari.TersKayit)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                ToplamBorc = g.Sum(f => f.ToplamBorc),
                ToplamAlacak = g.Sum(f => f.ToplamAlacak)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var toplamBorc = hareketFisleri?.ToplamBorc ?? 0m;
        var toplamAlacak = hareketFisleri?.ToplamAlacak ?? 0m;
        var fark = toplamBorc - toplamAlacak;

        // Açık dönemler
        var acikDonemler = await donemQuery
            .Where(d => !d.KapaliMi)
            .OrderBy(d => d.TesisId)
            .ThenBy(d => d.DonemNo)
            .Select(d => new MuhasebeDashboardDonemOzetDto
            {
                Id = d.Id,
                TesisId = d.TesisId,
                MaliYil = d.MaliYil,
                DonemNo = d.DonemNo,
                BaslangicTarihi = d.BaslangicTarihi,
                BitisTarihi = d.BitisTarihi,
                KapaliMi = d.KapaliMi
            })
            .ToListAsync(cancellationToken);

        // Son 10 fiş (FisTarihi desc, CreatedAt desc)
        var sonFisler = await fisQuery
            .OrderByDescending(f => f.FisTarihi)
            .ThenByDescending(f => f.CreatedAt)
            .Take(10)
            .Select(f => new MuhasebeDashboardFisOzetDto
            {
                Id = f.Id,
                TesisId = f.TesisId,
                FisNo = f.FisNo,
                YevmiyeNo = f.YevmiyeNo,
                FisTarihi = f.FisTarihi,
                MaliYil = f.MaliYil,
                Donem = f.Donem,
                FisTipi = f.FisTipi,
                Durum = f.Durum,
                ToplamBorc = f.ToplamBorc,
                ToplamAlacak = f.ToplamAlacak,
                Aciklama = f.Aciklama
            })
            .ToListAsync(cancellationToken);

        // Uyarılar
        var uyarilar = new List<MuhasebeDashboardUyariDto>();

        if (dengesizTaslakSayisi > 0)
        {
            uyarilar.Add(new MuhasebeDashboardUyariDto
            {
                Tip = "DengesizTaslak",
                Mesaj = $"{dengesizTaslakSayisi} adet dengesiz taslak fiş bulunmaktadır.",
                Route = "muhasebe/fisler",
                Severity = "warn"
            });
        }

        if (taslakSayisi > 0)
        {
            uyarilar.Add(new MuhasebeDashboardUyariDto
            {
                Tip = "TaslakFis",
                Mesaj = $"{taslakSayisi} adet taslak fiş onay beklemektedir.",
                Route = "muhasebe/fisler",
                Severity = "info"
            });
        }

        if (acikDonemSayisi == 0)
        {
            uyarilar.Add(new MuhasebeDashboardUyariDto
            {
                Tip = "AcikDonemYok",
                Mesaj = "Seçili kriterlere uygun açık dönem bulunmamaktadır.",
                Route = "muhasebe/donemler",
                Severity = "warn"
            });
        }

        // Aynı mali yılda birden fazla açık dönem kontrolü
        var acikDonemTesisGruplar = acikDonemler
            .GroupBy(d => d.TesisId)
            .Where(g => g.Count() > 1)
            .ToList();
        if (acikDonemTesisGruplar.Count > 0)
        {
            uyarilar.Add(new MuhasebeDashboardUyariDto
            {
                Tip = "CokluAcikDonem",
                Mesaj = "Bazı tesislerde aynı mali yıl içinde birden fazla açık dönem bulunmaktadır.",
                Route = "muhasebe/donemler",
                Severity = "info"
            });
        }

        // Kapalı döneme yakın bitiş tarihi olan dönem kontrolü (30 gün)
        var bugun = DateTime.UtcNow.Date;
        var yaklasanDonemler = acikDonemler
            .Where(d => (d.BitisTarihi.Date - bugun).TotalDays is >= 0 and <= 30)
            .ToList();
        if (yaklasanDonemler.Count > 0)
        {
            uyarilar.Add(new MuhasebeDashboardUyariDto
            {
                Tip = "YaklasanDonemKapanisi",
                Mesaj = $"{yaklasanDonemler.Count} dönemin kapanış tarihi yaklaşmaktadır (30 gün içinde).",
                Route = "muhasebe/donemler",
                Severity = "info"
            });
        }

        return new MuhasebeDashboardDto
        {
            TesisId = filter.TesisId,
            MaliYil = maliYil,
            Donem = filter.Donem,
            AcikDonemSayisi = acikDonemSayisi,
            KapaliDonemSayisi = kapaliDonemSayisi,
            TaslakFisSayisi = taslakSayisi,
            OnayliFisSayisi = onayliSayisi,
            IptalFisSayisi = iptalSayisi,
            TersKayitFisSayisi = tersKayitSayisi,
            DengesizTaslakFisSayisi = dengesizTaslakSayisi,
            ToplamBorc = toplamBorc,
            ToplamAlacak = toplamAlacak,
            Fark = fark,
            AcikDonemler = acikDonemler,
            SonFisler = sonFisler,
            Uyarilar = uyarilar
        };
    }

    private static void EnsureCanAccessTesis(DomainAccessScope scope, int tesisId)
    {
        if (!scope.IsScoped)
        {
            return;
        }

        if (!scope.TesisIds.Contains(tesisId))
        {
            throw new BaseException("Seçilen tesis için yetkiniz bulunmuyor.", 403);
        }
    }
}
