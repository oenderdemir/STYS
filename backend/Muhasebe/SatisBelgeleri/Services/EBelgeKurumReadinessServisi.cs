using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using STYS.Infrastructure.EntityFramework;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// Faz 2B.11 görev md.31 - kurumun e-belge işleme HAZIRLIĞINI hesaplayan TEK, otoriter servis.
/// Business logic (yöntem operasyonel mi, hangi yöntemde snapshot/unsigned-UBL/imza gerekir,
/// global kapı açık mı, aktivasyon tarihi geldi mi, satıcı verisi tamam mı) BURADA yaşar -
/// controller'a YAZILMAZ, frontend'de TEKRARLANMAZ (görev md.7). Mevcut
/// <see cref="IEBelgeKurumPolitikaYonetimServisi"/>/<see cref="IEBelgeYontemYetenekSaglayici"/>/
/// <see cref="IEBelgeProcessingActivationGate"/>/<see cref="IEBelgeSigningActivationGate"/>
/// yapılarını BİR ARAYA GETİRİR - hiçbirinin algoritmasını İKİNCİ KEZ YAZMAZ.
/// </summary>
public interface IEBelgeKurumReadinessService
{
    Task<KurumEBelgeReadinessDto> GetirAsync(int kurumId, CancellationToken cancellationToken = default);
}

