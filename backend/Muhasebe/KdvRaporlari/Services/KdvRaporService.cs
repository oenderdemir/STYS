using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.KdvRaporlari.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.KdvRaporlari.Services;

public sealed class KdvRaporService : IKdvRaporService
{
    private const int MaxRowCount = 100_000;

    private readonly StysAppDbContext _db;
    private readonly IUserAccessScopeService _accessScopeService;

    public KdvRaporService(StysAppDbContext db, IUserAccessScopeService accessScopeService)
    {
        _db = db;
        _accessScopeService = accessScopeService;
    }

    public async Task<KdvOzetRaporDto> GetOzetAsync(KdvRaporFilterDto filter, CancellationToken cancellationToken = default)
    {
        var lines = await LoadLinesAsync(filter, cancellationToken);
        var ozet = BuildKdvOzet(lines);
        return new KdvOzetRaporDto
        {
            BaslangicTarihi = ResolveDateRange(filter).baslangic,
            BitisTarihi = ResolveDateRange(filter).bitis,
            Ozet = ozet,
            OranOzetleri = BuildKdvOranOzetleri(lines),
            IstisnaOzetleri = BuildKdvIstisnaOzetleri(lines)
        };
    }

    public async Task<TevkifatOzetRaporDto> GetTevkifatOzetAsync(KdvRaporFilterDto filter, CancellationToken cancellationToken = default)
    {
        var lines = await LoadLinesAsync(filter, cancellationToken);
        var tevkifatLines = lines.Where(IsTevkifatli).ToList();
        return new TevkifatOzetRaporDto
        {
            BaslangicTarihi = ResolveDateRange(filter).baslangic,
            BitisTarihi = ResolveDateRange(filter).bitis,
            SatisTevkifatToplam = tevkifatLines.Where(x => IsSatisBelgesi(x.BelgeTipi)).Sum(x => x.TevkifatTutari),
            AlisTevkifatToplam = tevkifatLines.Where(x => IsAlisBelgesi(x.BelgeTipi)).Sum(x => x.TevkifatTutari),
            NetTevkifat = tevkifatLines.Where(x => IsSatisBelgesi(x.BelgeTipi)).Sum(x => x.TevkifatTutari)
                          - tevkifatLines.Where(x => IsAlisBelgesi(x.BelgeTipi)).Sum(x => x.TevkifatTutari),
            ToplamKayitSayisi = tevkifatLines.Count,
            OranOzetleri = BuildTevkifatOranOzetleri(tevkifatLines)
        };
    }

    public async Task<KdvHareketRaporDto> GetHareketlerAsync(KdvRaporFilterDto filter, CancellationToken cancellationToken = default)
    {
        var lines = await LoadLinesAsync(filter, cancellationToken);
        var reportLines = lines.Select(ToKdvHareketSatiri).OrderByDescending(x => x.BelgeTarihi).ThenByDescending(x => x.BelgeId).ThenBy(x => x.SatirId).ToList();
        return new KdvHareketRaporDto
        {
            BaslangicTarihi = ResolveDateRange(filter).baslangic,
            BitisTarihi = ResolveDateRange(filter).bitis,
            ToplamKayitSayisi = reportLines.Count,
            Ozet = new KdvHareketRaporOzetDto
            {
                SatisKayitSayisi = lines.Count(x => IsSatisBelgesi(x.BelgeTipi)),
                AlisKayitSayisi = lines.Count(x => IsAlisBelgesi(x.BelgeTipi)),
                IadeKayitSayisi = lines.Count(x => IsSatisIadeBelgesi(x.BelgeTipi) || IsAlisIadeBelgesi(x.BelgeTipi)),
                IstisnaKayitSayisi = lines.Count(x => IsIstisnaTipi(x.KdvUygulamaTipi)),
                TevkifatKayitSayisi = lines.Count(IsTevkifatli),
                ToplamMatrah = lines.Sum(x => x.Matrah),
                ToplamKdvTutari = lines.Sum(x => x.KdvTutari)
            },
            Satirlar = reportLines
        };
    }

