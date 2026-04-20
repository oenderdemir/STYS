using Microsoft.AspNetCore.Mvc;
using STYS.Tesisler.Dto;
using STYS.Tesisler.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Identity;
using TOD.Platform.Identity.Users.DTO;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Tesisler.Controllers;

public class TesisController : UIController
{
    private readonly ITesisService _tesisService;

    public TesisController(ITesisService tesisService)
    {
        _tesisService = tesisService;
    }

    [HttpGet]
    [Permission(StructurePermissions.TesisYonetimi.View)]
    public async Task<List<TesisDto>> GetAll()
    {
        var tesisler = await _tesisService.GetAllAsync();
        return tesisler.OrderBy(x => x.Ad).ToList();
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.TesisYonetimi.View)]
    public async Task<ActionResult<PagedResult<TesisDto>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery(Name = "q")] string? query,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir = "asc")
    {
        var orderBy = BuildOrderBy(sortBy, sortDir);
        if (orderBy is null && !string.IsNullOrWhiteSpace(sortBy))
        {
            return BadRequest("Desteklenmeyen siralama kolonu. Desteklenen alanlar: ad, ilId, telefon, adres, aktifMi, id, createdAt.");
        }

        var normalizedQuery = query?.Trim();
        var result = await _tesisService.GetPagedAsync(
            request,
            predicate: string.IsNullOrWhiteSpace(normalizedQuery)
                ? null
                : x => x.Ad.Contains(normalizedQuery)
                    || x.Telefon.Contains(normalizedQuery)
                    || x.Adres.Contains(normalizedQuery)
                    || (x.Eposta != null && x.Eposta.Contains(normalizedQuery)),
            orderBy: orderBy ?? (q => q.OrderBy(x => x.Ad).ThenBy(x => x.Id)));
        return Ok(result);
    }

    [HttpGet("by-il/{ilId:int}")]
    [Permission(StructurePermissions.TesisYonetimi.View)]
    public async Task<List<TesisDto>> GetByIl(int ilId)
    {
        if (ilId <= 0)
        {
            return [];
        }

        var tesisler = await _tesisService.WhereAsync(x => x.IlId == ilId);
        return tesisler.OrderBy(x => x.Ad).ToList();
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.TesisYonetimi.View)]
    public async Task<ActionResult<TesisDto>> GetById(int id)
    {
        var tesis = await _tesisService.GetByIdAsync(id);
        if (tesis is null)
        {
            return NotFound();
        }

        return Ok(tesis);
    }

    [HttpPost]
    [Permission(StructurePermissions.TesisYonetimi.Manage)]
    public async Task<ActionResult<TesisDto>> Create([FromBody] TesisDto dto)
    {
        var created = await _tesisService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.TesisYonetimi.Manage)]
    public async Task<ActionResult<TesisDto>> Update(int id, [FromBody] TesisDto dto)
    {
        dto.Id = id;
        var updated = await _tesisService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.TesisYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _tesisService.DeleteAsync(id);
        return Ok();
    }

    [HttpPost("{tesisId:int}/resepsiyonist-kullanici")]
    [Permission(IdentityPermissions.UserManagement.Manage)]
    public async Task<ActionResult<UserDto>> CreateResepsiyonistUser(int tesisId, [FromBody] UserDto dto)
    {
        var created = await _tesisService.CreateResepsiyonistUserAsync(tesisId, dto);
        return Ok(created);
    }

    [HttpPost("{tesisId:int}/bina-yonetici-kullanici")]
    [Permission(IdentityPermissions.UserManagement.Manage)]
    public async Task<ActionResult<UserDto>> CreateBinaYoneticisiUser(int tesisId, [FromBody] UserDto dto)
    {
        var created = await _tesisService.CreateBinaYoneticisiUserAsync(tesisId, dto);
        return Ok(created);
    }

    [HttpPost("{tesisId:int}/restoran-yonetici-kullanici")]
    [Permission(IdentityPermissions.UserManagement.Manage)]
    public async Task<ActionResult<UserDto>> CreateRestoranYoneticisiUser(int tesisId, [FromBody] UserDto dto)
    {
        var created = await _tesisService.CreateRestoranYoneticisiUserAsync(tesisId, dto);
        return Ok(created);
    }

    [HttpPost("{tesisId:int}/garson-kullanici")]
    [Permission(IdentityPermissions.UserManagement.Manage)]
    public async Task<ActionResult<UserDto>> CreateRestoranGarsonuUser(int tesisId, [FromBody] UserDto dto)
    {
        var created = await _tesisService.CreateRestoranGarsonuUserAsync(tesisId, dto);
        return Ok(created);
    }

    [HttpPost("{tesisId:int}/muhasebeci-kullanici")]
    [Permission(IdentityPermissions.UserManagement.Manage)]
    public async Task<ActionResult<UserDto>> CreateMuhasebeciUser(int tesisId, [FromBody] UserDto dto)
    {
        var created = await _tesisService.CreateMuhasebeciUserAsync(tesisId, dto);
        return Ok(created);
    }

    private static Func<IQueryable<STYS.Tesisler.Entities.Tesis>, IOrderedQueryable<STYS.Tesisler.Entities.Tesis>>? BuildOrderBy(string? sortBy, string? sortDir)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return null;
        }

        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        var normalized = sortBy.Trim().ToLowerInvariant();
        return normalized switch
        {
            "ad" => desc ? q => q.OrderByDescending(x => x.Ad).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.Ad).ThenBy(x => x.Id),
            "ilid" => desc ? q => q.OrderByDescending(x => x.IlId).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.IlId).ThenBy(x => x.Id),
            "telefon" => desc ? q => q.OrderByDescending(x => x.Telefon).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.Telefon).ThenBy(x => x.Id),
            "adres" => desc ? q => q.OrderByDescending(x => x.Adres).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.Adres).ThenBy(x => x.Id),
            "aktifmi" => desc ? q => q.OrderByDescending(x => x.AktifMi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.AktifMi).ThenBy(x => x.Id),
            "id" => desc ? q => q.OrderByDescending(x => x.Id) : q => q.OrderBy(x => x.Id),
            "createdat" => desc ? q => q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            _ => null
        };
    }
}
