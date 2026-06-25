using Microsoft.AspNetCore.Http;

namespace STYS.Kurumlar.Dto;

public class UploadKurumLogoRequest
{
    public IFormFile File { get; set; } = default!;
}
