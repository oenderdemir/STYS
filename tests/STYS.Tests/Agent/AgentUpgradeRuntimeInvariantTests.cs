using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using STYS.Agent.Contracts.Versioning;
using STYS.Agent.Controllers;
using Xunit;

namespace STYS.Tests.Agent;

/// <summary>
/// Runtime invariants for remote upgrade that only bite in production: version identity across
/// build metadata, and the upload limits that gate a release publish.
/// </summary>
public sealed class AgentUpgradeRuntimeInvariantTests
{
    // ---------------------------------------------------------------- A/B/C. health probe identity

    [Theory]
    // A: exact match
    [InlineData("1.0.1", "1.0.1", true)]
    // B: SDK-appended source revision must not look like a different build
    [InlineData("1.0.1", "1.0.1+abcdef", true)]
    [InlineData("1.0.1+abcdef", "1.0.1", true)]
    [InlineData("1.0.1+abcdef", "1.0.1+999999", true)]
    // C: a genuinely different version must still fail
    [InlineData("1.0.1", "1.0.2", false)]
    [InlineData("1.0.1", "2.0.1", false)]
    [InlineData("1.0.1", "1.1.1", false)]
    // Prerelease identity is preserved, not collapsed
    [InlineData("1.0.1-beta.1", "1.0.1-beta.1", true)]
    [InlineData("1.0.1-beta.1", "1.0.1-beta.1+sha", true)]
    [InlineData("1.0.1-beta.1", "1.0.1-beta.2", false)]
    [InlineData("1.0.1-beta.1", "1.0.1", false)]
    [InlineData("1.0.1", "1.0.1-beta.1", false)]
    public void SurumKimligi_YalnizBuildMetadataYokSayar(string target, string reported, bool expected)
    {
        Assert.Equal(expected, AgentVersionComparison.SameRelease(reported, target));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BildirilmeyenSurum_EslesmisSayilmaz(string? reported)
    {
        // An agent that fails to report a version must not be accepted as a successful upgrade.
        Assert.False(AgentVersionComparison.SameRelease(reported, "1.0.1"));
    }

    [Theory]
    [InlineData("1.0.1+abcdef", "1.0.1")]
    [InlineData("1.0.1-beta.1+abcdef", "1.0.1-beta.1")]
    [InlineData("1.0.1", "1.0.1")]
    [InlineData("  1.0.1+sha  ", "1.0.1")]
    public void BuildMetadataAyiklanir_PrereleaseKorunur(string input, string expected)
    {
        Assert.Equal(expected, AgentVersionComparison.StripBuildMetadata(input));
    }

    // ---------------------------------------------------------------- G. upload limits

    [Fact]
    public void PublishActionı_MultipartVarsayilaniniAsacakSekildeYapilandirilmis()
    {
        var publish = typeof(AgentReleasesController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(x => x.Name == nameof(AgentReleasesController.Publish));

        var sizeLimit = publish.GetCustomAttribute<RequestSizeLimitAttribute>();
        var formLimits = publish.GetCustomAttribute<RequestFormLimitsAttribute>();

        Assert.NotNull(sizeLimit);
        Assert.NotNull(formLimits);

        // The framework default is 128 MB and applies during multipart binding, i.e. before the
        // service can enforce MaxPackageSizeBytes. Without this the raw-body limit alone is moot.
        const long defaultMultipartLimit = 134_217_728;
        Assert.True(
            formLimits!.MultipartBodyLengthLimit > defaultMultipartLimit,
            $"MultipartBodyLengthLimit ({formLimits.MultipartBodyLengthLimit}) 128 MB varsayilanindan buyuk olmali.");

        Assert.Equal(AgentReleasesController.MaxUploadBytes, formLimits.MultipartBodyLengthLimit);
    }

    [Fact]
    public void UploadTavani_ServisVarsayilaniniKarsilar()
    {
        // The transport ceiling must not sit below the configured service maximum, or uploads would
        // be rejected by the HTTP layer before the authoritative check ran.
        var serviceDefault = new STYS.Agent.Options.AgentReleasePublishingOptions().MaxPackageSizeBytes;
        Assert.True(
            AgentReleasesController.MaxUploadBytes >= serviceDefault,
            $"Transport tavani ({AgentReleasesController.MaxUploadBytes}) servis varsayilanindan ({serviceDefault}) kucuk olmamali.");
    }
}
