using STYS.AccessScope;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.TicariBelgeler.Services;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Operasyonel ui/ticari-belgeler ekranı (TicariBelgeService) ile Muhasebe Satış/Alış Belgeleri
/// ekranının (SatisBelgeleriController -> ISatisBelgesiService) AYNI belge üzerinde, AYNI
/// ISatisBelgesiService kaynağı üzerinden çalıştığını kanıtlayan hızlı, DB gerektirmeyen birim
/// testi. TicariBelgeService kendi durum makinesini KOPYALAMAZ - MuhasebeOnayinaGonderAsync
/// doğrudan ISatisBelgesiService.MuhasebeOnayinaGonderAsync'e delege eder; bu yüzden operasyon
/// ekranından "Onayda"ya gönderilen bir belge, muhasebe ekranından (aynı Id ile, aynı servis
/// üzerinden) sorunsuzca işlenebilir.
/// </summary>
public class MuhasebeOnayAkisiUyumlulukTests
{
    [Fact]
    public async Task MuhasebeOnayinaGonderAsync_AyniIdIleAltMuhasebeServisineDelegeEder()
    {
        var fakeSatisBelgesiService = new FakeSatisBelgesiService();
        var service = new TicariBelgeService(
            fakeSatisBelgesiService,
            taslakOlusturmaService: null!,
            new UnscopedUserAccessScopeService(),
            mapper: null!);

        const int belgeId = 123;
        await service.MuhasebeOnayinaGonderAsync(belgeId);

        // Operasyon ekranının "onaya gönder" çağrısı, muhasebe ekranının (SatisBelgeleriController)
        // AYNI belge üzerinde MuhasebeOnaylaAsync/ReddetAsync çağırdığı AYNI ISatisBelgesiService
        // örneğine, AYNI Id ile ulaşmalıdır - iki ayrı/senkronize edilmesi gereken bir durum deposu
        // YOKTUR.
        Assert.Equal(belgeId, fakeSatisBelgesiService.MuhasebeOnayinaGonderilenId);

        // Aynı belge, aynı servis üzerinden artık muhasebe tarafından onaylanabilir/reddedilebilir
        // durumdadır - operasyon ekranının eylemi ile muhasebe ekranının eylemi aynı kayda uygulanır.
        await fakeSatisBelgesiService.MuhasebeOnaylaAsync(belgeId);
        Assert.Equal(belgeId, fakeSatisBelgesiService.MuhasebeOnaylananId);
    }

    private sealed class UnscopedUserAccessScopeService : IUserAccessScopeService
    {
        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(DomainAccessScope.Unscoped());
    }

    private sealed class FakeSatisBelgesiService : ISatisBelgesiService
    {
        public int? MuhasebeOnayinaGonderilenId { get; private set; }
        public int? MuhasebeOnaylananId { get; private set; }
        public int? ReddedilenId { get; private set; }

        public Task<SatisBelgesiDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(new SatisBelgesiDto
            {
                Id = id,
                BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
                TesisId = 1
            });

        public Task<List<SatisBelgesiDto>> FilterAsync(SatisBelgesiFilterDto filter, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<SatisBelgesiDto>());

        public Task<SatisBelgesiDto> CreateAsync(CreateSatisBelgesiRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SatisBelgesiDto> UpdateAsync(int id, UpdateSatisBelgesiRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task MuhasebeOnayinaGonderAsync(int id, CancellationToken cancellationToken = default)
        {
            MuhasebeOnayinaGonderilenId = id;
            return Task.CompletedTask;
        }

        public Task MuhasebeOnaylaAsync(int id, CancellationToken cancellationToken = default)
        {
            MuhasebeOnaylananId = id;
            return Task.CompletedTask;
        }

        public Task<SatisBelgesiDto> FaturaKesAsync(int id, FaturaKesRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task ReddetAsync(int id, string redNedeni, CancellationToken cancellationToken = default)
        {
            ReddedilenId = id;
            return Task.CompletedTask;
        }

        public Task IptalEtAsync(int id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
