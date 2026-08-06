namespace STYS.Muhasebe.SatisBelgeleri.Enums;

/// <summary>
/// Faz 2B.10 - <see cref="STYS.Muhasebe.SatisBelgeleri.Services.IEBelgeKurumPolitikaServisi"/>'nin
/// ürettiği kararın NEDENİ - type-safe, fail-closed sınıflandırma. Hiçbir durum "e-belge
/// gerekmiyor" biçiminde SESSİZCE yorumlanmaz - <see cref="Aktif"/>, <see cref="YontemKullanilmayacak"/>
/// ve <see cref="HariciSistemSorumlu"/> DIŞINDAKİ TÜM nedenler fail-closed (İşlemeİzinliMi=false)
/// sonuç üretir.
/// </summary>
public enum EBelgeKurumPolitikaKararNedeni
{
    /// <summary>Global kapılar açık, kurum politikası aktif ve aktivasyon tarihi geçilmiş - işleme İZİNLİDİR.</summary>
    Aktif = 1,

    /// <summary>Global e-belge süreci (<see cref="EBelgeProcessingActivationReason.Disabled"/>) KAPALI - kurum politikası bunu HİÇBİR ZAMAN açamaz.</summary>
    GlobalKapali = 2,

    /// <summary>Global aktivasyon tarihi (<see cref="EBelgeProcessingActivationReason.BeforeActivationDate"/>) HENÜZ gelmedi.</summary>
    GlobalAktivasyonTarihiGelmedi = 3,

    /// <summary>Kurum için hiç politika kaydı YOK VEYA yöntem <see cref="EBelgeEntegrasyonYontemi.Yapilandirilmadi"/> - fail-closed.</summary>
    PolitikaYapilandirilmadi = 4,

    /// <summary>Kurum politikası mevcut ama <c>AktifMi=false</c> (soft-delete DAHİL) - fail-closed.</summary>
    PolitikaPasif = 5,

    /// <summary>Kurum politikası aktif ama <c>AktivasyonYerelTarihi</c> henüz gelmedi.</summary>
    KurumAktivasyonTarihiGelmedi = 6,

    /// <summary>Kurum AÇIKÇA <see cref="EBelgeEntegrasyonYontemi.Kullanilmayacak"/> seçmiştir - bu bir HATA DEĞİLDİR, işleme yalnız İZİNLİ DEĞİLDİR (yerel pipeline gerekmez).</summary>
    YontemKullanilmayacak = 7,

    /// <summary>Mali belge HARİCİ bir sistemde oluşturulacaktır - bu bir HATA DEĞİLDİR, yerel pipeline gerekmez.</summary>
    HariciSistemSorumlu = 8,

    /// <summary><see cref="EBelgeEntegrasyonYontemi.OzelEntegrator"/>/<see cref="EBelgeEntegrasyonYontemi.DogrudanGib"/> - gerçek adapter YOKKEN production'da operasyonel DEĞİLDİR.</summary>
    YontemHenuzDesteklenmiyor = 9,

    /// <summary>Politika kaydı yapısal olarak geçersiz/tutarsız (ör. tanınmayan enum değeri) - fail-closed.</summary>
    PolitikaGecersiz = 10
}
