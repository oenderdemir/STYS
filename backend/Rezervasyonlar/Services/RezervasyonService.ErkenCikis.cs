using System.Data;
using Microsoft.EntityFrameworkCore;
using STYS.Fiyatlandirma.Dto;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Rezervasyonlar.Dto;
using STYS.Rezervasyonlar.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Rezervasyonlar.Services;

public partial class RezervasyonService
{
    public async Task<RezervasyonErkenCikisOzetDto> GetErkenCikisOzetiAsync(
        int rezervasyonId,
        RezervasyonErkenCikisRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var reservation = await GetScopedReservationForManageAsync(rezervasyonId, cancellationToken);
        await ValidateErkenCikisRequestAsync(reservation, request, cancellationToken);

        var segmentDtos = await BuildErkenCikisSegmentDtosAsync(rezervasyonId, request.YeniCikisTarihi, cancellationToken);
        var fiyat = await CalculateReservationPricingWithExistingDiscountsAsync(
            reservation,
            segmentDtos,
            request.YeniCikisTarihi,
            cancellationToken);

        var ekHizmetToplami = await GetRezervasyonEkHizmetToplamiAsync(rezervasyonId, cancellationToken);
        var restoranToplami = await GetRezervasyonRestoranToplamiAsync(rezervasyonId, cancellationToken);
        var tahsilatToplami = await GetRezervasyonTahsilatToplamiAsync(rezervasyonId, cancellationToken);

        var yeniToplamTutar = fiyat.ToplamUcret + ekHizmetToplami + restoranToplami;
        var kalanBakiye = Math.Max(0m, yeniToplamTutar - tahsilatToplami);
        var fazlaTahsilat = Math.Max(0m, tahsilatToplami - yeniToplamTutar);

        return new RezervasyonErkenCikisOzetDto
        {
            RezervasyonId = reservation.Id,
            ReferansNo = reservation.ReferansNo,
            EskiCikisTarihi = reservation.CikisTarihi,
            YeniCikisTarihi = request.YeniCikisTarihi,
            EskiGeceSayisi = await GetReservationNightCountAsync(reservation.TesisId, reservation.GirisTarihi, reservation.CikisTarihi, cancellationToken),
            YeniGeceSayisi = await GetReservationNightCountAsync(reservation.TesisId, reservation.GirisTarihi, request.YeniCikisTarihi, cancellationToken),
            EskiKonaklamaTutari = reservation.ToplamUcret,
            YeniKonaklamaTutari = fiyat.ToplamUcret,
            FiyatFarki = fiyat.ToplamUcret - reservation.ToplamUcret,
            EkHizmetToplami = ekHizmetToplami,
            RestoranToplami = restoranToplami,
            YeniToplamTutar = yeniToplamTutar,
            TahsilatToplami = tahsilatToplami,
            KalanBakiye = kalanBakiye,
            FazlaTahsilat = fazlaTahsilat,
            ParaBirimi = reservation.ParaBirimi,
            Mesaj = fazlaTahsilat > 0m
                ? $"Fazla tahsilat: {fazlaTahsilat:N2} {reservation.ParaBirimi}"
                : kalanBakiye > 0m
                    ? $"Kalan bakiye: {kalanBakiye:N2} {reservation.ParaBirimi}"
                    : "Tahsilat ve yeni toplam tutar esit."
        };
    }

