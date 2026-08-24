using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.StokCikis.Services;

public class StokCikisStrategyResolver : IStokCikisStrategyResolver
{
    private readonly Dictionary<string, IStokCikisStrategy> _strategies;

    public StokCikisStrategyResolver(IEnumerable<IStokCikisStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(x => x.Yontem, StringComparer.Ordinal);
    }

    public IStokCikisStrategy Resolve(string yontem)
    {
        if (_strategies.TryGetValue(yontem, out var strategy))
        {
            return strategy;
        }

        throw new BaseException($"Bilinmeyen stok çıkış yöntemi: {yontem}", 400);
    }
}
