using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
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
///
/// BEKLENEN vs BULUNAN (madde 1): <see cref="OdemeCaprazAramaFilterDto"/>'daki Beklenen* alanlari
/// (cari, banka/kasa hesabi, muhasebe hesabi, mali yil, donem, tesis, kurum, tutar, para birimi)
/// ARTIK WHERE'e GIRMEZ - bir adayi ELEMEK icin kullanilmaz. Bunun yerine, bulunan aday ile
/// KARSILASTIRILIR ve celiski varsa aday yine DONER, yalnizca CelisenAlanlar/OdemeCeliskiKodlari
/// ile isaretlenir. Boylece "baska hesaba/cariye/doneme yanlislikla islenmis" kayitlar SESSIZCE
/// elenmez - asil arastirmanin hedefi budur.
///
/// DETERMINISTIK BIRLESTIRME (madde 3): her kaynagin KaynakOncelik degeri vardir (dusuk deger =
/// yuksek oncelik). Ayni tekillestirme anahtarina birden fazla kaynak dustugunde, gosterilecek
/// tek (Tutar/ParaBirimi/Tarih/CariKartId/... ) deger SQL'in fiziksel satir sirasina DEGIL, bu
/// onceliğe gore secilir - materyalizasyon sorgusu KaynakOncelik+KaynakId ile ACIKCA siralanir.
/// </summary>
public class OdemeCaprazAramaService : IOdemeCaprazAramaService
{
    private const int MaksimumSayfaBoyutu = 200;

    /// <summary>Yalnizca tutar/tarih araligiyla (guclu bir referans olmadan) arama yapildiginda
    /// izin verilen en genis tarih araligi (gun). Asiri genis, hesap taramasi niteligindeki
    /// sorgulari engellemek icin konuldu; proje veri hacmi buyudukce yeniden degerlendirilmelidir.</summary>
    private const int MaksimumDaralticiTarihAraligiGunSayisiReferansYokken = 92;

    /// <summary>Tutar karsilastirmasinda kabul edilen yuvarlama toleransi.</summary>
    private const decimal TutarKarsilastirmaToleransi = 0.01m;

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

        /// <summary>Dusuk deger = yuksek oncelik. Deterministik birlestirme icin kullanilir
        /// (madde 3): 1=OdemeBelgesi, 2=Banka/KasaHareket, 3=PosValor, 4=CariHareket, 5=MuhasebeFis.</summary>
        public int KaynakOncelik { get; set; }

        public DateTime? Tarih { get; set; }
        public decimal? Tutar { get; set; }
        /// <summary>Tutarin ANLAMI (madde 3) - "Brüt"/"Net"/"Borç-Alacak Net"/"İşaretli" vb.
        /// Farkli kaynaklarin tutarlari birbirinin YERINE KULLANILMAZ, yalnizca gosterim icin
        /// ayri ayri tasinir (bkz. OdemeAdayiDto.KaynakTutarlari).</summary>
        public string TutarTuru { get; set; } = string.Empty;
        public string? ParaBirimi { get; set; }
        public int? TesisId { get; set; }
        public int? KurumId { get; set; }
        public int? CariKartId { get; set; }
        public int? KasaBankaHesapId { get; set; }
        public string? KasaBankaHesapTipi { get; set; }
        public int? MuhasebeHesapPlaniId { get; set; }
        public int? MaliYil { get; set; }
        public int? Donem { get; set; }
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

