using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.10 görev md.7 - TEST-ONLY yetenek sağlayıcısı. `DogrudanGib`'i (production'da HENÜZ
/// desteklenmeyen - gerçek adapter/HSM/mali mühür YOK) TAM operasyonel olarak işaretler - yalnız
/// mevcut GERÇEK XAdES/SignedReady E2E testlerini (Faz 2B.5-2B.9) korumak İÇİNDİR. Production
/// DI'a (`backend/Program.cs`) ASLA kaydedilmez - yalnız bu test assembly'sinde tanımlıdır.
/// </summary>
public sealed class EBelgeTestYontemYetenekSaglayici : IEBelgeYontemYetenekSaglayici
{
    public static readonly EBelgeTestYontemYetenekSaglayici Instance = new();

    private readonly EBelgeYontemYetenekSaglayici _prodSaglayici = new();

    public EBelgeYontemYetenekleri Getir(EBelgeEntegrasyonYontemi yontem) =>
        yontem == EBelgeEntegrasyonYontemi.DogrudanGib
            ? new EBelgeYontemYetenekleri(OperasyonelMi: true, YerelSnapshotOlustur: true, YerelUnsignedUblOlustur: true, YerelImzaOlustur: true, OtomatikGonderimYap: false)
            : _prodSaglayici.Getir(yontem);
}

/// <summary>
/// Faz 2B.10.2 - <see cref="STYS.Muhasebe.SatisBelgeleri.Services.EBelgeUblImzalamaService"/>'in
/// artık zorunlu bağımlılığı olan global signing gate İÇİN, gate davranışını (Enabled/tarih
/// kapısı) test ETMEYEN senaryolarda kullanılan sabit "HER ZAMAN AÇIK" test double'ı - gerçek
/// `EBelgeSigningActivationGate`'in Enabled/NotBeforeLocalDate algoritmasını AYRI test dosyaları
/// (ör. <c>EBelgeSigningActivationGateTests</c>) zaten doğrudan kapsar.
/// </summary>
public sealed class EBelgeTestSigningActivationGate : STYS.Muhasebe.SatisBelgeleri.Services.IEBelgeSigningActivationGate
{
    public static readonly EBelgeTestSigningActivationGate Acik = new(true);
    public static readonly EBelgeTestSigningActivationGate Kapali = new(false);

    private readonly bool _sonuc;

    private EBelgeTestSigningActivationGate(bool sonuc) => _sonuc = sonuc;

    public bool ShouldCreateSigningMessage() => _sonuc;

    public bool CanSignNow() => _sonuc;
}

/// <summary>
/// Faz 2B.10 - mevcut (2B.5-2B.9) e-belge testlerinin, EBelgeKaydi/outbox/imzalama zincirini
/// SatisBelgesiService'in normal akışını (ve dolayısıyla kurum politikası kararını) KULLANMADAN
/// DOĞRUDAN seed ettiği yerlerde, YENİ politika-tabanlı fail-closed kapıların bu testleri
/// BOZMAMASI için gereken minimum test altyapısı. Gerçek bir test SİLİNMEDEN/gevşetilmeden,
/// GERÇEK bir aktif politika + immutable karar seed edilir.
/// </summary>
public static class EBelgeKurumPolitikaTestSupport
{
    /// <summary>Her zaman `Aktif` (DogrudanGib, tam yerel yetenek) döndüren GERÇEK bir karar servisi - global işlem kapısı `Enabled=true` + uzak geçmiş bir tarihle kurulur.</summary>
    public static IEBelgeKurumPolitikaServisi CreateAlwaysAktifServisi(StysAppDbContext dbContext, TimeProvider? timeProvider = null)
    {
        var tp = timeProvider ?? TimeProvider.System;
        var gate = new EBelgeProcessingActivationGate(
            Options.Create(new EBelgeProcessingOptions { Enabled = true, NotBeforeLocalDate = "2020-01-01" }),
            tp,
            NullLogger<EBelgeProcessingActivationGate>.Instance);

        return new EBelgeKurumPolitikaServisi(dbContext, gate, EBelgeTestYontemYetenekSaglayici.Instance, tp);
    }

    /// <summary>Kurum İÇİN aktif bir `DogrudanGib` politikası seed eder (idempotent - satır ZATEN varsa NO-OP).</summary>
    public static async Task SeedAktifDogrudanGibPolitikaAsync(StysAppDbContext dbContext, int kurumId)
    {
        var mevcut = await dbContext.Set<KurumEBelgePolitikasi>().IgnoreQueryFilters().AnyAsync(p => p.KurumId == kurumId);
        if (mevcut)
        {
            return;
        }

        dbContext.Add(new KurumEBelgePolitikasi
        {
            KurumId = kurumId,
            EntegrasyonYontemi = EBelgeEntegrasyonYontemi.DogrudanGib,
            AktifMi = true,
            AktivasyonYerelTarihi = new DateTime(2020, 1, 1),
            PolitikaSurumu = 1,
        });
        await dbContext.SaveChangesAsync();
    }

    /// <summary>Bir satış belgesi İÇİN tam yerel yetenekli (`DogrudanGib`, snapshot+unsignedUbl+imza=true) immutable karar seed eder ve yeni kaydın Id'sini döner.</summary>
    public static async Task<int> SeedEBelgeKarariAsync(
        StysAppDbContext dbContext, int kurumId, int satisBelgesiId, int? eBelgeKaydiId, TimeProvider? timeProvider = null)
    {
        var tp = timeProvider ?? TimeProvider.System;
        var karar = new SatisBelgesiEBelgeKarari
        {
            KurumId = kurumId,
            SatisBelgesiId = satisBelgesiId,
            EntegrasyonYontemi = EBelgeEntegrasyonYontemi.DogrudanGib,
            KararNedeni = EBelgeKurumPolitikaKararNedeni.Aktif,
            YerelSnapshotOlustur = true,
            YerelUnsignedUblOlustur = true,
            YerelImzaOlustur = true,
            OtomatikGonderimYap = false,
            KararZamaniUtc = tp.GetUtcNow().UtcDateTime,
            EBelgeKaydiId = eBelgeKaydiId,
        };

        dbContext.Add(karar);
        await dbContext.SaveChangesAsync();
        return karar.Id;
    }
}
