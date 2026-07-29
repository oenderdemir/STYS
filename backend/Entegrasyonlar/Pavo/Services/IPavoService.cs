using STYS.Entegrasyonlar.Pavo.Dtos;

namespace STYS.Entegrasyonlar.Pavo.Services;

public interface IPavoService
{
    Task<List<PavoTerminalDto>> GetTerminallerAsync(int? tesisId, int? kasaBankaHesapId, CancellationToken cancellationToken);
    Task<PavoTerminalDto> KaydetTerminalAsync(int? id, PavoTerminalKaydetRequest request, CancellationToken cancellationToken);
    Task<PavoTerminalDto> EslesmeBaslatAsync(int id, CancellationToken cancellationToken);
    Task<PavoTerminalDto> EslesmeKontrolAsync(int id, CancellationToken cancellationToken);
    Task<PavoOdemeIslemiDto> OdemeBaslatAsync(PavoOdemeBaslatRequest request, CancellationToken cancellationToken);
    Task<PavoOdemeIslemiDto?> BekleyenOdemeAsync(int rezervasyonId, CancellationToken cancellationToken);
    Task<PavoOdemeIslemiDto> OdemeDurumuAsync(int id, CancellationToken cancellationToken);
}
