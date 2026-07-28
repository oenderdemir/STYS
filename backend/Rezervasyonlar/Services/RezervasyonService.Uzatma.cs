using System.Security.Cryptography;
using System.Text;
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

        // Mevcut son segmentteki oda atamalari - AyniOdadaDevam VE degisim sayisi karsilastirmalari
        // icin TEK, ORTAK bir DTO listesine cevrilir (bkz. CalculateRoomChangeCount).
        var currentAtamaDtos = currentAssignments
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

        // Mevcut dagilim BUTUNLUGU: son segmentteki toplam AyrilanKisiSayisi, rezervasyonun
        // KisiSayisi'na esit degilse (eksik/parcali bir dagilim varsa) bu dagilim GECERLI bir
        // "ayni odada devam" adayi SAYILMAZ - ne fiyatlandirilir ne de secenek olarak sunulur.
        var currentAssignmentComplete = currentAtamaDtos.Sum(x => x.AyrilanKisiSayisi) == reservation.KisiSayisi;

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

        // Oda-DUZEYINDEKI (yukaridaki) cinsiyet farkindaligi, Gelmedi kaydi veya eksik cinsiyet
        // verisi durumunda TAMAMEN kapanabilir - ancak bu, KISI BAZLI (her aktif konaklayanin
        // GERCEK, DB'den yeniden hesaplanan sabit-cinsiyetli odalarla uyumu) dogrulamayi
        // ATLAMAMALIDIR. Bu yuzden, asagida her adayin fiyatlanip kullaniciya sunulmadan ONCE,
        // RezervasyonUzatAsync'in kaydetme sirasinda kullandigi AYNI ortak eslesme altyapisi
        // (ComputeUzatmaGuestAssignmentPlanAsync/ValidateUzatmaCandidateAssignabilityAsync) ile
        // SALT OKUNUR olarak dogrulanir - seceneklerde ASLA kisisel olarak kaydedilemeyecek bir
        // plan SUNULMAZ.
        var tumKonaklayanlar = await _stysDbContext.RezervasyonKonaklayanlar
            .Where(x => x.RezervasyonId == rezervasyonId)
            .ToListAsync(cancellationToken);

        var currentSegmentId = currentAssignments[0].SegmentId;
        var (oncekiOdaMevcut, oncekiYatakMevcut) = await GetSegmentGuestRoomBedMapAsync(currentSegmentId, cancellationToken);

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

        // 1) AyniOdadaDevam: mevcut son segmentteki TAM (ve KISI SAYISI ACISINDAN BUTUN) oda
        // dagilimi, uzatma araliginin TAMAMINDA (kendi rezervasyonu haric tutularak) kullanilabiliyor mu?
        var ayniOdaGecerliMi = currentAssignmentComplete && currentAssignments.All(assignment =>
        {
            var availability = fullAvailability.FirstOrDefault(x => x.OdaId == assignment.OdaId);
            return availability is not null && availability.RemainingCapacity >= assignment.AyrilanKisiSayisi;
        });

        if (ayniOdaGecerliMi)
        {
            candidates.Add(new UzatmaSenaryoAday(
                RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam,
                "Mevcut odada uzatma",
                0,
                [
                    new KonaklamaSenaryoSegmentDto
                    {
                        BaslangicTarihi = extendStart,
                        BitisTarihi = extendEnd,
                        OdaAtamalari = currentAtamaDtos
                    }
                ]));
        }

        // 2) CheckoutGunundeOdaDegisimi: uzatma araliginin TAMAMI icin (mevcut oda uygun olsun ya
        // da olmasin) alternatif tek-segmentli oda dagilimlari - mevcut GetKonaklamaSenaryolariAsync
        // akisiyla AYNI ureteci (BuildSingleSegmentVariants) kullanilir, farkli bicimde kopyalanmaz.
        var fullIntervalVariants = BuildSingleSegmentVariants(
            reservation.KisiSayisi,
            guestGenderRequirements,
            extendStart,
            extendEnd,
            fullAvailability);

        foreach (var variant in fullIntervalVariants)
        {
            var segment = variant.Segmentler[0];
            // CalculateRoomChangeCount SIMETRIKTIR ve toplam kisi sayisi farkini da yansitir - mevcut
            // dagilim EKSIK (currentAssignmentComplete=false) oldugunda toplamlar farkli olacagi icin
            // sonuc HER ZAMAN >0'dir; ayrica bir "en az 1'e yukselt" duzeltmesine gerek YOKTUR.
            var odaDegisimSayisi = CalculateRoomChangeCount(currentAtamaDtos, segment.OdaAtamalari);

            if (odaDegisimSayisi == 0)
            {
                candidates.Add(new UzatmaSenaryoAday(RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam, "Mevcut odada uzatma", 0, [segment]));
                continue;
            }

            candidates.Add(new UzatmaSenaryoAday(
                RezervasyonUzatmaSenaryoTipleri.CheckoutGunundeOdaDegisimi,
                $"Cikis gunu oda degisimiyle uzatma ({variant.Aciklama})",
                odaDegisimSayisi,
                [segment]));
        }

        // Tek segmentli adaylar (AyniOdadaDevam + CheckoutGunundeOdaDegisimi) ONCE tekillestirilip
        // KISI BAZLI dogrulanir. fullIntervalVariants/ayniOdaGecerliMi yalnizca ODA DUZEYINDEKI
        // (sayisal kapasite + genel cinsiyet sayisi) musaitligi yansitir - bir adayin GERCEKTEN
        // kaydedilebilir olup olmadigi (ör. paylasimli bir odada haricî cinsiyeti bilinmeyen bir
        // kisi bulunmasi) yalnizca KISI BAZLI dogrulama (ValidateUzatmaCandidateAssignabilityAsync)
        // ile bilinebilir. Bu yuzden iki segmentli aramaya gecis kararinda ARTIK YALNIZCA
        // fullIntervalVariants.Count/ayniOdaGecerliMi'ye GUVENILMEZ - "en az bir KISI BAZLI GECERLI
        // tek segmentli aday var mi" sorusu esas alinir.
        var distinctSingleSegmentCandidates = candidates
            .GroupBy(x => CreateScenarioKey(new KonaklamaSenaryoDto { Segmentler = x.Segmentler }))
            .Select(group => group.First())
            .ToList();

        var validatedSingleSegmentCandidates = new List<UzatmaSenaryoAday>();
        foreach (var candidate in distinctSingleSegmentCandidates)
        {
            var dogrulama = await ComputeUzatmaCandidateAssignabilityAsync(
                rezervasyonId,
                oncekiOdaMevcut,
                oncekiYatakMevcut,
                candidate.Segmentler,
                tumKonaklayanlar,
                requireNoMovesForFirstSegment: candidate.SenaryoTipi == RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam,
                cancellationToken);

            if (!dogrulama.GecerliMi)
            {
                continue;
            }

            // OdaDegisimSayisi, konaklayan kaydi VARSA (kisi bazli dogrulama gercekten calistiysa)
            // GERCEK hareket sayisiyla degistirilir - toplu CalculateRoomChangeCount yalnizca aday
            // SINIFLANDIRMASI (AyniOdadaDevam/CheckoutGunundeOdaDegisimi ayrimi, yukarida) icin
            // kullanilir, nihai raporlanan/siralanan deger DEGILDIR. Konaklayan kaydi yoksa (eski/
            // planli-doluluk-only rezervasyonlar) kisi bazli hareket bilinemez - toplu deger korunur.
            var gercekOdaDegisimSayisi = tumKonaklayanlar.Count > 0 ? dogrulama.ToplamGercekHareket : candidate.OdaDegisimSayisi;
            validatedSingleSegmentCandidates.Add(candidate with { OdaDegisimSayisi = gercekOdaDegisimSayisi });
        }

        var assignableCandidates = new List<UzatmaSenaryoAday>(validatedSingleSegmentCandidates);

        // 3) UzatmaSirasindaOdaDegisimi: KISI BAZLI GECERLI hicbir tek segmentli aday YOKSA aranir
        // (oda duzeyinde uretilmis olup olmadiklarindan BAGIMSIZ - bkz. yukaridaki not). Boylece oda
        // duzeyinde musait gorunen ama kisisel dogrulamada elenen bir tek segmentli aday yuzunden
        // gecerli bir iki segmentli plan KACIRILMAZ.
        if (validatedSingleSegmentCandidates.Count == 0)
        {
            var twoSegmentCandidates = await BuildUzatmaTwoSegmentScenariosAsync(
                reservation.TesisId,
                rezervasyonId,
                reservation.KisiSayisi,
                guestGenderRequirements,
                extendStart,
                extendEnd,
                cancellationToken);

            // Iki segmentli adaylar da AYNI sekilde tekillestirilip AYNI ortak yardimciyla kisi
            // bazinda dogrulanir.
            var distinctTwoSegmentCandidates = twoSegmentCandidates
                .GroupBy(x => CreateScenarioKey(new KonaklamaSenaryoDto { Segmentler = x.Segmentler }))
                .Select(group => group.First())
                .ToList();

            foreach (var candidate in distinctTwoSegmentCandidates)
            {
                // UzatmaSirasindaOdaDegisimi'nin ILK segmenti, checkout gununde DEGIL, yalnizca
                // uzatma sirasindaki (iki uzatma segmenti arasindaki) sinirda oda degisikligine izin
                // verir - bu yuzden konaklayanlar BIRINCI uzatma segmentinde MEVCUT (checkout
                // oncesindeki) odalarinda kalmalidir; checkout gununde degisiklik zaten
                // CheckoutGunundeOdaDegisimi tipi tarafindan ayrica sunulur.
                var dogrulama = await ComputeUzatmaCandidateAssignabilityAsync(
                    rezervasyonId,
                    oncekiOdaMevcut,
                    oncekiYatakMevcut,
                    candidate.Segmentler,
                    tumKonaklayanlar,
                    requireNoMovesForFirstSegment: true,
                    cancellationToken);

                if (!dogrulama.GecerliMi)
                {
                    continue;
                }

                var gercekOdaDegisimSayisi = tumKonaklayanlar.Count > 0 ? dogrulama.ToplamGercekHareket : candidate.OdaDegisimSayisi;
                assignableCandidates.Add(candidate with { OdaDegisimSayisi = gercekOdaDegisimSayisi });
            }
        }

        var existingDiscounts = DeserializeAppliedDiscounts(reservation.UygulananIndirimlerJson);
        var fiyatUyarisi = existingDiscounts.Count > 0
            ? "Rezervasyonun mevcut doneminde uygulanmis indirim(ler) bu uzatma tutarina otomatik olarak yansitilmadi; gerekiyorsa manuel degerlendirin."
            : null;

        var priced = new List<(UzatmaSenaryoAday Aday, SenaryoFiyatHesaplamaSonucuDto Fiyat)>();
        foreach (var candidate in assignableCandidates)
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
                SenaryoKodu = CreateUzatmaPlanKodu(rezervasyonId, extendStart, extendEnd, aday.Segmentler),
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

    /// <summary>
    /// Uzatma araliginin TAMAMINDA tek bir oda dagilimi bulunamadigi durumlar icin, GERCEK
    /// musaitlik sinirlarinin (cakisan rezervasyon segmentlerinin ve aktif oda kullanim
    /// bloklarinin baslangic/bitis tarihlerinin) HER BIRINDE, BuildSingleSegmentVariants ile
    /// uretilen BIRDEN FAZLA deterministik oda dagilimini birinci/ikinci aralik icin ayri ayri
    /// dener ve GECERLI (en fazla bir oda degisikligi iceren) TUM kombinasyonlari dondurur.
    /// Ilk bulunan adayda ARANMAZ - nihai secim/siralama GetUzatmaSecenekleriAsync'te yapilir.
    /// Bolme tarihi ARALIGIN ORTA NOKTASI KULLANILMAZ.
    /// </summary>
    private async Task<List<UzatmaSenaryoAday>> BuildUzatmaTwoSegmentScenariosAsync(
        int tesisId,
        int rezervasyonId,
        int kisiSayisi,
        ScenarioGuestGenderRequirements guestGenderRequirements,
        DateTime extendStart,
        DateTime extendEnd,
        CancellationToken cancellationToken)
    {
        // Sinir uretiminde KULLANILAN durum filtresi, GetCurrentOccupancyByRoomAsync/
        // GetSharedRoomGuestOccupanciesAsync'in doluluk hesabinda "aktif/engelleyici" saydigi
        // durumlarla BIREBIR AYNIDIR (Iptal VE CheckOutTamamlandi haric tutulur) - check-out
        // tamamlanmis veya iptal edilmis bir rezervasyonun tarihi, doluluga hicbir etkisi
        // olmadigi icin GERCEK bir musaitlik sinirini de OLUSTURMAMALIDIR.
        var segmentBoundaries = await (
                from segment in _stysDbContext.RezervasyonSegmentleri
                where segment.Rezervasyon != null
                      && segment.Rezervasyon.TesisId == tesisId
                      && segment.Rezervasyon.AktifMi
                      && segment.Rezervasyon.RezervasyonDurumu != RezervasyonDurumlari.Iptal
                      && segment.Rezervasyon.RezervasyonDurumu != RezervasyonDurumlari.CheckOutTamamlandi
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

        var results = new List<UzatmaSenaryoAday>();

        foreach (var boundary in boundaryPoints)
        {
            var firstAvailability = await GetRoomAvailabilitiesAsync(
                tesisId, null, kisiSayisi, guestGenderRequirements, extendStart, boundary, cancellationToken, rezervasyonId);
            var firstVariants = BuildSingleSegmentVariants(kisiSayisi, guestGenderRequirements, extendStart, boundary, firstAvailability);
            if (firstVariants.Count == 0)
            {
                continue;
            }

            var secondAvailability = await GetRoomAvailabilitiesAsync(
                tesisId, null, kisiSayisi, guestGenderRequirements, boundary, extendEnd, cancellationToken, rezervasyonId);
            var secondVariants = BuildSingleSegmentVariants(kisiSayisi, guestGenderRequirements, boundary, extendEnd, secondAvailability);
            if (secondVariants.Count == 0)
            {
                continue;
            }

            foreach (var firstVariant in firstVariants)
            {
                var firstSegment = firstVariant.Segmentler[0];

                foreach (var secondVariant in secondVariants)
                {
                    var secondSegment = secondVariant.Segmentler[0];

                    // "En fazla bir oda degisikligi" kurali, tek/iki segmentli seceneklerde AYNI
                    // (kisi sayisi farkindaligi olan) ortak hesaplama metoduyla degerlendirilir.
                    var changedRoomCount = CalculateRoomChangeCount(firstSegment.OdaAtamalari, secondSegment.OdaAtamalari);
                    if (changedRoomCount is 0 or > 1)
                    {
                        // 0: segmentler arasinda GERCEK bir degisim yok (tek segmentli bir
                        // senaryoyla ayni anlama gelir, oradan zaten uretilmis olmali).
                        // >1: birden fazla es zamanli oda degisikligi gerektiren planlar
                        // bu asamada uretilmez.
                        continue;
                    }

                    results.Add(new UzatmaSenaryoAday(
                        RezervasyonUzatmaSenaryoTipleri.UzatmaSirasindaOdaDegisimi,
                        $"Uzatma sirasinda {boundary:dd.MM.yyyy HH:mm} tarihinde oda degisimiyle uzatma ({firstVariant.Aciklama} / {secondVariant.Aciklama})",
                        changedRoomCount,
                        [
                            new KonaklamaSenaryoSegmentDto { BaslangicTarihi = extendStart, BitisTarihi = boundary, OdaAtamalari = firstSegment.OdaAtamalari },
                            new KonaklamaSenaryoSegmentDto { BaslangicTarihi = boundary, BitisTarihi = extendEnd, OdaAtamalari = secondSegment.OdaAtamalari }
                        ]));
                }
            }
        }

        return results;
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

        // Bir bakim/ariza blogunun VAR OLMASI TEK BASINA yeterli degildir - blok(lar) KALDIRILMIS
        // GIBI, GetRoomAvailabilitiesAsync'in KENDI (kapasite + doluluk + PAYLASIMLI ODA CINSIYET)
        // kurallariyla, GERCEK guestGenderRequirements kullanilarak tam bir plan kurulabiliyor MU
        // diye dogrulanir. RoomAvailability nesneleri elle, eksik bilgiyle (ör. cinsiyet bilgisi
        // olmadan) KURULMAZ - ayni ortak altyapi, yalnizca "bakim bloklarini yok say" bayragiyla
        // tekrar cagrilir; bu bayrak SADECE bu neden analizinde kullanilir, normal aramayi etkilemez.
        var availabilityIgnoringBlocks = await GetRoomAvailabilitiesAsync(
            tesisId,
            null,
            kisiSayisi,
            guestGenderRequirements,
            extendStart,
            extendEnd,
            cancellationToken,
            rezervasyonId,
            ignoreMaintenanceBlocks: true);

        var allocationsIgnoringBlocks = AllocatePeople(
            availabilityIgnoringBlocks.OrderByDescending(x => x.RemainingCapacity).ThenBy(x => x.OdaId).ToList(),
            kisiSayisi,
            guestGenderRequirements);

        if (allocationsIgnoringBlocks.Count > 0)
        {
            // Bloklar GERCEKTEN kaldirilsaydi kapasite, doluluk VE cinsiyet kurallarinin TUMUYLE
            // uyumlu tam bir plan kurulabilecegi dogrulandi - bakim/ariza GERCEK nedendir.
            return "Secilen tarih araliginda bakim/ariza kaydi nedeniyle uygun oda bulunamadi.";
        }

        return "Secilen tarih araliginda uygun oda veya kapasite bulunamadi.";
    }

    /// <summary>
    /// Iki oda atama dagilimi ARASINDA ODA DEGISTIREN KISI SAYISINI hesaplar. YON BAGIMSIZDIR
    /// (CalculateRoomChangeCount(A,B) == CalculateRoomChangeCount(B,A)) ve oda basina AYRILAN KISI
    /// SAYISINI da dikkate alir - ayni oda ID'sinde bile kisi sayisi degisiyorsa (ör.
    /// {101:2} -> {101:1,102:1}) bu GERCEK bir degisikliktir.
    ///
    /// Yontem: "ayni odada kalabilecek" kisi sayisi, HER oda ID'si icin onceki/sonraki kisi
    /// sayisinin MINIMUM'unun toplamidir (bir kisi ayni odada TUTULABILIR ancak hem onceki hem
    /// sonraki dagilimda o odada yer varsa). Tasinan kisi sayisi ise:
    ///   max(toplam onceki kisi, toplam sonraki kisi) - ayni odada kalabilen kisi sayisi.
    /// Bu formul dogal olarak simetriktir (max ve min islemleri simetriktir), toplam kisi sayisi
    /// farkli olan (ör. eksik/tutarsiz) dagilimlarda da GUVENLI ve ACIKLANABILIR bir sonuc uretir
    /// (sonuc her zaman >= |toplam farki|), ve basit bir A->B tam takasinda (ayni kisi sayisiyla)
    /// dogru sekilde "o kisi sayisi kadar" degisiklik dondurur - iki farkli odanin ID'si TEK
    /// BASINA fazladan bir degisiklik olarak SAYILMAZ. Tek ve iki segmentli senaryolarda AYNI,
    /// ORTAK bu metot kullanilir - kural farkli bicimde kopyalanip tutarsizlastirilmaz.
    /// </summary>
    private static int CalculateRoomChangeCount(
        IReadOnlyCollection<KonaklamaSenaryoOdaAtamaDto> oncekiAtamalar,
        IReadOnlyCollection<KonaklamaSenaryoOdaAtamaDto> sonrakiAtamalar)
    {
        var oncekiByRoom = oncekiAtamalar
            .GroupBy(x => x.OdaId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.AyrilanKisiSayisi));
        var sonrakiByRoom = sonrakiAtamalar
            .GroupBy(x => x.OdaId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.AyrilanKisiSayisi));

        var oncekiToplam = oncekiByRoom.Values.Sum();
        var sonrakiToplam = sonrakiByRoom.Values.Sum();

        var ayniOdadaKalabilenKisiSayisi = oncekiByRoom.Keys
            .Union(sonrakiByRoom.Keys)
            .Sum(odaId => Math.Min(oncekiByRoom.GetValueOrDefault(odaId), sonrakiByRoom.GetValueOrDefault(odaId)));

        return Math.Max(oncekiToplam, sonrakiToplam) - ayniOdadaKalabilenKisiSayisi;
    }

    /// <summary>
    /// Uzatma secenegi icin KARARLI (siralamadaki sira numarasina BAGLI OLMAYAN) bir plan kimligi
    /// uretir. Kimlik; rezervasyon ID, mevcut/yeni cikis tarihi ve segment tarihleri+(OdaId,
    /// AyrilanKisiSayisi) dagilimindan turetilir - AYNI plan HER ZAMAN ayni kodu, FARKLI bir plan
    /// (musaitlik degisip baska bir dagilim olustugunda) FARKLI bir kod uretir. Kodun tahmin
    /// edilebilir olmasi GUVENLIK SORUNU degildir - kaydetme sirasinda (RezervasyonUzatAsync) plan
    /// MUTLAKA sunucuda yeniden hesaplanip bu kodla eslestirilir; istemciden gelen kod DOGRUDAN
    /// GUVENILMEZ.
    /// </summary>
    private static string CreateUzatmaPlanKodu(
        int rezervasyonId,
        DateTime mevcutCikisTarihi,
        DateTime yeniCikisTarihi,
        IReadOnlyList<KonaklamaSenaryoSegmentDto> segmentler)
    {
        var segmentImzasi = string.Join("|", segmentler.Select(s =>
        {
            var odaImzasi = string.Join(",", s.OdaAtamalari
                .OrderBy(a => a.OdaId)
                .ThenBy(a => a.AyrilanKisiSayisi)
                .Select(a => $"{a.OdaId}:{a.AyrilanKisiSayisi}"));
            return $"{s.BaslangicTarihi:O}~{s.BitisTarihi:O}~[{odaImzasi}]";
        }));

        var kaynakMetin = $"{rezervasyonId}|{mevcutCikisTarihi:O}|{yeniCikisTarihi:O}|{segmentImzasi}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(kaynakMetin));
        return "UZT-" + Convert.ToHexString(hash)[..12];
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
