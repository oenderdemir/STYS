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

    /// <summary>Faz 2B.10.2 görev md.8 - politika satırı bulunduysa (`PolitikaId.HasValue`) karar ANINDAKİ `AktifMi` değeri; politika satırı YOKSA `false`. Karar sonrası, kilitli-satır KARŞILAŞTIRMASI İÇİN kullanılır (bkz. `SatisBelgesiService.FaturaKesAsync`).</summary>
    public required bool AktifMi { get; init; }

    /// <summary>Faz 2B.10.2 görev md.8 - politika satırı bulunduysa karar ANINDAKİ `AktivasyonYerelTarihi` değeri; YOKSA `null`.</summary>
    public required DateTime? AktivasyonYerelTarihi { get; init; }

    public required EBelgeYontemYetenekleri Yetenekler { get; init; }

    public required DateTime KararZamaniUtc { get; init; }
}

/// <summary>
/// Faz 2B.10.1 görev md.4 - <see cref="IEBelgeKurumPolitikaServisi.DegerlendirIslemUygunlugunuAsync"/>'in
/// type-safe, immutable sonucu. Ham kurum/müşteri/belge bilgisi TAŞIMAZ - yalnız politika
/// kimliği/sürümü VE nedeni (bkz. <see cref="EBelgeIslemPolitikaUygunlukNedeni"/>).
/// </summary>
public sealed record EBelgeIslemPolitikaUygunlukSonucu
{
    public required bool UygunMu { get; init; }

    public required EBelgeIslemPolitikaUygunlukNedeni Neden { get; init; }

    public required int? PolitikaId { get; init; }

    public required int? PolitikaSurumu { get; init; }
}

/// <summary>Faz 2B.10.1 görev md.4 - bir outbox işinin (claim SONRASI/commit ÖNCESİ) hâlâ izinli olup olmadığının type-safe ayrım kümesi.</summary>
public enum EBelgeIslemPolitikaUygunlukNedeni
{
    /// <summary>Karar + güncel politika + güncel yetenek matrisi ÜÇÜ de işlemi desteklemektedir.</summary>
    Uygun = 1,

    /// <summary>İmmutable `SatisBelgesiEBelgeKarari` bulunamadı - karar-öncesi/legacy kayıt (bkz. görev md.3, "fail-open DEĞİL, fail-closed").</summary>
    KararBulunamadi = 2,

    /// <summary>Karar var ama kurum İÇİN `KurumEBelgePolitikasi` satırı bulunamadı (silinmiş/hiç oluşturulmamış).</summary>
    PolitikaBulunamadi = 3,

    /// <summary>Politika bulundu ama `AktifMi=false` (kill switch tetiklenmiş).</summary>
    PolitikaPasif = 4,

    /// <summary>Güncel politikanın yöntemi, kararın alındığı ANDAKİ yöntemden FARKLI (kurum yöntem değiştirmiş).</summary>
    YontemDegisti = 5,

    /// <summary>Politika aktif/yöntem uyumlu ama kurumun aktivasyon tarihi (bugüne göre) henüz gelmedi.</summary>
    AktivasyonTarihiGelmedi = 6,

    /// <summary>Kararın KENDİSİ (karar anındaki snapshot) bu iş türü İÇİN gereken yeteneği taşımıyor (ör. UblImzala istenmiş ama karar YerelImzaOlustur=false).</summary>
    ImmutableYetenekYok = 7,

    /// <summary>Güncel yöntem yetenek matrisinde bu iş türü İÇİN gereken yetenek artık YOK (ör. yöntem OperasyonelMi=false'a düşürüldü).</summary>
    GuncelYontemDesteklenmiyor = 8,