        /// <summary>Kaynak, bir odeme belgesine baglanmis GORUNUYOR (KaynakModul/KaynakId dolu) ANCAK
        /// o belge baska bir tesise ait oldugu icin (veya tesis iliskisi dogrulanamadigi icin) bu
        /// baglanti GECERSIZ sayildi - "BELGE:{id}" anahtari URETILMEDI, bagimsiz kayit olarak
        /// isaretlendi. Yalnizca CariHareketAdaylari tarafindan doldurulur (bkz. metot yorumu).</summary>
        public int KopuklukYetkiDisindaOdemeBaglantisi { get; set; }
    }

    public async Task<PagedResult<OdemeAdayiDto>> AraAsync(
        PagedRequest request, OdemeCaprazAramaFilterDto filter, CancellationToken cancellationToken = default)
    {
        DogrulaSorguSinirlari(filter);

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
        // DETERMINISTIK BIRLESTIRME icin, satirlar SQL'de KaynakOncelik+KaynakId'ye gore
        // SIRALANARAK cekilir - Birlestir() bu sirayla ilerledigi icin ayni veri kumesinde HER
        // ZAMAN AYNI sonucu uretir; SQL'in fiziksel satir donusum sirasina BAGLI DEGILDIR.
        var sayfaSatirlari = await birlesik
            .Where(x => sayfaAnahtarlari.Contains(x.Anahtar))
            .OrderBy(x => x.KaynakOncelik).ThenBy(x => x.KaynakId)
            .ToListAsync(cancellationToken);

        var adaylar = Birlestir(sayfaSatirlari, sayfaAnahtarlari, filter);
        return new PagedResult<OdemeAdayiDto>(adaylar, pageNumber, pageSize, toplam);
    }

    /// <summary>
    /// Asiri genis (daraltici alani olmayan) finansal taramayi engeller (madde 5). Beklenen*
    /// alanlari (cari/hesap/donem) ARTIK DARALTICI SAYILMAZ - onlar sadece karsilastirma icindir
    /// ve SQL'e hic girmezler; bu yuzden guclu bir referans veya (tutar araligi + dar tarih
    /// araligi) sarti aranir.
    /// </summary>
    private static void DogrulaSorguSinirlari(OdemeCaprazAramaFilterDto filter)
    {
        if (filter.TutarMin.HasValue && filter.TutarMax.HasValue && filter.TutarMin.Value > filter.TutarMax.Value)
        {
            throw new BaseException("Tutar aralığı geçersiz: alt sınır üst sınırdan büyük olamaz.", 400);
        }

        if (filter.TarihBaslangic.HasValue && filter.TarihBitis.HasValue && filter.TarihBaslangic.Value > filter.TarihBitis.Value)
        {
            throw new BaseException("Tarih aralığı geçersiz: başlangıç bitişten sonra olamaz.", 400);
        }

        var guclüReferansVar =
            !string.IsNullOrWhiteSpace(filter.BelgeNo)
            || !string.IsNullOrWhiteSpace(filter.MuhasebeFisNo)
            || !string.IsNullOrWhiteSpace(filter.RezervasyonReferansNo);

        var tutarAraligiTamVar = filter.TutarMin.HasValue && filter.TutarMax.HasValue;
        var tarihAraligiVar = filter.TarihBaslangic.HasValue && filter.TarihBitis.HasValue;

        if (!guclüReferansVar)
        {
            if (filter.TutarMin.HasValue != filter.TutarMax.HasValue)
            {
                throw new BaseException(
                    "Güçlü bir referans (belge no, muhasebe fiş no, rezervasyon referans no) verilmediyse tutar aralığının " +
                    "hem alt hem üst sınırı birlikte girilmelidir; tek taraflı tutar sınırı çok geniş bir tarama yapar.", 400);
            }

            if (!tutarAraligiTamVar)
            {
                throw new BaseException(
                    "Çapraz kaynak araştırması için en az bir güçlü referans (belge no, muhasebe fiş no, rezervasyon referans no) " +
                    "veya birlikte verilmiş tutar aralığı + tarih aralığı girilmelidir.", 400);
            }

            if (!tarihAraligiVar)
            {
                throw new BaseException(
                    "Yalnızca tutar aralığıyla arama yapılıyorsa dar bir tarih aralığı (başlangıç + bitiş) da zorunludur.", 400);
            }
        }

        if (tarihAraligiVar)
        {
            var gunFarki = (filter.TarihBitis!.Value.ToDateTime(TimeOnly.MinValue) - filter.TarihBaslangic!.Value.ToDateTime(TimeOnly.MinValue)).Days;
            if (!guclüReferansVar && gunFarki > MaksimumDaralticiTarihAraligiGunSayisiReferansYokken)
            {
                throw new BaseException(
                    $"Güçlü bir referans verilmediyse tarih aralığı en fazla {MaksimumDaralticiTarihAraligiGunSayisiReferansYokken} gün olabilir " +
                    $"(girilen: {gunFarki} gün).", 400);
            }
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
        if (!string.IsNullOrWhiteSpace(filter.ParaBirimi)) q = q.Where(b => b.ParaBirimi == filter.ParaBirimi);
        if (!string.IsNullOrWhiteSpace(filter.BelgeNo)) q = q.Where(b => b.BelgeNo.Contains(filter.BelgeNo));
        if (!string.IsNullOrWhiteSpace(filter.MuhasebeFisNo))
        {
            q = q.Where(b => b.MuhasebeFisId != null
                && _dbContext.MuhasebeFisler.Any(f => f.Id == b.MuhasebeFisId && f.FisNo.Contains(filter.MuhasebeFisNo)));
        }
        if (!string.IsNullOrWhiteSpace(filter.RezervasyonReferansNo))
        {
            q = q.Where(b => _dbContext.RezervasyonOdemeler.Any(r => r.TahsilatOdemeBelgesiId == b.Id
                && r.Rezervasyon != null && r.Rezervasyon.ReferansNo.Contains(filter.RezervasyonReferansNo)));
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
            KaynakOncelik = 1,
            Tarih = b.BelgeTarihi,
            Tutar = b.Tutar,
            TutarTuru = "Belge Tutarı",
            ParaBirimi = b.ParaBirimi,
            TesisId = b.CariKart!.TesisId,
            KurumId = _dbContext.Tesisler.Where(t => t.Id == b.CariKart.TesisId).Select(t => (int?)t.KurumId).FirstOrDefault(),
            CariKartId = b.CariKartId,
            KasaBankaHesapId = b.KasaBankaHesapId,
            KasaBankaHesapTipi = b.KasaBankaHesapId == null ? null
                : _dbContext.KasaBankaHesaplari.Where(k => k.Id == b.KasaBankaHesapId).Select(k => k.Tip).FirstOrDefault(),
            MuhasebeHesapPlaniId = b.KasaBankaHesapId == null ? null
                : _dbContext.KasaBankaHesaplari.Where(k => k.Id == b.KasaBankaHesapId).Select(k => k.MuhasebeHesapPlaniId).FirstOrDefault(),
            MaliYil = b.MuhasebeFisId == null ? null
                : _dbContext.MuhasebeFisler.Where(f => f.Id == b.MuhasebeFisId).Select(f => (int?)f.MaliYil).FirstOrDefault(),
            Donem = b.MuhasebeFisId == null ? null
                : _dbContext.MuhasebeFisler.Where(f => f.Id == b.MuhasebeFisId).Select(f => (int?)f.Donem).FirstOrDefault(),
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
            KopuklukYetkiDisindaOdemeBaglantisi = 0,
            BagimsizKayit = 0
        });
    }

    private IQueryable<AdayHam> CariHareketAdaylari(
        IReadOnlyList<int> tesisIds, OdemeCaprazAramaFilterDto filter, DateTime? baslangic, DateTime? bitis)
    {
        // BAGIMSIZ ARASTIRMA: KaynakModul kisiti YOKTUR - odeme belgesine hic baglanmamis
        // (KaynakModul null/farkli) cari hareketler de aday olur. CariKartId/MaliYil/Donem ARTIK
        // BURADA FILTRELEMEZ (bkz. sinif aciklamasi, madde 1) - yalnizca gercek narrowing alanlari
        // (tarih, tutar, para birimi, belge no) uygulanir.
        var q = _dbContext.CariHareketler.AsNoTracking()
            .Where(h => !h.IsDeleted && h.CariKart != null && h.CariKart.TesisId.HasValue && tesisIds.Contains(h.CariKart.TesisId.Value));

        if (baslangic.HasValue) q = q.Where(h => h.HareketTarihi >= baslangic.Value);
        if (bitis.HasValue) q = q.Where(h => h.HareketTarihi < bitis.Value);
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
        // Muhasebe fis no / rezervasyon referansi CariHareket'te DOGRUDAN veya guvenilir bir
        // iliski uzerinden uygulanamaz -> bu filtreler verildiginde bu KAYNAK SONUC DISI birakilir
        // (sessizce yok sayilmaz, bkz. teslim raporu filtre tablosu).
        if (!string.IsNullOrWhiteSpace(filter.MuhasebeFisNo) || !string.IsNullOrWhiteSpace(filter.RezervasyonReferansNo))
        {
            q = q.Where(_ => false);
        }

        // GECERLI BAGLANTI: KaynakModul/KaynakId dolu VE isaret edilen belge silinmemis VE bu
        // hareketle AYNI tesise ait (b.CariKart.TesisId == h.CariKart.TesisId). Tesis iliskisi
        // dogrulanamiyorsa (CariKart null/TesisId farkli) veya belge baska tesisteyse baglanti
        // GECERSIZ sayilir - "BELGE:{id}" anahtari URETILMEZ, yabanci belge ID'si/ayrintisi DTO'ya
        // TASINMAZ, aday bagimsiz (BagimsizKayit=1) sayilarak asla YuksekOlasilik URETILMEZ.
        // KONTROL SQL SORGUSUNDA uygulanir - sonradan DTO temizligi YAPILMAZ.
        return q.Select(h => new AdayHam
        {
            Anahtar = h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && h.KaynakId != null
                && _dbContext.TahsilatOdemeBelgeleri.Any(b => !b.IsDeleted && b.Id == h.KaynakId
                    && b.CariKart != null && b.CariKart.TesisId == h.CariKart!.TesisId)
                ? "BELGE:" + h.KaynakId.ToString()
                : "CARIHAREKET:" + h.Id.ToString(),
            Kaynak = OdemeAdayKaynaklari.CariHareket,
            KaynakId = h.Id,
            KaynakOncelik = 4,
            Tarih = h.HareketTarihi,
            Tutar = h.BorcTutari - h.AlacakTutari,
            TutarTuru = "İşaretli (Borç-Alacak)",
            ParaBirimi = h.ParaBirimi,
            TesisId = h.CariKart!.TesisId,
            KurumId = _dbContext.Tesisler.Where(t => t.Id == h.CariKart.TesisId).Select(t => (int?)t.KurumId).FirstOrDefault(),
            CariKartId = h.CariKartId,
            KasaBankaHesapId = null,
            KasaBankaHesapTipi = null,
            MuhasebeHesapPlaniId = null,
            MaliYil = null,
            Donem = null,
            TahsilatOdemeBelgesiId = h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && h.KaynakId != null
                && _dbContext.TahsilatOdemeBelgeleri.Any(b => !b.IsDeleted && b.Id == h.KaynakId
                    && b.CariKart != null && b.CariKart.TesisId == h.CariKart!.TesisId)
                ? h.KaynakId : null,
            CariHareketId = h.Id,
            PosTahsilatValorId = null,
            MuhasebeFisId = null,
            BelgeNo = h.BelgeNo,
            KopuklukFisYok = 0,
            KopuklukCariYok = 0,
            KopuklukValorYok = 0,
            KopuklukHedefHesapYok = 0,
            // Odeme belgesinden dogdugu ISARETLI ama kaynak belge HICBIR TESISTE bulunamiyor
            // (silinmis/hic var olmamis) - tesis uyumsuzlugundan AYRI bir kopuklukdur.
            KopuklukOdemeBaglantisiYok = h.Durum == CariHareketDurumlari.Aktif
                && h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi
                && !_dbContext.TahsilatOdemeBelgeleri.Any(b => !b.IsDeleted && b.Id == h.KaynakId)
                ? 1 : 0,
            KopuklukSoftDelete = h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi
                && _dbContext.TahsilatOdemeBelgeleri.IgnoreQueryFilters().Any(b => b.IsDeleted && b.Id == h.KaynakId
                    && b.CariKart != null && b.CariKart.TesisId.HasValue && tesisIds.Contains(b.CariKart.TesisId.Value))
                ? 1 : 0,
            // Belge AKTIF olarak baska bir yerde bulunuyor (silinmemis) ANCAK bu hareketin
            // tesisiyle UYUSMUYOR (veya tesis iliskisi dogrulanamiyor) - yetki kapsami disinda
            // bir baglanti, "belge yok" (KopuklukOdemeBaglantisiYok) ile KARISTIRILMAZ.
            KopuklukYetkiDisindaOdemeBaglantisi = h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && h.KaynakId != null
                && !_dbContext.TahsilatOdemeBelgeleri.Any(b => !b.IsDeleted && b.Id == h.KaynakId
                    && b.CariKart != null && b.CariKart.TesisId == h.CariKart!.TesisId)
                && _dbContext.TahsilatOdemeBelgeleri.Any(b => !b.IsDeleted && b.Id == h.KaynakId)
                ? 1 : 0,
            // BAGIMSIZ: hicbir odeme belgesine GECERLI sekilde baglanmamis cari hareket (KaynakModul
            // farkli/bos OLDUGU gibi, KaynakModul dogru ama baglanti tesis uyumsuzlugu YUZUNDEN
            // GECERSIZ oldugunda da bagimsiz sayilir).
            BagimsizKayit = h.KaynakModul != MuhasebeKaynakModulleri.TahsilatOdemeBelgesi
                || h.KaynakId == null
                || !_dbContext.TahsilatOdemeBelgeleri.Any(b => !b.IsDeleted && b.Id == h.KaynakId
                    && b.CariKart != null && b.CariKart.TesisId == h.CariKart!.TesisId)
                ? 1 : 0
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
        if (!string.IsNullOrWhiteSpace(filter.BelgeNo))
        {
            q = q.Where(v => _dbContext.TahsilatOdemeBelgeleri.Any(b => b.Id == v.TahsilatOdemeBelgesiId && b.BelgeNo.Contains(filter.BelgeNo)));
        }
        if (!string.IsNullOrWhiteSpace(filter.MuhasebeFisNo))
        {
            q = q.Where(v => v.MuhasebeFisId != null
                && _dbContext.MuhasebeFisler.Any(f => f.Id == v.MuhasebeFisId && f.FisNo.Contains(filter.MuhasebeFisNo)));
        }
        if (!string.IsNullOrWhiteSpace(filter.RezervasyonReferansNo))
        {
            q = q.Where(v => _dbContext.RezervasyonOdemeler.Any(r => r.TahsilatOdemeBelgesiId == v.TahsilatOdemeBelgesiId
                && r.Rezervasyon != null && r.Rezervasyon.ReferansNo.Contains(filter.RezervasyonReferansNo)));
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
            KaynakOncelik = 3,
            Tarih = v.OdemeTarihi,
            Tutar = v.NetTutar,
            TutarTuru = "Net Tutar",
            ParaBirimi = v.ParaBirimi,
            TesisId = v.TesisId,
            KurumId = _dbContext.Tesisler.Where(t => t.Id == v.TesisId).Select(t => (int?)t.KurumId).FirstOrDefault(),
            CariKartId = null,
            KasaBankaHesapId = v.BagliBankaHesapId,
            KasaBankaHesapTipi = v.BagliBankaHesapId == null ? null
                : _dbContext.KasaBankaHesaplari.Where(k => k.Id == v.BagliBankaHesapId).Select(k => k.Tip).FirstOrDefault(),
            MuhasebeHesapPlaniId = v.BagliBankaHesapId == null ? null
                : _dbContext.KasaBankaHesaplari.Where(k => k.Id == v.BagliBankaHesapId).Select(k => k.MuhasebeHesapPlaniId).FirstOrDefault(),
            MaliYil = v.MuhasebeFisId == null ? null
                : _dbContext.MuhasebeFisler.Where(f => f.Id == v.MuhasebeFisId).Select(f => (int?)f.MaliYil).FirstOrDefault(),
            Donem = v.MuhasebeFisId == null ? null
                : _dbContext.MuhasebeFisler.Where(f => f.Id == v.MuhasebeFisId).Select(f => (int?)f.Donem).FirstOrDefault(),
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
            KopuklukYetkiDisindaOdemeBaglantisi = 0,
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
        if (!string.IsNullOrWhiteSpace(filter.BelgeNo)) q = q.Where(h => h.BelgeNo != null && h.BelgeNo.Contains(filter.BelgeNo));
        if (filter.SadeceIptalEdilmisOlanlar.HasValue)
        {
            q = filter.SadeceIptalEdilmisOlanlar.Value
                ? q.Where(h => h.Durum != CariHareketDurumlari.Aktif)
                : q.Where(h => h.Durum == CariHareketDurumlari.Aktif);
        }
        if (!string.IsNullOrWhiteSpace(filter.MuhasebeFisNo) || !string.IsNullOrWhiteSpace(filter.RezervasyonReferansNo))
        {
            q = q.Where(_ => false); // KasaHareket'te guvenilir fis/rezervasyon iliskisi yok.
        }

        // GECERLI BAGLANTI: KaynakModul/KaynakId dolu VE isaret edilen belge silinmemis VE bu
        // hareketin kasa/banka hesabiyla AYNI tesise ait (b.CariKart.TesisId == h.KasaBankaHesap.TesisId).
        // Tesis iliskisi dogrulanamiyorsa (CariKart null/TesisId farkli) veya belge baska tesisteyse
        // baglanti GECERSIZ sayilir - "BELGE:{id}" anahtari URETILMEZ, yabanci belge ID'si/ayrintisi
        // DTO'ya TASINMAZ, aday bagimsiz (BagimsizKayit=1) sayilarak asla YuksekOlasilik URETILMEZ.
        // KONTROL SQL SORGUSUNDA uygulanir - sonradan DTO temizligi YAPILMAZ (bkz. CariHareketAdaylari
        // ile ayni yaklasim).
        return q.Select(h => new AdayHam
        {
            Anahtar = h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && h.KaynakId != null
                && _dbContext.TahsilatOdemeBelgeleri.Any(b => !b.IsDeleted && b.Id == h.KaynakId
                    && b.CariKart != null && b.CariKart.TesisId == h.KasaBankaHesap!.TesisId)
                ? "BELGE:" + h.KaynakId.ToString()
                : "KASAHAREKET:" + h.Id.ToString(),
            Kaynak = OdemeAdayKaynaklari.KasaHareket,
            KaynakId = h.Id,
            KaynakOncelik = 2,
            Tarih = h.HareketTarihi,
            Tutar = h.Tutar,
            TutarTuru = "Hareket Tutarı",
            ParaBirimi = h.ParaBirimi,
            TesisId = h.KasaBankaHesap!.TesisId,
            KurumId = _dbContext.Tesisler.Where(t => t.Id == h.KasaBankaHesap.TesisId).Select(t => (int?)t.KurumId).FirstOrDefault(),
            CariKartId = h.CariKartId,
            KasaBankaHesapId = h.KasaBankaHesapId,
            KasaBankaHesapTipi = h.KasaBankaHesapId == null ? null
                : _dbContext.KasaBankaHesaplari.Where(k => k.Id == h.KasaBankaHesapId).Select(k => k.Tip).FirstOrDefault(),
            MuhasebeHesapPlaniId = h.KasaBankaHesapId == null ? null
                : _dbContext.KasaBankaHesaplari.Where(k => k.Id == h.KasaBankaHesapId).Select(k => k.MuhasebeHesapPlaniId).FirstOrDefault(),
            MaliYil = null,
            Donem = null,
            TahsilatOdemeBelgesiId = h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && h.KaynakId != null
                && _dbContext.TahsilatOdemeBelgeleri.Any(b => !b.IsDeleted && b.Id == h.KaynakId
                    && b.CariKart != null && b.CariKart.TesisId == h.KasaBankaHesap!.TesisId)
                ? h.KaynakId : null,
            CariHareketId = null,
            PosTahsilatValorId = null,
            MuhasebeFisId = null,
            BelgeNo = h.BelgeNo,
            KopuklukFisYok = 0,
            KopuklukCariYok = 0,
            KopuklukValorYok = 0,
            KopuklukHedefHesapYok = 0,
            // Odeme belgesinden dogdugu ISARETLI ama kaynak belge HICBIR TESISTE bulunamiyor
            // (silinmis/hic var olmamis) - tesis uyumsuzlugundan AYRI bir kopuklukdur.
            KopuklukOdemeBaglantisiYok = h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi
                && !_dbContext.TahsilatOdemeBelgeleri.Any(b => !b.IsDeleted && b.Id == h.KaynakId)
                ? 1 : 0,
            KopuklukSoftDelete = 0,
            // Belge AKTIF olarak baska bir yerde bulunuyor (silinmemis) ANCAK bu hareketin
            // kasa/banka hesabinin tesisiyle UYUSMUYOR (veya tesis iliskisi dogrulanamiyor) -
            // yetki kapsami disinda bir baglanti, "belge yok" ile KARISTIRILMAZ.
            KopuklukYetkiDisindaOdemeBaglantisi = h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && h.KaynakId != null
                && !_dbContext.TahsilatOdemeBelgeleri.Any(b => !b.IsDeleted && b.Id == h.KaynakId
                    && b.CariKart != null && b.CariKart.TesisId == h.KasaBankaHesap!.TesisId)
                && _dbContext.TahsilatOdemeBelgeleri.Any(b => !b.IsDeleted && b.Id == h.KaynakId)
                ? 1 : 0,
            // BAGIMSIZ: hicbir odeme belgesine GECERLI sekilde baglanmamis kasa hareketi (KaynakModul
            // farkli/bos OLDUGU gibi, KaynakModul dogru ama baglanti tesis uyumsuzlugu YUZUNDEN
            // GECERSIZ oldugunda da bagimsiz sayilir).
            BagimsizKayit = h.KaynakModul != MuhasebeKaynakModulleri.TahsilatOdemeBelgesi
                || h.KaynakId == null
                || !_dbContext.TahsilatOdemeBelgeleri.Any(b => !b.IsDeleted && b.Id == h.KaynakId
                    && b.CariKart != null && b.CariKart.TesisId == h.KasaBankaHesap!.TesisId)
                ? 1 : 0
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
        if (!string.IsNullOrWhiteSpace(filter.BelgeNo)) q = q.Where(h => h.BelgeNo != null && h.BelgeNo.Contains(filter.BelgeNo));
        if (filter.SadeceIptalEdilmisOlanlar.HasValue)
        {
            q = filter.SadeceIptalEdilmisOlanlar.Value
                ? q.Where(h => h.Durum != CariHareketDurumlari.Aktif)
                : q.Where(h => h.Durum == CariHareketDurumlari.Aktif);
        }
        if (!string.IsNullOrWhiteSpace(filter.MuhasebeFisNo) || !string.IsNullOrWhiteSpace(filter.RezervasyonReferansNo))
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
            KaynakOncelik = 2,
            Tarih = h.HareketTarihi,
            Tutar = h.Tutar,
            TutarTuru = "Hareket Tutarı",
            ParaBirimi = h.ParaBirimi,
            TesisId = h.KasaBankaHesap!.TesisId,
            KurumId = _dbContext.Tesisler.Where(t => t.Id == h.KasaBankaHesap.TesisId).Select(t => (int?)t.KurumId).FirstOrDefault(),
            CariKartId = h.CariKartId,
            KasaBankaHesapId = h.KasaBankaHesapId,
            KasaBankaHesapTipi = h.KasaBankaHesapId == null ? null
                : _dbContext.KasaBankaHesaplari.Where(k => k.Id == h.KasaBankaHesapId).Select(k => k.Tip).FirstOrDefault(),
            MuhasebeHesapPlaniId = h.KasaBankaHesapId == null ? null
                : _dbContext.KasaBankaHesaplari.Where(k => k.Id == h.KasaBankaHesapId).Select(k => k.MuhasebeHesapPlaniId).FirstOrDefault(),
            MaliYil = null,
            Donem = null,
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
            KopuklukYetkiDisindaOdemeBaglantisi = 0,
            BagimsizKayit = h.KaynakModul != MuhasebeKaynakModulleri.TahsilatOdemeBelgesi ? 1 : 0
        });
    }

    private IQueryable<AdayHam> MuhasebeFisiAdaylari(
        IReadOnlyList<int> tesisIds, OdemeCaprazAramaFilterDto filter, DateTime? baslangic, DateTime? bitis)
    {
        // BAGIMSIZ ARASTIRMA: KaynakModul kisiti YOKTUR - odeme belgesine hic baglanmamis fisler de
        // aday olur (mali etki olusturan durumlarla sinirli).
        //
        // ITpal/ters kayit ayrimi (madde 4): base sorgu YALNIZCA mali etki olusturan durumlar
        // (Onayli/TersKayit) ile sinirlidir. "SadeceIptalEdilmisOlanlar=true" istendiginde bu,
        // KAYNAK KAYDIN iptali (Durum=Iptal) DEGIL, bu fisin bir TERS KAYIT niteliginde olup
        // olmadigini ifade eder (Durum=TersKayit) - iki kavram BILINCLI olarak KARISTIRILMAZ.
        var q = _dbContext.MuhasebeFisler.AsNoTracking()
            .Where(f => !f.IsDeleted && tesisIds.Contains(f.TesisId)
                && (f.Durum == MuhasebeFisDurumlari.Onayli || f.Durum == MuhasebeFisDurumlari.TersKayit));

        if (baslangic.HasValue) q = q.Where(f => f.FisTarihi >= baslangic.Value);
        if (bitis.HasValue) q = q.Where(f => f.FisTarihi < bitis.Value);
        if (filter.TutarMin.HasValue) q = q.Where(f => f.ToplamBorc >= filter.TutarMin.Value);
        if (filter.TutarMax.HasValue) q = q.Where(f => f.ToplamBorc <= filter.TutarMax.Value);
        if (!string.IsNullOrWhiteSpace(filter.MuhasebeFisNo)) q = q.Where(f => f.FisNo.Contains(filter.MuhasebeFisNo));
        if (!string.IsNullOrWhiteSpace(filter.BelgeNo)) q = q.Where(f => f.FisNo.Contains(filter.BelgeNo));
        // Para birimi fis BASLIGINDA yok; satir bazindadir - guvenilir sekilde uygulanamadigi icin
        // bu filtre verildiginde fis kaynagi sonuc disi birakilir.
        if (!string.IsNullOrWhiteSpace(filter.ParaBirimi))
        {
            q = q.Where(f => _dbContext.MuhasebeFisSatirlari.Any(s => !s.IsDeleted && s.MuhasebeFisId == f.Id && s.ParaBirimi == filter.ParaBirimi));
        }
        if (!string.IsNullOrWhiteSpace(filter.RezervasyonReferansNo))
        {
            q = q.Where(_ => false); // MuhasebeFis'te guvenilir rezervasyon iliskisi yok.
        }
        if (filter.SadeceIptalEdilmisOlanlar.HasValue)
        {
            q = filter.SadeceIptalEdilmisOlanlar.Value
                ? q.Where(f => f.Durum == MuhasebeFisDurumlari.TersKayit)
                : q.Where(f => f.Durum == MuhasebeFisDurumlari.Onayli);
        }

        return q.Select(f => new AdayHam
        {
            Anahtar = f.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && f.KaynakId != null
                ? "BELGE:" + f.KaynakId.ToString()
                : "FIS:" + f.Id.ToString(),
            Kaynak = OdemeAdayKaynaklari.MuhasebeFis,
            KaynakId = f.Id,
            KaynakOncelik = 5,
            Tarih = f.FisTarihi,
            Tutar = f.ToplamBorc,
            TutarTuru = "Toplam Borç",
            ParaBirimi = _dbContext.MuhasebeFisSatirlari
                .Where(s => !s.IsDeleted && s.MuhasebeFisId == f.Id)
                .OrderBy(s => s.SiraNo).Select(s => s.ParaBirimi).FirstOrDefault(),
            TesisId = f.TesisId,
            KurumId = _dbContext.Tesisler.Where(t => t.Id == f.TesisId).Select(t => (int?)t.KurumId).FirstOrDefault(),
            CariKartId = _dbContext.MuhasebeFisSatirlari
                .Where(s => !s.IsDeleted && s.MuhasebeFisId == f.Id && s.CariKartId != null)
                .OrderBy(s => s.SiraNo).Select(s => s.CariKartId).FirstOrDefault(),
            KasaBankaHesapId = _dbContext.MuhasebeFisSatirlari
                .Where(s => !s.IsDeleted && s.MuhasebeFisId == f.Id && s.KasaBankaHesapId != null)
                .OrderBy(s => s.SiraNo).Select(s => s.KasaBankaHesapId).FirstOrDefault(),
            KasaBankaHesapTipi = _dbContext.MuhasebeFisSatirlari
                .Where(s => !s.IsDeleted && s.MuhasebeFisId == f.Id && s.KasaBankaHesapId != null)
                .OrderBy(s => s.SiraNo)
                .Select(s => _dbContext.KasaBankaHesaplari.Where(k => k.Id == s.KasaBankaHesapId).Select(k => k.Tip).FirstOrDefault())
                .FirstOrDefault(),
            MuhasebeHesapPlaniId = _dbContext.MuhasebeFisSatirlari
                .Where(s => !s.IsDeleted && s.MuhasebeFisId == f.Id)
                .OrderBy(s => s.SiraNo).Select(s => (int?)s.MuhasebeHesapPlaniId).FirstOrDefault(),
            MaliYil = f.MaliYil,
            Donem = f.Donem,
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
            KopuklukYetkiDisindaOdemeBaglantisi = 0,
            BagimsizKayit = f.KaynakModul != MuhasebeKaynakModulleri.TahsilatOdemeBelgesi ? 1 : 0
        });
    }

    // ─────────────────────────────────────────────────────────────
    // Sayfa satirlarini DTO'ya donusturme (DETERMINISTIK - bkz. sinif aciklamasi)
    // ─────────────────────────────────────────────────────────────

    private static List<OdemeAdayiDto> Birlestir(
        List<AdayHam> satirlarKaynakOncelikSirali, List<string> sirali, OdemeCaprazAramaFilterDto filter)
    {
        var sozluk = new Dictionary<string, OdemeAdayiDto>(StringComparer.Ordinal);

        // satirlarKaynakOncelikSirali ZATEN KaynakOncelik+KaynakId'ye gore SIRALI geldigi icin
        // (SQL'de OrderBy uygulandi) buradaki ??= atamalari deterministiktir - hangi kaynagin
        // "kazanacagi" SQL'in fiziksel satir donusum sirasina DEGIL, acikca tanimlanmis onceliğe baglidir.
        foreach (var s in satirlarKaynakOncelikSirali)
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

            // Madde 3: her kaynagin KENDI tutari, kendi anlamiyla ayrica saklanir - birbirinin
            // yerine kullanilmaz.
            aday.KaynakTutarlari.Add(new OdemeKaynakTutariDto
            {
                Kaynak = s.Kaynak,
                KaynakId = s.KaynakId,
                Tutar = s.Tutar,
                ParaBirimi = s.ParaBirimi,
                TutarTuru = s.TutarTuru
            });

            aday.Tarih ??= s.Tarih;
            aday.Tutar ??= s.Tutar;
            aday.ParaBirimi ??= s.ParaBirimi;
            aday.TesisId ??= s.TesisId;
            aday.BulunanKurumId ??= s.KurumId;
            aday.CariKartId ??= s.CariKartId;
            aday.KasaBankaHesapId ??= s.KasaBankaHesapId;
            aday.BulunanKasaBankaHesapTipi ??= s.KasaBankaHesapTipi;
            aday.BulunanMuhasebeHesapPlaniId ??= s.MuhasebeHesapPlaniId;
            aday.BulunanMaliYil ??= s.MaliYil;
            aday.BulunanDonem ??= s.Donem;
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
            if (s.KopuklukYetkiDisindaOdemeBaglantisi == 1)
            {
                aday.AyrintiYetkiNedeniyleGizliMi = true;
                if (!aday.GuvenliNedenKodlari.Contains(OdemeErisimKisitiNedenKodlari.YetkiKapsamiDisindaOdemeBaglantisi))
                {
                    aday.GuvenliNedenKodlari.Add(OdemeErisimKisitiNedenKodlari.YetkiKapsamiDisindaOdemeBaglantisi);
                }
            }
            if (s.BagimsizKayit == 1)
            {
                aday.BagimsizKayitMi = true;
            }
        }

        foreach (var aday in sozluk.Values)
        {
            KarsilastirBeklenenVeBulunan(aday, filter);

            if (aday.BagimsizKayitMi)
            {
                aday.GuvenSeviyesi = OdemeGuvenSeviyeleri.IncelenmesiGereken;
                aday.GuvenGerekcesi =
                    "Bu kayıt herhangi bir tahsilat/ödeme belgesine bağlı değil. Aynı ödemeye ait olup olmadığı " +
                    "yalnızca filtre kriterleriyle (tarih/tutar/cari/hesap) daraltılmıştır; KANITLANMAMIŞTIR.";
            }
            else if (aday.CelisenAlanlar.Count > 0)
            {
                // Kaynaklar arasi baglanti gercek olsa bile, beklenen ile CELISEN bir kayit
                // "yuksek olasilik" olarak sunulamaz - kullanicinin dikkatle incelemesi gerekir.
                aday.GuvenSeviyesi = OdemeGuvenSeviyeleri.IncelenmesiGereken;
                aday.GuvenGerekcesi =
                    "Kayıtlar ödeme belgesi kimliği üzerinden birbirine bağlıdır ANCAK beklenen değerlerden " +
                    $"{aday.CelisenAlanlar.Count} alanda çelişki tespit edildi (bkz. ÇelişenAlanlar) - incelenmelidir.";
            }
            else
            {
                aday.GuvenSeviyesi = OdemeGuvenSeviyeleri.YuksekOlasilik;
                aday.GuvenGerekcesi = "Kayıtlar ödeme belgesi kimliği üzerinden birbirine gerçekten bağlıdır.";
            }
        }

        // Sayfa sirasini SQL'den gelen anahtar sirasina gore koru.
        return [.. sirali.Where(sozluk.ContainsKey).Select(a => sozluk[a])];
    }

    /// <summary>Madde 1: Beklenen* alanlarini bulunan adayin GERCEK degerleriyle karsilastirir.
    /// Hicbir sekilde adayi ELEMEZ - yalnizca Eslesen/Celisen listelerini ve veri kalitesi
    /// uyarilarini doldurur.</summary>
    private static void KarsilastirBeklenenVeBulunan(OdemeAdayiDto aday, OdemeCaprazAramaFilterDto filter)
    {
        void Karsilastir(int? beklenen, int? bulunan, string alanEtiketi, string uyusmazlikKodu, string? veriYokMesaji)
        {
            if (!beklenen.HasValue)
            {
                return;
            }
            if (!bulunan.HasValue)
            {
                if (veriYokMesaji is not null)
                {
                    aday.VeriKalitesiUyarilari.Add(veriYokMesaji);
                }
                return;
            }
            if (beklenen.Value == bulunan.Value)
            {
                aday.EslesenAlanlar.Add(alanEtiketi);
            }
            else
            {
                aday.CelisenAlanlar.Add(uyusmazlikKodu);
            }
        }

        Karsilastir(filter.BeklenenCariKartId, aday.CariKartId, "Cari Kart", OdemeCeliskiKodlari.CariHesapUyusmazligi,
            filter.BeklenenCariKartId.HasValue ? "Bu adayın hangi cari karta ait olduğu bulunamadı; cari karşılaştırması yapılamadı." : null);

        if (filter.BeklenenBankaHesapId.HasValue || filter.BeklenenKasaHesapId.HasValue)
        {
            if (!aday.KasaBankaHesapId.HasValue)
            {
                aday.VeriKalitesiUyarilari.Add("Bu adayın bağlı olduğu kasa/banka hesabı bulunamadı; hesap karşılaştırması yapılamadı.");
            }
            else if (string.Equals(aday.BulunanKasaBankaHesapTipi, KasaBankaHesapTipleri.Banka, StringComparison.Ordinal) && filter.BeklenenBankaHesapId.HasValue)
            {
                Karsilastir(filter.BeklenenBankaHesapId, aday.KasaBankaHesapId, "Banka Hesabı", OdemeCeliskiKodlari.BankaHesabiUyusmazligi, null);
            }
            else if (string.Equals(aday.BulunanKasaBankaHesapTipi, KasaBankaHesapTipleri.NakitKasa, StringComparison.Ordinal) && filter.BeklenenKasaHesapId.HasValue)
            {
                Karsilastir(filter.BeklenenKasaHesapId, aday.KasaBankaHesapId, "Kasa Hesabı", OdemeCeliskiKodlari.KasaHesabiUyusmazligi, null);
            }
            else
            {
                var beklenen = filter.BeklenenBankaHesapId ?? filter.BeklenenKasaHesapId;
                var kod = filter.BeklenenBankaHesapId.HasValue ? OdemeCeliskiKodlari.BankaHesabiUyusmazligi : OdemeCeliskiKodlari.KasaHesabiUyusmazligi;
                Karsilastir(beklenen, aday.KasaBankaHesapId, "Kasa/Banka Hesabı", kod, null);
            }
        }

        Karsilastir(filter.BeklenenMuhasebeHesapPlaniId, aday.BulunanMuhasebeHesapPlaniId, "Muhasebe Hesabı", OdemeCeliskiKodlari.MuhasebeHesabiUyusmazligi,
            filter.BeklenenMuhasebeHesapPlaniId.HasValue ? "Bu adayın muhasebe hesabı belirlenemedi; hesap karşılaştırması yapılamadı." : null);

        Karsilastir(filter.BeklenenMaliYil, aday.BulunanMaliYil, "Mali Yıl", OdemeCeliskiKodlari.MaliYilUyusmazligi,
            filter.BeklenenMaliYil.HasValue ? "Bu adayın mali yılı belirlenemedi (bağlı bir muhasebe fişi yok/bulunamadı)." : null);

        Karsilastir(filter.BeklenenDonem, aday.BulunanDonem, "Dönem", OdemeCeliskiKodlari.DonemUyusmazligi,
            filter.BeklenenDonem.HasValue ? "Bu adayın dönemi belirlenemedi (bağlı bir muhasebe fişi yok/bulunamadı)." : null);

        Karsilastir(filter.BeklenenTesisId, aday.TesisId, "Tesis", OdemeCeliskiKodlari.TesisUyusmazligi, null);
        Karsilastir(filter.BeklenenKurumId, aday.BulunanKurumId, "Kurum", OdemeCeliskiKodlari.KurumUyusmazligi, null);

        if (filter.BeklenenTutar.HasValue)
        {
            if (!aday.Tutar.HasValue)
            {
                aday.VeriKalitesiUyarilari.Add("Bu adayın tutarı belirlenemedi; tutar karşılaştırması yapılamadı.");
            }
            else if (Math.Abs(aday.Tutar.Value - filter.BeklenenTutar.Value) <= TutarKarsilastirmaToleransi)
            {
                aday.EslesenAlanlar.Add("Tutar");
            }
            else
            {
                aday.CelisenAlanlar.Add(OdemeCeliskiKodlari.TutarUyusmazligi);
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.BeklenenParaBirimi))
        {
            if (string.IsNullOrWhiteSpace(aday.ParaBirimi))
            {
                aday.VeriKalitesiUyarilari.Add("Bu adayın para birimi belirlenemedi; para birimi karşılaştırması yapılamadı.");
            }
            else if (string.Equals(aday.ParaBirimi, filter.BeklenenParaBirimi, StringComparison.OrdinalIgnoreCase))
            {
                aday.EslesenAlanlar.Add("Para Birimi");
            }
            else
            {
                aday.CelisenAlanlar.Add(OdemeCeliskiKodlari.ParaBirimiUyusmazligi);
            }
        }
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
