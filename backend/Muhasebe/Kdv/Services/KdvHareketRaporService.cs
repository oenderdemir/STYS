using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Dtos;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.StokHareketleri.Entities;

namespace STYS.Muhasebe.Kdv.Services;

public class KdvHareketRaporService : IKdvHareketRaporService
{
    private readonly StysAppDbContext _db;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public KdvHareketRaporService(
        StysAppDbContext db,
        IUserAccessScopeService userAccessScopeService)
    {
        _db = db;
        _userAccessScopeService = userAccessScopeService;
    }

    public async Task<KdvHareketRaporDto> GetRaporAsync(
        KdvHareketRaporFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);

        var stokQuery = _db.StokHareketleri
            .Include(s => s.Depo)
            .Include(s => s.TasinirKart)
            .Where(s => s.IsDeleted == false)
            .Where(s => s.HareketTarihi >= filter.BaslangicTarihi
                        && s.HareketTarihi <= filter.BitisTarihi);

        if (scope.IsScoped)
            stokQuery = stokQuery.Where(s =>
                s.Depo != null &&
                s.Depo.TesisId.HasValue &&
                scope.TesisIds.Contains(s.Depo.TesisId.Value));

        if (filter.TesisId.HasValue)
            stokQuery = stokQuery.Where(s => s.Depo != null && s.Depo.TesisId == filter.TesisId.Value);

        if (filter.DepoId.HasValue)
            stokQuery = stokQuery.Where(s => s.DepoId == filter.DepoId.Value);

        if (filter.TasinirKartId.HasValue)
            stokQuery = stokQuery.Where(s => s.TasinirKartId == filter.TasinirKartId.Value);

        if (!string.IsNullOrWhiteSpace(filter.HareketTipi))
            stokQuery = stokQuery.Where(s => s.HareketTipi == filter.HareketTipi.Trim());

        if (filter.KdvUygulamaTipi.HasValue)
            stokQuery = stokQuery.Where(s => s.KdvUygulamaTipi == (int)filter.KdvUygulamaTipi.Value);

        if (filter.KdvIstisnaTanimId.HasValue)
            stokQuery = stokQuery.Where(s => s.KdvIstisnaTanimId == filter.KdvIstisnaTanimId.Value);

        if (!string.IsNullOrWhiteSpace(filter.KdvIstisnaKodu))
        {
            var kod = filter.KdvIstisnaKodu.Trim();
            stokQuery = stokQuery.Where(s => s.KdvIstisnaKodu != null && s.KdvIstisnaKodu.Contains(kod));
        }

        // Muhasebe fiş left join: KaynakModul == "StokHareket" && KaynakId == StokHareket.Id && IsDeleted == false && Durum != Iptal
        var query = from stok in stokQuery
                    join musFis in _db.MuhasebeFisler
                        .Where(f => f.KaynakModul == "StokHareket" && f.IsDeleted == false && f.Durum != MuhasebeFisDurumlari.Iptal)
                        on stok.Id equals musFis.KaynakId into fisGroup
                    from fis in fisGroup.DefaultIfEmpty()
                    select new { stok, fis };

        // MusFisDurumu filtresi
        if (!string.IsNullOrWhiteSpace(filter.MusFisDurumu))
        {
            if (filter.MusFisDurumu == "FisiOlan")
                query = query.Where(x => x.fis != null);
            else if (filter.MusFisDurumu == "FisiOlmayan")
                query = query.Where(x => x.fis == null);
        }

        // Özet: tüm filtrelenmiş dataset üzerinden hesapla (Take öncesi)
        var ozetRaw = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                ToplamKayitSayisi = g.Count(),
                KdvliSayisi = g.Count(x => x.stok.KdvUygulamaTipi == 1),
                IstisnaliSayisi = g.Count(x => x.stok.KdvUygulamaTipi == 2 || x.stok.KdvUygulamaTipi == 3),
                KdvKapsamDisiSayisi = g.Count(x => x.stok.KdvUygulamaTipi == 4),
                TevkifatliSayisi = g.Count(x => x.stok.KdvUygulamaTipi == 5),
                FisiOlanSayisi = g.Count(x => x.fis != null),
                FisiOlmayanSayisi = g.Count(x => x.fis == null),
                ToplamKdvTutari = g.Sum(x => x.stok.KdvTutari),
                ToplamTutar = g.Sum(x => x.stok.Tutar)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var ozet = ozetRaw != null
            ? new KdvHareketRaporOzetDto
            {
                ToplamKayitSayisi = ozetRaw.ToplamKayitSayisi,
                KdvliSayisi = ozetRaw.KdvliSayisi,
                IstisnaliSayisi = ozetRaw.IstisnaliSayisi,
                KdvKapsamDisiSayisi = ozetRaw.KdvKapsamDisiSayisi,
                TevkifatliSayisi = ozetRaw.TevkifatliSayisi,
                FisiOlanSayisi = ozetRaw.FisiOlanSayisi,
                FisiOlmayanSayisi = ozetRaw.FisiOlmayanSayisi,
                ToplamKdvTutari = ozetRaw.ToplamKdvTutari,
                ToplamTutar = ozetRaw.ToplamTutar
            }
            : new KdvHareketRaporOzetDto();

        var totalCount = ozet.ToplamKayitSayisi;

        // Satırlar: ilk 1000 kayıt, tarih + id desc
        var rawItems = await query
            .OrderByDescending(x => x.stok.HareketTarihi)
            .ThenByDescending(x => x.stok.Id)
            .Take(1000)
            .Select(x => new KdvHareketRaporSatirDto
            {
                Id = x.stok.Id,
                HareketTarihi = x.stok.HareketTarihi,
                HareketTipi = x.stok.HareketTipi,
                DepoAdi = x.stok.Depo != null ? x.stok.Depo.Ad : string.Empty,
                TasinirKod = x.stok.TasinirKart != null ? x.stok.TasinirKart.StokKodu : string.Empty,
                TasinirAd = x.stok.TasinirKart != null ? x.stok.TasinirKart.Ad : string.Empty,
                Miktar = x.stok.Miktar,
                BirimFiyat = x.stok.BirimFiyat,
                Tutar = x.stok.Tutar,
                KdvUygulamaTipi = x.stok.KdvUygulamaTipi,
                KdvUygulamaTipiAd = KdvUygulamaTipiAdi(x.stok.KdvUygulamaTipi),
                KdvIstisnaKodu = x.stok.KdvIstisnaKodu,
                KdvIstisnaAciklamasi = x.stok.KdvIstisnaAciklamasi,
                KdvOrani = x.stok.KdvOrani,
                KdvTutari = x.stok.KdvTutari,
                KdvliTutar = x.stok.Tutar + x.stok.KdvTutari,
                MusFisId = x.fis != null ? x.fis.Id : null,
                MusFisNo = x.fis != null ? x.fis.FisNo : null,
                MusFisDurumu = x.fis != null ? x.fis.Durum : null,
                BelgeNo = x.stok.BelgeNo,
                Aciklama = x.stok.Aciklama
            })
            .ToListAsync(cancellationToken);

        return new KdvHareketRaporDto
        {
            Satirlar = rawItems,
            Ozet = ozet,
            ToplamKayitSayisi = totalCount
        };
    }

    private static string KdvUygulamaTipiAdi(int uygulamaTipi)
    {
        return uygulamaTipi switch
        {
            1 => "KDV'li",
            2 => "Tam İstisna",
            3 => "Kısmi İstisna",
            4 => "KDV Kapsam Dışı",
            5 => "Tevkifatlı",
            _ => "Bilinmiyor"
        };
    }
}
