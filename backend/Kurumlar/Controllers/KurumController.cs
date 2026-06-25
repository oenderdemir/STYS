using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using STYS.Kurumlar.Dto;
using STYS.Kurumlar.Options;
using STYS.Kurumlar.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Identity;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using STYS.Infrastructure.EntityFramework;

namespace STYS.Kurumlar.Controllers;

public class KurumController : UIController
{
    private readonly IKurumService _kurumService;
    private readonly TodIdentityDbContext _identityDbContext;
    private readonly StysAppDbContext _stysDbContext;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;
    private readonly KurumLogoStorageOptions _logoOptions;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<KurumController> _logger;

    public KurumController(
        IKurumService kurumService,
        TodIdentityDbContext identityDbContext,
        StysAppDbContext stysDbContext,
        ICurrentUserAccessor currentUserAccessor,
        ICurrentTenantAccessor currentTenantAccessor,
        IOptions<KurumLogoStorageOptions> logoOptions,
        IWebHostEnvironment env,
        ILogger<KurumController> logger)
    {
        _kurumService = kurumService;
        _identityDbContext = identityDbContext;
        _stysDbContext = stysDbContext;
        _currentUserAccessor = currentUserAccessor;
        _currentTenantAccessor = currentTenantAccessor;
        _logoOptions = logoOptions.Value;
        _env = env;
        _logger = logger;
    }

    // Kurum yaratma ve silme SuperAdmin ile sinirli. Guncelleme aktif kurum scope'una gore yapilir.

