using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Dtos;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.CariKartlar.Services;
using STYS.Muhasebe.Kdv.Dtos;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.Kdv.Services;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Tesisler.Services;
using STYS.TicariBelgeler.Dtos;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.TicariBelgeler.Services;

/// <inheritdoc cref="ITicariBelgeLookupService" />
public class TicariBelgeLookupService : ITicariBelgeLookupService
{
    private const int IadeAdaylariMaxSonuc = 20;

    private readonly ITesisService _tesisService;
    private readonly ICariKartService _cariKartService;
    private readonly IKdvIstisnaTanimService _kdvIstisnaTanimService;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;
    private readonly StysAppDbContext _db;

    public TicariBelgeLookupService(
        ITesisService tesisService,
        ICariKartService cariKartService,
        IKdvIstisnaTanimService kdvIstisnaTanimService,
        IUserAccessScopeService userAccessScopeService,
        ICurrentTenantAccessor currentTenantAccessor,
        StysAppDbContext db)
    {
        _tesisService = tesisService;
        _cariKartService = cariKartService;
        _kdvIstisnaTanimService = kdvIstisnaTanimService;
        _userAccessScopeService = userAccessScopeService;
        _currentTenantAccessor = currentTenantAccessor;
        _db = db;
    }

    public async Task<List<TicariBelgeTesisLookupDto>> GetTesislerAsync(CancellationToken cancellationToken = default)
    {
        // TesisService.GetAktifKurumTesisleriAsync zaten aktif-kurum + scope-kesişim kuralını
        // uyguluyor (scope boşsa boş sonuç) - burada TEKRARLANMAZ, yalnızca minimal DTO'ya çevrilir.
        var tesisler = await _tesisService.GetAktifKurumTesisleriAsync(cancellationToken);
        return tesisler.Select(t => new TicariBelgeTesisLookupDto { Id = t.Id!.Value, Ad = t.Ad }).ToList();
    }

    public async Task<List<TicariBelgeCariKartLookupDto>> GetCariKartlarAsync(
        int tesisId, SatisBelgesiTipi belgeTipi, CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && !scope.TesisIds.Contains(tesisId))
        {
            throw new BaseException("Bu tesis için yetkiniz bulunmuyor.", 403);
        }

        var izinliCariTipleri = belgeTipi.IsAlisBelgesi()
            ? new HashSet<string> { CariKartTipleri.Tedarikci }
            : new HashSet<string> { CariKartTipleri.Musteri, CariKartTipleri.KurumsalMusteri };

