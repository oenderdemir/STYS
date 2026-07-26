using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.OdemeIzleme.Dtos;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.OdemeIzleme.Services;

/// <summary>
/// Odeme arastirmasini ODEME BELGESI MERKEZLI olmaktan cikaran, capraz-kaynak aday uretici.
///
/// Her kaynak icin AYRI ve dar bir "aday saglayici" calisir; sonuclar ortak bir tekillestirme
/// anahtari uzerinden birlestirilir. Boylece ayni mali islem belge/cari hareket/valor/fis
/// kayitlarinda bulundugu icin BIRDEN FAZLA kez sayilmaz.
///
/// TESIS KAPSAMI her saglayicinin KENDI sorgusunda uygulanir - istemciden gelen id'ye guvenilmez.
/// </summary>
public class OdemeCaprazAramaService : IOdemeCaprazAramaService
{
    private const int VarsayilanSayfaBoyutu = 25;
    private const int MaksimumSayfaBoyutu = 200;

    private readonly StysAppDbContext _dbContext;
    private readonly IMuhasebeTesisScopeService _tesisScopeService;

    public OdemeCaprazAramaService(StysAppDbContext dbContext, IMuhasebeTesisScopeService tesisScopeService)
    {
        _dbContext = dbContext;
        _tesisScopeService = tesisScopeService;
    }

    public async Task<PagedResult<OdemeAdayiDto>> AraAsync(
        PagedRequest request, OdemeCaprazAramaFilterDto filter, CancellationToken cancellationToken = default)
    {
        var (pageNumber, pageSizeIstenen) = request.Normalize();
        var pageSize = Math.Min(pageSizeIstenen, MaksimumSayfaBoyutu);

        var tesisIds = await ResolveTesisIdsAsync(filter.TesisId, cancellationToken);
        if (tesisIds.Count == 0)
        {
            return new PagedResult<OdemeAdayiDto>([], pageNumber, pageSize, 0);
        }

        var baslangic = filter.TarihBaslangic?.ToDateTime(TimeOnly.MinValue);
        var bitis = filter.TarihBitis?.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var adaylar = new Dictionary<string, OdemeAdayiDto>(StringComparer.Ordinal);

        await EkleOdemeBelgesiAdaylariAsync(adaylar, tesisIds, filter, baslangic, bitis, cancellationToken);
        await EkleCariHareketAdaylariAsync(adaylar, tesisIds, filter, baslangic, bitis, cancellationToken);
        await EklePosValorAdaylariAsync(adaylar, tesisIds, filter, baslangic, bitis, cancellationToken);
        await EkleKasaBankaHareketAdaylariAsync(adaylar, tesisIds, filter, baslangic, bitis, cancellationToken);
        await EkleMuhasebeFisiAdaylariAsync(adaylar, tesisIds, filter, baslangic, bitis, cancellationToken);

        var sonuclar = adaylar.Values.AsEnumerable();

        if (filter.SadeceKopukOlanlar)
        {
            sonuclar = sonuclar.Where(x => x.KopuklukKodlari.Count > 0);
        }
        if (!string.IsNullOrWhiteSpace(filter.KopuklukTipi))
        {
            sonuclar = sonuclar.Where(x => x.KopuklukKodlari.Contains(filter.KopuklukTipi));
        }

        // KARARLI SIRALAMA: tarih (yeniden eskiye), sonra tekillestirme anahtari - sayfalar
        // arasinda atlama/tekrar olusmaz.
        var sirali = sonuclar
            .OrderByDescending(x => x.Tarih ?? DateTime.MinValue)
            .ThenBy(x => x.TekillestirmeAnahtari, StringComparer.Ordinal)
            .ToList();

        var sayfa = sirali.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<OdemeAdayiDto>(sayfa, pageNumber, pageSize, sirali.Count);
    }

    // ─────────────────────────────────────────────────────────────
    // Aday saglayicilari
    // ─────────────────────────────────────────────────────────────