    /// <summary>Karar kaydı BAŞKA bir kuruma ait (composite FK/tenant izolasyonu ihlali savunma kontrolü).</summary>
    TenantUyusmazligi = 9,
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
    /// Faz 2B.10.1 görev md.4/md.14 - worker/outbox savunma katmanı VE artifact/imza commit-öncesi
    /// kontrolleri İÇİN zengin, type-safe uygunluk sonucu. Karar kaydı hiç YOKSA (karar-öncesi/
    /// legacy outbox mesajı) ARTIK fail-closed (`KararBulunamadi`, `UygunMu=false`) döner - ÖNCEKİ
    /// `IslemHalaIzinliMiAsync`'in geriye dönük uyumluluk İÇİN `true` dönen fail-open davranışı
    /// KALDIRILDI (bkz. görev md.3, "legacy kayıtlar fail-closed"). Bugünkü Türkiye yerel tarihi
    /// (belgenin KENDİ tarihi DEĞİL) aktivasyon tarihi karşılaştırması İÇİN kullanılır - bu, işin
    /// hâlâ İZİNLİ olup OLMADIĞI şu ANDA sorulmaktadır.
    /// </summary>
    Task<EBelgeIslemPolitikaUygunlukSonucu> DegerlendirIslemUygunlugunuAsync(int kurumId, int eBelgeKaydiId, EBelgeOutboxIsTuru isTuru, CancellationToken cancellationToken = default);