    public async Task<TevkifatHareketRaporDto> GetTevkifatHareketlerAsync(KdvRaporFilterDto filter, CancellationToken cancellationToken = default)
    {
        var lines = await LoadLinesAsync(filter, cancellationToken);
        var reportLines = lines.Where(IsTevkifatli).Select(ToTevkifatHareketSatiri).OrderByDescending(x => x.BelgeTarihi).ThenByDescending(x => x.BelgeId).ThenBy(x => x.SatirId).ToList();
        return new TevkifatHareketRaporDto
        {
            BaslangicTarihi = ResolveDateRange(filter).baslangic,
            BitisTarihi = ResolveDateRange(filter).bitis,
            ToplamKayitSayisi = reportLines.Count,
            Ozet = new TevkifatHareketRaporOzetDto
            {
                SatisKayitSayisi = reportLines.Count(x => x.IslemYonu == "Satis"),
                AlisKayitSayisi = reportLines.Count(x => x.IslemYonu == "Alis"),
                ToplamMatrah = reportLines.Sum(x => x.Matrah),
                ToplamTevkifatTutari = reportLines.Sum(x => x.TevkifatTutari)
            },
            Satirlar = reportLines
        };
    }

    private async Task<List<RaporSatiri>> LoadLinesAsync(KdvRaporFilterDto filter, CancellationToken cancellationToken)
    {
        var (baslangic, bitis) = ResolveDateRange(filter);
        var scope = await _accessScopeService.GetCurrentScopeAsync(cancellationToken);

        var belgeQuery = _db.SatisBelgeleri
            .AsNoTracking()
            .Include(x => x.Satirlar)
            .Where(x => !x.IsDeleted)
            .Where(x => x.BelgeTarihi >= baslangic && x.BelgeTarihi <= bitis)
            .Where(x => x.BelgeTipi != SatisBelgesiTipi.Proforma);

        if (scope.IsScoped)
            belgeQuery = belgeQuery.Where(x => !x.TesisId.HasValue || scope.TesisIds.Contains(x.TesisId.Value));

        if (filter.TesisId.HasValue)
        {
            if (scope.IsScoped && !scope.TesisIds.Contains(filter.TesisId.Value))
                throw new BaseException("Bu tesis için rapor görüntüleme yetkiniz bulunmamaktadır.", 403);

            belgeQuery = belgeQuery.Where(x => x.TesisId == filter.TesisId.Value);
        }

        belgeQuery = ApplyBelgeYonuFilter(belgeQuery, filter.BelgeYonu);

        var belgeler = await belgeQuery.ToListAsync(cancellationToken);

        var lines = belgeler
            .SelectMany(belge =>
                belge.Satirlar
                    .Where(satir => !satir.IsDeleted)
                    .Where(satir => filter.IstisnalarDahilMi || !IsIstisnaTipi(satir.KdvUygulamaTipi))
                    .Where(satir => filter.TevkifatDahilMi || !IsTevkifatli(satir)))
            .Select(satir => new RaporSatiri(
                satir.SatisBelgesiId,
                satir.SatisBelgesi.BelgeNo,
                satir.SatisBelgesi.BelgeTarihi,
                satir.SatisBelgesi.BelgeTipi,
                satir.SiraNo,
                satir.Aciklama,
                satir.Matrah,
                satir.KdvOrani,
                satir.KdvTutari,
                satir.KdvUygulamaTipi,
                satir.KdvIstisnaTanimId,
                satir.KdvIstisnaKodu,
                satir.KdvIstisnaAciklamasi,
                satir.TevkifatPay,
                satir.TevkifatPayda,
                satir.TevkifatTutari))
            .ToList();

        if (lines.Count > MaxRowCount)
            throw new BaseException($"Rapor için maksimum {MaxRowCount:N0} satır desteklenmektedir.", 400);

        return lines;
    }

