using STYS.Muhasebe.StokMaliyetPolitikalari.Dtos;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.StokMaliyetPolitikalari.Services;

public class StokMaliyetStrategyResolver : IStokMaliyetStrategyResolver
{
    private readonly IReadOnlyDictionary<string, IStokMaliyetStrategy> _strategies;

    public StokMaliyetStrategyResolver(IEnumerable<IStokMaliyetStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(x => x.MaliyetYontemi, StringComparer.Ordinal);
    }

    public IStokMaliyetStrategy Resolve(string maliyetYontemi)
    {
        if (_strategies.TryGetValue(maliyetYontemi, out var strategy))
        {
            return strategy;
        }

        if (string.Equals(maliyetYontemi, StokMaliyetYontemleri.FIFO, StringComparison.Ordinal)
            || string.Equals(maliyetYontemi, StokMaliyetYontemleri.LIFO, StringComparison.Ordinal))
        {
            throw new BaseException("Seçilen stok maliyet yöntemi henüz desteklenmiyor.", 400);
        }

        throw new BaseException("Geçersiz stok maliyet yöntemi seçildi.", 400);
    }
}
