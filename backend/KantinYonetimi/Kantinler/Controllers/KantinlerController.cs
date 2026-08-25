using Microsoft.AspNetCore.Mvc;
using STYS.KantinYonetimi.Kantinler.Dtos;
using STYS.KantinYonetimi.Kantinler.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.KantinYonetimi.Kantinler.Controllers;

[Route("ui/kantinler")]
public class KantinlerController : UIController
{
    private readonly IKantinService _service;

    public KantinlerController(IKantinService service)
    {
        _service = service;
    }

    [HttpGet]
    [Permission(StructurePermissions.KantinYonetimi.View)]
    public async Task<ActionResult<List<KantinDto>>> GetList([FromQuery] int? tesisId, CancellationToken cancellationToken)
        => Ok(await _service.GetListAsync(tesisId, cancellationToken));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.KantinYonetimi.View)]
    public async Task<ActionResult<KantinDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.KantinYonetimi.Manage)]
    public async Task<ActionResult<KantinDto>> Create([FromBody] CreateKantinRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddAsync(new KantinDto
        {
            TesisId = request.TesisId,
            DepoId = request.DepoId,
            VarsayilanNakitKasaId = request.VarsayilanNakitKasaId,
            VarsayilanPosHesapId = request.VarsayilanPosHesapId,
            PerakendeCariKartId = request.PerakendeCariKartId,
            Kod = request.Kod,
            Ad = request.Ad,
            AktifMi = request.AktifMi,
            Aciklama = request.Aciklama
        }, cancellationToken));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.KantinYonetimi.Manage)]
    public async Task<ActionResult<KantinDto>> Update(int id, [FromBody] UpdateKantinRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateAsync(new KantinDto
        {
            Id = id,
            TesisId = request.TesisId,
            DepoId = request.DepoId,
            VarsayilanNakitKasaId = request.VarsayilanNakitKasaId,
            VarsayilanPosHesapId = request.VarsayilanPosHesapId,
            PerakendeCariKartId = request.PerakendeCariKartId,
            Kod = request.Kod,
            Ad = request.Ad,
            AktifMi = request.AktifMi,
            Aciklama = request.Aciklama
        }, cancellationToken));

    [HttpGet("{id:int}/urunler")]
    [Permission(StructurePermissions.KantinYonetimi.View)]
    public async Task<ActionResult<List<KantinUrunDto>>> GetUrunler(int id, CancellationToken cancellationToken)
        => Ok(await _service.GetUrunlerAsync(id, cancellationToken));

    [HttpPost("{id:int}/urunler")]
    [Permission(StructurePermissions.KantinYonetimi.Manage)]
    public async Task<ActionResult<KantinUrunDto>> AddUrun(int id, [FromBody] CreateKantinUrunRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddUrunAsync(id, new KantinUrunDto
        {
            TasinirKartId = request.TasinirKartId,
            Barkod = request.Barkod,
            SatisFiyati = request.SatisFiyati,
            AktifMi = request.AktifMi,
            SiraNo = request.SiraNo,
            Aciklama = request.Aciklama
        }, cancellationToken));

    [HttpPut("{id:int}/urunler/{urunId:int}")]
    [Permission(StructurePermissions.KantinYonetimi.Manage)]
    public async Task<ActionResult<KantinUrunDto>> UpdateUrun(int id, int urunId, [FromBody] UpdateKantinUrunRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateUrunAsync(id, new KantinUrunDto
        {
            Id = urunId,
            TasinirKartId = request.TasinirKartId,
            Barkod = request.Barkod,
            SatisFiyati = request.SatisFiyati,
            AktifMi = request.AktifMi,
            SiraNo = request.SiraNo,
            Aciklama = request.Aciklama
        }, cancellationToken));

    [HttpGet("depolar")]
    [Permission(StructurePermissions.KantinYonetimi.View)]
    public async Task<ActionResult<List<KantinDepoSecenekDto>>> GetDepolar([FromQuery] int tesisId, CancellationToken cancellationToken)
        => Ok(await _service.GetDepolarAsync(tesisId, cancellationToken));

    [HttpGet("nakit-kasalar")]
    [Permission(StructurePermissions.KantinYonetimi.View)]
    public async Task<ActionResult<List<KantinKasaSecenekDto>>> GetNakitKasalar([FromQuery] int tesisId, CancellationToken cancellationToken)
        => Ok(await _service.GetNakitKasalarAsync(tesisId, cancellationToken));

    [HttpGet("perakende-cari-kartlar")]
    [Permission(StructurePermissions.KantinYonetimi.View)]
    public async Task<ActionResult<List<KantinCariKartSecenekDto>>> GetPerakendeCariKartlar([FromQuery] int tesisId, CancellationToken cancellationToken)
        => Ok(await _service.GetPerakendeCariKartlarAsync(tesisId, cancellationToken));

    [HttpGet("odeme-hesaplari")]
    [Permission(StructurePermissions.KantinYonetimi.View)]
    public async Task<ActionResult<List<KantinOdemeHesapSecenekDto>>> GetOdemeHesaplari([FromQuery] int tesisId, [FromQuery] string odemeYontemi, CancellationToken cancellationToken)
        => Ok(await _service.GetOdemeHesaplariAsync(tesisId, odemeYontemi, cancellationToken));

    [HttpGet("tasinir-kartlar")]
    [Permission(StructurePermissions.KantinYonetimi.View)]
    public async Task<ActionResult<List<KantinTasinirKartSecenekDto>>> GetTasinirKartlar([FromQuery] int tesisId, CancellationToken cancellationToken)
        => Ok(await _service.GetTasinirKartlarAsync(tesisId, cancellationToken));
}
