using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.Hesaplar.Dtos;
using STYS.Muhasebe.Hesaplar.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.Hesaplar.Controllers;

[Route("api/muhasebe/hesaplar")]
[ApiController]
public class HesaplarController : UIController
{
    private readonly IHesapService _service;
    private readonly IMapper _mapper;

    public HesaplarController(IHesapService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet]
    [Permission(StructurePermissions.HesapYonetimi.View)]
    public async Task<ActionResult<List<HesapDto>>> GetList(CancellationToken cancellationToken)
        => Ok(await _service.GetDetailedListAsync(cancellationToken));

    [HttpGet("paged")]
    [Permission(StructurePermissions.HesapYonetimi.View)]
    public async Task<ActionResult<PagedResult<HesapDto>>> GetPaged([FromQuery] PagedRequest request, CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(request, orderBy: q => q.OrderBy(x => x.Ad).ThenBy(x => x.Id)));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.HesapYonetimi.View)]
    public async Task<ActionResult<HesapDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetDetailByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("lookups/kasa-hesaplari")]
    [Permission(StructurePermissions.HesapYonetimi.View)]
    public async Task<ActionResult<List<HesapLookupDto>>> GetKasaHesaplari(CancellationToken cancellationToken)
        => Ok(await _service.GetKasaHesapLookupsAsync(cancellationToken));

    [HttpGet("lookups/banka-hesaplari")]
    [Permission(StructurePermissions.HesapYonetimi.View)]
    public async Task<ActionResult<List<HesapLookupDto>>> GetBankaHesaplari(CancellationToken cancellationToken)
        => Ok(await _service.GetBankaHesapLookupsAsync(cancellationToken));

    [HttpGet("lookups/depolar")]
    [Permission(StructurePermissions.HesapYonetimi.View)]
    public async Task<ActionResult<List<HesapLookupDto>>> GetDepolar(CancellationToken cancellationToken)
        => Ok(await _service.GetDepoLookupsAsync(cancellationToken));

    [HttpGet("lookups/muhasebe-kodlari")]
    [Permission(StructurePermissions.HesapYonetimi.View)]
    public async Task<ActionResult<List<HesapLookupDto>>> GetMuhasebeKodlari([FromQuery] string? startsWith, CancellationToken cancellationToken)
        => Ok(await _service.GetMuhasebeKodLookupsAsync(startsWith, cancellationToken));

    [HttpPost]
    [Permission(StructurePermissions.HesapYonetimi.Manage)]
    public async Task<ActionResult<HesapDto>> Create([FromBody] CreateHesapRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddAsync(_mapper.Map<HesapDto>(request)));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.HesapYonetimi.Manage)]
    public async Task<ActionResult<HesapDto>> Update(int id, [FromBody] UpdateHesapRequest request, CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<HesapDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.HesapYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }
}
