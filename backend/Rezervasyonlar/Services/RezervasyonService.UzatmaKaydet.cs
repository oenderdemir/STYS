using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using STYS.Rezervasyonlar.Dto;
using STYS.Rezervasyonlar.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Rezervasyonlar.Services;

/// <summary>
/// GetUzatmaSecenekleriAsync (RezervasyonService.Uzatma.cs, SALT OKUNUR) tarafindan sunulan
/// uzatma seceneklerinden kullanicinin sectigini GUVENLI ve ATOMIK bicimde kaydeder. Musaitlik,
/// fiyat ve cinsiyet kurallari burada KOPYALANMAZ - plan, kaydetme sirasinda GetUzatmaSecenekleriAsync
/// TEKRAR cagirilarak sunucuda yeniden hesaplanir ve istemciden gelen SenaryoKodu bu yeniden
/// hesaplanan sonuca karsi dogrulanir; istemciden gelen fiyat/oda/segment bilgisi HICBIR SEKILDE
/// kullanilmaz.
/// </summary>
public partial class RezervasyonService
{
    public async Task<RezervasyonUzatmaSonucDto> RezervasyonUzatAsync(
        int rezervasyonId,
        RezervasyonUzatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var reservation = await GetScopedReservationForManageAsync(rezervasyonId, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.SenaryoKodu))
        {
            throw new BaseException("Senaryo kodu belirtilmelidir.", 400);
        }

        if (request.YeniCikisTarihi <= reservation.CikisTarihi)
        {
            // Yeni cikis tarihi mevcut cikis tarihinden once/esitse, bu ISTEMLI bir tekrar (aynen
            // uygulanmis bir uzatma isteginin ikinci kez gonderilmesi) olabilir - yeni bir
            // idempotency tablosu OLUSTURMADAN, mevcut degisiklik gecmisi kaydiyla ayirt edilir.
            await EnsureNotAlreadyAppliedUzatmaAsync(rezervasyonId, request, cancellationToken);
            throw new BaseException("Yeni cikis tarihi, mevcut cikis tarihinden sonra olmalidir.", 400);
        }

        var eskiCikisTarihi = reservation.CikisTarihi;
        var yeniCikisTarihi = request.YeniCikisTarihi;

