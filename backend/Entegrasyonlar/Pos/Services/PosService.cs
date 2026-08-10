using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Entegrasyonlar.Pos.Options;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Dto;
using STYS.Rezervasyonlar.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Entegrasyonlar.Pos.Services;

public sealed class PosService : IPosService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IReadOnlyDictionary<string, IPosOdemeSaglayicisi> _saglayicilar;
    private readonly IRezervasyonService _rezervasyonService;
    private readonly IRezervasyonCariKartResolver _cariKartResolver;
    private readonly PosOdemeTakipOptions _takipOptions;

    public PosService(
        StysAppDbContext dbContext,
        IEnumerable<IPosOdemeSaglayicisi> saglayicilar,
        IRezervasyonService rezervasyonService,
        IRezervasyonCariKartResolver cariKartResolver,
        IOptions<PosOdemeTakipOptions> takipOptions)
    {
        _dbContext = dbContext;
        _saglayicilar = saglayicilar.ToDictionary(x => x.Kod, StringComparer.OrdinalIgnoreCase);
        _rezervasyonService = rezervasyonService;
        _cariKartResolver = cariKartResolver;
        _takipOptions = takipOptions.Value;
    }

    public List<PosSaglayiciDto> GetSaglayicilar() =>
        _saglayicilar.Values
            .OrderBy(x => x.Ad)
            .Select(x => new PosSaglayiciDto
            {
                Kod = x.Kod,
                Ad = x.Ad,
                EslesmeDestekliyorMu = x.EslesmeDestekliyorMu
            })
            .ToList();

    public async Task<List<PosTerminalDto>> GetTerminallerAsync(
        int? tesisId,
        int? kasaBankaHesapId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.PosTerminaller.AsNoTracking().AsQueryable();
        if (tesisId.HasValue)
        {
            query = query.Where(x => x.TesisId == tesisId.Value);
        }

        if (kasaBankaHesapId.HasValue)
        {
            query = query.Where(x => x.KasaBankaHesapId == kasaBankaHesapId.Value);
        }

        return await query
            .OrderBy(x => x.Ad)
            .Select(ToTerminalDtoExpression())
            .ToListAsync(cancellationToken);
    }

    public async Task<PosTerminalDto> KaydetTerminalAsync(
        int? id,
        PosTerminalKaydetRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Ad)
            || string.IsNullOrWhiteSpace(request.SerialNumber)
            || string.IsNullOrWhiteSpace(request.SaglayiciKodu))
        {
            throw new BaseException("Saglayici, terminal adi ve seri numarasi zorunludur.", 400);
        }

        var saglayiciKodu = request.SaglayiciKodu.Trim().ToUpperInvariant();
        var saglayici = GetSaglayici(saglayiciKodu);
        var hesap = await _dbContext.KasaBankaHesaplari.FirstOrDefaultAsync(
            x => x.Id == request.KasaBankaHesapId
                 && x.AktifMi
                 && x.Tip == KasaBankaHesapTipleri.KrediKarti,
            cancellationToken);
        if (hesap is null)
        {
            throw new BaseException("Aktif kredi karti/POS hesabi bulunamadi.", 404);
        }

        if (hesap.TesisId.HasValue && hesap.TesisId.Value != request.TesisId)
        {
            throw new BaseException("POS terminali ile kredi karti hesabi ayni tesise ait olmalidir.", 400);
        }

        PosTerminal terminal;
        if (id.HasValue)
        {
            terminal = await _dbContext.PosTerminaller.FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
                ?? throw new BaseException("POS terminali bulunamadi.", 404);
        }
        else
        {
            terminal = new PosTerminal();
            await _dbContext.PosTerminaller.AddAsync(terminal, cancellationToken);
        }

        var serial = request.SerialNumber.Trim().ToUpperInvariant();
        var duplicate = await _dbContext.PosTerminaller.AnyAsync(
            x => x.SaglayiciKodu == saglayiciKodu
                 && x.SerialNumber == serial
                 && (!id.HasValue || x.Id != id.Value),
            cancellationToken);
        if (duplicate)
        {
            throw new BaseException("Bu saglayici ve seri numarasi ile bir terminal zaten tanimli.", 409);
        }

        var sourceFingerprint = Normalize(request.SourceFingerprint);
        var pairingIdentityChanged = terminal.SaglayiciKodu != saglayiciKodu
            || terminal.SerialNumber != serial
            || terminal.SourceFingerprint != sourceFingerprint;

        terminal.TesisId = request.TesisId;
        terminal.KasaBankaHesapId = request.KasaBankaHesapId;
        terminal.SaglayiciKodu = saglayiciKodu;
        terminal.Ad = request.Ad.Trim();
        terminal.SerialNumber = serial;
        terminal.SourceFingerprint = sourceFingerprint;
        terminal.SourceTerminalReference = Normalize(request.SourceTerminalReference);
        terminal.AktifMi = request.AktifMi;
        saglayici.TerminalBilgileriniDogrula(terminal);

        if (pairingIdentityChanged)
        {
            terminal.PairingId = null;
            terminal.PairingCode = null;
            terminal.TargetFingerprint = null;
            terminal.EslesmeOnayliMi = !saglayici.EslesmeDestekliyorMu;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(terminal);
    }

    public async Task<PosTerminalDto> EslesmeBaslatAsync(int id, CancellationToken cancellationToken)
    {
        var terminal = await GetTerminalAsync(id, cancellationToken);
        var saglayici = GetSaglayici(terminal.SaglayiciKodu);
        EnsurePairingSupported(saglayici);
        var result = await saglayici.EslesmeBaslatAsync(terminal, cancellationToken);
        ApplyPairingResult(terminal, result);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(terminal);
    }

    public async Task<PosTerminalDto> EslesmeKontrolAsync(int id, CancellationToken cancellationToken)
    {
        var terminal = await GetTerminalAsync(id, cancellationToken);
        var saglayici = GetSaglayici(terminal.SaglayiciKodu);
        EnsurePairingSupported(saglayici);
        var result = await saglayici.EslesmeKontrolAsync(terminal, cancellationToken);
        ApplyPairingResult(terminal, result);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(terminal);
    }

    public async Task<PosOdemeIslemiDto> OdemeBaslatAsync(
        PosOdemeBaslatRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Tutar <= 0)
        {
            throw new BaseException("Odeme tutari sifirdan buyuk olmalidir.", 400);
        }

        var terminal = await GetTerminalAsync(request.PosTerminalId, cancellationToken);
        var saglayici = GetSaglayici(terminal.SaglayiciKodu);
        if (!terminal.AktifMi || !terminal.EslesmeOnayliMi)
        {
            throw new BaseException("POS terminali aktif ve kullanima hazir olmalidir.", 409);
        }

        var rezervasyon = await _dbContext.Rezervasyonlar
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.RezervasyonId, cancellationToken)
            ?? throw new BaseException("Rezervasyon bulunamadi.", 404);
        if (rezervasyon.TesisId != terminal.TesisId)
        {
            throw new BaseException("POS terminali rezervasyonun tesisiyle uyumlu degil.", 400);
        }

        var ozet = await _rezervasyonService.GetOdemeOzetiAsync(request.RezervasyonId, cancellationToken);
        if (request.Tutar > ozet.KalanTutar)
        {
            throw new BaseException("Odeme tutari kalan bakiyeden buyuk olamaz.", 400);
        }

        var cariKartId = await _cariKartResolver.ResolveAsync(rezervasyon, request.CariKartId, cancellationToken);
        var islem = new PosOdemeIslemi
        {
            TesisId = terminal.TesisId,
            RezervasyonId = request.RezervasyonId,
            PosTerminalId = terminal.Id,
            KasaBankaHesapId = terminal.KasaBankaHesapId ?? throw new BaseException("Ödeme başlatmak için terminale bağlı kredi kartı hesabı seçilmelidir.", 400),
            CariKartId = cariKartId,
            IslemReferansi = $"STYS-{terminal.TesisId}-{Guid.NewGuid():N}",
            Tutar = request.Tutar,
            ParaBirimi = ozet.ParaBirimi,
            Aciklama = Normalize(request.Aciklama)
        };
        await _dbContext.PosOdemeIslemleri.AddAsync(islem, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var result = await saglayici.OdemeBaslatAsync(
                terminal,
                islem.IslemReferansi,
                islem.Tutar,
                islem.ParaBirimi,
                cancellationToken);
            islem.SaglayiciIslemId = result.SaglayiciIslemId;
            islem.SaglayiciDurumKodu = result.SaglayiciDurumKodu;
            islem.SonSaglayiciYaniti = result.HamYanit;
            islem.Durum = PosOdemeDurumlari.PosIslemiBekleniyor;
            islem.SonrakiSorgulamaTarihi = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToDto(islem, terminal.SaglayiciKodu);
        }
        catch (Exception ex)
        {
            // Saglayici istegi almis fakat HTTP cevabi kaybolmus olabilir. Bu durumda
            // otomatik "basarisiz" demek kart tahsilatini gizleyebilecegi icin mutabakat gerekir.
            islem.Durum = PosOdemeDurumlari.MutabakatGerekli;
            islem.HataMesaji = Truncate(ex.Message, 1024);
            islem.TamamlanmaTarihi = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<PosOdemeIslemiDto> OdemeDurumuAsync(int id, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var leaseToken = Guid.NewGuid();
        var leaseBitis = now.AddSeconds(Math.Clamp(_takipOptions.LeaseSeconds, 15, 300));
        var claimed = await _dbContext.PosOdemeIslemleri
            .Where(x => x.Id == id
                        && (x.Durum == PosOdemeDurumlari.Olusturuldu
                            || x.Durum == PosOdemeDurumlari.PosIslemiBekleniyor
                            || x.Durum == PosOdemeDurumlari.Basarili)
                        && (!x.SonrakiSorgulamaTarihi.HasValue || x.SonrakiSorgulamaTarihi <= now)
                        && (!x.TakipKilitBitisTarihi.HasValue || x.TakipKilitBitisTarihi < now))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.TakipKilitToken, leaseToken)
                    .SetProperty(x => x.TakipKilitBitisTarihi, leaseBitis)
                    .SetProperty(x => x.SonSorgulamaTarihi, now)
                    .SetProperty(x => x.SorgulamaDenemeSayisi, x => x.SorgulamaDenemeSayisi + 1)
                    .SetProperty(x => x.ConcurrencyToken, Guid.NewGuid()),
                cancellationToken);

        var islem = await _dbContext.PosOdemeIslemleri
            .Include(x => x.PosTerminal)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BaseException("POS odeme islemi bulunamadi.", 404);

        if (claimed == 0)
        {
            return ToDto(islem, islem.PosTerminal?.SaglayiciKodu);
        }

        try
        {
            if (TakipSiniriAsildi(islem, now))
            {
                MutabakataAl(islem, "POS islemi belirlenen sure veya sorgulama sinirinda tamamlanmadi.");
                await _dbContext.SaveChangesAsync(cancellationToken);
                return ToDto(islem, islem.PosTerminal?.SaglayiciKodu);
            }

            if (islem.Durum == PosOdemeDurumlari.Basarili)
            {
                return await MuhasebelestirAsync(islem, cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(islem.SaglayiciIslemId) || islem.PosTerminal is null)
            {
                SorgulamayiErtele(islem, "POS saglayici islem bilgisi henuz olusmadi.");
                await _dbContext.SaveChangesAsync(cancellationToken);
                return ToDto(islem, islem.PosTerminal?.SaglayiciKodu);
            }

            var saglayici = GetSaglayici(islem.PosTerminal.SaglayiciKodu);
            var result = await saglayici.OdemeDurumuAsync(
                islem.PosTerminal,
                islem.SaglayiciIslemId,
                islem.IslemReferansi,
                cancellationToken);
            islem.SaglayiciDurumKodu = result.SaglayiciDurumKodu;
            islem.SonSaglayiciYaniti = result.HamYanit;
            islem.SonSorgulamaHatasi = null;

            if (result.Bekliyor)
            {
                SorgulamayiErtele(islem);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return ToDto(islem, islem.PosTerminal.SaglayiciKodu);
            }

            if (!result.Basarili)
            {
                islem.Durum = PosOdemeDurumlari.Basarisiz;
                islem.HataMesaji = Truncate(result.HataMesaji, 1024);
                islem.TamamlanmaTarihi = DateTime.UtcNow;
                TakipKilidiniBirak(islem);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return ToDto(islem, islem.PosTerminal.SaglayiciKodu);
            }

            islem.Durum = PosOdemeDurumlari.Basarili;
            islem.RetrievalReferenceNo = result.RetrievalReferenceNo;
            islem.AcquirerReference = result.AcquirerReference;
            islem.AuthorizationCode = result.AuthorizationCode;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return await MuhasebelestirAsync(islem, cancellationToken);
        }
        catch (Exception ex)
        {
            islem.SonSorgulamaHatasi = Truncate(ex.Message, 1024);
            SorgulamayiErtele(islem);
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<PosOdemeIslemiDto?> BekleyenOdemeAsync(int rezervasyonId, CancellationToken cancellationToken)
    {
        var islem = await _dbContext.PosOdemeIslemleri
            .AsNoTracking()
            .Include(x => x.PosTerminal)
            .Where(x => x.RezervasyonId == rezervasyonId
                        && (x.Durum == PosOdemeDurumlari.Olusturuldu
                            || x.Durum == PosOdemeDurumlari.PosIslemiBekleniyor
                            || x.Durum == PosOdemeDurumlari.Basarili
                            || x.Durum == PosOdemeDurumlari.MutabakatGerekli))
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return islem is null ? null : ToDto(islem, islem.PosTerminal?.SaglayiciKodu);
    }

    private async Task<PosOdemeIslemiDto> MuhasebelestirAsync(
        PosOdemeIslemi islem,
        CancellationToken cancellationToken)
    {
        await _rezervasyonService.KaydetOdemeAsync(
            islem.RezervasyonId,
            new RezervasyonOdemeKaydetRequestDto
            {
                OdemeTutari = islem.Tutar,
                OdemeTipi = OdemeTipleri.KrediKarti,
                KasaBankaHesapId = islem.KasaBankaHesapId,
                CariKartId = islem.CariKartId,
                Aciklama = islem.Aciklama,
                PosOdemeIslemiId = islem.Id
            },
            cancellationToken);

        await _dbContext.Entry(islem).ReloadAsync(cancellationToken);
        TakipKilidiniBirak(islem);
        islem.SonSorgulamaHatasi = null;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(islem, islem.PosTerminal?.SaglayiciKodu);
    }

    private bool TakipSiniriAsildi(PosOdemeIslemi islem, DateTime now)
    {
        var maxAttempts = Math.Clamp(_takipOptions.MaxAttempts, 1, 10_000);
        var timeout = TimeSpan.FromMinutes(Math.Clamp(_takipOptions.TimeoutMinutes, 1, 24 * 60));
        return islem.SorgulamaDenemeSayisi >= maxAttempts
            || (islem.CreatedAt.HasValue && islem.CreatedAt.Value.Add(timeout) <= now);
    }

    private void SorgulamayiErtele(PosOdemeIslemi islem, string? hata = null)
    {
        var baseSeconds = Math.Clamp(_takipOptions.BaseRetrySeconds, 1, 60);
        var maxSeconds = Math.Clamp(_takipOptions.MaxRetrySeconds, baseSeconds, 300);
        var exponent = Math.Min(Math.Max(0, islem.SorgulamaDenemeSayisi - 1), 5);
        var delaySeconds = Math.Min(maxSeconds, baseSeconds * (1 << exponent));
        islem.SonrakiSorgulamaTarihi = DateTime.UtcNow.AddSeconds(delaySeconds);
        if (!string.IsNullOrWhiteSpace(hata))
        {
            islem.SonSorgulamaHatasi = Truncate(hata, 1024);
        }
        TakipKilidiniBirak(islem);
    }

    private static void MutabakataAl(PosOdemeIslemi islem, string hata)
    {
        islem.Durum = PosOdemeDurumlari.MutabakatGerekli;
        islem.HataMesaji = hata;
        islem.TamamlanmaTarihi = DateTime.UtcNow;
        islem.SonrakiSorgulamaTarihi = null;
        TakipKilidiniBirak(islem);
    }

    private static void TakipKilidiniBirak(PosOdemeIslemi islem)
    {
        islem.TakipKilitToken = null;
        islem.TakipKilitBitisTarihi = null;
        islem.ConcurrencyToken = Guid.NewGuid();
    }

    private IPosOdemeSaglayicisi GetSaglayici(string kod)
    {
        if (_saglayicilar.TryGetValue(kod, out var saglayici))
        {
            return saglayici;
        }

        throw new BaseException($"'{kod}' kodlu POS saglayicisi desteklenmiyor.", 400);
    }

    private async Task<PosTerminal> GetTerminalAsync(int id, CancellationToken cancellationToken) =>
        await _dbContext.PosTerminaller.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new BaseException("POS terminali bulunamadi.", 404);

    private static void EnsurePairingSupported(IPosOdemeSaglayicisi saglayici)
    {
        if (!saglayici.EslesmeDestekliyorMu)
        {
            throw new BaseException($"{saglayici.Ad} saglayicisi cihaz eslestirmeyi desteklemiyor.", 409);
        }
    }

    private static void ApplyPairingResult(PosTerminal terminal, PosEslesmeSonucu result)
    {
        terminal.PairingId = result.PairingId;
        terminal.PairingCode = result.PairingCode ?? terminal.PairingCode;
        terminal.TargetFingerprint = result.TargetFingerprint;
        terminal.EslesmeOnayliMi = result.OnayliMi;
    }

    private static System.Linq.Expressions.Expression<Func<PosTerminal, PosTerminalDto>> ToTerminalDtoExpression() =>
        x => new PosTerminalDto
        {
            Id = x.Id,
            TesisId = x.TesisId,
            KasaBankaHesapId = x.KasaBankaHesapId,
            SaglayiciKodu = x.SaglayiciKodu,
            AcquirerId = x.AcquirerId,
            AcquirerName = x.AcquirerName,
            Ad = x.Ad,
            SerialNumber = x.SerialNumber,
            SourceFingerprint = x.SourceFingerprint,
            SourceTerminalReference = x.SourceTerminalReference,
            EslesmeOnayliMi = x.EslesmeOnayliMi,
            AktifMi = x.AktifMi,
            PairingId = x.PairingId,
            PairingCode = x.PairingCode
        };

    private static PosTerminalDto ToDto(PosTerminal x) => new()
    {
        Id = x.Id,
        TesisId = x.TesisId,
        KasaBankaHesapId = x.KasaBankaHesapId,
        SaglayiciKodu = x.SaglayiciKodu,
        AcquirerId = x.AcquirerId,
        AcquirerName = x.AcquirerName,
        Ad = x.Ad,
        SerialNumber = x.SerialNumber,
        SourceFingerprint = x.SourceFingerprint,
        SourceTerminalReference = x.SourceTerminalReference,
        EslesmeOnayliMi = x.EslesmeOnayliMi,
        AktifMi = x.AktifMi,
        PairingId = x.PairingId,
        PairingCode = x.PairingCode
    };

    private static PosOdemeIslemiDto ToDto(PosOdemeIslemi x, string? saglayiciKodu) => new()
    {
        Id = x.Id,
        RezervasyonId = x.RezervasyonId,
        PosTerminalId = x.PosTerminalId,
        KasaBankaHesapId = x.KasaBankaHesapId,
        SaglayiciKodu = saglayiciKodu ?? string.Empty,
        SaglayiciIslemId = x.SaglayiciIslemId,
        SaglayiciDurumKodu = x.SaglayiciDurumKodu,
        IslemReferansi = x.IslemReferansi,
        Tutar = x.Tutar,
        ParaBirimi = x.ParaBirimi,
        Durum = x.Durum,
        HataMesaji = x.HataMesaji,
        SonSorgulamaTarihi = x.SonSorgulamaTarihi,
        SorgulamaDenemeSayisi = x.SorgulamaDenemeSayisi,
        RezervasyonOdemeId = x.RezervasyonOdemeId,
        TamamlandiMi = x.Durum is PosOdemeDurumlari.Muhasebelestirildi
            or PosOdemeDurumlari.Basarisiz
            or PosOdemeDurumlari.MutabakatGerekli
    };

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];
}
