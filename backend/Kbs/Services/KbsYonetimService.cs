using System.Security.Cryptography;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using STYS.Infrastructure.EntityFramework;
using STYS.Kbs.Connectors;
using STYS.Kbs.Constants;
using STYS.Kbs.Dtos;
using STYS.Kbs.Entities;
using STYS.Kbs.Options;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Kbs.Services;

public class KbsYonetimService(StysAppDbContext db, IKbsConnectorResolver resolver, IOptions<KbsOptions> options) : IKbsYonetimService
{
    public async Task<KbsTesisAyariDto?> GetAyarAsync(int tesisId, CancellationToken ct)
    {
        await EnsureTesisAsync(tesisId, ct);
        return await db.KbsTesisAyarlari.Where(x => x.TesisId == tesisId).Select(x => new KbsTesisAyariDto(x.TesisId, x.KollukSistemi, x.EntegrasyonTipi, x.TesisKodu, x.SecretReference, x.AktifMi, x.CanliGonderimAktifMi, x.SonBaglantiKontrolTarihi, x.SonBaglantiKontrolSonucu)).FirstOrDefaultAsync(ct);
    }

    public async Task<KbsTesisAyariDto> UpdateAyarAsync(int tesisId, KbsTesisAyariGuncelleDto request, CancellationToken ct)
    {
        var tesis = await EnsureTesisAsync(tesisId, ct);
        if (request.CanliGonderimAktifMi && (!options.Value.LiveConnectorsEnabled || !string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Production", StringComparison.OrdinalIgnoreCase)))
            throw new BaseException("Canli gonderim yalnizca Production ortaminda ve Kbs:LiveConnectorsEnabled acikken etkinlestirilebilir.", 400);
        _ = resolver.Resolve(request.EntegrasyonTipi);
        var entity = await db.KbsTesisAyarlari.FirstOrDefaultAsync(x => x.TesisId == tesisId, ct);
        if (entity is null)
        {
            entity = new KbsTesisAyari { KurumId = tesis.KurumId, TesisId = tesisId };
            await db.KbsTesisAyarlari.AddAsync(entity, ct);
        }
        entity.KollukSistemi = request.KollukSistemi.Trim(); entity.EntegrasyonTipi = request.EntegrasyonTipi.Trim(); entity.TesisKodu = Clean(request.TesisKodu);
        entity.SecretReference = Clean(request.SecretReference); entity.AktifMi = request.AktifMi; entity.CanliGonderimAktifMi = request.CanliGonderimAktifMi;
        await db.SaveChangesAsync(ct);
        return (await GetAyarAsync(tesisId, ct))!;
    }

    public async Task<KbsBaglantiTestSonucu> BaglantiKontrolAsync(int tesisId, CancellationToken ct)
    {
        var setting = await db.KbsTesisAyarlari.FirstOrDefaultAsync(x => x.TesisId == tesisId, ct) ?? throw new BaseException("KBS tesis ayari bulunamadi.", 404);
        var result = await resolver.Resolve(setting.EntegrasyonTipi).BaglantiKontrolAsync(tesisId, ct);
        setting.SonBaglantiKontrolTarihi = DateTime.UtcNow; setting.SonBaglantiKontrolSonucu = result.Aciklama;
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<KbsSayfaliSonucDto<KbsBildirimListeDto>> ListeleAsync(int? tesisId, string? durum, string? bildirimTipi, int sayfa, int sayfaBoyutu, bool hassasVeriGoster, CancellationToken ct)
    {
        var page = Math.Max(1, sayfa); var size = Math.Clamp(sayfaBoyutu, 1, 100);
        var query = from b in db.KbsBildirimler join k in db.RezervasyonKonaklayanlar on b.RezervasyonKonaklayanId equals k.Id
                    where (!tesisId.HasValue || b.TesisId == tesisId) && (string.IsNullOrEmpty(durum) || b.Durum == durum) && (string.IsNullOrEmpty(bildirimTipi) || b.BildirimTipi == bildirimTipi)
                    select new { b, k };
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.b.CreatedAt).ThenByDescending(x => x.b.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new KbsBildirimListeDto(x.b.Id, x.b.TesisId, x.b.RezervasyonId, x.b.RezervasyonKonaklayanId, hassasVeriGoster ? x.k.AdSoyad : MaskName(x.k.AdSoyad), x.b.BildirimTipi, x.b.Saglayici, x.b.Durum, x.b.DenemeSayisi, x.b.SonHataMesaji, x.b.GonderimTarihi, x.b.TamamlanmaTarihi)).ToListAsync(ct);
        return new(rows, total, page, size);
    }

    public async Task<KbsGunlukOzetDto> GunlukOzetAsync(int? tesisId, CancellationToken ct)
    {
        var start = DateTime.UtcNow.Date; var end = start.AddDays(1);
        var query = db.KbsBildirimler.Where(x => x.CreatedAt >= start && x.CreatedAt < end && (!tesisId.HasValue || x.TesisId == tesisId));
        return new(
            await query.CountAsync(x => x.Durum == KbsBildirimDurumlari.Basarili || x.Durum == KbsBildirimDurumlari.Dogrulandi, ct),
            await query.CountAsync(x => x.Durum == KbsBildirimDurumlari.Hazir || x.Durum == KbsBildirimDurumlari.Gonderiliyor || x.Durum == KbsBildirimDurumlari.TekrarBekliyor || x.Durum == KbsBildirimDurumlari.DosyaUretildi || x.Durum == KbsBildirimDurumlari.YuklemeOnayiBekliyor, ct),
            await query.CountAsync(x => x.Durum == KbsBildirimDurumlari.SonucuBelirsiz, ct),
            await query.CountAsync(x => x.Durum == KbsBildirimDurumlari.MudahaleGerekli, ct));
    }

    public async Task TekrarKuyrugaAlAsync(long bildirimId, CancellationToken ct)
    {
        var item = await db.KbsBildirimler.FirstOrDefaultAsync(x => x.Id == bildirimId, ct) ?? throw new BaseException("KBS bildirimi bulunamadi.", 404);
        if (item.Durum is KbsBildirimDurumlari.Basarili or KbsBildirimDurumlari.Dogrulandi) throw new BaseException("Tamamlanmis bildirim tekrar kuyruga alinamaz.", 400);
        item.Durum = KbsBildirimDurumlari.Hazir; item.SonrakiDenemeTarihi = DateTime.UtcNow; item.SonHataKodu = null; item.SonHataMesaji = null;
        await db.SaveChangesAsync(ct);
    }

    public async Task ManuelMudahaleAsync(long bildirimId, CancellationToken ct)
    {
        var item = await db.KbsBildirimler.FirstOrDefaultAsync(x => x.Id == bildirimId, ct) ?? throw new BaseException("KBS bildirimi bulunamadi.", 404);
        item.Durum = KbsBildirimDurumlari.MudahaleGerekli; item.TamamlanmaTarihi = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<(byte[] Content, string FileName, string ManifestHash)> EgmExcelOlusturAsync(int tesisId, string bildirimTipi, CancellationToken ct)
    {
        await EnsureTesisAsync(tesisId, ct);
        var setting = await db.KbsTesisAyarlari.FirstOrDefaultAsync(x => x.TesisId == tesisId && x.EntegrasyonTipi == KbsEntegrasyonTipleri.Excel, ct) ?? throw new BaseException("Tesis icin EGM Excel ayari bulunamadi.", 400);
        var templatePath = options.Value.EgmTemplatePath;
        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath)) throw new BaseException("Resmi EGM Excel sablonu bulunamadi. Kbs:EgmTemplatePath ayarlanmalidir; kolonlar tahmin edilmez.", 400);
        var required = new[] { "Ad", "Soyad", "KimlikNo", "BelgeNo", "UyrukKodu", "OlayTarihi" };
        if (required.Any(x => !options.Value.EgmColumns.ContainsKey(x))) throw new BaseException("EGM sablon kolon eslemesi eksik. Kbs:EgmColumns resmi sablona gore tanimlanmalidir.", 400);

        var rows = await (from b in db.KbsBildirimler join k in db.RezervasyonKonaklayanlar on b.RezervasyonKonaklayanId equals k.Id
                          where b.TesisId == tesisId && b.BildirimTipi == bildirimTipi && b.Durum == KbsBildirimDurumlari.Hazir
                          orderby b.Id select new { b, k }).ToListAsync(ct);
        if (rows.Count == 0) throw new BaseException("Excel'e aktarilacak hazir KBS bildirimi bulunamadi.", 400);
        var manifest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', rows.Select(x => x.b.IdempotencyKey)))));
        using var workbook = new XLWorkbook(templatePath);
        var sheet = workbook.Worksheets.FirstOrDefault() ?? throw new BaseException("EGM Excel sablonunda calisma sayfasi bulunamadi.", 400);
        var rowNo = (sheet.LastRowUsed()?.RowNumber() ?? 1) + 1;
        foreach (var row in rows)
        {
            SetText(sheet, rowNo, "Ad", row.k.Ad); SetText(sheet, rowNo, "Soyad", row.k.Soyad); SetText(sheet, rowNo, "KimlikNo", row.k.KimlikNo);
            SetText(sheet, rowNo, "BelgeNo", row.k.BelgeNo); SetText(sheet, rowNo, "UyrukKodu", row.k.UyrukKodu);
            sheet.Cell(rowNo, options.Value.EgmColumns["OlayTarihi"]).Value = row.b.BildirimTipi == KbsBildirimTipleri.Giris ? row.k.FiiliGirisTarihi : row.k.FiiliCikisTarihi;
            row.b.Durum = KbsBildirimDurumlari.DosyaUretildi; row.b.ExcelManifestHash = manifest; rowNo++;
        }
        workbook.CalculateMode = XLCalculateMode.Manual;
        using var stream = new MemoryStream(); workbook.SaveAs(stream, validate: true, evaluateFormulae: false);
        await db.SaveChangesAsync(ct);
        return (stream.ToArray(), $"egm-{bildirimTipi.ToLowerInvariant()}-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx", manifest);

        void SetText(IXLWorksheet ws, int r, string name, string? value)
        {
            var safe = SanitizeExcelText(value); var cell = ws.Cell(r, options.Value.EgmColumns[name]); cell.Value = safe;
        }
    }

    public async Task EgmYuklemeOnaylaAsync(int tesisId, string manifestHash, CancellationToken ct)
    {
        var items = await db.KbsBildirimler.Where(x => x.TesisId == tesisId && x.ExcelManifestHash == manifestHash && x.Durum == KbsBildirimDurumlari.DosyaUretildi).ToListAsync(ct);
        if (items.Count == 0) throw new BaseException("Onaylanacak EGM manifesti bulunamadi.", 404);
        foreach (var item in items) { item.Durum = KbsBildirimDurumlari.YuklemeOnayiBekliyor; item.GonderimTarihi = DateTime.UtcNow; }
        await db.SaveChangesAsync(ct); // Kullanici onayi tek basina Basarili sayilmaz; ayrica dogrulama gerekir.
    }

    public static string SanitizeExcelText(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length > 0 && "=+-@\t\r".Contains(text[0]) ? "'" + text : text;
    }
    private async Task<STYS.Tesisler.Entities.Tesis> EnsureTesisAsync(int tesisId, CancellationToken ct) => await db.Tesisler.FirstOrDefaultAsync(x => x.Id == tesisId, ct) ?? throw new BaseException("Tesis bulunamadi veya erisim yetkiniz yok.", 404);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string MaskName(string value) { var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries); return string.Join(' ', parts.Select(x => x.Length <= 1 ? "*" : x[0] + new string('*', Math.Min(4, x.Length - 1)))); }
}
