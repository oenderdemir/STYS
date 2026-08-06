using Microsoft.Extensions.Logging.Abstractions;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

[Trait("Domain", "EBelge")]
[Trait("TestLevel", "Unit")]
public class EBelgeOutboxMesajIslemeServiceTests
{
    private static readonly Guid TestToken = Guid.Parse("aabbccdd-eeff-aabb-ccdd-eeffaabbccdd");

    [Fact]
    public async Task GecerliClaimVeBasariliHandlerIleCompleteBirKezCagrilir()
    {
        var handler = new FakeHandler(
            EBelgeOutboxIsTuru.ArtefaktOlustur,
            _ => EBelgeOutboxHandlerSonucu.Basarili());
        var sut = CreateSut(handler);
        var transition = sut.Transition;

        var sonuc = await sut.Service.IsleAsync(CreateClaim());

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(1, transition.CompleteCallCount);
        Assert.Equal(0, transition.FailCallCount);
        Assert.Equal(EBelgeOutboxIslemeSonucuTuru.Tamamlandi, sonuc.SonucTuru);
    }

    [Fact]
    public async Task AtomikTamamlandiSonucundaCompleteIkinciKezCagrilmaz()
    {
        // Handler (ör. artefakt oluşturma), outbox geçişini KENDİ atomik transaction'ında zaten
        // yapmıştır (bkz. Faz 2B.6.1 görev md.3) - IsleAsync bu durumda TryCompleteAsync'i İKİNCİ
        // kez ÇAĞIRMAMALIDIR.
        var handler = new FakeHandler(
            EBelgeOutboxIsTuru.ArtefaktOlustur,
            _ => EBelgeOutboxHandlerSonucu.AtomikTamamlandi());
        var sut = CreateSut(handler);

        var sonuc = await sut.Service.IsleAsync(CreateClaim());

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(0, sut.Transition.CompleteCallCount);
        Assert.Equal(0, sut.Transition.FailCallCount);
        Assert.Equal(EBelgeOutboxIslemeSonucuTuru.Tamamlandi, sonuc.SonucTuru);
    }

    [Fact]
    public async Task AtomikTerminalHataSonucundaFailIkinciKezCagrilmaz()
    {
        var handler = new FakeHandler(
            EBelgeOutboxIsTuru.ArtefaktOlustur,
            _ => EBelgeOutboxHandlerSonucu.AtomikTerminalHata());
        var sut = CreateSut(handler);

        var sonuc = await sut.Service.IsleAsync(CreateClaim());

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(0, sut.Transition.CompleteCallCount);
        Assert.Equal(0, sut.Transition.FailCallCount);
        Assert.Equal(EBelgeOutboxIslemeSonucuTuru.TerminalHata, sonuc.SonucTuru);
    }

    [Fact]
    public async Task SahiplikKaybedildiSonucundaHicbirTransitionCagrilmaz()
    {
        var handler = new FakeHandler(
            EBelgeOutboxIsTuru.ArtefaktOlustur,
            _ => EBelgeOutboxHandlerSonucu.SahiplikKaybedildi());
        var sut = CreateSut(handler);

        var sonuc = await sut.Service.IsleAsync(CreateClaim());

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(0, sut.Transition.CompleteCallCount);
        Assert.Equal(0, sut.Transition.FailCallCount);
        Assert.Equal(EBelgeOutboxIslemeSonucuTuru.SahiplikKaybedildi, sonuc.SonucTuru);
    }

    [Fact]
    public async Task TryCompleteAsyncExceptionUretirseExceptionYayilirFailVePolicyCagrilmaz()
    {
        var sut = CreateSut(
            new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarili()),
            transition: new FakeTransitionService(completeException: new InvalidOperationException("complete-hata")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Service.IsleAsync(CreateClaim()));

        Assert.Equal(1, sut.Handler.CallCount);
        Assert.Equal(1, sut.Transition.CompleteCallCount);
        Assert.Equal(0, sut.Transition.FailCallCount);
        Assert.Equal(0, sut.Policy.CallCount);
    }

    [Fact]
    public async Task NormalFailAkisindaTryFailAsyncExceptionUretirseExceptionYayilirVeFailYalnizBirKezCagrilir()
    {
        var sut = CreateSut(
            new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarisiz(EBelgeOutboxHataSinifi.Gecici, "E1", "mesaj")),
            transition: new FakeTransitionService(failException: new InvalidOperationException("fail-hata")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Service.IsleAsync(CreateClaim(denemeSayisi: 1)));

        Assert.Equal(1, sut.Handler.CallCount);
        Assert.Equal(1, sut.Policy.CallCount);
        Assert.Equal(1, sut.Transition.FailCallCount);
    }

