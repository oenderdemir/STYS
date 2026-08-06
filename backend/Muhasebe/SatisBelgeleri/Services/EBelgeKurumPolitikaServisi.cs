using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// Faz 2B.10 - <see cref="IEBelgeKurumPolitikaServisi.DegerlendirAsync"/>'in type-safe, immutable
/// sonucu. Hiçbir alan `null`/varsayılan bırakılmaz (`required`) - çağıran KARARIN TÜM
/// bağlamını (neden + yetenekler + hangi politika sürümü) her zaman ALIR.
/// </summary>
public sealed record EBelgeKurumPolitikaKarari
{
    public required bool IslemeIzinliMi { get; init; }

    public required EBelgeKurumPolitikaKararNedeni Neden { get; init; }

    public required EBelgeEntegrasyonYontemi EntegrasyonYontemi { get; init; }

    public required int? PolitikaId { get; init; }

    public required int? PolitikaSurumu { get; init; }

    public required EBelgeYontemYetenekleri Yetenekler { get; init; }

    public required DateTime KararZamaniUtc { get; init; }
}

/// <summary>
/// Faz 2B.10 - satış belgesi akışının ÖNÜNE konan, kurum bazlı fail-closed karar katmanı.
/// Karar sırası (bkz. görev md.3): (1) global feature flag, (2) global cutover/not-before,
/// (3) kurum politikası. Global değerlendirme (1+2) MEVCUT <see cref="IEBelgeProcessingActivationGate"/>
/// üzerinden yapılır - AYRI bir algoritma İLE YENİDEN YAZILMAZ; kurum politikası bu global kapıyı
/// HİÇBİR ZAMAN açamaz (global kapı kapalıyken bu servis HER ZAMAN fail-closed döner).
/// </summary>
public interface IEBelgeKurumPolitikaServisi
{
    /// <summary><paramref name="belgeTarihi"/> Türkiye yerel takvim tarihidir (saat bileşeni önemsizdir) - "şu an" DEĞİL, belgenin KENDİ tarihi kurumun aktivasyon tarihiyle karşılaştırılır (mevcut cutover-tarihi karşılaştırma deseniyle TUTARLI).</summary>
    Task<EBelgeKurumPolitikaKarari> DegerlendirAsync(int kurumId, DateTime belgeTarihi, CancellationToken cancellationToken = default);

    /// <summary>
    /// Faz 2B.10 görev md.14 - worker/outbox savunma katmanı İÇİN: bir EBelgeKaydi'ye bağlı
    /// immutable karardaki yetenek (ör. `YerelImzaOlustur`) HEM karar ANINDA true OLMALI HEM
    /// GÜNCEL kurum politikasında hâlâ true OLMALIDIR - biri EKSİKSE `false` döner. Karar kaydı
    /// hiç YOKSA (karar-öncesi/legacy outbox mesajı) GERİYE DÖNÜK UYUMLULUK için `true` döner
    /// (bkz. `EBelgeOutboxMesajIslemeService` XML doc'u).
    /// </summary>
    Task<bool> IslemHalaIzinliMiAsync(int kurumId, int eBelgeKaydiId, EBelgeOutboxIsTuru isTuru, CancellationToken cancellationToken = default);
}

public sealed class EBelgeKurumPolitikaServisi : IEBelgeKurumPolitikaServisi
{
    private static readonly EBelgeYontemYetenekleri BosYetenekler = new(false, false, false, false, false);

    private readonly StysAppDbContext _dbContext;
    private readonly IEBelgeProcessingActivationGate _globalGate;
    private readonly IEBelgeYontemYetenekSaglayici _yetenekSaglayici;
    private readonly TimeProvider _timeProvider;

    public EBelgeKurumPolitikaServisi(
        StysAppDbContext dbContext,
        IEBelgeProcessingActivationGate globalGate,
        IEBelgeYontemYetenekSaglayici yetenekSaglayici,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _globalGate = globalGate;
        _yetenekSaglayici = yetenekSaglayici;
        _timeProvider = timeProvider;
    }

    public async Task<EBelgeKurumPolitikaKarari> DegerlendirAsync(int kurumId, DateTime belgeTarihi, CancellationToken cancellationToken = default)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        // Karar sırası md.1-2: global feature flag + global cutover/not-before - MEVCUT gate,
        // AYRI algoritmayla YENİDEN YAZILMAZ.
        var globalKarar = _globalGate.Evaluate();
        if (globalKarar.Reason == EBelgeProcessingActivationReason.Disabled)
        {
            return Karar(false, EBelgeKurumPolitikaKararNedeni.GlobalKapali, EBelgeEntegrasyonYontemi.Yapilandirilmadi, null, null, BosYetenekler, nowUtc);
        }

        if (globalKarar.Reason != EBelgeProcessingActivationReason.Active)
        {
            // BeforeActivationDate / InvalidDateConfiguration / InvalidTimeZoneConfiguration -
            // hepsi fail-closed: kurum politikası bu kapıyı AÇAMAZ.
            return Karar(false, EBelgeKurumPolitikaKararNedeni.GlobalAktivasyonTarihiGelmedi, EBelgeEntegrasyonYontemi.Yapilandirilmadi, null, null, BosYetenekler, nowUtc);
        }

