using STYS.Raporlar.KonaklamaKisiSayisi.Dto;

namespace STYS.Raporlar.KonaklamaKisiSayisi.Services;

public interface IKonaklamaKisiSayisiRaporService
{
    Task<KonaklamaKisiSayisiRaporDto> GetRaporAsync(
        int tesisId,
        int ay,
        int baslangicYil,
        int bitisYil,
        CancellationToken cancellationToken = default);
}
