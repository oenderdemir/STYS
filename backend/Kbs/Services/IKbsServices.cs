using STYS.Kbs.Dtos;

namespace STYS.Kbs.Services;

public interface IKbsBildirimOlusturmaService
{
    Task<KbsFiiliOlaySonucDto> FiiliGirisYapAsync(int konaklayanId, DateTime? olayTarihi = null, CancellationToken cancellationToken = default);
    Task<KbsFiiliOlaySonucDto> FiiliCikisYapAsync(int konaklayanId, DateTime? olayTarihi = null, CancellationToken cancellationToken = default);
    Task OdaDegisikligiBildirimleriniHazirlaAsync(KbsOdaDegisikligiOlayi olay, CancellationToken cancellationToken = default);
    Task<KbsFiiliOlaySonucDto> GelmeyecekOlarakIsaretleAsync(int konaklayanId, CancellationToken cancellationToken = default);
}

public interface IKbsYonetimService
{
    Task<KbsTesisAyariDto?> GetAyarAsync(int tesisId, CancellationToken cancellationToken);
    Task<KbsTesisAyariDto> UpdateAyarAsync(int tesisId, KbsTesisAyariGuncelleDto request, CancellationToken cancellationToken);
    Task<KbsBaglantiTestSonucu> BaglantiKontrolAsync(int tesisId, CancellationToken cancellationToken);
    Task<KbsSayfaliSonucDto<KbsBildirimListeDto>> ListeleAsync(int? tesisId, string? durum, string? bildirimTipi, int sayfa, int sayfaBoyutu, bool hassasVeriGoster, CancellationToken cancellationToken);
    Task<KbsGunlukOzetDto> GunlukOzetAsync(int? tesisId, CancellationToken cancellationToken);
    Task TekrarKuyrugaAlAsync(long bildirimId, CancellationToken cancellationToken);
    Task ManuelMudahaleAsync(long bildirimId, CancellationToken cancellationToken);
    Task<(byte[] Content, string FileName, string ManifestHash)> EgmExcelOlusturAsync(int tesisId, string bildirimTipi, CancellationToken cancellationToken);
    Task EgmYuklemeOnaylaAsync(int tesisId, string manifestHash, CancellationToken cancellationToken);
    Task MutabakatYapAsync(long bildirimId, KbsMutabakatRequestDto request, CancellationToken cancellationToken);
    Task EgmDogrulaAsync(long bildirimId, KbsEgmDogrulamaRequestDto request, CancellationToken cancellationToken);
}