        // Kilit ONCESI ilk hesaplama: sadece kilitlenecek oda ID'lerini belirlemek icin (asil,
        // BAGLAYICI dogrulama kilit ALINDIKTAN SONRA tekrar yapilir - asagida).
        var onSecenekler = await GetUzatmaSecenekleriAsync(
            rezervasyonId,
            new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikisTarihi },
            cancellationToken);

        var onSecilenSecenek = onSecenekler.Secenekler.FirstOrDefault(x => x.SenaryoKodu == request.SenaryoKodu)
            ?? throw new BaseException("Seçilen uzatma planı artık müsait değil. Lütfen seçenekleri yenileyin.", 409);

        var kilitlenecekOdaIds = onSecilenSecenek.Segmentler
            .SelectMany(x => x.OdaAtamalari)
            .Select(x => x.OdaId)
            .Distinct()
            .ToList();

        await using var transaction = await _stysDbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        // Ayni rezervasyonun eszamanli iki kez uzatilmasini engellemek icin rezervasyon duzeyinde
        // ayri bir application lock + kilitler DETERMINISTIK (sirali) oda ID sirasiyla alinir.
        await AcquireReservationApplicationLockAsync(rezervasyonId, cancellationToken);
        await AcquireRoomApplicationLocksAsync(kilitlenecekOdaIds, cancellationToken);

        // EF Core ayni context icinde ayni PK ile TEKRAR sorgulanan, zaten TRACKED bir entity'yi
        // DB'den YENIDEN OKUYARAK guncellemez (identity resolution) - kilit alindiktan sonra GERCEK
        // guncel veriyi gormek icin ACIKCA yeniden yuklenmesi gerekir.
        await _stysDbContext.Entry(reservation).ReloadAsync(cancellationToken);

        if (reservation.RezervasyonDurumu != RezervasyonDurumlari.CheckInTamamlandi
            || reservation.CikisTarihi != eskiCikisTarihi)
        {
            throw new BaseException("Seçilen uzatma planı artık müsait değil. Lütfen seçenekleri yenileyin.", 409);
        }

        // Kilit ALTINDA, BAGLAYICI/AUTHORITATIVE yeniden hesaplama - TOCTOU'yu onlemek icin plan
        // musaitligi burada TEKRAR dogrulanir; uygun baska bir secenek olsa bile OTOMATIK olarak
        // baska bir secenege GECILMEZ.
        var guncelSecenekler = await GetUzatmaSecenekleriAsync(
            rezervasyonId,
            new RezervasyonUzatmaSecenekleriRequestDto { YeniCikisTarihi = yeniCikisTarihi },
            cancellationToken);

        var secilenSecenek = guncelSecenekler.Secenekler.FirstOrDefault(x => x.SenaryoKodu == request.SenaryoKodu)
            ?? throw new BaseException("Seçilen uzatma planı artık müsait değil. Lütfen seçenekleri yenileyin.", 409);

        if (!string.Equals(secilenSecenek.ParaBirimi, reservation.ParaBirimi, StringComparison.OrdinalIgnoreCase))
        {
            throw new BaseException("Uzatma tutarinin para birimi, rezervasyonun para birimiyle uyumlu degil.", 400);
        }

        var lastSegment = await _stysDbContext.RezervasyonSegmentleri
            .Include(x => x.OdaAtamalari)
            .Where(x => x.RezervasyonId == rezervasyonId)
            .OrderByDescending(x => x.SegmentSirasi)
            .FirstAsync(cancellationToken);

        var maxSegmentSirasi = lastSegment.SegmentSirasi;

        var tumKonaklayanlar = await _stysDbContext.RezervasyonKonaklayanlar
            .Where(x => x.RezervasyonId == rezervasyonId)
            .ToListAsync(cancellationToken);
        var konaklayanKaydiVarMi = tumKonaklayanlar.Count > 0;

        switch (secilenSecenek.SenaryoTipi)
        {
            case RezervasyonUzatmaSenaryoTipleri.AyniOdadaDevam:
            {
                if (lastSegment.BitisTarihi != eskiCikisTarihi)
                {
                    throw new BaseException("Seçilen uzatma planı artık müsait değil. Lütfen seçenekleri yenileyin.", 409);
                }

                // Gereksiz yeni segment olusturulmaz - mevcut son segment uzatilir, mevcut oda ve
                // konaklayan segment atamalari (ayni segment ID'sine bagli kaldiklari icin)
                // OLDUGU GIBI korunur.
                lastSegment.BitisTarihi = yeniCikisTarihi;
                break;
            }

            case RezervasyonUzatmaSenaryoTipleri.CheckoutGunundeOdaDegisimi:
            {
                var segmentPlan = secilenSecenek.Segmentler[0];

                // Konaklayan/yatak/cinsiyet plani, HERHANGI BIR segment/atama olusturulmadan ONCE
                // TAMAMEN hesaplanip dogrulanir - boylece cinsiyet/kapasite/yatak acisindan gecerli
                // bir atama kurulamazsa (409) hicbir kismi kayit (segment dahil) birakilmaz.
                List<UzatmaGuestPlanEntry>? guestPlan = null;
                if (konaklayanKaydiVarMi)
                {
                    var (oncekiOda, oncekiYatak) = await GetSegmentGuestRoomBedMapAsync(lastSegment.Id, cancellationToken);
                    guestPlan = await ComputeUzatmaGuestAssignmentPlanAsync(
                        reservation.Id, oncekiOda, oncekiYatak, segmentPlan, tumKonaklayanlar, reservation.KisiSayisi, cancellationToken);
                }

                var yeniSegment = await CreateUzatmaSegmentAsync(reservation.Id, maxSegmentSirasi + 1, segmentPlan, cancellationToken);

                if (guestPlan is not null)
                {
                    MaterializeUzatmaGuestAssignments(yeniSegment, guestPlan);
                    await _stysDbContext.SaveChangesAsync(cancellationToken);
                }

                break;
            }

            case RezervasyonUzatmaSenaryoTipleri.UzatmaSirasindaOdaDegisimi:
            {
                var ilkPlan = secilenSecenek.Segmentler[0];
                var ikinciPlan = secilenSecenek.Segmentler[1];

                // Her iki segmentin konaklayan/yatak/cinsiyet plani da, HERHANGI BIR segment/atama
                // olusturulmadan ONCE tamamen hesaplanip dogrulanir (ikinci segmentin plani, ilk
                // segmentin HENUZ veritabanina yazilmamis dry-run sonucu uzerinden kurulur) - boylece
                // iki segmentten HERHANGI biri icin gecerli bir atama kurulamazsa hicbir kismi kayit
                // (ilk segment dahil) birakilmaz.
                List<UzatmaGuestPlanEntry>? ilkGuestPlan = null;
                List<UzatmaGuestPlanEntry>? ikinciGuestPlan = null;

                if (konaklayanKaydiVarMi)
                {
                    var (oncekiOda1, oncekiYatak1) = await GetSegmentGuestRoomBedMapAsync(lastSegment.Id, cancellationToken);
                    ilkGuestPlan = await ComputeUzatmaGuestAssignmentPlanAsync(
                        reservation.Id, oncekiOda1, oncekiYatak1, ilkPlan, tumKonaklayanlar, reservation.KisiSayisi, cancellationToken);

                    var oncekiOda2 = ilkGuestPlan.ToDictionary(x => x.KonaklayanId, x => x.OdaId);
                    var oncekiYatak2 = ilkGuestPlan.ToDictionary(x => x.KonaklayanId, x => x.YatakNo);
                    ikinciGuestPlan = await ComputeUzatmaGuestAssignmentPlanAsync(
                        reservation.Id, oncekiOda2, oncekiYatak2, ikinciPlan, tumKonaklayanlar, reservation.KisiSayisi, cancellationToken);
                }

                var ilkSegment = await CreateUzatmaSegmentAsync(reservation.Id, maxSegmentSirasi + 1, ilkPlan, cancellationToken);

                if (ilkGuestPlan is not null)
                {
                    MaterializeUzatmaGuestAssignments(ilkSegment, ilkGuestPlan);
                    await _stysDbContext.SaveChangesAsync(cancellationToken);
                }

                var ikinciSegment = await CreateUzatmaSegmentAsync(reservation.Id, maxSegmentSirasi + 2, ikinciPlan, cancellationToken);

                if (ikinciGuestPlan is not null)
                {
                    MaterializeUzatmaGuestAssignments(ikinciSegment, ikinciGuestPlan);
                    await _stysDbContext.SaveChangesAsync(cancellationToken);
                }

                break;
            }

            default:
                throw new BaseException("Bilinmeyen uzatma senaryo tipi.", 409);
        }

        var eskiToplamBazUcret = reservation.ToplamBazUcret;
        var eskiToplamUcret = reservation.ToplamUcret;

        // Rezervasyonun cikis tarihi, YALNIZCA segment islemleri basariyla hazirlandiktan SONRA
        // guncellenir.
        reservation.CikisTarihi = yeniCikisTarihi;
        reservation.ToplamBazUcret += secilenSecenek.EkBazUcret;
        reservation.ToplamUcret += secilenSecenek.EkNihaiUcret;

        if (reservation.KonaklamaTipiId.HasValue)
        {
            await AddExtensionKonaklamaHaklariAsync(reservation, eskiCikisTarihi, yeniCikisTarihi, cancellationToken);
        }

        AppendHistoryEntry(
            reservation,
            RezervasyonGecmisIslemTipleri.Uzatildi,
            $"Rezervasyon {eskiCikisTarihi:dd.MM.yyyy} tarihinden {yeniCikisTarihi:dd.MM.yyyy} tarihine uzatildi ({secilenSecenek.SenaryoTipi}).",
            new UzatmaOncekiDegerPayload
            {
                EskiCikisTarihi = eskiCikisTarihi,
                EskiToplamBazUcret = eskiToplamBazUcret,
                EskiToplamUcret = eskiToplamUcret,
                SonSegment = new UzatmaSegmentOzetPayload
                {
                    BaslangicTarihi = lastSegment.BaslangicTarihi,
                    BitisTarihi = eskiCikisTarihi,
                    OdaAtamalari = lastSegment.OdaAtamalari
                        .Select(x => new UzatmaOdaAtamaOzetPayload { OdaId = x.OdaId, AyrilanKisiSayisi = x.AyrilanKisiSayisi })
                        .ToList()
                }
            },
            new UzatmaYeniDegerPayload
            {
                YeniCikisTarihi = yeniCikisTarihi,
                SenaryoKodu = secilenSecenek.SenaryoKodu,
                SenaryoTipi = secilenSecenek.SenaryoTipi,
                EkBazUcret = secilenSecenek.EkBazUcret,
                EkNihaiUcret = secilenSecenek.EkNihaiUcret,
                YeniToplamBazUcret = reservation.ToplamBazUcret,
                YeniToplamUcret = reservation.ToplamUcret,
                Segmentler = secilenSecenek.Segmentler
            });

        await _stysDbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new RezervasyonUzatmaSonucDto
        {
            RezervasyonId = reservation.Id,
            ReferansNo = reservation.ReferansNo,
            SenaryoKodu = secilenSecenek.SenaryoKodu,
            SenaryoTipi = secilenSecenek.SenaryoTipi,
            EskiCikisTarihi = eskiCikisTarihi,
            YeniCikisTarihi = yeniCikisTarihi,
            EkBazUcret = secilenSecenek.EkBazUcret,
            EkNihaiUcret = secilenSecenek.EkNihaiUcret,
            YeniToplamBazUcret = reservation.ToplamBazUcret,
            YeniToplamUcret = reservation.ToplamUcret,
            ParaBirimi = reservation.ParaBirimi,
            Segmentler = secilenSecenek.Segmentler,
            Mesaj = "Rezervasyon uzatma islemi basariyla kaydedildi."
        };
    }

    private async Task EnsureNotAlreadyAppliedUzatmaAsync(
        int rezervasyonId,
        RezervasyonUzatRequestDto request,
        CancellationToken cancellationToken)
    {
        var sonUzatmaYeniDegerJson = await _stysDbContext.RezervasyonDegisiklikGecmisleri
            .Where(x => x.RezervasyonId == rezervasyonId && x.IslemTipi == RezervasyonGecmisIslemTipleri.Uzatildi)
            .OrderByDescending(x => x.Id)
            .Select(x => x.YeniDegerJson)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(sonUzatmaYeniDegerJson))
        {
            return;
        }

        UzatmaYeniDegerPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<UzatmaYeniDegerPayload>(sonUzatmaYeniDegerJson);
        }
        catch (JsonException)
        {
            return;
        }

        if (payload is not null
            && payload.YeniCikisTarihi.Date == request.YeniCikisTarihi.Date
            && string.Equals(payload.SenaryoKodu, request.SenaryoKodu, StringComparison.Ordinal))
        {
            throw new BaseException("Bu uzatma islemi zaten uygulanmis; rezervasyon zaten bu tarihe kadar uzatilmis.", 409);
        }
    }

    private async Task AcquireReservationApplicationLockAsync(int rezervasyonId, CancellationToken cancellationToken)
    {
        if (!_stysDbContext.Database.IsRelational())
        {
            return;
        }

        var resource = $"stys:rezervasyon:kayit:{rezervasyonId}";
        var lockResults = await _stysDbContext.Database
            .SqlQueryRaw<int>(
                "DECLARE @result int; EXEC @result = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = {1}; SELECT @result;",
                resource,
                15000)
            .ToListAsync(cancellationToken);
        var lockResult = lockResults.Single();

        if (lockResult < 0)
        {
            throw new BaseException("Rezervasyon icin kilit alinamadi. Lutfen islemi tekrar deneyin.", 409);
        }
    }

    private async Task<RezervasyonSegment> CreateUzatmaSegmentAsync(
        int rezervasyonId,
        int segmentSirasi,
        KonaklamaSenaryoSegmentDto plan,
        CancellationToken cancellationToken)
    {
        var segment = new RezervasyonSegment
        {
            RezervasyonId = rezervasyonId,
            SegmentSirasi = segmentSirasi,
            BaslangicTarihi = plan.BaslangicTarihi,
            BitisTarihi = plan.BitisTarihi
        };

        _stysDbContext.RezervasyonSegmentleri.Add(segment);
        // Segment.Id, bagli oda atamalarinda kullanilmadan once uretilmelidir (iki-adimli
        // SaveChanges deseni) - her iki cagri da AYNI transaction icinde kalir.
        await _stysDbContext.SaveChangesAsync(cancellationToken);

        foreach (var atama in plan.OdaAtamalari)
        {
            _stysDbContext.RezervasyonSegmentOdaAtamalari.Add(new RezervasyonSegmentOdaAtama
            {
                RezervasyonSegmentId = segment.Id,
                OdaId = atama.OdaId,
                AyrilanKisiSayisi = atama.AyrilanKisiSayisi,
                OdaNoSnapshot = atama.OdaNo,
                BinaAdiSnapshot = atama.BinaAdi,
                OdaTipiAdiSnapshot = atama.OdaTipiAdi,
                PaylasimliMiSnapshot = atama.PaylasimliMi,
                KapasiteSnapshot = atama.Kapasite
            });
        }

        await _stysDbContext.SaveChangesAsync(cancellationToken);
        return segment;
    }

    /// <summary>
    /// Bir uzatma segmentindeki bir konaklayanin PLANLANAN (henuz veritabanina yazilmamis) oda/yatak
    /// atamasini tutar. GetSegmentGuestRoomBedMapAsync (gercek, onceden var olan bir segmentten) veya
    /// bir onceki ComputeUzatmaGuestAssignmentPlanAsync cagrisinin sonucundan (henuz olusturulmamis
    /// bir "onceki" segment icin dry-run) turetilerek zincirlenebilir.
    /// </summary>
    private sealed class UzatmaGuestPlanEntry
    {
        public required int KonaklayanId { get; init; }
        public required int OdaId { get; init; }
        public int? YatakNo { get; set; }
    }

    private async Task<(Dictionary<int, int> OdaByKonaklayanId, Dictionary<int, int?> YatakByKonaklayanId)> GetSegmentGuestRoomBedMapAsync(
        int segmentId,
        CancellationToken cancellationToken)
    {
        var rows = await _stysDbContext.RezervasyonKonaklayanSegmentAtamalari
            .Where(x => x.RezervasyonSegmentId == segmentId)
            .Select(x => new { x.RezervasyonKonaklayanId, x.OdaId, x.YatakNo })
            .ToListAsync(cancellationToken);

        return (
            rows.ToDictionary(x => x.RezervasyonKonaklayanId, x => x.OdaId),
            rows.ToDictionary(x => x.RezervasyonKonaklayanId, x => x.YatakNo));
    }

    /// <summary>
    /// Bir uzatma segmenti icin, rezervasyonun AKTIF (Gelmedi HARIC) konaklayanlarinin oda/yatak
    /// atama PLANINI, HICBIR VERITABANI YAZIMI YAPMADAN hesaplar (salt-okunur DB sorgulari haric) -
    /// gecersiz bir plan (cinsiyet veya kapasite acisindan) burada 409 firlatir ve cagiran, bu segment
    /// (veya iki-segmentli senaryoda ONCEKI segment) icin HENUZ HICBIR entity OLUSTURMAMIS olur.
    /// Onceki segmentteki oda hala hedef dagilimda mevcutsa VE cinsiyet acisindan uygunsa konaklayan
    /// O ODADA TUTULUR; kalan konaklayanlar deterministik bicimde (konaklayan SiraNo, hedef oda ID
    /// sirasiyla) kalan slotlara atanir. Cinsiyet farkindaligi tam olarak GetUzatmaSecenekleriAsync'in
    /// uyguladigi kosulda (tum aktif konaklayanlarin cinsiyeti biliniyor ve sayilari KisiSayisi'na
    /// esitse) devreye girer - paylasimli her hedef odanin GERCEK (DB'den, baska rezervasyonlardan
    /// gelen) sabit cinsiyeti GetSharedRoomGuestOccupanciesAsync/GetDistinctSharedRoomGenders ile
    /// YENIDEN belirlenir; istemciden veya onceki hesaplamadan gelen bilgiye GUVENILMEZ. Paylasimli
    /// odalarda yatak numarasi, ReassignGuestSegmentAssignmentsAfterRoomChangeAsync ile AYNI "diger
    /// rezervasyonlarin isgal ettigi yataklari haric tut" desenini kullanir - yatak kontrolu cinsiyet
    /// kontrolunun YERINE GECMEZ, cinsiyet uygunlugu ayrica ve ONCE dogrulanir.
    /// </summary>
    private async Task<List<UzatmaGuestPlanEntry>> ComputeUzatmaGuestAssignmentPlanAsync(
        int rezervasyonId,
        IReadOnlyDictionary<int, int> oncekiOdaByKonaklayanId,
        IReadOnlyDictionary<int, int?> oncekiYatakByKonaklayanId,
        KonaklamaSenaryoSegmentDto plan,
        IReadOnlyCollection<RezervasyonKonaklayan> tumKonaklayanlar,
        int kisiSayisi,
        CancellationToken cancellationToken)
    {
        var aktifKonaklayanlar = tumKonaklayanlar
            .Where(x => x.KatilimDurumu != KonaklayanKatilimDurumlari.Gelmedi)
            .OrderBy(x => x.SiraNo)
            .ThenBy(x => x.Id)
            .ToList();

        if (aktifKonaklayanlar.Count == 0)
        {
            return [];
        }

        var odaInfoById = plan.OdaAtamalari.ToDictionary(x => x.OdaId);
        var kalanSlotByOda = plan.OdaAtamalari
            .OrderBy(x => x.OdaId)
            .ToDictionary(x => x.OdaId, x => x.AyrilanKisiSayisi);

        // GetUzatmaSecenekleriAsync ile BIREBIR AYNI kosul: tum aktif konaklayanlarin cinsiyeti
        // biliniyor VE sayilari rezervasyonun KisiSayisi'na esitse cinsiyet farkindaligi UYGULANIR -
        // aksi halde (secenek uretiminde de oldugu gibi) cinsiyet kisitlanmaz.
        var normalizedCinsiyetler = aktifKonaklayanlar
            .Select(x => NormalizeStoredKonaklayanCinsiyet(x.Cinsiyet))
            .ToList();
        var cinsiyetFarkindaligiGerekli = aktifKonaklayanlar.Count == kisiSayisi
            && normalizedCinsiyetler.All(x => x is not null);

        // Paylasimli her hedef odanin, bu uzatma segmentinin tarih araliginda GERCEK (DB'den, BASKA
        // rezervasyonlardan gelen) sabit cinsiyetini yeniden belirle - mevcut ortak yardimcilar
        // (GetSharedRoomGuestOccupanciesAsync/GetDistinctSharedRoomGenders) AYNEN yeniden kullanilir.
        var sabitCinsiyetByOda = new Dictionary<int, string?>();
        if (cinsiyetFarkindaligiGerekli)
        {
            var paylasimliOdaIds = plan.OdaAtamalari.Where(x => x.PaylasimliMi).Select(x => x.OdaId).Distinct().ToList();
            var digerRezervasyonDoluluklari = await GetSharedRoomGuestOccupanciesAsync(
                paylasimliOdaIds, plan.BaslangicTarihi, plan.BitisTarihi, cancellationToken, excludeRezervasyonId: rezervasyonId);

            foreach (var odaId in paylasimliOdaIds)
            {
                var cinsiyetler = GetDistinctSharedRoomGenders(digerRezervasyonDoluluklari, odaId, plan.BaslangicTarihi, plan.BitisTarihi);
                if (cinsiyetler.Count > 1)
                {
                    throw new BaseException($"'{odaInfoById[odaId].OdaNo}' odasinda cinsiyet tutarsizligi tespit edildi; konaklayan atamasi yapilamiyor.", 409);
                }

                sabitCinsiyetByOda[odaId] = cinsiyetler.SingleOrDefault();
            }
        }

        // Bos (dis rezervasyonlarca henuz sabitlenmemis) paylasimli bir odaya BU atama sirasinda
        // hangi cinsiyetin yerlestirildigini izler - ayni odada FARKLI cinsiyetler KARISTIRILMAZ.
        var atananBatchCinsiyetByOda = new Dictionary<int, string?>(sabitCinsiyetByOda);

        bool OdaUygunMu(int odaId, string? konaklayanCinsiyeti)
        {
            if (!cinsiyetFarkindaligiGerekli || !odaInfoById[odaId].PaylasimliMi)
            {
                return true;
            }

            var mevcutBatch = atananBatchCinsiyetByOda.GetValueOrDefault(odaId);
            return mevcutBatch is null
                || konaklayanCinsiyeti is null
                || string.Equals(mevcutBatch, konaklayanCinsiyeti, StringComparison.OrdinalIgnoreCase);
        }

        void OdaCinsiyetiniKaydet(int odaId, string? konaklayanCinsiyeti)
        {
            if (cinsiyetFarkindaligiGerekli && odaInfoById[odaId].PaylasimliMi && konaklayanCinsiyeti is not null)
            {
                atananBatchCinsiyetByOda[odaId] = konaklayanCinsiyeti;
            }
        }

        var atananOdaByKonaklayanId = new Dictionary<int, int>();

        // 1) Konaklayanin onceki odasi hedef dagilimda hala mevcutsa VE cinsiyet acisindan uygunsa
        // AYNI odada tutulur - sabit kadin odasina erkek, sabit erkek odasina kadin ATANMAZ.
        foreach (var konaklayan in aktifKonaklayanlar)
        {
            var kendiCinsiyeti = NormalizeStoredKonaklayanCinsiyet(konaklayan.Cinsiyet);
            if (oncekiOdaByKonaklayanId.TryGetValue(konaklayan.Id, out var oncekiOdaId)
                && kalanSlotByOda.TryGetValue(oncekiOdaId, out var kalan)
                && kalan > 0
                && OdaUygunMu(oncekiOdaId, kendiCinsiyeti))
            {
                atananOdaByKonaklayanId[konaklayan.Id] = oncekiOdaId;
                kalanSlotByOda[oncekiOdaId] = kalan - 1;
                OdaCinsiyetiniKaydet(oncekiOdaId, kendiCinsiyeti);
            }
        }

        // 2) Yalnizca GERCEKTEN tasinmasi gereken konaklayanlar, hedef odanin cinsiyet ihtiyacina
        // gore DETERMINISTIK sirayla kalan slotlara atanir - dis rezervasyonlarca sabitlenmis odalar
        // ONCELIKLI doldurulur (aksi halde bos bir odaya yanlis cinsiyet yerlesip sabit odanin
        // ihtiyaci karsilanamaz hale gelebilir).
        foreach (var konaklayan in aktifKonaklayanlar)
        {
            if (atananOdaByKonaklayanId.ContainsKey(konaklayan.Id))
            {
                continue;
            }

            var kendiCinsiyeti = NormalizeStoredKonaklayanCinsiyet(konaklayan.Cinsiyet);
            var hedefOdaId = kalanSlotByOda
                .Where(x => x.Value > 0 && OdaUygunMu(x.Key, kendiCinsiyeti))
                .OrderBy(x => sabitCinsiyetByOda.GetValueOrDefault(x.Key) is not null ? 0 : 1)
                .ThenBy(x => x.Key)
                .Select(x => (int?)x.Key)
                .FirstOrDefault();

            if (hedefOdaId is null)
            {
                throw new BaseException(
                    cinsiyetFarkindaligiGerekli
                        ? "Konaklayanlar icin cinsiyet acisindan gecerli oda/kapasite bulunamadi."
                        : "Konaklayanlar icin yeterli oda/kapasite bulunamadi.",
                    409);
            }

            atananOdaByKonaklayanId[konaklayan.Id] = hedefOdaId.Value;
            kalanSlotByOda[hedefOdaId.Value] -= 1;
            OdaCinsiyetiniKaydet(hedefOdaId.Value, kendiCinsiyeti);
        }

        var sonuc = new List<UzatmaGuestPlanEntry>();

        foreach (var odaGrubu in atananOdaByKonaklayanId.GroupBy(x => x.Value).OrderBy(x => x.Key))
        {
            var odaId = odaGrubu.Key;
            var odaInfo = odaInfoById[odaId];
            var konaklayanIdsInOda = odaGrubu.Select(x => x.Key).OrderBy(x => x).ToList();

            if (!odaInfo.PaylasimliMi)
            {
                sonuc.AddRange(konaklayanIdsInOda.Select(id => new UzatmaGuestPlanEntry { KonaklayanId = id, OdaId = odaId, YatakNo = null }));
                continue;
            }

            var doluYataklar = await (
                    from atama in _stysDbContext.RezervasyonKonaklayanSegmentAtamalari
                    join segment in _stysDbContext.RezervasyonSegmentleri on atama.RezervasyonSegmentId equals segment.Id
                    join konaklayan in _stysDbContext.RezervasyonKonaklayanlar on atama.RezervasyonKonaklayanId equals konaklayan.Id
                    where atama.OdaId == odaId
                          && atama.YatakNo.HasValue
                          && segment.BaslangicTarihi < plan.BitisTarihi
                          && segment.BitisTarihi > plan.BaslangicTarihi
                          && konaklayan.KatilimDurumu != KonaklayanKatilimDurumlari.Gelmedi
                          && konaklayan.RezervasyonId != rezervasyonId
                    select atama.YatakNo!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            var musaitYataklar = Enumerable.Range(1, odaInfo.Kapasite)
                .Except(doluYataklar)
                .OrderBy(x => x)
                .ToList();

            if (musaitYataklar.Count < konaklayanIdsInOda.Count)
            {
                throw new BaseException($"'{odaInfo.OdaNo}' odasi icin konaklayan yatak atamasi yapilamadi.", 409);
            }

            var odaPlanlari = new List<UzatmaGuestPlanEntry>();

            foreach (var konaklayanId in konaklayanIdsInOda)
            {
                var entry = new UzatmaGuestPlanEntry { KonaklayanId = konaklayanId, OdaId = odaId };

                if (oncekiYatakByKonaklayanId.TryGetValue(konaklayanId, out var oncekiYatak)
                    && oncekiYatak.HasValue
                    && musaitYataklar.Remove(oncekiYatak.Value))
                {
                    entry.YatakNo = oncekiYatak.Value;
                }

                odaPlanlari.Add(entry);
            }

            foreach (var entry in odaPlanlari.Where(x => !x.YatakNo.HasValue))
            {
                var sonrakiYatak = musaitYataklar[0];
                musaitYataklar.RemoveAt(0);
                entry.YatakNo = sonrakiYatak;
            }

            sonuc.AddRange(odaPlanlari);
        }

        return sonuc;
    }

    /// <summary>
    /// ComputeUzatmaGuestAssignmentPlanAsync tarafindan ONCEDEN DOGRULANMIS bir plani, artik
    /// veritabaninda var olan (Id'si uretilmis) bir segmente GERCEK RezervasyonKonaklayanSegmentAtama
    /// kayitlari olarak ekler. Bu asamada hicbir dogrulama/hesaplama yapilmaz - yalnizca yazilir.
    /// </summary>
    private void MaterializeUzatmaGuestAssignments(RezervasyonSegment yeniSegment, List<UzatmaGuestPlanEntry> plan)
    {
        foreach (var entry in plan)
        {
            _stysDbContext.RezervasyonKonaklayanSegmentAtamalari.Add(new RezervasyonKonaklayanSegmentAtama
            {
                RezervasyonKonaklayanId = entry.KonaklayanId,
                RezervasyonSegmentId = yeniSegment.Id,
                OdaId = entry.OdaId,
                YatakNo = entry.YatakNo
            });
        }
    }

    private async Task AddExtensionKonaklamaHaklariAsync(
        Rezervasyon reservation,
        DateTime eskiCikisTarihi,
        DateTime yeniCikisTarihi,
        CancellationToken cancellationToken)
    {
        // "HakTarihi >= eskiCikisTarihi" filtresi YANLIS - eski konaklamanin SON GECESI (ör.
        // CheckOutGunuGecerliMi=false oldugu icin ilk rezervasyonda hic uretilmemis bir hak),
        // uzatma sonrasi ARA GUN haline geldiginde HakTarihi hala eskiCikisTarihi'NDEN ONCEKI bir
        // tarih olabilir ve bu filtreyle atlanir. Bunun yerine KUME FARKI yaklasimi kullanilir:
        // 1) eski TOPLAM aralik (giris->eski cikis) icin beklenen haklar,
        // 2) yeni TOPLAM aralik (giris->yeni cikis) icin beklenen haklar uretilir,
        // 3) yenide olup eskide OLMAYAN haklar (anahtar: HizmetKodu+HakTarihi) belirlenir,
        // 4) veritabaninda zaten var olan AKTIF haklar da dislanir,
        // 5) yalnizca GERCEKTEN yeni doğan haklar eklenir. Onceki haklar SILINMEZ/yeniden
        // OLUSTURULMAZ, KonaklamaBoyunca haklar (HakTarihi hep GirisTarihi) bu kume farkinda dogal
        // olarak her iki tarafta da ayni anahtarla yer alip ELENIR - cogaltilmaz.
        var eskiHaklar = await BuildKonaklamaHaklariAsync(
            reservation.TesisId,
            reservation.KonaklamaTipiId!.Value,
            reservation.GirisTarihi,
            eskiCikisTarihi,
            cancellationToken);

        var yeniToplamHaklar = await BuildKonaklamaHaklariAsync(
            reservation.TesisId,
            reservation.KonaklamaTipiId!.Value,
            reservation.GirisTarihi,
            yeniCikisTarihi,
            cancellationToken);

        var eskiAnahtarSeti = eskiHaklar
            .Select(x => (x.HizmetKodu, x.HakTarihi, x.Periyot, x.KullanimTipi))
            .ToHashSet();

        var sonradanDoganHaklar = yeniToplamHaklar
            .Where(x => !eskiAnahtarSeti.Contains((x.HizmetKodu, x.HakTarihi, x.Periyot, x.KullanimTipi)))
            .ToList();

        if (sonradanDoganHaklar.Count == 0)
        {
            return;
        }

        var mevcutAnahtarlar = await _stysDbContext.RezervasyonKonaklamaHaklari
            .Where(x => x.RezervasyonId == reservation.Id && x.AktifMi)
            .Select(x => new { x.HizmetKodu, x.HakTarihi, x.Periyot, x.KullanimTipi })
            .ToListAsync(cancellationToken);
        var mevcutAnahtarSeti = mevcutAnahtarlar
            .Select(x => (x.HizmetKodu, x.HakTarihi, x.Periyot, x.KullanimTipi))
            .ToHashSet();

        var eklenecekHaklar = new List<RezervasyonKonaklamaHakki>();
        foreach (var hak in sonradanDoganHaklar)
        {
            var anahtar = (hak.HizmetKodu, hak.HakTarihi, hak.Periyot, hak.KullanimTipi);
            if (mevcutAnahtarSeti.Contains(anahtar))
            {
                continue;
            }

            // Ayni (henuz kaydedilmemis) toplu eklemede de mukerrer anahtar birikmesin.
            mevcutAnahtarSeti.Add(anahtar);

            hak.RezervasyonId = reservation.Id;
            eklenecekHaklar.Add(hak);
        }

        if (eklenecekHaklar.Count == 0)
        {
            return;
        }

        _stysDbContext.RezervasyonKonaklamaHaklari.AddRange(eklenecekHaklar);
        await _stysDbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed class UzatmaOncekiDegerPayload
    {
        public DateTime EskiCikisTarihi { get; set; }
        public decimal EskiToplamBazUcret { get; set; }
        public decimal EskiToplamUcret { get; set; }
        public UzatmaSegmentOzetPayload? SonSegment { get; set; }
    }

    private sealed class UzatmaSegmentOzetPayload
    {
        public DateTime BaslangicTarihi { get; set; }
        public DateTime BitisTarihi { get; set; }
        public List<UzatmaOdaAtamaOzetPayload> OdaAtamalari { get; set; } = [];
    }

    private sealed class UzatmaOdaAtamaOzetPayload
    {
        public int OdaId { get; set; }
        public int AyrilanKisiSayisi { get; set; }
    }

    private sealed class UzatmaYeniDegerPayload
    {
        public DateTime YeniCikisTarihi { get; set; }
        public string SenaryoKodu { get; set; } = string.Empty;
        public string SenaryoTipi { get; set; } = string.Empty;
        public decimal EkBazUcret { get; set; }
        public decimal EkNihaiUcret { get; set; }
        public decimal YeniToplamBazUcret { get; set; }
        public decimal YeniToplamUcret { get; set; }
        public List<KonaklamaSenaryoSegmentDto> Segmentler { get; set; } = [];
    }
}
