using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using STYS.Bildirimler.Dto;
using STYS.Bildirimler.Entities;
using STYS.Bildirimler.Hubs;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Bildirimler.Services;

public class BildirimService : IBildirimService
{
    private readonly StysAppDbContext _stysDbContext;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IHubContext<BildirimHub> _hubContext;

    public BildirimService(
        StysAppDbContext stysDbContext,
        ICurrentUserAccessor currentUserAccessor,
        IHubContext<BildirimHub> hubContext)
    {
        _stysDbContext = stysDbContext;
        _currentUserAccessor = currentUserAccessor;
        _hubContext = hubContext;
    }

    public async Task<List<BildirimDto>> GetCurrentUserBildirimlerAsync(int take = 20, CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserIdOrThrow();
        var normalizedTake = Math.Clamp(take, 1, 100);

        return await _stysDbContext.Bildirimler
            .Where(x => x.UserId == currentUserId)
            .OrderBy(x => x.IsRead)
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(normalizedTake)
            .Select(x => new BildirimDto
            {
                Id = x.Id,
                Tip = x.Tip,
                Baslik = x.Baslik,
                Mesaj = x.Mesaj,
                Link = x.Link,
                Severity = x.Severity,
                IsRead = x.IsRead,
                CreatedAt = x.CreatedAt ?? DateTime.UtcNow
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCurrentUserUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserIdOrThrow();
        return await _stysDbContext.Bildirimler
            .CountAsync(x => x.UserId == currentUserId && !x.IsRead, cancellationToken);
    }

    public async Task MarkAsReadAsync(int bildirimId, CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserIdOrThrow();
        var entity = await _stysDbContext.Bildirimler
            .FirstOrDefaultAsync(x => x.Id == bildirimId && x.UserId == currentUserId, cancellationToken);

        if (entity is null)
        {
            throw new BaseException("Bildirim bulunamadi.", 404);
        }

        if (entity.IsRead)
        {
            return;
        }

        entity.IsRead = true;
        entity.ReadAt = DateTime.UtcNow;
        await _stysDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllAsReadAsync(CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserIdOrThrow();
        var unread = await _stysDbContext.Bildirimler
            .Where(x => x.UserId == currentUserId && !x.IsRead)
            .ToListAsync(cancellationToken);

        if (unread.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var entity in unread)
        {
            entity.IsRead = true;
            entity.ReadAt = now;
        }

        await _stysDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task PublishToTesisUsersAsync(int tesisId, BildirimOlusturRequestDto request, CancellationToken cancellationToken = default)
    {
        if (tesisId <= 0)
        {
            return;
        }

        var tesisYoneticiUserIds = await _stysDbContext.TesisYoneticileri
            .Where(x => x.TesisId == tesisId)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);

        var resepsiyonistUserIds = await _stysDbContext.TesisResepsiyonistleri
            .Where(x => x.TesisId == tesisId)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);

        var binaYoneticiUserIds = await (
            from binaYonetici in _stysDbContext.BinaYoneticileri
            join bina in _stysDbContext.Binalar on binaYonetici.BinaId equals bina.Id
            where bina.TesisId == tesisId
            select binaYonetici.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var tesisSahiplikUserIds = await _stysDbContext.KullaniciTesisSahiplikleri
            .Where(x => x.TesisId == tesisId)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);

        var userIds = tesisYoneticiUserIds
            .Concat(resepsiyonistUserIds)
            .Concat(binaYoneticiUserIds)
            .Concat(tesisSahiplikUserIds)
            .Distinct()
            .ToList();

        var currentUserId = _currentUserAccessor.GetCurrentUserId();
        if (currentUserId.HasValue && currentUserId.Value != Guid.Empty)
        {
            userIds.Add(currentUserId.Value);
        }

        await PublishToUsersAsync(userIds, request, cancellationToken);
    }

    public async Task PublishToUsersAsync(IEnumerable<Guid> userIds, BildirimOlusturRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedUserIds = userIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (normalizedUserIds.Count == 0)
        {
            return;
        }

        var normalizedType = NormalizeOrFallback(request.Tip, "Genel");
        var normalizedTitle = NormalizeOrFallback(request.Baslik, "Yeni Bildirim");
        var normalizedMessage = NormalizeOrFallback(request.Mesaj, "Yeni bir bildirim var.");
        var normalizedSeverity = BildirimSeverityleri.Normalize(request.Severity);
        var normalizedLink = string.IsNullOrWhiteSpace(request.Link)
            ? null
            : request.Link.Trim();

        var entities = normalizedUserIds
            .Select(userId => new Bildirim
            {
                UserId = userId,
                Tip = normalizedType,
                Baslik = normalizedTitle,
                Mesaj = normalizedMessage,
                Link = normalizedLink,
                Severity = normalizedSeverity,
                IsRead = false
            })
            .ToList();

        await _stysDbContext.Bildirimler.AddRangeAsync(entities, cancellationToken);
        await _stysDbContext.SaveChangesAsync(cancellationToken);

        foreach (var entity in entities)
        {
            var dto = new BildirimDto
            {
                Id = entity.Id,
                Tip = entity.Tip,
                Baslik = entity.Baslik,
                Mesaj = entity.Mesaj,
                Link = entity.Link,
                Severity = entity.Severity,
                IsRead = entity.IsRead,
                CreatedAt = entity.CreatedAt ?? DateTime.UtcNow
            };

            await _hubContext.Clients
                .Group(BildirimHub.GetUserGroupName(entity.UserId))
                .SendAsync(BildirimHub.BildirimAlindiEventName, dto, cancellationToken);
        }
    }

    private Guid GetCurrentUserIdOrThrow()
    {
        var currentUserId = _currentUserAccessor.GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            throw new BaseException("Kullanici oturumu bulunamadi.", 401);
        }

        return currentUserId.Value;
    }

    private static string NormalizeOrFallback(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim();
    }

}