    private static IQueryable<SatisBelgesi> ApplyBelgeYonuFilter(IQueryable<SatisBelgesi> query, string? belgeYonu)
    {
        if (string.IsNullOrWhiteSpace(belgeYonu) || belgeYonu.Equals("Hepsi", StringComparison.OrdinalIgnoreCase))
            return query;

        if (belgeYonu.Equals("Satis", StringComparison.OrdinalIgnoreCase))
            return query.Where(x => x.BelgeTipi == SatisBelgesiTipi.FaturaTaslagi || x.BelgeTipi == SatisBelgesiTipi.SatisFaturasi);

        if (belgeYonu.Equals("Alis", StringComparison.OrdinalIgnoreCase))
            return query.Where(x => x.BelgeTipi == SatisBelgesiTipi.AlisFaturasi);

        if (belgeYonu.Equals("Iade", StringComparison.OrdinalIgnoreCase))
            return query.Where(x =>
                x.BelgeTipi == SatisBelgesiTipi.SatisIadeFaturasi
                || x.BelgeTipi == SatisBelgesiTipi.AlisIadeFaturasi
                || x.BelgeTipi == SatisBelgesiTipi.IadeFaturasi);

        throw new BaseException("Geçersiz belge yönü filtresi.", 400);
    }

    private static (DateTime baslangic, DateTime bitis) ResolveDateRange(KdvRaporFilterDto filter)
    {
        var now = DateTime.UtcNow;
        var baslangic = filter.BaslangicTarihi ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var bitis = filter.BitisTarihi ?? baslangic.AddMonths(1).AddSeconds(-1);

        if (bitis < baslangic)
            throw new BaseException("Bitiş tarihi başlangıç tarihinden önce olamaz.", 400);

        return (baslangic, bitis);
    }

    private static KdvOzetRaporOzetDto BuildKdvOzet(List<RaporSatiri> lines)
    {
        var satisLines = lines.Where(x => IsSatisBelgesi(x.BelgeTipi)).ToList();
        var alisLines = lines.Where(x => IsAlisBelgesi(x.BelgeTipi)).ToList();
        var satisIadeLines = lines.Where(x => IsSatisIadeBelgesi(x.BelgeTipi)).ToList();
        var alisIadeLines = lines.Where(x => IsAlisIadeBelgesi(x.BelgeTipi)).ToList();

        var satisMatrah = satisLines.Sum(x => x.Matrah);
        var satisKdv = satisLines.Sum(x => x.KdvTutari);
        var alisMatrah = alisLines.Sum(x => x.Matrah);
        var alisKdv = alisLines.Sum(x => x.KdvTutari);
        var satisIadeMatrah = satisIadeLines.Sum(x => x.Matrah);
        var satisIadeKdv = satisIadeLines.Sum(x => x.KdvTutari);
        var alisIadeMatrah = alisIadeLines.Sum(x => x.Matrah);
        var alisIadeKdv = alisIadeLines.Sum(x => x.KdvTutari);
        var istisnaMatrah = lines.Where(x => IsIstisnaTipi(x.KdvUygulamaTipi)).Sum(x => x.Matrah);
        var tevkifatToplam = lines.Where(IsTevkifatli).Sum(x => x.TevkifatTutari);

        return new KdvOzetRaporOzetDto
        {
            ToplamKayitSayisi = lines.Count,
            SatisKayitSayisi = satisLines.Count,
            AlisKayitSayisi = alisLines.Count,
            IadeKayitSayisi = satisIadeLines.Count + alisIadeLines.Count,
            SatisMatrahToplam = satisMatrah,
            HesaplananKdvToplam = satisKdv,
            AlisMatrahToplam = alisMatrah,
            IndirilecekKdvToplam = alisKdv,
            SatisIadeMatrahToplam = satisIadeMatrah,
            SatisIadeKdvToplam = satisIadeKdv,
            AlisIadeMatrahToplam = alisIadeMatrah,
            AlisIadeKdvToplam = alisIadeKdv,
            IstisnaMatrahToplam = istisnaMatrah,
            TevkifatToplam = tevkifatToplam,
            NetKdv = (satisKdv - satisIadeKdv) - (alisKdv - alisIadeKdv)
        };
    }

