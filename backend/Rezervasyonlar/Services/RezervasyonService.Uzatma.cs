using Microsoft.EntityFrameworkCore;
using STYS.Rezervasyonlar.Dto;
using STYS.Rezervasyonlar.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Rezervasyonlar.Services;

/// <summary>
/// Check-in yapilmis (henuz check-out yapilmamis) bir rezervasyon icin, kullanicinin istedigi yeni
/// cikis tarihine kadar uygulanabilecek KONAKLAMA UZATMA seceneklerini SALT OKUNUR olarak hesaplar.
/// Bu dosya, GetKonaklamaSenaryolariAsync'in kullandigi musaitlik/cakisma/fiyatlama altyapisini
/// (GetRoomAvailabilitiesAsync, BuildSingleSegmentVariants, AllocatePeople, CalculateScenarioPriceAsync,
/// vb.) AYNEN yeniden kullanir - musaitlik/cakisma kurallari burada farkli bicimde kopyalanmaz.
/// Hicbir entity uzerinde degisiklik yapmaz, SaveChangesAsync cagirmaz.
/// </summary>
public partial class RezervasyonService
{
    public async Task<RezervasyonUzatmaSecenekleriDto> GetUzatmaSecenekleriAsync(
        int rezervasyonId,
        RezervasyonUzatmaSecenekleriRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var reservation = await GetScopedReservationForManageAsync(rezervasyonId, cancellationToken);

        if (reservation.RezervasyonDurumu != RezervasyonDurumlari.CheckInTamamlandi)
        {
            throw new BaseException("Uzatma secenekleri yalnizca check-in tamamlanmis rezervasyonlar icin hesaplanabilir.", 400);
        }

        if (request.YeniCikisTarihi <= reservation.CikisTarihi)
        {
            throw new BaseException("Yeni cikis tarihi, mevcut cikis tarihinden sonra olmalidir.", 400);
        }

        if (!reservation.MisafirTipiId.HasValue || !reservation.KonaklamaTipiId.HasValue)
        {
            throw new BaseException("Rezervasyonun misafir tipi veya konaklama tipi bilgisi eksik; uzatma hesaplanamaz.", 400);
        }

        var extendStart = reservation.CikisTarihi;
        var extendEnd = request.YeniCikisTarihi;

        // Uzatma araligi da tesisin sezon/tarife (stop-sale, minimum gece) kurallarina tabidir -
        // CalculateScenarioPriceAsync her aday icin bunu ZATEN ayrica dogrular, ancak hicbir aday
        // uretilemeyen (MusaitlikYok) durumda da bu kural erken ve acik bicimde raporlanmalidir.
        await EnsureSeasonRuleComplianceAsync(reservation.TesisId, extendStart, extendEnd, cancellationToken);

        var allAssignments = await GetReservationSegmentAssignmentsAsync(rezervasyonId, cancellationToken);
        if (allAssignments.Count == 0)
        {
            throw new BaseException("Rezervasyon segment/oda atama kaydi bulunamadi.", 400);
        }

        var lastSegmentSirasi = allAssignments.Max(x => x.SegmentSirasi);
        var currentAssignments = allAssignments.Where(x => x.SegmentSirasi == lastSegmentSirasi).ToList();
        var currentRoomIds = currentAssignments.Select(x => x.OdaId).Distinct().ToList();

        var currentRoomIdInfo = await (
                from oda in _stysDbContext.Odalar
                join bina in _stysDbContext.Binalar on oda.BinaId equals bina.Id
                join odaTipi in _stysDbContext.OdaTipleri on oda.TesisOdaTipiId equals odaTipi.Id
                where currentRoomIds.Contains(oda.Id)
                select new { OdaId = oda.Id, BinaId = bina.Id, OdaTipiId = odaTipi.Id })
            .ToListAsync(cancellationToken);
        var currentRoomIdInfoMap = currentRoomIdInfo.ToDictionary(x => x.OdaId);
        var currentRoomTypeIds = currentRoomIdInfo.Select(x => x.OdaTipiId).ToHashSet();

        // Rezervasyonun MEVCUT (check-in yapilmis) konaklayanlarinin bilinen cinsiyetleri - paylasimli
        // oda/cinsiyet kurallarinin korunmasi icin GetKonaklamaSenaryolariAsync ile AYNI mekanizma
        // (BuildScenarioGuestGenderRequirements) kullanilir. Sayilar/cinsiyetler tam olarak
        // KisiSayisi'yla eslesmiyorsa (plan eksik/kismi girilmis olabilir) cinsiyet farkindaligi
        // ZORLANMAZ - bu, bos liste verildiginde mevcut yontemin varsayilan davranisiyla birebir aynidir.
        var activeGuestGenders = await _stysDbContext.RezervasyonKonaklayanlar
            .Where(x => x.RezervasyonId == rezervasyonId && x.KatilimDurumu != KonaklayanKatilimDurumlari.Gelmedi)
            .Select(x => x.Cinsiyet)
            .ToListAsync(cancellationToken);

        var guestGenderRequirements = activeGuestGenders.Count == reservation.KisiSayisi
                                       && activeGuestGenders.All(x => !string.IsNullOrWhiteSpace(x))
            ? BuildScenarioGuestGenderRequirements(activeGuestGenders, reservation.KisiSayisi)
            : ScenarioGuestGenderRequirements.None(reservation.KisiSayisi);

        var fullAvailability = await GetRoomAvailabilitiesAsync(
            reservation.TesisId,
            null,
            reservation.KisiSayisi,
            guestGenderRequirements,
            extendStart,
            extendEnd,
            cancellationToken,
            rezervasyonId);

        var candidates = new List<UzatmaSenaryoAday>();

        // 1) AyniOdadaDevam: mevcut son segmentteki TAM oda dagilimi, uzatma araliginin
        // TAMAMINDA (kendi rezervasyonu haric tutularak) kullanilabiliyor mu?
        var ayniOdaGecerliMi = currentAssignments.All(assignment =>
        {
            var availability = fullAvailability.FirstOrDefault(x => x.OdaId == assignment.OdaId);
            return availability is not null && availability.RemainingCapacity >= assignment.AyrilanKisiSayisi;
        });

        if (ayniOdaGecerliMi)
        {
            var ayniOdaAtamalari = currentAssignments
                .Select(x => new KonaklamaSenaryoOdaAtamaDto
                {
                    OdaId = x.OdaId,
                    OdaNo = x.OdaNo,
                    BinaId = currentRoomIdInfoMap.TryGetValue(x.OdaId, out var info) ? info.BinaId : 0,
                    BinaAdi = x.BinaAdi,
                    OdaTipiId = currentRoomIdInfoMap.TryGetValue(x.OdaId, out var info2) ? info2.OdaTipiId : 0,
                    OdaTipiAdi = x.OdaTipiAdi,
                    PaylasimliMi = x.PaylasimliMi,
                    Kapasite = x.Kapasite,
                    AyrilanKisiSayisi = x.AyrilanKisiSayisi
                })
                .ToList();

            candidates.Add(new UzatmaSenaryoAday(
                RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam,
                "Mevcut odada uzatma",
                0,
                [
                    new KonaklamaSenaryoSegmentDto
                    {
                        BaslangicTarihi = extendStart,
                        BitisTarihi = extendEnd,
                        OdaAtamalari = ayniOdaAtamalari
                    }
                ]));
        }

        // 2) CheckoutGunundeOdaDegisimi: uzatma araliginin TAMAMI icin (mevcut oda uygun olsun ya
        // da olmasin) alternatif tek-segmentli oda dagilimlari - mevcut GetKonaklamaSenaryolariAsync
        // akisiyla AYNI ureteci (BuildSingleSegmentVariants) kullanilir, farkli bicimde kopyalanmaz.
        var oldRoomIds = currentAssignments.Select(x => x.OdaId).Distinct().ToHashSet();
        var fullIntervalVariants = BuildSingleSegmentVariants(
            reservation.KisiSayisi,
            guestGenderRequirements,
            extendStart,
            extendEnd,
            fullAvailability);

        foreach (var variant in fullIntervalVariants)
        {
            var segment = variant.Segmentler[0];
            var newRoomIds = segment.OdaAtamalari.Select(x => x.OdaId).Distinct().ToHashSet();
            var isSameAsCurrent = oldRoomIds.SetEquals(newRoomIds)
                && segment.OdaAtamalari
                    .Select(x => (x.OdaId, x.AyrilanKisiSayisi))
                    .OrderBy(x => x.OdaId)
                    .SequenceEqual(currentAssignments.Select(x => (x.OdaId, x.AyrilanKisiSayisi)).OrderBy(x => x.OdaId));

            if (isSameAsCurrent)
            {
                candidates.Add(new UzatmaSenaryoAday(RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam, "Mevcut odada uzatma", 0, [segment]));
                continue;
            }

            var odaDegisimSayisi = oldRoomIds.Except(newRoomIds).Count();
            candidates.Add(new UzatmaSenaryoAday(
                RezervasyonUzatmaSenaryoTipleri.CheckoutGunundeOdaDegisimi,
                $"Cikis gunu oda degisimiyle uzatma ({variant.Aciklama})",
                odaDegisimSayisi,
                [segment]));
        }

        // 3) UzatmaSirasindaOdaDegisimi: uzatma araliginin TAMAMINDA tek bir oda dagilimi
        // bulunamiyorsa, gercek musaitlik sinirlarindan uretilen en fazla bir oda degisikligi
        // iceren iki segmentli bir plan aranir.
        if (fullIntervalVariants.Count == 0 && !ayniOdaGecerliMi)
        {
            var twoSegmentCandidate = await BuildUzatmaTwoSegmentScenarioAsync(
                reservation.TesisId,
                rezervasyonId,
                reservation.KisiSayisi,
                guestGenderRequirements,
                extendStart,
                extendEnd,
                cancellationToken);

            if (twoSegmentCandidate is not null)
            {
                candidates.Add(twoSegmentCandidate);
            }
        }

        // Ayni oda/segment plani birden fazla kez DONMEZ (madde 9) - GetKonaklamaSenaryolariAsync'in
        // CreateScenarioKey'i (segment+oda+kisi imzasi) AYNEN yeniden kullanilir.
        var distinctCandidates = candidates
            .GroupBy(x => CreateScenarioKey(new KonaklamaSenaryoDto { Segmentler = x.Segmentler }))
            .Select(group => group.First())
            .ToList();

        var existingDiscounts = DeserializeAppliedDiscounts(reservation.UygulananIndirimlerJson);
        var fiyatUyarisi = existingDiscounts.Count > 0
            ? "Rezervasyonun mevcut doneminde uygulanmis indirim(ler) bu uzatma tutarina otomatik olarak yansitilmadi; gerekiyorsa manuel degerlendirin."
            : null;

        var priced = new List<(UzatmaSenaryoAday Aday, SenaryoFiyatHesaplamaSonucuDto Fiyat)>();
        foreach (var candidate in distinctCandidates)
        {
            var fiyat = await CalculateScenarioPriceAsync(
                reservation.TesisId,
                reservation.MisafirTipiId!.Value,
                reservation.KonaklamaTipiId!.Value,
                reservation.KisiSayisi,
                reservation.TekKisilikFiyatUygulandiMi,
                extendStart,
                extendEnd,
                candidate.Segmentler.Select(x => new SenaryoFiyatHesaplaSegmentDto
                {
                    BaslangicTarihi = x.BaslangicTarihi,
                    BitisTarihi = x.BitisTarihi,
                    OdaAtamalari = x.OdaAtamalari.Select(y => new SenaryoFiyatHesaplaOdaAtamaDto
                    {
                        OdaId = y.OdaId,
                        AyrilanKisiSayisi = y.AyrilanKisiSayisi
                    }).ToList()
                }).ToList(),
                [],
                cancellationToken);

            priced.Add((candidate, fiyat));
        }

        var sorted = priced
            .OrderBy(x => UzatmaSenaryoTipiSirasi(x.Aday.SenaryoTipi))
            .ThenBy(x => x.Aday.OdaDegisimSayisi)
            .ThenBy(x => x.Aday.Segmentler.SelectMany(s => s.OdaAtamalari).Select(a => a.OdaId).Distinct().Count())
            .ThenBy(x => MatchesCurrentRoomType(x.Aday, currentRoomTypeIds) ? 0 : 1)
            .ThenBy(x => x.Fiyat.ToplamNihaiUcret)
            .ThenBy(x => x.Aday.Segmentler.SelectMany(s => s.OdaAtamalari).Min(a => a.OdaId))
            .Take(5)
            .ToList();

        var secenekler = new List<RezervasyonUzatmaSecenegiDto>();
        for (var i = 0; i < sorted.Count; i++)
        {
            var (aday, fiyat) = sorted[i];
            secenekler.Add(new RezervasyonUzatmaSecenegiDto
            {
                SenaryoKodu = $"UZATMA-{i + 1}",
                SenaryoTipi = aday.SenaryoTipi,
                Aciklama = aday.Aciklama,
                OdaDegisimSayisi = aday.OdaDegisimSayisi,
                EkBazUcret = fiyat.ToplamBazUcret,
                EkNihaiUcret = fiyat.ToplamNihaiUcret,
                ParaBirimi = fiyat.ParaBirimi,
                FiyatlamaTipi = fiyat.FiyatlamaTipi,
                FiyatUyarisi = fiyatUyarisi,
                Segmentler = aday.Segmentler
            });
        }

        var sonucKodu = secenekler.Count > 0
            ? RezervasyonUzatmaSonucKodlari.SecenekBulundu
            : RezervasyonUzatmaSonucKodlari.MusaitlikYok;

        var mesaj = secenekler.Count > 0
            ? $"{secenekler.Count} adet uzatma secenegi bulundu."
            : await BuildUzatmaMusaitlikYokMesajiAsync(
                reservation.TesisId,
                reservation.KisiSayisi,
                extendStart,
                extendEnd,
                guestGenderRequirements,
                rezervasyonId,
                cancellationToken);

        return new RezervasyonUzatmaSecenekleriDto
        {
            RezervasyonId = reservation.Id,
            ReferansNo = reservation.ReferansNo,
            MevcutCikisTarihi = reservation.CikisTarihi,
            YeniCikisTarihi = request.YeniCikisTarihi,
            SonucKodu = sonucKodu,
            Mesaj = mesaj,
            Secenekler = secenekler
        };
    }

