using System.Collections.Generic;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

[Trait("Domain", "EBelge")]
[Trait("TestLevel", "Unit")]
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
    public async Task KurumIdEBelgeKaydiIdVeCancellationTokenAynenAktarilir()
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
    public async Task OutboxMesajiIdVeGercekKilitBilgisiAynenAktarilirVeLoglanmaz()
    {
        var (sut, service) = CreateSut();
        var kilitBitisi = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var baglam = CreateBaglam(outboxMesajiId: 55, kilitToken: "gercek-lease-token-guid", kilitBitisZamaniUtc: kilitBitisi);

        await sut.HandleAsync(baglam);

        Assert.Equal(55, service.LastTalep!.OutboxMesajiId);
        Assert.Equal("gercek-lease-token-guid", service.LastTalep.KilitToken);
        Assert.Equal(kilitBitisi, service.LastTalep.KilitBitisZamaniUtc);
    }

    [Fact]
    public async Task BasariliSonucBasariliHandlerSonucunaDoner()
    {
        var (sut, _) = CreateSut();

        var sonuc = await sut.HandleAsync(CreateBaglam());

        Assert.True(sonuc.BasariliMi);
        Assert.Equal(EBelgeOutboxHandlerSonucTuru.AtomikTamamlandi, sonuc.SonucTuru);
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
    public async Task AtomikKaliciHataAtomikTerminalHataSonucunaDoner()
    {
        const string hataKodu = "ARTEF-02";
        const string hataMesaji = "kalıcı artefakt hatası";
        var (sut, _) = CreateSut((_, _) => Task.FromResult<EBelgeArtefaktOlusturmaSonucu?>(EBelgeArtefaktOlusturmaSonucu.AtomikKaliciHata(hataKodu, hataMesaji)));

        var sonuc = await sut.HandleAsync(CreateBaglam());

        // Artefakt servisi kalıcı hatayı KENDİ atomik transaction'ında outbox'a zaten
        // yansıtmıştır (bkz. EBelgeArtefaktOlusturmaService.SonuclandirKaliciHataAtomikAsync) -
        // IsleAsync İKİNCİ bir DB geçişi yapmamalı, bu yüzden hata detayları burada TEKRAR
        // taşınmaz (zaten kalıcılaştırılmıştır).
        Assert.False(sonuc.BasariliMi);
        Assert.Equal(EBelgeOutboxHandlerSonucTuru.AtomikTerminalHata, sonuc.SonucTuru);
        Assert.Null(sonuc.HataSinifi);
        Assert.Null(sonuc.HataKodu);
        Assert.Null(sonuc.HataMesaji);
    }

    [Fact]
    public async Task SahiplikKaybedildiSonucuAynenYansir()
    {
        var (sut, _) = CreateSut((_, _) => Task.FromResult<EBelgeArtefaktOlusturmaSonucu?>(EBelgeArtefaktOlusturmaSonucu.SahiplikKaybedildi()));

        var sonuc = await sut.HandleAsync(CreateBaglam());

        Assert.False(sonuc.BasariliMi);
        Assert.Equal(EBelgeOutboxHandlerSonucTuru.SahiplikKaybedildi, sonuc.SonucTuru);
        Assert.Null(sonuc.HataSinifi);
        Assert.Null(sonuc.HataKodu);
        Assert.Null(sonuc.HataMesaji);
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
            return Task.FromResult<EBelgeArtefaktOlusturmaSonucu?>(EBelgeArtefaktOlusturmaSonucu.AtomikBasarili());
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
        Assert.Throws<BaseException>(() => EBelgeArtefaktOlusturmaSonucu.AtomikKaliciHata(hataKodu, hataMesaji));
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
        => Task.FromResult<EBelgeArtefaktOlusturmaSonucu?>(EBelgeArtefaktOlusturmaSonucu.AtomikBasarili());

    private static EBelgeOutboxIslemBaglami CreateBaglam(
        int kurumId = 42,
        int eBelgeKaydiId = 7,
        int outboxMesajiId = 11,
        EBelgeOutboxIsTuru isTuru = EBelgeOutboxIsTuru.ArtefaktOlustur,
        string kilitToken = "test-lease-token",
        DateTime? kilitBitisZamaniUtc = null)
        => new(
            OutboxMesajiId: outboxMesajiId,
            KurumId: kurumId,
            EBelgeKaydiId: eBelgeKaydiId,
            IsTuru: isTuru,
            DenemeSayisi: 1,
            KilitToken: kilitToken,
            KilitBitisZamaniUtc: kilitBitisZamaniUtc ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

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
