using STYS.Muhasebe.MuhasebeDonemleri.Dtos;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.MuhasebeDonemleri.Services;

public interface IMuhasebeDonemService : IBaseRdbmsService<MuhasebeDonemDto, MuhasebeDonem, int>
{
    Task<MuhasebeDonemDto?> GetAktifDonemAsync(
        int tesisId,
        DateTime tarih,
        CancellationToken cancellationToken = default);

    Task DonemKapatAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task DonemAcAsync(
        int id,
        CancellationToken cancellationToken = default);
}
