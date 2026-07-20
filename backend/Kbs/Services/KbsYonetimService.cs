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
using STYS.Kbs.Payload;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Kbs.Services;

public class KbsYonetimService(StysAppDbContext db, IKbsConnectorResolver resolver, IOptions<KbsOptions> options,
    IKbsPayloadProtector? payloadProtector = null, ICurrentUserAccessor? currentUser = null) : IKbsYonetimService
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
            .Select(x => new KbsBildirimListeDto(x.b.Id, x.b.TesisId, x.b.RezervasyonId, x.b.RezervasyonKonaklayanId, hassasVeriGoster ? x.k.AdSoyad : MaskName(x.k.AdSoyad), x.b.BildirimTipi, x.b.Saglayici, x.b.Durum, x.b.DenemeSayisi, x.b.SonHataMesaji, x.b.GonderimTarihi, x.b.TamamlanmaTarihi,
                x.b.Durum == KbsBildirimDurumlari.MudahaleGerekli, x.b.Durum == KbsBildirimDurumlari.SonucuBelirsiz, x.b.Durum == KbsBildirimDurumlari.YuklemeOnayiBekliyor)).ToListAsync(ct);
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
        if (!KbsDurumMakinesi.ManuelRetryYapilabilir(item.Durum)) throw new BaseException("Yalnizca manuel mudahale gerektiren bildirim tekrar kuyruga alinabilir.", 400);
        var onceki = item.Durum;
        item.Durum = KbsBildirimDurumlari.Hazir; item.SonrakiDenemeTarihi = DateTime.UtcNow; item.SonHataKodu = null; item.SonHataMesaji = null; item.TamamlanmaTarihi = null;
        AddHistory(item, onceki, item.Durum, "ManuelRetry", "Bildirim yetkili kullanici tarafindan tekrar kuyruga alindi.", null);
        await db.SaveChangesAsync(ct);
    }

    public async Task ManuelMudahaleAsync(long bildirimId, CancellationToken ct)
    {
        var item = await db.KbsBildirimler.FirstOrDefaultAsync(x => x.Id == bildirimId, ct) ?? throw new BaseException("KBS bildirimi bulunamadi.", 404);
        var onceki = item.Durum;
        item.Durum = KbsBildirimDurumlari.MudahaleGerekli; item.TamamlanmaTarihi = DateTime.UtcNow;
        AddHistory(item, onceki, item.Durum, "ManuelMudahale", "Bildirim manuel mudahale durumuna alindi.", null);
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

        var rows = await db.KbsBildirimler.Where(b => b.TesisId == tesisId && b.BildirimTipi == bildirimTipi && b.Durum == KbsBildirimDurumlari.Hazir)
            .OrderBy(b => b.Id).ToListAsync(ct);
        if (rows.Count == 0) throw new BaseException("Excel'e aktarilacak hazir KBS bildirimi bulunamadi.", 400);
        var snapshots = rows.Select(ReadAndVerifyPayload).ToList();
        var manifest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', rows.Select(x => $"{x.Id}:{x.PayloadHash}:{x.IdempotencyKey}")))));
        using var workbook = new XLWorkbook(templatePath);
        var sheet = workbook.Worksheets.FirstOrDefault() ?? throw new BaseException("EGM Excel sablonunda calisma sayfasi bulunamadi.", 400);
        var rowNo = (sheet.LastRowUsed()?.RowNumber() ?? 1) + 1;
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index]; var snapshot = snapshots[index];
            SetText(sheet, rowNo, "Ad", snapshot.Ad); SetText(sheet, rowNo, "Soyad", snapshot.Soyad); SetText(sheet, rowNo, "KimlikNo", snapshot.KimlikNo);
            SetText(sheet, rowNo, "BelgeNo", snapshot.BelgeNo); SetText(sheet, rowNo, "UyrukKodu", snapshot.UyrukKodu);
            sheet.Cell(rowNo, options.Value.EgmColumns["OlayTarihi"]).Value = snapshot.OlayTarihi;
            row.Durum = KbsBildirimDurumlari.DosyaUretildi; row.ExcelManifestHash = manifest; rowNo++;
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
        foreach (var item in items)
        {
            var previous = item.Durum; item.Durum = KbsBildirimDurumlari.YuklemeOnayiBekliyor; item.GonderimTarihi = DateTime.UtcNow;
            AddHistory(item, previous, item.Durum, "EgmYuklemeOnayi", "EGM dosyasinin portala yuklendigi kullanici tarafindan onaylandi.", manifestHash);
        }
        await db.SaveChangesAsync(ct); // Kullanici onayi tek basina Basarili sayilmaz; ayrica dogrulama gerekir.
    }

    public async Task MutabakatYapAsync(long bildirimId, KbsMutabakatRequestDto request, CancellationToken ct)
    {
        var item = await db.KbsBildirimler.FirstOrDefaultAsync(x => x.Id == bildirimId, ct) ?? throw new BaseException("KBS bildirimi bulunamadi.", 404);
        if (!KbsDurumMakinesi.MutabakatYapilabilir(item.Durum)) throw new BaseException("Yalnizca sonucu belirsiz bildirim icin mutabakat yapilabilir.", 400);
        var explanation = ValidateExplanation(request.Aciklama);
        var decision = request.Karar?.Trim();
        var next = decision switch { "Islendi" => KbsBildirimDurumlari.Dogrulandi, "Islenmedi" => KbsBildirimDurumlari.MudahaleGerekli, _ => throw new BaseException("Mutabakat karari Islendi veya Islenmedi olmalidir.", 400) };
        var previous = item.Durum; item.Durum = next; item.TamamlanmaTarihi = next == KbsBildirimDurumlari.Dogrulandi ? DateTime.UtcNow : null;
        item.SonHataKodu = next == KbsBildirimDurumlari.Dogrulandi ? null : "RECONCILED-NOT-PROCESSED";
        item.SonHataMesaji = next == KbsBildirimDurumlari.Dogrulandi ? null : "Dis sistemde islenmedigi mutabakat ile teyit edildi.";
        AddHistory(item, previous, next, "Mutabakat", explanation, ValidateReference(request.KurumReferansNo));
        await db.SaveChangesAsync(ct);
    }

    public async Task EgmDogrulaAsync(long bildirimId, KbsEgmDogrulamaRequestDto request, CancellationToken ct)
    {
        var item = await db.KbsBildirimler.FirstOrDefaultAsync(x => x.Id == bildirimId, ct) ?? throw new BaseException("KBS bildirimi bulunamadi.", 404);
        if (!KbsDurumMakinesi.EgmDogrulanabilir(item.Durum)) throw new BaseException("Yalnizca yukleme onayi bekleyen EGM bildirimi dogrulanabilir.", 400);
        var explanation = ValidateExplanation(request.Aciklama); var previous = item.Durum;
        item.Durum = request.Basarili ? KbsBildirimDurumlari.Dogrulandi : KbsBildirimDurumlari.MudahaleGerekli;
        item.TamamlanmaTarihi = DateTime.UtcNow;
        item.SonHataKodu = request.Basarili ? null : "EGM-VERIFICATION-FAILED";
        item.SonHataMesaji = request.Basarili ? null : "EGM yuklemesi basarisiz olarak dogrulandi.";
        AddHistory(item, previous, item.Durum, "EgmDogrulama", explanation, ValidateReference(request.KurumReferansNo));
        await db.SaveChangesAsync(ct);
    }

    public static string SanitizeExcelText(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length > 0 && "=+-@\t\r".Contains(text[0]) ? "'" + text : text;
    }
    private KbsPayloadSnapshot ReadAndVerifyPayload(KbsBildirim item)
    {
        if (payloadProtector is null) throw new BaseException("KBS payload koruyucusu yapilandirilmamis.", 503);
        string canonical;
        try { canonical = payloadProtector.Unprotect(item.ProtectedPayload); }
        catch { throw new BaseException($"KBS bildirimi #{item.Id} icin korunan payload acilamadi.", 409); }
        if (!string.Equals(KbsCanonicalPayload.Hash(canonical), item.PayloadHash, StringComparison.Ordinal))
            throw new BaseException($"KBS bildirimi #{item.Id} payload butunluk kontrolunden gecemedi.", 409);
        KbsPayloadSnapshot snapshot;
        try { snapshot = KbsCanonicalPayload.Deserialize(canonical); }
        catch { throw new BaseException($"KBS bildirimi #{item.Id} payload formati gecersiz.", 409); }
        if (snapshot.Version != item.PayloadVersion || snapshot.KurumId != item.KurumId || snapshot.TesisId != item.TesisId
            || snapshot.RezervasyonId != item.RezervasyonId || snapshot.RezervasyonKonaklayanId != item.RezervasyonKonaklayanId || snapshot.BildirimTipi != item.BildirimTipi)
            throw new BaseException($"KBS bildirimi #{item.Id} payload kimligi ile eslesmiyor.", 409);
        return snapshot;
    }
    private void AddHistory(KbsBildirim item, string previous, string next, string operation, string explanation, string? reference)
        => db.KbsDurumGecmisleri.Add(new KbsDurumGecmisi { KurumId = item.KurumId, KbsBildirimId = item.Id, OncekiDurum = previous, YeniDurum = next,
            IslemTipi = operation, Aciklama = explanation, KurumReferansNo = reference, IslemYapanUserId = currentUser?.GetCurrentUserId(),
            IslemYapanUserAdi = currentUser?.GetCurrentUserName(), IslemTarihi = DateTime.UtcNow });
    private static string ValidateExplanation(string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text)) throw new BaseException("Aciklama zorunludur.", 400);
        if (text.Length > 512) throw new BaseException("Aciklama en fazla 512 karakter olabilir.", 400);
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"\b\d{9,}\b")) throw new BaseException("Aciklamaya kimlik veya belge numarasi yazmayin.", 400);
        return text;
    }
    private static string? ValidateReference(string? value)
    {
        var text = Clean(value);
        if (text?.Length > 128) throw new BaseException("Kurum referansi en fazla 128 karakter olabilir.", 400);
        return text;
    }
    private async Task<STYS.Tesisler.Entities.Tesis> EnsureTesisAsync(int tesisId, CancellationToken ct) => await db.Tesisler.FirstOrDefaultAsync(x => x.Id == tesisId, ct) ?? throw new BaseException("Tesis bulunamadi veya erisim yetkiniz yok.", 404);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string MaskName(string value) { var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries); return string.Join(' ', parts.Select(x => x.Length <= 1 ? "*" : x[0] + new string('*', Math.Min(4, x.Length - 1)))); }
}
