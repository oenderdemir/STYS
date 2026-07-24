namespace STYS.Tests.TestSupport;

/// <summary>Tek bir temizlik adimini (adi + calistirma delegesi) temsil eder.</summary>
public sealed record CleanupAdimi(string Ad, Func<Task> Calistir);

/// <summary>
/// FK sirasiyla tanimlanmis bagimsiz temizlik adimlarini IKI GECISLI olarak calistirir: 1. gecis
/// TUM adimlari dener, 2. gecis yalnizca 1. geciste basarisiz olanlari AYNI sirayla tekrar dener
/// (arizi/gecici hatalarin kaliciya donusmesini onlemek icin). Yalnizca HER IKI denemede de
/// basarisiz kalan adimlarin hatasi donen listede kalir - 1. geciste basarisiz olup 2. geciste
/// basarili olan bir adimin hatasi KAYITTAN CIKARILIR.
///
/// PosTahsilatValorIntegrationTests.DisposeAsync tarafindan GERCEK SQL Server temizlik adimlarini
/// calistirmak icin kullanilir; DB'siz, saf mantik olarak da (bkz. PosTahsilatValorCleanupTests)
/// bagimsiz test edilebilir.
/// </summary>
public static class TwoPhaseCleanupRunner
{
    public static async Task<IReadOnlyList<Exception>> CalistirAsync(IEnumerable<CleanupAdimi> adimlar)
    {
        var adimListesi = adimlar as IReadOnlyList<CleanupAdimi> ?? adimlar.ToList();
        var hatalar = new List<Exception>();
        var basarisizAdimlar = new List<CleanupAdimi>();

        foreach (var adim in adimListesi)
        {
            try
            {
                await adim.Calistir();
            }
            catch (Exception ex)
            {
                basarisizAdimlar.Add(adim);
                hatalar.Add(new InvalidOperationException($"[1. gecis] {adim.Ad}: {ex.GetType().Name} - {ex.Message}", ex));
            }
        }

        foreach (var adim in basarisizAdimlar)
        {
            // 1. gecisteki hata KAYITTAN CIKARILIR - 2. gecisin SONUCU (basarili ya da kalici hata)
            // NIHAI durumu belirler, ikisi asla AYNI ANDA listede kalmaz.
            hatalar.RemoveAll(h => h.Message.StartsWith($"[1. gecis] {adim.Ad}:", StringComparison.Ordinal));

            try
            {
                await adim.Calistir();
            }
            catch (Exception ex)
            {
                hatalar.Add(new InvalidOperationException($"[2. gecis - kalici hata] {adim.Ad}: {ex.GetType().Name} - {ex.Message}", ex));
            }
        }

        return hatalar;
    }
}