    [Fact]
    public async Task RetryPolicyExceptionUretirseExceptionYayilirPolicyYalnizBirKezCagrilirVeTransitionCagrilmaz()
    {
        var sut = CreateSut(
            new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarisiz(EBelgeOutboxHataSinifi.Gecici, "E1", "mesaj")),
            policy: new FakePolicy(EBelgeOutboxRetryKarari.Retry(TimeSpan.FromMinutes(1)), new InvalidOperationException("policy-hata")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Service.IsleAsync(CreateClaim(denemeSayisi: 1)));

        Assert.Equal(1, sut.Handler.CallCount);
        Assert.Equal(1, sut.Policy.CallCount);
        Assert.Equal(0, sut.Transition.FailCallCount);
        Assert.Equal(0, sut.Transition.CompleteCallCount);
    }

    [Fact]
    public async Task BuyukHarfliGecerliTokenCompleteOnceNormalizeEdilir()
    {
        var sut = CreateSut(new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarili()));
        var beklenenKanonikToken = TestToken.ToString("D");
        var buyukHarfliToken = beklenenKanonikToken.ToUpperInvariant();

        Assert.NotEqual(beklenenKanonikToken, buyukHarfliToken);

        var claim = CreateClaim(kilitToken: buyukHarfliToken);

        await sut.Service.IsleAsync(claim);