    private async Task EkleOdemeBelgesiAdaylariAsync(
        Dictionary<string, OdemeAdayiDto> adaylar, IReadOnlyList<int> tesisIds, OdemeCaprazAramaFilterDto filter,
        DateTime? baslangic, DateTime? bitis, CancellationToken cancellationToken)
    {
        var query = _dbContext.TahsilatOdemeBelgeleri.AsNoTracking()
            .Where(b => !b.IsDeleted && b.CariKart != null && b.CariKart.TesisId.HasValue && tesisIds.Contains(b.CariKart.TesisId.Value));

        query = UygulaOrtakFiltreler(query, filter, baslangic, bitis);

        var kayitlar = await query
            .Select(b => new
            {
                b.Id, b.BelgeNo, b.BelgeTarihi, b.Tutar, b.ParaBirimi, b.CariKartId, b.MuhasebeFisId,
                b.KasaBankaHesapId, b.OdemeYontemi, b.Durum, b.KapatilacakCariHareketId,
                CariUnvan = b.CariKart!.UnvanAdSoyad,
                TesisId = b.CariKart.TesisId,
                // Kopukluk tespiti: bu belgeye ait cari hareket / POS valor VAR MI (tek sorguda,
                // N+1 olmadan - EF bunlari EXISTS alt sorgusuna cevirir).
                CariHareketiVarMi = _dbContext.CariHareketler.Any(h => !h.IsDeleted
                    && h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && h.KaynakId == b.Id),
                PosValoruVarMi = _dbContext.PosTahsilatValorleri.Any(v => !v.IsDeleted && v.TahsilatOdemeBelgesiId == b.Id)
            })
            .ToListAsync(cancellationToken);

        foreach (var k in kayitlar)
        {
            var aday = AdayAl(adaylar, AnahtarBelge(k.Id), OdemeAdayKaynaklari.TahsilatOdemeBelgesi, k.Id);
            aday.TahsilatOdemeBelgesiId = k.Id;
            aday.MuhasebeFisId = k.MuhasebeFisId;
            aday.KasaBankaHesapId = k.KasaBankaHesapId;
            aday.BelgeNo = k.BelgeNo;
            aday.Tarih = k.BelgeTarihi;
            aday.Tutar = k.Tutar;
            aday.ParaBirimi = k.ParaBirimi;
            aday.CariKartId = k.CariKartId;
            aday.CariUnvan = k.CariUnvan;
            aday.TesisId = k.TesisId;

            if (k.Durum != TahsilatOdemeBelgeDurumlari.Aktif)
            {
                continue; // iptal edilmis belge icin kopukluk uyarisi uretilmez.
            }

            if (!k.MuhasebeFisId.HasValue && OdemeYontemleri.NakitHareketiGerektirenler.Contains(k.OdemeYontemi))
            {
                EkleKopukluk(aday, OdemeKopuklukTipleri.MuhasebeFisiOlmayanOdemeBelgesi,
                    "Ödeme nakit/banka/POS hareketi doğurduğu hâlde bağlı bir muhasebe fişi yok.");
            }

            if (!k.CariHareketiVarMi && k.KapatilacakCariHareketId.HasValue)
            {
                EkleKopukluk(aday, OdemeKopuklukTipleri.CariHareketEtkisiOlmayanOdemeBelgesi,
                    "Ödeme bir borcu kapatmak üzere işaretlenmiş ancak karşılık gelen cari hareket bulunamadı.");
            }

            if (!k.PosValoruVarMi && k.OdemeYontemi == OdemeYontemleri.KrediKarti)
            {
                EkleKopukluk(aday, OdemeKopuklukTipleri.ValorKaydiOlmayanPosTahsilati,
                    "Kredi kartı tahsilatı olduğu hâlde POS valör takip kaydı yok.");
            }
        }
    }

