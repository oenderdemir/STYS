using Microsoft.AspNetCore.Mvc;
using STYS.RestoranMenuUrunleri.Dtos;
using STYS.RestoranMenuUrunleri.Services;
using System.Linq;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.RestoranMenuUrunleri.Controllers;

[Route("api/restoran-menu-urunleri")]
[ApiController]
public class RestoranMenuUrunleriController : UIController
{
    private readonly IRestoranMenuUrunService _service;

    public RestoranMenuUrunleriController(IRestoranMenuUrunService service)
    {
        _service = service;
    }

    [HttpGet]
    [Permission(StructurePermissions.RestoranMenuYonetimi.View)]
    public async Task<ActionResult<List<RestoranMenuUrunDto>>> GetList([FromQuery] int? kategoriId, CancellationToken cancellationToken)
    {
        var items = kategoriId.HasValue && kategoriId.Value > 0
            ? await _service.WhereAsync(x => x.RestoranMenuKategoriId == kategoriId.Value)
            : await _service.GetAllAsync();

        return Ok(items.ToList());
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.RestoranMenuYonetimi.View)]
    public async Task<ActionResult<RestoranMenuUrunDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.RestoranMenuYonetimi.Manage)]
    public async Task<ActionResult<RestoranMenuUrunDto>> Create([FromBody] CreateRestoranMenuUrunRequest request, CancellationToken cancellationToken)
    {
        var dto = new RestoranMenuUrunDto
        {
            RestoranMenuKategoriId = request.RestoranMenuKategoriId,
            Ad = request.Ad,
            Aciklama = request.Aciklama,
            Fiyat = request.Fiyat,
            ParaBirimi = request.ParaBirimi,
            HazirlamaSuresiDakika = request.HazirlamaSuresiDakika,
            AktifMi = request.AktifMi
        };

        return Ok(await _service.AddAsync(dto));
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.RestoranMenuYonetimi.Manage)]
    public async Task<ActionResult<RestoranMenuUrunDto>> Update(int id, [FromBody] UpdateRestoranMenuUrunRequest request, CancellationToken cancellationToken)
    {
        var dto = new RestoranMenuUrunDto
        {
            Id = id,
            RestoranMenuKategoriId = request.RestoranMenuKategoriId,
            Ad = request.Ad,
            Aciklama = request.Aciklama,
            Fiyat = request.Fiyat,
            ParaBirimi = request.ParaBirimi,
            HazirlamaSuresiDakika = request.HazirlamaSuresiDakika,
            AktifMi = request.AktifMi
        };

        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.RestoranMenuYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }
}
