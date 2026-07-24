namespace STYS.Tests.TestSupport;

/// <summary>
/// xUnit'in gercek IAsyncLifetime calisma sirasini (test govdesi -> DisposeAsync) ve HER IKISI de
/// basarisiz oldugunda hatalarin nasil BIRLESTIRILDIGINI (xUnit.Sdk.ExceptionAggregator.ToException:
/// tek hata varsa OLDUGU GIBI, birden fazla hata varsa AggregateException OLARAK) modelleyen,
/// bagimsiz test edilebilir bir yardimci. PosTahsilatValorCleanupTests, gercek bir xUnit test
/// calistiricisi kurmadan bu davranisi (test govdesi hatasi ile cleanup hatasinin BIRBIRINI
/// MASKELEMEDIGINI, ikisinin de InnerExceptions icinde ayri ayri GOZLEMLENEBILIR kaldigini)
/// dogrulamak icin kullanir.
/// </summary>
public static class TestLifecycleHarness
{
    /// <summary>Test govdesini calistirir, ARDINDAN (govde basarili da olsa) cleanup'i calistirir -
    /// tipki xUnit'in IAsyncLifetime.DisposeAsync'i her zaman cagirmasi gibi. Ikisi de basarisiz
    /// olursa AggregateException (iki InnerException ile) doner; yalnizca biri basarisiz olursa O
    /// exception dogrudan doner; ikisi de basarili olursa null doner.</summary>
    public static async Task<Exception?> CalistirAsync(Func<Task> testGovdesi, Func<Task> cleanup)
    {
        var hatalar = new List<Exception>();

        try
        {
            await testGovdesi();
        }
        catch (Exception ex)
        {
            hatalar.Add(ex);
        }

        try
        {
            await cleanup();
        }
        catch (Exception ex)
        {
            hatalar.Add(ex);
        }

        return hatalar.Count switch
        {
            0 => null,
            1 => hatalar[0],
            _ => new AggregateException(hatalar),
        };
    }
}