        var cariKartlar = await _cariKartService.GetAllAsync(tesisId);
        return cariKartlar
            .Where(c => c.AktifMi && izinliCariTipleri.Contains(c.CariTipi))
            .OrderBy(c => c.CariKodu)
            .ThenBy(c => c.Id)
            .Select(ToCariKartLookupDto)
            .ToList();
    }

    public async Task<List<TicariBelgeKdvIstisnaLookupDto>> GetKdvIstisnalarAsync(
        TicariBelgeKdvIstisnaLookupFilterDto filter, CancellationToken cancellationToken = default)
    {
        // KDV'li ve tevkifatlı satırlarda istisna seçeneği YOKTUR - mevcut frontend kuralıyla
        // (bkz. ticari-belge-guncelle-dialog.getKdvIstisnaSecenekleri) aynı, tek kaynaktan (burada).
        if (filter.KdvUygulamaTipi is KdvUygulamaTipi.Kdvli or KdvUygulamaTipi.Tevkifatli)
        {
            return [];
        }

        var alis = filter.BelgeTipi.IsAlisBelgesi();
        var tanimFilter = new KdvIstisnaTanimFilterDto
        {
            UygulamaTipi = filter.KdvUygulamaTipi,
            AktifMi = true,
            SatisIslemlerindeKullanilirMi = alis ? null : true,
            AlisIslemlerindeKullanilirMi = alis ? true : null
        };

        var tanimlar = await _kdvIstisnaTanimService.FilterAsync(tanimFilter, cancellationToken);

        // Sunucu tarafında tarih-geçerlilik filtresi YOK (KdvIstisnaTanimFilterDto'da böyle bir
        // alan bulunmuyor) - bu yüzden burada, tek kaynaktan, belge tarihine göre uygulanır.
        return tanimlar
            .Where(t =>
                (!t.GecerlilikBaslangicTarihi.HasValue || t.GecerlilikBaslangicTarihi.Value.Date <= filter.BelgeTarihi.Date) &&
                (!t.GecerlilikBitisTarihi.HasValue || t.GecerlilikBitisTarihi.Value.Date >= filter.BelgeTarihi.Date))
            .OrderBy(t => t.Kod)
            .Select(t => new TicariBelgeKdvIstisnaLookupDto { Id = t.Id!.Value, Kod = t.Kod, Ad = t.Ad, UygulamaTipi = t.UygulamaTipi })
            .ToList();
    }

    public async Task<List<TicariBelgeIadeAdayiDto>> GetIadeAdaylariAsync(
        TicariBelgeIadeAdayiFilterDto filter, CancellationToken cancellationToken = default)
    {
        if (filter.BelgeTipi is not (SatisBelgesiTipi.SatisIadeFaturasi or SatisBelgesiTipi.AlisIadeFaturasi))
        {
            throw new BaseException("Aday sorgusu yalnızca iade belgesi tipleri için geçerlidir.", 400);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && !scope.TesisIds.Contains(filter.TesisId))
        {
            throw new BaseException("Bu tesis için yetkiniz bulunmuyor.", 403);
        }

        var currentKurumId = _currentTenantAccessor.GetCurrentKurumId();
        if (!currentKurumId.HasValue)
        {
            throw new BaseException("Aktif kurum bilgisi bulunamadı.", 400);
        }

        var alanBazliUygunluk = IadeEdilenBelgeEligibility.AlanBazliUygunlukIfadesi(
            filter.BelgeTipi, currentKurumId.Value, filter.CariKartId, filter.BelgeTarihi, filter.MevcutBelgeId);
        var fisUygunluk = IadeEdilenBelgeEligibility.FisUygunlukIfadesi(_db.MuhasebeFisler.AsNoTracking());

        var query = _db.SatisBelgeleri
            .AsNoTracking()
            .Where(x => x.TesisId == filter.TesisId)
            .Where(alanBazliUygunluk)
            .Where(fisUygunluk);

        if (!string.IsNullOrWhiteSpace(filter.BelgeNoArama))
        {
            var arama = filter.BelgeNoArama.Trim();
            query = query.Where(x => x.BelgeNo.Contains(arama));
        }

        var adaylar = await query
            .OrderByDescending(x => x.BelgeTarihi)
            .ThenByDescending(x => x.Id)
            .Take(IadeAdaylariMaxSonuc)
            .Select(x => new TicariBelgeIadeAdayiDto
            {
                Id = x.Id,
                BelgeNo = x.BelgeNo,
                BelgeTarihi = x.BelgeTarihi,
                ResmiFaturaNo = x.ResmiFaturaNo,
                KarsiTarafFaturaNo = x.KarsiTarafFaturaNo
            })
            .ToListAsync(cancellationToken);

        return adaylar;
    }

    public async Task<List<TicariBelgeKaynakSatirDto>> GetKaynakSatirlarAsync(
        int kaynakBelgeId, int? mevcutBelgeId, CancellationToken cancellationToken = default)
    {
        var kaynakBelge = await _db.SatisBelgeleri
            .AsNoTracking()
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
            .SingleOrDefaultAsync(x => x.Id == kaynakBelgeId, cancellationToken);

        if (kaynakBelge is null)
        {
            throw new BaseException("Kaynak belge bulunamadı.", 404);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && (!kaynakBelge.TesisId.HasValue || !scope.TesisIds.Contains(kaynakBelge.TesisId.Value)))
        {
            throw new BaseException("Bu belge için yetkiniz bulunmuyor.", 403);
        }

        var satirIdler = kaynakBelge.Satirlar.Select(s => s.Id).ToList();
        if (satirIdler.Count == 0)
        {
            return [];
        }

        var durumListesi = string.Join(",", IadeEdilenBelgeEligibility.IadeKumulatifSayilanMuhasebeDurumlari.Select(d => (int)d));
        var satirIdListesi = string.Join(",", satirIdler);
        var mevcutBelgeDisla = mevcutBelgeId.HasValue ? $"AND sb.[Id] <> {mevcutBelgeId.Value}" : string.Empty;

        var sql = $"""
            SELECT TRY_CAST(ssb.[KaynakSatirId] AS BIGINT) AS KaynakSatirId, SUM(ssb.[Miktar]) AS ToplamMiktar
            FROM [muhasebe].[SatisBelgesiSatirlari] ssb
            INNER JOIN [muhasebe].[SatisBelgeleri] sb ON sb.[Id] = ssb.[SatisBelgesiId]
            WHERE ssb.[IsDeleted] = 0
              AND sb.[IsDeleted] = 0
              AND sb.[MuhasebeDurumu] IN ({durumListesi})
              AND TRY_CAST(ssb.[KaynakSatirId] AS BIGINT) IN ({satirIdListesi})
              {mevcutBelgeDisla}
            GROUP BY TRY_CAST(ssb.[KaynakSatirId] AS BIGINT)
            """;

        var digerToplamlar = (await _db.Database
                .SqlQueryRaw<KaynakSatirToplamSatiri>(sql)
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.KaynakSatirId, x => x.ToplamMiktar);

        return kaynakBelge.Satirlar
            .OrderBy(s => s.SiraNo)
            .Select(s =>
            {
                var digerToplam = digerToplamlar.GetValueOrDefault(s.Id, 0m);
                var kalan = s.Miktar - digerToplam;
                return new TicariBelgeKaynakSatirDto
                {
                    Id = s.Id,
                    Aciklama = s.Aciklama,
                    Birim = s.Birim,
                    Miktar = s.Miktar,
                    IadeEdilebilirKalanMiktar = kalan < 0 ? 0 : kalan,
                    BirimFiyat = s.BirimFiyat,
                    IndirimOrani = s.IndirimOrani,
                    KdvUygulamaTipi = (int)s.KdvUygulamaTipi,
                    KdvOrani = s.KdvOrani,
                    KdvIstisnaTanimId = s.KdvIstisnaTanimId,
                    TevkifatPay = s.TevkifatPay,
                    TevkifatPayda = s.TevkifatPayda
                };
            })
            .ToList();
    }

    private static TicariBelgeCariKartLookupDto ToCariKartLookupDto(CariKartDto c) => new()
    {
        Id = c.Id!.Value,
        CariKodu = c.CariKodu,
        CariTipi = c.CariTipi,
        UnvanAdSoyad = c.UnvanAdSoyad,
        VergiNoTckn = c.VergiNoTckn,
        VergiDairesi = c.VergiDairesi,
        Adres = c.Adres,
        Eposta = c.Eposta,
        Telefon = c.Telefon,
        KurumsalMi = c.CariTipi == CariKartTipleri.KurumsalMusteri
    };

    private sealed class KaynakSatirToplamSatiri
    {
        public long KaynakSatirId { get; set; }
        public decimal ToplamMiktar { get; set; }
    }
}
