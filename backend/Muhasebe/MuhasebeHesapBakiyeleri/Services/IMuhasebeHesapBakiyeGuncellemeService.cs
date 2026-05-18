using STYS.Muhasebe.MuhasebeFisleri.Entities;

namespace STYS.Muhasebe.MuhasebeHesapBakiyeleri.Services;

/// <summary>
/// Fiş onaylama ve ters kayıt oluşturma sırasında MuhasebeHesapBakiye
/// tablosunu güncelleyen servis. SaveChanges çağırmaz; sadece
/// DbContext üzerinde tracking değişikliklerini hazırlar.
/// Asıl commit işlemi, çağıran MuhasebeFisService transaction'ı
/// içinde yapılır.
/// </summary>
public interface IMuhasebeHesapBakiyeGuncellemeService
{
    /// <summary>
    /// Belirtilen fişin aktif satırlarındaki borç/alacak hareketlerini
    /// hem gerçek hesap (KonsolideMi=false) hem de üst hesap (KonsolideMi=true)
    /// bakiyelerine işler.
    ///
    /// Yalnızca Durum = Onayli veya TersKayit olan fişler için çalışır.
    ///
    /// Bu servis OnaylaAsync/IptalEtAsync akışlarında yalnızca bir kez
    /// çağrılmalıdır. İleride idempotency için fiş üzerinde BakiyeIslendiMi
    /// alanı veya bakiye hareket log tablosu eklenebilir.
    /// </summary>
    Task FisBakiyeleriniIsleAsync(
        MuhasebeFis fis,
        CancellationToken cancellationToken = default);
}
