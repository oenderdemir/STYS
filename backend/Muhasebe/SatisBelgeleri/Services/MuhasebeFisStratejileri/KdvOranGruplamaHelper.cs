using STYS.Muhasebe.SatisBelgeleri.Entities;

namespace STYS.Muhasebe.SatisBelgeleri.Services.MuhasebeFisStratejileri;

/// <summary>
/// KDV içeren (KdvTutari>0) aktif satırları KdvOrani'na göre gruplayan, tüm standart (tevkifatsız)
/// muhasebe fişi stratejileri tarafından paylaşılan tek yardımcı - her strateji kendi oran
/// gruplama mantığını AYRI AYRI yeniden üretmez. Tevkifatlı stratejiler bu yardımcıyı kullanmaz
/// (kendi ayrı KDV/tevkifat hesaplama mantığına sahiptir).
/// </summary>
public static class KdvOranGruplamaHelper
{
    public static IReadOnlyList<(decimal Oran, decimal Tutar)> Grupla(IEnumerable<SatisBelgesiSatiri> satirlar)
        => satirlar
            .Where(s => !s.IsDeleted && s.KdvTutari > 0)
            .GroupBy(s => s.KdvOrani)
            .Select(g => (Oran: g.Key, Tutar: g.Sum(s => s.KdvTutari)))
            .OrderBy(x => x.Oran)
            .ToList();
}
