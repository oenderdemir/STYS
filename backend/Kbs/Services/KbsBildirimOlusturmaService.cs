using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using STYS.Infrastructure.EntityFramework;
using STYS.Kbs.Constants;
using STYS.Kbs.Dtos;
using STYS.Kbs.Entities;
using STYS.Kbs.Payload;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Kbs.Services;

public class KbsBildirimOlusturmaService(StysAppDbContext db, IKbsPayloadProtector payloadProtector) : IKbsBildirimOlusturmaService
{
    public Task<KbsFiiliOlaySonucDto> FiiliGirisYapAsync(int konaklayanId, DateTime? olayTarihi = null, CancellationToken cancellationToken = default)
        => SavePhysicalEventAsync(konaklayanId, KbsBildirimTipleri.Giris, olayTarihi, cancellationToken);

    public Task<KbsFiiliOlaySonucDto> FiiliCikisYapAsync(int konaklayanId, DateTime? olayTarihi = null, CancellationToken cancellationToken = default)
        => SavePhysicalEventAsync(konaklayanId, KbsBildirimTipleri.Cikis, olayTarihi, cancellationToken);

    public async Task<KbsFiiliOlaySonucDto> GelmeyecekOlarakIsaretleAsync(int konaklayanId, CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(konaklayanId, cancellationToken);
        if (context.Konaklayan.FiiliGirisTarihi.HasValue) throw new BaseException("Fiili girisi yapilmis konaklayan gelmeyecek olarak isaretlenemez.", 400);
        context.Konaklayan.KatilimDurumu = KonaklayanKatilimDurumlari.Gelmedi;
        var occurredAt = DateTime.UtcNow;
        var snapshot = CreateGuestSnapshot(context, KbsBildirimTipleri.Iptal, occurredAt);
        var notification = CreateNotification(context, KbsBildirimTipleri.Iptal, $"stay:{konaklayanId}:iptal", snapshot);
        notification.Durum = KbsBildirimDurumlari.Iptal;
        notification.TamamlanmaTarihi = occurredAt;
        db.KbsBildirimler.Add(notification);
        await db.SaveChangesAsync(cancellationToken);
        return new(konaklayanId, occurredAt, notification.Id, false);
    }

    public async Task OdaDegisikligiBildirimleriniHazirlaAsync(KbsOdaDegisikligiOlayi olay, CancellationToken cancellationToken = default)
    {
        if (olay.EventId == Guid.Empty || olay.AtamaId <= 0 || olay.KonaklayanIds.Count == 0) return;
        var guests = await db.RezervasyonKonaklayanlar
            .Include(x => x.Rezervasyon).ThenInclude(x => x!.Tesis)
            .Where(x => olay.KonaklayanIds.Contains(x.Id) && x.RezervasyonId == olay.RezervasyonId)
            .ToListAsync(cancellationToken);

        foreach (var guest in guests.Where(x => x.FiiliGirisTarihi.HasValue && !x.FiiliCikisTarihi.HasValue))
        {
            var reservation = guest.Rezervasyon!;
            var context = new Context(guest, reservation.Tesis!.KurumId, reservation.TesisId, reservation.Id,
                await db.KbsTesisAyarlari.FirstOrDefaultAsync(x => x.TesisId == reservation.TesisId && x.AktifMi, cancellationToken));
            if (context.Ayar is null) continue;
            var snapshot = CreateGuestSnapshot(context, KbsBildirimTipleri.OdaGuncelleme, olay.OlayTarihi) with
            {
                OdaDegisiklikAtamaId = olay.AtamaId, OdaDegisiklikEventId = olay.EventId,
                EskiOdaId = olay.EskiOdaId, EskiOdaNo = olay.EskiOdaNo,
                YeniOdaId = olay.YeniOdaId, YeniOdaNo = olay.YeniOdaNo
            };
            var eventKey = $"room:{olay.AtamaId}:{olay.EventId:N}:{guest.Id}";
            db.KbsBildirimler.Add(CreateNotification(context, KbsBildirimTipleri.OdaGuncelleme, eventKey, snapshot));
        }
    }

