using STYS.Muhasebe.OdemeIzleme.Dtos;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.OdemeIzleme.Services;

/// <summary>
/// "Ödeme İzleme/Araştırma" ekrani icin SALT-OKUNUR sorgu/analiz servisi. Hicbir odeme/fis/valor
/// kaydi olusturmaz veya degistirmez; yalnizca mevcut kayitlari arar, iliskilendirir ve olasi
/// tutarsizliklari/yanlis-yonlendirme belirtilerini raporlar. Otomatik duzeltme/tasima YAPMAZ.
/// </summary>
public interface IOdemeIzlemeService
{
    Task<PagedResult<OdemeAramaSatiriDto>> AraAsync(PagedRequest request, OdemeAramaFilterDto filter, CancellationToken cancellationToken = default);

    /// <summary>Tek bir odemenin tam detayini, ilgili kayitlarini ve kendisine ozel uyarilarini dondurur.</summary>
    Task<OdemeDetayDto> GetDetayAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Bir carinin acilis bakiyesinden itibaren acikca aciklanabilir hareket dokumunu dondurur.</summary>
    Task<CariHareketDokumDto> GetCariHareketDokumuAsync(CariHareketDokumFilterDto filter, CancellationToken cancellationToken = default);

    /// <summary>Kullanicinin beyan ettigi odeme bilgileriyle sistemdeki olasi kayitlari, guven
    /// seviyesine gore kategorize ederek arar - YENI KALICI KAYIT OLUSTURMAZ.</summary>
    Task<List<BeyanEdilenOdemeEslesmeDto>> KarsilastirAsync(BeyanEdilenOdemeKarsilastirmaFilterDto filter, CancellationToken cancellationToken = default);
}