        Assert.Equal(beklenenKanonikToken, sut.Transition.LastCompleteKilitToken);
    }

    [Fact]
    public async Task BuyukHarfliGecerliTokenFailOnceNormalizeEdilir()
    {
        var sut = CreateSut(
            new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarisiz(EBelgeOutboxHataSinifi.Gecici, "E1", "mesaj")),
            policy: new FakePolicy(EBelgeOutboxRetryKarari.Terminal()));
        var beklenenKanonikToken = TestToken.ToString("D");
        var buyukHarfliToken = beklenenKanonikToken.ToUpperInvariant();

        Assert.NotEqual(beklenenKanonikToken, buyukHarfliToken);

        var claim = CreateClaim(kilitToken: buyukHarfliToken);

        await sut.Service.IsleAsync(claim);

        Assert.Equal(beklenenKanonikToken, sut.Transition.LastFailKilitToken);
    }

    [Fact]
    public void TanimsizEnumDegeriniDesteklediginiBildirenHandlerReddedilir()
    {
        var handler = new FakeHandler((EBelgeOutboxIsTuru)999, _ => EBelgeOutboxHandlerSonucu.Basarili());

        Assert.Throws<InvalidOperationException>(() => CreateService(new[] { handler }));
    }

    [Fact]
    public async Task NullClaimBaseExceptionIleReddedilirVeHiçbirBagimlilikCagrilmaz()
    {
        var sut = CreateSut(new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarili()));

        var ex = await Assert.ThrowsAsync<BaseException>(() => sut.Service.IsleAsync(null!));

        Assert.Equal(400, ex.ErrorCode);

        Assert.Equal(0, sut.Handler.CallCount);
        Assert.Equal(0, sut.Policy.CallCount);
        Assert.Equal(0, sut.Transition.CompleteCallCount);
        Assert.Equal(0, sut.Transition.FailCallCount);
    }

    [Fact]
    public async Task CompleteTrueIseSonucTamamlandiOlur()
    {
        var sut = CreateSut(new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarili()));

        var sonuc = await sut.Service.IsleAsync(CreateClaim());

        Assert.Equal(EBelgeOutboxIslemeSonucuTuru.Tamamlandi, sonuc.SonucTuru);
    }

    [Fact]
    public async Task CompleteFalseIseSonucSahiplikKaybedildiOlur()
    {
        var sut = CreateSut(new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarili()), completeResult: false);

        var sonuc = await sut.Service.IsleAsync(CreateClaim());

        Assert.Equal(EBelgeOutboxIslemeSonucuTuru.SahiplikKaybedildi, sonuc.SonucTuru);
    }

    [Fact]
    public async Task CompleteFalseSonrasindaFailCagrilmaz()
    {
        var sut = CreateSut(new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarili()), completeResult: false);

        await sut.Service.IsleAsync(CreateClaim());

        Assert.Equal(1, sut.Transition.CompleteCallCount);
        Assert.Equal(0, sut.Transition.FailCallCount);
    }

    [Fact]
    public async Task GeciciHataVeDenemeSayisiBirIcinFailBirDakikaGonderilir()
    {
        var sut = CreateSut(new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarisiz(EBelgeOutboxHataSinifi.Gecici, "E1", "mesaj")));

        await sut.Service.IsleAsync(CreateClaim(denemeSayisi: 1));

        Assert.Equal(TimeSpan.FromMinutes(1), sut.Transition.LastFailRetryDelay);
    }

    [Fact]
    public async Task GeciciHataVeDenemeSayisiBesIcinFailAltiSaatGonderilir()
    {
        var sut = CreateSut(
            new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarisiz(EBelgeOutboxHataSinifi.Gecici, "E1", "mesaj")),
            new FakePolicy(EBelgeOutboxRetryKarari.Retry(TimeSpan.FromHours(6))));

        await sut.Service.IsleAsync(CreateClaim(denemeSayisi: 5));

        Assert.Equal(TimeSpan.FromHours(6), sut.Transition.LastFailRetryDelay);
    }

    [Fact]
    public async Task GeciciHataVeDenemeSayisiAltiIcinFailNullGonderilir()
    {
        var sut = CreateSut(
            new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarisiz(EBelgeOutboxHataSinifi.Gecici, "E1", "mesaj")),
            new FakePolicy(EBelgeOutboxRetryKarari.Terminal()));

        await sut.Service.IsleAsync(CreateClaim(denemeSayisi: 6));

        Assert.Null(sut.Transition.LastFailRetryDelay);
    }

    [Fact]
    public async Task KaliciHataIlkDenemedeFailNullGonderilir()
    {
        var sut = CreateSut(
            new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarisiz(EBelgeOutboxHataSinifi.Kalici, "E1", "mesaj")),
            new FakePolicy(EBelgeOutboxRetryKarari.Terminal()));

        await sut.Service.IsleAsync(CreateClaim(denemeSayisi: 1));

        Assert.Null(sut.Transition.LastFailRetryDelay);
    }

    [Fact]
    public async Task FailTrueVeRetryDoluIseSonucRetryPlanlandiOlur()
    {
        var sut = CreateSut(
            new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarisiz(EBelgeOutboxHataSinifi.Gecici, "E1", "mesaj")),
            new FakePolicy(EBelgeOutboxRetryKarari.Retry(TimeSpan.FromMinutes(1))));

        var sonuc = await sut.Service.IsleAsync(CreateClaim(denemeSayisi: 1));

        Assert.Equal(EBelgeOutboxIslemeSonucuTuru.RetryPlanlandi, sonuc.SonucTuru);
    }

    [Fact]
    public async Task FailTrueVeRetryNullIseSonucTerminalHataOlur()
    {
        var sut = CreateSut(
            new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarisiz(EBelgeOutboxHataSinifi.Gecici, "E1", "mesaj")),
            new FakePolicy(EBelgeOutboxRetryKarari.Terminal()));

        var sonuc = await sut.Service.IsleAsync(CreateClaim(denemeSayisi: 6));

        Assert.Equal(EBelgeOutboxIslemeSonucuTuru.TerminalHata, sonuc.SonucTuru);
    }

    [Fact]
    public async Task FailFalseIseSonucSahiplikKaybedildiOlur()
    {
        var sut = CreateSut(
            new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarisiz(EBelgeOutboxHataSinifi.Gecici, "E1", "mesaj")),
            failResult: false);

        var sonuc = await sut.Service.IsleAsync(CreateClaim(denemeSayisi: 1));

        Assert.Equal(EBelgeOutboxIslemeSonucuTuru.SahiplikKaybedildi, sonuc.SonucTuru);
    }

    [Fact]
    public async Task BasarisizIslemdeCompleteCagrilmaz()
    {
        var sut = CreateSut(new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarisiz(EBelgeOutboxHataSinifi.Gecici, "E1", "mesaj")));

        await sut.Service.IsleAsync(CreateClaim(denemeSayisi: 1));

        Assert.Equal(0, sut.Transition.CompleteCallCount);
        Assert.Equal(1, sut.Transition.FailCallCount);
    }

    [Fact]
    public async Task HandlerBaglaminaDogruAlanlarGider()
    {
        var handler = new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarili());
        var claim = CreateClaim(denemeSayisi: 4);
        var sut = CreateSut(handler);

        await sut.Service.IsleAsync(claim);

        Assert.NotNull(handler.LastBaglam);
        Assert.Equal(claim.OutboxMesajiId, handler.LastBaglam!.OutboxMesajiId);
        Assert.Equal(claim.KurumId, handler.LastBaglam.KurumId);
        Assert.Equal(claim.EBelgeKaydiId, handler.LastBaglam.EBelgeKaydiId);
        Assert.Equal(claim.IsTuru, handler.LastBaglam.IsTuru);
        Assert.Equal(claim.DenemeSayisi, handler.LastBaglam.DenemeSayisi);
    }

    [Fact]
    public async Task TransitionaDogruClaimVeTokenGider()
    {
        var sut = CreateSut(new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarili()));
        var claim = CreateClaim();

        await sut.Service.IsleAsync(claim);

        Assert.Equal(claim.OutboxMesajiId, sut.Transition.LastCompleteOutboxMesajiId);
        Assert.Equal(claim.KurumId, sut.Transition.LastCompleteKurumId);
        Assert.Equal(claim.KilitToken, sut.Transition.LastCompleteKilitToken);
    }

    [Fact]
    public async Task DenemeSayisiPolicyyeArtirilmadanGider()
    {
        var policy = new FakePolicy(EBelgeOutboxRetryKarari.Retry(TimeSpan.FromMinutes(1)));
        var sut = CreateSut(new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarisiz(EBelgeOutboxHataSinifi.Gecici, "E1", "mesaj")), policy);

        await sut.Service.IsleAsync(CreateClaim(denemeSayisi: 3));

        Assert.Equal(3, policy.LastDenemeSayisi);
        Assert.Equal(1, policy.CallCount);
    }

    [Fact]
    public async Task PolicyYalnizBirKezCagrilir()
    {
        var policy = new FakePolicy(EBelgeOutboxRetryKarari.Retry(TimeSpan.FromMinutes(1)));
        var sut = CreateSut(new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarisiz(EBelgeOutboxHataSinifi.Gecici, "E1", "mesaj")), policy);

        await sut.Service.IsleAsync(CreateClaim(denemeSayisi: 1));

        Assert.Equal(1, policy.CallCount);
    }

    [Fact]
    public async Task BasariliIslemdePolicyVeFailCagrilmaz()
    {
        var policy = new FakePolicy(EBelgeOutboxRetryKarari.Retry(TimeSpan.FromMinutes(1)));
        var sut = CreateSut(new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarili()), policy);

        await sut.Service.IsleAsync(CreateClaim());

        Assert.Equal(0, policy.CallCount);
        Assert.Equal(0, sut.Transition.FailCallCount);
    }

    [Fact]
    public async Task BeklenmeyenExceptionGuvenliGeciciHataOlarakIslenir()
    {
        var handler = new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => throw new InvalidOperationException("gizli hata"));
        var policy = new FakePolicy(EBelgeOutboxRetryKarari.Retry(TimeSpan.FromMinutes(1)));
        var sut = CreateSut(handler, policy);

        var sonuc = await sut.Service.IsleAsync(CreateClaim(denemeSayisi: 1));

        Assert.Equal(EBelgeOutboxIslemeSonucuTuru.RetryPlanlandi, sonuc.SonucTuru);
        Assert.Equal("EBEKLENMEYENISLEMHATASI", sut.Transition.LastFailHataKodu);
        Assert.Equal("E-belge outbox işi işlenirken beklenmeyen bir hata oluştu.", sut.Transition.LastFailHataMesaji);
    }

    [Fact]
    public async Task BeklenmeyenExceptionMesajiTransitionaAktarilmaz()
    {
        var handler = new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => throw new InvalidOperationException("gizli hata"));
        var policy = new FakePolicy(EBelgeOutboxRetryKarari.Retry(TimeSpan.FromMinutes(1)));
        var sut = CreateSut(handler, policy);

        await sut.Service.IsleAsync(CreateClaim(denemeSayisi: 1));

        Assert.DoesNotContain("gizli hata", sut.Transition.LastFailHataMesaji);
    }

    [Fact]
    public async Task IptalEdilenTokenYenidenFirlatilirVeTransitionCagrilmaz()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, (_, token) =>
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(EBelgeOutboxHandlerSonucu.Basarili());
        });
        var sut = CreateSut(handler);

        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.Service.IsleAsync(CreateClaim(), cts.Token));
        Assert.Equal(0, sut.Transition.CompleteCallCount);
        Assert.Equal(0, sut.Transition.FailCallCount);
    }

    [Fact]
    public async Task DesteklenmeyenIsTuruBasariliKabulEdilmez()
    {
        var sut = CreateSut(new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarili()));
        var claim = CreateClaim(isTuru: (EBelgeOutboxIsTuru)999);

        var sonuc = await sut.Service.IsleAsync(claim);

        Assert.Equal(EBelgeOutboxIslemeSonucuTuru.TerminalHata, sonuc.SonucTuru);
        Assert.Equal(0, sut.Policy.CallCount);
        Assert.Equal(0, sut.Handler.CallCount);
        Assert.Equal(1, sut.Transition.FailCallCount);
        Assert.Equal("EBELGE_DESTEKLENMEYEN_IS_TURU", sut.Transition.LastFailHataKodu);
    }

    [Fact]
    public void AyniIsTuruIcinBirdenFazlaHandlerKabulEdilmez()
    {
        var handler1 = new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarili());
        var handler2 = new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarili());

        Assert.Throws<InvalidOperationException>(() => CreateService(new[] { handler1, handler2 }));
    }

    [Fact]
    public void HataKoduVeMesajiFactoryValidation()
    {
        Assert.Throws<BaseException>(() => EBelgeOutboxHandlerSonucu.Basarisiz(EBelgeOutboxHataSinifi.Gecici, "", "mesaj"));
        Assert.Throws<BaseException>(() => EBelgeOutboxHandlerSonucu.Basarisiz(EBelgeOutboxHataSinifi.Gecici, new string('A', 101), "mesaj"));
        Assert.Throws<BaseException>(() => EBelgeOutboxHandlerSonucu.Basarisiz(EBelgeOutboxHataSinifi.Gecici, "E1", ""));
        Assert.Throws<BaseException>(() => EBelgeOutboxHandlerSonucu.Basarisiz(EBelgeOutboxHataSinifi.Gecici, "E1", new string('B', 2001)));
    }

    [Fact]
    public void BilinmeyenHataSinifiFactoryValidation()
    {
        Assert.Throws<BaseException>(() => EBelgeOutboxHandlerSonucu.Basarisiz((EBelgeOutboxHataSinifi)int.MaxValue, "E1", "mesaj"));
    }

    [Fact]
    public void BasariliHandlerSonucuHataAlanlariTasinamaz()
    {
        var sonuc = EBelgeOutboxHandlerSonucu.Basarili();

        Assert.True(sonuc.BasariliMi);
        Assert.Null(sonuc.HataSinifi);
        Assert.Null(sonuc.HataKodu);
        Assert.Null(sonuc.HataMesaji);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GecersizOutboxMesajiIdReddedilir(int outboxMesajiId)
    {
        var sut = CreateSut(new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarili()));

        await Assert.ThrowsAsync<BaseException>(() => sut.Service.IsleAsync(CreateClaim(outboxMesajiId: outboxMesajiId)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GecersizKurumIdReddedilir(int kurumId)
    {
        var sut = CreateSut(new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarili()));

        await Assert.ThrowsAsync<BaseException>(() => sut.Service.IsleAsync(CreateClaim(kurumId: kurumId)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GecersizEBelgeKaydiIdReddedilir(int eBelgeKaydiId)
    {
        var sut = CreateSut(new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarili()));

        await Assert.ThrowsAsync<BaseException>(() => sut.Service.IsleAsync(CreateClaim(eBelgeKaydiId: eBelgeKaydiId)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SifirVeyaNegatifDenemeSayisiReddedilir(int denemeSayisi)
    {
        var sut = CreateSut(new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarili()));

        await Assert.ThrowsAsync<BaseException>(() => sut.Service.IsleAsync(CreateClaim(denemeSayisi: denemeSayisi)));
    }

    [Theory]
    [InlineData(EBelgeOutboxDurumu.Bekliyor)]
    [InlineData(EBelgeOutboxDurumu.Tamamlandi)]
    [InlineData(EBelgeOutboxDurumu.Hata)]
    public async Task IsleniyorDisiDurumReddedilir(EBelgeOutboxDurumu durum)
    {
        var sut = CreateSut(new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarili()));

        await Assert.ThrowsAsync<BaseException>(() => sut.Service.IsleAsync(CreateClaim(durum: durum)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task BosVeyaGecersizTokenReddedilir(string token)
    {
        var sut = CreateSut(new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarili()));

        await Assert.ThrowsAsync<BaseException>(() => sut.Service.IsleAsync(CreateClaim(kilitToken: token)));
    }

    [Fact]
    public async Task GecersizClaimdeHandlerPolicyTransitionCagrilmaz()
    {
        var handler = new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarili());
        var sut = CreateSut(handler);

        await Assert.ThrowsAsync<BaseException>(() => sut.Service.IsleAsync(CreateClaim(outboxMesajiId: 0)));

        Assert.Equal(0, handler.CallCount);
        Assert.Equal(0, sut.Policy.CallCount);
        Assert.Equal(0, sut.Transition.CompleteCallCount);
        Assert.Equal(0, sut.Transition.FailCallCount);
    }

    [Fact]
    public void RetryPlanlandiSonucundaGecikmeDoluDigerSonuclarNullOlur()
    {
        var retry = EBelgeOutboxIslemeSonucu.RetryPlanlandi(TimeSpan.FromMinutes(1));
        var tamamlandi = EBelgeOutboxIslemeSonucu.Tamamlandi();
        var terminal = EBelgeOutboxIslemeSonucu.TerminalHata();
        var sahiplik = EBelgeOutboxIslemeSonucu.SahiplikKaybedildi();

        Assert.NotNull(retry.RetryGecikmesi);
        Assert.Null(tamamlandi.RetryGecikmesi);
        Assert.Null(terminal.RetryGecikmesi);
        Assert.Null(sahiplik.RetryGecikmesi);
    }

    [Fact]
    public async Task TransitionFalseOldugundaIkinciGecisDenemez()
    {
        var sut = CreateSut(
            new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarisiz(EBelgeOutboxHataSinifi.Gecici, "E1", "mesaj")),
            failResult: false);

        await sut.Service.IsleAsync(CreateClaim());

        Assert.Equal(0, sut.Transition.CompleteCallCount);
        Assert.Equal(1, sut.Transition.FailCallCount);
    }

    // ---- Faz 2B.10 görev md.14 - claim SONRASI kurum politikası savunma katmanı ----

    [Fact]
    public async Task PolitikaIzinliDegilseHandlerHicCagrilmazVeGuvenliRetryPlanlanir()
    {
        var handler = new FakeHandler(EBelgeOutboxIsTuru.UblImzala, _ => EBelgeOutboxHandlerSonucu.Basarili());
        var politika = new FakePolitikaServisi { IzinliMi = false };
        var transition = new FakeTransitionService();
        var service = CreateService(new[] { handler }, transition: transition, kurumPolitikaServisi: politika);

        var sonuc = await service.IsleAsync(CreateClaim(isTuru: EBelgeOutboxIsTuru.UblImzala));

        Assert.Equal(0, handler.CallCount);
        Assert.Equal(EBelgeOutboxIslemeSonucuTuru.RetryPlanlandi, sonuc.SonucTuru);
        Assert.Equal(TimeSpan.FromMinutes(5), sonuc.RetryGecikmesi);
        Assert.Equal(1, transition.FailCallCount);
        Assert.Equal(0, transition.CompleteCallCount);
        Assert.StartsWith("EBELGE_KURUM_POLICY", transition.LastFailHataKodu);
    }

    [Fact]
    public async Task PolitikaIzinliDegilseVeGuvenliRetryBasarisizsaSahiplikKaybedildiDoner()
    {
        var handler = new FakeHandler(EBelgeOutboxIsTuru.UblImzala, _ => EBelgeOutboxHandlerSonucu.Basarili());
        var politika = new FakePolitikaServisi { IzinliMi = false };
        var transition = new FakeTransitionService(failResult: false);
        var service = CreateService(new[] { handler }, transition: transition, kurumPolitikaServisi: politika);

        var sonuc = await service.IsleAsync(CreateClaim(isTuru: EBelgeOutboxIsTuru.UblImzala));

        Assert.Equal(0, handler.CallCount);
        Assert.Equal(EBelgeOutboxIslemeSonucuTuru.SahiplikKaybedildi, sonuc.SonucTuru);
    }

    [Fact]
    public async Task PolitikaIzinliIseNormalAkisHicEtkilenmez()
    {
        var handler = new FakeHandler(EBelgeOutboxIsTuru.ArtefaktOlustur, _ => EBelgeOutboxHandlerSonucu.Basarili());
        var politika = new FakePolitikaServisi { IzinliMi = true };
        var service = CreateService(new[] { handler }, kurumPolitikaServisi: politika);

        var sonuc = await service.IsleAsync(CreateClaim());

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(1, politika.IslemHalaIzinliMiCagriSayisi);
        Assert.Equal(EBelgeOutboxIslemeSonucuTuru.Tamamlandi, sonuc.SonucTuru);
    }

    [Fact]
    public async Task PolitikaKontroluClaimAlanlariIleDogruCagrilir()
    {
        var handler = new FakeHandler(EBelgeOutboxIsTuru.UblImzala, _ => EBelgeOutboxHandlerSonucu.Basarili());
        var politika = new FakePolitikaServisi { IzinliMi = true };
        var service = CreateService(new[] { handler }, kurumPolitikaServisi: politika);
        var claim = CreateClaim(kurumId: 42, eBelgeKaydiId: 7, isTuru: EBelgeOutboxIsTuru.UblImzala);

        await service.IsleAsync(claim);

        Assert.Equal(42, politika.LastKurumId);
        Assert.Equal(7, politika.LastEBelgeKaydiId);
        Assert.Equal(EBelgeOutboxIsTuru.UblImzala, politika.LastIsTuru);
    }

    private static (EBelgeOutboxMesajIslemeService Service, FakeTransitionService Transition, FakePolicy Policy, FakeHandler Handler) CreateSut(
        FakeHandler handler,
        FakePolicy? policy = null,
        FakeTransitionService? transition = null,
        bool completeResult = true,
        bool failResult = true)
    {
        policy ??= new FakePolicy(EBelgeOutboxRetryKarari.Retry(TimeSpan.FromMinutes(1)));
        transition ??= new FakeTransitionService(completeResult, failResult);
        var service = CreateService(new[] { handler }, policy, transition);
        return (service, transition, policy, handler);
    }

    private static EBelgeOutboxMesajIslemeService CreateService(
        IEnumerable<IEBelgeOutboxIsTuruHandler> handlers,
        IEBelgeOutboxRetryPolicy? policy = null,
        IEBelgeOutboxLeaseTransitionService? transition = null,
        IEBelgeKurumPolitikaServisi? kurumPolitikaServisi = null)
        => new(
            handlers,
            policy ?? new EBelgeOutboxRetryPolicy(),
            transition ?? new FakeTransitionService(),
            kurumPolitikaServisi ?? new FakePolitikaServisi(),
            NullLogger<EBelgeOutboxMesajIslemeService>.Instance);

    private static EBelgeOutboxClaimLeaseResultDto CreateClaim(
        int outboxMesajiId = 1,
        int kurumId = 2,
        int eBelgeKaydiId = 3,
        EBelgeOutboxIsTuru isTuru = EBelgeOutboxIsTuru.ArtefaktOlustur,
        int denemeSayisi = 1,
        string? kilitToken = null,
        EBelgeOutboxDurumu durum = EBelgeOutboxDurumu.Isleniyor)
        => new()
        {
            OutboxMesajiId = outboxMesajiId,
            KurumId = kurumId,
            EBelgeKaydiId = eBelgeKaydiId,
            IsTuru = isTuru,
            Durum = durum,
            DenemeSayisi = denemeSayisi,
            KilitToken = kilitToken ?? TestToken.ToString("D"),
            KilitBitisZamaniUtc = DateTime.UtcNow.AddMinutes(5),
            IslemBaslamaZamaniUtc = DateTime.UtcNow,
            SonrakiDenemeZamaniUtc = null
        };

    private sealed class FakeHandler : IEBelgeOutboxIsTuruHandler
    {
        private readonly Func<EBelgeOutboxIslemBaglami, Task<EBelgeOutboxHandlerSonucu>> _handler;

        public FakeHandler(EBelgeOutboxIsTuru isTuru, Func<EBelgeOutboxIslemBaglami, EBelgeOutboxHandlerSonucu> handler)
        {
            IsTuru = isTuru;
            _handler = baglam => Task.FromResult(handler(baglam));
        }

        public FakeHandler(EBelgeOutboxIsTuru isTuru, Func<EBelgeOutboxIslemBaglami, CancellationToken, Task<EBelgeOutboxHandlerSonucu>> handler)
        {
            IsTuru = isTuru;
            _handler = baglam => handler(baglam, CancellationToken.None);
            AsyncHandler = handler;
        }

        public EBelgeOutboxIsTuru IsTuru { get; }

        public int CallCount { get; private set; }

        public EBelgeOutboxIslemBaglami? LastBaglam { get; private set; }

        private Func<EBelgeOutboxIslemBaglami, CancellationToken, Task<EBelgeOutboxHandlerSonucu>>? AsyncHandler { get; }

        public Task<EBelgeOutboxHandlerSonucu> HandleAsync(EBelgeOutboxIslemBaglami baglam, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastBaglam = baglam;

            if (AsyncHandler is not null)
            {
                return AsyncHandler(baglam, cancellationToken);
            }

            return _handler(baglam);
        }
    }

    private sealed class FakePolicy : IEBelgeOutboxRetryPolicy
    {
        private readonly EBelgeOutboxRetryKarari _result;
        private readonly Exception? _exception;

        public FakePolicy(EBelgeOutboxRetryKarari result, Exception? exception = null)
        {
            _result = result;
            _exception = exception;
        }

        public int CallCount { get; private set; }

        public int LastDenemeSayisi { get; private set; }

        public EBelgeOutboxHataSinifi LastHataSinifi { get; private set; }

        public EBelgeOutboxRetryKarari Hesapla(int denemeSayisi, EBelgeOutboxHataSinifi hataSinifi)
        {
            CallCount++;
            LastDenemeSayisi = denemeSayisi;
            LastHataSinifi = hataSinifi;
            if (_exception is not null)
            {
                throw _exception;
            }
            return _result;
        }
    }

    /// <summary>Faz 2B.10 - saf in-memory fake (gerçek DbContext GEREKTİRMEZ) - varsayılan olarak HER işlemi izinli sayar; `IzinliMi=false` ile "claim sonrası politika bloklu" senaryosu simüle edilir.</summary>
    private sealed class FakePolitikaServisi : IEBelgeKurumPolitikaServisi
    {
        public bool IzinliMi { get; set; } = true;

        public int IslemHalaIzinliMiCagriSayisi { get; private set; }

        public int? LastKurumId { get; private set; }

        public int? LastEBelgeKaydiId { get; private set; }

        public EBelgeOutboxIsTuru? LastIsTuru { get; private set; }

        public Task<EBelgeKurumPolitikaKarari> DegerlendirAsync(int kurumId, DateTime belgeTarihi, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Bu fake yalnız IslemHalaIzinliMiAsync'i destekler.");

        public Task<bool> IslemHalaIzinliMiAsync(int kurumId, int eBelgeKaydiId, EBelgeOutboxIsTuru isTuru, CancellationToken cancellationToken = default)
        {
            IslemHalaIzinliMiCagriSayisi++;
            LastKurumId = kurumId;
            LastEBelgeKaydiId = eBelgeKaydiId;
            LastIsTuru = isTuru;
            return Task.FromResult(IzinliMi);
        }
    }

    private sealed class FakeTransitionService : IEBelgeOutboxLeaseTransitionService
    {
        private readonly bool _completeResult;
        private readonly bool _failResult;
        private readonly Exception? _completeException;
        private readonly Exception? _failException;

        public FakeTransitionService(
            bool completeResult = true,
            bool failResult = true,
            Exception? completeException = null,
            Exception? failException = null)
        {
            _completeResult = completeResult;
            _failResult = failResult;
            _completeException = completeException;
            _failException = failException;
        }

        public int CompleteCallCount { get; private set; }

        public int FailCallCount { get; private set; }

        public int RenewCallCount { get; private set; }

        public int? LastCompleteOutboxMesajiId { get; private set; }

        public int? LastCompleteKurumId { get; private set; }

        public string? LastCompleteKilitToken { get; private set; }

        public int? LastFailOutboxMesajiId { get; private set; }

        public int? LastFailKurumId { get; private set; }

        public string? LastFailKilitToken { get; private set; }

        public string? LastFailHataKodu { get; private set; }

        public string? LastFailHataMesaji { get; private set; }

        public TimeSpan? LastFailRetryDelay { get; private set; }

        public Task<bool> TryCompleteAsync(int outboxMesajiId, int kurumId, string kilitToken, CancellationToken cancellationToken = default)
        {
            CompleteCallCount++;
            LastCompleteOutboxMesajiId = outboxMesajiId;
            LastCompleteKurumId = kurumId;
            LastCompleteKilitToken = kilitToken;
            if (_completeException is not null)
            {
                throw _completeException;
            }
            return Task.FromResult(_completeResult);
        }

        public Task<bool> TryFailAsync(int outboxMesajiId, int kurumId, string kilitToken, string sonHataKodu, string sonHataMesaji, TimeSpan? retryDelay, CancellationToken cancellationToken = default)
        {
            FailCallCount++;
            LastFailOutboxMesajiId = outboxMesajiId;
            LastFailKurumId = kurumId;
            LastFailKilitToken = kilitToken;
            LastFailHataKodu = sonHataKodu;
            LastFailHataMesaji = sonHataMesaji;
            LastFailRetryDelay = retryDelay;
            if (_failException is not null)
            {
                throw _failException;
            }
            return Task.FromResult(_failResult);
        }

        public Task<bool> TryRenewAsync(int outboxMesajiId, int kurumId, string kilitToken, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
        {
            RenewCallCount++;
            return Task.FromResult(true);
        }

        public int OwnedCallCount { get; private set; }

        public Task<bool> IsOwnedAsync(int outboxMesajiId, int kurumId, string kilitToken, CancellationToken cancellationToken = default)
        {
            OwnedCallCount++;
            return Task.FromResult(true);
        }

        public Task<bool> IsOwnedForJobAsync(int outboxMesajiId, int kurumId, int eBelgeKaydiId, EBelgeOutboxIsTuru expectedIsTuru, string kilitToken, CancellationToken cancellationToken = default)
        {
            OwnedCallCount++;
            return Task.FromResult(true);
        }

        public Task<bool> TryCompleteJobAsync(int outboxMesajiId, int kurumId, int eBelgeKaydiId, EBelgeOutboxIsTuru expectedIsTuru, string kilitToken, CancellationToken cancellationToken = default)
        {
            CompleteCallCount++;
            LastCompleteOutboxMesajiId = outboxMesajiId;
            LastCompleteKurumId = kurumId;
            LastCompleteKilitToken = kilitToken;
            if (_completeException is not null)
            {
                throw _completeException;
            }
            return Task.FromResult(_completeResult);
        }

        public Task<bool> TryFailJobAsync(int outboxMesajiId, int kurumId, int eBelgeKaydiId, EBelgeOutboxIsTuru expectedIsTuru, string kilitToken, string sonHataKodu, string sonHataMesaji, TimeSpan? retryDelay, CancellationToken cancellationToken = default)
        {
            FailCallCount++;
            LastFailOutboxMesajiId = outboxMesajiId;
            LastFailKurumId = kurumId;
            LastFailKilitToken = kilitToken;
            LastFailHataKodu = sonHataKodu;
            LastFailHataMesaji = sonHataMesaji;
            LastFailRetryDelay = retryDelay;
            if (_failException is not null)
            {
                throw _failException;
            }
            return Task.FromResult(_failResult);
        }
    }
}
