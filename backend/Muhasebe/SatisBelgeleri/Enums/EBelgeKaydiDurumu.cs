namespace STYS.Muhasebe.SatisBelgeleri.Enums;

/// <summary>
/// E-belgenin İŞ (business lifecycle) durumu - outbox İŞLEME durumundan (EBelgeOutboxDurumu)
/// KASITLI olarak AYRIDIR (bkz. Faz 2B.6 görev md.9: "işlem durumu ile belge iş durumu zaten
/// ayrı tutuluyorsa ikisini tek enum'a sıkıştırma"). Geçiş denemesi/retry sayısı gibi işlem
/// detayları burada YER ALMAZ - yalnız NİHAİ iş sonucu.
/// </summary>
public enum EBelgeKaydiDurumu
{
    SnapshotHazir = 1,

    /// <summary>İmzasız UBL XML artefaktı başarıyla üretildi ve kalıcılaştırıldı.</summary>
    UnsignedUblHazir = 2,

    /// <summary>
    /// İmzasız UBL üretimi KALICI olarak başarısız oldu (bkz. görev md.10 - kalıcı hata
    /// sınıfı). Snapshot immutable olduğundan aynı kayıt üzerinde otomatik retry ANLAMSIZDIR;
    /// düzeltme yeni bir fatura/snapshot/outbox mesajı gerektirir.
    /// </summary>
    UnsignedUblKaliciHata = 3
}
