using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Muhasebe.SatisBelgeleri.Controllers;

[Route("ui/muhasebe/satis-belgeleri")]
public class SatisBelgeleriController : UIController
{
    private readonly ISatisBelgesiService _service;
    private readonly ISatisBelgesiTaslakOlusturmaService _taslakOlusturmaService;
    private readonly ISatisBelgesiMuhasebeFisService _muhasebeFisService;

    public SatisBelgeleriController(
        ISatisBelgesiService service,
        ISatisBelgesiTaslakOlusturmaService taslakOlusturmaService,
        ISatisBelgesiMuhasebeFisService muhasebeFisService)
    {
        _service = service;
        _taslakOlusturmaService = taslakOlusturmaService;
        _muhasebeFisService = muhasebeFisService;
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.MuhasebeSatisBelgeleriYonetimi.View)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("filter")]
    [Permission(StructurePermissions.MuhasebeSatisBelgeleriYonetimi.View)]
    public async Task<IActionResult> Filter(
        [FromBody] SatisBelgesiFilterDto filter,
        CancellationToken cancellationToken)
    {
        var result = await _service.FilterAsync(filter, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Permission(StructurePermissions.MuhasebeSatisBelgeleriYonetimi.Manage)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSatisBelgesiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("kaynaktan-taslak-olustur")]
    [Permission(StructurePermissions.MuhasebeSatisBelgeleriYonetimi.Manage)]
    public async Task<IActionResult> KaynaktanTaslakOlustur(
        [FromBody] SatisBelgesiTaslakOlusturRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _taslakOlusturmaService.KaynaktanTaslakOlusturAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.MuhasebeSatisBelgeleriYonetimi.Manage)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateSatisBelgesiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.MuhasebeSatisBelgeleriYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok();
    }

    [HttpPost("{id:int}/muhasebe-onayina-gonder")]
    [Permission(StructurePermissions.MuhasebeSatisBelgeleriYonetimi.Manage)]
    public async Task<IActionResult> MuhasebeOnayinaGonder(int id, CancellationToken cancellationToken)
    {
        await _service.MuhasebeOnayinaGonderAsync(id, cancellationToken);
        return Ok();
    }

    [HttpPost("{id:int}/muhasebe-onayla")]
    [Permission(StructurePermissions.MuhasebeSatisBelgeleriYonetimi.Manage)]
    public async Task<IActionResult> MuhasebeOnayla(int id, CancellationToken cancellationToken)
    {
        await _service.MuhasebeOnaylaAsync(id, cancellationToken);
        return Ok();
    }

    [HttpPost("{id:int}/reddet")]
    [Permission(StructurePermissions.MuhasebeSatisBelgeleriYonetimi.Manage)]
    public async Task<IActionResult> Reddet(
        int id,
        [FromBody] SatisBelgesiRedRequest request,
        CancellationToken cancellationToken)
    {
        await _service.ReddetAsync(id, request.RedNedeni, cancellationToken);
        return Ok();
    }

    [HttpPost("{id:int}/iptal")]
    [Permission(StructurePermissions.MuhasebeSatisBelgeleriYonetimi.Manage)]
    public async Task<IActionResult> IptalEt(int id, CancellationToken cancellationToken)
    {
        await _service.IptalEtAsync(id, cancellationToken);
        return Ok();
    }

    [HttpPost("{id:int}/muhasebe-fisi-olustur")]
    [Permission(StructurePermissions.MuhasebeSatisBelgeleriYonetimi.Manage)]
    public async Task<IActionResult> MuhasebeFisiOlustur(int id, CancellationToken cancellationToken)
    {
        var result = await _muhasebeFisService.MuhasebeFisiOlusturAsync(id, cancellationToken);
        return Ok(result);
    }
}

/// <summary>Ret nedeni taşıyan küçük request.</summary>
public class SatisBelgesiRedRequest
{
    public string RedNedeni { get; set; } = string.Empty;
}
