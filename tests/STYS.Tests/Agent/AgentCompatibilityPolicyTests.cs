using System.Reflection;
using System.Text.Json;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Contracts.Versioning;
using STYS.Agent.Options;
using STYS.Agent.Services;
using STYS.Agent.Versioning;
using Xunit;

namespace STYS.Tests.Agent;

public sealed class AgentCompatibilityPolicyTests
{
    [Theory]
    [InlineData("1.2.0", "1.0.0", "1.0.0", "1.2.0", "1.0.0", AgentCompatibilityStatus.Supported)]
    [InlineData("2.0.0", "1.0.0", "1.0.0", "1.2.0", "1.0.0", AgentCompatibilityStatus.Supported)]
    [InlineData("1.5.0", "1.0.0", "1.0.0", "2.0.0", "1.0.0", AgentCompatibilityStatus.UpdateAvailable)]
    [InlineData("0.9.9", "1.0.0", "1.0.0", "2.0.0", "1.0.0", AgentCompatibilityStatus.UpdateRequired)]
    [InlineData("1.2.0", "2.0.0", "1.0.0", "2.0.0", "1.0.0", AgentCompatibilityStatus.IncompatibleContract)]
    [InlineData("1.0.0-rc.1", "1.0.0", "1.0.0", "1.0.0", "1.0.0", AgentCompatibilityStatus.UpdateRequired)]
    [InlineData("1.0.0-rc.1", "1.0.0", "0.9.0", "1.0.0", "1.0.0", AgentCompatibilityStatus.UpdateAvailable)]
    [InlineData("1.0.0+build.5", "1.0.0+build.7", "1.0.0", "1.0.0", "1.0.0", AgentCompatibilityStatus.Supported)]
    [InlineData("v1.2.3", "v1.0.0", "1.0.0", "1.2.0", "1.0.0", AgentCompatibilityStatus.Supported)]
    public void VersionPolicy_ShouldClassifyExpectedStatus(
        string agentVersion,
        string contractVersion,
        string minimumSupported,
        string recommended,
        string supportedContract,
        AgentCompatibilityStatus expected)
    {
        var evaluation = Evaluate(agentVersion, contractVersion, minimumSupported, recommended, supportedContract);

        Assert.Equal(expected, evaluation.CompatibilityStatus);
    }

    [Theory]
    [InlineData(null, "1.0.0")]
    [InlineData("", "1.0.0")]
    [InlineData("1.0.0", null)]
    [InlineData("1.0.0", "")]
    [InlineData("bad.version", "1.0.0")]
    [InlineData("1..2", "1.0.0")]
    [InlineData("1.0.0+build..5", "1.0.0")]
    [InlineData("1.0.0+", "1.0.0")]
    public void InvalidOrMissingVersion_ShouldReturnUnknown(string? agentVersion, string? contractVersion)
    {
        var evaluation = Evaluate(agentVersion, contractVersion);

        Assert.Equal(AgentCompatibilityStatus.Unknown, evaluation.CompatibilityStatus);
    }

    [Fact]
    public void HeartbeatCompatibilityResponse_ShouldNotLeakSecrets()
    {
        var response = new AgentHeartbeatResponse
        {
            MinimumSupportedAgentVersion = "1.0.0",
            RecommendedAgentVersion = "1.2.0",
            SupportedContractVersion = "1.0.0",
            LatestAgentVersion = "1.2.0",
            RequiredContractVersion = "1.0.0",
            CompatibilityStatus = AgentCompatibilityStatus.UpdateAvailable,
            RequiredUpdate = false
        };

        var json = JsonSerializer.Serialize(response);

        Assert.False(json.Contains("secret", StringComparison.OrdinalIgnoreCase));
        Assert.False(json.Contains("fingerprint", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PaymentGuard_ShouldAllowOnlySupportedOrUpdateAvailable()
    {
        Assert.True(AgentCompatibilityEvaluator.CanStartPayment(AgentCompatibilityStatus.Supported));
        Assert.True(AgentCompatibilityEvaluator.CanStartPayment(AgentCompatibilityStatus.UpdateAvailable));
        Assert.False(AgentCompatibilityEvaluator.CanStartPayment(AgentCompatibilityStatus.UpdateRequired));
        Assert.False(AgentCompatibilityEvaluator.CanStartPayment(AgentCompatibilityStatus.Unknown));
        Assert.False(AgentCompatibilityEvaluator.CanStartPayment(AgentCompatibilityStatus.IncompatibleContract));
    }

    [Fact]
    public void ContractVersion_ShouldComeFromAuthoritativeConstant()
    {
        Assert.Equal("1.0.0", AgentContractVersion.Current);
    }

    [Fact]
    public void AgentVersionInfo_ShouldReadAssemblyInformationalVersion()
    {
        var assembly = typeof(AgentVersionInfo).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var expected = !string.IsNullOrWhiteSpace(informationalVersion)
            ? informationalVersion.Trim()
            : AgentVersionInfo.ResolveFromAssemblyVersion(assembly.GetName().Version);

        Assert.Equal(expected, AgentVersionInfo.Current);
    }

    [Fact]
    public void AgentVersionInfo_ShouldNormalizeAssemblyVersion()
    {
        Assert.Equal("1.2.3", AgentVersionInfo.ResolveFromAssemblyVersion(new Version(1, 2, 3, 0)));
        Assert.Equal("1.2.3", AgentVersionInfo.ResolveFromAssemblyVersion(new Version(1, 2, 3)));
    }

    private static AgentCompatibilityEvaluation Evaluate(
        string? agentVersion,
        string? contractVersion,
        string minimumSupported = "1.0.0",
        string recommended = "1.2.0",
        string supportedContract = "1.0.0")
    {
        return AgentCompatibilityEvaluator.Evaluate(
            agentVersion,
            contractVersion,
            new AgentCompatibilityOptions
            {
                MinimumSupportedAgentVersion = minimumSupported,
                RecommendedAgentVersion = recommended,
                SupportedContractVersion = supportedContract
            });
    }
}
