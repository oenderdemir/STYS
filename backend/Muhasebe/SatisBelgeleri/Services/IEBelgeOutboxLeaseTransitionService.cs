using STYS.Muhasebe.SatisBelgeleri.Enums;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

public interface IEBelgeOutboxLeaseTransitionService
{
    Task<bool> TryCompleteAsync(int outboxMesajiId, int kurumId, string kilitToken, CancellationToken cancellationToken = default);

    Task<bool> TryFailAsync(
        int outboxMesajiId,
        int kurumId,
        string kilitToken,
        string sonHataKodu,
        string sonHataMesaji,
        TimeSpan? retryDelay,
        CancellationToken cancellationToken = default);

    Task<bool> TryRenewAsync(int outboxMesajiId, int kurumId, string kilitToken, TimeSpan leaseDuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Satırın HÂLÂ (Id+KurumId+IsDeleted=0+Durum=Isleniyor+KilitToken+KilitBitisZamaniUtc>now)
    /// çağıran tarafından sahiplenildiğini, `UPDLOCK` ile satırı KİLİTLEYEREK doğrular - satır
    /// kilidi, ambient transaction commit/rollback olana kadar TUTULUR (bkz. Faz 2B.6.1 görev
    /// md.2). Değişiklik YAPMAZ, yalnız OKUR+KİLİTLER. Ambient `_dbContext.Database.CurrentTransaction`
    /// varsa onu kullanır (diğer Try* metotlarıyla AYNI desen) - böylece çağıran, bu kontrolü
    /// kendi transaction'ının İÇİNE alıp arkasından EF değişiklikleri + diğer Try* çağrılarını
    /// AYNI transaction'da sürdürebilir.
    ///
    /// NOT (Faz 2B.6.2): bu genel metot `EBelgeKaydiId`/`IsTuru`'yü DOĞRULAMAZ - yalnız
    /// outbox+kurum+token+lease sahipliğini kontrol eder. Artifact akışı (tek belirli
    /// `EBelgeKaydiId`'ye yazma yapan tek iş türü) için <see cref="IsOwnedForArtifactAsync"/>
    /// kullanılmalıdır; bu metot DİĞER (artifact-dışı) handler'lar için AYNEN korunur.
    /// </summary>
    Task<bool> IsOwnedAsync(int outboxMesajiId, int kurumId, string kilitToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// <see cref="IsOwnedAsync"/> ile AYNI UPDLOCK/ambient-transaction deseni, EK olarak
    /// `EBelgeKaydiId` ve `IsTuru = expectedIsTuru` eşleşmesini de zorunlu kılar (bkz. Faz
    /// 2B.6.2 görev md.1, Faz 2B.7 görev md.16 - "artifact-aware guard'ı iş türü parametreli
    /// ve type-safe hale getir"). Bu, bir outbox mesajının (doğru token'la bile) YANLIŞ bir
    /// `EBelgeKaydi`'yi hedeflemesini veya yanlış iş türündeki bir satırın (ör. `UblImzala`
    /// mesajının `ArtefaktOlustur` akışında ya da tersi) kullanılmasını engeller - çapraz kayıt
    /// mutasyonuna karşı sahiplik sözleşmesinin TAMAMLANMIŞ hâlidir.
    /// </summary>
    Task<bool> IsOwnedForJobAsync(int outboxMesajiId, int kurumId, int eBelgeKaydiId, EBelgeOutboxIsTuru expectedIsTuru, string kilitToken, CancellationToken cancellationToken = default);

    /// <summary><see cref="TryCompleteAsync"/> ile AYNI, EK olarak `EBelgeKaydiId` + `IsTuru = expectedIsTuru` guard'ı taşıyan iş-türü-farkında tamamlama (bkz. Faz 2B.6.2/2B.7 görev md.1/md.16).</summary>
    Task<bool> TryCompleteJobAsync(int outboxMesajiId, int kurumId, int eBelgeKaydiId, EBelgeOutboxIsTuru expectedIsTuru, string kilitToken, CancellationToken cancellationToken = default);

    /// <summary><see cref="TryFailAsync"/> ile AYNI, EK olarak `EBelgeKaydiId` + `IsTuru = expectedIsTuru` guard'ı taşıyan iş-türü-farkında terminal/geçici hata geçişi (bkz. Faz 2B.6.2/2B.7 görev md.1/md.16).</summary>
    Task<bool> TryFailJobAsync(
        int outboxMesajiId,
        int kurumId,
        int eBelgeKaydiId,
        EBelgeOutboxIsTuru expectedIsTuru,
        string kilitToken,
        string sonHataKodu,
        string sonHataMesaji,
        TimeSpan? retryDelay,
        CancellationToken cancellationToken = default);
}
