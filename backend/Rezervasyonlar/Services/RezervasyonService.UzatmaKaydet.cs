using System.Data;
using System.Text;
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

                // AyniOdadaDevam'da mevcut konaklayan/oda atamalari DEGISTIRILMEZ - bu yuzden genel
                // backtracking'in "en iyi eslesme" sonucu yerine, HER aktif konaklayanin MEVCUT
                // odasinda GERCEKTEN kalabildigini (kilit altinda, GUNCEL dis dolulukla) ayrica
                // dogrulamak gerekir. Kilit oncesi GetUzatmaSecenekleriAsync de AYNI kontrolu
                // uyguladigi icin bu normalde basarisiz OLMAMALIDIR - ancak kilit ile bu nokta
                // arasinda musaitlik degismis olabilir (TOCTOU), bu yuzden burada TEKRAR dogrulanir.
                if (konaklayanKaydiVarMi)
                {
                    var (oncekiOda, oncekiYatak) = await GetSegmentGuestRoomBedMapAsync(lastSegment.Id, cancellationToken);
                    var dogrulama = await ComputeUzatmaCandidateAssignabilityAsync(
                        reservation.Id,
                        oncekiOda,
                        oncekiYatak,
                        secilenSecenek.Segmentler,
                        tumKonaklayanlar,
                        requireNoMovesForFirstSegment: true,
                        cancellationToken);

                    if (!dogrulama.GecerliMi)
                    {
                        throw new BaseException("Seçilen uzatma planı artık müsait değil. Lütfen seçenekleri yenileyin.", 409);
                    }
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
                // bir atama kurulamazsa VEYA gercek toplam hareket sayisi urun kuralini asarsa (409)
                // hicbir kismi kayit (segment dahil) birakilmaz. Secenek uretiminde kullanilan AYNI
                // paylasilan dogrulayici burada da cagrilir (bkz. GetUzatmaSecenekleriAsync) - boylece
                // kaydetme sirasinda FARKLI bir hareket sayisi/kural uygulanamaz.
                List<UzatmaGuestPlanEntry>? guestPlan = null;
                if (konaklayanKaydiVarMi)
                {
                    var (oncekiOda, oncekiYatak) = await GetSegmentGuestRoomBedMapAsync(lastSegment.Id, cancellationToken);
                    var dogrulama = await ComputeUzatmaCandidateAssignabilityAsync(
                        reservation.Id, oncekiOda, oncekiYatak, [segmentPlan], tumKonaklayanlar,
                        requireNoMovesForFirstSegment: false, cancellationToken);

                    if (!dogrulama.GecerliMi)
                    {
                        throw new BaseException("Seçilen uzatma planı artık müsait değil. Lütfen seçenekleri yenileyin.", 409);
                    }

                    guestPlan = dogrulama.SegmentPlanlari[0];
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
                // iki segmentten HERHANGI biri icin gecerli bir atama kurulamazsa, ilk segmentte
                // konaklayanlar mevcut odalarinda kalmazsa VEYA toplam gercek hareket sayisi 1'i
                // asarsa hicbir kismi kayit (ilk segment dahil) birakilmaz. Secenek uretiminde
                // kullanilan AYNI paylasilan dogrulayici burada da cagrilir.
                List<UzatmaGuestPlanEntry>? ilkGuestPlan = null;
                List<UzatmaGuestPlanEntry>? ikinciGuestPlan = null;

                if (konaklayanKaydiVarMi)
                {
                    var (oncekiOda, oncekiYatak) = await GetSegmentGuestRoomBedMapAsync(lastSegment.Id, cancellationToken);
                    var dogrulama = await ComputeUzatmaCandidateAssignabilityAsync(
                        reservation.Id, oncekiOda, oncekiYatak, [ilkPlan, ikinciPlan], tumKonaklayanlar,
                        requireNoMovesForFirstSegment: true, cancellationToken);

                    if (!dogrulama.GecerliMi)
                    {
                        throw new BaseException("Seçilen uzatma planı artık müsait değil. Lütfen seçenekleri yenileyin.", 409);
                    }

                    ilkGuestPlan = dogrulama.SegmentPlanlari[0];
                    ikinciGuestPlan = dogrulama.SegmentPlanlari[1];
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

    /// <summary>
    /// ComputeUzatmaCandidateAssignabilityAsync'in sonucunu tasir: adayin GERCEKTEN kaydedilebilir
    /// olup olmadigi, tum segmentler boyunca GERCEK (kisi bazli, KonaklayanId uzerinden karsilastirilan)
    /// oda gecisi sayisi, ve HER segment icin uretilen gercek konaklayan/oda dry-run plani (Gelmedi
    /// konaklayanlar bu planlarda hic YER ALMAZ - ComputeUzatmaGuestAssignmentPlanAsync onlari zaten
    /// aktifKonaklayanlar disinda tutar). GecerliMi=false ise ToplamGercekHareket=0 ve
    /// SegmentPlanlari bostur - cagiran bu degerlere GUVENMEMELIDIR.
    /// </summary>
    private sealed class UzatmaCandidateAssignabilityResult
    {
        public required bool GecerliMi { get; init; }
        public required int ToplamGercekHareket { get; init; }
        public required List<List<UzatmaGuestPlanEntry>> SegmentPlanlari { get; init; }
    }

    /// <summary>
    /// Bir uzatma adayinin (1 veya 2 segmentli) kisisel konaklayan/yatak atamasinin GERCEKTEN
    /// kaydedilebilir olup olmadigini, HICBIR VERITABANI YAZIMI YAPMADAN dogrular VE bu arada
    /// uretilen GERCEK (kisi bazli) dry-run planlarini/hareket sayisini da dondurur - boylece
    /// cagiran (kaydetme akisi dahil) ayrica ComputeUzatmaGuestAssignmentPlanAsync'i TEKRAR
    /// cagirmak zorunda kalmaz. GetUzatmaSecenekleriAsync (secenek uretimi, salt okunur,
    /// fiyatlanip kullaniciya sunulmadan ONCE her aday icin) VE RezervasyonUzatAsync (kilit
    /// altinda, baglayici yeniden dogrulama/materyalizasyon sirasinda TUM senaryo tipleri icin)
    /// TARAFINDAN AYNI SEKILDE cagirilir - boylece "seceneklerde gosterilen bir plan kisisel
    /// olarak kaydedilemez VEYA farkli bir hareket sayisiyla kaydedilir" tutarsizligi yapisal
    /// olarak olusamaz.
    ///
    /// Hareket sayisi, HER segment gecisinde (mevcut son segment -> 1. uzatma segmenti, 1. uzatma
    /// segmenti -> 2. uzatma segmenti) her aktif konaklayanin KonaklayanId'si uzerinden GERCEK oda
    /// atamasi karsilastirilarak hesaplanir - TOPLU (oda bazli kisi sayisi) karsilastirma KULLANILMAZ,
    /// cunku toplu karsilastirma iki farkli konaklayanin birbirinin onceki odasina TAKAS yapmasini
    /// (ayni oda ID kumesi + ayni sayilar korundugu icin) "degisiklik yok" olarak YANLIS raporlayabilir.
    ///
    /// requireNoMovesForFirstSegment=true ise (AyniOdadaDevam, oda degisim sayisi 0 olan tek-segmentli
    /// adaylar, VE UzatmaSirasindaOdaDegisimi'nin ILK segmenti - konaklayanlar checkout gununde degil,
    /// YALNIZCA uzatma sirasindaki sinirda tasinmalidir), BIRINCI segmentte HICBIR aktif konaklayanin
    /// oda degistirmemesi sarti da aranir. Ayrica - iki SEGMENTLI (UzatmaSirasindaOdaDegisimi) adaylar
    /// icin mevcut urun kurali "en fazla bir oda degisikligi" oldugu icin, TUM segmentler boyunca
    /// toplanan GERCEK hareket sayisi 1'i asarsa aday GECERSIZ sayilir - bu, "checkout gununde bir +
    /// uzatma sirasinda ikinci degisiklik" gibi cift hareketli iki-segmentli planlari kapsar (varsayilan
    /// "tek toplam hareket" politikasi; iş kurali gelecekte ikinci degisikligi acikca izin verirse bu
    /// kisitlama ayrica gevsetilmelidir - bkz. GetUzatmaSecenekleriAsync). TEK segmentli (ör.
    /// CheckoutGunundeOdaDegisimi) adaylarda ise BIRDEN FAZLA konaklayanin AYNI checkout-gunu
    /// degisikliginde es zamanli GERCEKTEN yer degistirmesi (ör. paylasilan bir odanin iki farkli
    /// cinsiyet-sabit odaya bolunmesi) tek bir "degisiklik olayi" sayilir ve bu kisitlamaya TABI
    /// DEGILDIR - OdaDegisimSayisi bu durumda gercek hareket sayisini (>1 olabilir) DOGRU sekilde
    /// yansitir. Toplu CalculateRoomChangeCount, HER IKI durumda da nihai karar icin KULLANILMAZ,
    /// yalnizca aday uretiminde erken/iyimser eleme icin kullanilabilir. Konaklayan kaydi yoksa (eski/
    /// planli-doluluk-only rezervasyonlar) dogrulama uygulanmaz - mevcut davranis korunur
    /// (GecerliMi=true, ToplamGercekHareket=0, SegmentPlanlari bos).
    /// </summary>
    private async Task<UzatmaCandidateAssignabilityResult> ComputeUzatmaCandidateAssignabilityAsync(
        int rezervasyonId,
        IReadOnlyDictionary<int, int> oncekiOdaBaslangic,
        IReadOnlyDictionary<int, int?> oncekiYatakBaslangic,
        IReadOnlyList<KonaklamaSenaryoSegmentDto> segmentler,
        IReadOnlyCollection<RezervasyonKonaklayan> tumKonaklayanlar,
        bool requireNoMovesForFirstSegment,
        CancellationToken cancellationToken)
    {
        if (tumKonaklayanlar.Count == 0)
        {
            return new UzatmaCandidateAssignabilityResult { GecerliMi = true, ToplamGercekHareket = 0, SegmentPlanlari = [] };
        }

        static UzatmaCandidateAssignabilityResult Gecersiz() =>
            new() { GecerliMi = false, ToplamGercekHareket = 0, SegmentPlanlari = [] };

        try
        {
            var oncekiOda = oncekiOdaBaslangic;
            var oncekiYatak = oncekiYatakBaslangic;
            var segmentPlanlari = new List<List<UzatmaGuestPlanEntry>>();
            var toplamHareket = 0;

            for (var i = 0; i < segmentler.Count; i++)
            {
                var sonuc = await ComputeUzatmaGuestAssignmentPlanAsync(
                    rezervasyonId, oncekiOda, oncekiYatak, segmentler[i], tumKonaklayanlar, 0, cancellationToken);

                var oncekiOdaSnapshot = oncekiOda;
                var hareketSayisi = sonuc.Count(x => !oncekiOdaSnapshot.TryGetValue(x.KonaklayanId, out var oda) || oda != x.OdaId);

                if (i == 0 && requireNoMovesForFirstSegment && hareketSayisi > 0)
                {
                    return Gecersiz();
                }

                toplamHareket += hareketSayisi;
                segmentPlanlari.Add(sonuc);

                oncekiOda = sonuc.ToDictionary(x => x.KonaklayanId, x => x.OdaId);
                oncekiYatak = sonuc.ToDictionary(x => x.KonaklayanId, x => x.YatakNo);
            }

            // "En fazla bir oda degisikligi" toplam-hareket kisitlamasi yalnizca IKI SEGMENTLI
            // (UzatmaSirasindaOdaDegisimi) adaylara uygulanir - tek segmentli adaylarda (ör.
            // CheckoutGunundeOdaDegisimi) birden fazla konaklayanin AYNI checkout-gunu degisikliginde
            // es zamanli yer degistirmesi tek bir "degisiklik olayi"dir ve reddedilmez. Bu durumda
            // (GecerliMi=false DONSE BILE) GERCEK hesaplanan ToplamGercekHareket DEGERI korunur
            // (SegmentPlanlari bos birakilir) - boylece cagiran/testler "kac gercek hareket TESPIT
            // EDILDI" bilgisini, adayin NEDEN reddedildiginden BAGIMSIZ olarak gozlemleyebilir.
            if (segmentler.Count > 1 && toplamHareket > 1)
            {
                return new UzatmaCandidateAssignabilityResult { GecerliMi = false, ToplamGercekHareket = toplamHareket, SegmentPlanlari = [] };
            }

            return new UzatmaCandidateAssignabilityResult
            {
                GecerliMi = true,
                ToplamGercekHareket = toplamHareket,
                SegmentPlanlari = segmentPlanlari
            };
        }
        catch (BaseException)
        {
            return Gecersiz();
        }
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
    /// Bir paylasimli odanin, kendi rezervasyonumuzdan HERHANGI BIR konaklayan icin (bilinen VEYA
    /// bilinmeyen cinsiyetli) KULLANILAMAZ oldugunu isaretlemek icin kullanilan ozel bir "sabit
    /// cinsiyet" degeri. Gercek bir Kadin/Erkek degerine ASLA esit olmadigindan (ve null da
    /// OLMADIGINDAN), FindBestGuestRoomAssignment'in mevcut OdaUygunMu kontrolu (mevcutDurum is
    /// null || string.Equals(mevcutDurum, cinsiyet)) bu degeri gordugunde HER cinsiyet icin
    /// (bilinen VEYA bilinmeyen) otomatik olarak "uygun degil" sonucunu uretir - algoritmada baska
    /// hicbir degisiklik gerektirmez.
    /// </summary>
    private const string ExternalUnknownGenderBlockedSentinel = "__STYS_UZATMA_DIS_BILINMEYEN_CINSIYET_ENGELLI__";

    /// <summary>
    /// GetSharedRoomGuestOccupanciesAsync ile TAMAMEN AYNI sorguyu (aktif/iptal-degil/checkout-
    /// tamamlanmamis/Gelmedi-degil filtreleri dahil) calistirir - ANCAK cinsiyeti BILINMEYEN (null)
    /// aktif konaklayanlari da SONUCTA TUTAR. GetSharedRoomGuestOccupanciesAsync bu satirlari
    /// FILTRELER (baska, uzatma-disi cagiranlarin - check-in/oda degisimi/rezervasyon olusturma vb.
    /// - MEVCUT davranisini bozmamak icin O metot DEGISTIRILMEDI); uzatma konaklayan eslesmesi ise
    /// haricî cinsiyeti bilinmeyen bir kisinin VARLIGINI da bilmek ZORUNDADIR - aksi halde boyle
    /// bir kisinin bulundugu paylasimli oda yanlislikla "bos/serbest" sayilip baska bir
    /// konaklayan (bilinen VEYA bilinmeyen cinsiyetli) oraya atanabilir.
    /// </summary>
    private async Task<List<SharedRoomGuestOccupancy>> GetSharedRoomGuestOccupanciesIncludingUnknownGenderAsync(
        IReadOnlyCollection<int> roomIds,
        DateTime baslangic,
        DateTime bitis,
        CancellationToken cancellationToken,
        int? excludeRezervasyonId = null)
    {
        if (roomIds.Count == 0)
        {
            return [];
        }

        var occupancies = await (
                from atama in _stysDbContext.RezervasyonKonaklayanSegmentAtamalari
                join konaklayan in _stysDbContext.RezervasyonKonaklayanlar on atama.RezervasyonKonaklayanId equals konaklayan.Id
                join segment in _stysDbContext.RezervasyonSegmentleri on atama.RezervasyonSegmentId equals segment.Id
                join rezervasyon in _stysDbContext.Rezervasyonlar on konaklayan.RezervasyonId equals rezervasyon.Id
                where rezervasyon.AktifMi
                      && rezervasyon.RezervasyonDurumu != RezervasyonDurumlari.Iptal
                      && rezervasyon.RezervasyonDurumu != RezervasyonDurumlari.CheckOutTamamlandi
                      && konaklayan.KatilimDurumu != KonaklayanKatilimDurumlari.Gelmedi
                      && (!excludeRezervasyonId.HasValue || rezervasyon.Id != excludeRezervasyonId.Value)
                      && roomIds.Contains(atama.OdaId)
                      && segment.BaslangicTarihi < bitis
                      && segment.BitisTarihi > baslangic
                select new
                {
                    atama.OdaId,
                    segment.BaslangicTarihi,
                    segment.BitisTarihi,
                    konaklayan.Cinsiyet
                })
            .ToListAsync(cancellationToken);

        return occupancies
            .Select(x => new SharedRoomGuestOccupancy(
                x.OdaId,
                x.BaslangicTarihi,
                x.BitisTarihi,
                NormalizeStoredKonaklayanCinsiyet(x.Cinsiyet)))
            .ToList();
    }

    /// <summary>
    /// Bir uzatma segmenti icin, rezervasyonun AKTIF (Gelmedi HARIC) konaklayanlarinin oda/yatak
    /// atama PLANINI, HICBIR VERITABANI YAZIMI YAPMADAN hesaplar (salt-okunur DB sorgulari haric) -
    /// gecersiz bir plan (cinsiyet veya kapasite acisindan) burada 409 firlatir ve cagiran, bu segment
    /// (veya iki-segmentli senaryoda ONCEKI segment) icin HENUZ HICBIR entity OLUSTURMAMIS olur.
    ///
    /// Oda/kisi eslesmesi, TAM BIR BACKTRACKING ARAMASI (FindBestGuestRoomAssignment) ile bulunur -
    /// "once ayni odada tutmayi dene, sonra kalanlari dagit" seklindeki AC-GOZLU iki gecisli yaklasim
    /// KULLANILMAZ, cunku bu yaklasim GECERLI bir genel dagilim varken bile erken yapilan "ayni odada
    /// tut" secimi yuzunden yanlislikla 409 uretebilir (ornek: bos paylasimli oda A ile digerinden
    /// gelen kadin nedeniyle kadin-sabit paylasimli oda B varken, kadin konaklayanin onceki odasi A
    /// ise, erkek konaklayan A'ya SIGMAZ VE B'ye de giremez - oysa kadin->B, erkek->A GECERLIDIR).
    /// Arama, ONCE tam eslesmenin (feasibility) VAR OLUP olmadigini, sonra bu eslesmeler arasinda
    /// mumkun olan EN FAZLA konaklayanin onceki odasinda kaldigi cozumu bulur; esit cozumlerde
    /// SiraNo/konaklayanId/odaId sirasiyla deterministik secim yapilir.
    ///
    /// Cinsiyet farkindaligi HER KONAKLAYAN ICIN AYRI AYRI degerlendirilir: bir konaklayanin cinsiyeti
    /// BILINIYORSA (Gelmedi disindaki BASKA bir konaklayanin cinsiyeti bilinmese veya rezervasyonun
    /// KisiSayisi'ndan az aktif konaklayan olsa BILE) sabit karsi cinsiyet odasina ATANMAZ ve bos bir
    /// paylasimli odada farkli cinsiyetler KARISTIRILMAZ. Cinsiyeti BILINMEYEN bir konaklayan icin ise
    /// MUHAFAZAKAR (guvenli) bir politika uygulanir - "herhangi bir odaya girebilir" DEGILDIR: SADECE
    /// (a) paylasimli OLMAYAN (ozel) bir odaya, veya (b) hicbir dis rezervasyon (bilinen VEYA
    /// cinsiyeti bilinmeyen bir kisi dahil) ya da bilinen bir konaklayan tarafindan sabitlenmemis/
    /// engellenmemis TAMAMEN ACIK bir paylasimli odaya, ve o odada YALNIZCA kendisi (kendi
    /// rezervasyonundan TEK aktif konaklayan) olacaksa atanabilir - bu odaya baska HICBIR konaklayan
    /// (bilinen VEYA bilinmeyen cinsiyetli) SONRADAN KATILAMAZ. Paylasimli her hedef odanin GERCEK
    /// (DB'den, baska rezervasyonlardan gelen) sabit cinsiyeti - ve cinsiyeti bilinmeyen HARICÎ aktif
    /// bir kisinin varligi - GetSharedRoomGuestOccupanciesIncludingUnknownGenderAsync ile YENIDEN
    /// belirlenir; istemciden veya onceki hesaplamadan gelen bilgiye GUVENILMEZ. Paylasimli odalarda
    /// yatak numarasi, oda eslesmesi KESINLESTIKTEN SONRA,
    /// ReassignGuestSegmentAssignmentsAfterRoomChangeAsync ile AYNI "diger rezervasyonlarin isgal
    /// ettigi yataklari haric tut" desenini kullanir - yatak kontrolu cinsiyet kontrolunun YERINE
    /// GECMEZ.
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
        _ = kisiSayisi; // GetUzatmaSecenekleriAsync ile parametre uyumu icin korunur; cinsiyet farkindaligi artik HER KONAKLAYAN ICIN AYRI belirlenir (bkz. yontem ozeti).

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

        // Paylasimli her hedef odanin, bu uzatma segmentinin tarih araliginda GERCEK (DB'den, BASKA
        // rezervasyonlardan gelen) sabit cinsiyetini yeniden belirle. GetDistinctSharedRoomGenders
        // (ve onun kullandigi GetSharedRoomGuestOccupanciesAsync) cinsiyeti BILINMEYEN aktif
        // konaklayanlari SESSIZCE ELER - bu, diger (uzatma-disi) cagiranlarin mevcut davranisini
        // BOZMAMAK icin degistirilmedi. Bunun yerine, uzatmaya OZEL
        // GetSharedRoomGuestOccupanciesIncludingUnknownGenderAsync ile AYNI sorgu, cinsiyeti
        // bilinmeyenleri de TUTARAK tekrar calistirilir - boylece haricî cinsiyeti bilinmeyen bir
        // kisinin bulundugu paylasimli oda YANLISLIKLA "bos/serbest" sayilmaz. Bu, konaklayanlarin
        // cinsiyetinin bilinip bilinmemesinden VEYA sayilarinin KisiSayisi'na esit olup olmamasindan
        // (Gelmedi kaydi) BAGIMSIZ, HER ZAMAN yapilir - boylece Gelmedi kaydi VARLIGI digerlerinin
        // cinsiyet korumasini KAPATMAZ.
        var sabitCinsiyetByOda = new Dictionary<int, string?>();
        var paylasimliOdaIds = plan.OdaAtamalari.Where(x => x.PaylasimliMi).Select(x => x.OdaId).Distinct().ToList();
        if (paylasimliOdaIds.Count > 0)
        {
            var digerRezervasyonDoluluklariButun = await GetSharedRoomGuestOccupanciesIncludingUnknownGenderAsync(
                paylasimliOdaIds, plan.BaslangicTarihi, plan.BitisTarihi, cancellationToken, excludeRezervasyonId: rezervasyonId);

            foreach (var odaId in paylasimliOdaIds)
            {
                var buOdadakiDoluluklar = digerRezervasyonDoluluklariButun
                    .Where(x => x.OdaId == odaId && x.BaslangicTarihi < plan.BitisTarihi && x.BitisTarihi > plan.BaslangicTarihi)
                    .ToList();

                var bilinenCinsiyetler = buOdadakiDoluluklar
                    .Where(x => x.Cinsiyet is not null)
                    .Select(x => x.Cinsiyet!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (bilinenCinsiyetler.Count > 1)
                {
                    throw new BaseException($"'{odaInfoById[odaId].OdaNo}' odasinda cinsiyet tutarsizligi tespit edildi; konaklayan atamasi yapilamiyor.", 409);
                }

                var bilinmeyenHaricîKisiVarMi = buOdadakiDoluluklar.Any(x => x.Cinsiyet is null);
                if (bilinmeyenHaricîKisiVarMi)
                {
                    // Baska bir rezervasyondan, cinsiyeti bilinmeyen AKTIF bir konaklayan bu odada
                    // kaliyor - uyumlulugu DOGRULANAMAYACAGI icin oda, kendi rezervasyonumuzdan
                    // HICBIR konaklayan (bilinen VEYA bilinmeyen cinsiyetli) icin KULLANILAMAZ.
                    sabitCinsiyetByOda[odaId] = ExternalUnknownGenderBlockedSentinel;
                }
                else
                {
                    sabitCinsiyetByOda[odaId] = bilinenCinsiyetler.SingleOrDefault();
                }
            }
        }

        var guests = aktifKonaklayanlar
            .Select(x => new UzatmaGuestMatchInput(
                x.Id,
                NormalizeStoredKonaklayanCinsiyet(x.Cinsiyet),
                oncekiOdaByKonaklayanId.TryGetValue(x.Id, out var oncekiOdaId) ? oncekiOdaId : null))
            .ToList();

        var rooms = plan.OdaAtamalari
            .Select(x => new UzatmaRoomMatchInput(x.OdaId, x.AyrilanKisiSayisi, x.PaylasimliMi, sabitCinsiyetByOda.GetValueOrDefault(x.OdaId)))
            .ToList();

        var atananOdaByKonaklayanId = FindBestGuestRoomAssignment(guests, rooms)
            ?? throw new BaseException(
                aktifKonaklayanlar.Any(x => NormalizeStoredKonaklayanCinsiyet(x.Cinsiyet) is not null)
                    ? "Konaklayanlar icin cinsiyet acisindan gecerli oda/kapasite bulunamadi."
                    : "Konaklayanlar icin yeterli oda/kapasite bulunamadi.",
                409);

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

    private sealed record UzatmaGuestMatchInput(int KonaklayanId, string? Cinsiyet, int? OncekiOdaId);

    private sealed record UzatmaRoomMatchInput(int OdaId, int Kapasite, bool PaylasimliMi, string? SabitCinsiyet);

    /// <summary>
    /// Konaklayan-oda eslesmesini TAM DURUM ANAHTARLI MEMOIZATION ile bulur: her alt problem
    /// (kalan konaklayan kumesi + oda basina kalan kapasite + oda basina cinsiyet durumu + unknown-
    /// solo odalar) TEK BIR anahtarla temsil edilir ve bir kez cozulur - AYNI duruma FARKLI bir
    /// yoldan tekrar ulasilirsa onceki sonuc DOGRUDAN kullanilir. Bu, riskli/yerel bir "simetri
    /// eleme" sezgiselinden (odalari kapasite+cinsiyet durumuna gore GRUPLAYIP yalnizca bir
    /// temsilciyi deneme) KACINIR - boyle bir sezgisel, ozel (paylasimli olmayan) bir odayi bos bir
    /// paylasimli odayla yanlislikla ES DEGER sayabilir (ikisi de "durum=null" gorunse de PAYLASIMLI
    /// OLMA ozelligi FARKLIDIR) veya kalan bir konaklayanin ONCEKI odasini "sadece bir temsilci"
    /// olarak elenebilir hale getirip "en fazla kisiyi ayni odada tut" hedefini bozabilirdi. Burada
    /// HICBIR oda BASKA bir odayla ES DEGER sayilmaz - durum anahtari HER odayi (PaylasimliMi dahil)
    /// AYRI AYRI kodlar; memoization yalnizca GERCEKTEN AYNI global duruma (ayni kalan konaklayan
    /// kumesi + ayni tum oda durumlari) birden fazla yoldan ulasildiginda devreye girer, bu da HER
    /// ZAMAN guvenlidir (klasik alt-kume + kaynak durumu DP'si).
    ///
    /// Performans icin iki teknik daha uygulanir:
    /// 1) MRV (en kisitli konaklayan once): her alt problemde, uygun aday odasi EN AZ olan
    ///    konaklayan islenir - cikmaz sokaklarin erken tespitini saglar VE ayni zamanda anahtarin
    ///    "hangi konaklayan once islenecek" belirsizligini de deterministik kilar (bkz. asagida).
    /// 2) YEREL UST SINIR BUDAMASI: bir konaklayanin aday odalari denenirken, "bu adayda kalinip
    ///    kalinmadigi + kalan konaklayanlarin ULASILABILECEGI AZAMI ek kalma sayisi" o konaklayan
    ///    icin su ana kadarki EN IYI sonucu GECEMEYECEKSE, o aday HICBIR SEKILDE aranmadan atlanir.
    /// Gecerli bir tam eslesme YOKSA null doner - cagiran bunu 409 olarak yorumlar. MRV secimi VE
    /// aday oda siralamasi (onceki oda / sabit cinsiyetli oda / OdaId) TAMAMEN deterministik
    /// oldugundan ve memoization SADECE ayni girdi icin ayni ciktiyi tekrar kullandigindan, SONUC
    /// HER ZAMAN DETERMINISTIKTIR.
    ///
    /// Cinsiyeti BILINMEYEN bir konaklayan icin MUHAFAZAKAR politika uygulanir: byle bir konaklayan
    /// SADECE (a) paylasimli OLMAYAN bir odaya, veya (b) hicbir dis rezervasyon VEYA bilinen
    /// konaklayan tarafindan sabitlenmemis/engellenmemis BOS bir paylasimli odaya, ve YALNIZCA o
    /// odadaki TEK (kendi rezervasyonundan) aktif konaklayan olacaksa atanabilir - bu odaya baska
    /// HICBIR konaklayan (bilinen VEYA bilinmeyen cinsiyetli) SONRADAN KATILAMAZ. Haricî (baska bir
    /// rezervasyondan) cinsiyeti bilinmeyen bir kisinin bulundugu paylasimli oda, ROOM.SabitCinsiyet
    /// alaninda ExternalUnknownGenderBlockedSentinel degeriyle temsil edilir - bu deger GERCEK bir
    /// Kadin/Erkek degerine ASLA esit olmadigindan (ve null da OLMADIGINDAN), asagidaki OdaUygunMu
    /// kontrolu bu odayi HER cinsiyet (bilinen VEYA bilinmeyen) icin otomatik olarak "uygun degil"
    /// sayar - ayrica bir ozel durum kodu gerekmez.
    /// </summary>
    private static Dictionary<int, int>? FindBestGuestRoomAssignment(
        IReadOnlyList<UzatmaGuestMatchInput> guests,
        IReadOnlyList<UzatmaRoomMatchInput> rooms)
    {
        var roomById = rooms.ToDictionary(x => x.OdaId);
        var sortedRoomIds = rooms.Select(x => x.OdaId).OrderBy(x => x).ToList();

        // Memoization: ayni (kalan konaklayan kumesi + tum odalarin kapasite/cinsiyet durumu)
        // durumuna FARKLI bir yoldan tekrar ulasilirsa, alt problem TEKRAR COZULMEZ.
        var memo = new Dictionary<string, (int BestEk, Dictionary<int, int>? Atama)>();

        bool OdaUygunMu(IReadOnlyDictionary<int, int> kalanKapasite, IReadOnlyDictionary<int, string?> odaDurumu, HashSet<int> unknownSoloOdalar, int odaId, string? cinsiyet)
        {
            var oda = roomById[odaId];
            if (!oda.PaylasimliMi)
            {
                return true;
            }

            if (unknownSoloOdalar.Contains(odaId))
            {
                return false;
            }

            var mevcutDurum = odaDurumu.GetValueOrDefault(odaId);
            if (cinsiyet is null)
            {
                // Cinsiyeti bilinmeyen konaklayan, SADECE tamamen acik (dis/bilinen/haricî-bilinmeyen
                // kisi tarafindan hic sabitlenmemis/engellenmemis) bir paylasimli odaya girebilir.
                return mevcutDurum is null;
            }

            return mevcutDurum is null || string.Equals(mevcutDurum, cinsiyet, StringComparison.OrdinalIgnoreCase);
        }

        int MaxEkPotansiyel(List<int> indeksler) =>
            indeksler.Count(i => guests[i].OncekiOdaId.HasValue && roomById.ContainsKey(guests[i].OncekiOdaId!.Value));

        string BuildStateKey(List<int> kalanIndeksler, Dictionary<int, int> kalanKapasite, Dictionary<int, string?> odaDurumu, HashSet<int> unknownSoloOdalar)
        {
            var sb = new StringBuilder();
            sb.Append(string.Join(',', kalanIndeksler.OrderBy(i => i)));
            sb.Append('|');
            foreach (var odaId in sortedRoomIds)
            {
                sb.Append(odaId).Append(':').Append(kalanKapasite[odaId]).Append(':');
                if (!roomById[odaId].PaylasimliMi)
                {
                    sb.Append('P'); // Ozel (paylasimli olmayan) - paylasimli bir odanin durum
                                    // kodlarindan (cinsiyet adlari, "O"=acik, "U"=unknown-solo, sentinel)
                                    // HER ZAMAN ayirt edilir; hicbir zaman onlarla ES DEGER sayilmaz.
                }
                else if (unknownSoloOdalar.Contains(odaId))
                {
                    sb.Append('U');
                }
                else
                {
                    sb.Append(odaDurumu.GetValueOrDefault(odaId) ?? "O");
                }

                sb.Append(';');
            }

            return sb.ToString();
        }

        (int BestEk, Dictionary<int, int>? Atama) Solve(
            List<int> kalanIndeksler,
            Dictionary<int, int> kalanKapasite,
            Dictionary<int, string?> odaDurumu,
            HashSet<int> unknownSoloOdalar)
        {
            if (kalanIndeksler.Count == 0)
            {
                return (0, new Dictionary<int, int>());
            }

            var key = BuildStateKey(kalanIndeksler, kalanKapasite, odaDurumu, unknownSoloOdalar);
            if (memo.TryGetValue(key, out var onbellek))
            {
                return onbellek;
            }

            // MRV: aday odasi EN AZ olan konaklayan ONCE islenir - esitlikte konaklayanin ORIJINAL
            // (SiraNo/Id sirali) liste konumu kullanilir - bu, ayni durum icin HER ZAMAN AYNI
            // konaklayanin secilmesini (dolayisiyla anahtarin GERCEKTEN ayni alt problemi temsil
            // etmesini) garanti eder.
            var siraliAdaylar = kalanIndeksler
                .Select(i => (Index: i, Adaylar: rooms
                    .Where(r => kalanKapasite[r.OdaId] > 0 && OdaUygunMu(kalanKapasite, odaDurumu, unknownSoloOdalar, r.OdaId, guests[i].Cinsiyet))
                    .Select(r => r.OdaId)
                    .ToList()))
                .OrderBy(x => x.Adaylar.Count)
                .ThenBy(x => x.Index)
                .First();

            var guestIndex = siraliAdaylar.Index;
            var guest = guests[guestIndex];

            if (siraliAdaylar.Adaylar.Count == 0)
            {
                var yok = (BestEk: int.MinValue, Atama: (Dictionary<int, int>?)null);
                memo[key] = yok;
                return yok;
            }

            var adayOdalar = siraliAdaylar.Adaylar
                .OrderBy(odaId => odaId == guest.OncekiOdaId ? 0 : 1)
                .ThenBy(odaId => roomById[odaId].SabitCinsiyet is not null ? 0 : 1)
                .ThenBy(odaId => odaId)
                .ToList();

            var yeniKalanIndeksler = kalanIndeksler.Where(i => i != guestIndex).ToList();
            var kalanMaxPotansiyel = MaxEkPotansiyel(yeniKalanIndeksler);

            var enIyiEk = -1;
            Dictionary<int, int>? enIyiAtama = null;

            foreach (var odaId in adayOdalar)
            {
                var kalindiMi = odaId == guest.OncekiOdaId;

                // Yerel ust sinir budamasi: bu adayin en iyimser toplami (kalindiysa +1, artik
                // kalan tum konaklayanlarin teorik AZAMI ek kalmasi), su ana kadarki en iyiyi
                // GECEMEYECEKSE, bu aday HIC DENENMEDEN atlanir.
                var buAdayinUstSiniri = (kalindiMi ? 1 : 0) + kalanMaxPotansiyel;
                if (buAdayinUstSiniri <= enIyiEk)
                {
                    continue;
                }

                var paylasimli = roomById[odaId].PaylasimliMi;
                var oncekiDurum = odaDurumu.GetValueOrDefault(odaId);
                var unknownAtandi = paylasimli && guest.Cinsiyet is null;

                kalanKapasite[odaId]--;
                if (paylasimli)
                {
                    if (guest.Cinsiyet is not null)
                    {
                        odaDurumu[odaId] = guest.Cinsiyet;
                    }
                    else
                    {
                        unknownSoloOdalar.Add(odaId);
                    }
                }

                var altSonuc = Solve(yeniKalanIndeksler, kalanKapasite, odaDurumu, unknownSoloOdalar);

                kalanKapasite[odaId]++;
                if (paylasimli)
                {
                    if (unknownAtandi)
                    {
                        unknownSoloOdalar.Remove(odaId);
                    }
                    else
                    {
                        odaDurumu[odaId] = oncekiDurum;
                    }
                }

                if (altSonuc.Atama is null)
                {
                    continue; // Bu dal cikmaz sokak.
                }

                var toplamEk = (kalindiMi ? 1 : 0) + altSonuc.BestEk;
                if (toplamEk > enIyiEk)
                {
                    enIyiEk = toplamEk;
                    enIyiAtama = new Dictionary<int, int>(altSonuc.Atama) { [guest.KonaklayanId] = odaId };
                }
            }

            var sonuc = (BestEk: enIyiAtama is null ? int.MinValue : enIyiEk, Atama: enIyiAtama);
            memo[key] = sonuc;
            return sonuc;
        }

        var baslangicKalanKapasite = rooms.ToDictionary(x => x.OdaId, x => x.Kapasite);
        // Paylasimli odalarin baslangic durumu: null = tamamen ACIK; bir cinsiyet degeri = o
        // cinsiyete SABITLENMIS; ExternalUnknownGenderBlockedSentinel = haricî cinsiyeti bilinmeyen
        // biri nedeniyle TAMAMEN ENGELLI.
        var baslangicOdaDurumu = rooms.Where(x => x.PaylasimliMi).ToDictionary(x => x.OdaId, x => x.SabitCinsiyet);
        var baslangicUnknownSoloOdalar = new HashSet<int>();

        var sonuc = Solve(Enumerable.Range(0, guests.Count).ToList(), baslangicKalanKapasite, baslangicOdaDurumu, baslangicUnknownSoloOdalar);
        return sonuc.Atama;
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
