using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.OdemeIzleme.Dtos;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.OdemeIzleme.Services;

/// <summary>
/// Odeme arastirmasini ODEME BELGESI MERKEZLI olmaktan cikaran capraz-kaynak arama.
///
/// SAYFALAMA TASARIMI: her kaynak icin ORTAK bir ham aday projeksiyonu (<see cref="AdayHam"/>)
/// uretilir ve bu projeksiyonlar <c>Concat</c> ile TEK bir IQueryable'da birlestirilir (EF Core
/// bunu SQL'de <c>UNION ALL</c>'a cevirir). Tekillestirme <c>GroupBy</c> ile, siralama ve sayfalama
/// <c>OrderBy/Skip/Take</c> ile VERITABANI TARAFINDA yapilir (SQL Server'da OFFSET/FETCH).
/// Bir sayfa icin belleye alinan satir sayisi = sayfa boyutu (+ detay zenginlestirme icin ayni
/// anahtarlara ait sinirli sayida satir). Kaynak tablolarin tamami ASLA belleye alinmaz.
/// </summary>
public class OdemeCaprazAramaService : IOdemeCaprazAramaService
{
    private const int MaksimumSayfaBoyutu = 200;

    private readonly StysAppDbContext _dbContext;
    private readonly IMuhasebeTesisScopeService _tesisScopeService;

    public OdemeCaprazAramaService(StysAppDbContext dbContext, IMuhasebeTesisScopeService tesisScopeService)
    {
        _dbContext = dbContext;
        _tesisScopeService = tesisScopeService;
    }

    /// <summary>Tum kaynaklarin ortak ham projeksiyonu - SQL'de UNION ALL ile birlesir.
    ///
    /// ONEMLI: positional record DEGIL, parametresiz ctor + set'li property'lerden olusan bir SINIF
    /// olmalidir. EF Core, constructor'a dayali projeksiyonu "client projection" sayar ve
    /// <c>Concat</c>/<c>Union</c> gibi set operasyonlarini CEVIREMEZ ("Unable to translate set
    /// operation after client projection has been applied"). Object-initializer ile yazilan
    /// projeksiyon sunucu tarafinda kalir ve UNION ALL uretilebilir.
    ///
    /// Kopukluk bayraklari int'tir (SQL Server'da bit uzerinde MAX desteklenmedigi icin).</summary>
    private sealed class AdayHam
    {
        public string Anahtar { get; set; } = string.Empty;
        public string Kaynak { get; set; } = string.Empty;
        public int KaynakId { get; set; }
        public DateTime? Tarih { get; set; }
        public decimal? Tutar { get; set; }
        public string? ParaBirimi { get; set; }
        public int? TesisId { get; set; }
        public int? CariKartId { get; set; }
        public int? KasaBankaHesapId { get; set; }
        public int? TahsilatOdemeBelgesiId { get; set; }
        public int? CariHareketId { get; set; }
        public int? PosTahsilatValorId { get; set; }
        public int? MuhasebeFisId { get; set; }
        public string? BelgeNo { get; set; }
        public int KopuklukFisYok { get; set; }
        public int KopuklukCariYok { get; set; }
        public int KopuklukValorYok { get; set; }
        public int KopuklukHedefHesapYok { get; set; }
        public int KopuklukOdemeBaglantisiYok { get; set; }
        public int KopuklukSoftDelete { get; set; }
        public int BagimsizKayit { get; set; }
    }