    /// <summary>
    /// Faz 2B.10.2 görev md.3/md.4 - <see cref="DegerlendirIslemUygunlugunuAsync"/> İLE AYNI
    /// uygunluk algoritmasını (İKİNCİ KEZ YAZILMADAN, ortak bir çekirdek üzerinden) kullanır, ama
    /// güncel politikayı KENDİ SORGUSUYLA OKUMAZ - ÖNCEDEN <see cref="IEBelgeKurumPolitikaTransactionGuard.KilitleVeOkuAsync"/>
    /// ile KİLİTLENMİŞ bir anlık görüntüyü PARAMETRE olarak alır. Bu, commit-öncesi kontrol İLE
    /// artifact/SignedReady yazımı ARASINDA politikanın DEĞİŞEMEYECEĞİNİ garanti eder (bkz. görev
    /// md.3/md.4, "TOCTOU yarışı").
    /// </summary>
    Task<EBelgeIslemPolitikaUygunlukSonucu> DegerlendirIslemUygunlugunuKilitliAsync(
        int kurumId, int eBelgeKaydiId, EBelgeOutboxIsTuru isTuru, EBelgeKilitliPolitikaSnapshot? kilitliPolitika, CancellationToken cancellationToken = default);
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
            return Karar(false, EBelgeKurumPolitikaKararNedeni.GlobalKapali, EBelgeEntegrasyonYontemi.Yapilandirilmadi, null, null, false, null, BosYetenekler, nowUtc);
        }

        if (globalKarar.Reason != EBelgeProcessingActivationReason.Active)
        {
            // BeforeActivationDate / InvalidDateConfiguration / InvalidTimeZoneConfiguration -
            // hepsi fail-closed: kurum politikası bu kapıyı AÇAMAZ.
            return Karar(false, EBelgeKurumPolitikaKararNedeni.GlobalAktivasyonTarihiGelmedi, EBelgeEntegrasyonYontemi.Yapilandirilmadi, null, null, false, null, BosYetenekler, nowUtc);
        }

        // Karar sırası md.3: kurum politikası.
        var politika = await _dbContext.Set<KurumEBelgePolitikasi>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.KurumId == kurumId, cancellationToken);

        if (politika is null || politika.EntegrasyonYontemi == EBelgeEntegrasyonYontemi.Yapilandirilmadi)
        {
            // Görev md.9 - global süreç aktif olduktan SONRA "politika yok" SESSİZCE
            // Kullanilmayacak yorumlanmaz - açık, fail-closed bir neden döner.
            return Karar(false, EBelgeKurumPolitikaKararNedeni.PolitikaYapilandirilmadi, EBelgeEntegrasyonYontemi.Yapilandirilmadi, politika?.Id, politika?.PolitikaSurumu, politika?.AktifMi ?? false, politika?.AktivasyonYerelTarihi, BosYetenekler, nowUtc);
        }

        if (!Enum.IsDefined(politika.EntegrasyonYontemi))
        {
            return Karar(false, EBelgeKurumPolitikaKararNedeni.PolitikaGecersiz, politika.EntegrasyonYontemi, politika.Id, politika.PolitikaSurumu, politika.AktifMi, politika.AktivasyonYerelTarihi, BosYetenekler, nowUtc);
        }

        if (!politika.AktifMi)
        {
            return Karar(false, EBelgeKurumPolitikaKararNedeni.PolitikaPasif, politika.EntegrasyonYontemi, politika.Id, politika.PolitikaSurumu, politika.AktifMi, politika.AktivasyonYerelTarihi, BosYetenekler, nowUtc);
        }

        if (politika.AktivasyonYerelTarihi is null)
        {
            // AktifMi=true iken AktivasyonYerelTarihi'nin null OLMAMASI yönetim servisi
            // tarafından garanti edilir - buraya kadar geldiyse bu, veri bütünlüğü açısından
            // GEÇERSİZ bir durumdur (ör. elle DB düzenlemesi) - "tarih henüz gelmedi" DEĞİL.
            return Karar(false, EBelgeKurumPolitikaKararNedeni.PolitikaGecersiz, politika.EntegrasyonYontemi, politika.Id, politika.PolitikaSurumu, politika.AktifMi, politika.AktivasyonYerelTarihi, BosYetenekler, nowUtc);
        }

        if (politika.AktivasyonYerelTarihi.Value.Date > belgeTarihi.Date)
        {
            return Karar(false, EBelgeKurumPolitikaKararNedeni.KurumAktivasyonTarihiGelmedi, politika.EntegrasyonYontemi, politika.Id, politika.PolitikaSurumu, politika.AktifMi, politika.AktivasyonYerelTarihi, BosYetenekler, nowUtc);
        }

        var yetenekler = _yetenekSaglayici.Getir(politika.EntegrasyonYontemi);

        if (politika.EntegrasyonYontemi == EBelgeEntegrasyonYontemi.Kullanilmayacak)
        {
            // Görev md.9 "Açıkça Kullanılmayacak" - HATA DEĞİLDİR; yerel pipeline gerekmez.
            return Karar(false, EBelgeKurumPolitikaKararNedeni.YontemKullanilmayacak, politika.EntegrasyonYontemi, politika.Id, politika.PolitikaSurumu, politika.AktifMi, politika.AktivasyonYerelTarihi, yetenekler, nowUtc);
        }

        if (politika.EntegrasyonYontemi == EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi)
        {
            // Görev md.9 "Harici muhasebe sistemi" - HATA DEĞİLDİR; yerel pipeline gerekmez.
            return Karar(false, EBelgeKurumPolitikaKararNedeni.HariciSistemSorumlu, politika.EntegrasyonYontemi, politika.Id, politika.PolitikaSurumu, politika.AktifMi, politika.AktivasyonYerelTarihi, yetenekler, nowUtc);
        }

        if (!yetenekler.OperasyonelMi)
        {
            // OzelEntegrator/DogrudanGib - gerçek adapter YOKKEN fail-closed (görev md.9/24).
            return Karar(false, EBelgeKurumPolitikaKararNedeni.YontemHenuzDesteklenmiyor, politika.EntegrasyonYontemi, politika.Id, politika.PolitikaSurumu, politika.AktifMi, politika.AktivasyonYerelTarihi, BosYetenekler, nowUtc);
        }

        return Karar(true, EBelgeKurumPolitikaKararNedeni.Aktif, politika.EntegrasyonYontemi, politika.Id, politika.PolitikaSurumu, politika.AktifMi, politika.AktivasyonYerelTarihi, yetenekler, nowUtc);
    }

    public Task<EBelgeIslemPolitikaUygunlukSonucu> DegerlendirIslemUygunlugunuAsync(
        int kurumId, int eBelgeKaydiId, EBelgeOutboxIsTuru isTuru, CancellationToken cancellationToken = default) =>
        DegerlendirIslemUygunlugunuCoreAsync(
            kurumId, eBelgeKaydiId, isTuru,
            politikaGetir: async ct =>
            {
                var p = await _dbContext.Set<KurumEBelgePolitikasi>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.KurumId == kurumId, ct);
                return p is null ? null : PolitikaSnapshotUret(p);
            },
            cancellationToken);

    public Task<EBelgeIslemPolitikaUygunlukSonucu> DegerlendirIslemUygunlugunuKilitliAsync(
        int kurumId, int eBelgeKaydiId, EBelgeOutboxIsTuru isTuru, EBelgeKilitliPolitikaSnapshot? kilitliPolitika, CancellationToken cancellationToken = default) =>
        DegerlendirIslemUygunlugunuCoreAsync(kurumId, eBelgeKaydiId, isTuru, _ => Task.FromResult(kilitliPolitika), cancellationToken);

    /// <summary>
    /// Faz 2B.10.2 görev md.3/md.4 - `DegerlendirIslemUygunlugunuAsync` (kendi unlocked
    /// sorgusuyla) VE `DegerlendirIslemUygunlugunuKilitliAsync` (ÖNCEDEN kilitlenmiş bir anlık
    /// görüntüyle) arasında PAYLAŞILAN TEK karar çekirdeği - politika okuma STRATEJİSİ
    /// (`politikaGetir` delegesi) HARİÇ, uygunluk ALGORİTMASI İKİ YERDE YENİDEN YAZILMAZ.
    /// </summary>
    private async Task<EBelgeIslemPolitikaUygunlukSonucu> DegerlendirIslemUygunlugunuCoreAsync(
        int kurumId, int eBelgeKaydiId, EBelgeOutboxIsTuru isTuru,
        Func<CancellationToken, Task<EBelgeKilitliPolitikaSnapshot?>> politikaGetir,
        CancellationToken cancellationToken)
    {
        var karar = await _dbContext.Set<SatisBelgesiEBelgeKarari>()
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.EBelgeKaydiId == eBelgeKaydiId && k.KurumId == kurumId, cancellationToken);

        if (karar is null)
        {
            // Faz 2B.10.1 görev md.3 - karar-öncesi/legacy outbox mesajı ARTIK fail-open "true"
            // DEĞİL, açıkça fail-closed'dır. Otomatik bir yöntem (ör. DogrudanGib) VARSAYILMAZ.
            return Uygunluk(false, EBelgeIslemPolitikaUygunlukNedeni.KararBulunamadi, null, null);
        }

        if (karar.KurumId != kurumId)
        {
            // Composite tenant-aware FK bunu zaten yapısal olarak engeller - savunma amaçlı runtime kontrolü.
            return Uygunluk(false, EBelgeIslemPolitikaUygunlukNedeni.TenantUyusmazligi, karar.KurumEBelgePolitikasiId, karar.PolitikaSurumu);
        }

        var kararYetenekVar = isTuru switch
        {
            EBelgeOutboxIsTuru.ArtefaktOlustur => karar.YerelSnapshotOlustur && karar.YerelUnsignedUblOlustur,
            EBelgeOutboxIsTuru.UblImzala => karar.YerelImzaOlustur,
            _ => false,
        };

        if (!kararYetenekVar)
        {
            return Uygunluk(false, EBelgeIslemPolitikaUygunlukNedeni.ImmutableYetenekYok, karar.KurumEBelgePolitikasiId, karar.PolitikaSurumu);
        }

        var politika = await politikaGetir(cancellationToken);

        if (politika is null)
        {
            return Uygunluk(false, EBelgeIslemPolitikaUygunlukNedeni.PolitikaBulunamadi, karar.KurumEBelgePolitikasiId, karar.PolitikaSurumu);
        }

        if (!politika.AktifMi)
        {
            return Uygunluk(false, EBelgeIslemPolitikaUygunlukNedeni.PolitikaPasif, politika.Id, politika.PolitikaSurumu);
        }

        if (politika.EntegrasyonYontemi != karar.EntegrasyonYontemi)
        {
            return Uygunluk(false, EBelgeIslemPolitikaUygunlukNedeni.YontemDegisti, politika.Id, politika.PolitikaSurumu);
        }

        var buguneTrt = TurkeyTimeZoneHelper.UtcdenTurkiyeYereleCevir(_timeProvider.GetUtcNow().UtcDateTime);
        if (politika.AktivasyonYerelTarihi is null || politika.AktivasyonYerelTarihi.Value.Date > buguneTrt.Date)
        {
            return Uygunluk(false, EBelgeIslemPolitikaUygunlukNedeni.AktivasyonTarihiGelmedi, politika.Id, politika.PolitikaSurumu);
        }

        var guncelYetenekler = _yetenekSaglayici.Getir(politika.EntegrasyonYontemi);
        if (!guncelYetenekler.OperasyonelMi)
        {
            return Uygunluk(false, EBelgeIslemPolitikaUygunlukNedeni.GuncelYontemDesteklenmiyor, politika.Id, politika.PolitikaSurumu);
        }

        var guncelYetenekVar = isTuru switch
        {
            EBelgeOutboxIsTuru.ArtefaktOlustur => guncelYetenekler.YerelSnapshotOlustur && guncelYetenekler.YerelUnsignedUblOlustur,
            EBelgeOutboxIsTuru.UblImzala => guncelYetenekler.YerelImzaOlustur,
            _ => false,
        };

        if (!guncelYetenekVar)
        {
            return Uygunluk(false, EBelgeIslemPolitikaUygunlukNedeni.GuncelYontemDesteklenmiyor, politika.Id, politika.PolitikaSurumu);
        }

        return Uygunluk(true, EBelgeIslemPolitikaUygunlukNedeni.Uygun, politika.Id, politika.PolitikaSurumu);
    }

    private static EBelgeKilitliPolitikaSnapshot PolitikaSnapshotUret(KurumEBelgePolitikasi p) => new()
    {
        Id = p.Id,
        KurumId = p.KurumId,
        PolitikaSurumu = p.PolitikaSurumu,
        AktifMi = p.AktifMi,
        EntegrasyonYontemi = p.EntegrasyonYontemi,
        AktivasyonYerelTarihi = p.AktivasyonYerelTarihi,
    };

    private static EBelgeIslemPolitikaUygunlukSonucu Uygunluk(
        bool uygunMu, EBelgeIslemPolitikaUygunlukNedeni neden, int? politikaId, int? politikaSurumu) => new()
    {
        UygunMu = uygunMu,
        Neden = neden,
        PolitikaId = politikaId,
        PolitikaSurumu = politikaSurumu,
    };

    private static EBelgeKurumPolitikaKarari Karar(
        bool islemeIzinliMi,
        EBelgeKurumPolitikaKararNedeni neden,
        EBelgeEntegrasyonYontemi yontem,
        int? politikaId,
        int? politikaSurumu,
        bool aktifMi,
        DateTime? aktivasyonYerelTarihi,
        EBelgeYontemYetenekleri yetenekler,
        DateTime kararZamaniUtc) => new()
    {
        IslemeIzinliMi = islemeIzinliMi,
        Neden = neden,
        EntegrasyonYontemi = yontem,
        PolitikaId = politikaId,
        PolitikaSurumu = politikaSurumu,
        AktifMi = aktifMi,
        AktivasyonYerelTarihi = aktivasyonYerelTarihi,
        Yetenekler = yetenekler,
        KararZamaniUtc = kararZamaniUtc,
    };
}
