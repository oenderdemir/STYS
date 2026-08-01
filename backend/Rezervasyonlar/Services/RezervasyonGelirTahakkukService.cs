using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariHareketler.Services;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Rezervasyonlar.Dto;
using STYS.Rezervasyonlar.Entities;
using STYS.TicariBelgeler.Dtos;
using STYS.TicariBelgeler.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Rezervasyonlar.Services;

/// <inheritdoc cref="IRezervasyonGelirTahakkukService" />
public class RezervasyonGelirTahakkukService : IRezervasyonGelirTahakkukService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly IRezervasyonSatisBelgesiService _rezervasyonSatisBelgesiService;
    private readonly ITicariBelgeService _ticariBelgeService;
    private readonly ICariHareketKapamaService _cariHareketKapamaService;

    public RezervasyonGelirTahakkukService(
        StysAppDbContext dbContext,
        IUserAccessScopeService userAccessScopeService,
        IRezervasyonSatisBelgesiService rezervasyonSatisBelgesiService,
        ITicariBelgeService ticariBelgeService,
        ICariHareketKapamaService cariHareketKapamaService)
    {
        _dbContext = dbContext;
        _userAccessScopeService = userAccessScopeService;
        _rezervasyonSatisBelgesiService = rezervasyonSatisBelgesiService;
        _ticariBelgeService = ticariBelgeService;
        _cariHareketKapamaService = cariHareketKapamaService;
    }

    // ──────────────────────────────────────────────
    //  OlusturTaslakAsync — idempotent taslak olusturma
    // ──────────────────────────────────────────────

    public async Task<TicariBelgeDetayDto> OlusturTaslakAsync(int rezervasyonId, CancellationToken cancellationToken = default)
    {
        var rezervasyon = await GetScopedRezervasyonAsync(rezervasyonId, cancellationToken);

        // Katman 1 — idempotency: belge zaten varsa yenisini yaratma, mevcut olani don.
        // ISatisBelgesiService yerine ITicariBelgeService kullanilir (bkz. gorev D) - TicariBelgeDetayDto
        // DOĞRUDAN döner, geçici reverse-compatibility mapping'e ihtiyaç YOKTUR.
        if (rezervasyon.SatisBelgesiId.HasValue)
        {
            return await _ticariBelgeService.GetByIdAsync(rezervasyon.SatisBelgesiId.Value, cancellationToken);
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
        // arariz — otoriter sinyal budur (bkz. GetAktifFaturaHareketiAsync, BuildOzetAsync ile PAYLAŞILIR).
        var faturaHareket = await GetAktifFaturaHareketiAsync(rezervasyon.SatisBelgesiId.Value, cancellationToken);

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
            ozet.TahsilatlarKapatilabilirMi = false;
            ozet.TahsilatlarKapatilamazNedeni = "Önce gelir belgesi oluşturulmalıdır.";
            return ozet;
        }

        // Belge bilgileri artık doğrudan _dbContext.SatisBelgeleri yerine ITicariBelgeService
        // üzerinden alınır - muhasebe ayrıntısına (MuhasebeFisId dahil) bağımlılık YOKTUR (bkz. görev C).
        var ticariBelge = await _ticariBelgeService.GetByIdAsync(rezervasyon.SatisBelgesiId.Value, cancellationToken);

        ozet.SatisBelgesiNo = ticariBelge.BelgeNo;
        ozet.SatisBelgesiDurumu = ticariBelge.OperasyonelDurumAciklamasi;
        ozet.GenelToplam = ticariBelge.GenelToplam;

        // Muhasebeleştirilmiş mi kararı muhasebe fişi kimliğinin varlığına DEĞİL, aktif SatisBelgesi
        // kaynaklı CariHareket'in varlığına göre verilir (bkz. GetAktifFaturaHareketiAsync,
        // KapatOncekiTahsilatlariAsync ile PAYLAŞILAN merkezi kontrol).
        var faturaHareket = await GetAktifFaturaHareketiAsync(rezervasyon.SatisBelgesiId.Value, cancellationToken);
        ozet.MuhasebelestirildiMi = faturaHareket is not null;

        if (faturaHareket is null)
        {
            ozet.TahsilatlarKapatilabilirMi = false;
            ozet.TahsilatlarKapatilamazNedeni =
                "Gelir belgesi için henüz muhasebe fişi onaylanmamış (SatisBelgesi kaynaklı cari hareket bulunamadı).";
        }
        else
        {
            ozet.TahsilatlarKapatilabilirMi = true;
        }

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

    /// <summary>
    /// "Muhasebeleştirildi mi" sinyalinin OTORİTER, TEK kaynağı: aktif, SatisBelgesi kaynaklı
    /// CariHareket'in varlığı (muhasebe fişi kimliğinin varlığı DEĞİL). BuildOzetAsync ve
    /// KapatOncekiTahsilatlariAsync arasında PAYLAŞILIR - aynı kontrol iki yerde ayrı ayrı
    /// yeniden uygulanmaz.
    /// </summary>
    private async Task<CariHareket?> GetAktifFaturaHareketiAsync(int satisBelgesiId, CancellationToken cancellationToken)
    {
        return await _dbContext.CariHareketler
            .FirstOrDefaultAsync(
                x => !x.IsDeleted
                     && x.Durum == CariHareketDurumlari.Aktif
                     && x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi
                     && x.KaynakId == satisBelgesiId,
                cancellationToken);
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
