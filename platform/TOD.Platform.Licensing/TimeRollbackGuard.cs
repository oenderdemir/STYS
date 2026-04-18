using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TOD.Platform.Licensing.Abstractions;

namespace TOD.Platform.Licensing;

/// <summary>
/// Sistem saatinin geri alinmasini tespit eder.
///
/// Calisma mantigi (cift state dogrulamasi):
/// 1. Her basarili dogrulamada mevcut UTC zaman IKI ayri state dosyasina sifreli olarak yazilir:
///    - Birincil: <see cref="LicensingOptions.TimeGuardStatePath"/>
///    - Mirror: <see cref="LicensingOptions.TimeGuardMirrorPath"/>
///      (bos birakilirsa birincilin yanina gizli bir ".license-state.mirror" yerlestirilir)
/// 2. Sonraki dogrulamada her iki state okunur; herhangi biri rollback gosteriyorsa rollback kabul edilir.
///    Saldirgan tek dosyayi silse/degistirse bile digeri rollback'i yakalar.
/// 3. State dosyalari AES ile sifrelenir. Anahtar machine-specific veriden turetilir;
///    dosyanin baska makineye kopyalanmasi isine yaramaz.
///
/// Platform notlari:
/// - Windows: MachineName + OS + UserDomain
/// - Linux/Container: MachineName + OS + /etc/machine-id (varsa)
/// - Container'larda persistent volume yoksa state restart'ta sifirlanir; bu kabul edilebilir.
/// </summary>
public sealed class TimeRollbackGuard : ITimeRollbackGuard
{
    private readonly string _primaryPath;
    private readonly string _mirrorPath;
    private readonly byte[] _encryptionKey;

    // Tolerans: Kucuk saat kaymalari (NTP sync vb.) icin 2 dakika
    private static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(2);

    public TimeRollbackGuard(IOptions<LicensingOptions> options)
    {
        var opt = options.Value;
        _primaryPath = opt.TimeGuardStatePath;
        _mirrorPath = ResolveMirrorPath(opt.TimeGuardStatePath, opt.TimeGuardMirrorPath);
        _encryptionKey = DeriveEncryptionKey();
    }

    public bool IsTimeRolledBack()
    {
        var now = DateTimeOffset.UtcNow;

        var primary = ReadRecordedTime(_primaryPath);
        var mirror = ReadRecordedTime(_mirrorPath);

        // Her iki state yoksa ilk calistirma
        if (primary is null && mirror is null)
            return false;

        // Herhangi bir state rollback gosteriyorsa rollback kabul edilir.
        // Ek olarak: Iki state birbirinden asiri sapiyorsa (tampering suphesi) yine rollback say.
        if (IsRolledBack(primary, now) || IsRolledBack(mirror, now))
            return true;

        if (primary is not null && mirror is not null)
        {
            var divergence = (primary.Value - mirror.Value).Duration();
            if (divergence > Tolerance * 2)
            {
                // Iki dosya birbirinden asiri ayriliyorsa bir tarafi degismis demektir.
                return true;
            }
        }

        return false;
    }

    public void RecordCurrentTime()
    {
        var now = DateTimeOffset.UtcNow;
        var plaintext = Encoding.UTF8.GetBytes(now.ToString("O"));
        var encrypted = Encrypt(plaintext);

        TryWrite(_primaryPath, encrypted);
        TryWrite(_mirrorPath, encrypted);
    }

    private static bool IsRolledBack(DateTimeOffset? recorded, DateTimeOffset now)
    {
        if (recorded is null)
            return false;
        return now < recorded.Value - Tolerance;
    }

    private static string ResolveMirrorPath(string primaryPath, string configuredMirror)
    {
        if (!string.IsNullOrWhiteSpace(configuredMirror))
            return configuredMirror;

        // Varsayilan: birincilin yanina gizli bir mirror dosyasi
        var dir = Path.GetDirectoryName(primaryPath);
        if (string.IsNullOrWhiteSpace(dir))
            dir = ".";
        return Path.Combine(dir, ".license-state.mirror");
    }

    private static void TryWrite(string path, byte[] data)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(path, data);
        }
        catch
        {
            // State yazilamazsa sessizce devam et; sonraki check'te tekrar denenir.
        }
    }

    private DateTimeOffset? ReadRecordedTime(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var encrypted = File.ReadAllBytes(path);
            var plaintext = Decrypt(encrypted);
            var text = Encoding.UTF8.GetString(plaintext);
            return DateTimeOffset.Parse(text);
        }
        catch
        {
            return null;
        }
    }

    private byte[] Encrypt(byte[] plaintext)
    {
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);

        var result = new byte[aes.IV.Length + ciphertext.Length];
        aes.IV.CopyTo(result, 0);
        ciphertext.CopyTo(result, aes.IV.Length);
        return result;
    }

    private byte[] Decrypt(byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;

        var iv = new byte[aes.BlockSize / 8];
        var ciphertext = new byte[data.Length - iv.Length];

        Array.Copy(data, 0, iv, 0, iv.Length);
        Array.Copy(data, iv.Length, ciphertext, 0, ciphertext.Length);

        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
    }

    private static byte[] DeriveEncryptionKey()
    {
        var material = new StringBuilder();
        material.Append(Environment.MachineName);
        material.Append('|');
        material.Append(System.Runtime.InteropServices.RuntimeInformation.OSDescription);
        material.Append('|');

        if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            try
            {
                if (File.Exists("/etc/machine-id"))
                    material.Append(File.ReadAllText("/etc/machine-id").Trim());
            }
            catch
            {
                // Okunamazsa atla
            }
        }
        else
        {
            material.Append(Environment.UserDomainName);
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(material.ToString()));
    }
}
