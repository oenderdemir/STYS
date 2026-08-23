using STYS.Muhasebe.StokMaliyetPolitikalari.Dtos;

namespace STYS.Muhasebe.StokMaliyetPolitikalari.Services;

public interface IStokMaliyetPolitikasiService
{
    Task<CurrentStokMaliyetPolitikasiDto> GetCurrentAsync(int tesisId, DateTime tarih, CancellationToken cancellationToken = default);
    Task<StokMaliyetPolitikasiDto?> GetByTesisMaliYilAsync(int tesisId, int maliYil, CancellationToken cancellationToken = default);
    Task<StokMaliyetPolitikasiDto> UpsertAsync(UpsertStokMaliyetPolitikasiRequest request, CancellationToken cancellationToken = default);
    Task<string> GetRequiredMaliyetYontemiAsync(int tesisId, DateTime tarih, CancellationToken cancellationToken = default);
}
