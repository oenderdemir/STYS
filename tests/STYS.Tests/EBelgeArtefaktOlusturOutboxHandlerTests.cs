using System.Collections.Generic;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class EBelgeArtefaktOlusturOutboxHandlerTests
{
    [Fact]
    public void HandlerinDestekledigiIsTuruArtefaktOlusturOlur()
    {
        var (sut, _) = CreateSut();

        Assert.Equal(EBelgeOutboxIsTuru.ArtefaktOlustur, sut.IsTuru);
    }

    [Fact]
    public async Task BasariliAkistaServisYalnizBirKezCagrilir()
    {
        var (sut, service) = CreateSut();

        await sut.HandleAsync(CreateBaglam());

        Assert.Equal(1, service.CallCount);
    }

    [Fact]
    public async Task KurumIdEBelgeKaydiIdVeTokenAynenAktarilir()
    {
        var (sut, service) = CreateSut();
        using var cts = new CancellationTokenSource();
        var claimToken = cts.Token;
        var baglam = CreateBaglam(kurumId: 17, eBelgeKaydiId: 29);

        await sut.HandleAsync(baglam, claimToken);

        Assert.Equal(17, service.LastTalep!.KurumId);
        Assert.Equal(29, service.LastTalep.EBelgeKaydiId);
        Assert.Equal(claimToken, service.LastToken);
    }

    [Fact]
    public async Task BasariliSonucBasariliHandlerSonucunaDoner()
    {
        var (sut, _) = CreateSut();

        var sonuc = await sut.HandleAsync(CreateBaglam());

        Assert.True(sonuc.BasariliMi);
        Assert.Null(sonuc.HataSinifi);
        Assert.Null(sonuc.HataKodu);
        Assert.Null(sonuc.HataMesaji);
    }

    [Fact]
    public async Task GeciciHataGeciciOutboxSonucunaDoner()
    {
        const string hataKodu = "ARTEF-01";
        const string hataMesaji = "geçici artefakt hatası";
        var (sut, _) = CreateSut((_, _) => Task.FromResult<EBelgeArtefaktOlusturmaSonucu?>(EBelgeArtefaktOlusturmaSonucu.GeciciHata(hataKodu, hataMesaji)));

        var sonuc = await sut.HandleAsync(CreateBaglam());

        Assert.False(sonuc.BasariliMi);
        Assert.Equal(EBelgeOutboxHataSinifi.Gecici, sonuc.HataSinifi);
        Assert.Equal(hataKodu, sonuc.HataKodu);
        Assert.Equal(hataMesaji, sonuc.HataMesaji);
    }

    [Fact]
    public async Task KaliciHataKaliciOutboxSonucunaDoner()
    {
        const string hataKodu = "ARTEF-02";
        const string hataMesaji = "kalıcı artefakt hatası";
        var (sut, _) = CreateSut((_, _) => Task.FromResult<EBelgeArtefaktOlusturmaSonucu?>(EBelgeArtefaktOlusturmaSonucu.KaliciHata(hataKodu, hataMesaji)));

        var sonuc = await sut.HandleAsync(CreateBaglam());

        Assert.False(sonuc.BasariliMi);
        Assert.Equal(EBelgeOutboxHataSinifi.Kalici, sonuc.HataSinifi);
        Assert.Equal(hataKodu, sonuc.HataKodu);
        Assert.Equal(hataMesaji, sonuc.HataMesaji);
    }

    [Fact]
    public async Task BeklenmeyenExceptionAynenYayilir()
    {
        var beklenen = new InvalidOperationException("beklenmeyen-artefakt");
        var (sut, service) = CreateSut((_, _) => throw beklenen);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.HandleAsync(CreateBaglam()));

        Assert.Same(beklenen, actual);
        Assert.Equal("beklenmeyen-artefakt", actual.Message);
        Assert.Equal(1, service.CallCount);
    }

    [Fact]
    public async Task CancellationTokenAynenYayilir()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var (sut, service) = CreateSut((_, token) =>
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult<EBelgeArtefaktOlusturmaSonucu?>(EBelgeArtefaktOlusturmaSonucu.Basarili());
        });

        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.HandleAsync(CreateBaglam(), cts.Token));

        Assert.Equal(1, service.CallCount);
        Assert.True(service.LastToken.IsCancellationRequested);
    }

    [Fact]
    public async Task NullBaglamHTTP400VeServisCagrilmaz()
    {
        var (sut, service) = CreateSut();

        var ex = await Assert.ThrowsAsync<BaseException>(() => sut.HandleAsync(null!));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public async Task YanlisIsTuruHTTP400VeServisCagrilmaz()
    {
        var (sut, service) = CreateSut();

        var ex = await Assert.ThrowsAsync<BaseException>(() => sut.HandleAsync(CreateBaglam(isTuru: (EBelgeOutboxIsTuru)999)));

        Assert.Equal(400, ex.ErrorCode);
        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public async Task NullServisSonucuKontrolluExceptionUretir()
    {
        var (sut, _) = CreateSut((_, _) => Task.FromResult<EBelgeArtefaktOlusturmaSonucu?>(null));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.HandleAsync(CreateBaglam()));

        Assert.Contains("null sonuç", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(GecersizHataAlanlariCases))]
    public void GecersizHataAlanlariReddedilir(string hataKodu, string hataMesaji)
    {
        Assert.Throws<BaseException>(() => EBelgeArtefaktOlusturmaSonucu.GeciciHata(hataKodu, hataMesaji));
        Assert.Throws<BaseException>(() => EBelgeArtefaktOlusturmaSonucu.KaliciHata(hataKodu, hataMesaji));
    }

    public static IEnumerable<object[]> GecersizHataAlanlariCases()
    {
        yield return new object[] { "", "mesaj" };
        yield return new object[] { new string('A', 101), "mesaj" };
        yield return new object[] { "KOD-1", "" };
        yield return new object[] { "KOD-1", new string('B', 2001) };
    }

    private static (EBelgeArtefaktOlusturOutboxHandler Handler, FakeArtefaktOlusturmaService Service) CreateSut(
        Func<EBelgeArtefaktOlusturmaTalebi, CancellationToken, Task<EBelgeArtefaktOlusturmaSonucu?>>? callback = null)
    {
        var service = new FakeArtefaktOlusturmaService(callback ?? DefaultCallback);
        return (new EBelgeArtefaktOlusturOutboxHandler(service), service);
    }

    private static Task<EBelgeArtefaktOlusturmaSonucu?> DefaultCallback(
        EBelgeArtefaktOlusturmaTalebi _,
        CancellationToken __)
        => Task.FromResult<EBelgeArtefaktOlusturmaSonucu?>(EBelgeArtefaktOlusturmaSonucu.Basarili());

    private static EBelgeOutboxIslemBaglami CreateBaglam(
        int kurumId = 42,
        int eBelgeKaydiId = 7,
        EBelgeOutboxIsTuru isTuru = EBelgeOutboxIsTuru.ArtefaktOlustur)
        => new(
            OutboxMesajiId: 11,
            KurumId: kurumId,
            EBelgeKaydiId: eBelgeKaydiId,
            IsTuru: isTuru,
            DenemeSayisi: 1);

    private sealed class FakeArtefaktOlusturmaService : IEBelgeArtefaktOlusturmaService
    {
        private readonly Func<EBelgeArtefaktOlusturmaTalebi, CancellationToken, Task<EBelgeArtefaktOlusturmaSonucu?>> _callback;

        public FakeArtefaktOlusturmaService(Func<EBelgeArtefaktOlusturmaTalebi, CancellationToken, Task<EBelgeArtefaktOlusturmaSonucu?>> callback)
        {
            _callback = callback;
        }

        public int CallCount { get; private set; }

        public EBelgeArtefaktOlusturmaTalebi? LastTalep { get; private set; }

        public CancellationToken LastToken { get; private set; }

        public async Task<EBelgeArtefaktOlusturmaSonucu?> OlusturAsync(
            EBelgeArtefaktOlusturmaTalebi talep,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastTalep = talep;
            LastToken = cancellationToken;
            return await _callback(talep, cancellationToken);
        }
    }
}
