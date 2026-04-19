using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.KasaBankaHesaplari.Dtos;
using STYS.Muhasebe.KasaBankaHesaplari.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.KasaBankaHesaplari.Controllers;

[Route("api/muhasebe/kasa-banka-hesaplari")]
[ApiController]
public class KasaBankaHesaplariController : UIController
{
    private readonly IKasaBankaHesapService _service;
    private readonly IMapper _mapper;

    public KasaBankaHesaplariController(IKasaBankaHesapService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.View)]
    public async Task<ActionResult<List<KasaBankaHesapDto>>> GetList(CancellationToken cancellationToken)
        => Ok((await _service.GetAllAsync()).OrderBy(x => x.Tip).ThenBy(x => x.Kod).ThenBy(x => x.Id).ToList());

    [HttpGet("paged")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.View)]
    public async Task<ActionResult<PagedResult<KasaBankaHesapDto>>> GetPaged([FromQuery] PagedRequest request, CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(request, orderBy: q => q.OrderBy(x => x.Tip).ThenBy(x => x.Kod).ThenBy(x => x.Id)));

    [HttpGet("tip/{tip}")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.View)]
    public async Task<ActionResult<List<KasaBankaHesapDto>>> GetByTip(string tip, [FromQuery] bool sadeceAktif = true, CancellationToken cancellationToken = default)
        => Ok(await _service.GetByTipAsync(tip, sadeceAktif, cancellationToken));

    [HttpGet("muhasebe-hesap-secimleri/{tip}")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.View)]
    public async Task<ActionResult<List<MuhasebeHesapSecimDto>>> GetMuhasebeHesapSecimleri(string tip, CancellationToken cancellationToken = default)
        => Ok(await _service.GetMuhasebeHesapSecimleriAsync(tip, cancellationToken));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.View)]
    public async Task<ActionResult<KasaBankaHesapDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.Manage)]
    public async Task<ActionResult<KasaBankaHesapDto>> Create([FromBody] CreateKasaBankaHesapRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddAsync(_mapper.Map<KasaBankaHesapDto>(request)));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.Manage)]
    public async Task<ActionResult<KasaBankaHesapDto>> Update(int id, [FromBody] UpdateKasaBankaHesapRequest request, CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<KasaBankaHesapDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }
}