public sealed class EBelgeKurumReadinessService : IEBelgeKurumReadinessService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IEBelgeYontemYetenekSaglayici _yetenekSaglayici;
    private readonly IEBelgeProcessingActivationGate _globalGate;
    private readonly IEBelgeSigningActivationGate _signingGate;
    private readonly TimeProvider _timeProvider;
    private readonly EBelgeProcessingOptions _processingOptions;
    private readonly EBelgeUblOptions _ublOptions;

    private static readonly EBelgeEntegrasyonYontemi[] TumYontemler =
    [
        EBelgeEntegrasyonYontemi.Kullanilmayacak,
        EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi,
        EBelgeEntegrasyonYontemi.GibPortal,
        EBelgeEntegrasyonYontemi.OzelEntegrator,
        EBelgeEntegrasyonYontemi.DogrudanGib,
    ];

    public EBelgeKurumReadinessService(
        StysAppDbContext dbContext,
        IEBelgeYontemYetenekSaglayici yetenekSaglayici,
        IEBelgeProcessingActivationGate globalGate,
        IEBelgeSigningActivationGate signingGate,
        TimeProvider timeProvider,
        IOptions<EBelgeProcessingOptions> processingOptions,
        IOptions<EBelgeUblOptions> ublOptions)
    {
        _dbContext = dbContext;
        _yetenekSaglayici = yetenekSaglayici;
        _globalGate = globalGate;
        _signingGate = signingGate;
        _timeProvider = timeProvider;
        _processingOptions = processingOptions.Value;
        _ublOptions = ublOptions.Value;
    }

    public async Task<KurumEBelgeReadinessDto> GetirAsync(int kurumId, CancellationToken cancellationToken = default)
    {
        if (kurumId <= 0)
        {
            throw new BaseException("KurumId pozitif olmalıdır.", 400);
        }

        var politika = await _dbContext.Set<KurumEBelgePolitikasi>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.KurumId == kurumId, cancellationToken);

        var kurum = await _dbContext.Set<Kurum>()
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == kurumId && !k.IsDeleted, cancellationToken);

        var entegrasyonYontemi = politika?.EntegrasyonYontemi ?? EBelgeEntegrasyonYontemi.Yapilandirilmadi;
        var politikaYapilandirilmisMi = politika is not null && politika.EntegrasyonYontemi != EBelgeEntegrasyonYontemi.Yapilandirilmadi;
        var politikaAktifMi = politika?.AktifMi ?? false;
        var yetenekler = _yetenekSaglayici.Getir(entegrasyonYontemi);

        var globalKarar = _globalGate.Evaluate();

        // Faz 2B.10.1 görev md.4'teki AYNI desen - "aktivasyon tarihi geldi mi" sorusu burada
        // BUGÜNÜN Türkiye yerel tarihine göre sorulur (bir belge tarihine göre DEĞİL - readiness
        // "şu an" sorusudur).
        var buguneTrt = TurkeyTimeZoneHelper.UtcdenTurkiyeYereleCevir(_timeProvider.GetUtcNow().UtcDateTime).Date;
        var kurumAktivasyonTarihiGelmisMi = politika?.AktivasyonYerelTarihi is { } aktivasyonTarihi && aktivasyonTarihi.Date <= buguneTrt;

        var eksikSaticiAlanlari = new List<string>();
        if (string.IsNullOrWhiteSpace(kurum?.VergiNo)) eksikSaticiAlanlari.Add(EBelgeKurumReadinessKodlari.VergiNo);
        if (string.IsNullOrWhiteSpace(kurum?.VergiDairesi)) eksikSaticiAlanlari.Add(EBelgeKurumReadinessKodlari.VergiDairesi);
        if (string.IsNullOrWhiteSpace(kurum?.Adres)) eksikSaticiAlanlari.Add(EBelgeKurumReadinessKodlari.Adres);
        if (string.IsNullOrWhiteSpace(kurum?.Ilce)) eksikSaticiAlanlari.Add(EBelgeKurumReadinessKodlari.Ilce);
        if (string.IsNullOrWhiteSpace(kurum?.Il)) eksikSaticiAlanlari.Add(EBelgeKurumReadinessKodlari.Il);
        var saticiAnaVerileriHazirMi = eksikSaticiAlanlari.Count == 0;

        // Faz 2B.11 görev md.30 - signing gate yalnız bu yöntem GERÇEKTEN yerel imza GEREKTİRİYORSA
        // readiness'e KATILIR. Şu an hiçbir üretim yönteminin (GibPortal dahil) YerelImzaOlustur=true
        // OLMADIĞINI biliyoruz (bkz. EBelgeYontemYetenekSaglayici) - ama bu alan yöntem MATRİSİNDEN
        // TÜRETİLDİĞİNDEN, ileride bir yöntem yerel imza kazanırsa KOD DEĞİŞİKLİĞİ GEREKMEDEN doğru
        // davranır.
        var signingGateUygulanabilirMi = yetenekler.YerelImzaOlustur;
        var signingSuAnMumkunMu = _signingGate.CanSignNow();

        // Faz 2B.11.1 görev md.1 - UBL feature flag'i (EBelgeUblOptions.Enabled), signing gate İLE
        // AYNI desende: yalnız yöntem GERÇEKTEN yerel unsigned UBL GEREKTİRİYORSA (bugün yalnız
        // GibPortal) readiness'e KATILIR. Kullanilmayacak/HariciMuhasebeSistemi gibi yerel UBL
        // GEREKTİRMEYEN yöntemlerde bu bayrağın değeri readiness'i HİÇ ETKİLEMEZ.
        var ublFeatureUygulanabilirMi = yetenekler.YerelUnsignedUblOlustur;
        var ublFeatureAktifMi = _ublOptions.Enabled;

        var (islemeHazirMi, blokajNedenleri) = DegerlendirIslemeHazirlik(
            politikaYapilandirilmisMi, politikaAktifMi, entegrasyonYontemi, yetenekler,
            globalKarar.CanProcess, kurumAktivasyonTarihiGelmisMi, saticiAnaVerileriHazirMi,
            ublFeatureUygulanabilirMi, ublFeatureAktifMi, signingGateUygulanabilirMi, signingSuAnMumkunMu);

        return new KurumEBelgeReadinessDto
        {
            KurumId = kurumId,
            PolitikaYapilandirilmisMi = politikaYapilandirilmisMi,
            PolitikaAktifMi = politikaAktifMi,
            EntegrasyonYontemi = entegrasyonYontemi,
            YontemOperasyonelMi = yetenekler.OperasyonelMi,
            KurumAktivasyonYerelTarihi = politika?.AktivasyonYerelTarihi,
            GlobalMinimumAktivasyonYerelTarihi = GlobalMinimumAktivasyonYerelTarihiOku(),
            GlobalProcessingAktifMi = globalKarar.CanProcess,
            GlobalProcessingDurumu = globalKarar.Reason.ToString(),
            YerelSnapshotGerekliMi = yetenekler.YerelSnapshotOlustur,
            YerelUnsignedUblGerekliMi = yetenekler.YerelUnsignedUblOlustur,
            UblFeatureUygulanabilirMi = ublFeatureUygulanabilirMi,
            UblFeatureAktifMi = ublFeatureAktifMi,
            YerelImzaGerekliMi = yetenekler.YerelImzaOlustur,
            OtomatikGonderimGerekliMi = yetenekler.OtomatikGonderimYap,
            SaticiAnaVerileriHazirMi = saticiAnaVerileriHazirMi,
            EksikSaticiAlanlari = eksikSaticiAlanlari,
            SigningGateUygulanabilirMi = signingGateUygulanabilirMi,
            SigningSuAnMumkunMu = signingSuAnMumkunMu,
            IslemeHazirMi = islemeHazirMi,
            BlokajNedenleri = blokajNedenleri,
            Yontemler = TumYontemler.Select(YontemReadinessUret).ToList(),
        };
    }

    /// <summary>
    /// Faz 2B.11 görev md.28 - yöntem bazlı `IslemeHazirMi`/`BlokajNedenleri` hesaplaması. Karar
    /// sırası, mevcut <see cref="EBelgeKurumPolitikaServisi.DegerlendirAsync"/> İLE TUTARLI bir
    /// önceliklendirme izler (politika yapılandırma/aktiflik ÖNCE, yöntem-özgü kurallar SONRA) -
    /// ama BU metot invoice-time bir karar ÜRETMEZ, yalnız GÖRÜNTÜLEME amaçlı bir "hazırlık"
    /// anlık görüntüsü döner.
    /// </summary>
    private static (bool IslemeHazirMi, IReadOnlyList<string> BlokajNedenleri) DegerlendirIslemeHazirlik(
        bool politikaYapilandirilmisMi, bool politikaAktifMi, EBelgeEntegrasyonYontemi entegrasyonYontemi,
        EBelgeYontemYetenekleri yetenekler, bool globalProcessingAktifMi, bool kurumAktivasyonTarihiGelmisMi,
        bool saticiAnaVerileriHazirMi, bool ublFeatureUygulanabilirMi, bool ublFeatureAktifMi,
        bool signingGateUygulanabilirMi, bool signingSuAnMumkunMu)
    {
        var nedenler = new List<string>();
        bool islemeHazirMi;

        if (!politikaYapilandirilmisMi)
        {
            nedenler.Add(EBelgeKurumReadinessKodlari.PolitikaYapilandirilmadi);
            islemeHazirMi = false;
        }
        else if (!politikaAktifMi)
        {
            nedenler.Add(EBelgeKurumReadinessKodlari.PolitikaPasif);
            islemeHazirMi = false;
        }
        else
        {
            switch (entegrasyonYontemi)
            {
                // Görev md.28 - bu iki yöntem YEREL e-belge pipeline'ı GEREKTİRMEZ; politika
                // yapılandırılmış+aktif olduğunda BAŞKA HİÇBİR koşul yoktur - "Hazır değil" (kırmızı)
                // OLARAK GÖSTERİLMEZ.
                case EBelgeEntegrasyonYontemi.Kullanilmayacak:
                case EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi:
                    islemeHazirMi = true;
                    break;

                case EBelgeEntegrasyonYontemi.GibPortal:
                    if (!yetenekler.OperasyonelMi)
                    {
                        nedenler.Add(EBelgeKurumReadinessKodlari.YontemDesteklenmiyor);
                    }

                    if (!globalProcessingAktifMi)
                    {
                        nedenler.Add(EBelgeKurumReadinessKodlari.GlobalIslemKapisiKapali);
                    }

                    if (!kurumAktivasyonTarihiGelmisMi)
                    {
                        nedenler.Add(EBelgeKurumReadinessKodlari.AktivasyonTarihiGelmedi);
                    }

                    if (!saticiAnaVerileriHazirMi)
                    {
                        nedenler.Add(EBelgeKurumReadinessKodlari.SaticiAnaVerileriEksik);
                    }

                    // Faz 2B.11.1 görev md.2 - UBL feature flag (EBelgeUblOptions.Enabled) kapalıyken
                    // GİB Portal ASLA "Hazır" görünmemeli; runtime'ın (SatisBelgesiService.FaturaKesAsync)
                    // AYNI durumda fail-closed reddettiği İLE TUTARLI (bkz. "Runtime da fail-closed
                    // olmalı"). Görev md.2 - signing gate GİB Portal readiness'ini BLOKE ETMEZ; bu
                    // yöntem yerel imza YAPMAZ (YerelImzaOlustur=false).
                    if (ublFeatureUygulanabilirMi && !ublFeatureAktifMi)
                    {
                        nedenler.Add(EBelgeKurumReadinessKodlari.UblFeatureKapali);
                    }

                    islemeHazirMi = nedenler.Count == 0;
                    break;

                // Görev md.28 - adapter operasyonel DEĞİLSE ("henüz uygulanmadı") diğer TÜM
                // koşullardan BAĞIMSIZ olarak hazır DEĞİLDİR.
                case EBelgeEntegrasyonYontemi.OzelEntegrator:
                case EBelgeEntegrasyonYontemi.DogrudanGib:
                default:
                    nedenler.Add(EBelgeKurumReadinessKodlari.YontemDesteklenmiyor);
                    islemeHazirMi = false;
                    break;
            }
        }

        // Görev md.30 - signing gate yalnız YEREL İMZA GERÇEKTEN gerektiğinde (bkz.
        // signingGateUygulanabilirMi) VE diğer tüm koşullar zaten sağlandığında değerlendirmeye
        // katılır. Bugün İÇİN hiçbir yöntem YerelImzaOlustur=true OLMADIĞINDAN bu dal PRATİKTE
        // tetiklenmez - ama matristen türetildiği İçin ileride bir yöntem yerel imza KAZANIRSA kod
        // değişikliği GEREKMEDEN doğru davranır.
        if (islemeHazirMi && signingGateUygulanabilirMi && !signingSuAnMumkunMu)
        {
            nedenler.Add(EBelgeKurumReadinessKodlari.SigningGateKapali);
            islemeHazirMi = false;
        }

        return (islemeHazirMi, nedenler);
    }

    private EBelgeYontemReadinessDto YontemReadinessUret(EBelgeEntegrasyonYontemi yontem)
    {
        var yetenekler = _yetenekSaglayici.Getir(yontem);
        return new EBelgeYontemReadinessDto
        {
            Yontem = yontem,
            OperasyonelMi = yetenekler.OperasyonelMi,
            HariciSistemKoduGerekliMi = yontem == EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi,
            YerelSnapshotOlustur = yetenekler.YerelSnapshotOlustur,
            YerelUnsignedUblOlustur = yetenekler.YerelUnsignedUblOlustur,
            YerelImzaOlustur = yetenekler.YerelImzaOlustur,
            OtomatikGonderimYap = yetenekler.OtomatikGonderimYap,
        };
    }

    /// <summary>Faz 2B.11 görev md.13 - global minimum aktivasyon tarihi, YALNIZ GÖRÜNTÜLEME amaçlı; parse edilemezse (fail-closed config) `null` döner - bu durumda zaten `GlobalProcessingDurumu=InvalidDateConfiguration` ile ayrıca yansıtılır.</summary>
    private DateTime? GlobalMinimumAktivasyonYerelTarihiOku() =>
        DateOnly.TryParseExact(_processingOptions.NotBeforeLocalDate, "yyyy-MM-dd", out var parsed)
            ? parsed.ToDateTime(TimeOnly.MinValue)
            : null;
}
