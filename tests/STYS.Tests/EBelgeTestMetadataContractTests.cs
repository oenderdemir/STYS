using System.Reflection;
using System.Text.Json;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.9.1 - e-Belge test kümesinin KENDİ trait sözleşmesini (Faz 2B.9'da tanımlanan
/// `Domain`/`TestLevel`/`Dependency`/`CriticalInvariant` taksonomisi) reflection üzerinden
/// otomatik doğrular. Bu sınıf ÖNCEDEN yalnız dokümana yazılmış bir tablo olan kritik invariant
/// manifestini VE trait kurallarını, gerçek derlenmiş assembly'yi tarayarak KANITLAR - bir test
/// veya trait yanlışlıkla silinir/bozulursa bu dosya BAŞARISIZ olur (görev md.19/md.3, "yalnız
/// dokümana yazılmış tablo yeterli değildir").
///
/// Sınıf adında "EBelge" GEÇEN her PUBLIC test sınıfı "e-Belge test sınıfı" sayılır - TEK istisna
/// <see cref="DomainSozlesmesiIstisnalari"/> allowlist'idir (test ALTYAPISI/preflight sınıfları
/// İÇİN, bkz. `EBelgeSqlSidecarPreflightTests` - bu sınıf domain davranışı DEĞİL, dependency
/// erişilebilirliğini test eder, bu yüzden `Domain=EBelge` sözleşmesine TABİ DEĞİLDİR).
/// </summary>
[Trait("Domain", "EBelge")]
[Trait("TestLevel", "Contract")]
public class EBelgeTestMetadataContractTests
{
    private static readonly string[] KnownTestLevels =
    [
        "Unit", "Contract", "SqlIntegration", "SidecarIntegration", "CryptoIntegration", "WorkerEndToEnd", "ReleaseGate",
    ];

    private static readonly string[] KnownDependencies = ["SqlServer", "JavaSidecar", "Cryptography"];

    /// <summary>Sınıf adında "EBelge" geçtiği halde domain trait sözleşmesine TABİ OLMAYAN, açık allowlist.</summary>
    private static readonly string[] DomainSozlesmesiIstisnalari = ["EBelgeSqlSidecarPreflightTests"];

    /// <summary>Bilinçli olarak birden fazla teste uygulanması KABUL EDİLEN CriticalInvariant değerleri (şu an boş - yeni bir tane gerekiyorsa buraya gerekçesiyle eklenir).</summary>
    private static readonly string[] KasitliCriticalInvariantTekrarlari = [];

    private static List<(Type Type, MethodInfo Method)> TumEBelgeTestMetodlari()
    {
        var assembly = typeof(EBelgeTestMetadataContractTests).Assembly;
        var sonuc = new List<(Type, MethodInfo)>();

        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract || !type.IsPublic)
            {
                continue;
            }

            if (!type.Name.Contains("EBelge", StringComparison.Ordinal))
            {
                continue;
            }

            if (DomainSozlesmesiIstisnalari.Contains(type.Name))
            {
                continue;
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.GetCustomAttributes(inherit: true).Any(a => a is FactAttribute))
                {
                    sonuc.Add((type, method));
                }
            }
        }

        return sonuc;
    }

    /// <summary>
    /// xUnit v2'nin `TraitAttribute`'ı Name/Value'yu PUBLIC property olarak DEĞİL, yalnız kendi
    /// `ITraitDiscoverer` altyapısı üzerinden ifşa eder - bu yüzden burada attribute NESNESİ
    /// OLUŞTURULMAZ, yalnız `CustomAttributeData` (ham metadata) üzerinden constructor argümanları
    /// OKUNUR - hem sınıf hem metod düzeyi için.
    /// </summary>
    private static List<string> TraitDegerleri(Type type, MethodInfo method, string traitAdi)
    {
        var degerler = new List<string>();
        foreach (var data in type.GetCustomAttributesData().Concat(method.GetCustomAttributesData()))
        {
            if (data.AttributeType != typeof(TraitAttribute) || data.ConstructorArguments.Count != 2)
            {
                continue;
            }

            var name = (string?)data.ConstructorArguments[0].Value;
            var value = (string?)data.ConstructorArguments[1].Value;
            if (name == traitAdi && value is not null)
            {
                degerler.Add(value);
            }
        }

        return degerler;
    }

    [Fact]
    public void TumEBelgeTestleriDomainEBelgeTasirVeTamOlarakBirGecerliTestLevelTasir()
    {
        var ihlaller = new List<string>();
        foreach (var (type, method) in TumEBelgeTestMetodlari())
        {
            var isim = $"{type.FullName}.{method.Name}";
            var domain = TraitDegerleri(type, method, "Domain");
            if (!domain.Contains("EBelge"))
            {
                ihlaller.Add($"{isim}: Domain=EBelge eksik (bulunan: [{string.Join(",", domain)}])");
                continue;
            }

            var testLevel = TraitDegerleri(type, method, "TestLevel");
            if (testLevel.Count != 1)
            {
                ihlaller.Add($"{isim}: tam olarak 1 TestLevel beklenir, bulunan {testLevel.Count} ([{string.Join(",", testLevel)}])");
                continue;
            }

            if (!KnownTestLevels.Contains(testLevel[0]))
            {
                ihlaller.Add($"{isim}: bilinmeyen TestLevel '{testLevel[0]}'");
            }
        }

        Assert.True(ihlaller.Count == 0, "Trait sozlesmesi ihlalleri:\n" + string.Join("\n", ihlaller));
    }

    [Fact]
    public void DependencyDegerleriYalnizWhitelistIcindedir()
    {
        var ihlaller = new List<string>();
        foreach (var (type, method) in TumEBelgeTestMetodlari())
        {
            foreach (var dep in TraitDegerleri(type, method, "Dependency"))
            {
                if (!KnownDependencies.Contains(dep))
                {
                    ihlaller.Add($"{type.FullName}.{method.Name}: bilinmeyen Dependency '{dep}'");
                }
            }
        }

        Assert.True(ihlaller.Count == 0, "Dependency ihlalleri:\n" + string.Join("\n", ihlaller));
    }

    [Fact]
    public void CriticalInvariantDegerleriYalnizWhitelistIcindedirVeKasitsizTekrarEtmez()
    {
        var kullanimlar = new Dictionary<string, List<string>>();
        var bilinmeyenler = new List<string>();

        foreach (var (type, method) in TumEBelgeTestMetodlari())
        {
            foreach (var inv in TraitDegerleri(type, method, "CriticalInvariant"))
            {
                if (!EBelgeCriticalInvariantManifest.KnownInvariants.Contains(inv))
                {
                    bilinmeyenler.Add($"{type.FullName}.{method.Name}: bilinmeyen CriticalInvariant '{inv}'");
                    continue;
                }

                if (!kullanimlar.TryGetValue(inv, out var liste))
                {
                    liste = [];
                    kullanimlar[inv] = liste;
                }

                liste.Add($"{type.FullName}.{method.Name}");
            }
        }

        Assert.True(bilinmeyenler.Count == 0, "Bilinmeyen CriticalInvariant degerleri:\n" + string.Join("\n", bilinmeyenler));

        var kasitsizTekrarlar = kullanimlar
            .Where(kv => kv.Value.Count > 1 && !KasitliCriticalInvariantTekrarlari.Contains(kv.Key))
            .Select(kv => $"{kv.Key}: {string.Join(", ", kv.Value)}")
            .ToList();
        Assert.True(kasitsizTekrarlar.Count == 0,
            "Kasitsiz CriticalInvariant tekrarlari (KasitliCriticalInvariantTekrarlari allowlist'ine gerekceyle eklenmeli veya duzeltilmeli):\n" + string.Join("\n", kasitsizTekrarlar));
    }

    public static IEnumerable<object[]> BilinenInvariantler() =>
        EBelgeCriticalInvariantManifest.KnownInvariants.Select(i => new object[] { i });

    [Theory]
    [MemberData(nameof(BilinenInvariantler))]
    public void KritikInvariantManifestindekiHerBirIcinEnAzBirGecerliTestBulunur(string invariant)
    {
        var eslesenler = TumEBelgeTestMetodlari()
            .Where(tm => TraitDegerleri(tm.Type, tm.Method, "CriticalInvariant").Contains(invariant))
            .ToList();

        Assert.True(eslesenler.Count >= 1, $"CriticalInvariant='{invariant}' icin hicbir test bulunamadi.");

        foreach (var (type, method) in eslesenler)
        {
            Assert.Contains("EBelge", TraitDegerleri(type, method, "Domain"));
            var testLevel = TraitDegerleri(type, method, "TestLevel");
            Assert.True(testLevel.Count == 1 && KnownTestLevels.Contains(testLevel[0]),
                $"{type.FullName}.{method.Name}: CriticalInvariant testi gecerli TEK bir TestLevel tasimalidir (bulunan: [{string.Join(",", testLevel)}]).");
        }
    }

    public static IEnumerable<object[]> BilinenTestLevelleri() => KnownTestLevels.Select(l => new object[] { l });

    [Theory]
    [MemberData(nameof(BilinenTestLevelleri))]
    public void HerBilinenTestLevelIcinEnAzBirGercekTestVardir(string testLevel)
    {
        var sayi = TumEBelgeTestMetodlari().Count(tm => TraitDegerleri(tm.Type, tm.Method, "TestLevel").Contains(testLevel));
        Assert.True(sayi >= 1, $"TestLevel='{testLevel}' icin hicbir test bulunamadi.");
    }

    [Fact]
    public void ReleaseProfiliTumKritikInvariantlariKapsar()
    {
        // release profili "Domain=EBelge" filtresiyle TestLevel'dan BAGIMSIZ TUMUNU secer.
        // Yukaridaki testler her kritik invariant testinin Domain=EBelge + gecerli TEK bir
        // TestLevel tasidigini ZATEN dogruladigindan, release filtresi bunlarin TAMAMINI
        // OTOMATIK kapsar - bu test bu ILISKIYI ACIKCA, tek bir yerde dogrular (bkz. gorev md.3
        // kural 5, "Release profili kritik testlerin tamamini secer").
        foreach (var invariant in EBelgeCriticalInvariantManifest.KnownInvariants)
        {
            var eslesenler = TumEBelgeTestMetodlari()
                .Where(tm => TraitDegerleri(tm.Type, tm.Method, "CriticalInvariant").Contains(invariant))
                .ToList();
            Assert.True(eslesenler.Count >= 1, $"CriticalInvariant='{invariant}' testi bulunamadi - release profili bunu KAPSAYAMAZ.");

            foreach (var (type, method) in eslesenler)
            {
                Assert.Contains("EBelge", TraitDegerleri(type, method, "Domain"));
            }
        }
    }

    [Fact]
    public void ProfilManifestindekiBilinenListelerKodlaSenkrondur()
    {
        // Faz 2B.9.1 gorev md.1/md.3 - "aynı listeyi elle kopyalama" kuralı: JSON manifest'in
        // knownTestLevels/knownDependencies dizileri, BURADAKİ (C# tarafındaki, gerçek reflection
        // dogrulamasinin kullandigi) whitelist ile HER ZAMAN birebir aynı kalmalıdır - biri
        // GUNCELLENIP digeri UNUTULURSA bu test BASARISIZ olur (sessiz drift onlenir).
        var manifestYolu = ManifestDosyaYolunuBul();
        Assert.True(File.Exists(manifestYolu), $"scripts/ebelge-test-profiles.json bulunamadi: {manifestYolu}");

        using var doc = JsonDocument.Parse(File.ReadAllText(manifestYolu));
        var root = doc.RootElement;

        var manifestTestLevels = root.GetProperty("knownTestLevels").EnumerateArray().Select(e => e.GetString()!).ToList();
        var manifestDependencies = root.GetProperty("knownDependencies").EnumerateArray().Select(e => e.GetString()!).ToList();

        Assert.Equal(KnownTestLevels.OrderBy(x => x, StringComparer.Ordinal), manifestTestLevels.OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(KnownDependencies.OrderBy(x => x, StringComparer.Ordinal), manifestDependencies.OrderBy(x => x, StringComparer.Ordinal));
    }

    private static string ManifestDosyaYolunuBul()
    {
        var dizin = new DirectoryInfo(AppContext.BaseDirectory);
        while (dizin is not null && !File.Exists(Path.Combine(dizin.FullName, "STYS.sln")))
        {
            dizin = dizin.Parent;
        }

        return dizin is null
            ? "scripts/ebelge-test-profiles.json"
            : Path.Combine(dizin.FullName, "scripts", "ebelge-test-profiles.json");
    }
}
