namespace STYS.Tests;

/// <summary>Testler için repo kökünden GİB kural seti dizinini bulur (AppContext.BaseDirectory'den yukarı doğru STYS.sln arar).</summary>
internal static class EBelgeUblKuralSetiTestYardimcisi
{
    public static string KuralSetiKokDizin()
    {
        var dizin = new DirectoryInfo(AppContext.BaseDirectory);
        while (dizin is not null && !File.Exists(Path.Combine(dizin.FullName, "STYS.sln")))
        {
            dizin = dizin.Parent;
        }

        if (dizin is null)
        {
            throw new InvalidOperationException("Repo kökü (STYS.sln) bulunamadı.");
        }

        return Path.Combine(dizin.FullName, "backend", "Muhasebe", "SatisBelgeleri", "EBelgeUblKuralSeti");
    }
}
