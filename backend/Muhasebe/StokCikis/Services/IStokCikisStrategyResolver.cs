namespace STYS.Muhasebe.StokCikis.Services;

public interface IStokCikisStrategyResolver
{
    IStokCikisStrategy Resolve(string yontem);
}
