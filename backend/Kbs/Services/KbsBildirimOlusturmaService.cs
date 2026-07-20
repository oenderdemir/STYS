using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Kbs.Constants;
using STYS.Kbs.Dtos;
using STYS.Kbs.Entities;
using STYS.Rezervasyonlar;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Kbs.Services;

public class KbsBildirimOlusturmaService(StysAppDbContext db) : IKbsBildirimOlusturmaService
{
    public async Task<KbsFiiliOlaySonucDto> FiiliGirisYapAsync(int konaklayanId, DateTime? olayTarihi = null, CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(konaklayanId, cancellationToken);
        if (context.Konaklayan.FiiliGirisTarihi.HasValue)
            return await ExistingAsync(konaklayanId, KbsBildirimTipleri.Giris, context.Konaklayan.FiiliGirisTarihi.Value, cancellationToken);

        ValidateGuest(context.Ayar, context.Konaklayan.Ad, context.Konaklayan.Soyad, context.Konaklayan.KimlikTuru, context.Konaklayan.KimlikNo, context.Konaklayan.BelgeNo, context.Konaklayan.UyrukKodu);
        var occurredAt = Normalize(olayTarihi);
        context.Konaklayan.FiiliGirisTarihi = occurredAt;
        context.Konaklayan.KatilimDurumu = KonaklayanKatilimDurumlari.Geldi;
        var notification = Create(context, KbsBildirimTipleri.Giris, occurredAt, null);
        await db.KbsBildirimler.AddAsync(notification, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new(konaklayanId, occurredAt, notification.Id, false);
    }

    public async Task<KbsFiiliOlaySonucDto> FiiliCikisYapAsync(int konaklayanId, DateTime? olayTarihi = null, CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(konaklayanId, cancellationToken);
        if (context.Konaklayan.FiiliCikisTarihi.HasValue)
            return await ExistingAsync(konaklayanId, KbsBildirimTipleri.Cikis, context.Konaklayan.FiiliCikisTarihi.Value, cancellationToken);
        if (!context.Konaklayan.FiiliGirisTarihi.HasValue) throw new BaseException("Fiili giris kaydi bulunmadan cikis yapilamaz.", 400);

        var occurredAt = Normalize(olayTarihi);
        if (occurredAt < context.Konaklayan.FiiliGirisTarihi.Value) throw new BaseException("Fiili cikis tarihi giris tarihinden once olamaz.", 400);
        context.Konaklayan.FiiliCikisTarihi = occurredAt;
        context.Konaklayan.KatilimDurumu = KonaklayanKatilimDurumlari.Ayrildi;
        var notification = Create(context, KbsBildirimTipleri.Cikis, occurredAt, null);
        await db.KbsBildirimler.AddAsync(notification, cancellationToken);
        await db.SaveChangesAsync(cancellationToken); // Mali check-out veya odeme kontrolu kasitli olarak yoktur.
        return new(konaklayanId, occurredAt, notification.Id, false);
    }

    public async Task<KbsFiiliOlaySonucDto> OdaDegisikligiBildirAsync(int konaklayanId, string odaNo, DateTime? olayTarihi = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(odaNo)) throw new BaseException("Oda numarasi zorunludur.", 400);
        var context = await GetContextAsync(konaklayanId, cancellationToken);
        if (!context.Konaklayan.FiiliGirisTarihi.HasValue || context.Konaklayan.FiiliCikisTarihi.HasValue) throw new BaseException("Oda degisikligi yalnizca tesiste bulunan konaklayan icin bildirilebilir.", 400);
        var occurredAt = Normalize(olayTarihi);
        var notification = Create(context, KbsBildirimTipleri.OdaGuncelleme, occurredAt, odaNo.Trim());
        await db.KbsBildirimler.AddAsync(notification, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new(konaklayanId, occurredAt, notification.Id, false);
    }

    public async Task<KbsFiiliOlaySonucDto> GelmeyecekOlarakIsaretleAsync(int konaklayanId, CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(konaklayanId, cancellationToken);
        if (context.Konaklayan.FiiliGirisTarihi.HasValue) throw new BaseException("Fiili girisi yapilmis konaklayan gelmeyecek olarak isaretlenemez.", 400);
        context.Konaklayan.KatilimDurumu = KonaklayanKatilimDurumlari.Gelmedi;
        var occurredAt = DateTime.UtcNow;
        var notification = Create(context, KbsBildirimTipleri.Iptal, occurredAt, null);
        notification.Durum = KbsBildirimDurumlari.Iptal;
        notification.TamamlanmaTarihi = occurredAt;
        await db.KbsBildirimler.AddAsync(notification, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new(konaklayanId, occurredAt, notification.Id, false);
    }

    private async Task<KbsFiiliOlaySonucDto> ExistingAsync(int guestId, string type, DateTime occurredAt, CancellationToken ct)
    {
        var id = await db.KbsBildirimler.Where(x => x.RezervasyonKonaklayanId == guestId && x.BildirimTipi == type).OrderBy(x => x.Id).Select(x => (long?)x.Id).FirstOrDefaultAsync(ct);
        return new(guestId, occurredAt, id, true);
    }

    private async Task<Context> GetContextAsync(int guestId, CancellationToken ct)
    {
        var guest = await db.RezervasyonKonaklayanlar.Include(x => x.Rezervasyon).ThenInclude(x => x!.Tesis).FirstOrDefaultAsync(x => x.Id == guestId, ct)
            ?? throw new BaseException("Konaklayan bulunamadi.", 404);
        var reservation = guest.Rezervasyon ?? throw new BaseException("Rezervasyon bulunamadi.", 404);
        var setting = await db.KbsTesisAyarlari.FirstOrDefaultAsync(x => x.TesisId == reservation.TesisId && x.AktifMi, ct);
        return new(guest, reservation.Tesis?.KurumId ?? throw new BaseException("Tesis kurum bilgisi bulunamadi.", 400), reservation.TesisId, reservation.Id, setting);
    }

    private static KbsBildirim Create(Context context, string type, DateTime occurredAt, string? discriminator)
    {
        var eventKey = $"{occurredAt:O}|{discriminator}";
        var identity = $"{context.KurumId}|{context.TesisId}|{context.GuestId}|{type}|{eventKey}";
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return new KbsBildirim
        {
            KurumId = context.KurumId, TesisId = context.TesisId, RezervasyonId = context.ReservationId,
            RezervasyonKonaklayanId = context.GuestId, BildirimTipi = type,
            Saglayici = context.Ayar?.EntegrasyonTipi ?? KbsEntegrasyonTipleri.Fake,
            Durum = KbsBildirimDurumlari.Hazir, IdempotencyKey = key, OlayAnahtari = eventKey,
            PayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"v1|{identity}"))), SonrakiDenemeTarihi = DateTime.UtcNow
        };
    }

    internal static void ValidateGuest(KbsTesisAyari? setting, string? ad, string? soyad, string? kimlikTuru, string? kimlikNo, string? belgeNo, string? uyrukKodu)
    {
        if (string.IsNullOrWhiteSpace(ad) || string.IsNullOrWhiteSpace(soyad)) throw new BaseException("KBS icin ad ve soyad zorunludur; mevcut AdSoyad alani otomatik bolunmez.", 400);
        if (kimlikTuru is KbsKimlikTurleri.Tckn or KbsKimlikTurleri.Ykn or KbsKimlikTurleri.MaviKart)
        {
            if (string.IsNullOrWhiteSpace(kimlikNo)) throw new BaseException("Secilen kimlik turu icin kimlik numarasi zorunludur.", 400);
        }
        else if (kimlikTuru == KbsKimlikTurleri.YabanciBelge && (string.IsNullOrWhiteSpace(belgeNo) || string.IsNullOrWhiteSpace(uyrukKodu)))
            throw new BaseException("Yabanci belge icin belge numarasi ve uyruk kodu zorunludur.", 400);
        else if (string.IsNullOrWhiteSpace(kimlikTuru)) throw new BaseException("Kimlik turu zorunludur.", 400);
        if (setting?.EntegrasyonTipi == KbsEntegrasyonTipleri.Soap && string.IsNullOrWhiteSpace(setting.SecretReference)) throw new BaseException("Jandarma connector icin secret reference zorunludur.", 400);
    }

    private static DateTime Normalize(DateTime? value) => (value ?? DateTime.UtcNow).ToUniversalTime();
    private sealed record Context(STYS.Rezervasyonlar.Entities.RezervasyonKonaklayan Konaklayan, int KurumId, int TesisId, int ReservationId, KbsTesisAyari? Ayar) { public int GuestId => Konaklayan.Id; }
}
