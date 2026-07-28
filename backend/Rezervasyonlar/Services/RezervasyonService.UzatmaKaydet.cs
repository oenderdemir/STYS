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
                var yeniSegment = await CreateUzatmaSegmentAsync(reservation.Id, maxSegmentSirasi + 1, segmentPlan, cancellationToken);

                if (konaklayanKaydiVarMi)
                {
                    await AssignGuestsToNewUzatmaSegmentAsync(
                        reservation.Id, lastSegment.Id, yeniSegment, segmentPlan, tumKonaklayanlar, cancellationToken);
                }

                break;
            }

            case RezervasyonUzatmaSenaryoTipleri.UzatmaSirasindaOdaDegisimi:
            {
                var ilkPlan = secilenSecenek.Segmentler[0];
                var ikinciPlan = secilenSecenek.Segmentler[1];

                var ilkSegment = await CreateUzatmaSegmentAsync(reservation.Id, maxSegmentSirasi + 1, ilkPlan, cancellationToken);

                if (konaklayanKaydiVarMi)
                {
                    await AssignGuestsToNewUzatmaSegmentAsync(
                        reservation.Id, lastSegment.Id, ilkSegment, ilkPlan, tumKonaklayanlar, cancellationToken);
                }

                var ikinciSegment = await CreateUzatmaSegmentAsync(reservation.Id, maxSegmentSirasi + 2, ikinciPlan, cancellationToken);

                if (konaklayanKaydiVarMi)
                {
                    await AssignGuestsToNewUzatmaSegmentAsync(
                        reservation.Id, ilkSegment.Id, ikinciSegment, ikinciPlan, tumKonaklayanlar, cancellationToken);
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
    /// Yeni olusturulan bir uzatma segmenti icin, rezervasyonun AKTIF (Gelmedi HARIC) konaklayanlarina
    /// oda/yatak atamasi olusturur. Onceki segmentteki oda hala hedef dagilimda mevcutsa konaklayan
    /// O ODADA TUTULUR (mumkun oldugunca az kisi taginir); kalan konaklayanlar deterministik
    /// bicimde (konaklayan SiraNo, hedef oda ID sirasiyla) kalan slotlara atanir. Paylasimli odalarda
    /// yatak numarasi, ReassignGuestSegmentAssignmentsAfterRoomChangeAsync ile AYNI "diger
    /// rezervasyonlarin isgal ettigi yataklari haric tut" desenini kullanir.
    /// </summary>
    private async Task AssignGuestsToNewUzatmaSegmentAsync(
        int rezervasyonId,
        int oncekiSegmentId,
        RezervasyonSegment yeniSegment,
        KonaklamaSenaryoSegmentDto plan,
        IReadOnlyCollection<RezervasyonKonaklayan> tumKonaklayanlar,
        CancellationToken cancellationToken)
    {
        var aktifKonaklayanlar = tumKonaklayanlar
            .Where(x => x.KatilimDurumu != KonaklayanKatilimDurumlari.Gelmedi)
            .OrderBy(x => x.SiraNo)
            .ThenBy(x => x.Id)
            .ToList();

        if (aktifKonaklayanlar.Count == 0)
        {
            return;
        }

        var oncekiOdaByKonaklayanId = await _stysDbContext.RezervasyonKonaklayanSegmentAtamalari
            .Where(x => x.RezervasyonSegmentId == oncekiSegmentId)
            .ToDictionaryAsync(x => x.RezervasyonKonaklayanId, x => x.OdaId, cancellationToken);

        var kalanSlotByOda = plan.OdaAtamalari
            .OrderBy(x => x.OdaId)
            .ToDictionary(x => x.OdaId, x => x.AyrilanKisiSayisi);

        var atananOdaByKonaklayanId = new Dictionary<int, int>();

        // 1) Konaklayanin onceki odasi hedef dagilimda hala mevcutsa AYNI odada tutulur.
        foreach (var konaklayan in aktifKonaklayanlar)
        {
            if (oncekiOdaByKonaklayanId.TryGetValue(konaklayan.Id, out var oncekiOdaId)
                && kalanSlotByOda.TryGetValue(oncekiOdaId, out var kalan)
                && kalan > 0)
            {
                atananOdaByKonaklayanId[konaklayan.Id] = oncekiOdaId;
                kalanSlotByOda[oncekiOdaId] = kalan - 1;
            }
        }

        // 2) Yalnizca GERCEKTEN tasinmasi gereken konaklayanlar, deterministik sirayla kalan
        // slotlara atanir.
        foreach (var konaklayan in aktifKonaklayanlar)
        {
            if (atananOdaByKonaklayanId.ContainsKey(konaklayan.Id))
            {
                continue;
            }

            var hedefOdaId = kalanSlotByOda
                .Where(x => x.Value > 0)
                .OrderBy(x => x.Key)
                .Select(x => (int?)x.Key)
                .FirstOrDefault();

            if (hedefOdaId is null)
            {
                throw new BaseException("Konaklayanlar icin yeterli oda/kapasite bulunamadi.", 409);
            }

            atananOdaByKonaklayanId[konaklayan.Id] = hedefOdaId.Value;
            kalanSlotByOda[hedefOdaId.Value] -= 1;
        }

        var odaInfoById = plan.OdaAtamalari.ToDictionary(x => x.OdaId);

        foreach (var odaGrubu in atananOdaByKonaklayanId.GroupBy(x => x.Value).OrderBy(x => x.Key))
        {
            var odaId = odaGrubu.Key;
            var odaInfo = odaInfoById[odaId];
            var konaklayanIdsInOda = odaGrubu.Select(x => x.Key).OrderBy(x => x).ToList();

            if (!odaInfo.PaylasimliMi)
            {
                foreach (var konaklayanId in konaklayanIdsInOda)
                {
                    _stysDbContext.RezervasyonKonaklayanSegmentAtamalari.Add(new RezervasyonKonaklayanSegmentAtama
                    {
                        RezervasyonKonaklayanId = konaklayanId,
                        RezervasyonSegmentId = yeniSegment.Id,
                        OdaId = odaId,
                        YatakNo = null
                    });
                }

                continue;
            }

            var doluYataklar = await (
                    from atama in _stysDbContext.RezervasyonKonaklayanSegmentAtamalari
                    join segment in _stysDbContext.RezervasyonSegmentleri on atama.RezervasyonSegmentId equals segment.Id
                    join konaklayan in _stysDbContext.RezervasyonKonaklayanlar on atama.RezervasyonKonaklayanId equals konaklayan.Id
                    where atama.OdaId == odaId
                          && atama.YatakNo.HasValue
                          && segment.BaslangicTarihi < yeniSegment.BitisTarihi
                          && segment.BitisTarihi > yeniSegment.BaslangicTarihi
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

            var oncekiYatakByKonaklayanId = await _stysDbContext.RezervasyonKonaklayanSegmentAtamalari
                .Where(x => x.RezervasyonSegmentId == oncekiSegmentId
                            && x.OdaId == odaId
                            && konaklayanIdsInOda.Contains(x.RezervasyonKonaklayanId))
                .ToDictionaryAsync(x => x.RezervasyonKonaklayanId, x => x.YatakNo, cancellationToken);

            var yeniAtamalar = new List<RezervasyonKonaklayanSegmentAtama>();

            foreach (var konaklayanId in konaklayanIdsInOda)
            {
                var atama = new RezervasyonKonaklayanSegmentAtama
                {
                    RezervasyonKonaklayanId = konaklayanId,
                    RezervasyonSegmentId = yeniSegment.Id,
                    OdaId = odaId
                };

                if (oncekiYatakByKonaklayanId.TryGetValue(konaklayanId, out var oncekiYatak)
                    && oncekiYatak.HasValue
                    && musaitYataklar.Remove(oncekiYatak.Value))
                {
                    atama.YatakNo = oncekiYatak.Value;
                }

                yeniAtamalar.Add(atama);
            }

            foreach (var atama in yeniAtamalar.Where(x => !x.YatakNo.HasValue))
            {
                var sonrakiYatak = musaitYataklar[0];
                musaitYataklar.RemoveAt(0);
                atama.YatakNo = sonrakiYatak;
            }

            _stysDbContext.RezervasyonKonaklayanSegmentAtamalari.AddRange(yeniAtamalar);
        }

        // Bir sonraki segmentin "onceki oda/yatak" sorgulari bu segmentin kayitlarini DB'den
        // GORMELIDIR (EF, henuz kaydedilmemis Added entity'leri LINQ-to-SQL sorgularina yansitmaz).
        await _stysDbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task AddExtensionKonaklamaHaklariAsync(
        Rezervasyon reservation,
        DateTime eskiCikisTarihi,
        DateTime yeniCikisTarihi,
        CancellationToken cancellationToken)
    {
        // KonaklamaBoyunca (bir kereye mahsus) haklarin HakTarihi'nin HER ZAMAN GirisTarihi olmasi
        // sayesinde, TAM araligi (girisTarihi -> yeniCikisTarihi) yeniden uretip yalnizca
        // eskiCikisTarihi'nden ITIBAREN OLAN haklari eklemek, hem bu haklari DOGAL olarak
        // mukerrerlemeyi ONLER hem de eski cikis gununun artik ARA GUN olmasinin Gunluk haklara
        // etkisini (CheckOutGunuGecerliMi artik yeni son geceye uygulanir) DOGRU sekilde yansitir.
        var tumHaklar = await BuildKonaklamaHaklariAsync(
            reservation.TesisId,
            reservation.KonaklamaTipiId!.Value,
            reservation.GirisTarihi,
            yeniCikisTarihi,
            cancellationToken);

        var yeniHaklar = tumHaklar
            .Where(x => x.HakTarihi.HasValue && x.HakTarihi.Value.Date >= eskiCikisTarihi.Date)
            .ToList();

        if (yeniHaklar.Count == 0)
        {
            return;
        }

        var mevcutAnahtarlar = await _stysDbContext.RezervasyonKonaklamaHaklari
            .Where(x => x.RezervasyonId == reservation.Id && x.AktifMi)
            .Select(x => new { x.HizmetKodu, x.HakTarihi })
            .ToListAsync(cancellationToken);
        var mevcutAnahtarSeti = mevcutAnahtarlar
            .Select(x => (x.HizmetKodu, x.HakTarihi))
            .ToHashSet();

        var eklenecekHaklar = new List<RezervasyonKonaklamaHakki>();
        foreach (var hak in yeniHaklar)
        {
            if (mevcutAnahtarSeti.Contains((hak.HizmetKodu, hak.HakTarihi)))
            {
                continue;
            }

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