    public async Task<PagedResult<OdemeAdayiDto>> AraAsync(
        PagedRequest request, OdemeCaprazAramaFilterDto filter, CancellationToken cancellationToken = default)
    {
        DogrulaDaralticiAlanVarligi(filter);

        var (pageNumber, pageSizeIstenen) = request.Normalize();
        var pageSize = Math.Min(pageSizeIstenen, MaksimumSayfaBoyutu);

        var tesisIds = await ResolveTesisIdsAsync(filter.TesisId, cancellationToken);
        if (tesisIds.Count == 0)
        {
            return new PagedResult<OdemeAdayiDto>([], pageNumber, pageSize, 0);
        }

        var birlesik = BirlesikAdaySorgusu(tesisIds, filter);

        // TEKILLESTIRME + SIRALAMA + SAYFALAMA: hepsi SQL tarafinda.
        var grupli = birlesik
            .GroupBy(x => x.Anahtar)
            .Select(g => new
            {
                Anahtar = g.Key,
                Tarih = g.Max(x => x.Tarih),
                Tutar = g.Max(x => x.Tutar),
                FisYok = g.Max(x => x.KopuklukFisYok),
                CariYok = g.Max(x => x.KopuklukCariYok),
                ValorYok = g.Max(x => x.KopuklukValorYok),
                HedefHesapYok = g.Max(x => x.KopuklukHedefHesapYok),
                OdemeBaglantisiYok = g.Max(x => x.KopuklukOdemeBaglantisiYok),
                SoftDelete = g.Max(x => x.KopuklukSoftDelete),
                Bagimsiz = g.Max(x => x.BagimsizKayit)
            });

        // Kopukluk turu filtresi de SQL tarafinda uygulanir.
        grupli = filter.KopuklukTipi switch
        {
            OdemeKopuklukTipleri.MuhasebeFisiOlmayanOdemeBelgesi => grupli.Where(x => x.FisYok > 0),
            OdemeKopuklukTipleri.CariHareketEtkisiOlmayanOdemeBelgesi => grupli.Where(x => x.CariYok > 0),
            OdemeKopuklukTipleri.ValorKaydiOlmayanPosTahsilati => grupli.Where(x => x.ValorYok > 0),
            OdemeKopuklukTipleri.HedefBankaHesabiOlmayanValor => grupli.Where(x => x.HedefHesapYok > 0),
            OdemeKopuklukTipleri.OdemeBaglantisiOlmayanMuhasebeFisi => grupli.Where(x => x.OdemeBaglantisiYok > 0),
            OdemeKopuklukTipleri.SoftDeleteIliskiNedeniyleGorunmeyen => grupli.Where(x => x.SoftDelete > 0),
            _ => grupli
        };

        if (filter.SadeceKopukOlanlar)
        {
            // "Kopuk" = en az bir kopukluk bayragi VEYA hicbir odeme belgesine bagli olmayan
            // bagimsiz kayit (arastirmanin asil hedefi).
            grupli = grupli.Where(x => x.FisYok + x.CariYok + x.ValorYok + x.HedefHesapYok
                + x.OdemeBaglantisiYok + x.SoftDelete + x.Bagimsiz > 0);
        }

        // totalElements: filtre VE tekillestirme sonrasi gercek aday sayisi (SQL COUNT).
        var toplam = await grupli.CountAsync(cancellationToken);

        // KARARLI SIRALAMA: tarih (yeniden eskiye) + benzersiz ikincil anahtar (Anahtar).
        var sayfaAnahtarlari = await grupli
            .OrderByDescending(x => x.Tarih)
            .ThenBy(x => x.Anahtar)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => x.Anahtar)
            .ToListAsync(cancellationToken);

        if (sayfaAnahtarlari.Count == 0)
        {
            return new PagedResult<OdemeAdayiDto>([], pageNumber, pageSize, toplam);
        }

        // Zenginlestirme: YALNIZCA sayfadaki anahtarlara ait ham satirlar cekilir (sinirli).
        var sayfaSatirlari = await birlesik
            .Where(x => sayfaAnahtarlari.Contains(x.Anahtar))
            .ToListAsync(cancellationToken);

