using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.MuhasebeDonemleri.Repositories;

public interface IMuhasebeDonemRepository : IBaseRdbmsRepository<MuhasebeDonem, int>
{
    Task<MuhasebeDonem?> GetAktifDonemAsync(
        int tesisId,
        DateTime tarih,
        CancellationToken cancellationToken = default);

    Task<MuhasebeDonem?> GetByTesisYilDonemAsync(
        int tesisId,
        int maliYil,
        int donemNo,
        CancellationToken cancellationToken = default);

    Task<bool> TarihAraligiCakisiyorMuAsync(
        int tesisId,
        DateTime baslangic,
        DateTime bitis,
        int? haricId = null,
        CancellationToken cancellationToken = default);
}
