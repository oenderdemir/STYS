using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.NakitBankaPozisyonu.Dtos;
using STYS.Muhasebe.NakitBankaPozisyonu.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Muhasebe.NakitBankaPozisyonu.Controllers;

/// <summary>
/// Nakit ve Banka Pozisyonu - SALT OKUNUR izleme/raporlama ekrani. Hicbir endpoint veri
/// degistirmez; tumu StructurePermissions.NakitBankaPozisyonuYonetimi.View yetkisi ister.
/// </summary>
[Route("ui/muhasebe/nakit-banka-pozisyonu")]
public class NakitBankaPozisyonuController : UIController
{
    private readonly INakitBankaPozisyonuService _service;

    public NakitBankaPozisyonuController(INakitBankaPozisyonuService service)
    {
        _service = service;
    }

    [HttpGet("ozet")]
    [Permission(StructurePermissions.NakitBankaPozisyonuYonetimi.View)]
    public async Task<ActionResult<NakitBankaPozisyonuOzetDto>> GetOzet([FromQuery] NakitBankaPozisyonuFilterDto filter, CancellationToken cancellationToken)
        => Ok(await _service.GetOzetAsync(filter, cancellationToken));

    [HttpGet("hesaplar")]
    [Permission(StructurePermissions.NakitBankaPozisyonuYonetimi.View)]
    public async Task<ActionResult<NakitBankaPozisyonuHesaplarDto>> GetHesaplar([FromQuery] NakitBankaPozisyonuFilterDto filter, CancellationToken cancellationToken)
        => Ok(await _service.GetHesaplarAsync(filter, cancellationToken));

    [HttpGet("banka-hesaplari/{id:int}/valor-takvimi")]
    [Permission(StructurePermissions.NakitBankaPozisyonuYonetimi.View)]
    public async Task<ActionResult<BankaValorTakvimiDto>> GetValorTakvimi(int id, [FromQuery] DateOnly? raporTarihi, CancellationToken cancellationToken)
        => Ok(await _service.GetValorTakvimiAsync(id, raporTarihi, cancellationToken));
}
