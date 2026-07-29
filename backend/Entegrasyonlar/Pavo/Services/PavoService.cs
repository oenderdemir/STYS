using Microsoft.EntityFrameworkCore;
using STYS.Entegrasyonlar.Pavo.Dtos;
using STYS.Entegrasyonlar.Pavo.Entities;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Dto;
using STYS.Rezervasyonlar.Entities;
using STYS.Rezervasyonlar.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Entegrasyonlar.Pavo.Services;

public sealed class PavoService : IPavoService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IPavoUniCloudClient _client;
    private readonly IRezervasyonService _rezervasyonService;
    private readonly IRezervasyonCariKartResolver _cariKartResolver;

    public PavoService(
        StysAppDbContext dbContext,
        IPavoUniCloudClient client,
        IRezervasyonService rezervasyonService,
        IRezervasyonCariKartResolver cariKartResolver)
    {
        _dbContext = dbContext;
        _client = client;
        _rezervasyonService = rezervasyonService;
        _cariKartResolver = cariKartResolver;
    }

    public async Task<List<PavoTerminalDto>> GetTerminallerAsync(
        int? tesisId,
        int? kasaBankaHesapId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.PavoTerminaller.AsNoTracking().AsQueryable();
        if (tesisId.HasValue)
        {
            query = query.Where(x => x.TesisId == tesisId.Value);
        }

        if (kasaBankaHesapId.HasValue)
        {
            query = query.Where(x => x.KasaBankaHesapId == kasaBankaHesapId.Value);
        }

        return await query.OrderBy(x => x.Ad).Select(ToTerminalDtoExpression()).ToListAsync(cancellationToken);
    }

    public async Task<PavoTerminalDto> KaydetTerminalAsync(
        int? id,
        PavoTerminalKaydetRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Ad)
            || string.IsNullOrWhiteSpace(request.SerialNumber)
            || string.IsNullOrWhiteSpace(request.SourceFingerprint))
        {
            throw new BaseException("Terminal adi, seri numarasi ve fingerprint zorunludur.", 400);
        }

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
            throw new BaseException("PAVO terminali ile kredi karti hesabi ayni tesise ait olmalidir.", 400);
        }

        PavoTerminal terminal;
        if (id.HasValue)
        {
            terminal = await _dbContext.PavoTerminaller.FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
                ?? throw new BaseException("PAVO terminali bulunamadi.", 404);
        }
        else
        {
            terminal = new PavoTerminal();
            await _dbContext.PavoTerminaller.AddAsync(terminal, cancellationToken);
        }

        var serial = request.SerialNumber.Trim().ToUpperInvariant();
        var duplicate = await _dbContext.PavoTerminaller.AnyAsync(
            x => x.SerialNumber == serial && (!id.HasValue || x.Id != id.Value),
            cancellationToken);
        if (duplicate)
        {
            throw new BaseException("Bu PAVO seri numarasi zaten tanimli.", 409);
        }

        var pairingIdentityChanged = terminal.SerialNumber != serial
            || terminal.SourceFingerprint != request.SourceFingerprint.Trim();

        terminal.TesisId = request.TesisId;
        terminal.KasaBankaHesapId = request.KasaBankaHesapId;
        terminal.Ad = request.Ad.Trim();
        terminal.SerialNumber = serial;
        terminal.SourceFingerprint = request.SourceFingerprint.Trim();
        terminal.SourceTerminalReference = Normalize(request.SourceTerminalReference);
        terminal.AktifMi = request.AktifMi;

        if (pairingIdentityChanged)
        {
            terminal.PairingId = null;
            terminal.PairingCode = null;
            terminal.TargetFingerprint = null;
            terminal.EslesmeOnayliMi = false;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(terminal);
    }

    public async Task<PavoTerminalDto> EslesmeBaslatAsync(int id, CancellationToken cancellationToken)
    {
        var terminal = await GetTerminalAsync(id, cancellationToken);
        var result = await _client.PairingRequestAsync(terminal, cancellationToken);
        terminal.PairingId = result.Id;
        terminal.PairingCode = result.PairingCode;
        terminal.TargetFingerprint = result.TargetFingerprint;
        terminal.EslesmeOnayliMi = result.IsApproved;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(terminal);
    }

    public async Task<PavoTerminalDto> EslesmeKontrolAsync(int id, CancellationToken cancellationToken)
    {
        var terminal = await GetTerminalAsync(id, cancellationToken);
        var result = await _client.CheckPairingAsync(terminal, cancellationToken);
        terminal.PairingCode = result.PairingCode ?? terminal.PairingCode;
        terminal.TargetFingerprint = result.TargetFingerprint;
        terminal.EslesmeOnayliMi = result.IsApproved;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(terminal);
    }

    public async Task<PavoOdemeIslemiDto> OdemeBaslatAsync(
        PavoOdemeBaslatRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Tutar <= 0)
        {
            throw new BaseException("Odeme tutari sifirdan buyuk olmalidir.", 400);
        }

        var terminal = await GetTerminalAsync(request.PavoTerminalId, cancellationToken);
        if (!terminal.AktifMi || !terminal.EslesmeOnayliMi)
        {
            throw new BaseException("PAVO terminali aktif ve eslesmis olmalidir.", 409);
        }

        var rezervasyon = await _dbContext.Rezervasyonlar
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.RezervasyonId, cancellationToken)
            ?? throw new BaseException("Rezervasyon bulunamadi.", 404);
        if (rezervasyon.TesisId != terminal.TesisId)
        {
            throw new BaseException("PAVO terminali rezervasyonun tesisiyle uyumlu degil.", 400);
        }

        var ozet = await _rezervasyonService.GetOdemeOzetiAsync(request.RezervasyonId, cancellationToken);
        if (request.Tutar > ozet.KalanTutar)
        {
            throw new BaseException("Odeme tutari kalan bakiyeden buyuk olamaz.", 400);
        }

        // Cari secimi odeme POS'a gonderilmeden once kesinlestirilir. Aksi halde karttan
        // tahsilat basarili olduktan sonra yerel muhasebelestirme 422 ile bekleyebilir.
        var cariKartId = await _cariKartResolver.ResolveAsync(rezervasyon, request.CariKartId, cancellationToken);

        var islem = new PavoOdemeIslemi
        {
            TesisId = terminal.TesisId,
            RezervasyonId = request.RezervasyonId,
            PavoTerminalId = terminal.Id,
            KasaBankaHesapId = terminal.KasaBankaHesapId,
            CariKartId = cariKartId,
            PaymentLinkReference = $"STYS-{terminal.TesisId}-{Guid.NewGuid():N}",
            Tutar = request.Tutar,
            ParaBirimi = ozet.ParaBirimi,
            Aciklama = Normalize(request.Aciklama)
        };
        await _dbContext.PavoOdemeIslemleri.AddAsync(islem, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var result = await _client.CreateLinkAsync(
                terminal,
                islem.PaymentLinkReference,
                islem.Tutar,
                islem.ParaBirimi,
                cancellationToken);
            islem.PaymentLinkId = result.Id;
            islem.SonPavoYaniti = result.RawJson;
            islem.Durum = PavoOdemeDurumlari.PosIslemiBekleniyor;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToDto(islem);
        }
        catch (Exception ex)
        {
            islem.Durum = PavoOdemeDurumlari.Basarisiz;
            islem.HataMesaji = Truncate(ex.Message, 1024);
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<PavoOdemeIslemiDto> OdemeDurumuAsync(int id, CancellationToken cancellationToken)
    {
        var islem = await _dbContext.PavoOdemeIslemleri
            .Include(x => x.PavoTerminal)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BaseException("PAVO odeme islemi bulunamadi.", 404);

        if (islem.Durum is PavoOdemeDurumlari.Muhasebelestirildi or PavoOdemeDurumlari.Basarisiz)
        {
            return ToDto(islem);
        }

        if (!islem.PaymentLinkId.HasValue || islem.PavoTerminal is null)
        {
            throw new BaseException("PAVO odeme baglantisi eksik.", 409);
        }

        var result = await _client.CheckLinkAsync(
            islem.PavoTerminal,
            islem.PaymentLinkId.Value,
            islem.PaymentLinkReference,
            cancellationToken);
        islem.SonSorgulamaTarihi = DateTime.UtcNow;
        islem.SonPavoYaniti = result.RawJson;

        if (result.Pending)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToDto(islem);
        }

        if (!result.Successful)
        {
            islem.Durum = PavoOdemeDurumlari.Basarisiz;
            islem.HataMesaji = Truncate(result.ErrorMessage, 1024);
            islem.TamamlanmaTarihi = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToDto(islem);
        }

        islem.Durum = PavoOdemeDurumlari.Basarili;
        islem.RetrievalReferenceNo = result.RetrievalReferenceNo;
        islem.AcquirerReference = result.AcquirerReference;
        islem.AuthorizationCode = result.AuthorizationCode;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // KaydetOdemeAsync, PavoOdemeIslemiId unique baglantisini ve son durum guncellemesini
        // ayni veritabani transaction'inda yapar. Tekrar sorgulama ikinci tahsilat uretemez.
        await _rezervasyonService.KaydetOdemeAsync(
            islem.RezervasyonId,
            new RezervasyonOdemeKaydetRequestDto
            {
                OdemeTutari = islem.Tutar,
                OdemeTipi = OdemeTipleri.KrediKarti,
                KasaBankaHesapId = islem.KasaBankaHesapId,
                CariKartId = islem.CariKartId,
                Aciklama = islem.Aciklama,
                PavoOdemeIslemiId = islem.Id
            },
            cancellationToken);

        await _dbContext.Entry(islem).ReloadAsync(cancellationToken);
        return ToDto(islem);
    }

    public async Task<PavoOdemeIslemiDto?> BekleyenOdemeAsync(int rezervasyonId, CancellationToken cancellationToken)
    {
        var islem = await _dbContext.PavoOdemeIslemleri
            .AsNoTracking()
            .Where(x => x.RezervasyonId == rezervasyonId
                        && (x.Durum == PavoOdemeDurumlari.Olusturuldu
                            || x.Durum == PavoOdemeDurumlari.PosIslemiBekleniyor
                            || x.Durum == PavoOdemeDurumlari.Basarili
                            || x.Durum == PavoOdemeDurumlari.MutabakatGerekli))
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return islem is null ? null : ToDto(islem);
    }

    private async Task<PavoTerminal> GetTerminalAsync(int id, CancellationToken cancellationToken) =>
        await _dbContext.PavoTerminaller.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new BaseException("PAVO terminali bulunamadi.", 404);

    private static System.Linq.Expressions.Expression<Func<PavoTerminal, PavoTerminalDto>> ToTerminalDtoExpression() =>
        x => new PavoTerminalDto
        {
            Id = x.Id,
            TesisId = x.TesisId,
            KasaBankaHesapId = x.KasaBankaHesapId,
            Ad = x.Ad,
            SerialNumber = x.SerialNumber,
            SourceFingerprint = x.SourceFingerprint,
            SourceTerminalReference = x.SourceTerminalReference,
            EslesmeOnayliMi = x.EslesmeOnayliMi,
            AktifMi = x.AktifMi,
            PairingId = x.PairingId,
            PairingCode = x.PairingCode
        };

    private static PavoTerminalDto ToDto(PavoTerminal x) => new()
    {
        Id = x.Id,
        TesisId = x.TesisId,
        KasaBankaHesapId = x.KasaBankaHesapId,
        Ad = x.Ad,
        SerialNumber = x.SerialNumber,
        SourceFingerprint = x.SourceFingerprint,
        SourceTerminalReference = x.SourceTerminalReference,
        EslesmeOnayliMi = x.EslesmeOnayliMi,
        AktifMi = x.AktifMi,
        PairingId = x.PairingId,
        PairingCode = x.PairingCode
    };

    private static PavoOdemeIslemiDto ToDto(PavoOdemeIslemi x) => new()
    {
        Id = x.Id,
        RezervasyonId = x.RezervasyonId,
        PavoTerminalId = x.PavoTerminalId,
        KasaBankaHesapId = x.KasaBankaHesapId,
        PaymentLinkId = x.PaymentLinkId,
        PaymentLinkReference = x.PaymentLinkReference,
        Tutar = x.Tutar,
        ParaBirimi = x.ParaBirimi,
        Durum = x.Durum,
        HataMesaji = x.HataMesaji,
        RezervasyonOdemeId = x.RezervasyonOdemeId,
        TamamlandiMi = x.Durum is PavoOdemeDurumlari.Muhasebelestirildi or PavoOdemeDurumlari.Basarisiz
    };

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];
}
