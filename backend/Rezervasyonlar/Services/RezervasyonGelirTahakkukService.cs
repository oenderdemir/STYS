using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariHareketler.Services;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Rezervasyonlar.Dto;
using STYS.Rezervasyonlar.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Rezervasyonlar.Services;

/// <inheritdoc cref="IRezervasyonGelirTahakkukService" />
public class RezervasyonGelirTahakkukService : IRezervasyonGelirTahakkukService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly IRezervasyonSatisBelgesiService _rezervasyonSatisBelgesiService;
    private readonly ISatisBelgesiService _satisBelgesiService;
    private readonly ICariHareketKapamaService _cariHareketKapamaService;

    public RezervasyonGelirTahakkukService(
        StysAppDbContext dbContext,
        IUserAccessScopeService userAccessScopeService,
        IRezervasyonSatisBelgesiService rezervasyonSatisBelgesiService,
        ISatisBelgesiService satisBelgesiService,
        ICariHareketKapamaService cariHareketKapamaService)
    {
        _dbContext = dbContext;
        _userAccessScopeService = userAccessScopeService;
        _rezervasyonSatisBelgesiService = rezervasyonSatisBelgesiService;
        _satisBelgesiService = satisBelgesiService;
        _cariHareketKapamaService = cariHareketKapamaService;
    }

    // ──────────────────────────────────────────────
    //  OlusturTaslakAsync — idempotent taslak olusturma
    // ──────────────────────────────────────────────

    public async Task<SatisBelgesiDto> OlusturTaslakAsync(int rezervasyonId, CancellationToken cancellationToken = default)
    {
        var rezervasyon = await GetScopedRezervasyonAsync(rezervasyonId, cancellationToken);

        // Katman 1 — idempotency: belge zaten varsa yenisini yaratma, mevcut olani don.
        if (rezervasyon.SatisBelgesiId.HasValue)
        {
            return await _satisBelgesiService.GetByIdAsync(rezervasyon.SatisBelgesiId.Value, cancellationToken);
        }

        // Katman 2 — RezervasyonSatisBelgesiService.SatisBelgesiTaslagiOlusturAsync zaten
        // ThrowIfKaynakDuplicateAsync ile ikinci bir savunma hatti calistirir.
        var result = await _rezervasyonSatisBelgesiService.SatisBelgesiTaslagiOlusturAsync(
            rezervasyonId,
            new RezervasyonSatisBelgesiTaslakRequest { RezervasyonId = rezervasyonId },
            cancellationToken);

        // Katman 3 — DB'deki filtrelenmis unique index (Rezervasyon.SatisBelgesiId), esizamanli
        // iki cagridan birini burada degil SaveChanges'te reddeder.
        rezervasyon.SatisBelgesiId = result.Id;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    // ──────────────────────────────────────────────
    //  GetGelirOzetiAsync
    // ──────────────────────────────────────────────

    public async Task<RezervasyonGelirOzetiDto> GetGelirOzetiAsync(int rezervasyonId, CancellationToken cancellationToken = default)
    {
        var rezervasyon = await GetScopedRezervasyonAsync(rezervasyonId, cancellationToken);
        return await BuildOzetAsync(rezervasyon, cancellationToken);
    }

    // ──────────────────────────────────────────────
    //  KapatOncekiTahsilatlariAsync
    // ──────────────────────────────────────────────

    public async Task<RezervasyonTahsilatKapamaSonucuDto> KapatOncekiTahsilatlariAsync(int rezervasyonId, CancellationToken cancellationToken = default)
    {
        var rezervasyon = await GetScopedRezervasyonAsync(rezervasyonId, cancellationToken);

        if (!rezervasyon.SatisBelgesiId.HasValue)
        {
            throw new BaseException("Once gelir belgesi (satis belgesi taslagi) olusturulmalidir.", 400);
        }

        // Kural: satis belgesi onaylanip SatisBelgesi kaynakli CariHareket olusmadan onceki
        // tahsilatlar kapatilamaz. Fis/onay durumunu degil, dogrudan bu CariHareket'in varligini
        // arariz — otoriter sinyal budur.
        var faturaHareket = await _dbContext.CariHareketler
            .FirstOrDefaultAsync(
                x => !x.IsDeleted
                     && x.Durum == CariHareketDurumlari.Aktif
                     && x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi
                     && x.KaynakId == rezervasyon.SatisBelgesiId.Value,
                cancellationToken);

        if (faturaHareket is null)
        {
            throw new BaseException(
                "Gelir belgesi icin henuz muhasebe fisi onaylanmamis (SatisBelgesi kaynakli cari hareket bulunamadi). " +
                "Once Muhasebe > Satis Belgeleri ekranindan fis olusturulmalidir.",
                400);
        }

        var odemeIdleri = await _dbContext.RezervasyonOdemeler
            .Where(x => x.RezervasyonId == rezervasyonId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var kapatilacakBelgeler = await _dbContext.TahsilatOdemeBelgeleri
            .Where(x => !x.IsDeleted
                        && x.Durum == TahsilatOdemeBelgeDurumlari.Aktif
                        && x.KaynakModul == MuhasebeKaynakModulleri.Rezervasyon
                        && x.KaynakId != null
                        && odemeIdleri.Contains(x.KaynakId!.Value))
            .ToListAsync(cancellationToken);

        var mevcutKapamaBelgeIdleri = await _dbContext.CariHareketler
            .Where(x => !x.IsDeleted
                        && x.Durum == CariHareketDurumlari.Aktif
                        && x.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi)
            .Select(x => x.KaynakId)
            .ToListAsync(cancellationToken);

        var sonuc = new RezervasyonTahsilatKapamaSonucuDto();

        foreach (var belge in kapatilacakBelgeler)
        {
            if (mevcutKapamaBelgeIdleri.Contains(belge.Id))
            {
                // Zaten kapatilmis (daha once basariyla islenmis).
                sonuc.AtlananSayisi++;
                continue;
            }

            try
            {
                if (!belge.KapatilacakCariHareketId.HasValue)
                {
                    belge.KapatilacakCariHareketId = faturaHareket.Id;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                // CariHareketKapamaService degistirilmeden yeniden kullanilir — kim doldurdugu
                // onemli degil, yalnizca KapatilacakCariHareketId'nin dolu olmasina bakar.
                await _cariHareketKapamaService.TahsilatOdemeIcinCariHareketOlusturVeKapatAsync(belge.Id, cancellationToken);
                sonuc.BasariliSayisi++;
            }
            catch (BaseException ex)
            {
                sonuc.HataliSayisi++;
                sonuc.Hatalar.Add($"{belge.BelgeNo}: {ex.Message}");
            }
        }

        sonuc.Ozet = await BuildOzetAsync(rezervasyon, cancellationToken);
        return sonuc;
    }

    // ──────────────────────────────────────────────
    //  Private — ozet hesaplama
    // ──────────────────────────────────────────────

    private async Task<RezervasyonGelirOzetiDto> BuildOzetAsync(Rezervasyon rezervasyon, CancellationToken cancellationToken)
    {
        var ozet = new RezervasyonGelirOzetiDto
        {
            RezervasyonId = rezervasyon.Id,
            ReferansNo = rezervasyon.ReferansNo,
            SatisBelgesiId = rezervasyon.SatisBelgesiId,
            TahsilatKapamaDurumu = TahsilatKapamaDurumlari.Kapatilmadi
        };

        if (!rezervasyon.SatisBelgesiId.HasValue)
        {
            return ozet;
        }

        var belge = await _dbContext.SatisBelgeleri
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == rezervasyon.SatisBelgesiId.Value, cancellationToken);

        if (belge is null)
        {
            return ozet;
        }

        ozet.SatisBelgesiNo = belge.BelgeNo;
        ozet.SatisBelgesiDurumu = belge.Durum.ToString();
        ozet.GenelToplam = belge.GenelToplam;
        ozet.MuhasebeFisId = belge.MuhasebeFisId;

        var odemeIdleri = await _dbContext.RezervasyonOdemeler
            .Where(x => x.RezervasyonId == rezervasyon.Id)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var adaylar = await _dbContext.TahsilatOdemeBelgeleri
            .AsNoTracking()
            .Where(x => !x.IsDeleted
                        && x.Durum == TahsilatOdemeBelgeDurumlari.Aktif
                        && x.KaynakModul == MuhasebeKaynakModulleri.Rezervasyon
                        && x.KaynakId != null
                        && odemeIdleri.Contains(x.KaynakId!.Value))
            .Select(x => new { x.Id, x.KapatilacakCariHareketId })
            .ToListAsync(cancellationToken);

        ozet.TahsilatToplamSayisi = adaylar.Count;

        if (adaylar.Count == 0)
        {
            ozet.TahsilatKapamaDurumu = TahsilatKapamaDurumlari.TamKapatildi;
            return ozet;
        }

        var kapatilmisBelgeIdleri = await _dbContext.CariHareketler
            .AsNoTracking()
            .Where(x => !x.IsDeleted
                        && x.Durum == CariHareketDurumlari.Aktif
                        && x.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi
                        && x.KaynakId != null
                        && adaylar.Select(a => a.Id).Contains(x.KaynakId!.Value))
            .Select(x => x.KaynakId!.Value)
            .ToListAsync(cancellationToken);

        var kapatilmisSet = kapatilmisBelgeIdleri.ToHashSet();
        var kapaliSayisi = adaylar.Count(a => kapatilmisSet.Contains(a.Id));
        var hataliSayisi = adaylar.Count(a => a.KapatilacakCariHareketId.HasValue && !kapatilmisSet.Contains(a.Id));

        ozet.TahsilatKapaliSayisi = kapaliSayisi;
        ozet.TahsilatHataliSayisi = hataliSayisi;

        ozet.TahsilatKapamaDurumu = hataliSayisi > 0
            ? TahsilatKapamaDurumlari.Hata
            : kapaliSayisi == adaylar.Count
                ? TahsilatKapamaDurumlari.TamKapatildi
                : kapaliSayisi == 0
                    ? TahsilatKapamaDurumlari.Kapatilmadi
                    : TahsilatKapamaDurumlari.KismenKapatildi;

        return ozet;
    }

    // ──────────────────────────────────────────────
    //  Private — Rezervasyon bulma ve access scope
    // ──────────────────────────────────────────────

    private async Task<Rezervasyon> GetScopedRezervasyonAsync(int rezervasyonId, CancellationToken cancellationToken)
    {
        if (rezervasyonId <= 0)
        {
            throw new BaseException("Gecersiz rezervasyon ID.", 400);
        }

        var rezervasyon = await _dbContext.Rezervasyonlar
            .FirstOrDefaultAsync(x => x.Id == rezervasyonId, cancellationToken);

        if (rezervasyon is null)
        {
            throw new BaseException("Rezervasyon bulunamadi.", 404);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && !scope.TesisIds.Contains(rezervasyon.TesisId))
        {
            throw new BaseException("Bu rezervasyon icin yetkiniz bulunmuyor.", 403);
        }

        return rezervasyon;
    }
}