    public async Task<RezervasyonErkenCikisOzetDto> KaydetErkenCikisAsync(
        int rezervasyonId,
        RezervasyonErkenCikisRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var reservation = await GetScopedReservationForManageAsync(rezervasyonId, cancellationToken);
        await ValidateErkenCikisRequestAsync(reservation, request, cancellationToken);

        var onizleme = await GetErkenCikisOzetiAsync(rezervasyonId, request, cancellationToken);
        var kilitlenecekOdaIds = await _stysDbContext.RezervasyonSegmentOdaAtamalari
            .Where(x => x.RezervasyonSegment != null && x.RezervasyonSegment.RezervasyonId == rezervasyonId)
            .Select(x => x.OdaId)
            .Distinct()
            .ToListAsync(cancellationToken);

        await using var transaction = await _stysDbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        await AcquireReservationApplicationLockAsync(rezervasyonId, cancellationToken);
        await AcquireRoomApplicationLocksAsync(kilitlenecekOdaIds, cancellationToken);
        await _stysDbContext.Entry(reservation).ReloadAsync(cancellationToken);

        await ValidateErkenCikisRequestAsync(reservation, request, cancellationToken);

        var guncelOnizleme = await GetErkenCikisOzetiAsync(rezervasyonId, request, cancellationToken);
        if (!string.Equals(onizleme.ParaBirimi, guncelOnizleme.ParaBirimi, StringComparison.OrdinalIgnoreCase))
        {
            throw new BaseException("Erken cikis fiyat para birimi degisti. Lutfen onizlemeyi yenileyin.", 409);
        }

        if (onizleme.YeniKonaklamaTutari != guncelOnizleme.YeniKonaklamaTutari
            || onizleme.YeniToplamTutar != guncelOnizleme.YeniToplamTutar
            || onizleme.TahsilatToplami != guncelOnizleme.TahsilatToplami
            || onizleme.FazlaTahsilat != guncelOnizleme.FazlaTahsilat
            || onizleme.KalanBakiye != guncelOnizleme.KalanBakiye)
        {
            throw new BaseException("Erken cikis onizleme tutarlari degisti. Lutfen onizlemeyi yenileyin.", 409);
        }

        var recalculatedPricing = await CalculateReservationPricingWithExistingDiscountsAsync(
            reservation,
            await BuildErkenCikisSegmentDtosAsync(rezervasyonId, request.YeniCikisTarihi, cancellationToken),
            request.YeniCikisTarihi,
            cancellationToken);

        var eskiCikisTarihi = reservation.CikisTarihi;
        var eskiToplamBazUcret = reservation.ToplamBazUcret;
        var eskiToplamUcret = reservation.ToplamUcret;

        var segments = await _stysDbContext.RezervasyonSegmentleri
            .Include(x => x.OdaAtamalari)
            .Include(x => x.KonaklayanAtamalari)
            .Where(x => x.RezervasyonId == rezervasyonId)
            .OrderBy(x => x.SegmentSirasi)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (segments.Count == 0)
        {
            throw new BaseException("Rezervasyon segment kaydi bulunamadi.", 400);
        }

        var silinecekSegmentler = segments
            .Where(x => x.BaslangicTarihi >= request.YeniCikisTarihi)
            .ToList();

        foreach (var segment in silinecekSegmentler)
        {
            _stysDbContext.RezervasyonKonaklayanSegmentAtamalari.RemoveRange(segment.KonaklayanAtamalari);
            _stysDbContext.RezervasyonSegmentOdaAtamalari.RemoveRange(segment.OdaAtamalari);
        }

        _stysDbContext.RezervasyonSegmentleri.RemoveRange(silinecekSegmentler);

        var guncellenecekSonSegment = segments
            .Where(x => x.BaslangicTarihi < request.YeniCikisTarihi && x.BitisTarihi > request.YeniCikisTarihi)
            .OrderByDescending(x => x.SegmentSirasi)
            .ThenByDescending(x => x.Id)
            .FirstOrDefault();

        if (guncellenecekSonSegment is not null)
        {
            guncellenecekSonSegment.BitisTarihi = request.YeniCikisTarihi;
        }
        else
        {
            var kalanSegmentVar = segments
                .Except(silinecekSegmentler)
                .Any(x => x.BitisTarihi == request.YeniCikisTarihi);

            if (!kalanSegmentVar)
            {
                throw new BaseException("Yeni cikis tarihini kapsayan veya bu tarihte biten aktif bir rezervasyon segmenti bulunamadi.", 400);
            }
        }

        await CancelRemovedKonaklamaHaklariAsync(reservation, request.YeniCikisTarihi, cancellationToken);

        reservation.CikisTarihi = request.YeniCikisTarihi;
        reservation.ToplamBazUcret = recalculatedPricing.ToplamBazUcret;
        reservation.ToplamUcret = guncelOnizleme.YeniKonaklamaTutari;
        reservation.UygulananIndirimlerJson = SerializeAppliedDiscounts(recalculatedPricing.UygulananIndirimler);

        AppendHistoryEntry(
            reservation,
            RezervasyonGecmisIslemTipleri.ErkenCikisYapildi,
            $"Rezervasyon cikis tarihi {eskiCikisTarihi:dd.MM.yyyy} tarihinden {request.YeniCikisTarihi:dd.MM.yyyy} tarihine cekildi.",
            new ErkenCikisOncekiDegerPayload
            {
                EskiCikisTarihi = eskiCikisTarihi,
                EskiToplamBazUcret = eskiToplamBazUcret,
                EskiToplamUcret = eskiToplamUcret
            },
            new ErkenCikisYeniDegerPayload
            {
                YeniCikisTarihi = request.YeniCikisTarihi,
                YeniToplamBazUcret = reservation.ToplamBazUcret,
                YeniToplamUcret = reservation.ToplamUcret,
                FazlaTahsilat = guncelOnizleme.FazlaTahsilat,
                KalanBakiye = guncelOnizleme.KalanBakiye
            });

        await _stysDbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        guncelOnizleme.Mesaj = "Erken cikis islemi basariyla kaydedildi.";
        return guncelOnizleme;
    }

