using STYS.Entegrasyonlar.Pos.Dtos;

namespace STYS.Entegrasyonlar.Pos.Services;

public interface IPosService
{
    List<PosSaglayiciDto> GetSaglayicilar();
    Task<List<PosTerminalDto>> GetTerminallerAsync(int? tesisId, int? kasaBankaHesapId, CancellationToken cancellationToken);
    Task<PosTerminalDto> KaydetTerminalAsync(int? id, PosTerminalKaydetRequest request, CancellationToken cancellationToken);
    Task<PosTerminalDto> EslesmeBaslatAsync(int id, CancellationToken cancellationToken);
    Task<PosTerminalDto> EslesmeKontrolAsync(int id, CancellationToken cancellationToken);
    Task<PosOdemeIslemiDto> OdemeBaslatAsync(PosOdemeBaslatRequest request, CancellationToken cancellationToken);
    Task<PosOdemeIslemiDto?> BekleyenOdemeAsync(int rezervasyonId, CancellationToken cancellationToken);
    Task<PosOdemeIslemiDto> OdemeDurumuAsync(int id, CancellationToken cancellationToken);
}
