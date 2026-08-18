using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Entegrasyonlar.Pos.Services;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// StartAsync returns early, without queuing a PavoStartPayment command, whenever the payment is
/// already finished or already handed to the agent. A newly created payment starts at Pending, so
/// if that state is classified as dispatched the command is never sent at all — the payment sits at
/// Pending with no AgentCommandId while every other agent command keeps working.
/// </summary>
public sealed class PosPaymentDispatchStateTests
{
    [Fact]
    public void YeniOlusturulanOdemeDurumu_GonderilmisSayilmaz()
    {
        // This is the exact regression: Pending is the state assigned at creation.
        Assert.False(PosPaymentTestService.IsAlreadyDispatchedState(PosOdemeDurumlari.Pending));
        Assert.False(PosPaymentTestService.IsFinalPaymentState(PosOdemeDurumlari.Pending));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(PosOdemeDurumlari.Olusturuldu)]
    [InlineData(PosOdemeDurumlari.PosIslemiBekleniyor)]
    public void HenuzGonderilmemisDurumlar_KomutGonderiminiEngellemez(string? durum)
    {
        Assert.False(PosPaymentTestService.IsAlreadyDispatchedState(durum));
        Assert.False(PosPaymentTestService.IsFinalPaymentState(durum));
    }

    [Theory]
    [InlineData(PosOdemeDurumlari.SentToAgent)]
    [InlineData(PosOdemeDurumlari.Processing)]
    public void AjanaGonderilmisDurumlar_TekrarGonderilmez(string durum)
    {
        // Guarding these is the point of the check: re-sending would duplicate the payment.
        Assert.True(PosPaymentTestService.IsAlreadyDispatchedState(durum));
    }

    [Theory]
    [InlineData(PosOdemeDurumlari.Successful)]
    [InlineData(PosOdemeDurumlari.Failed)]
    [InlineData(PosOdemeDurumlari.Cancelled)]
    public void SonlanmisDurumlar_TekrarGonderilmez(string durum)
    {
        Assert.True(PosPaymentTestService.IsFinalPaymentState(durum));
    }

    [Fact]
    public void DurumKarsilastirmasi_BuyukKucukHarfDuyarsiz()
    {
        Assert.True(PosPaymentTestService.IsAlreadyDispatchedState("senttoagent"));
        Assert.True(PosPaymentTestService.IsFinalPaymentState("SUCCESSFUL"));
        Assert.False(PosPaymentTestService.IsAlreadyDispatchedState("PENDING"));
    }
}