        var adaylar = Birlestir(sayfaSatirlari, sayfaAnahtarlari, filter);
        return new PagedResult<OdemeAdayiDto>(adaylar, pageNumber, pageSize, toplam);
    }

    /// <summary>
    /// Asiri genis (daraltici alani olmayan) finansal taramayi engeller. Bagimsiz kaynak
    /// arastirmasi, yetki kapsamindaki TUM banka/kasa/cari/fis kayitlarini tarayabildigi icin
    /// en az BIR guclu daraltici alan zorunludur.
    /// </summary>
    private static void DogrulaDaralticiAlanVarligi(OdemeCaprazAramaFilterDto filter)
    {
        var tarihAraligiVar = filter.TarihBaslangic.HasValue && filter.TarihBitis.HasValue;
        var tutarVar = filter.TutarMin.HasValue || filter.TutarMax.HasValue;

        var daralticiVar =
            filter.CariKartId.HasValue
            || !string.IsNullOrWhiteSpace(filter.BelgeNo)
            || !string.IsNullOrWhiteSpace(filter.MuhasebeFisNo)
            || filter.KasaBankaHesapId.HasValue
            || tarihAraligiVar
            || tutarVar;

        if (!daralticiVar)
        {
            throw new BaseException(
                "Çapraz kaynak araştırması için en az bir daraltıcı alan girilmelidir: cari hesap, belge no, " +
                "muhasebe fiş no, kasa/banka hesabı, tarih aralığı (başlangıç+bitiş) veya tutar aralığı.", 400);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Ortak (UNION ALL) aday sorgusu
    // ─────────────────────────────────────────────────────────────

    private IQueryable<AdayHam> BirlesikAdaySorgusu(IReadOnlyList<int> tesisIds, OdemeCaprazAramaFilterDto filter)
    {
        var baslangic = filter.TarihBaslangic?.ToDateTime(TimeOnly.MinValue);
        var bitis = filter.TarihBitis?.AddDays(1).ToDateTime(TimeOnly.MinValue);

        return OdemeBelgesiAdaylari(tesisIds, filter, baslangic, bitis)
            .Concat(CariHareketAdaylari(tesisIds, filter, baslangic, bitis))
            .Concat(PosValorAdaylari(tesisIds, filter, baslangic, bitis))
            .Concat(KasaHareketAdaylari(tesisIds, filter, baslangic, bitis))
            .Concat(BankaHareketAdaylari(tesisIds, filter, baslangic, bitis))
            .Concat(MuhasebeFisiAdaylari(tesisIds, filter, baslangic, bitis));
    }

    private IQueryable<AdayHam> OdemeBelgesiAdaylari(
        IReadOnlyList<int> tesisIds, OdemeCaprazAramaFilterDto filter, DateTime? baslangic, DateTime? bitis)
    {
        var q = _dbContext.TahsilatOdemeBelgeleri.AsNoTracking()
            .Where(b => !b.IsDeleted && b.CariKart != null && b.CariKart.TesisId.HasValue && tesisIds.Contains(b.CariKart.TesisId.Value));

        if (baslangic.HasValue) q = q.Where(b => b.BelgeTarihi >= baslangic.Value);
        if (bitis.HasValue) q = q.Where(b => b.BelgeTarihi < bitis.Value);
        if (filter.TutarMin.HasValue) q = q.Where(b => b.Tutar >= filter.TutarMin.Value);
        if (filter.TutarMax.HasValue) q = q.Where(b => b.Tutar <= filter.TutarMax.Value);
        if (filter.CariKartId.HasValue) q = q.Where(b => b.CariKartId == filter.CariKartId.Value);
        if (!string.IsNullOrWhiteSpace(filter.ParaBirimi)) q = q.Where(b => b.ParaBirimi == filter.ParaBirimi);
        if (!string.IsNullOrWhiteSpace(filter.BelgeNo)) q = q.Where(b => b.BelgeNo.Contains(filter.BelgeNo));
        if (filter.KasaBankaHesapId.HasValue) q = q.Where(b => b.KasaBankaHesapId == filter.KasaBankaHesapId.Value);
        if (!string.IsNullOrWhiteSpace(filter.MuhasebeFisNo))
        {
            q = q.Where(b => b.MuhasebeFisId != null
                && _dbContext.MuhasebeFisler.Any(f => f.Id == b.MuhasebeFisId && f.FisNo.Contains(filter.MuhasebeFisNo)));
        }
        if (filter.MaliYil.HasValue || filter.Donem.HasValue)
        {
            q = q.Where(b => b.MuhasebeFisId != null && _dbContext.MuhasebeFisler.Any(f => f.Id == b.MuhasebeFisId
                && (!filter.MaliYil.HasValue || f.MaliYil == filter.MaliYil.Value)
                && (!filter.Donem.HasValue || f.Donem == filter.Donem.Value)));
        }
        if (filter.SadeceIptalEdilmisOlanlar.HasValue)
        {
            q = filter.SadeceIptalEdilmisOlanlar.Value
                ? q.Where(b => b.Durum == TahsilatOdemeBelgeDurumlari.Iptal)
                : q.Where(b => b.Durum == TahsilatOdemeBelgeDurumlari.Aktif);
        }

        return q.Select(b => new AdayHam
        {
            Anahtar = "BELGE:" + b.Id.ToString(),
            Kaynak = OdemeAdayKaynaklari.TahsilatOdemeBelgesi,
            KaynakId = b.Id,
            Tarih = b.BelgeTarihi,
            Tutar = b.Tutar,
            ParaBirimi = b.ParaBirimi,
            TesisId = b.CariKart!.TesisId,
            CariKartId = b.CariKartId,
            KasaBankaHesapId = b.KasaBankaHesapId,
            TahsilatOdemeBelgesiId = b.Id,
            CariHareketId = null,
            PosTahsilatValorId = null,
            MuhasebeFisId = b.MuhasebeFisId,
            BelgeNo = b.BelgeNo,
            KopuklukFisYok = b.Durum == TahsilatOdemeBelgeDurumlari.Aktif && b.MuhasebeFisId == null
                && (b.OdemeYontemi == OdemeYontemleri.Nakit || b.OdemeYontemi == OdemeYontemleri.KrediKarti || b.OdemeYontemi == OdemeYontemleri.HavaleEft)
                ? 1 : 0,
            KopuklukCariYok = b.Durum == TahsilatOdemeBelgeDurumlari.Aktif && b.KapatilacakCariHareketId != null
                && !_dbContext.CariHareketler.Any(h => !h.IsDeleted
                    && h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && h.KaynakId == b.Id)
                ? 1 : 0,
            KopuklukValorYok = b.Durum == TahsilatOdemeBelgeDurumlari.Aktif && b.OdemeYontemi == OdemeYontemleri.KrediKarti
                && !_dbContext.PosTahsilatValorleri.Any(v => !v.IsDeleted && v.TahsilatOdemeBelgesiId == b.Id)
                ? 1 : 0,
            KopuklukHedefHesapYok = 0,
            KopuklukOdemeBaglantisiYok = 0,
            KopuklukSoftDelete = 0,
            BagimsizKayit = 0
        });
    }

    private IQueryable<AdayHam> CariHareketAdaylari(
        IReadOnlyList<int> tesisIds, OdemeCaprazAramaFilterDto filter, DateTime? baslangic, DateTime? bitis)
    {
        // BAGIMSIZ ARASTIRMA: KaynakModul kisiti YOKTUR - odeme belgesine hic baglanmamis
        // (KaynakModul null/farkli) cari hareketler de aday olur.
        var q = _dbContext.CariHareketler.AsNoTracking()
            .Where(h => !h.IsDeleted && h.CariKart != null && h.CariKart.TesisId.HasValue && tesisIds.Contains(h.CariKart.TesisId.Value));

        if (baslangic.HasValue) q = q.Where(h => h.HareketTarihi >= baslangic.Value);
        if (bitis.HasValue) q = q.Where(h => h.HareketTarihi < bitis.Value);
        if (filter.CariKartId.HasValue) q = q.Where(h => h.CariKartId == filter.CariKartId.Value);
        if (!string.IsNullOrWhiteSpace(filter.ParaBirimi)) q = q.Where(h => h.ParaBirimi == filter.ParaBirimi);
        if (!string.IsNullOrWhiteSpace(filter.BelgeNo)) q = q.Where(h => h.BelgeNo != null && h.BelgeNo.Contains(filter.BelgeNo));
        if (filter.TutarMin.HasValue) q = q.Where(h => h.BorcTutari >= filter.TutarMin.Value || h.AlacakTutari >= filter.TutarMin.Value);
        if (filter.TutarMax.HasValue) q = q.Where(h => h.BorcTutari <= filter.TutarMax.Value && h.AlacakTutari <= filter.TutarMax.Value);
        if (filter.SadeceIptalEdilmisOlanlar.HasValue)
        {
            q = filter.SadeceIptalEdilmisOlanlar.Value
                ? q.Where(h => h.Durum != CariHareketDurumlari.Aktif)
                : q.Where(h => h.Durum == CariHareketDurumlari.Aktif);
        }
        // Kasa/banka hesabi ve muhasebe fis no filtreleri CariHareket'te DOGRUDAN veya guvenilir bir
        // iliski uzerinden uygulanamaz -> bu filtreler verildiginde bu KAYNAK SONUC DISI birakilir
        // (sessizce yok sayilmaz, bkz. teslim raporu filtre tablosu).
        if (filter.KasaBankaHesapId.HasValue || !string.IsNullOrWhiteSpace(filter.MuhasebeFisNo)
            || filter.MaliYil.HasValue || filter.Donem.HasValue)
        {
            q = q.Where(_ => false);
        }

        return q.Select(h => new AdayHam
        {
            Anahtar = h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && h.KaynakId != null
                ? "BELGE:" + h.KaynakId.ToString()
                : "CARIHAREKET:" + h.Id.ToString(),
            Kaynak = OdemeAdayKaynaklari.CariHareket,
            KaynakId = h.Id,
            Tarih = h.HareketTarihi,
            Tutar = h.BorcTutari - h.AlacakTutari,
            ParaBirimi = h.ParaBirimi,
            TesisId = h.CariKart!.TesisId,
            CariKartId = h.CariKartId,
            KasaBankaHesapId = null,
            TahsilatOdemeBelgesiId = h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi ? h.KaynakId : null,
            CariHareketId = h.Id,
            PosTahsilatValorId = null,
            MuhasebeFisId = null,
            BelgeNo = h.BelgeNo,
            KopuklukFisYok = 0,
            KopuklukCariYok = 0,
            KopuklukValorYok = 0,
            KopuklukHedefHesapYok = 0,
            // Odeme belgesinden dogdugu ISARETLI ama kaynak belge bulunamiyor.
            KopuklukOdemeBaglantisiYok = h.Durum == CariHareketDurumlari.Aktif
                && h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi
                && !_dbContext.TahsilatOdemeBelgeleri.Any(b => !b.IsDeleted && b.Id == h.KaynakId)
                ? 1 : 0,
            KopuklukSoftDelete = h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi
                && _dbContext.TahsilatOdemeBelgeleri.IgnoreQueryFilters().Any(b => b.IsDeleted && b.Id == h.KaynakId
                    && b.CariKart != null && b.CariKart.TesisId.HasValue && tesisIds.Contains(b.CariKart.TesisId.Value))
                ? 1 : 0,
            // BAGIMSIZ: hicbir odeme belgesine baglanmamis cari hareket.
            BagimsizKayit = h.KaynakModul != MuhasebeKaynakModulleri.TahsilatOdemeBelgesi ? 1 : 0
        });
    }

    private IQueryable<AdayHam> PosValorAdaylari(
        IReadOnlyList<int> tesisIds, OdemeCaprazAramaFilterDto filter, DateTime? baslangic, DateTime? bitis)
    {
        var q = _dbContext.PosTahsilatValorleri.AsNoTracking()
            .Where(v => !v.IsDeleted && tesisIds.Contains(v.TesisId));

        if (baslangic.HasValue) q = q.Where(v => v.OdemeTarihi >= baslangic.Value);
        if (bitis.HasValue) q = q.Where(v => v.OdemeTarihi < bitis.Value);
        if (filter.TutarMin.HasValue) q = q.Where(v => v.NetTutar >= filter.TutarMin.Value);
        if (filter.TutarMax.HasValue) q = q.Where(v => v.NetTutar <= filter.TutarMax.Value);
        if (!string.IsNullOrWhiteSpace(filter.ParaBirimi)) q = q.Where(v => v.ParaBirimi == filter.ParaBirimi);
        if (filter.KasaBankaHesapId.HasValue) q = q.Where(v => v.BagliBankaHesapId == filter.KasaBankaHesapId.Value);
        if (filter.CariKartId.HasValue)
        {
            q = q.Where(v => _dbContext.TahsilatOdemeBelgeleri.Any(b => b.Id == v.TahsilatOdemeBelgesiId && b.CariKartId == filter.CariKartId.Value));
        }
        if (!string.IsNullOrWhiteSpace(filter.BelgeNo))
        {
            q = q.Where(v => _dbContext.TahsilatOdemeBelgeleri.Any(b => b.Id == v.TahsilatOdemeBelgesiId && b.BelgeNo.Contains(filter.BelgeNo)));
        }
        if (!string.IsNullOrWhiteSpace(filter.MuhasebeFisNo))
        {
            q = q.Where(v => v.MuhasebeFisId != null
                && _dbContext.MuhasebeFisler.Any(f => f.Id == v.MuhasebeFisId && f.FisNo.Contains(filter.MuhasebeFisNo)));
        }
        if (filter.MaliYil.HasValue || filter.Donem.HasValue)
        {
            q = q.Where(v => v.MuhasebeFisId != null && _dbContext.MuhasebeFisler.Any(f => f.Id == v.MuhasebeFisId
                && (!filter.MaliYil.HasValue || f.MaliYil == filter.MaliYil.Value)
                && (!filter.Donem.HasValue || f.Donem == filter.Donem.Value)));
        }
        if (filter.SadeceIptalEdilmisOlanlar.HasValue)
        {
            q = filter.SadeceIptalEdilmisOlanlar.Value
                ? q.Where(v => v.Durum == PosTahsilatValorDurumlari.Iptal || v.Durum == PosTahsilatValorDurumlari.AktarimFisiIptalEdildi)
                : q.Where(v => v.Durum != PosTahsilatValorDurumlari.Iptal && v.Durum != PosTahsilatValorDurumlari.AktarimFisiIptalEdildi);
        }

        return q.Select(v => new AdayHam
        {
            Anahtar = "BELGE:" + v.TahsilatOdemeBelgesiId.ToString(),
            Kaynak = OdemeAdayKaynaklari.PosTahsilatValor,
            KaynakId = v.Id,
            Tarih = v.OdemeTarihi,
            Tutar = v.NetTutar,
            ParaBirimi = v.ParaBirimi,
            TesisId = v.TesisId,
            CariKartId = null,
            KasaBankaHesapId = v.BagliBankaHesapId,
            TahsilatOdemeBelgesiId = v.TahsilatOdemeBelgesiId,
            CariHareketId = null,
            PosTahsilatValorId = v.Id,
            MuhasebeFisId = v.MuhasebeFisId,
            BelgeNo = null,
            KopuklukFisYok = 0,
            KopuklukCariYok = 0,
            KopuklukValorYok = 0,
            // Hedef banka hesabi yok / pasif / silinmis VEYA tesisi uyusmuyor.
            KopuklukHedefHesapYok = v.Durum != PosTahsilatValorDurumlari.Iptal
                && (v.BagliBankaHesapId == null
                    || !_dbContext.KasaBankaHesaplari.Any(k => !k.IsDeleted && k.AktifMi && k.Id == v.BagliBankaHesapId && k.TesisId == v.TesisId))
                ? 1 : 0,
            KopuklukOdemeBaglantisiYok = 0,
            KopuklukSoftDelete = 0,
            BagimsizKayit = 0
        });
    }

    private IQueryable<AdayHam> KasaHareketAdaylari(
        IReadOnlyList<int> tesisIds, OdemeCaprazAramaFilterDto filter, DateTime? baslangic, DateTime? bitis)
    {
        // BAGIMSIZ ARASTIRMA: KaynakModul kisiti YOKTUR.
        var q = _dbContext.KasaHareketleri.AsNoTracking()
            .Where(h => !h.IsDeleted && h.KasaBankaHesap != null && h.KasaBankaHesap.TesisId.HasValue
                && tesisIds.Contains(h.KasaBankaHesap.TesisId.Value));

        if (baslangic.HasValue) q = q.Where(h => h.HareketTarihi >= baslangic.Value);
        if (bitis.HasValue) q = q.Where(h => h.HareketTarihi < bitis.Value);
        if (filter.TutarMin.HasValue) q = q.Where(h => h.Tutar >= filter.TutarMin.Value);
        if (filter.TutarMax.HasValue) q = q.Where(h => h.Tutar <= filter.TutarMax.Value);
        if (!string.IsNullOrWhiteSpace(filter.ParaBirimi)) q = q.Where(h => h.ParaBirimi == filter.ParaBirimi);
        if (filter.CariKartId.HasValue) q = q.Where(h => h.CariKartId == filter.CariKartId.Value);
        if (filter.KasaBankaHesapId.HasValue) q = q.Where(h => h.KasaBankaHesapId == filter.KasaBankaHesapId.Value);
        if (!string.IsNullOrWhiteSpace(filter.BelgeNo)) q = q.Where(h => h.BelgeNo != null && h.BelgeNo.Contains(filter.BelgeNo));
        if (!string.IsNullOrWhiteSpace(filter.MuhasebeFisNo) || filter.MaliYil.HasValue || filter.Donem.HasValue)
        {
            q = q.Where(_ => false); // KasaHareket'te guvenilir fis/donem iliskisi yok.
        }

        return q.Select(h => new AdayHam
        {
            Anahtar = h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && h.KaynakId != null
                ? "BELGE:" + h.KaynakId.ToString()
                : "KASAHAREKET:" + h.Id.ToString(),
            Kaynak = OdemeAdayKaynaklari.KasaHareket,
            KaynakId = h.Id,
            Tarih = h.HareketTarihi,
            Tutar = h.Tutar,
            ParaBirimi = h.ParaBirimi,
            TesisId = h.KasaBankaHesap!.TesisId,
            CariKartId = h.CariKartId,
            KasaBankaHesapId = h.KasaBankaHesapId,
            TahsilatOdemeBelgesiId = h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi ? h.KaynakId : null,
            CariHareketId = null,
            PosTahsilatValorId = null,
            MuhasebeFisId = null,
            BelgeNo = h.BelgeNo,
            KopuklukFisYok = 0,
            KopuklukCariYok = 0,
            KopuklukValorYok = 0,
            KopuklukHedefHesapYok = 0,
            KopuklukOdemeBaglantisiYok = h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi
                && !_dbContext.TahsilatOdemeBelgeleri.Any(b => !b.IsDeleted && b.Id == h.KaynakId)
                ? 1 : 0,
            KopuklukSoftDelete = 0,
            BagimsizKayit = h.KaynakModul != MuhasebeKaynakModulleri.TahsilatOdemeBelgesi ? 1 : 0
        });
    }

    private IQueryable<AdayHam> BankaHareketAdaylari(
        IReadOnlyList<int> tesisIds, OdemeCaprazAramaFilterDto filter, DateTime? baslangic, DateTime? bitis)
    {
        var q = _dbContext.BankaHareketleri.AsNoTracking()
            .Where(h => !h.IsDeleted && h.KasaBankaHesap != null && h.KasaBankaHesap.TesisId.HasValue
                && tesisIds.Contains(h.KasaBankaHesap.TesisId.Value));

        if (baslangic.HasValue) q = q.Where(h => h.HareketTarihi >= baslangic.Value);
        if (bitis.HasValue) q = q.Where(h => h.HareketTarihi < bitis.Value);
        if (filter.TutarMin.HasValue) q = q.Where(h => h.Tutar >= filter.TutarMin.Value);
        if (filter.TutarMax.HasValue) q = q.Where(h => h.Tutar <= filter.TutarMax.Value);
        if (!string.IsNullOrWhiteSpace(filter.ParaBirimi)) q = q.Where(h => h.ParaBirimi == filter.ParaBirimi);
        if (filter.CariKartId.HasValue) q = q.Where(h => h.CariKartId == filter.CariKartId.Value);
        if (filter.KasaBankaHesapId.HasValue) q = q.Where(h => h.KasaBankaHesapId == filter.KasaBankaHesapId.Value);
        if (!string.IsNullOrWhiteSpace(filter.BelgeNo)) q = q.Where(h => h.BelgeNo != null && h.BelgeNo.Contains(filter.BelgeNo));
        if (!string.IsNullOrWhiteSpace(filter.MuhasebeFisNo) || filter.MaliYil.HasValue || filter.Donem.HasValue)
        {
            q = q.Where(_ => false);
        }

        return q.Select(h => new AdayHam
        {
            Anahtar = h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && h.KaynakId != null
                ? "BELGE:" + h.KaynakId.ToString()
                : "BANKAHAREKET:" + h.Id.ToString(),
            Kaynak = OdemeAdayKaynaklari.BankaHareket,
            KaynakId = h.Id,
            Tarih = h.HareketTarihi,
            Tutar = h.Tutar,
            ParaBirimi = h.ParaBirimi,
            TesisId = h.KasaBankaHesap!.TesisId,
            CariKartId = h.CariKartId,
            KasaBankaHesapId = h.KasaBankaHesapId,
            TahsilatOdemeBelgesiId = h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi ? h.KaynakId : null,
            CariHareketId = null,
            PosTahsilatValorId = null,
            MuhasebeFisId = null,
            BelgeNo = h.BelgeNo,
            KopuklukFisYok = 0,
            KopuklukCariYok = 0,
            KopuklukValorYok = 0,
            KopuklukHedefHesapYok = 0,
            KopuklukOdemeBaglantisiYok = h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi
                && !_dbContext.TahsilatOdemeBelgeleri.Any(b => !b.IsDeleted && b.Id == h.KaynakId)
                ? 1 : 0,
            KopuklukSoftDelete = 0,
            BagimsizKayit = h.KaynakModul != MuhasebeKaynakModulleri.TahsilatOdemeBelgesi ? 1 : 0
        });
    }

    private IQueryable<AdayHam> MuhasebeFisiAdaylari(
        IReadOnlyList<int> tesisIds, OdemeCaprazAramaFilterDto filter, DateTime? baslangic, DateTime? bitis)
    {
        // BAGIMSIZ ARASTIRMA: KaynakModul kisiti YOKTUR - odeme belgesine hic baglanmamis fisler de
        // aday olur (mali etki olusturan durumlarla sinirli).
        var q = _dbContext.MuhasebeFisler.AsNoTracking()
            .Where(f => !f.IsDeleted && tesisIds.Contains(f.TesisId)
                && (f.Durum == MuhasebeFisDurumlari.Onayli || f.Durum == MuhasebeFisDurumlari.TersKayit));

        if (baslangic.HasValue) q = q.Where(f => f.FisTarihi >= baslangic.Value);
        if (bitis.HasValue) q = q.Where(f => f.FisTarihi < bitis.Value);
        if (filter.TutarMin.HasValue) q = q.Where(f => f.ToplamBorc >= filter.TutarMin.Value);
        if (filter.TutarMax.HasValue) q = q.Where(f => f.ToplamBorc <= filter.TutarMax.Value);
        if (!string.IsNullOrWhiteSpace(filter.MuhasebeFisNo)) q = q.Where(f => f.FisNo.Contains(filter.MuhasebeFisNo));
        if (filter.MaliYil.HasValue) q = q.Where(f => f.MaliYil == filter.MaliYil.Value);
        if (filter.Donem.HasValue) q = q.Where(f => f.Donem == filter.Donem.Value);
        if (filter.CariKartId.HasValue)
        {
            // Fis SATIRLARINDA ilgili cari etkilenmis mi (gercek iliski).
            q = q.Where(f => _dbContext.MuhasebeFisSatirlari.Any(s => !s.IsDeleted && s.MuhasebeFisId == f.Id && s.CariKartId == filter.CariKartId.Value));
        }
        if (filter.KasaBankaHesapId.HasValue)
        {
            q = q.Where(f => _dbContext.MuhasebeFisSatirlari.Any(s => !s.IsDeleted && s.MuhasebeFisId == f.Id && s.KasaBankaHesapId == filter.KasaBankaHesapId.Value));
        }
        if (!string.IsNullOrWhiteSpace(filter.BelgeNo)) q = q.Where(f => f.FisNo.Contains(filter.BelgeNo));
        // Para birimi fis BASLIGINDA yok; satir bazindadir - guvenilir sekilde uygulanamadigi icin
        // bu filtre verildiginde fis kaynagi sonuc disi birakilir.
        if (!string.IsNullOrWhiteSpace(filter.ParaBirimi))
        {
            q = q.Where(f => _dbContext.MuhasebeFisSatirlari.Any(s => !s.IsDeleted && s.MuhasebeFisId == f.Id && s.ParaBirimi == filter.ParaBirimi));
        }

        return q.Select(f => new AdayHam
        {
            Anahtar = f.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && f.KaynakId != null
                ? "BELGE:" + f.KaynakId.ToString()
                : "FIS:" + f.Id.ToString(),
            Kaynak = OdemeAdayKaynaklari.MuhasebeFis,
            KaynakId = f.Id,
            Tarih = f.FisTarihi,
            Tutar = f.ToplamBorc,
            ParaBirimi = null,
            TesisId = f.TesisId,
            CariKartId = null,
            KasaBankaHesapId = null,
            TahsilatOdemeBelgesiId = f.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi ? f.KaynakId : null,
            CariHareketId = null,
            PosTahsilatValorId = null,
            MuhasebeFisId = f.Id,
            BelgeNo = f.FisNo,
            KopuklukFisYok = 0,
            KopuklukCariYok = 0,
            KopuklukValorYok = 0,
            KopuklukHedefHesapYok = 0,
            KopuklukOdemeBaglantisiYok = f.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi
                && !_dbContext.TahsilatOdemeBelgeleri.Any(b => !b.IsDeleted && b.Id == f.KaynakId)
                ? 1 : 0,
            KopuklukSoftDelete = f.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi
                && _dbContext.TahsilatOdemeBelgeleri.IgnoreQueryFilters().Any(b => b.IsDeleted && b.Id == f.KaynakId
                    && b.CariKart != null && b.CariKart.TesisId.HasValue && tesisIds.Contains(b.CariKart.TesisId.Value))
                ? 1 : 0,
            BagimsizKayit = f.KaynakModul != MuhasebeKaynakModulleri.TahsilatOdemeBelgesi ? 1 : 0
        });
    }

    // ─────────────────────────────────────────────────────────────
    // Sayfa satirlarini DTO'ya donusturme
    // ─────────────────────────────────────────────────────────────

    private static List<OdemeAdayiDto> Birlestir(
        List<AdayHam> satirlar, List<string> sirali, OdemeCaprazAramaFilterDto filter)
    {
        var sozluk = new Dictionary<string, OdemeAdayiDto>(StringComparer.Ordinal);

        foreach (var s in satirlar)
        {
            if (!sozluk.TryGetValue(s.Anahtar, out var aday))
            {
                aday = new OdemeAdayiDto { TekillestirmeAnahtari = s.Anahtar, Kaynak = s.Kaynak, KaynakId = s.KaynakId };
                sozluk[s.Anahtar] = aday;
            }

            if (!aday.BulunduguKaynaklar.Contains(s.Kaynak))
            {
                aday.BulunduguKaynaklar.Add(s.Kaynak);
            }

            aday.Tarih ??= s.Tarih;
            aday.Tutar ??= s.Tutar;
            aday.ParaBirimi ??= s.ParaBirimi;
            aday.TesisId ??= s.TesisId;
            aday.CariKartId ??= s.CariKartId;
            aday.KasaBankaHesapId ??= s.KasaBankaHesapId;
            aday.TahsilatOdemeBelgesiId ??= s.TahsilatOdemeBelgesiId;
            aday.CariHareketId ??= s.CariHareketId;
            aday.PosTahsilatValorId ??= s.PosTahsilatValorId;
            aday.MuhasebeFisId ??= s.MuhasebeFisId;
            aday.BelgeNo ??= s.BelgeNo;

            if (s.KopuklukFisYok == 1) EkleKopukluk(aday, OdemeKopuklukTipleri.MuhasebeFisiOlmayanOdemeBelgesi,
                "Ödeme nakit/banka/POS hareketi doğurduğu hâlde bağlı bir muhasebe fişi yok.");
            if (s.KopuklukCariYok == 1) EkleKopukluk(aday, OdemeKopuklukTipleri.CariHareketEtkisiOlmayanOdemeBelgesi,
                "Ödeme bir borcu kapatmak üzere işaretlenmiş ancak karşılık gelen cari hareket bulunamadı.");
            if (s.KopuklukValorYok == 1) EkleKopukluk(aday, OdemeKopuklukTipleri.ValorKaydiOlmayanPosTahsilati,
                "Kredi kartı tahsilatı olduğu hâlde POS valör takip kaydı yok.");
            if (s.KopuklukHedefHesapYok == 1) EkleKopukluk(aday, OdemeKopuklukTipleri.HedefBankaHesabiOlmayanValor,
                "POS valör kaydının hedef banka hesabı tanımsız, bulunamıyor, pasif veya tesisi uyuşmuyor.");
            if (s.KopuklukOdemeBaglantisiYok == 1) EkleKopukluk(aday, OdemeKopuklukTipleri.OdemeBaglantisiOlmayanMuhasebeFisi,
                "Kayıt tahsilat/ödeme kaynaklı görünüyor ancak bağlı ödeme belgesi bulunamadı.");
            if (s.KopuklukSoftDelete == 1) EkleKopukluk(aday, OdemeKopuklukTipleri.SoftDeleteIliskiNedeniyleGorunmeyen,
                "Kaynak ödeme belgesi silinmiş (soft-delete); normal aramalarda görünmez.");
            if (s.BagimsizKayit == 1)
            {
                aday.BagimsizKayitMi = true;
                aday.GuvenSeviyesi = OdemeGuvenSeviyeleri.IncelenmesiGereken;
                aday.GuvenGerekcesi =
                    "Bu kayıt herhangi bir tahsilat/ödeme belgesine bağlı değil. Aynı ödemeye ait olup olmadığı " +
                    "yalnızca filtre kriterleriyle (tarih/tutar/cari/hesap) daraltılmıştır; KANITLANMAMIŞTIR.";
            }
        }

        foreach (var aday in sozluk.Values.Where(x => !x.BagimsizKayitMi))
        {
            aday.GuvenSeviyesi = OdemeGuvenSeviyeleri.YuksekOlasilik;
            aday.GuvenGerekcesi = "Kayıtlar ödeme belgesi kimliği üzerinden birbirine gerçekten bağlıdır.";
        }

        // Sayfa sirasini SQL'den gelen anahtar sirasina gore koru.
        return [.. sirali.Where(sozluk.ContainsKey).Select(a => sozluk[a])];
    }

    private static void EkleKopukluk(OdemeAdayiDto aday, string kod, string aciklama)
    {
        if (aday.KopuklukKodlari.Contains(kod))
        {
            return;
        }

        aday.KopuklukKodlari.Add(kod);
        aday.KopuklukAciklamalari.Add(aciklama);
    }

    private async Task<IReadOnlyList<int>> ResolveTesisIdsAsync(int? tesisId, CancellationToken cancellationToken)
    {
        if (tesisId.HasValue)
        {
            await _tesisScopeService.EnsureCanAccessTesisAsync(tesisId.Value, cancellationToken);
            return [tesisId.Value];
        }

        return await _tesisScopeService.GetEffectiveTesisIdsAsync(cancellationToken);
    }
}