    private async Task<UzatmaSenaryoAday?> BuildUzatmaTwoSegmentScenarioAsync(
        int tesisId,
        int rezervasyonId,
        int kisiSayisi,
        ScenarioGuestGenderRequirements guestGenderRequirements,
        DateTime extendStart,
        DateTime extendEnd,
        CancellationToken cancellationToken)
    {
        // Bolme tarihi araligin orta noktasi OLAMAZ (bkz. istek) - yalnizca GERCEK musaitlik
        // sinirlarindan (cakisan rezervasyon segmentlerinin ve aktif oda kullanim bloklarinin
        // baslangic/bitis tarihlerinden) turetilen adaylar denenir.
        var segmentBoundaries = await (
                from segment in _stysDbContext.RezervasyonSegmentleri
                where segment.Rezervasyon != null
                      && segment.Rezervasyon.TesisId == tesisId
                      && segment.Rezervasyon.AktifMi
                      && segment.Rezervasyon.RezervasyonDurumu != RezervasyonDurumlari.Iptal
                      && segment.RezervasyonId != rezervasyonId
                      && segment.BaslangicTarihi < extendEnd
                      && segment.BitisTarihi > extendStart
                select new { segment.BaslangicTarihi, segment.BitisTarihi })
            .ToListAsync(cancellationToken);

        var blockBoundaries = await _stysDbContext.OdaKullanimBloklari
            .Where(x => x.AktifMi && x.TesisId == tesisId && x.BaslangicTarihi < extendEnd && x.BitisTarihi > extendStart)
            .Select(x => new { x.BaslangicTarihi, x.BitisTarihi })
            .ToListAsync(cancellationToken);

        var boundaryPoints = segmentBoundaries
            .SelectMany(x => new[] { x.BaslangicTarihi, x.BitisTarihi })
            .Concat(blockBoundaries.SelectMany(x => new[] { x.BaslangicTarihi, x.BitisTarihi }))
            .Where(x => x > extendStart && x < extendEnd)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        foreach (var boundary in boundaryPoints)
        {
            var firstAvailability = await GetRoomAvailabilitiesAsync(
                tesisId, null, kisiSayisi, guestGenderRequirements, extendStart, boundary, cancellationToken, rezervasyonId);
            var firstAllocations = AllocatePeople(
                firstAvailability.OrderByDescending(x => x.RemainingCapacity).ThenBy(x => x.OdaId).ToList(),
                kisiSayisi,
                guestGenderRequirements);
            if (firstAllocations.Count == 0)
            {
                continue;
            }

            var secondAvailability = await GetRoomAvailabilitiesAsync(
                tesisId, null, kisiSayisi, guestGenderRequirements, boundary, extendEnd, cancellationToken, rezervasyonId);
            var secondAllocations = AllocatePeople(
                secondAvailability.OrderByDescending(x => x.RemainingCapacity).ThenBy(x => x.OdaId).ToList(),
                kisiSayisi,
                guestGenderRequirements);
            if (secondAllocations.Count == 0)
            {
                continue;
            }

            var firstRoomIds = firstAllocations.Select(x => x.OdaId).Distinct().ToHashSet();
            var secondRoomIds = secondAllocations.Select(x => x.OdaId).Distinct().ToHashSet();
            if (firstRoomIds.SetEquals(secondRoomIds))
            {
                // Iki segment arasinda GERCEK bir oda degisimi yok - bu, tek segmentli bir
                // senaryoyla ayni anlama gelir ve zaten oradan uretilmis olmalidir.
                continue;
            }

            var changedRoomCount = firstRoomIds.Except(secondRoomIds).Count();
            if (changedRoomCount > 1)
            {
                // En fazla BIR oda degisikligi icermeyen (birden fazla es zamanli oda degisimi
                // gerektiren) planlar bu asamada uretilmez.
                continue;
            }

            return new UzatmaSenaryoAday(
                RezervasyonUzatmaSenaryoTipleri.UzatmaSirasindaOdaDegisimi,
                $"Uzatma sirasinda {boundary:dd.MM.yyyy HH:mm} tarihinde oda degisimiyle uzatma",
                changedRoomCount,
                [
                    new KonaklamaSenaryoSegmentDto { BaslangicTarihi = extendStart, BitisTarihi = boundary, OdaAtamalari = firstAllocations },
                    new KonaklamaSenaryoSegmentDto { BaslangicTarihi = boundary, BitisTarihi = extendEnd, OdaAtamalari = secondAllocations }
                ]);
        }

        return null;
    }

