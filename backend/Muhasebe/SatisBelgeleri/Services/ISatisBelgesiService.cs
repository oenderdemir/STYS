using STYS.Muhasebe.SatisBelgeleri.Dtos;
using TOD.Platform.SharedKernel.Responses;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

public interface ISatisBelgesiService
{
    Task<SatisBelgesiDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<SatisBelgesiDto>> FilterAsync(SatisBelgesiFilterDto filter, CancellationToken cancellationToken = default);
    Task<SatisBelgesiDto> CreateAsync(CreateSatisBelgesiRequest request, CancellationToken cancellationToken = default);
    Task<SatisBelgesiDto> UpdateAsync(int id, UpdateSatisBelgesiRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task MuhasebeOnayinaGonderAsync(int id, CancellationToken cancellationToken = default);
    Task MuhasebeOnaylaAsync(int id, CancellationToken cancellationToken = default);
    Task<SatisBelgesiDto> FaturaKesAsync(int id, FaturaKesRequest request, CancellationToken cancellationToken = default);
    Task ReddetAsync(int id, string redNedeni, CancellationToken cancellationToken = default);

    /// <summary>
    /// Muhasebe ekranından (Muhasebe &gt; Satış/Alış Belgeleri) iptal — mevcut yetkili ters kayıt
    /// davranışını korur: bağlı bir muhasebe fişi varsa (MuhasebeFisId doluysa)
    /// SatisBelgesiFisiIptalEtAsync üzerinden ters kayıt oluşturulur (bkz. c799337).
    /// </summary>
    Task IptalEtAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Operasyon sınırından (ui/ticari-belgeler) iptal — TicariBelgeService YALNIZCA bu metodu
    /// kullanır (bkz. görev 1). Mali etkisi doğmuş (bağlı muhasebe fişi VEYA
    /// MuhasebeDurumu=Onaylandi) bir belgeyi HİÇBİR KOŞULDA iptal etmez; bu kontrol, transaction
    /// içinde kilitli/güncel bir DB okumasına dayanır (bkz. görev 2) — GetByIdAsync ile alınan
    /// transaction-dışı bir ön kontrole ASLA güvenilmez. Bu giriş, hiçbir durumda
    /// SatisBelgesiFisiIptalEtAsync çağırmaz veya ters kayıt oluşturmaz.
    /// </summary>
    Task OperasyonelIptalEtAsync(int id, CancellationToken cancellationToken = default);
}
