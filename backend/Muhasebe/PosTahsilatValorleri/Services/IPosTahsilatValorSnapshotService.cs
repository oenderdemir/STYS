using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;

namespace STYS.Muhasebe.PosTahsilatValorleri.Services;

public interface IPosTahsilatValorSnapshotService
{
    /// <summary>
    /// Yalnizca OdemeYontemi=KrediKarti ve bagli KasaBankaHesap.Tip=KrediKarti ise valor takip
    /// kaydi olusturur (aksi halde null doner - nakit/banka tahsilatlarina dokunulmaz). Ambient
    /// transaction icinde cagrilir (TahsilatOdemeBelgesiService.AddAsync'in commit'inden once),
    /// kendi transaction'ini acmaz.
    /// </summary>
    Task<PosTahsilatValor?> OlusturSnapshotAsync(TahsilatOdemeBelgesi belge, CancellationToken cancellationToken = default);

    /// <summary>
    /// Iliskili valor kaydini (varsa) iptal zincirine dahil eder. TahsilatOdemeBelgesiService.
    /// IptalEtAsync'in ambient transaction'inda, existing.Durum=Iptal'dan ONCE cagrilir. Kayit
    /// yoksa no-op. Yalnizca kendi valor transfer fisini yonetir, belgenin ilk tahsil fisine
    /// dokunmaz.
    /// </summary>
    Task IptalEtAsync(int tahsilatOdemeBelgesiId, CancellationToken cancellationToken = default);
}
