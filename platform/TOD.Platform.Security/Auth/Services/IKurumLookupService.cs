namespace TOD.Platform.Security.Auth.Services;

public interface IKurumLookupService
{
    Task<bool> IsActiveKurumAsync(int kurumId, CancellationToken cancellationToken = default);
}