    private async Task<KbsFiiliOlaySonucDto> SavePhysicalEventAsync(int guestId, string type, DateTime? eventDate, CancellationToken ct)
    {
        var eventKey = $"stay:{guestId}:{type.ToLowerInvariant()}";
        var existing = await ExistingAsync(guestId, eventKey, ct);
        if (existing is not null) return existing;

        IDbContextTransaction? transaction = null;
        try
        {
            if (db.Database.IsRelational()) transaction = await db.Database.BeginTransactionAsync(ct);
            var context = await GetContextAsync(guestId, ct);
            var guest = context.Konaklayan;
            var storedDate = type == KbsBildirimTipleri.Giris ? guest.FiiliGirisTarihi : guest.FiiliCikisTarihi;
            if (storedDate.HasValue)
            {
                if (transaction is not null) await transaction.RollbackAsync(ct);
                return await ExistingAsync(guestId, eventKey, ct) ?? new(guestId, storedDate.Value, null, true);
            }

            ValidateGuest(context.Ayar, guest.Ad, guest.Soyad, guest.KimlikTuru, guest.KimlikNo, guest.BelgeNo, guest.UyrukKodu);
            var occurredAt = Normalize(eventDate);
            if (type == KbsBildirimTipleri.Cikis)
            {
                if (!guest.FiiliGirisTarihi.HasValue) throw new BaseException("Fiili giris kaydi bulunmadan cikis yapilamaz.", 400);
                if (occurredAt < guest.FiiliGirisTarihi.Value) throw new BaseException("Fiili cikis tarihi giris tarihinden once olamaz.", 400);
                guest.FiiliCikisTarihi = occurredAt;
                guest.KatilimDurumu = KonaklayanKatilimDurumlari.Ayrildi;
            }
            else
            {
                guest.FiiliGirisTarihi = occurredAt;
                guest.KatilimDurumu = KonaklayanKatilimDurumlari.Geldi;
            }

            var snapshot = CreateGuestSnapshot(context, type, occurredAt);
            var notification = CreateNotification(context, type, eventKey, snapshot);
            db.KbsBildirimler.Add(notification);
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return new(guestId, occurredAt, notification.Id, false);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null) await transaction.RollbackAsync(ct);
            return await ReadWinnerAsync(guestId, eventKey, ct);
        }
        catch (DbUpdateException ex) when (IsExpectedKbsUniqueViolation(ex))
        {
            if (transaction is not null) await transaction.RollbackAsync(ct);
            return await ReadWinnerAsync(guestId, eventKey, ct);
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    private async Task<KbsFiiliOlaySonucDto> ReadWinnerAsync(int guestId, string eventKey, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var winner = await ExistingAsync(guestId, eventKey, ct);
            if (winner is not null) return winner with { ZatenKayitli = true };
            await Task.Delay(TimeSpan.FromMilliseconds(25 * (attempt + 1)), ct);
        }
        throw new BaseException("Eszamanli KBS olayi kaydedildi ancak mevcut bildirim okunamadi.", 409);
    }

    private async Task<KbsFiiliOlaySonucDto?> ExistingAsync(int guestId, string eventKey, CancellationToken ct)
    {
        var item = await db.KbsBildirimler.AsNoTracking()
            .Where(x => x.RezervasyonKonaklayanId == guestId && x.OlayAnahtari == eventKey)
            .Select(x => new { x.Id, x.ProtectedPayload, x.CreatedAt })
            .FirstOrDefaultAsync(ct);
        if (item is null) return null;

        DateTime occurredAt;
        try
        {
            var canonical = payloadProtector.Unprotect(item.ProtectedPayload);
            occurredAt = KbsCanonicalPayload.Deserialize(canonical).OlayTarihi;
        }
        catch
        {
            occurredAt = item.CreatedAt ?? DateTime.UtcNow;
        }
        return new(guestId, occurredAt, item.Id, true);
    }

    private async Task<Context> GetContextAsync(int guestId, CancellationToken ct)
    {
        var guest = await db.RezervasyonKonaklayanlar.Include(x => x.Rezervasyon).ThenInclude(x => x!.Tesis).FirstOrDefaultAsync(x => x.Id == guestId, ct)
            ?? throw new BaseException("Konaklayan bulunamadi.", 404);
        var reservation = guest.Rezervasyon ?? throw new BaseException("Rezervasyon bulunamadi.", 404);
        var setting = await db.KbsTesisAyarlari.FirstOrDefaultAsync(x => x.TesisId == reservation.TesisId && x.AktifMi, ct);
        return new(guest, reservation.Tesis?.KurumId ?? throw new BaseException("Tesis kurum bilgisi bulunamadi.", 400), reservation.TesisId, reservation.Id, setting);
    }