    private async Task ValidateErkenCikisRequestAsync(
        Rezervasyon reservation,
        RezervasyonErkenCikisRequestDto request,
        CancellationToken cancellationToken)
    {
        if (reservation.RezervasyonDurumu != RezervasyonDurumlari.CheckInTamamlandi)
        {
            throw new BaseException("Erken cikis yalnizca check-in tamamlanmis ve check-out yapilmamis rezervasyonlarda uygulanabilir.", 400);
        }

        if (request.YeniCikisTarihi <= reservation.GirisTarihi)
        {
            throw new BaseException("Yeni cikis tarihi giris tarihinden sonra olmalidir.", 400);
        }

        if (request.YeniCikisTarihi >= reservation.CikisTarihi)
        {
            throw new BaseException("Yeni cikis tarihi mevcut cikis tarihinden once olmalidir.", 400);
        }

        if (!reservation.MisafirTipiId.HasValue || !reservation.KonaklamaTipiId.HasValue)
        {
            throw new BaseException("Rezervasyonun fiyatlama bilgileri eksik; erken cikis hesaplanamaz.", 400);
        }

        var minimumCikisTarihi = await GetMinimumErkenCikisTarihiAsync(reservation.TesisId, cancellationToken);
        if (request.YeniCikisTarihi < minimumCikisTarihi)
        {
            throw new BaseException("Yeni cikis tarihi bugunun tesis cikis saatinden once olamaz.", 400);
        }
    }

    private async Task<DateTime> GetMinimumErkenCikisTarihiAsync(int tesisId, CancellationToken cancellationToken)
    {
        var cikisSaati = await _stysDbContext.Tesisler
            .Where(x => x.Id == tesisId)
            .Select(x => x.CikisSaati)
            .FirstOrDefaultAsync(cancellationToken);

        var bugunTr = TurkeyTimeZoneHelper.UtcdenTurkiyeYereleCevir(_timeProvider.GetUtcNow().UtcDateTime).Date;
        return bugunTr.Add(cikisSaati);
    }

