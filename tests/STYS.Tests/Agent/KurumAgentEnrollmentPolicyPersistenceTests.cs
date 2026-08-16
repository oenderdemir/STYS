using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Kurumlar.Dto;
using STYS.Kurumlar.Entities;
using STYS.Kurumlar.Mapping;
using Xunit;

namespace STYS.Tests.Agent;

/// <summary>
/// The kurum agent-enrollment policy travels Create/UpdateKurumRequest → KurumDto → Kurum through
/// AutoMapper convention mapping. Because KurumDto declares `= true` as a fail-safe default, a
/// request type that is MISSING the property silently discards the caller's choice: AutoMapper
/// leaves the destination initializer in place. These tests pin the whole chain so that regression
/// cannot come back unnoticed.
/// </summary>
public sealed class KurumAgentEnrollmentPolicyPersistenceTests
{
    private static IMapper CreateMapper() =>
        new MapperConfiguration(cfg => cfg.AddProfile<KurumProfile>(), NullLoggerFactory.Instance)
            .CreateMapper();

    private static CreateKurumRequest NewCreateRequest(bool requiresApproval) => new()
    {
        Kod = "K-001",
        Ad = "Test Kurum",
        AktifMi = true,
        AgentEnrollmentRequiresApproval = requiresApproval
    };

    private static UpdateKurumRequest NewUpdateRequest(bool requiresApproval, string ad = "Test Kurum") => new()
    {
        Kod = "K-001",
        Ad = ad,
        AktifMi = true,
        AgentEnrollmentRequiresApproval = requiresApproval
    };

    // ---------------------------------------------------------------- A. create

    [Fact]
    public void Create_PolicyFalse_DtoVeEntityyeFalseOlarakTasinir()
    {
        var mapper = CreateMapper();

        var dto = mapper.Map<KurumDto>(NewCreateRequest(requiresApproval: false));
        var entity = mapper.Map<Kurum>(dto);

        // The whole point: an explicit false must survive, not be replaced by KurumDto's `= true`.
        Assert.False(dto.AgentEnrollmentRequiresApproval);
        Assert.False(entity.AgentEnrollmentRequiresApproval);
    }

    [Fact]
    public void Create_PolicyBelirtilmezse_FailSafeTrueKalir()
    {
        var mapper = CreateMapper();

        // Property omitted from the request object entirely (JSON without the field binds to the
        // request default), so the fail-safe wins.
        var request = new CreateKurumRequest { Kod = "K-002", Ad = "Varsayilan Kurum" };
        var dto = mapper.Map<KurumDto>(request);
        var entity = mapper.Map<Kurum>(dto);

        Assert.True(dto.AgentEnrollmentRequiresApproval);
        Assert.True(entity.AgentEnrollmentRequiresApproval);
    }

    // ---------------------------------------------------------------- B. update true -> false

    [Fact]
    public void Update_PolicyTruedanFalsaCevrilebilir()
    {
        var mapper = CreateMapper();

        var dto = mapper.Map<KurumDto>(NewUpdateRequest(requiresApproval: false));
        var entity = mapper.Map<Kurum>(dto);

        Assert.False(dto.AgentEnrollmentRequiresApproval);
        Assert.False(entity.AgentEnrollmentRequiresApproval);
    }

    // ---------------------------------------------------------------- C. unrelated update

    [Fact]
    public void Update_IlgisizAlanDegisirken_FalsePolicyKorunur()
    {
        var mapper = CreateMapper();

        // Simulates the reported regression: a kurum already stored with policy=false is edited to
        // change only its name. KurumService builds a FRESH KurumDto from the request, so if the
        // request did not carry the policy the stored false would be overwritten with true.
        var stored = new Kurum
        {
            Id = 7,
            Kod = "K-001",
            Ad = "Eski Ad",
            AktifMi = true,
            AgentEnrollmentRequiresApproval = false
        };

        var request = NewUpdateRequest(requiresApproval: stored.AgentEnrollmentRequiresApproval, ad: "Yeni Ad");
        var dto = mapper.Map<KurumDto>(request);
        dto.Id = stored.Id;
        var updated = mapper.Map<Kurum>(dto);

        Assert.Equal("Yeni Ad", updated.Ad);
        Assert.False(updated.AgentEnrollmentRequiresApproval);
    }

    // ---------------------------------------------------------------- D. round-trip

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Entity_DtoRoundTrip_PolicyDegerinikorur(bool requiresApproval)
    {
        var mapper = CreateMapper();

        // The enrollment-policy endpoint reads the kurum and projects it, so the reverse direction
        // must be faithful too or the UI would be told the wrong policy.
        var entity = new Kurum
        {
            Id = 1,
            Kod = "K-003",
            Ad = "Round Trip",
            AktifMi = true,
            AgentEnrollmentRequiresApproval = requiresApproval
        };

        var dto = mapper.Map<KurumDto>(entity);
        var back = mapper.Map<Kurum>(dto);

        Assert.Equal(requiresApproval, dto.AgentEnrollmentRequiresApproval);
        Assert.Equal(requiresApproval, back.AgentEnrollmentRequiresApproval);
    }
}