    private KbsBildirim CreateNotification(Context context, string type, string eventKey, KbsPayloadSnapshot snapshot)
    {
        var canonical = KbsCanonicalPayload.Serialize(snapshot);
        string protectedPayload;
        try { protectedPayload = payloadProtector.Protect(canonical); }
        catch (Exception) { throw new BaseException("KBS payload korumasi hazir degil.", 503); }
        var identity = $"{context.KurumId}|{context.TesisId}|{context.GuestId}|{type}|{eventKey}";
        return new KbsBildirim
        {
            KurumId = context.KurumId, TesisId = context.TesisId, RezervasyonId = context.ReservationId,
            RezervasyonKonaklayanId = context.GuestId, BildirimTipi = type,
            Saglayici = context.Ayar?.EntegrasyonTipi ?? KbsEntegrasyonTipleri.Fake,
            Durum = KbsBildirimDurumlari.Hazir,
            IdempotencyKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))),
            OlayAnahtari = eventKey, PayloadVersion = snapshot.Version,
            PayloadHash = KbsCanonicalPayload.Hash(canonical), ProtectedPayload = protectedPayload,
            SonrakiDenemeTarihi = DateTime.UtcNow
        };
    }

    private static KbsPayloadSnapshot CreateGuestSnapshot(Context context, string type, DateTime occurredAt) => new()
    {
        BildirimTipi = type, KurumId = context.KurumId, TesisId = context.TesisId,
        RezervasyonId = context.ReservationId, RezervasyonKonaklayanId = context.GuestId, OlayTarihi = occurredAt,
        Ad = Clean(context.Konaklayan.Ad), Soyad = Clean(context.Konaklayan.Soyad), KimlikTuru = Clean(context.Konaklayan.KimlikTuru),
        KimlikNo = Clean(context.Konaklayan.KimlikNo), BelgeNo = Clean(context.Konaklayan.BelgeNo), BelgeTuru = Clean(context.Konaklayan.BelgeTuru),
        UyrukKodu = Clean(context.Konaklayan.UyrukKodu), DogumTarihi = context.Konaklayan.DogumTarihi, DogumYeri = Clean(context.Konaklayan.DogumYeri),
        Cinsiyet = Clean(context.Konaklayan.Cinsiyet), Telefon = Clean(context.Konaklayan.Telefon), AracPlakasi = Clean(context.Konaklayan.AracPlakasi),
        KonaklamaKullanimSekli = Clean(context.Konaklayan.KonaklamaKullanimSekli)
    };

    internal static bool IsExpectedKbsUniqueViolation(DbUpdateException exception)
    {
        var text = exception.InnerException?.Message ?? exception.Message;
        var expectedIndex = text.Contains("IX_KbsBildirimler_IdempotencyKey", StringComparison.OrdinalIgnoreCase)
            || text.Contains("IX_KbsBildirimler_KurumId_RezervasyonKonaklayanId_BildirimTipi_OlayAnahtari", StringComparison.OrdinalIgnoreCase);
        var sqlUniqueCode = text.Contains("2601", StringComparison.Ordinal) || text.Contains("2627", StringComparison.Ordinal)
            || text.Contains("duplicate", StringComparison.OrdinalIgnoreCase) || text.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase);
        return expectedIndex && sqlUniqueCode;
    }

    internal static void ValidateGuest(KbsTesisAyari? setting, string? ad, string? soyad, string? kimlikTuru, string? kimlikNo, string? belgeNo, string? uyrukKodu)
    {
        if (string.IsNullOrWhiteSpace(ad) || string.IsNullOrWhiteSpace(soyad)) throw new BaseException("KBS icin ad ve soyad zorunludur; mevcut AdSoyad alani otomatik bolunmez.", 400);
        if (kimlikTuru is KbsKimlikTurleri.Tckn or KbsKimlikTurleri.Ykn or KbsKimlikTurleri.MaviKart)
        { if (string.IsNullOrWhiteSpace(kimlikNo)) throw new BaseException("Secilen kimlik turu icin kimlik numarasi zorunludur.", 400); }
        else if (kimlikTuru == KbsKimlikTurleri.YabanciBelge && (string.IsNullOrWhiteSpace(belgeNo) || string.IsNullOrWhiteSpace(uyrukKodu)))
            throw new BaseException("Yabanci belge icin belge numarasi ve uyruk kodu zorunludur.", 400);
        else if (string.IsNullOrWhiteSpace(kimlikTuru)) throw new BaseException("Kimlik turu zorunludur.", 400);
        if (setting?.EntegrasyonTipi == KbsEntegrasyonTipleri.Soap && string.IsNullOrWhiteSpace(setting.SecretReference)) throw new BaseException("Jandarma connector icin secret reference zorunludur.", 400);
    }

    private static DateTime Normalize(DateTime? value) => (value ?? DateTime.UtcNow).ToUniversalTime();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private sealed record Context(RezervasyonKonaklayan Konaklayan, int KurumId, int TesisId, int ReservationId, KbsTesisAyari? Ayar) { public int GuestId => Konaklayan.Id; }
}
