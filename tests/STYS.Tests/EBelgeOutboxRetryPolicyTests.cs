using STYS.Muhasebe.SatisBelgeleri.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class EBelgeOutboxRetryPolicyTests
{
    private readonly IEBelgeOutboxRetryPolicy _policy = new EBelgeOutboxRetryPolicy();

    public static IEnumerable<object[]> GeciciHataCizelgesiBeklentileri() =>
    [
        [1, true, 60],
        [2, true, 300],
        [3, true, 900],
        [4, true, 3_600],
        [5, true, 21_600],
        [6, false, null]
    ];

    public static IEnumerable<object[]> GeciciHataSinirUstuBeklentileri() =>
    [
        [7],
        [int.MaxValue]
    ];

    public static IEnumerable<object[]> KaliciHataBeklentileri() =>
    [
        [1],
        [int.MaxValue]
    ];

    public static IEnumerable<object[]> GecersizDenemeSayilari() =>
    [
        [0],
        [-1]
    ];

    [Theory]
    [MemberData(nameof(GeciciHataCizelgesiBeklentileri))]
    public void GeciciHataIcinDeterministikCizelgeUygulanir(int denemeSayisi, bool yenidenDenenecekMi, int? beklenenGecikmeSaniyesi)
    {
        var karar = _policy.Hesapla(denemeSayisi, EBelgeOutboxHataSinifi.Gecici);

        Assert.Equal(yenidenDenenecekMi, karar.YenidenDenenecekMi);
        if (beklenenGecikmeSaniyesi is null)
        {
            Assert.Null(karar.RetryGecikmesi);
            return;
        }

        Assert.NotNull(karar.RetryGecikmesi);
        Assert.Equal(TimeSpan.FromSeconds(beklenenGecikmeSaniyesi.Value), karar.RetryGecikmesi);
        Assert.True(karar.RetryGecikmesi > TimeSpan.Zero);
        Assert.Equal(0, karar.RetryGecikmesi.Value.Ticks % TimeSpan.TicksPerSecond);
        Assert.True(karar.RetryGecikmesi.Value <= TimeSpan.FromDays(30));
    }

    [Theory]
    [MemberData(nameof(GeciciHataSinirUstuBeklentileri))]
    public void GeciciHataIcinAltinciDenemedenSonraTerminalKararUretir(int denemeSayisi)
    {
        var karar = _policy.Hesapla(denemeSayisi, EBelgeOutboxHataSinifi.Gecici);

        Assert.False(karar.YenidenDenenecekMi);
        Assert.Null(karar.RetryGecikmesi);
    }

    [Theory]
    [MemberData(nameof(KaliciHataBeklentileri))]
    public void KaliciHataHerDenemedeTerminalKararUretir(int denemeSayisi)
    {
        var karar = _policy.Hesapla(denemeSayisi, EBelgeOutboxHataSinifi.Kalici);

        Assert.False(karar.YenidenDenenecekMi);
        Assert.Null(karar.RetryGecikmesi);
    }

    [Theory]
    [MemberData(nameof(GecersizDenemeSayilari))]
    public void DenemeSayisiSifirVeyaNegatifOlamaz(int denemeSayisi)
    {
        Assert.Throws<BaseException>(() => _policy.Hesapla(denemeSayisi, EBelgeOutboxHataSinifi.Gecici));
        Assert.Throws<BaseException>(() => _policy.Hesapla(denemeSayisi, EBelgeOutboxHataSinifi.Kalici));
    }

    [Fact]
    public void BilinmeyenHataSinifiKontrolluHataUretir()
    {
        var bilinmeyen = (EBelgeOutboxHataSinifi)int.MaxValue;

        Assert.Throws<BaseException>(() => _policy.Hesapla(1, bilinmeyen));
    }

    [Fact]
    public void AyniGirdilerAyniKarariUretir()
    {
        var ilk = _policy.Hesapla(3, EBelgeOutboxHataSinifi.Gecici);
        var ikinci = _policy.Hesapla(3, EBelgeOutboxHataSinifi.Gecici);

        Assert.Equal(ilk, ikinci);
    }

    [Theory]
    [InlineData(true, 60)]
    [InlineData(false, null)]
    public void FactoryInvariantiYenidenDenenecekMiVeGecikmeTutarlidir(bool retryBekleniyor, int? beklenenSaniye)
    {
        var karar = retryBekleniyor
            ? EBelgeOutboxRetryKarari.Retry(TimeSpan.FromSeconds(beklenenSaniye!.Value))
            : EBelgeOutboxRetryKarari.Terminal();

        Assert.Equal(retryBekleniyor, karar.YenidenDenenecekMi);
        if (retryBekleniyor)
        {
            Assert.NotNull(karar.RetryGecikmesi);
            Assert.Equal(TimeSpan.FromSeconds(beklenenSaniye!.Value), karar.RetryGecikmesi);
        }
        else
        {
            Assert.Null(karar.RetryGecikmesi);
        }
    }
}