        // Karar sırası md.3: kurum politikası.
        var politika = await _dbContext.Set<KurumEBelgePolitikasi>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.KurumId == kurumId, cancellationToken);

        if (politika is null || politika.EntegrasyonYontemi == EBelgeEntegrasyonYontemi.Yapilandirilmadi)
        {
            // Görev md.9 - global süreç aktif olduktan SONRA "politika yok" SESSİZCE
            // Kullanilmayacak yorumlanmaz - açık, fail-closed bir neden döner.
            return Karar(false, EBelgeKurumPolitikaKararNedeni.PolitikaYapilandirilmadi, EBelgeEntegrasyonYontemi.Yapilandirilmadi, politika?.Id, politika?.PolitikaSurumu, BosYetenekler, nowUtc);
        }

        if (!Enum.IsDefined(politika.EntegrasyonYontemi))
        {
            return Karar(false, EBelgeKurumPolitikaKararNedeni.PolitikaGecersiz, politika.EntegrasyonYontemi, politika.Id, politika.PolitikaSurumu, BosYetenekler, nowUtc);
        }

        if (!politika.AktifMi)
        {
            return Karar(false, EBelgeKurumPolitikaKararNedeni.PolitikaPasif, politika.EntegrasyonYontemi, politika.Id, politika.PolitikaSurumu, BosYetenekler, nowUtc);
        }

        if (politika.AktivasyonYerelTarihi is null)
        {
            // AktifMi=true iken AktivasyonYerelTarihi'nin null OLMAMASI yönetim servisi
            // tarafından garanti edilir - buraya kadar geldiyse bu, veri bütünlüğü açısından
            // GEÇERSİZ bir durumdur (ör. elle DB düzenlemesi) - "tarih henüz gelmedi" DEĞİL.
            return Karar(false, EBelgeKurumPolitikaKararNedeni.PolitikaGecersiz, politika.EntegrasyonYontemi, politika.Id, politika.PolitikaSurumu, BosYetenekler, nowUtc);
        }

        if (politika.AktivasyonYerelTarihi.Value.Date > belgeTarihi.Date)
        {
            return Karar(false, EBelgeKurumPolitikaKararNedeni.KurumAktivasyonTarihiGelmedi, politika.EntegrasyonYontemi, politika.Id, politika.PolitikaSurumu, BosYetenekler, nowUtc);
        }

        var yetenekler = _yetenekSaglayici.Getir(politika.EntegrasyonYontemi);

        if (politika.EntegrasyonYontemi == EBelgeEntegrasyonYontemi.Kullanilmayacak)
        {
            // Görev md.9 "Açıkça Kullanılmayacak" - HATA DEĞİLDİR; yerel pipeline gerekmez.
            return Karar(false, EBelgeKurumPolitikaKararNedeni.YontemKullanilmayacak, politika.EntegrasyonYontemi, politika.Id, politika.PolitikaSurumu, yetenekler, nowUtc);
        }

        if (politika.EntegrasyonYontemi == EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi)
        {
            // Görev md.9 "Harici muhasebe sistemi" - HATA DEĞİLDİR; yerel pipeline gerekmez.
            return Karar(false, EBelgeKurumPolitikaKararNedeni.HariciSistemSorumlu, politika.EntegrasyonYontemi, politika.Id, politika.PolitikaSurumu, yetenekler, nowUtc);
        }

        if (!yetenekler.OperasyonelMi)
        {
            // OzelEntegrator/DogrudanGib - gerçek adapter YOKKEN fail-closed (görev md.9/24).
            return Karar(false, EBelgeKurumPolitikaKararNedeni.YontemHenuzDesteklenmiyor, politika.EntegrasyonYontemi, politika.Id, politika.PolitikaSurumu, BosYetenekler, nowUtc);
        }

        return Karar(true, EBelgeKurumPolitikaKararNedeni.Aktif, politika.EntegrasyonYontemi, politika.Id, politika.PolitikaSurumu, yetenekler, nowUtc);
    }

    public async Task<bool> IslemHalaIzinliMiAsync(int kurumId, int eBelgeKaydiId, EBelgeOutboxIsTuru isTuru, CancellationToken cancellationToken = default)
    {
        var karar = await _dbContext.Set<SatisBelgesiEBelgeKarari>()
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.EBelgeKaydiId == eBelgeKaydiId && k.KurumId == kurumId, cancellationToken);

        if (karar is null)
        {
            // Karar-öncesi/legacy outbox mesajı - bkz. arayüz XML doc'u, geriye dönük uyumluluk.
            return true;
        }

        var buguneTrt = TurkeyTimeZoneHelper.UtcdenTurkiyeYereleCevir(_timeProvider.GetUtcNow().UtcDateTime);
        var guncelKarar = await DegerlendirAsync(kurumId, buguneTrt, cancellationToken);

        return isTuru switch
        {
            EBelgeOutboxIsTuru.ArtefaktOlustur => karar.YerelSnapshotOlustur && karar.YerelUnsignedUblOlustur &&
                guncelKarar.Yetenekler.YerelSnapshotOlustur && guncelKarar.Yetenekler.YerelUnsignedUblOlustur,
            EBelgeOutboxIsTuru.UblImzala => karar.YerelImzaOlustur && guncelKarar.Yetenekler.YerelImzaOlustur,
            _ => true,
        };
    }

    private static EBelgeKurumPolitikaKarari Karar(
        bool islemeIzinliMi,
        EBelgeKurumPolitikaKararNedeni neden,
        EBelgeEntegrasyonYontemi yontem,
        int? politikaId,
        int? politikaSurumu,
        EBelgeYontemYetenekleri yetenekler,
        DateTime kararZamaniUtc) => new()
    {
        IslemeIzinliMi = islemeIzinliMi,
        Neden = neden,
        EntegrasyonYontemi = yontem,
        PolitikaId = politikaId,
        PolitikaSurumu = politikaSurumu,
        Yetenekler = yetenekler,
        KararZamaniUtc = kararZamaniUtc,
    };
}