    private async Task<string> BuildUzatmaMusaitlikYokMesajiAsync(
        int tesisId,
        int kisiSayisi,
        DateTime extendStart,
        DateTime extendEnd,
        ScenarioGuestGenderRequirements guestGenderRequirements,
        int rezervasyonId,
        CancellationToken cancellationToken)
    {
        var genderFreeAvailability = await GetRoomAvailabilitiesAsync(
            tesisId,
            null,
            kisiSayisi,
            ScenarioGuestGenderRequirements.None(kisiSayisi),
            extendStart,
            extendEnd,
            cancellationToken,
            rezervasyonId);

        var genderFreeAllocatable = AllocatePeopleWithoutGenderRules(
            genderFreeAvailability.OrderByDescending(x => x.RemainingCapacity).ThenBy(x => x.OdaId).ToList(),
            kisiSayisi).Count > 0;

        if (guestGenderRequirements.RequiresSharedGenderAwareAllocation && genderFreeAllocatable)
        {
            return "Uygun oda kapasitesi bulunuyor ancak paylasimli oda cinsiyet uyumu saglanamadigi icin uzatma secenegi uretilemedi.";
        }

        var candidateRoomIds = await (
                from oda in _stysDbContext.Odalar
                join bina in _stysDbContext.Binalar on oda.BinaId equals bina.Id
                join odaTipi in _stysDbContext.OdaTipleri on oda.TesisOdaTipiId equals odaTipi.Id
                where oda.AktifMi && bina.AktifMi && odaTipi.AktifMi && bina.TesisId == tesisId
                select oda.Id)
            .ToListAsync(cancellationToken);

        if (candidateRoomIds.Count == 0)
        {
            return "Tesiste aktif oda tanimi bulunamadigi icin uzatma secenegi uretilemedi.";
        }

        if (genderFreeAvailability.Count == 0)
        {
            var blockedRoomIds = await _stysDbContext.OdaKullanimBloklari
                .Where(x => x.AktifMi && candidateRoomIds.Contains(x.OdaId) && x.BaslangicTarihi < extendEnd && x.BitisTarihi > extendStart)
                .Select(x => x.OdaId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (blockedRoomIds.Count > 0)
            {
                return "Secilen tarih araliginda bakim/ariza kaydi nedeniyle uygun oda bulunamadi.";
            }

            return "Secilen tarih araliginda uygun oda veya kapasite bulunamadi.";
        }

        return "Secilen tarih araliginda uzatma icin genel musaitlik bulunamadi.";
    }

    private static bool MatchesCurrentRoomType(UzatmaSenaryoAday aday, HashSet<int> currentRoomTypeIds)
    {
        if (currentRoomTypeIds.Count == 0)
        {
            return false;
        }

        return aday.Segmentler[0].OdaAtamalari.Any(x => currentRoomTypeIds.Contains(x.OdaTipiId));
    }

    private static int UzatmaSenaryoTipiSirasi(string senaryoTipi) => senaryoTipi switch
    {
        RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam => 0,
        RezervasyonUzatmaSenaryoTipleri.CheckoutGunundeOdaDegisimi => 1,
        RezervasyonUzatmaSenaryoTipleri.UzatmaSirasindaOdaDegisimi => 2,
        _ => 3
    };

    private sealed record UzatmaSenaryoAday(
        string SenaryoTipi,
        string Aciklama,
        int OdaDegisimSayisi,
        List<KonaklamaSenaryoSegmentDto> Segmentler);
}
