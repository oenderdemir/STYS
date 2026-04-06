namespace STYS.Kamp.Services;

public interface IKampParametreService
{
    Task LoadAsync(CancellationToken cancellationToken = default);
    decimal GetDecimal(string kod, decimal defaultValue = 0m);
    int GetInt(string kod, int defaultValue = 0);
    DateTime GetDate(string kod, DateTime defaultValue = default);
}
