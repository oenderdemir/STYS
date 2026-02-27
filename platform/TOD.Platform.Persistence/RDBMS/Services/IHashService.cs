namespace TOD.Platform.Persistence.RDBMS.Services;

public interface IHashService
{
    Task<string> ComputeHash(string data);
}
