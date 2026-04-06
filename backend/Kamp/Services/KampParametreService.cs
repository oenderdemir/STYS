using System.Globalization;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;

namespace STYS.Kamp.Services;

public class KampParametreService : IKampParametreService
{
    private readonly StysAppDbContext _dbContext;
    private Dictionary<string, string>? _cache;

    public KampParametreService(StysAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _cache ??= await _dbContext.KampParametreleri
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Kod, x => x.Deger, cancellationToken);
    }

    public decimal GetDecimal(string kod, decimal defaultValue = 0m)
    {
        if (_cache != null && _cache.TryGetValue(kod, out var val) && decimal.TryParse(val, CultureInfo.InvariantCulture, out var result))
            return result;
        return defaultValue;
    }

    public int GetInt(string kod, int defaultValue = 0)
    {
        if (_cache != null && _cache.TryGetValue(kod, out var val) && int.TryParse(val, CultureInfo.InvariantCulture, out var result))
            return result;
        return defaultValue;
    }

    public DateTime GetDate(string kod, DateTime defaultValue = default)
    {
        if (_cache != null && _cache.TryGetValue(kod, out var val) && DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
            return result;
        return defaultValue;
    }

    public string? GetString(string kod, string? defaultValue = null)
    {
        if (_cache != null && _cache.TryGetValue(kod, out var val))
        {
            return val;
        }

        return defaultValue;
    }

    public IReadOnlyDictionary<string, string> GetByPrefix(string prefix)
    {
        if (_cache is null || string.IsNullOrWhiteSpace(prefix))
        {
            return new Dictionary<string, string>();
        }

        return _cache
            .Where(x => x.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    }
}
