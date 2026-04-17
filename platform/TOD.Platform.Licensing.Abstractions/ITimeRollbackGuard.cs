namespace TOD.Platform.Licensing.Abstractions;

/// <summary>
/// Sistem saatinin geri alınmasını tespit eder.
/// Offline lisanslamada zaman manipülasyonunu önler.
/// </summary>
public interface ITimeRollbackGuard
{
    /// <summary>Mevcut zamanın geri alınıp alınmadığını kontrol eder.</summary>
    bool IsTimeRolledBack();

    /// <summary>Son bilinen zamanı günceller. Başarılı doğrulamadan sonra çağrılır.</summary>
    void RecordCurrentTime();
}
