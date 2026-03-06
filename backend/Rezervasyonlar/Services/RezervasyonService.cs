using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using STYS.AccessScope;
using STYS.Fiyatlandirma.Dto;
using STYS.Fiyatlandirma.Entities;
using STYS.Fiyatlandirma;
using STYS.Infrastructure.EntityFramework;
using STYS.OdaTipleri.Entities;
using STYS.Rezervasyonlar.Dto;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Rezervasyonlar.Services;

public class RezervasyonService : IRezervasyonService
{
    private readonly StysAppDbContext _stysDbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public RezervasyonService(
        StysAppDbContext stysDbContext,
        IUserAccessScopeService userAccessScopeService)
    {
        _stysDbContext = stysDbContext;
        _userAccessScopeService = userAccessScopeService;
    }

    public async Task<List<RezervasyonTesisDto>> GetErisilebilirTesislerAsync(CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var query = _stysDbContext.Tesisler
            .Where(x => x.AktifMi);

        if (scope.IsScoped)
        {
            query = query.Where(x => scope.TesisIds.Contains(x.Id));
        }

        return await query
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .Select(x => new RezervasyonTesisDto
            {
                Id = x.Id,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<RezervasyonOdaTipiDto>> GetOdaTipleriByTesisAsync(int tesisId, CancellationToken cancellationToken = default)
    {
        if (tesisId <= 0)
        {
            return [];
        }

        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        return await _stysDbContext.OdaTipleri
            .Where(x => x.TesisId == tesisId && x.AktifMi)
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .Select(x => new RezervasyonOdaTipiDto
            {
                Id = x.Id,
                TesisId = x.TesisId,
                Ad = x.Ad,
                Kapasite = x.Kapasite
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<RezervasyonMisafirTipiDto>> GetMisafirTipleriAsync(CancellationToken cancellationToken = default)
    {
        return await _stysDbContext.MisafirTipleri
            .Where(x => x.AktifMi)
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .Select(x => new RezervasyonMisafirTipiDto
            {
                Id = x.Id,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<RezervasyonKonaklamaTipiDto>> GetKonaklamaTipleriAsync(CancellationToken cancellationToken = default)
    {
        return await _stysDbContext.KonaklamaTipleri
            .Where(x => x.AktifMi)
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .Select(x => new RezervasyonKonaklamaTipiDto
            {
                Id = x.Id,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<RezervasyonIndirimKuraliSecenekDto>> GetUygulanabilirIndirimKurallariAsync(
        int tesisId,
        int misafirTipiId,
        int konaklamaTipiId,
        DateTime baslangicTarihi,
        DateTime bitisTarihi,
        CancellationToken cancellationToken = default)
    {
        if (misafirTipiId <= 0 || konaklamaTipiId <= 0)
        {
            return [];
        }

        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);
        if (baslangicTarihi >= bitisTarihi)
        {
            throw new BaseException("Baslangic tarihi bitis tarihinden kucuk olmalidir.", 400);
        }

        var rules = await QueryApplicableDiscountRulesAsync(
            tesisId,
            misafirTipiId,
            konaklamaTipiId,
            baslangicTarihi,
            bitisTarihi,
            cancellationToken);

        return rules
            .Select(x => new RezervasyonIndirimKuraliSecenekDto
            {
                Id = x.Id,
                Kod = x.Kod,
                Ad = x.Ad,
                IndirimTipi = x.IndirimTipi,
                Deger = x.Deger,
                KapsamTipi = x.KapsamTipi,
                Oncelik = x.Oncelik,
                BirlesebilirMi = x.BirlesebilirMi
            })
            .ToList();
    }

    public async Task<List<RezervasyonListeDto>> GetRezervasyonlarAsync(int? tesisId, CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);

        if (tesisId.HasValue && tesisId.Value > 0)
        {
            await EnsureCanAccessTesisAsync(tesisId.Value, cancellationToken);
        }

        var query = _stysDbContext.Rezervasyonlar.AsQueryable();

        if (scope.IsScoped)
        {
            query = query.Where(x => scope.TesisIds.Contains(x.TesisId));
        }

        if (tesisId.HasValue && tesisId.Value > 0)
        {
            query = query.Where(x => x.TesisId == tesisId.Value);
        }

        return await query
            .OrderByDescending(x => x.GirisTarihi)
            .ThenByDescending(x => x.Id)
            .Select(x => new RezervasyonListeDto
            {
                Id = x.Id,
                ReferansNo = x.ReferansNo,
                TesisId = x.TesisId,
                MisafirAdiSoyadi = x.MisafirAdiSoyadi,
                MisafirTelefon = x.MisafirTelefon,
                MisafirEposta = x.MisafirEposta,
                TcKimlikNo = x.TcKimlikNo,
                PasaportNo = x.PasaportNo,
                KisiSayisi = x.KisiSayisi,
                GirisTarihi = x.GirisTarihi,
                CikisTarihi = x.CikisTarihi,
                ToplamUcret = x.ToplamUcret,
                ParaBirimi = x.ParaBirimi,
                RezervasyonDurumu = x.RezervasyonDurumu
            })
            .Take(200)
            .ToListAsync(cancellationToken);
    }

    public async Task<RezervasyonDetayDto?> GetRezervasyonDetayAsync(int rezervasyonId, CancellationToken cancellationToken = default)
    {
        if (rezervasyonId <= 0)
        {
            throw new BaseException("Gecersiz rezervasyon id.", 400);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var query = _stysDbContext.Rezervasyonlar
            .Where(x => x.Id == rezervasyonId);

        if (scope.IsScoped)
        {
            query = query.Where(x => scope.TesisIds.Contains(x.TesisId));
        }

        var raw = await query
            .Select(x => new
            {
                x.Id,
                x.ReferansNo,
                x.TesisId,
                x.RezervasyonDurumu,
                x.KisiSayisi,
                x.GirisTarihi,
                x.CikisTarihi,
                x.ToplamBazUcret,
                x.ToplamUcret,
                x.ParaBirimi,
                x.UygulananIndirimlerJson,
                Segmentler = x.Segmentler
                    .OrderBy(s => s.SegmentSirasi)
                    .ThenBy(s => s.Id)
                    .Select(s => new RezervasyonDetaySegmentDto
                    {
                        SegmentSirasi = s.SegmentSirasi,
                        BaslangicTarihi = s.BaslangicTarihi,
                        BitisTarihi = s.BitisTarihi,
                        OdaAtamalari = s.OdaAtamalari
                            .OrderBy(a => a.OdaId)
                            .ThenBy(a => a.Id)
                            .Select(a => new RezervasyonDetayOdaAtamaDto
                            {
                                OdaId = a.OdaId,
                                OdaNo = a.OdaNoSnapshot,
                                BinaAdi = a.BinaAdiSnapshot,
                                OdaTipiAdi = a.OdaTipiAdiSnapshot,
                                AyrilanKisiSayisi = a.AyrilanKisiSayisi,
                                Kapasite = a.KapasiteSnapshot,
                                PaylasimliMi = a.PaylasimliMiSnapshot
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (raw is null)
        {
            return null;
        }

        return new RezervasyonDetayDto
        {
            Id = raw.Id,
            ReferansNo = raw.ReferansNo,
            TesisId = raw.TesisId,
            RezervasyonDurumu = raw.RezervasyonDurumu,
            KisiSayisi = raw.KisiSayisi,
            GirisTarihi = raw.GirisTarihi,
            CikisTarihi = raw.CikisTarihi,
            ToplamBazUcret = raw.ToplamBazUcret,
            ToplamUcret = raw.ToplamUcret,
            ParaBirimi = raw.ParaBirimi,
            UygulananIndirimler = DeserializeAppliedDiscounts(raw.UygulananIndirimlerJson),
            Segmentler = raw.Segmentler
        };
    }

    public async Task<List<UygunOdaDto>> GetUygunOdalarAsync(UygunOdaAramaRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        await EnsureCanAccessTesisAsync(request.TesisId, cancellationToken);

        if (request.OdaTipiId.HasValue && request.OdaTipiId.Value > 0)
        {
            var odaTipi = await _stysDbContext.OdaTipleri
                .Where(x => x.Id == request.OdaTipiId.Value && x.AktifMi)
                .Select(x => new { x.Id, x.TesisId })
                .FirstOrDefaultAsync(cancellationToken);
            if (odaTipi is null || odaTipi.TesisId != request.TesisId)
            {
                throw new BaseException("Secilen oda tipi, tesis ile uyumlu degil.", 400);
            }
        }

        var baslangic = request.BaslangicTarihi;
        var bitis = request.BitisTarihi;

        var occupancyByRoom = await GetCurrentOccupancyByRoomAsync(
            (await _stysDbContext.Odalar
                .Where(x => x.AktifMi)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken)),
            baslangic,
            bitis,
            cancellationToken);

        var uygunOdalarQuery =
            from oda in _stysDbContext.Odalar
            join bina in _stysDbContext.Binalar on oda.BinaId equals bina.Id
            join odaTipi in _stysDbContext.OdaTipleri on oda.TesisOdaTipiId equals odaTipi.Id
            where oda.AktifMi
                  && bina.AktifMi
                  && odaTipi.AktifMi
                  && bina.TesisId == request.TesisId
                  && odaTipi.Kapasite >= request.KisiSayisi
                  && (!request.OdaTipiId.HasValue || request.OdaTipiId.Value <= 0 || oda.TesisOdaTipiId == request.OdaTipiId.Value)
            select new UygunOdaDto
            {
                OdaId = oda.Id,
                OdaNo = oda.OdaNo,
                BinaId = bina.Id,
                BinaAdi = bina.Ad,
                OdaTipiId = odaTipi.Id,
                OdaTipiAdi = odaTipi.Ad,
                Kapasite = odaTipi.Kapasite,
                PaylasimliMi = odaTipi.PaylasimliMi
            };

        var suitableRooms = await uygunOdalarQuery
            .OrderBy(x => x.BinaAdi)
            .ThenBy(x => x.OdaNo)
            .ThenBy(x => x.OdaId)
            .ToListAsync(cancellationToken);

        return suitableRooms.Where(room =>
        {
            var occupied = occupancyByRoom.TryGetValue(room.OdaId, out var value) ? value : 0;
            if (!room.PaylasimliMi)
            {
                return occupied == 0;
            }

            return room.Kapasite - occupied >= request.KisiSayisi;
        }).ToList();
    }

    public async Task<List<KonaklamaSenaryoDto>> GetKonaklamaSenaryolariAsync(KonaklamaSenaryoAramaRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateScenarioRequest(request);
        await EnsureCanAccessTesisAsync(request.TesisId, cancellationToken);

        var scenarios = new List<KonaklamaSenaryoDto>();

        var fullIntervalAvailabilities = await GetRoomAvailabilitiesAsync(
            request.TesisId,
            request.OdaTipiId,
            request.KisiSayisi,
            request.BaslangicTarihi,
            request.BitisTarihi,
            cancellationToken);

        var fullIntervalVariants = BuildSingleSegmentVariants(
            request.KisiSayisi,
            request.BaslangicTarihi,
            request.BitisTarihi,
            fullIntervalAvailabilities);

        scenarios.AddRange(fullIntervalVariants);

        if (request.BitisTarihi - request.BaslangicTarihi > TimeSpan.FromHours(6))
        {
            var segmentedScenario = await BuildTwoSegmentScenarioAsync(request, cancellationToken);
            if (segmentedScenario is not null)
            {
                scenarios.Add(segmentedScenario);
            }
        }

        var distinct = scenarios
            .GroupBy(CreateScenarioKey)
            .Select(group => group.First())
            .ToList();

        foreach (var scenario in distinct)
        {
            var pricing = await CalculateScenarioPriceAsync(
                request.TesisId,
                request.MisafirTipiId,
                request.KonaklamaTipiId,
                request.BaslangicTarihi,
                request.BitisTarihi,
                scenario.Segmentler.Select(x => new SenaryoFiyatHesaplaSegmentDto
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

            scenario.ToplamBazUcret = pricing.ToplamBazUcret;
            scenario.ToplamNihaiUcret = pricing.ToplamNihaiUcret;
            scenario.ParaBirimi = pricing.ParaBirimi;
        }

        var sortedByPrice = distinct
            .OrderBy(x => x.ToplamNihaiUcret)
            .ThenBy(x => x.ToplamBazUcret)
            .ThenBy(x => x.ToplamOdaSayisi)
            .ThenBy(x => x.OdaDegisimSayisi)
            .Take(5)
            .ToList();

        for (var i = 0; i < sortedByPrice.Count; i++)
        {
            sortedByPrice[i].SenaryoKodu = $"SENARYO-{i + 1}";
        }

        return sortedByPrice;
    }

    public Task<SenaryoFiyatHesaplamaSonucuDto> HesaplaSenaryoFiyatiAsync(SenaryoFiyatHesaplaRequestDto request, CancellationToken cancellationToken = default)
    {
        return CalculateScenarioPriceAsync(
            request.TesisId,
            request.MisafirTipiId,
            request.KonaklamaTipiId,
            request.BaslangicTarihi,
            request.BitisTarihi,
            request.Segmentler,
            request.SeciliIndirimKuraliIds,
            cancellationToken);
    }

    public async Task<RezervasyonKayitSonucDto> KaydetAsync(RezervasyonKaydetRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateSaveRequest(request);
        await EnsureCanAccessTesisAsync(request.TesisId, cancellationToken);

        var distinctRoomIds = request.Segmentler
            .SelectMany(x => x.OdaAtamalari)
            .Select(x => x.OdaId)
            .Distinct()
            .ToList();

        var rooms = await (
            from oda in _stysDbContext.Odalar
            join bina in _stysDbContext.Binalar on oda.BinaId equals bina.Id
            join odaTipi in _stysDbContext.OdaTipleri on oda.TesisOdaTipiId equals odaTipi.Id
            where distinctRoomIds.Contains(oda.Id)
                  && oda.AktifMi
                  && bina.AktifMi
                  && odaTipi.AktifMi
            select new RoomInfo(
                oda.Id,
                oda.OdaNo,
                bina.TesisId,
                bina.Ad,
                odaTipi.Ad,
                odaTipi.Kapasite,
                odaTipi.PaylasimliMi))
            .ToListAsync(cancellationToken);

        if (rooms.Count != distinctRoomIds.Count || rooms.Any(x => x.TesisId != request.TesisId))
        {
            throw new BaseException("Secilen odalardan en az biri gecersiz veya secilen tesise ait degil.", 400);
        }

        foreach (var segment in request.Segmentler)
        {
            var occupancyByRoom = await GetCurrentOccupancyByRoomAsync(
                segment.OdaAtamalari.Select(x => x.OdaId).Distinct().ToList(),
                segment.BaslangicTarihi,
                segment.BitisTarihi,
                cancellationToken);

            var assignedByRoom = segment.OdaAtamalari
                .GroupBy(x => x.OdaId)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.AyrilanKisiSayisi));

            foreach (var roomAssignment in assignedByRoom)
            {
                var room = rooms.First(x => x.OdaId == roomAssignment.Key);
                var occupied = occupancyByRoom.TryGetValue(room.OdaId, out var value) ? value : 0;

                var remainingCapacity = room.PaylasimliMi
                    ? Math.Max(0, room.Kapasite - occupied)
                    : occupied > 0
                        ? 0
                        : room.Kapasite;

                if (roomAssignment.Value > remainingCapacity)
                {
                    throw new BaseException($"'{room.OdaNo}' odasi icin secilen aralikta yeterli kapasite yok.", 400);
                }
            }
        }

        var reservation = new Entities.Rezervasyon
        {
            ReferansNo = GenerateReferenceNo(),
            TesisId = request.TesisId,
            KisiSayisi = request.KisiSayisi,
            GirisTarihi = request.GirisTarihi,
            CikisTarihi = request.CikisTarihi,
            MisafirAdiSoyadi = request.MisafirAdiSoyadi.Trim(),
            MisafirTelefon = request.MisafirTelefon.Trim(),
            MisafirEposta = string.IsNullOrWhiteSpace(request.MisafirEposta) ? null : request.MisafirEposta.Trim(),
            TcKimlikNo = string.IsNullOrWhiteSpace(request.TcKimlikNo) ? null : request.TcKimlikNo.Trim(),
            PasaportNo = string.IsNullOrWhiteSpace(request.PasaportNo) ? null : request.PasaportNo.Trim(),
            Notlar = string.IsNullOrWhiteSpace(request.Notlar) ? null : request.Notlar.Trim(),
            ToplamBazUcret = request.ToplamBazUcret > 0 ? request.ToplamBazUcret : request.ToplamUcret,
            ToplamUcret = request.ToplamUcret,
            ParaBirimi = string.IsNullOrWhiteSpace(request.ParaBirimi) ? "TRY" : request.ParaBirimi.Trim().ToUpperInvariant(),
            UygulananIndirimlerJson = SerializeAppliedDiscounts(request.UygulananIndirimler),
            RezervasyonDurumu = RezervasyonDurumlari.Taslak,
            AktifMi = true
        };

        var orderedSegments = request.Segmentler
            .OrderBy(x => x.BaslangicTarihi)
            .ThenBy(x => x.BitisTarihi)
            .ToList();

        for (var i = 0; i < orderedSegments.Count; i++)
        {
            var segmentRequest = orderedSegments[i];
            var segment = new Entities.RezervasyonSegment
            {
                SegmentSirasi = i + 1,
                BaslangicTarihi = segmentRequest.BaslangicTarihi,
                BitisTarihi = segmentRequest.BitisTarihi
            };

            foreach (var odaAtamaRequest in segmentRequest.OdaAtamalari)
            {
                var room = rooms.First(x => x.OdaId == odaAtamaRequest.OdaId);
                segment.OdaAtamalari.Add(new Entities.RezervasyonSegmentOdaAtama
                {
                    OdaId = room.OdaId,
                    AyrilanKisiSayisi = odaAtamaRequest.AyrilanKisiSayisi,
                    OdaNoSnapshot = room.OdaNo,
                    BinaAdiSnapshot = room.BinaAdi,
                    OdaTipiAdiSnapshot = room.OdaTipiAdi,
                    PaylasimliMiSnapshot = room.PaylasimliMi,
                    KapasiteSnapshot = room.Kapasite
                });
            }

            reservation.Segmentler.Add(segment);
        }

        await _stysDbContext.Rezervasyonlar.AddAsync(reservation, cancellationToken);
        await _stysDbContext.SaveChangesAsync(cancellationToken);

        return new RezervasyonKayitSonucDto
        {
            Id = reservation.Id,
            ReferansNo = reservation.ReferansNo,
            RezervasyonDurumu = reservation.RezervasyonDurumu
        };
    }

    private async Task<SenaryoFiyatHesaplamaSonucuDto> CalculateScenarioPriceAsync(
        int tesisId,
        int misafirTipiId,
        int konaklamaTipiId,
        DateTime baslangicTarihi,
        DateTime bitisTarihi,
        IReadOnlyCollection<SenaryoFiyatHesaplaSegmentDto> segmentler,
        IReadOnlyCollection<int> seciliIndirimKuraliIds,
        CancellationToken cancellationToken)
    {
        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);
        ValidatePricingRequest(tesisId, misafirTipiId, konaklamaTipiId, baslangicTarihi, bitisTarihi, segmentler);

        var roomIds = segmentler.SelectMany(x => x.OdaAtamalari).Select(x => x.OdaId).Distinct().ToList();
        var roomMaps = await (
            from oda in _stysDbContext.Odalar
            join bina in _stysDbContext.Binalar on oda.BinaId equals bina.Id
            join odaTipi in _stysDbContext.OdaTipleri on oda.TesisOdaTipiId equals odaTipi.Id
            where roomIds.Contains(oda.Id)
                  && oda.AktifMi
                  && bina.AktifMi
                  && odaTipi.AktifMi
            select new
            {
                OdaId = oda.Id,
                bina.TesisId,
                OdaTipiId = odaTipi.Id
            })
            .ToListAsync(cancellationToken);

        if (roomMaps.Count != roomIds.Count || roomMaps.Any(x => x.TesisId != tesisId))
        {
            throw new BaseException("Senaryodaki odalardan en az biri gecersiz veya tesis kapsaminda degil.", 400);
        }

        var roomTypeIds = roomMaps.Select(x => x.OdaTipiId).Distinct().ToList();
        var roomTypeByRoomId = roomMaps.ToDictionary(x => x.OdaId, x => x.OdaTipiId);
        var tesisSaatleri = await _stysDbContext.Tesisler
            .Where(x => x.Id == tesisId)
            .Select(x => new { x.GirisSaati, x.CikisSaati })
            .FirstOrDefaultAsync(cancellationToken);

        if (tesisSaatleri is null)
        {
            throw new BaseException("Tesis bulunamadi.", 404);
        }

        var minDate = segmentler.Min(x => x.BaslangicTarihi).Date;
        var maxDate = segmentler.Max(x => x.BitisTarihi).Date;
        var fiyatKayitlari = await _stysDbContext.OdaFiyatlari
            .Where(x =>
                roomTypeIds.Contains(x.TesisOdaTipiId)
                && x.KonaklamaTipiId == konaklamaTipiId
                && x.MisafirTipiId == misafirTipiId
                && x.KisiSayisi == 1
                && x.AktifMi
                && x.BaslangicTarihi <= maxDate
                && x.BitisTarihi >= minDate)
            .OrderByDescending(x => x.BaslangicTarihi)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        var currency = string.Empty;
        var baseTotal = 0m;

        foreach (var chargeWindow in EnumerateChargeWindows(
                     baslangicTarihi,
                     bitisTarihi,
                     tesisSaatleri.GirisSaati,
                     tesisSaatleri.CikisSaati))
        {
            var aktifSegment = segmentler
                .Where(x => x.BaslangicTarihi <= chargeWindow.WindowStart && x.BitisTarihi > chargeWindow.WindowStart)
                .OrderByDescending(x => x.BaslangicTarihi)
                .FirstOrDefault();

            if (aktifSegment is null)
            {
                throw new BaseException($"Senaryo icin {chargeWindow.ChargeDay:yyyy-MM-dd} tarihinde aktif segment bulunamadi.", 400);
            }

            foreach (var atama in aktifSegment.OdaAtamalari)
            {
                var odaTipiId = roomTypeByRoomId[atama.OdaId];
                var fiyat = fiyatKayitlari.FirstOrDefault(x =>
                    x.TesisOdaTipiId == odaTipiId
                    && x.BaslangicTarihi.Date <= chargeWindow.ChargeDay
                    && x.BitisTarihi.Date >= chargeWindow.ChargeDay);

                if (fiyat is null)
                {
                    throw new BaseException($"Senaryo icin {chargeWindow.ChargeDay:yyyy-MM-dd} tarihinde uygun oda fiyati bulunamadi.", 400);
                }

                if (string.IsNullOrWhiteSpace(currency))
                {
                    currency = fiyat.ParaBirimi;
                }
                else if (!currency.Equals(fiyat.ParaBirimi, StringComparison.OrdinalIgnoreCase))
                {
                    throw new BaseException("Senaryo fiyatlari birden fazla para birimi iceriyor.", 400);
                }

                baseTotal += fiyat.Fiyat * atama.AyrilanKisiSayisi;
            }
        }

        var finalTotal = baseTotal;
        var appliedDiscounts = new List<UygulananIndirimDto>();
        if (seciliIndirimKuraliIds.Count > 0)
        {
            var selectedSet = seciliIndirimKuraliIds
                .Where(x => x > 0)
                .Distinct()
                .ToHashSet();

            if (selectedSet.Count > 0)
            {
                var candidateRules = await QueryApplicableDiscountRulesAsync(
                    tesisId,
                    misafirTipiId,
                    konaklamaTipiId,
                    baslangicTarihi,
                    bitisTarihi,
                    cancellationToken);

                var selectedRules = candidateRules
                    .Where(x => selectedSet.Contains(x.Id))
                    .ToList();

                foreach (var rule in selectedRules)
                {
                    var discountAmount = CalculateDiscountAmount(rule, finalTotal);
                    if (discountAmount <= 0)
                    {
                        continue;
                    }

                    finalTotal -= discountAmount;
                    appliedDiscounts.Add(new UygulananIndirimDto
                    {
                        IndirimKuraliId = rule.Id,
                        KuralAdi = rule.Ad,
                        IndirimTutari = discountAmount,
                        SonrasiTutar = finalTotal
                    });

                    if (!rule.BirlesebilirMi)
                    {
                        break;
                    }
                }
            }
        }

        return new SenaryoFiyatHesaplamaSonucuDto
        {
            ToplamBazUcret = baseTotal,
            ToplamNihaiUcret = finalTotal,
            ParaBirimi = string.IsNullOrWhiteSpace(currency) ? "TRY" : currency.ToUpperInvariant(),
            UygulananIndirimler = appliedDiscounts
        };
    }

    private async Task<List<IndirimKurali>> QueryApplicableDiscountRulesAsync(
        int tesisId,
        int misafirTipiId,
        int konaklamaTipiId,
        DateTime baslangicTarihi,
        DateTime bitisTarihi,
        CancellationToken cancellationToken)
    {
        var rules = await _stysDbContext.IndirimKurallari
            .Where(x =>
                x.AktifMi
                && x.BaslangicTarihi <= bitisTarihi
                && x.BitisTarihi >= baslangicTarihi
                && (x.KapsamTipi == IndirimKapsamTipleri.Sistem
                    || (x.KapsamTipi == IndirimKapsamTipleri.Tesis && x.TesisId == tesisId)))
            .Include(x => x.MisafirTipiKisitlari)
            .Include(x => x.KonaklamaTipiKisitlari)
            .OrderBy(x => x.KapsamTipi == IndirimKapsamTipleri.Tesis ? 0 : 1)
            .ThenByDescending(x => x.Oncelik)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return rules
            .Where(x => IsDiscountRuleApplicable(x, misafirTipiId, konaklamaTipiId))
            .ToList();
    }

    private static bool IsDiscountRuleApplicable(IndirimKurali rule, int misafirTipiId, int konaklamaTipiId)
    {
        if (rule.KonaklamaTipiKisitlari.Count > 0 && !rule.KonaklamaTipiKisitlari.Any(x => x.KonaklamaTipiId == konaklamaTipiId))
        {
            return false;
        }

        if (rule.MisafirTipiKisitlari.Count > 0 && !rule.MisafirTipiKisitlari.Any(x => x.MisafirTipiId == misafirTipiId))
        {
            return false;
        }

        return true;
    }

    private static decimal CalculateDiscountAmount(IndirimKurali rule, decimal currentAmount)
    {
        if (currentAmount <= 0 || rule.Deger <= 0)
        {
            return 0;
        }

        var discount = rule.IndirimTipi.Equals(IndirimTipleri.Yuzde, StringComparison.OrdinalIgnoreCase)
            ? Math.Round(currentAmount * rule.Deger / 100m, 2, MidpointRounding.AwayFromZero)
            : rule.Deger;

        return Math.Min(currentAmount, Math.Max(0, discount));
    }

    private static IEnumerable<(DateTime ChargeDay, DateTime WindowStart)> EnumerateChargeWindows(
        DateTime baslangic,
        DateTime bitis,
        TimeSpan girisSaati,
        TimeSpan cikisSaati)
    {
        if (bitis <= baslangic)
        {
            yield break;
        }

        var startDate = baslangic.Date;
        var firstWindowStart = startDate.Add(girisSaati);
        var firstWindowEnd = startDate.AddDays(1).Add(cikisSaati);

        // Ilk gun: giris saati -> ertesi gun cikis saati
        if (bitis > firstWindowStart && baslangic < firstWindowEnd)
        {
            yield return (startDate, firstWindowStart);
        }

        // Sonraki gunler: cikis saati -> ertesi gun cikis saati
        for (var windowStart = firstWindowEnd; windowStart < bitis; windowStart = windowStart.AddDays(1))
        {
            var windowEnd = windowStart.AddDays(1);
            if (bitis > windowStart && baslangic < windowEnd)
            {
                yield return (windowStart.Date, windowStart);
            }
        }
    }

    private static void ValidatePricingRequest(
        int tesisId,
        int misafirTipiId,
        int konaklamaTipiId,
        DateTime baslangicTarihi,
        DateTime bitisTarihi,
        IReadOnlyCollection<SenaryoFiyatHesaplaSegmentDto> segmentler)
    {
        if (tesisId <= 0)
        {
            throw new BaseException("Tesis secimi zorunludur.", 400);
        }

        if (misafirTipiId <= 0)
        {
            throw new BaseException("Misafir tipi secimi zorunludur.", 400);
        }

        if (konaklamaTipiId <= 0)
        {
            throw new BaseException("Konaklama tipi secimi zorunludur.", 400);
        }

        if (baslangicTarihi >= bitisTarihi)
        {
            throw new BaseException("Baslangic tarihi bitis tarihinden kucuk olmalidir.", 400);
        }

        if (segmentler.Count == 0)
        {
            throw new BaseException("En az bir senaryo segmenti gereklidir.", 400);
        }

        foreach (var segment in segmentler)
        {
            if (segment.BaslangicTarihi >= segment.BitisTarihi)
            {
                throw new BaseException("Segment baslangic tarihi bitis tarihinden kucuk olmalidir.", 400);
            }

            if (segment.BaslangicTarihi < baslangicTarihi || segment.BitisTarihi > bitisTarihi)
            {
                throw new BaseException("Segment araligi rezervasyon araligi disina cikamaz.", 400);
            }

            if (segment.OdaAtamalari.Count == 0 || segment.OdaAtamalari.Any(x => x.OdaId <= 0 || x.AyrilanKisiSayisi <= 0))
            {
                throw new BaseException("Segment oda atamalari gecersiz.", 400);
            }
        }
    }

    private static void ValidateRequest(UygunOdaAramaRequestDto request)
    {
        if (request.TesisId <= 0)
        {
            throw new BaseException("Tesis secimi zorunludur.", 400);
        }

        if (request.KisiSayisi <= 0)
        {
            throw new BaseException("Kisi sayisi sifirdan buyuk olmalidir.", 400);
        }

        var baslangic = request.BaslangicTarihi;
        var bitis = request.BitisTarihi;
        if (baslangic >= bitis)
        {
            throw new BaseException("Baslangic tarihi bitis tarihinden kucuk olmalidir.", 400);
        }
    }

    private static void ValidateScenarioRequest(KonaklamaSenaryoAramaRequestDto request)
    {
        if (request.TesisId <= 0)
        {
            throw new BaseException("Tesis secimi zorunludur.", 400);
        }

        if (request.MisafirTipiId <= 0)
        {
            throw new BaseException("Misafir tipi secimi zorunludur.", 400);
        }

        if (request.KonaklamaTipiId <= 0)
        {
            throw new BaseException("Konaklama tipi secimi zorunludur.", 400);
        }

        if (request.KisiSayisi <= 0)
        {
            throw new BaseException("Kisi sayisi sifirdan buyuk olmalidir.", 400);
        }

        if (request.BaslangicTarihi >= request.BitisTarihi)
        {
            throw new BaseException("Baslangic tarihi bitis tarihinden kucuk olmalidir.", 400);
        }
    }

    private static void ValidateSaveRequest(RezervasyonKaydetRequestDto request)
    {
        if (request.TesisId <= 0)
        {
            throw new BaseException("Tesis secimi zorunludur.", 400);
        }

        if (request.KisiSayisi <= 0)
        {
            throw new BaseException("Kisi sayisi sifirdan buyuk olmalidir.", 400);
        }

        if (request.GirisTarihi >= request.CikisTarihi)
        {
            throw new BaseException("Giris tarihi cikis tarihinden kucuk olmalidir.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.MisafirAdiSoyadi))
        {
            throw new BaseException("Misafir adi soyadi zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.MisafirTelefon))
        {
            throw new BaseException("Misafir telefon zorunludur.", 400);
        }

        if (request.Segmentler.Count == 0)
        {
            throw new BaseException("En az bir rezervasyon segmenti gereklidir.", 400);
        }

        if (request.ToplamUcret < 0)
        {
            throw new BaseException("Toplam ucret sifirdan kucuk olamaz.", 400);
        }

        if (request.ToplamBazUcret < 0)
        {
            throw new BaseException("Toplam baz ucret sifirdan kucuk olamaz.", 400);
        }

        if (request.ToplamBazUcret > 0 && request.ToplamUcret > request.ToplamBazUcret)
        {
            throw new BaseException("Toplam ucret, baz ucretten buyuk olamaz.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.ParaBirimi) || request.ParaBirimi.Trim().Length != 3)
        {
            throw new BaseException("Para birimi 3 karakter olmali (ornek: TRY).", 400);
        }

        if (request.UygulananIndirimler.Any(x =>
                x.IndirimKuraliId <= 0
                || string.IsNullOrWhiteSpace(x.KuralAdi)
                || x.IndirimTutari < 0
                || x.SonrasiTutar < 0))
        {
            throw new BaseException("Uygulanan indirim kayitlari gecersiz.", 400);
        }

        var orderedSegments = request.Segmentler
            .OrderBy(x => x.BaslangicTarihi)
            .ThenBy(x => x.BitisTarihi)
            .ToList();

        DateTime? previousEnd = null;
        foreach (var segment in orderedSegments)
        {
            if (segment.BaslangicTarihi >= segment.BitisTarihi)
            {
                throw new BaseException("Segment baslangic tarihi segment bitis tarihinden kucuk olmalidir.", 400);
            }

            if (segment.BaslangicTarihi < request.GirisTarihi || segment.BitisTarihi > request.CikisTarihi)
            {
                throw new BaseException("Segment araliklari rezervasyon araligi disina cikamaz.", 400);
            }

            if (previousEnd.HasValue && segment.BaslangicTarihi < previousEnd.Value)
            {
                throw new BaseException("Segment araliklari birbiriyle cakisamaz.", 400);
            }

            if (segment.OdaAtamalari.Count == 0)
            {
                throw new BaseException("Her segmentte en az bir oda atamasi olmalidir.", 400);
            }

            var peopleSum = segment.OdaAtamalari.Sum(x => x.AyrilanKisiSayisi);
            if (peopleSum != request.KisiSayisi)
            {
                throw new BaseException("Her segmentte ayrilan toplam kisi sayisi rezervasyon kisi sayisina esit olmalidir.", 400);
            }

            previousEnd = segment.BitisTarihi;
        }
    }

    private async Task<Dictionary<int, int>> GetCurrentOccupancyByRoomAsync(
        IReadOnlyCollection<int> roomIds,
        DateTime baslangic,
        DateTime bitis,
        CancellationToken cancellationToken)
    {
        if (roomIds.Count == 0)
        {
            return [];
        }

        var overlaps = await (
            from atama in _stysDbContext.RezervasyonSegmentOdaAtamalari
            join segment in _stysDbContext.RezervasyonSegmentleri on atama.RezervasyonSegmentId equals segment.Id
            join rezervasyon in _stysDbContext.Rezervasyonlar on segment.RezervasyonId equals rezervasyon.Id
            where rezervasyon.AktifMi
                  && rezervasyon.RezervasyonDurumu != RezervasyonDurumlari.Iptal
                  && roomIds.Contains(atama.OdaId)
                  && segment.BaslangicTarihi < bitis
                  && segment.BitisTarihi > baslangic
            select new
            {
                atama.OdaId,
                atama.AyrilanKisiSayisi
            })
            .ToListAsync(cancellationToken);

        return overlaps
            .GroupBy(x => x.OdaId)
            .ToDictionary(group => group.Key, group => group.Sum(x => x.AyrilanKisiSayisi));
    }

    private static string GenerateReferenceNo()
    {
        return $"RZV-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    }

    private static string SerializeAppliedDiscounts(IReadOnlyCollection<UygulananIndirimDto> discounts)
    {
        if (discounts.Count == 0)
        {
            return "[]";
        }

        return JsonSerializer.Serialize(discounts);
    }

    private static List<UygulananIndirimDto> DeserializeAppliedDiscounts(string? discountsJson)
    {
        if (string.IsNullOrWhiteSpace(discountsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<UygulananIndirimDto>>(discountsJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task<List<RoomAvailability>> GetRoomAvailabilitiesAsync(
        int tesisId,
        int? odaTipiId,
        int kisiSayisi,
        DateTime baslangic,
        DateTime bitis,
        CancellationToken cancellationToken)
    {
        if (odaTipiId.HasValue && odaTipiId.Value > 0)
        {
            var odaTipi = await _stysDbContext.OdaTipleri
                .Where(x => x.Id == odaTipiId.Value && x.AktifMi)
                .Select(x => new { x.Id, x.TesisId })
                .FirstOrDefaultAsync(cancellationToken);

            if (odaTipi is null || odaTipi.TesisId != tesisId)
            {
                throw new BaseException("Secilen oda tipi, tesis ile uyumlu degil.", 400);
            }
        }

        var candidateRooms = await (
            from oda in _stysDbContext.Odalar
            join bina in _stysDbContext.Binalar on oda.BinaId equals bina.Id
            join roomType in _stysDbContext.OdaTipleri on oda.TesisOdaTipiId equals roomType.Id
            where oda.AktifMi
                  && bina.AktifMi
                  && roomType.AktifMi
                  && bina.TesisId == tesisId
                  && roomType.Kapasite >= 1
                  && (!odaTipiId.HasValue || odaTipiId.Value <= 0 || roomType.Id == odaTipiId.Value)
            select new
            {
                OdaId = oda.Id,
                oda.OdaNo,
                BinaId = bina.Id,
                BinaAdi = bina.Ad,
                OdaTipiId = roomType.Id,
                OdaTipiAdi = roomType.Ad,
                roomType.Kapasite,
                roomType.PaylasimliMi
            })
            .ToListAsync(cancellationToken);

        if (candidateRooms.Count == 0)
        {
            return [];
        }

        var occupancyByRoom = await GetCurrentOccupancyByRoomAsync(
            candidateRooms.Select(x => x.OdaId).ToList(),
            baslangic,
            bitis,
            cancellationToken);

        var result = new List<RoomAvailability>();
        foreach (var room in candidateRooms)
        {
            var occupied = occupancyByRoom.TryGetValue(room.OdaId, out var value) ? value : 0;
            int remaining;

            if (room.PaylasimliMi)
            {
                remaining = Math.Max(0, room.Kapasite - occupied);
            }
            else
            {
                remaining = occupied > 0 ? 0 : room.Kapasite;
            }

            if (remaining <= 0)
            {
                continue;
            }

            result.Add(new RoomAvailability(
                room.OdaId,
                room.OdaNo,
                room.BinaId,
                room.BinaAdi,
                room.OdaTipiId,
                room.OdaTipiAdi,
                room.Kapasite,
                room.PaylasimliMi,
                remaining));
        }

        return result;
    }

    private static List<KonaklamaSenaryoDto> BuildSingleSegmentVariants(
        int kisiSayisi,
        DateTime baslangic,
        DateTime bitis,
        IReadOnlyCollection<RoomAvailability> availabilities)
    {
        var scenarios = new List<KonaklamaSenaryoDto>();
        if (availabilities.Count == 0)
        {
            return scenarios;
        }

        var variantSources = new List<(string Description, List<RoomAvailability> Rooms)>
        {
            ("Tek parca konaklama - minimum oda sayisi", availabilities.OrderByDescending(x => x.RemainingCapacity).ThenBy(x => x.OdaId).ToList()),
            ("Tek parca konaklama - az paylasimli tercih", availabilities.OrderBy(x => x.PaylasimliMi).ThenByDescending(x => x.RemainingCapacity).ThenBy(x => x.OdaId).ToList()),
            ("Tek parca konaklama - daginik alternatif", availabilities.OrderBy(x => x.RemainingCapacity).ThenBy(x => x.OdaId).ToList())
        };

        foreach (var variant in variantSources)
        {
            var allocations = AllocatePeople(variant.Rooms, kisiSayisi);
            if (allocations.Count == 0)
            {
                continue;
            }

            scenarios.Add(new KonaklamaSenaryoDto
            {
                SenaryoKodu = "SENARYO-X",
                Aciklama = variant.Description,
                ToplamOdaSayisi = allocations.Count,
                OdaDegisimSayisi = 0,
                Segmentler =
                [
                    new KonaklamaSenaryoSegmentDto
                    {
                        BaslangicTarihi = baslangic,
                        BitisTarihi = bitis,
                        OdaAtamalari = allocations
                    }
                ]
            });
        }

        return scenarios;
    }

    private async Task<KonaklamaSenaryoDto?> BuildTwoSegmentScenarioAsync(
        KonaklamaSenaryoAramaRequestDto request,
        CancellationToken cancellationToken)
    {
        var midpoint = request.BaslangicTarihi + TimeSpan.FromTicks((request.BitisTarihi - request.BaslangicTarihi).Ticks / 2);
        if (midpoint <= request.BaslangicTarihi || midpoint >= request.BitisTarihi)
        {
            return null;
        }

        var firstSegmentRooms = await GetRoomAvailabilitiesAsync(
            request.TesisId,
            request.OdaTipiId,
            request.KisiSayisi,
            request.BaslangicTarihi,
            midpoint,
            cancellationToken);
        var secondSegmentRooms = await GetRoomAvailabilitiesAsync(
            request.TesisId,
            request.OdaTipiId,
            request.KisiSayisi,
            midpoint,
            request.BitisTarihi,
            cancellationToken);

        var firstAllocations = AllocatePeople(firstSegmentRooms.OrderByDescending(x => x.RemainingCapacity).ThenBy(x => x.OdaId).ToList(), request.KisiSayisi);
        var secondAllocations = AllocatePeople(secondSegmentRooms.OrderByDescending(x => x.RemainingCapacity).ThenBy(x => x.OdaId).ToList(), request.KisiSayisi);
        if (firstAllocations.Count == 0 || secondAllocations.Count == 0)
        {
            return null;
        }

        var firstPattern = firstAllocations
            .OrderBy(x => x.OdaId)
            .ThenBy(x => x.AyrilanKisiSayisi)
            .Select(x => $"{x.OdaId}:{x.AyrilanKisiSayisi}")
            .ToArray();
        var secondPattern = secondAllocations
            .OrderBy(x => x.OdaId)
            .ThenBy(x => x.AyrilanKisiSayisi)
            .Select(x => $"{x.OdaId}:{x.AyrilanKisiSayisi}")
            .ToArray();

        // Segmentler arasi oda/dağılım aynıysa segmentli senaryo anlamlı değildir.
        if (firstPattern.SequenceEqual(secondPattern))
        {
            return null;
        }

        return new KonaklamaSenaryoDto
        {
            SenaryoKodu = "SENARYO-X",
            Aciklama = "Iki segmentli konaklama (oda degisimi olabilir)",
            ToplamOdaSayisi = firstAllocations.Select(x => x.OdaId).Union(secondAllocations.Select(x => x.OdaId)).Count(),
            OdaDegisimSayisi = 1,
            Segmentler =
            [
                new KonaklamaSenaryoSegmentDto
                {
                    BaslangicTarihi = request.BaslangicTarihi,
                    BitisTarihi = midpoint,
                    OdaAtamalari = firstAllocations
                },
                new KonaklamaSenaryoSegmentDto
                {
                    BaslangicTarihi = midpoint,
                    BitisTarihi = request.BitisTarihi,
                    OdaAtamalari = secondAllocations
                }
            ]
        };
    }

    private static List<KonaklamaSenaryoOdaAtamaDto> AllocatePeople(
        IReadOnlyList<RoomAvailability> rooms,
        int totalPeople)
    {
        var remainingPeople = totalPeople;
        var allocations = new List<KonaklamaSenaryoOdaAtamaDto>();

        foreach (var room in rooms)
        {
            if (remainingPeople <= 0)
            {
                break;
            }

            var assign = Math.Min(remainingPeople, room.RemainingCapacity);
            if (assign <= 0)
            {
                continue;
            }

            allocations.Add(new KonaklamaSenaryoOdaAtamaDto
            {
                OdaId = room.OdaId,
                OdaNo = room.OdaNo,
                BinaId = room.BinaId,
                BinaAdi = room.BinaAdi,
                OdaTipiId = room.OdaTipiId,
                OdaTipiAdi = room.OdaTipiAdi,
                Kapasite = room.Kapasite,
                PaylasimliMi = room.PaylasimliMi,
                AyrilanKisiSayisi = assign
            });
            remainingPeople -= assign;
        }

        return remainingPeople == 0 ? allocations : [];
    }

    private static string CreateScenarioKey(KonaklamaSenaryoDto scenario)
    {
        var segmentKeys = scenario.Segmentler
            .Select(segment =>
            {
                var assignments = segment.OdaAtamalari
                    .OrderBy(x => x.OdaId)
                    .ThenBy(x => x.AyrilanKisiSayisi)
                    .Select(x => $"{x.OdaId}:{x.AyrilanKisiSayisi}")
                    .ToArray();
                return $"{segment.BaslangicTarihi:O}-{segment.BitisTarihi:O}-[{string.Join(",", assignments)}]";
            });

        return string.Join("|", segmentKeys);
    }

    private async Task EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && !scope.TesisIds.Contains(tesisId))
        {
            throw new BaseException("Bu tesis altinda islem yapma yetkiniz bulunmuyor.", 403);
        }
    }

    private sealed record RoomAvailability(
        int OdaId,
        string OdaNo,
        int BinaId,
        string BinaAdi,
        int OdaTipiId,
        string OdaTipiAdi,
        int Kapasite,
        bool PaylasimliMi,
        int RemainingCapacity);

    private sealed record RoomInfo(
        int OdaId,
        string OdaNo,
        int TesisId,
        string BinaAdi,
        string OdaTipiAdi,
        int Kapasite,
        bool PaylasimliMi);
}
