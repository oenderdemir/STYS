using STYS.Muhasebe.SatisBelgeleri.Dtos;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// Operasyon modülleri (otel, restoran, kamp vb.) için ortak satış belgesi taslağı oluşturma servisi.
/// Kaynak modül bilgisi, müşteri/fatura bilgileri ve satış satırlarını alır;
/// validasyon, access scope ve duplicate kontrolü yaparak ISatisBelgesiService.CreateAsync çağırır.
/// </summary>
public interface ISatisBelgesiTaslakOlusturmaService
{
    Task<SatisBelgesiDto> KaynaktanTaslakOlusturAsync(
        SatisBelgesiTaslakOlusturRequest request,
        CancellationToken cancellationToken = default);
}
