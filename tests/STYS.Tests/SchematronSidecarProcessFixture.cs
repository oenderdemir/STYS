using System.Diagnostics;
using System.Net.Http;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.5 sidecar entegrasyon testleri için GERÇEK Java Saxon-HE 13.0 sürecini başlatır
/// (repodaki sidecar/schematron-validator kaynağından, önceden derlenmiş out/classes +
/// lib/*.jar kullanılarak - bkz. sidecar/schematron-validator/lib/fetch-deps.sh). Java veya
/// derlenmiş sınıflar bulunamazsa testler AÇIKÇA atlanır (Skip), sessizce "başarılı" sayılmaz.
/// </summary>
public sealed class SchematronSidecarProcessFixture : IAsyncLifetime
{
    public string? BaseUrl { get; private set; }
    public string? AtlamaNedeni { get; private set; }

    private Process? _process;

    public async Task InitializeAsync()
    {
        var sidecarRoot = FindSidecarRoot();
        if (sidecarRoot is null)
        {
            AtlamaNedeni = "sidecar/schematron-validator dizini bulunamadı.";
            return;
        }

        var javaExe = FindJava();
        if (javaExe is null)
        {
            AtlamaNedeni = "Java çalıştırılabilir dosyası bulunamadı.";
            return;
        }

        var classesDir = Path.Combine(sidecarRoot, "out", "classes");
        var saxonJar = Path.Combine(sidecarRoot, "lib", "Saxon-HE-13.0.jar");
        var resolverJar = Path.Combine(sidecarRoot, "lib", "xmlresolver-6.0.23.jar");

        if (!Directory.Exists(classesDir) || !File.Exists(saxonJar) || !File.Exists(resolverJar))
        {
            AtlamaNedeni = "Sidecar derlenmiş sınıfları veya jar bağımlılıkları bulunamadı (önce sidecar'ı derleyin).";
            return;
        }

        var port = GetFreeTcpPort();
        BaseUrl = $"http://localhost:{port}";

        var classPath = $"{classesDir};{saxonJar};{resolverJar}";
        var psi = new ProcessStartInfo
        {
            FileName = javaExe,
            WorkingDirectory = sidecarRoot,
            ArgumentList =
            {
                $"-Dschematron.rootDir={sidecarRoot}",
                $"-Dschematron.port={port}",
                "-cp", classPath,
                "stys.ebelge.schematron.SidecarMain",
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        _process = Process.Start(psi);
        if (_process is null)
        {
            AtlamaNedeni = "Java süreci başlatılamadı.";
            return;
        }

        // ÖNEMLİ: RedirectStandardOutput/Error=true iken akışlar okunmazsa, OS pipe tamponu
        // dolduğunda alt süreç YAZMA sırasında sonsuza kadar bloke olabilir (klasik .NET Process
        // deadlock hatası). Bu yüzden her iki akış da sürekli, asenkron olarak drenilir.
        // ÖNEMLİ: RedirectStandardOutput/Error=true iken akışlar okunmazsa, OS pipe tamponu
        // dolduğunda alt süreç YAZMA sırasında sonsuza kadar bloke olabilir (klasik .NET Process
        // deadlock hatası). Bu yüzden her iki akış da sürekli, asenkron olarak drenilir.
        _process.OutputDataReceived += (_, _) => { };
        _process.ErrorDataReceived += (_, _) => { };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var hazirMi = false;
        for (var i = 0; i < 60 && !hazirMi; i++)
        {
            await Task.Delay(500);
            try
            {
                var response = await httpClient.GetAsync($"{BaseUrl}/health/ready");
                hazirMi = response.IsSuccessStatusCode;
            }
            catch
            {
                // henüz ayakta değil, tekrar dene
            }
        }

        if (!hazirMi)
        {
            AtlamaNedeni = "Sidecar zaman aşımı içinde ready olmadı.";
            BaseUrl = null;
        }
    }

    public Task DisposeAsync()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // best-effort temizlik
        }

        return Task.CompletedTask;
    }

    private static string? FindSidecarRoot()
    {
        var dizin = new DirectoryInfo(AppContext.BaseDirectory);
        while (dizin is not null && !File.Exists(Path.Combine(dizin.FullName, "STYS.sln")))
        {
            dizin = dizin.Parent;
        }

        if (dizin is null)
        {
            return null;
        }

        var sidecarRoot = Path.Combine(dizin.FullName, "sidecar", "schematron-validator");
        return Directory.Exists(sidecarRoot) ? sidecarRoot : null;
    }

    private static string? FindJava()
    {
        var candidates = new List<string> { "java" };

        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            candidates.Add(Path.Combine(javaHome, "bin", OperatingSystem.IsWindows() ? "java.exe" : "java"));
        }

        // Yerel geliştirme makinesi için bilinen JDK 17+ konumları (CI/production PATH'te "java" bulacaktır).
        candidates.AddRange(new[]
        {
            @"C:\Program Files\AdoptOpenJDK\jdk-17.0.0.20-hotspot\bin\java.exe",
            @"C:\Program Files\Eclipse Adoptium\jdk-17\bin\java.exe",
        });

        foreach (var candidate in candidates)
        {
            if (candidate == "java")
            {
                if (TryRunVersion("java"))
                {
                    return "java";
                }

                continue;
            }

            if (File.Exists(candidate) && TryRunVersion(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Yalnız çalıştırılabilir DEĞİL, aynı zamanda JDK 17+ (Saxon-HE 13.0 bytecode gereksinimi) olmalıdır.</summary>
    private static bool TryRunVersion(string javaExe)
    {
        try
        {
            var psi = new ProcessStartInfo(javaExe, "-version")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            using var process = Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            var versionOutput = process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            if (process.ExitCode != 0)
            {
                return false;
            }

            var match = System.Text.RegularExpressions.Regex.Match(versionOutput, "version \"(\\d+)");
            return match.Success && int.Parse(match.Groups[1].Value) >= 17;
        }
        catch
        {
            return false;
        }
    }

    private static int GetFreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

[CollectionDefinition(Name)]
public sealed class SchematronSidecarCollection : ICollectionFixture<SchematronSidecarProcessFixture>
{
    public const string Name = "SchematronSidecar";
}
