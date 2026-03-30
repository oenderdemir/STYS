using STYS.Rezervasyonlar.Dto;

namespace STYS.Rezervasyonlar.Services;

public interface IRezervasyonService
{
    Task<List<RezervasyonTesisDto>> GetErisilebilirTesislerAsync(CancellationToken cancellationToken = default);

    Task<List<RezervasyonOdaTipiDto>> GetOdaTipleriByTesisAsync(int tesisId, CancellationToken cancellationToken = default);

    Task<List<RezervasyonMisafirTipiDto>> GetMisafirTipleriAsync(int tesisId, CancellationToken cancellationToken = default);

    Task<List<RezervasyonKonaklamaTipiDto>> GetKonaklamaTipleriAsync(int tesisId, CancellationToken cancellationToken = default);

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

    Task<RezervasyonDashboardDto> GetGunlukDashboardAsync(
        int tesisId,
        DateTime? tarih,
        DateTime? kpiBaslangicTarihi,
        DateTime? kpiBitisTarihi,
        CancellationToken cancellationToken = default);

    Task<RezervasyonDetayDto?> GetRezervasyonDetayAsync(int rezervasyonId, CancellationToken cancellationToken = default);

    Task<List<RezervasyonDegisiklikGecmisiDto>> GetDegisiklikGecmisiAsync(int rezervasyonId, CancellationToken cancellationToken = default);

    Task<RezervasyonKonaklayanPlanDto?> GetKonaklayanPlaniAsync(int rezervasyonId, CancellationToken cancellationToken = default);

    Task<RezervasyonKonaklayanPlanDto> KaydetKonaklayanPlaniAsync(int rezervasyonId, RezervasyonKonaklayanPlanKaydetRequestDto request, CancellationToken cancellationToken = default);

    Task<RezervasyonOdaDegisimSecenekDto> GetOdaDegisimSecenekleriAsync(int rezervasyonId, CancellationToken cancellationToken = default);

    Task<RezervasyonKayitSonucDto> KaydetOdaDegisimiAsync(int rezervasyonId, RezervasyonOdaDegisimKaydetRequestDto request, CancellationToken cancellationToken = default);

    Task<RezervasyonKayitSonucDto> TamamlaCheckInAsync(int rezervasyonId, CancellationToken cancellationToken = default);

    Task<RezervasyonCheckInKontrolDto> GetCheckInKontrolAsync(int rezervasyonId, CancellationToken cancellationToken = default);

    Task<RezervasyonKayitSonucDto> TamamlaCheckOutAsync(int rezervasyonId, CancellationToken cancellationToken = default);

    Task<RezervasyonKayitSonucDto> IptalEtAsync(int rezervasyonId, CancellationToken cancellationToken = default);

    Task<RezervasyonOdemeOzetDto> GetOdemeOzetiAsync(int rezervasyonId, CancellationToken cancellationToken = default);

    Task<RezervasyonEkHizmetSecenekleriDto> GetEkHizmetSecenekleriAsync(int rezervasyonId, CancellationToken cancellationToken = default);

    Task<RezervasyonOdemeOzetDto> KaydetEkHizmetAsync(int rezervasyonId, RezervasyonEkHizmetKaydetRequestDto request, CancellationToken cancellationToken = default);

    Task<RezervasyonOdemeOzetDto> GuncelleEkHizmetAsync(int rezervasyonId, int ekHizmetId, RezervasyonEkHizmetKaydetRequestDto request, CancellationToken cancellationToken = default);

    Task<RezervasyonOdemeOzetDto> SilEkHizmetAsync(int rezervasyonId, int ekHizmetId, CancellationToken cancellationToken = default);

    Task<RezervasyonOdemeOzetDto> KaydetOdemeAsync(int rezervasyonId, RezervasyonOdemeKaydetRequestDto request, CancellationToken cancellationToken = default);

    Task<RezervasyonDetayDto> GuncelleKonaklamaHakkiDurumuAsync(
        int rezervasyonId,
        int hakId,
        RezervasyonKonaklamaHakkiDurumGuncelleRequestDto request,
        CancellationToken cancellationToken = default);

    Task<RezervasyonDetayDto> KaydetKonaklamaHakkiTuketimAsync(
        int rezervasyonId,
        int hakId,
        RezervasyonKonaklamaHakkiTuketimKaydiKaydetRequestDto request,
        CancellationToken cancellationToken = default);

    Task<RezervasyonDetayDto> SilKonaklamaHakkiTuketimAsync(
        int rezervasyonId,
        int hakId,
        int tuketimKaydiId,
        CancellationToken cancellationToken = default);

    Task<OdemeRaporDto> GetOdemeRaporuAsync(IReadOnlyCollection<int> tesisIds, DateTime baslangicTarihi, DateTime bitisTarihi, CancellationToken cancellationToken = default);
}
