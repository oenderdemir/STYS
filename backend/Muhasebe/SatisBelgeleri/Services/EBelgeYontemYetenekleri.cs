using STYS.Muhasebe.SatisBelgeleri.Enums;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// Faz 2B.10 - bir <see cref="EBelgeEntegrasyonYontemi"/>'nin STYS'ten talep ettiği/izin verdiği
/// işlemlerin type-safe planı. Birbirinden bağımsız/çelişebilen config boolean'ları (ör.
/// "UblSTYSTarafindanUretilsin") YERİNE, bu plan HER ZAMAN <see cref="IEBelgeYontemYetenekSaglayici"/>
/// üzerinden, yöntemden TÜRETİLİR - hiçbir yerde elle kopyalanmaz/ayarlanmaz.
/// </summary>
public sealed record EBelgeYontemYetenekleri(
    bool OperasyonelMi,
    bool YerelSnapshotOlustur,
    bool YerelUnsignedUblOlustur,
    bool YerelImzaOlustur,
    bool OtomatikGonderimYap);

/// <summary>
/// Faz 2B.10 - yöntem yetenek matrisinin TEK, merkezi kaynağı.
/// </summary>
public interface IEBelgeYontemYetenekSaglayici
{
    EBelgeYontemYetenekleri Getir(EBelgeEntegrasyonYontemi yontem);
}

/// <summary>
/// Production yetenek matrisi (bkz. görev md.7). `OzelEntegrator`/`DogrudanGib` enum'da BULUNUR
/// ama gerçek adapter EKLENMEDEN `OperasyonelMi=false` döner - bu iki yöntem, gerçek bir
/// adapter/HSM/mali mühür entegrasyonu YAPILMADAN production'da AKTİF POLİTİKA olarak kabul
/// EDİLEMEZ (bkz. <see cref="EBelgeKurumPolitikaYonetimServisi"/>, "Pasif -> Aktif" kuralı).
/// </summary>
public sealed class EBelgeYontemYetenekSaglayici : IEBelgeYontemYetenekSaglayici
{
    public EBelgeYontemYetenekleri Getir(EBelgeEntegrasyonYontemi yontem) => yontem switch
    {
        EBelgeEntegrasyonYontemi.Kullanilmayacak => new EBelgeYontemYetenekleri(
            OperasyonelMi: true, YerelSnapshotOlustur: false, YerelUnsignedUblOlustur: false, YerelImzaOlustur: false, OtomatikGonderimYap: false),

        EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi => new EBelgeYontemYetenekleri(
            OperasyonelMi: true, YerelSnapshotOlustur: false, YerelUnsignedUblOlustur: false, YerelImzaOlustur: false, OtomatikGonderimYap: false),

        EBelgeEntegrasyonYontemi.GibPortal => new EBelgeYontemYetenekleri(
            OperasyonelMi: true, YerelSnapshotOlustur: true, YerelUnsignedUblOlustur: true, YerelImzaOlustur: false, OtomatikGonderimYap: false),

        // OzelEntegrator/DogrudanGib: gerçek adapter/HSM/mali mühür EKLENMEDEN OperasyonelMi=false -
        // diğer alanların değeri ÖNEMSİZDİR (OperasyonelMi=false iken karar servisi bunları HİÇ
        // KULLANMAZ, bkz. YontemHenuzDesteklenmiyor), yine de fail-closed/tutarlı olması için
        // hepsi false bırakılır.
        EBelgeEntegrasyonYontemi.OzelEntegrator => new EBelgeYontemYetenekleri(
            OperasyonelMi: false, YerelSnapshotOlustur: false, YerelUnsignedUblOlustur: false, YerelImzaOlustur: false, OtomatikGonderimYap: false),

        EBelgeEntegrasyonYontemi.DogrudanGib => new EBelgeYontemYetenekleri(
            OperasyonelMi: false, YerelSnapshotOlustur: false, YerelUnsignedUblOlustur: false, YerelImzaOlustur: false, OtomatikGonderimYap: false),

        // Yapilandirilmadi VE tanınmayan herhangi bir değer - fail-closed, tamamen pasif.
        _ => new EBelgeYontemYetenekleri(
            OperasyonelMi: false, YerelSnapshotOlustur: false, YerelUnsignedUblOlustur: false, YerelImzaOlustur: false, OtomatikGonderimYap: false),
    };
}
