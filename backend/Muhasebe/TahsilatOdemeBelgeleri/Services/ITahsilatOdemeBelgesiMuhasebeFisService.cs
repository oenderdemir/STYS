using STYS.Muhasebe.TahsilatOdemeBelgeleri.Dtos;

namespace STYS.Muhasebe.TahsilatOdemeBelgeleri.Services;

/// <summary>
/// TahsilatOdemeBelgesi'nden muhasebe fisi ureten, kaynak modulden bagimsiz servis.
/// Otomatik cagrilmaz — ayri, bilincli bir aksiyondur (bkz. Rezervasyon odeme entegrasyonu
/// revizyonu #1/#2: odeme kaydedilirken sadece TahsilatOdemeBelgesi olusur, fis degil).
/// </summary>
public interface ITahsilatOdemeBelgesiMuhasebeFisService
{
    Task<TahsilatOdemeBelgesiDto> FisOlusturAsync(int tahsilatOdemeBelgesiId, CancellationToken cancellationToken = default);
}
