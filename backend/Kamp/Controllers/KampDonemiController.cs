using Microsoft.AspNetCore.Mvc;
using STYS.Kamp.Dto;
using STYS.Kamp.Entities;
using STYS.Kamp.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Kamp.Controllers;

public class KampDonemiController : UIController
{
    private readonly IKampDonemiService _kampDonemiService;

    public KampDonemiController(IKampDonemiService kampDonemiService)
    {
        _kampDonemiService = kampDonemiService;
    }

    [HttpGet]
    [Permission(
        StructurePermissions.KampDonemiYonetimi.View,
        StructurePermissions.KampDonemiTanimYonetimi.View,
        StructurePermissions.KampDonemiTesisAtamaYonetimi.View)]
    public async Task<List<KampDonemiDto>> GetAll()
        => (await _kampDonemiService.GetAllAsync()).ToList();

    [HttpGet("yonetim-baglam")]
    [Permission(
        StructurePermissions.KampDonemiYonetimi.View,
        StructurePermissions.KampDonemiTesisAtamaYonetimi.View)]
    public async Task<ActionResult<KampDonemiYonetimBaglamDto>> GetYonetimBaglam(CancellationToken cancellationToken)
    {
        var result = await _kampDonemiService.GetYonetimBaglamAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{kampDonemiId:int}/tesis-atamalari")]
    [Permission(
        StructurePermissions.KampDonemiYonetimi.View,
        StructurePermissions.KampDonemiTesisAtamaYonetimi.View)]
    public async Task<ActionResult<List<KampDonemiTesisAtamaDto>>> GetTesisAtamalari([FromRoute] int kampDonemiId, CancellationToken cancellationToken)
    {
        var result = await _kampDonemiService.GetTesisAtamalariAsync(kampDonemiId, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{kampDonemiId:int}/tesis-atamalari")]
    [Permission(
        StructurePermissions.KampDonemiYonetimi.Manage,
        StructurePermissions.KampDonemiTesisAtamaYonetimi.Manage)]
    public async Task<ActionResult<List<KampDonemiTesisAtamaDto>>> KaydetTesisAtamalari(
        [FromRoute] int kampDonemiId,
        [FromBody] KampDonemiTesisAtamaKaydetRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _kampDonemiService.KaydetTesisAtamalariAsync(kampDonemiId, request.Kayitlar, cancellationToken);
        return Ok(result);
    }

    [HttpGet("paged")]
    [Permission(
        StructurePermissions.KampDonemiYonetimi.View,
        StructurePermissions.KampDonemiTanimYonetimi.View)]
    public async Task<ActionResult<PagedResult<KampDonemiDto>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery(Name = "q")] string? query,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir = "desc",
        CancellationToken cancellationToken = default)
    {
        var orderBy = BuildOrderBy(sortBy, sortDir);
        if (orderBy is null && !string.IsNullOrWhiteSpace(sortBy))
        {
            return BadRequest("Desteklenmeyen siralama kolonu. Desteklenen alanlar: yil, program, kod, ad, aktifMi, basvuruBaslangicTarihi, konaklamaBaslangicTarihi, id, createdAt.");
        }

        var normalizedQuery = query?.Trim();
        var result = await _kampDonemiService.GetPagedAsync(
            request,
            predicate: string.IsNullOrWhiteSpace(normalizedQuery)
                ? null
                : x => x.Ad.Contains(normalizedQuery)
                    || x.Kod.Contains(normalizedQuery)
                    || (x.KampProgrami != null && x.KampProgrami.Ad.Contains(normalizedQuery)),
            orderBy: orderBy ?? (q => q.OrderByDescending(x => x.KampProgrami!.Yil).ThenBy(x => x.KampProgrami!.Ad).ThenBy(x => x.Ad)));
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Permission(
        StructurePermissions.KampDonemiYonetimi.View,
        StructurePermissions.KampDonemiTanimYonetimi.View)]
    public async Task<ActionResult<KampDonemiDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _kampDonemiService.GetByIdAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    [HttpPost]
    [Permission(
        StructurePermissions.KampDonemiYonetimi.Manage,
        StructurePermissions.KampDonemiTanimYonetimi.Manage)]
    public async Task<ActionResult<KampDonemiDto>> Create([FromBody] KampDonemiDto dto)
    {
        var created = await _kampDonemiService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(
        StructurePermissions.KampDonemiYonetimi.Manage,
        StructurePermissions.KampDonemiTanimYonetimi.Manage)]
    public async Task<ActionResult<KampDonemiDto>> Update(int id, [FromBody] KampDonemiDto dto)
    {
        dto.Id = id;
        var updated = await _kampDonemiService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(
        StructurePermissions.KampDonemiYonetimi.Manage,
        StructurePermissions.KampDonemiTanimYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _kampDonemiService.DeleteAsync(id);
        return Ok();
    }

    private static Func<IQueryable<KampDonemi>, IOrderedQueryable<KampDonemi>>? BuildOrderBy(string? sortBy, string? sortDir)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return null;
        }

        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        var normalized = sortBy.Trim().ToLowerInvariant();
        return normalized switch
        {
            "yil" => desc ? q => q.OrderByDescending(x => x.KampProgrami!.Yil).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.KampProgrami!.Yil).ThenBy(x => x.Id),
            "program" => desc ? q => q.OrderByDescending(x => x.KampProgrami!.Ad).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.KampProgrami!.Ad).ThenBy(x => x.Id),
            "kod" => desc ? q => q.OrderByDescending(x => x.Kod).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.Kod).ThenBy(x => x.Id),
            "ad" => desc ? q => q.OrderByDescending(x => x.Ad).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.Ad).ThenBy(x => x.Id),
            "aktifmi" => desc ? q => q.OrderByDescending(x => x.AktifMi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.AktifMi).ThenBy(x => x.Id),
            "basvurubaslangictarihi" => desc ? q => q.OrderByDescending(x => x.BasvuruBaslangicTarihi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.BasvuruBaslangicTarihi).ThenBy(x => x.Id),
            "konaklamabaslangictarihi" => desc ? q => q.OrderByDescending(x => x.KonaklamaBaslangicTarihi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.KonaklamaBaslangicTarihi).ThenBy(x => x.Id),
            "id" => desc ? q => q.OrderByDescending(x => x.Id) : q => q.OrderBy(x => x.Id),
            "createdat" => desc ? q => q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            _ => null
        };
    }
}
