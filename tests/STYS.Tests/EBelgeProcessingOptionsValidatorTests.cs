using STYS.Muhasebe.SatisBelgeleri.Services;
using Xunit;

namespace STYS.Tests;

/// <summary>Faz 2B.8 görev md.10/md.18 - `EBelgeProcessingOptions` startup-time yapısal doğrulaması. Yalnız saf aritmetik kontroller (I/O YOK) - `Enabled` bayrağından BAĞIMSIZ çalışır.</summary>
public class EBelgeProcessingOptionsValidatorTests
{
    private static EBelgeProcessingOptions GecerliVarsayilanlar() => new()
    {
        Enabled = false,
        NotBeforeLocalDate = "2026-09-15",
        TimeZoneId = "Europe/Istanbul",
        PollIntervalSeconds = 10,
        IdlePollIntervalSeconds = 30,
        BatchSize = 10,
        LeaseDurationSeconds = 120,
        MaxParallelism = 1,
        ShutdownGracePeriodSeconds = 30,
    };

    private static readonly EBelgeProcessingOptionsValidator Validator = new();

    [Fact]
    public void VarsayilanConfigGecerlidir()
    {
        var sonuc = Validator.Validate(null, GecerliVarsayilanlar());
        Assert.True(sonuc.Succeeded);
    }

    [Fact]
    public void EnabledFalseIkenBileYapisalHatalarTespitEdilir()
    {
        // Faz 2B.8 görev md.10 - "geçersiz seçenekler uygulama başlangıcında tespit edilmeli";
        // bu, Enabled=false OLSA BİLE çalışır (dış bağımlılık İÇERMEDİĞİNDEN üretim başlangıcını
        // riske ATMAZ - bkz. EBelgeProcessingOptionsValidator XML doc'u).
        var options = GecerliVarsayilanlar();
        options.Enabled = false;
        options.PollIntervalSeconds = -5;

        var sonuc = Validator.Validate(null, options);

        Assert.False(sonuc.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GecersizPollIntervalReddedilir(int deger)
    {
        var options = GecerliVarsayilanlar();
        options.PollIntervalSeconds = deger;

        Assert.False(Validator.Validate(null, options).Succeeded);
    }

    [Fact]
    public void IdleIntervalPollIntervaldenKucukOlamaz()
    {
        var options = GecerliVarsayilanlar();
        options.PollIntervalSeconds = 20;
        options.IdlePollIntervalSeconds = 10;

        Assert.False(Validator.Validate(null, options).Succeeded);
    }

    [Fact]
    public void IdleIntervalPollIntervaleEsitOlabilir()
    {
        var options = GecerliVarsayilanlar();
        options.PollIntervalSeconds = 10;
        options.IdlePollIntervalSeconds = 10;

        Assert.True(Validator.Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(EBelgeProcessingOptions.MaxBatchSize + 1)]
    public void GecersizBatchSizeReddedilir(int deger)
    {
        var options = GecerliVarsayilanlar();
        options.BatchSize = deger;

        Assert.False(Validator.Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void GecersizLeaseDurationReddedilir(int deger)
    {
        var options = GecerliVarsayilanlar();
        options.LeaseDurationSeconds = deger;

        Assert.False(Validator.Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(EBelgeProcessingOptions.MaxParallelismLimit + 1)]
    public void GecersizMaxParallelismReddedilir(int deger)
    {
        var options = GecerliVarsayilanlar();
        options.MaxParallelism = deger;

        Assert.False(Validator.Validate(null, options).Succeeded);
    }

    [Fact]
    public void NegatifShutdownGracePeriodReddedilir()
    {
        var options = GecerliVarsayilanlar();
        options.ShutdownGracePeriodSeconds = -1;

        Assert.False(Validator.Validate(null, options).Succeeded);
    }

    [Fact]
    public void SifirShutdownGracePeriodGecerlidir()
    {
        var options = GecerliVarsayilanlar();
        options.ShutdownGracePeriodSeconds = 0;

        Assert.True(Validator.Validate(null, options).Succeeded);
    }
}
