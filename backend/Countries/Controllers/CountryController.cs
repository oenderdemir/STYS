using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using STYS.Countries.Dto;
using STYS.Countries.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Countries.Controllers;

public class CountryController : UIController
{
    private readonly ICountryService _countryService;

    public CountryController(ICountryService countryService)
    {
        _countryService = countryService;
    }

    [HttpGet]
    [Permission(CountryPermissions.View)]
    public async Task<List<CountryDto>> GetAll()
    {
        var countries = await _countryService.GetAllAsync();
        return countries.ToList();
    }

    [HttpGet("paged")]
    [Permission(CountryPermissions.View)]
    public async Task<ActionResult<PagedResult<CountryDto>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery(Name = "q")] string? query,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir = "asc")
    {
        var orderBy = BuildOrderBy(sortBy, sortDir);
        if (orderBy is null && !string.IsNullOrWhiteSpace(sortBy))
        {
            return BadRequest("Desteklenmeyen siralama kolonu. Desteklenen alanlar: code, name, id, createdAt.");
        }

        var normalizedQuery = query?.Trim();
        var result = await _countryService.GetPagedAsync(
            request,
            predicate: string.IsNullOrWhiteSpace(normalizedQuery)
                ? null
                : x => x.Name.Contains(normalizedQuery) || x.Code.Contains(normalizedQuery),
            orderBy: orderBy ?? (q => q.OrderBy(x => x.Name).ThenBy(x => x.Id)));
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Permission(CountryPermissions.View)]
    public async Task<ActionResult<CountryDto>> GetById(Guid id)
    {
        var country = await _countryService.GetByIdAsync(id);
        if (country is null)
        {
            return NotFound();
        }

        return Ok(country);
    }

    [HttpPost]
    [Permission(CountryPermissions.Manage)]
    public async Task<ActionResult<CountryDto>> Create([FromBody] CountryDto dto)
    {
        var created = await _countryService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Permission(CountryPermissions.Manage)]
    public async Task<ActionResult<CountryDto>> Update(Guid id, [FromBody] CountryDto dto)
    {
        dto.Id = id;
        var updated = await _countryService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [Permission(CountryPermissions.Manage)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _countryService.DeleteAsync(id);
        return Ok();
    }

    [HttpPost("upload-json")]
    [Permission(CountryPermissions.Manage)]
    public async Task<IActionResult> UploadJson(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("A valid file is required.");
        }

        using var stream = new StreamReader(file.OpenReadStream());
        var json = await stream.ReadToEndAsync(cancellationToken);

        List<CountryDto>? countries;
        try
        {
            countries = JsonSerializer.Deserialize<List<CountryDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return BadRequest("Invalid JSON format.");
        }

        if (countries is null || countries.Count == 0)
        {
            return BadRequest("No country records were found in file.");
        }

        var ids = await _countryService.AddRangeAsync(countries);
        return Ok(new { count = ids.Count, ids });
    }

    private static Func<IQueryable<STYS.Countries.Entities.Country>, IOrderedQueryable<STYS.Countries.Entities.Country>>? BuildOrderBy(string? sortBy, string? sortDir)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return null;
        }

        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        var normalized = sortBy.Trim().ToLowerInvariant();
        return normalized switch
        {
            "code" => desc ? q => q.OrderByDescending(x => x.Code).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.Code).ThenBy(x => x.Id),
            "name" => desc ? q => q.OrderByDescending(x => x.Name).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.Name).ThenBy(x => x.Id),
            "id" => desc ? q => q.OrderByDescending(x => x.Id) : q => q.OrderBy(x => x.Id),
            "createdat" => desc ? q => q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            _ => null
        };
    }
}