    [HttpGet]
    [Permission(IdentityPermissions.UserManagement.View, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<List<KurumDto>> GetAll(CancellationToken cancellationToken)
    {
        var kurumlar = await GetAccessibleKurumlarAsync(cancellationToken);
        EnrichWithLogoUrl(kurumlar);
        return kurumlar;
    }

    [HttpGet("{id:int}")]
    [Permission(IdentityPermissions.UserManagement.View, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<ActionResult<KurumDto>> GetById(int id, CancellationToken cancellationToken)
    {
        await EnsureCanAccessKurumAsync(id, cancellationToken);
        var kurum = await _kurumService.GetByIdAsync(id, cancellationToken);
        if (kurum is null)
        {
            return NotFound();
        }

        EnrichWithLogoUrl(kurum);
        return Ok(kurum);
    }

    [HttpPost]
    [Permission(TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<ActionResult<KurumDto>> Create([FromBody] CreateKurumRequest request, CancellationToken cancellationToken)
    {
        request.TenantKey = NormalizeTenantKey(request.TenantKey);
        request.LoginHost = NormalizeLoginHost(request.LoginHost);
        var created = await _kurumService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(IdentityPermissions.UserManagement.Manage, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<ActionResult<KurumDto>> Update(int id, [FromBody] UpdateKurumRequest request, CancellationToken cancellationToken)
    {
        request.TenantKey = NormalizeTenantKey(request.TenantKey);
        request.LoginHost = NormalizeLoginHost(request.LoginHost);
        await EnsureCanUpdateKurumAsync(id, cancellationToken);
        var updated = await _kurumService.UpdateAsync(id, request, cancellationToken);
        EnrichWithLogoUrl(updated);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _kurumService.DeleteAsync(id, cancellationToken);
        return Ok();
    }

    [HttpGet("benim-kurumlarim")]
    [Permission]
    public async Task<ActionResult<List<KurumDto>>> GetMyKurumlar(CancellationToken cancellationToken)
    {
        var kurumlar = await GetAccessibleKurumlarAsync(cancellationToken);
        EnrichWithLogoUrl(kurumlar);
        return Ok(kurumlar);
    }

    // ──────────────────────────── LOGO ENDPOINTLERİ ────────────────────────────

    [HttpGet("{kurumId:int}/logo")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLogo(int kurumId, CancellationToken cancellationToken)
    {
        var kurum = await _stysDbContext.Kurumlar
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == kurumId && !x.IsDeleted, cancellationToken);

        if (kurum is null || string.IsNullOrWhiteSpace(kurum.LogoDosyaAdi))
        {
            return NotFound();
        }

        var filePath = ResolveLogoPath(kurum.LogoDosyaAdi);
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        var contentType = string.IsNullOrWhiteSpace(kurum.LogoContentType)
            ? "application/octet-stream"
            : kurum.LogoContentType;

        Response.Headers.CacheControl = "private, max-age=300";
        return PhysicalFile(filePath, contentType);
    }

    [HttpPost("{kurumId:int}/logo")]
    [Consumes("multipart/form-data")]
    [Permission(IdentityPermissions.UserManagement.Manage, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<ActionResult<KurumDto>> UploadLogo(
        int kurumId,
        [FromForm] UploadKurumLogoRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageLogoAsync(kurumId, cancellationToken);

        var file = request.File;

        if (file is null || file.Length == 0)
        {
            throw new BaseException("Dosya boş olamaz.", 400);
        }

        if (file.Length > _logoOptions.MaxFileSizeBytes)
        {
            throw new BaseException($"Dosya boyutu {_logoOptions.MaxFileSizeBytes / 1024 / 1024} MB sınırını aşıyor.", 400);
        }

        var contentType = file.ContentType?.ToLowerInvariant().Trim() ?? string.Empty;
        if (!_logoOptions.AllowedContentTypes.Contains(contentType))
        {
            throw new BaseException("Desteklenmeyen dosya türü. İzin verilen: PNG, JPEG, WEBP.", 400);
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".webp" };
        if (!allowedExtensions.Contains(extension))
        {
            throw new BaseException("Desteklenmeyen dosya uzantısı.", 400);
        }

        var kurum = await _stysDbContext.Kurumlar
            .FirstOrDefaultAsync(x => x.Id == kurumId && !x.IsDeleted, cancellationToken);

        if (kurum is null)
        {
            return NotFound();
        }

        EnsureRootPathExists();

        var safeFileName = $"kurum-{kurumId}-{Guid.NewGuid():N}{extension}";
        var filePath = ResolveLogoPath(safeFileName);

        long savedFileSize;
        await using (var inputStream = file.OpenReadStream())
        {
            savedFileSize = await TrimAndSaveLogoAsync(inputStream, filePath, contentType, cancellationToken);
        }

        var oldFileName = kurum.LogoDosyaAdi;

        kurum.LogoDosyaAdi = safeFileName;
        kurum.LogoOrijinalDosyaAdi = file.FileName;
        kurum.LogoContentType = contentType;
        kurum.LogoBoyut = savedFileSize;
        kurum.LogoYuklenmeTarihi = DateTime.UtcNow;

        await _stysDbContext.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(oldFileName))
        {
            TryDeleteLogoFile(oldFileName);
        }

        _logger.LogInformation(
            "Tenant.Kurum.Logo.Uploaded KurumId={KurumId} LogoDosyaAdi={LogoDosyaAdi} LogoContentType={ContentType} LogoBoyut={Boyut}",
            kurumId, safeFileName, contentType, file.Length);

        var dto = await _kurumService.GetByIdAsync(kurumId, cancellationToken);
        if (dto is not null)
        {
            EnrichWithLogoUrl(dto);
        }

        return Ok(dto);
    }

    [HttpDelete("{kurumId:int}/logo")]
    [Permission(IdentityPermissions.UserManagement.Manage, TodPlatformAuthorizationConstants.SuperAdminPermission)]
    public async Task<IActionResult> DeleteLogo(int kurumId, CancellationToken cancellationToken)
    {
        await EnsureCanManageLogoAsync(kurumId, cancellationToken);

        var kurum = await _stysDbContext.Kurumlar
            .FirstOrDefaultAsync(x => x.Id == kurumId && !x.IsDeleted, cancellationToken);

        if (kurum is null)
        {
            return NotFound();
        }

        var oldFileName = kurum.LogoDosyaAdi;

        kurum.LogoDosyaAdi = null;
        kurum.LogoOrijinalDosyaAdi = null;
        kurum.LogoContentType = null;
        kurum.LogoBoyut = null;
        kurum.LogoYuklenmeTarihi = null;

        await _stysDbContext.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(oldFileName))
        {
            TryDeleteLogoFile(oldFileName);
        }

        _logger.LogInformation(
            "Tenant.Kurum.Logo.Deleted KurumId={KurumId}", kurumId);

        return NoContent();
    }

    // ──────────────────────────── YARDIMCI METOTLAR ────────────────────────────

    private static void EnrichWithLogoUrl(KurumDto kurum)
    {
        if (!string.IsNullOrWhiteSpace(kurum.LogoDosyaAdi) && kurum.Id.HasValue)
        {
            var ticks = kurum.LogoYuklenmeTarihi?.Ticks ?? 0;
            kurum.LogoUrl = $"/ui/kurum/{kurum.Id.Value}/logo?v={ticks}";
        }
    }

    private static void EnrichWithLogoUrl(IEnumerable<KurumDto> kurumlar)
    {
        foreach (var kurum in kurumlar)
        {
            EnrichWithLogoUrl(kurum);
        }
    }

    private string ResolveLogoPath(string fileName)
    {
        var rootPath = Path.IsPathRooted(_logoOptions.RootPath)
            ? _logoOptions.RootPath
            : Path.Combine(_env.ContentRootPath, _logoOptions.RootPath);

        return Path.Combine(rootPath, fileName);
    }

    private void EnsureRootPathExists()
    {
        var rootPath = Path.IsPathRooted(_logoOptions.RootPath)
            ? _logoOptions.RootPath
            : Path.Combine(_env.ContentRootPath, _logoOptions.RootPath);

        Directory.CreateDirectory(rootPath);
    }

    private void TryDeleteLogoFile(string fileName)
    {
        try
        {
            var filePath = ResolveLogoPath(fileName);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Eski logo dosyası silinemedi: {FileName}", fileName);
        }
    }

    private static async Task<long> TrimAndSaveLogoAsync(
        Stream inputStream,
        string outputPath,
        string contentType,
        CancellationToken cancellationToken)
    {
        using var image = await Image.LoadAsync<Rgba32>(inputStream, cancellationToken);

        var bounds = FindLogoContentBounds(image);
        if (bounds.HasValue)
        {
            var padded = AddPadding(bounds.Value, image.Width, image.Height);
            image.Mutate(ctx => ctx.Crop(padded));
        }

        await using var outputStream = System.IO.File.Create(outputPath);
        switch (contentType)
        {
            case "image/jpeg":
                await image.SaveAsJpegAsync(outputStream, cancellationToken: cancellationToken);
                break;
            case "image/webp":
                await image.SaveAsWebpAsync(outputStream, cancellationToken: cancellationToken);
                break;
            default:
                await image.SaveAsPngAsync(outputStream, cancellationToken: cancellationToken);
                break;
        }

        return outputStream.Length;
    }

    private static Rectangle? FindLogoContentBounds(Image<Rgba32> image)
    {
        var minX = image.Width;
        var minY = image.Height;
        var maxX = -1;
        var maxY = -1;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    ref var pixel = ref row[x];
                    var isTransparent = pixel.A < 10;
                    var isNearWhite = pixel.R >= 245 && pixel.G >= 245 && pixel.B >= 245;
                    if (isTransparent || isNearWhite)
                    {
                        continue;
                    }

                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }
        });

        if (maxX < minX || maxY < minY)
        {
            return null;
        }

        return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
    }

    private static Rectangle AddPadding(Rectangle bounds, int imageWidth, int imageHeight)
    {
        var padding = Math.Max(8, Math.Min(imageWidth, imageHeight) / 30);
        var x = Math.Max(0, bounds.X - padding);
        var y = Math.Max(0, bounds.Y - padding);
        var right = Math.Min(imageWidth, bounds.Right + padding);
        var bottom = Math.Min(imageHeight, bounds.Bottom + padding);
        return Rectangle.FromLTRB(x, y, right, bottom);
    }

    private static string? NormalizeTenantKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().ToLowerInvariant();
    }

    private static string? NormalizeLoginHost(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var host = value.Trim().ToLowerInvariant();
        if (host.StartsWith("https://")) host = host[8..];
        else if (host.StartsWith("http://")) host = host[7..];
        host = host.TrimEnd('/');
        var colonIdx = host.IndexOf(':');
        if (colonIdx >= 0) host = host[..colonIdx];
        if (host.StartsWith("www.")) host = host[4..];
        return string.IsNullOrWhiteSpace(host) ? null : host;
    }

    private async Task EnsureCanManageLogoAsync(int kurumId, CancellationToken cancellationToken)
    {
        if (_currentTenantAccessor.IsSuperAdmin())
        {
            return;
        }

        var currentKurumId = _currentTenantAccessor.GetCurrentKurumId();
        if (!currentKurumId.HasValue || !_currentTenantAccessor.IsKurumAdmin() || currentKurumId.Value != kurumId)
        {
            throw new BaseException("Bu kurum için logo yönetme yetkiniz bulunmuyor.", 403);
        }

        var exists = await _stysDbContext.Kurumlar.AnyAsync(x => x.Id == kurumId && x.AktifMi && !x.IsDeleted, cancellationToken);
        if (!exists)
        {
            throw new BaseException("Kurum bulunamadı veya aktif değil.", 404);
        }
    }

    private async Task<List<KurumDto>> GetAccessibleKurumlarAsync(CancellationToken cancellationToken)
    {
        var kurumlar = await _kurumService.GetAllAsync(cancellationToken);
        if (_currentTenantAccessor.IsSuperAdmin())
        {
            return kurumlar.Where(x => x.AktifMi).ToList();
        }

        var accessibleIds = (await GetAccessibleKurumIdsAsync(cancellationToken)).ToHashSet();
        if (accessibleIds.Count == 0)
        {
            return [];
        }

        return kurumlar
            .Where(x => x.AktifMi && x.Id.HasValue && accessibleIds.Contains(x.Id.Value))
            .ToList();
    }

    private async Task<List<int>> GetAccessibleKurumIdsAsync(CancellationToken cancellationToken)
    {
        if (_currentTenantAccessor.IsSuperAdmin())
        {
            var kurumlar = await _kurumService.GetAllAsync(cancellationToken);
            return kurumlar
                .Where(x => x.Id.HasValue)
                .Select(x => x.Id!.Value)
                .ToList();
        }

        var currentUserId = _currentUserAccessor.GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return [];
        }

        return await _identityDbContext.UserKurums
            .Where(x => x.UserId == currentUserId.Value && x.AktifMi)
            .Select(x => x.KurumId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private async Task EnsureCanAccessKurumAsync(int targetKurumId, CancellationToken cancellationToken)
    {
        var accessibleIds = (await GetAccessibleKurumIdsAsync(cancellationToken)).ToHashSet();
        if (accessibleIds.Count == 0 || !accessibleIds.Contains(targetKurumId))
        {
            throw new BaseException("Bu kurum icin yetkiniz bulunmuyor.", 403);
        }
    }

    private async Task EnsureCanUpdateKurumAsync(int targetKurumId, CancellationToken cancellationToken)
    {
        if (_currentTenantAccessor.IsSuperAdmin())
        {
            return;
        }

        var currentKurumId = _currentTenantAccessor.GetCurrentKurumId();
        if (!currentKurumId.HasValue || !_currentTenantAccessor.IsKurumAdmin() || currentKurumId.Value != targetKurumId)
        {
            throw new BaseException("Bu kurum icin yetkiniz bulunmuyor.", 403);
        }

        var exists = await _stysDbContext.Kurumlar.AnyAsync(x => x.Id == targetKurumId && x.AktifMi, cancellationToken);
        if (!exists)
        {
            throw new BaseException("Kurum bulunamadi veya aktif degil.", 404);
        }
    }
}
