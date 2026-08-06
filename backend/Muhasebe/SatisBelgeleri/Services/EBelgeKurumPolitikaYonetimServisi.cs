using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using STYS.Infrastructure.EntityFramework;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// Faz 2B.10 görev md.15 - kurum e-belge politikasının YÖNETİMİ (okuma/güncelleme). Karar
/// SERVİSİNDEN (<see cref="IEBelgeKurumPolitikaServisi"/>) KASITLI OLARAK AYRIDIR - bu servis
/// YAZAR, karar servisi yalnız OKUR/değerlendirir.
/// </summary>
public interface IEBelgeKurumPolitikaYonetimServisi
{
    Task<KurumEBelgePolitikasi?> GetirAsync(int kurumId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KurumEBelgePolitikaRevizyonu>> RevizyonlariGetirAsync(int kurumId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Politika satırını (yoksa OLUŞTURUR, varsa GÜNCELLER) ve AYNI transaction'da bir
    /// <see cref="KurumEBelgePolitikaRevizyonu"/> audit kaydı ekler. `dto.RowVersion` boş string
    /// ise "henüz politika satırı yok" beklenir - satır ZATEN varsa concurrency çakışması olarak
    /// reddedilir.
    /// </summary>
    Task<KurumEBelgePolitikasi> GuncelleAsync(int kurumId, Dtos.KurumEBelgePolitikasiGuncellemeDto dto, CancellationToken cancellationToken = default);
}

public sealed class EBelgeKurumPolitikaYonetimServisi : IEBelgeKurumPolitikaYonetimServisi
{
    private const int MaxDegisiklikNedeniUzunluk = 500;
    private const int MaxHariciSistemKoduUzunluk = 64;
    private const string DateFormat = "yyyy-MM-dd";

    private readonly StysAppDbContext _dbContext;
    private readonly IEBelgeYontemYetenekSaglayici _yetenekSaglayici;
    private readonly EBelgeProcessingOptions _processingOptions;
    private readonly TimeProvider _timeProvider;

    public EBelgeKurumPolitikaYonetimServisi(
        StysAppDbContext dbContext,
        IEBelgeYontemYetenekSaglayici yetenekSaglayici,
        IOptions<EBelgeProcessingOptions> processingOptions,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _yetenekSaglayici = yetenekSaglayici;
        _processingOptions = processingOptions.Value;
        _timeProvider = timeProvider;
    }

    public Task<KurumEBelgePolitikasi?> GetirAsync(int kurumId, CancellationToken cancellationToken = default) =>
        _dbContext.Set<KurumEBelgePolitikasi>().AsNoTracking().FirstOrDefaultAsync(p => p.KurumId == kurumId, cancellationToken);

    public async Task<IReadOnlyList<KurumEBelgePolitikaRevizyonu>> RevizyonlariGetirAsync(int kurumId, CancellationToken cancellationToken = default) =>
        await _dbContext.Set<KurumEBelgePolitikaRevizyonu>()
            .AsNoTracking()
            .Where(r => r.KurumId == kurumId)
            .OrderByDescending(r => r.YeniSurum)
            .ToListAsync(cancellationToken);

    public async Task<KurumEBelgePolitikasi> GuncelleAsync(int kurumId, Dtos.KurumEBelgePolitikasiGuncellemeDto dto, CancellationToken cancellationToken = default)
    {
        if (kurumId <= 0)
        {
            throw new BaseException("KurumId pozitif olmalıdır.", 400);
        }

        ArgumentNullException.ThrowIfNull(dto);

        if (!Enum.IsDefined(dto.EntegrasyonYontemi))
        {
            throw new BaseException("Bilinmeyen entegrasyon yöntemi.", 400);
        }

        var degisiklikNedeni = (dto.DegisiklikNedeni ?? string.Empty).Trim();
        if (degisiklikNedeni.Length == 0)
        {
            throw new BaseException("Değişiklik nedeni zorunludur.", 400);
        }

        if (degisiklikNedeni.Length > MaxDegisiklikNedeniUzunluk)
        {
            throw new BaseException($"Değişiklik nedeni en fazla {MaxDegisiklikNedeniUzunluk} karakter olabilir.", 400);
        }

        var hariciSistemKodu = NormalizeHariciSistemKodu(dto.HariciSistemKodu);

        byte[] beklenenRowVersion;
        try
        {
            beklenenRowVersion = string.IsNullOrEmpty(dto.RowVersion) ? [] : Convert.FromBase64String(dto.RowVersion);
        }
        catch (FormatException)
        {
            throw new EBelgeKurumPolitikaConcurrencyException();
        }

        var kurumVarMi = await _dbContext.Set<Kurum>().AnyAsync(k => k.Id == kurumId && !k.IsDeleted, cancellationToken);
        if (!kurumVarMi)
        {
            throw new BaseException("Kurum bulunamadı.", 404);
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var mevcut = await _dbContext.Set<KurumEBelgePolitikasi>()
            .FirstOrDefaultAsync(p => p.KurumId == kurumId, cancellationToken);

        if (mevcut is null)
        {
            if (beklenenRowVersion.Length != 0)
            {
                await tx.RollbackAsync(cancellationToken);
                throw new EBelgeKurumPolitikaConcurrencyException();
            }

            ValidateHedefDurum(dto.EntegrasyonYontemi, dto.AktifMi, dto.AktivasyonYerelTarihi, hariciSistemKodu);

            var yeniPolitika = new KurumEBelgePolitikasi
            {
                KurumId = kurumId,
                EntegrasyonYontemi = dto.EntegrasyonYontemi,
                AktifMi = dto.AktifMi,
                AktivasyonYerelTarihi = dto.AktivasyonYerelTarihi,
                HariciSistemKodu = hariciSistemKodu,
                PolitikaSurumu = 1,
            };

            _dbContext.Add(yeniPolitika);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _dbContext.Add(new KurumEBelgePolitikaRevizyonu
            {
                KurumId = kurumId,
                KurumEBelgePolitikasiId = yeniPolitika.Id,
                EskiSurum = 0,
                YeniSurum = 1,
                EskiYontem = EBelgeEntegrasyonYontemi.Yapilandirilmadi,
                YeniYontem = yeniPolitika.EntegrasyonYontemi,
                EskiAktifMi = false,
                YeniAktifMi = yeniPolitika.AktifMi,
                DegisiklikZamaniUtc = nowUtc,
                DegisiklikNedeni = degisiklikNedeni,
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return yeniPolitika;
        }

        if (!mevcut.RowVersion.AsSpan().SequenceEqual(beklenenRowVersion))
        {
            await tx.RollbackAsync(cancellationToken);
            throw new EBelgeKurumPolitikaConcurrencyException();
        }

        var yontemDegisiyorMu = mevcut.EntegrasyonYontemi != dto.EntegrasyonYontemi;
        var sadeceDeaktivasyon = mevcut.AktifMi && !dto.AktifMi && !yontemDegisiyorMu;

        // Görev md.15 "Her zaman izin verilebilecek işlem: Aktif -> Pasif" - acil kill switch,
        // pending iş VARSA BİLE her zaman uygulanır. Yöntem DEĞİŞİYORSA (AktifMi hedefinden
        // BAĞIMSIZ olarak) pending iş kontrolü uygulanır.
        if (yontemDegisiyorMu)
        {
            var pendingIsVarMi = await PendingIsVarMiAsync(kurumId, cancellationToken);
            if (pendingIsVarMi)
            {
                await tx.RollbackAsync(cancellationToken);
                throw new EBelgeKurumPolitikaDegisiklikEngellendiException();
            }
        }

        if (!sadeceDeaktivasyon)
        {
            ValidateHedefDurum(dto.EntegrasyonYontemi, dto.AktifMi, dto.AktivasyonYerelTarihi, hariciSistemKodu);
        }

        var eskiSurum = mevcut.PolitikaSurumu;
        var eskiYontem = mevcut.EntegrasyonYontemi;
        var eskiAktifMi = mevcut.AktifMi;

        mevcut.EntegrasyonYontemi = dto.EntegrasyonYontemi;
        mevcut.AktifMi = dto.AktifMi;
        mevcut.AktivasyonYerelTarihi = dto.AktivasyonYerelTarihi;
        mevcut.HariciSistemKodu = hariciSistemKodu;
        mevcut.PolitikaSurumu = eskiSurum + 1;

        _dbContext.Add(new KurumEBelgePolitikaRevizyonu
        {
            KurumId = kurumId,
            KurumEBelgePolitikasiId = mevcut.Id,
            EskiSurum = eskiSurum,
            YeniSurum = mevcut.PolitikaSurumu,
            EskiYontem = eskiYontem,
            YeniYontem = mevcut.EntegrasyonYontemi,
            EskiAktifMi = eskiAktifMi,
            YeniAktifMi = mevcut.AktifMi,
            DegisiklikZamaniUtc = nowUtc,
            DegisiklikNedeni = degisiklikNedeni,
        });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(cancellationToken);
            throw new EBelgeKurumPolitikaConcurrencyException();
        }

        await tx.CommitAsync(cancellationToken);
        return mevcut;
    }

    /// <summary>
    /// Görev md.15 - "Pasif -> Aktif" (VEYA aktif kalırken yöntem/tarih/harici kod DEĞİŞTİRİLİRKEN)
    /// uygulanan tam doğrulama. Yalnız `dto.AktifMi=false` hedeflendiğinde (deaktivasyon) BU
    /// metod hiç ÇAĞRILMAZ (bkz. çağıran, `sadeceDeaktivasyon`).
    /// </summary>
    private void ValidateHedefDurum(EBelgeEntegrasyonYontemi yontem, bool aktifMi, DateTime? aktivasyonYerelTarihi, string? hariciSistemKodu)
    {
        if (!aktifMi)
        {
            // Pasif hedefleniyor (ilk oluşturmada dahi olsa) - yöntem/tarih/harici-kod içerik
            // kuralları henüz GEÇERLİ değildir; yalnız temel format kontrolü yapılır.
            return;
        }

        var yetenekler = _yetenekSaglayici.Getir(yontem);
        if (!yetenekler.OperasyonelMi)
        {
            throw new EBelgeKurumPolitikaEngelliException(EBelgeKurumPolitikaKararNedeni.YontemHenuzDesteklenmiyor);
        }

        if (aktivasyonYerelTarihi is null)
        {
            throw new BaseException("Aktivasyon tarihi zorunludur.", 400);
        }

        var globalTarih = GlobalAktivasyonTarihi();
        if (globalTarih is { } g && DateOnly.FromDateTime(aktivasyonYerelTarihi.Value) < g)
        {
            throw new BaseException(
                $"Kurum aktivasyon tarihi, global aktivasyon tarihinden ({g:yyyy-MM-dd}) önce olamaz.", 400);
        }

        switch (yontem)
        {
            case EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi:
                if (string.IsNullOrWhiteSpace(hariciSistemKodu))
                {
                    throw new BaseException("HariciMuhasebeSistemi yöntemi için HariciSistemKodu zorunludur.", 400);
                }

                break;

            case EBelgeEntegrasyonYontemi.Kullanilmayacak:
            case EBelgeEntegrasyonYontemi.GibPortal:
                if (!string.IsNullOrWhiteSpace(hariciSistemKodu))
                {
                    throw new BaseException($"{yontem} yöntemi için HariciSistemKodu boş olmalıdır.", 400);
                }

                break;
        }
    }

    /// <summary>
    /// Global aktivasyon tarihini (`EBelgeProcessingOptions.NotBeforeLocalDate`) SAF bir tarih
    /// KARŞILAŞTIRMASI için PARSE eder - global aktivasyon KARAR ALGORİTMASI (Enabled/timezone/
    /// Active-Disabled sınıflandırması) BURADA TEKRARLANMAZ/YENİDEN YAZILMAZ (bkz. görev md.8,
    /// "ayrı algoritmayla yeniden yazma") - yalnız iki tarihi (kurum vs global) karşılaştırmak
    /// için gereken TEK SATIRLIK parse işlemi tekrarlanır.
    /// </summary>
    private DateOnly? GlobalAktivasyonTarihi() =>
        DateOnly.TryParseExact(_processingOptions.NotBeforeLocalDate, DateFormat, out var tarih) ? tarih : null;

    private async Task<bool> PendingIsVarMiAsync(int kurumId, CancellationToken cancellationToken)
    {
        var nonTerminalKayitVarMi = await _dbContext.Set<EBelgeKaydi>()
            .AnyAsync(k => k.KurumId == kurumId &&
                (k.Durum == EBelgeKaydiDurumu.SnapshotHazir || k.Durum == EBelgeKaydiDurumu.UnsignedUblHazir),
                cancellationToken);

        if (nonTerminalKayitVarMi)
        {
            return true;
        }

        return await _dbContext.Set<EBelgeOutboxMesaji>()
            .AnyAsync(o => o.KurumId == kurumId &&
                (o.Durum == EBelgeOutboxDurumu.Bekliyor ||
                 o.Durum == EBelgeOutboxDurumu.Isleniyor ||
                 (o.Durum == EBelgeOutboxDurumu.Hata && o.SonrakiDenemeZamaniUtc != null)),
                cancellationToken);
    }

    private static string? NormalizeHariciSistemKodu(string? deger)
    {
        var trimmed = deger?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > MaxHariciSistemKoduUzunluk)
        {
            throw new BaseException($"HariciSistemKodu en fazla {MaxHariciSistemKoduUzunluk} karakter olabilir.", 400);
        }

        return trimmed;
    }
}