    private async Task EkleCariHareketAdaylariAsync(
        Dictionary<string, OdemeAdayiDto> adaylar, IReadOnlyList<int> tesisIds, OdemeCaprazAramaFilterDto filter,
        DateTime? baslangic, DateTime? bitis, CancellationToken cancellationToken)
    {
        var query = _dbContext.CariHareketler.AsNoTracking()
            .Where(h => !h.IsDeleted && h.CariKart != null && h.CariKart.TesisId.HasValue && tesisIds.Contains(h.CariKart.TesisId.Value)
                && h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi);

        if (baslangic.HasValue) query = query.Where(h => h.HareketTarihi >= baslangic.Value);
        if (bitis.HasValue) query = query.Where(h => h.HareketTarihi < bitis.Value);
        if (filter.CariKartId.HasValue) query = query.Where(h => h.CariKartId == filter.CariKartId.Value);

        var kayitlar = await query
            .Select(h => new
            {
                h.Id, h.HareketTarihi, h.BorcTutari, h.AlacakTutari, h.ParaBirimi, h.CariKartId, h.KaynakId,
                h.BelgeNo, h.Aciklama, h.Durum,
                CariUnvan = h.CariKart!.UnvanAdSoyad,
                TesisId = h.CariKart.TesisId,
                // Kaynak odeme belgesi GERCEKTEN var mi (soft-delete edilmis olabilir).
                KaynakBelgeVarMi = _dbContext.TahsilatOdemeBelgeleri.Any(b => !b.IsDeleted && b.Id == h.KaynakId),
                KaynakBelgeSilinmisMi = _dbContext.TahsilatOdemeBelgeleri.IgnoreQueryFilters().Any(b => b.IsDeleted && b.Id == h.KaynakId)
            })
            .ToListAsync(cancellationToken);

        foreach (var k in kayitlar)
        {
            // Kaynak belge biliniyorsa AYNI mali islem olarak tekillestir.
            var anahtar = k.KaynakId.HasValue ? AnahtarBelge(k.KaynakId.Value) : $"CH:{k.Id}";
            var aday = AdayAl(adaylar, anahtar, OdemeAdayKaynaklari.CariHareket, k.Id);
            aday.CariHareketId = k.Id;
            aday.Tarih ??= k.HareketTarihi;
            aday.Tutar ??= k.BorcTutari - k.AlacakTutari;
            aday.ParaBirimi ??= k.ParaBirimi;
            aday.CariKartId ??= k.CariKartId;
            aday.CariUnvan ??= k.CariUnvan;
            aday.TesisId ??= k.TesisId;
            aday.BelgeNo ??= k.BelgeNo;
            aday.Aciklama ??= k.Aciklama;

            if (k.Durum != CariHareketDurumlari.Aktif)
            {
                continue;
            }

            if (!k.KaynakBelgeVarMi)
            {
                EkleKopukluk(aday, OdemeKopuklukTipleri.OdemeBelgesiOlmayanCariHareket,
                    "Cari hareket bir tahsilat/ödeme belgesinden doğmuş görünüyor ancak kaynak belge bulunamadı.");

                if (k.KaynakBelgeSilinmisMi)
                {
                    EkleKopukluk(aday, OdemeKopuklukTipleri.SoftDeleteIliskiNedeniyleGorunmeyen,
                        "Kaynak ödeme belgesi silinmiş (soft-delete); normal aramalarda görünmez.");
                }
            }
        }
    }

