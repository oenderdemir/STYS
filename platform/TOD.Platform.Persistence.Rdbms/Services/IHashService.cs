namespace TOD.Platform.Persistence.Rdbms.Services;

public interface IHashService
{
    Task<string> ComputeHash(string data);
}
