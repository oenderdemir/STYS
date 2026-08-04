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
    /// </summary>
    Task<bool> IsOwnedAsync(int outboxMesajiId, int kurumId, string kilitToken, CancellationToken cancellationToken = default);
}
