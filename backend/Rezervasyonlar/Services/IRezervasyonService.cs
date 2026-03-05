using STYS.Rezervasyonlar.Dto;

namespace STYS.Rezervasyonlar.Services;

public interface IRezervasyonService
{
    Task<List<RezervasyonTesisDto>> GetErisilebilirTesislerAsync(CancellationToken cancellationToken = default);

    Task<List<RezervasyonOdaTipiDto>> GetOdaTipleriByTesisAsync(int tesisId, CancellationToken cancellationToken = default);

    Task<List<RezervasyonMisafirTipiDto>> GetMisafirTipleriAsync(CancellationToken cancellationToken = default);

    Task<List<RezervasyonKonaklamaTipiDto>> GetKonaklamaTipleriAsync(CancellationToken cancellationToken = default);

    Task<List<RezervasyonIndirimKuraliSecenekDto>> GetUygulanabilirIndirimKurallariAsync(
        int tesisId,
        int misafirTipiId,
        int konaklamaTipiId,
        DateTime baslangicTarihi,
        DateTime bitisTarihi,
        CancellationToken cancellationToken = default);

    Task<List<UygunOdaDto>> GetUygunOdalarAsync(UygunOdaAramaRequestDto request, CancellationToken cancellationToken = default);

    Task<List<KonaklamaSenaryoDto>> GetKonaklamaSenaryolariAsync(KonaklamaSenaryoAramaRequestDto request, CancellationToken cancellationToken = default);

    Task<SenaryoFiyatHesaplamaSonucuDto> HesaplaSenaryoFiyatiAsync(SenaryoFiyatHesaplaRequestDto request, CancellationToken cancellationToken = default);

    Task<RezervasyonKayitSonucDto> KaydetAsync(RezervasyonKaydetRequestDto request, CancellationToken cancellationToken = default);

    Task<List<RezervasyonListeDto>> GetRezervasyonlarAsync(int? tesisId, CancellationToken cancellationToken = default);

    Task<RezervasyonDetayDto?> GetRezervasyonDetayAsync(int rezervasyonId, CancellationToken cancellationToken = default);
}
