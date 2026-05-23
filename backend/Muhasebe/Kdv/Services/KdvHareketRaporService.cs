using ClosedXML.Excel;
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

    private const int MaxExportRows = 50000;

    public KdvHareketRaporService(
        StysAppDbContext db,
        IUserAccessScopeService userAccessScopeService)
    {
        _db = db;
        _userAccessScopeService = userAccessScopeService;
    }

    /// <summary>
    /// Ortak filtreli stok hareketi sorgusunu oluşturur.
    /// Muhasebe fiş join'i ve MusFisDurumu filtresi bu metoda dahil DEĞİLDİR;
    /// her consumer kendi ihtiyacına göre join'i ve ek filtreleri ekler.
    /// </summary>
    private async Task<IQueryable<StokHareket>> BuildFilteredStokQueryAsync(
        KdvHareketRaporFilterDto filter,
        CancellationToken cancellationToken)
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

        return stokQuery;
    }

    public async Task<KdvHareketRaporDto> GetRaporAsync(
        KdvHareketRaporFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var stokQuery = await BuildFilteredStokQueryAsync(filter, cancellationToken);

        // Muhasebe fiş left join
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

    public async Task<byte[]> ExportExcelAsync(
        KdvHareketRaporFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var stokQuery = await BuildFilteredStokQueryAsync(filter, cancellationToken);

        // Muhasebe fiş left join
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

        // Toplam kayıt sayısını al — 50K limit kontrolü
        var totalCount = await query.CountAsync(cancellationToken);
        if (totalCount > MaxExportRows)
            throw new InvalidOperationException(
                $"Filtrelere uyan {totalCount:N0} kayıt bulundu. Excel export en fazla {MaxExportRows:N0} kayıt için desteklenmektedir. " +
                "Lütfen tarih aralığını daraltın veya ek filtreler kullanın.");

        // Tüm filtrelenmiş dataset'i oku (Take yok, export için tüm kayıtlar)
        var allRows = await query
            .OrderByDescending(x => x.stok.HareketTarihi)
            .ThenByDescending(x => x.stok.Id)
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

        // Özet hesapla
        var kdvliSayisi = allRows.Count(x => x.KdvUygulamaTipi == 1);
        var istisnaliSayisi = allRows.Count(x => x.KdvUygulamaTipi == 2 || x.KdvUygulamaTipi == 3);
        var kdvKapsamDisiSayisi = allRows.Count(x => x.KdvUygulamaTipi == 4);
        var tevkifatliSayisi = allRows.Count(x => x.KdvUygulamaTipi == 5);
        var fisiOlanSayisi = allRows.Count(x => x.MusFisId.HasValue);
        var fisiOlmayanSayisi = allRows.Count(x => !x.MusFisId.HasValue);
        var toplamKdvTutari = allRows.Sum(x => x.KdvTutari);
        var toplamTutar = allRows.Sum(x => x.Tutar);

        // Excel oluştur
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("KDV Hareket Raporu");

        var now = DateTime.Now;
        var baslangicStr = filter.BaslangicTarihi.ToString("dd.MM.yyyy");
        var bitisStr = filter.BitisTarihi.ToString("dd.MM.yyyy");

        // Başlık
        ws.Cell(1, 1).Value = "KDV Hareket Raporu";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        // Meta bilgiler
        ws.Cell(3, 1).Value = "Export Tarihi:";
        ws.Cell(3, 2).Value = now.ToString("dd.MM.yyyy HH:mm");
        ws.Cell(4, 1).Value = "Tarih Aralığı:";
        ws.Cell(4, 2).Value = $"{baslangicStr} — {bitisStr}";
        ws.Cell(5, 1).Value = "Toplam Kayıt:";
        ws.Cell(5, 2).Value = totalCount;

        // Özet bölümü (row 7-11)
        ws.Cell(7, 1).Value = "ÖZET";
        ws.Cell(7, 1).Style.Font.Bold = true;
        ws.Cell(7, 1).Style.Font.FontSize = 12;

        ws.Cell(8, 1).Value = "KDV'li:";
        ws.Cell(8, 2).Value = kdvliSayisi;
        ws.Cell(8, 3).Value = "İstisnalı:";
        ws.Cell(8, 4).Value = istisnaliSayisi;
        ws.Cell(8, 5).Value = "Kapsam Dışı:";
        ws.Cell(8, 6).Value = kdvKapsamDisiSayisi;
        ws.Cell(8, 7).Value = "Tevkifatlı:";
        ws.Cell(8, 8).Value = tevkifatliSayisi;
        ws.Cell(9, 1).Value = "Fişi Olan:";
        ws.Cell(9, 2).Value = fisiOlanSayisi;
        ws.Cell(9, 3).Value = "Fişi Olmayan:";
        ws.Cell(9, 4).Value = fisiOlmayanSayisi;
        ws.Cell(9, 5).Value = "Toplam KDV:";
        ws.Cell(9, 6).Value = toplamKdvTutari;
        ws.Cell(9, 6).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(9, 7).Value = "Toplam Tutar (KDV Hariç):";
        ws.Cell(9, 8).Value = toplamTutar;
        ws.Cell(9, 8).Style.NumberFormat.Format = "#,##0.00";

        // Sütun başlıkları (row 13)
        var headers = new[]
        {
            "Tarih", "İşlem", "Depo", "Taşınır Kod", "Taşınır Ad",
            "Miktar", "Birim Fiyat", "Tutar", "KDV Tipi",
            "İstisna Kodu", "İstisna Açıklaması", "KDV %",
            "KDV Tutarı", "KDV'li Tutar", "Muh. Fiş No", "Muh. Fiş Durumu",
            "Belge No", "Açıklama"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(13, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
        }
        ws.SheetView.FreezeRows(13);

        // Veri satırları (row 14'ten başlayarak)
        int row = 14;
        foreach (var satir in allRows)
        {
            ws.Cell(row, 1).Value = satir.HareketTarihi;
            ws.Cell(row, 1).Style.DateFormat.Format = "dd.MM.yyyy HH:mm";
            ws.Cell(row, 2).Value = satir.HareketTipi;
            ws.Cell(row, 3).Value = satir.DepoAdi;
            ws.Cell(row, 4).Value = satir.TasinirKod;
            ws.Cell(row, 5).Value = satir.TasinirAd;
            ws.Cell(row, 6).Value = (double)satir.Miktar;
            ws.Cell(row, 7).Value = satir.BirimFiyat;
            ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 8).Value = satir.Tutar;
            ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 9).Value = satir.KdvUygulamaTipiAd;
            ws.Cell(row, 10).Value = satir.KdvIstisnaKodu ?? "";
            ws.Cell(row, 11).Value = satir.KdvIstisnaAciklamasi ?? "";
            ws.Cell(row, 12).Value = satir.KdvOrani;
            ws.Cell(row, 13).Value = satir.KdvTutari;
            ws.Cell(row, 13).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 14).Value = satir.KdvliTutar;
            ws.Cell(row, 14).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 15).Value = satir.MusFisNo ?? "";
            ws.Cell(row, 16).Value = satir.MusFisDurumu ?? "";
            ws.Cell(row, 17).Value = satir.BelgeNo ?? "";
            ws.Cell(row, 18).Value = satir.Aciklama ?? "";
            row++;
        }

        // Otomatik sütun genişliği ve filtre
        ws.Columns().AdjustToContents();
        ws.RangeUsed()?.SetAutoFilter();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
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