    private static List<KdvOranOzetDto> BuildKdvOranOzetleri(List<RaporSatiri> lines)
    {
        return lines
            .Where(x => !IsTevkifatli(x))
            .GroupBy(x => new { x.BelgeTipi, x.KdvOrani })
            .Select(g => new KdvOranOzetDto
            {
                IslemYonu = GetBelgeYonuLabel(g.Key.BelgeTipi),
                KdvOrani = g.Key.KdvOrani,
                HareketSayisi = g.Count(),
                Matrah = g.Sum(x => x.Matrah),
                KdvTutari = g.Sum(x => x.KdvTutari)
            })
            .OrderBy(x => x.IslemYonu)
            .ThenBy(x => x.KdvOrani)
            .ToList();
    }

    private static List<KdvIstisnaOzetDto> BuildKdvIstisnaOzetleri(List<RaporSatiri> lines)
    {
        return lines
            .Where(x => IsIstisnaTipi(x.KdvUygulamaTipi))
            .Where(x => !string.IsNullOrWhiteSpace(x.KdvIstisnaKodu))
            .GroupBy(x => new { x.BelgeTipi, x.KdvIstisnaKodu, x.KdvIstisnaAciklamasi })
            .Select(g => new KdvIstisnaOzetDto
            {
                IslemYonu = GetBelgeYonuLabel(g.Key.BelgeTipi),
                KdvIstisnaKodu = g.Key.KdvIstisnaKodu,
                KdvIstisnaAciklamasi = g.Key.KdvIstisnaAciklamasi,
                HareketSayisi = g.Count(),
                Matrah = g.Sum(x => x.Matrah)
            })
            .OrderBy(x => x.IslemYonu)
            .ThenBy(x => x.KdvIstisnaKodu)
            .ToList();
    }

    private static List<TevkifatOranOzetDto> BuildTevkifatOranOzetleri(List<RaporSatiri> lines)
    {
        return lines
            .Where(IsTevkifatli)
            .GroupBy(x => new { x.BelgeTipi, Pay = x.TevkifatPay ?? 0, Payda = x.TevkifatPayda ?? 0 })
            .Select(g => new TevkifatOranOzetDto
            {
                IslemYonu = GetBelgeYonuLabel(g.Key.BelgeTipi),
                TevkifatPay = g.Key.Pay,
                TevkifatPayda = g.Key.Payda,
                HareketSayisi = g.Count(),
                Matrah = g.Sum(x => x.Matrah),
                TevkifatTutari = g.Sum(x => x.TevkifatTutari)
            })
            .OrderBy(x => x.IslemYonu)
            .ThenBy(x => x.TevkifatPay)
            .ThenBy(x => x.TevkifatPayda)
            .ToList();
    }

    private static KdvHareketRaporSatiriDto ToKdvHareketSatiri(RaporSatiri satir)
    {
        return new KdvHareketRaporSatiriDto
        {
            BelgeId = satir.BelgeId,
            BelgeNo = satir.BelgeNo,
            BelgeTarihi = satir.BelgeTarihi,
            BelgeTipi = satir.BelgeTipi.ToString(),
            IslemYonu = GetBelgeYonuLabel(satir.BelgeTipi),
            SatirId = satir.SatirId,
            SatirAciklama = satir.SatirAciklama,
            Matrah = satir.Matrah,
            KdvOrani = satir.KdvOrani,
            KdvTutari = satir.KdvTutari,
            KdvUygulamaTipi = KdvUygulamaTipiLabel(satir.KdvUygulamaTipi),
            KdvIstisnaTanimId = satir.KdvIstisnaTanimId,
            KdvIstisnaKodu = satir.KdvIstisnaKodu,
            KdvIstisnaAciklamasi = satir.KdvIstisnaAciklamasi,
            TevkifatPay = satir.TevkifatPay,
            TevkifatPayda = satir.TevkifatPayda,
            TevkifatTutari = satir.TevkifatTutari
        };
    }