    private async Task<List<SenaryoFiyatHesaplaSegmentDto>> BuildErkenCikisSegmentDtosAsync(
        int rezervasyonId,
        DateTime yeniCikisTarihi,
        CancellationToken cancellationToken)
    {
        var segments = await _stysDbContext.RezervasyonSegmentleri
            .Where(x => x.RezervasyonId == rezervasyonId && x.BaslangicTarihi < yeniCikisTarihi)
            .OrderBy(x => x.SegmentSirasi)
            .ThenBy(x => x.Id)
            .Select(x => new SenaryoFiyatHesaplaSegmentDto
            {
                BaslangicTarihi = x.BaslangicTarihi,
                BitisTarihi = x.BitisTarihi > yeniCikisTarihi ? yeniCikisTarihi : x.BitisTarihi,
                OdaAtamalari = x.OdaAtamalari
                    .OrderBy(a => a.OdaId)
                    .ThenBy(a => a.Id)
                    .Select(a => new SenaryoFiyatHesaplaOdaAtamaDto
                    {
                        OdaId = a.OdaId,
                        AyrilanKisiSayisi = a.AyrilanKisiSayisi
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var filtered = segments
            .Where(x => x.BitisTarihi > x.BaslangicTarihi)
            .ToList();

        if (filtered.Count == 0)
        {
            throw new BaseException("Yeni cikis tarihi icin gecerli segment plani bulunamadi.", 400);
        }

        return filtered;
    }

    private async Task<(decimal ToplamBazUcret, decimal ToplamUcret, List<UygulananIndirimDto> UygulananIndirimler)> CalculateReservationPricingWithExistingDiscountsAsync(
        Rezervasyon reservation,
        List<SenaryoFiyatHesaplaSegmentDto> segmentDtos,
        DateTime yeniCikisTarihi,
        CancellationToken cancellationToken)
    {
        var mevcutIndirimler = DeserializeAppliedDiscounts(reservation.UygulananIndirimlerJson);
        var seciliKuralIds = mevcutIndirimler
            .Where(x => x.IndirimKuraliId > 0)
            .Select(x => x.IndirimKuraliId)
            .Distinct()
            .ToList();
        var customIndirimToplami = mevcutIndirimler
            .Where(x => x.IndirimKuraliId <= 0 && x.IndirimTutari > 0)
            .Sum(x => x.IndirimTutari);

        var fiyatSonucu = await CalculateScenarioPriceAsync(
            reservation.TesisId,
            reservation.MisafirTipiId!.Value,
            reservation.KonaklamaTipiId!.Value,
            reservation.KisiSayisi,
            reservation.TekKisilikFiyatUygulandiMi,
            reservation.GirisTarihi,
            yeniCikisTarihi,
            segmentDtos,
            seciliKuralIds,
            cancellationToken);

        var nihaiTutar = fiyatSonucu.ToplamNihaiUcret;
        var guncelIndirimler = new List<UygulananIndirimDto>(fiyatSonucu.UygulananIndirimler);

        if (customIndirimToplami > 0)
        {
            var uygulanacakCustomIndirim = Math.Min(customIndirimToplami, nihaiTutar);
            if (uygulanacakCustomIndirim > 0)
            {
                nihaiTutar -= uygulanacakCustomIndirim;
                guncelIndirimler.Add(new UygulananIndirimDto
                {
                    IndirimKuraliId = 0,
                    KuralAdi = "Custom Indirim",
                    IndirimTutari = uygulanacakCustomIndirim,
                    SonrasiTutar = nihaiTutar
                });
            }
        }

        return (fiyatSonucu.ToplamBazUcret, nihaiTutar, guncelIndirimler);
    }

    private async Task<int> GetReservationNightCountAsync(
        int tesisId,
        DateTime girisTarihi,
        DateTime cikisTarihi,
        CancellationToken cancellationToken)
    {
        var tesisSaatleri = await _stysDbContext.Tesisler
            .Where(x => x.Id == tesisId)
            .Select(x => new { x.GirisSaati, x.CikisSaati })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BaseException("Tesis bulunamadi.", 404);

        return CalculateNightCount(girisTarihi, cikisTarihi, tesisSaatleri.GirisSaati, tesisSaatleri.CikisSaati);
    }

    private async Task<decimal> GetRezervasyonRestoranToplamiAsync(int rezervasyonId, CancellationToken cancellationToken)
        => await _stysDbContext.RezervasyonOdemeler
            .Where(x => x.RezervasyonId == rezervasyonId
                && x.Durum == RezervasyonOdemeDurumlari.Aktif
                && (x.OdemeTipi == OdemeYontemleri.OdayaEkle || x.OdemeTutari < 0))
            .Select(x => (decimal?)(x.OdemeTutari < 0 ? -x.OdemeTutari : x.OdemeTutari))
            .SumAsync(cancellationToken) ?? 0m;

    private async Task<decimal> GetRezervasyonTahsilatToplamiAsync(int rezervasyonId, CancellationToken cancellationToken)
        => await _stysDbContext.RezervasyonOdemeler
            .Where(x => x.RezervasyonId == rezervasyonId
                && x.Durum == RezervasyonOdemeDurumlari.Aktif
                && x.OdemeTipi != OdemeYontemleri.OdayaEkle
                && x.OdemeTutari > 0)
            .Select(x => (decimal?)x.OdemeTutari)
            .SumAsync(cancellationToken) ?? 0m;

    private async Task CancelRemovedKonaklamaHaklariAsync(
        Rezervasyon reservation,
        DateTime yeniCikisTarihi,
        CancellationToken cancellationToken)
    {
        if (!reservation.KonaklamaTipiId.HasValue)
        {
            return;
        }

        var yeniHaklar = await BuildKonaklamaHaklariAsync(
            reservation.TesisId,
            reservation.KonaklamaTipiId.Value,
            reservation.GirisTarihi,
            yeniCikisTarihi,
            cancellationToken);
        var yeniAnahtarlar = yeniHaklar
            .Select(x => (x.HizmetKodu, x.HakTarihi, x.Periyot, x.KullanimTipi))
            .ToHashSet();

        var mevcutHaklar = await _stysDbContext.RezervasyonKonaklamaHaklari
            .Include(x => x.TuketimKayitlari)
            .Where(x => x.RezervasyonId == reservation.Id && x.AktifMi && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var hak in mevcutHaklar)
        {
            var anahtar = (hak.HizmetKodu, hak.HakTarihi, hak.Periyot, hak.KullanimTipi);
            if (yeniAnahtarlar.Contains(anahtar))
            {
                continue;
            }

            var aktifTuketimVar = hak.TuketimKayitlari.Any(x => x.AktifMi && !x.IsDeleted);
            if (aktifTuketimVar)
            {
                throw new BaseException(
                    $"Yeni cikis sonrasina ait kullanilmis konaklama hakki bulundu ({hak.HizmetAdiSnapshot} - {hak.HakTarihi:dd.MM.yyyy}). Erken cikis kaydedilemedi.",
                    409);
            }

            hak.Durum = RezervasyonKonaklamaHakDurumlari.Iptal;
            hak.AktifMi = false;
        }
    }

    private sealed class ErkenCikisOncekiDegerPayload
    {
        public DateTime EskiCikisTarihi { get; set; }
        public decimal EskiToplamBazUcret { get; set; }
        public decimal EskiToplamUcret { get; set; }
    }

    private sealed class ErkenCikisYeniDegerPayload
    {
        public DateTime YeniCikisTarihi { get; set; }
        public decimal YeniToplamBazUcret { get; set; }
        public decimal YeniToplamUcret { get; set; }
        public decimal FazlaTahsilat { get; set; }
        public decimal KalanBakiye { get; set; }
    }
}