    private async Task EklePosValorAdaylariAsync(
        Dictionary<string, OdemeAdayiDto> adaylar, IReadOnlyList<int> tesisIds, OdemeCaprazAramaFilterDto filter,
        DateTime? baslangic, DateTime? bitis, CancellationToken cancellationToken)
    {
        var query = _dbContext.PosTahsilatValorleri.AsNoTracking()
            .Where(v => !v.IsDeleted && tesisIds.Contains(v.TesisId));

        if (baslangic.HasValue) query = query.Where(v => v.OdemeTarihi >= baslangic.Value);
        if (bitis.HasValue) query = query.Where(v => v.OdemeTarihi < bitis.Value);
        if (filter.TutarMin.HasValue) query = query.Where(v => v.NetTutar >= filter.TutarMin.Value);
        if (filter.TutarMax.HasValue) query = query.Where(v => v.NetTutar <= filter.TutarMax.Value);

        var kayitlar = await query
            .Select(v => new
            {
                v.Id, v.TesisId, v.TahsilatOdemeBelgesiId, v.BagliBankaHesapId, v.OdemeTarihi, v.NetTutar,
                v.ParaBirimi, v.Durum, v.MuhasebeFisId,
                // Hedef banka hesabi GERCEKTEN mevcut ve aktif mi.
                HedefHesapGecerliMi = _dbContext.KasaBankaHesaplari.Any(
                    k => !k.IsDeleted && k.AktifMi && k.Id == v.BagliBankaHesapId),
                HedefHesapTesisId = _dbContext.KasaBankaHesaplari
                    .Where(k => k.Id == v.BagliBankaHesapId).Select(k => k.TesisId).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        foreach (var k in kayitlar)
        {
            var aday = AdayAl(adaylar, AnahtarBelge(k.TahsilatOdemeBelgesiId), OdemeAdayKaynaklari.PosTahsilatValor, k.Id);
            aday.PosTahsilatValorId = k.Id;
            aday.TahsilatOdemeBelgesiId ??= k.TahsilatOdemeBelgesiId;
            aday.Tarih ??= k.OdemeTarihi;
            aday.Tutar ??= k.NetTutar;
            aday.ParaBirimi ??= k.ParaBirimi;
            aday.TesisId ??= k.TesisId;
            aday.KasaBankaHesapId ??= k.BagliBankaHesapId;

            if (k.Durum is PosTahsilatValorDurumlari.Iptal)
            {
                continue;
            }

            if (!k.BagliBankaHesapId.HasValue || !k.HedefHesapGecerliMi)
            {
                EkleKopukluk(aday, OdemeKopuklukTipleri.HedefBankaHesabiOlmayanValor,
                    "POS valör kaydının hedef banka hesabı tanımsız, bulunamıyor veya pasif.");
            }
            else if (k.HedefHesapTesisId.HasValue && k.HedefHesapTesisId.Value != k.TesisId)
            {
                EkleKopukluk(aday, OdemeKopuklukTipleri.HedefBankaHesabiOlmayanValor,
                    $"POS valör kaydının tesisi ({k.TesisId}) ile hedef banka hesabının tesisi ({k.HedefHesapTesisId}) farklı.");
            }
        }
    }

    private async Task EkleKasaBankaHareketAdaylariAsync(
        Dictionary<string, OdemeAdayiDto> adaylar, IReadOnlyList<int> tesisIds, OdemeCaprazAramaFilterDto filter,
        DateTime? baslangic, DateTime? bitis, CancellationToken cancellationToken)
    {
        // Kasa hareketleri
        var kasaQuery = _dbContext.KasaHareketleri.AsNoTracking()
            .Where(h => !h.IsDeleted && h.KasaBankaHesap != null && h.KasaBankaHesap.TesisId.HasValue
                && tesisIds.Contains(h.KasaBankaHesap.TesisId.Value)
                && h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi);

        if (baslangic.HasValue) kasaQuery = kasaQuery.Where(h => h.HareketTarihi >= baslangic.Value);
        if (bitis.HasValue) kasaQuery = kasaQuery.Where(h => h.HareketTarihi < bitis.Value);

        var kasaKayitlari = await kasaQuery
            .Select(h => new
            {
                h.Id, h.HareketTarihi, h.Tutar, h.ParaBirimi, h.CariKartId, h.KaynakId, h.KasaBankaHesapId, h.BelgeNo,
                TesisId = h.KasaBankaHesap!.TesisId,
                KaynakBelgeVarMi = _dbContext.TahsilatOdemeBelgeleri.Any(b => !b.IsDeleted && b.Id == h.KaynakId)
            })
            .ToListAsync(cancellationToken);

        foreach (var k in kasaKayitlari)
        {
            var anahtar = k.KaynakId.HasValue ? AnahtarBelge(k.KaynakId.Value) : $"KH:{k.Id}";
            var aday = AdayAl(adaylar, anahtar, OdemeAdayKaynaklari.KasaHareket, k.Id);
            aday.Tarih ??= k.HareketTarihi;
            aday.Tutar ??= k.Tutar;
            aday.ParaBirimi ??= k.ParaBirimi;
            aday.CariKartId ??= k.CariKartId;
            aday.TesisId ??= k.TesisId;
            aday.KasaBankaHesapId ??= k.KasaBankaHesapId;
            aday.BelgeNo ??= k.BelgeNo;

            if (!k.KaynakBelgeVarMi)
            {
                EkleKopukluk(aday, OdemeKopuklukTipleri.OdemeBelgesiOlmayanKasaHareketi,
                    "Kasa hareketi bir tahsilat/ödeme belgesinden doğmuş görünüyor ancak kaynak belge bulunamadı.");
            }
        }

        // Banka hareketleri
        var bankaQuery = _dbContext.BankaHareketleri.AsNoTracking()
            .Where(h => !h.IsDeleted && h.KasaBankaHesap != null && h.KasaBankaHesap.TesisId.HasValue
                && tesisIds.Contains(h.KasaBankaHesap.TesisId.Value)
                && h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi);

        if (baslangic.HasValue) bankaQuery = bankaQuery.Where(h => h.HareketTarihi >= baslangic.Value);
        if (bitis.HasValue) bankaQuery = bankaQuery.Where(h => h.HareketTarihi < bitis.Value);

        var bankaKayitlari = await bankaQuery
            .Select(h => new
            {
                h.Id, h.HareketTarihi, h.Tutar, h.ParaBirimi, h.CariKartId, h.KaynakId, h.KasaBankaHesapId, h.BelgeNo,
                TesisId = h.KasaBankaHesap!.TesisId,
                KaynakBelgeVarMi = _dbContext.TahsilatOdemeBelgeleri.Any(b => !b.IsDeleted && b.Id == h.KaynakId)
            })
            .ToListAsync(cancellationToken);

        foreach (var k in bankaKayitlari)
        {
            var anahtar = k.KaynakId.HasValue ? AnahtarBelge(k.KaynakId.Value) : $"BH:{k.Id}";
            var aday = AdayAl(adaylar, anahtar, OdemeAdayKaynaklari.BankaHareket, k.Id);
            aday.Tarih ??= k.HareketTarihi;
            aday.Tutar ??= k.Tutar;
            aday.ParaBirimi ??= k.ParaBirimi;
            aday.CariKartId ??= k.CariKartId;
            aday.TesisId ??= k.TesisId;
            aday.KasaBankaHesapId ??= k.KasaBankaHesapId;
            aday.BelgeNo ??= k.BelgeNo;

            if (!k.KaynakBelgeVarMi)
            {
                EkleKopukluk(aday, OdemeKopuklukTipleri.OdemeBelgesiOlmayanBankaHareketi,
                    "Banka hareketi bir tahsilat/ödeme belgesinden doğmuş görünüyor ancak kaynak belge bulunamadı.");
            }
        }
    }

    private async Task EkleMuhasebeFisiAdaylariAsync(
        Dictionary<string, OdemeAdayiDto> adaylar, IReadOnlyList<int> tesisIds, OdemeCaprazAramaFilterDto filter,
        DateTime? baslangic, DateTime? bitis, CancellationToken cancellationToken)
    {
        // Odeme/tahsilat kaynakli oldugu ISARETLENMIS fisler - ama kaynak belge bulunamiyorsa
        // "odeme baglantisi olmayan muhasebe fisi" kopuklugudur.
        var query = _dbContext.MuhasebeFisler.AsNoTracking()
            .Where(f => !f.IsDeleted && tesisIds.Contains(f.TesisId)
                && f.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi
                && (f.Durum == MuhasebeFisDurumlari.Onayli || f.Durum == MuhasebeFisDurumlari.TersKayit));

        if (baslangic.HasValue) query = query.Where(f => f.FisTarihi >= baslangic.Value);
        if (bitis.HasValue) query = query.Where(f => f.FisTarihi < bitis.Value);

        var kayitlar = await query
            .Select(f => new
            {
                f.Id, f.FisNo, f.FisTarihi, f.TesisId, f.KaynakId, f.ToplamBorc, f.Aciklama,
                KaynakBelgeVarMi = f.KaynakId != null && _dbContext.TahsilatOdemeBelgeleri.Any(b => !b.IsDeleted && b.Id == f.KaynakId),
                KaynakBelgeSilinmisMi = f.KaynakId != null && _dbContext.TahsilatOdemeBelgeleri.IgnoreQueryFilters().Any(b => b.IsDeleted && b.Id == f.KaynakId)
            })
            .ToListAsync(cancellationToken);

        foreach (var k in kayitlar)
        {
            var anahtar = k.KaynakId.HasValue ? AnahtarBelge(k.KaynakId.Value) : $"FIS:{k.Id}";
            var aday = AdayAl(adaylar, anahtar, OdemeAdayKaynaklari.MuhasebeFis, k.Id);
            aday.MuhasebeFisId ??= k.Id;
            aday.Tarih ??= k.FisTarihi;
            aday.Tutar ??= k.ToplamBorc;
            aday.TesisId ??= k.TesisId;
            aday.BelgeNo ??= k.FisNo;
            aday.Aciklama ??= k.Aciklama;

            if (!k.KaynakBelgeVarMi)
            {
                EkleKopukluk(aday, OdemeKopuklukTipleri.OdemeBaglantisiOlmayanMuhasebeFisi,
                    "Muhasebe fişi tahsilat/ödeme kaynaklı görünüyor ancak bağlı ödeme belgesi bulunamadı.");

                if (k.KaynakBelgeSilinmisMi)
                {
                    EkleKopukluk(aday, OdemeKopuklukTipleri.SoftDeleteIliskiNedeniyleGorunmeyen,
                        "Kaynak ödeme belgesi silinmiş (soft-delete); normal aramalarda görünmez.");
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Yardimcilar
    // ─────────────────────────────────────────────────────────────

    /// <summary>Ayni mali islemin TEK anahtari - odeme belgesi id'si bilindiginde her kaynak ayni
    /// anahtari uretir, boylece belge/cari hareket/valor/fis MUKERRER aday olusturmaz.</summary>
    private static string AnahtarBelge(int belgeId) => $"BELGE:{belgeId}";

    private static OdemeAdayiDto AdayAl(Dictionary<string, OdemeAdayiDto> adaylar, string anahtar, string kaynak, int kaynakId)
    {
        if (!adaylar.TryGetValue(anahtar, out var aday))
        {
            aday = new OdemeAdayiDto { TekillestirmeAnahtari = anahtar, Kaynak = kaynak, KaynakId = kaynakId };
            adaylar[anahtar] = aday;
        }

        if (!aday.BulunduguKaynaklar.Contains(kaynak))
        {
            aday.BulunduguKaynaklar.Add(kaynak);
        }

        return aday;
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

    private static IQueryable<TahsilatOdemeBelgesi> UygulaOrtakFiltreler(
        IQueryable<TahsilatOdemeBelgesi> query, OdemeCaprazAramaFilterDto filter, DateTime? baslangic, DateTime? bitis)
    {
        if (baslangic.HasValue) query = query.Where(b => b.BelgeTarihi >= baslangic.Value);
        if (bitis.HasValue) query = query.Where(b => b.BelgeTarihi < bitis.Value);
        if (filter.TutarMin.HasValue) query = query.Where(b => b.Tutar >= filter.TutarMin.Value);
        if (filter.TutarMax.HasValue) query = query.Where(b => b.Tutar <= filter.TutarMax.Value);
        if (filter.CariKartId.HasValue) query = query.Where(b => b.CariKartId == filter.CariKartId.Value);
        return query;
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