    private static TevkifatHareketRaporSatiriDto ToTevkifatHareketSatiri(RaporSatiri satir)
    {
        return new TevkifatHareketRaporSatiriDto
        {
            BelgeId = satir.BelgeId,
            BelgeNo = satir.BelgeNo,
            BelgeTarihi = satir.BelgeTarihi,
            BelgeTipi = satir.BelgeTipi.ToString(),
            IslemYonu = GetBelgeYonuLabel(satir.BelgeTipi),
            SatirId = satir.SatirId,
            SatirAciklama = satir.SatirAciklama,
            Matrah = satir.Matrah,
            KdvTutari = satir.KdvTutari,
            TevkifatPay = satir.TevkifatPay ?? 0,
            TevkifatPayda = satir.TevkifatPayda ?? 0,
            TevkifatTutari = satir.TevkifatTutari
        };
    }

    private static string GetBelgeYonuLabel(SatisBelgesiTipi belgeTipi)
    {
        return belgeTipi switch
        {
            SatisBelgesiTipi.FaturaTaslagi => "Satis",
            SatisBelgesiTipi.SatisFaturasi => "Satis",
            SatisBelgesiTipi.AlisFaturasi => "Alis",
            SatisBelgesiTipi.SatisIadeFaturasi => "Iade",
            SatisBelgesiTipi.AlisIadeFaturasi => "Iade",
            SatisBelgesiTipi.IadeFaturasi => "Iade",
            _ => "Hepsi"
        };
    }

    private static bool IsSatisBelgesi(SatisBelgesiTipi belgeTipi)
        => belgeTipi is SatisBelgesiTipi.FaturaTaslagi or SatisBelgesiTipi.SatisFaturasi;

    private static bool IsAlisBelgesi(SatisBelgesiTipi belgeTipi)
        => belgeTipi == SatisBelgesiTipi.AlisFaturasi;

    private static bool IsSatisIadeBelgesi(SatisBelgesiTipi belgeTipi)
        => belgeTipi is SatisBelgesiTipi.SatisIadeFaturasi or SatisBelgesiTipi.IadeFaturasi;

    private static bool IsAlisIadeBelgesi(SatisBelgesiTipi belgeTipi)
        => belgeTipi == SatisBelgesiTipi.AlisIadeFaturasi;

    private static bool IsIstisnaTipi(KdvUygulamaTipi uygulamaTipi)
        => uygulamaTipi is KdvUygulamaTipi.TamIstisna or KdvUygulamaTipi.KismiIstisna or KdvUygulamaTipi.KdvKapsamDisi;

    private static bool IsTevkifatli(RaporSatiri satir)
        => satir.KdvUygulamaTipi == KdvUygulamaTipi.Tevkifatli || satir.TevkifatTutari > 0;

    private static bool IsTevkifatli(SatisBelgesiSatiri satir)
        => satir.KdvUygulamaTipi == KdvUygulamaTipi.Tevkifatli || satir.TevkifatTutari > 0;

    private static string KdvUygulamaTipiLabel(KdvUygulamaTipi uygulamaTipi)
    {
        return uygulamaTipi switch
        {
            KdvUygulamaTipi.Kdvli => "KDV'li",
            KdvUygulamaTipi.TamIstisna => "Tam İstisna",
            KdvUygulamaTipi.KismiIstisna => "Kısmi İstisna",
            KdvUygulamaTipi.KdvKapsamDisi => "KDV Kapsam Dışı",
            KdvUygulamaTipi.Tevkifatli => "Tevkifatlı",
            _ => "Bilinmiyor"
        };
    }

    private sealed record RaporSatiri(
        int BelgeId,
        string BelgeNo,
        DateTime BelgeTarihi,
        SatisBelgesiTipi BelgeTipi,
        int SatirId,
        string SatirAciklama,
        decimal Matrah,
        decimal KdvOrani,
        decimal KdvTutari,
        KdvUygulamaTipi KdvUygulamaTipi,
        int? KdvIstisnaTanimId,
        string? KdvIstisnaKodu,
        string? KdvIstisnaAciklamasi,
        int? TevkifatPay,
        int? TevkifatPayda,
        decimal TevkifatTutari);
}
