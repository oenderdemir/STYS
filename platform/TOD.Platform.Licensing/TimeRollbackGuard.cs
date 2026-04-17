using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TOD.Platform.Licensing.Abstractions;

namespace TOD.Platform.Licensing;

/// <summary>
/// Sistem saatinin geri alınmasını tespit eder.
///
/// Çalışma mantığı:
/// 1. Her başarılı doğrulamada mevcut UTC zaman bir state dosyasına şifreli olarak yazılır.
/// 2. Sonraki doğrulamada mevcut zaman, kayıtlı zamandan eski ise "rollback" tespit edilir.
/// 3. State dosyası AES ile şifrelenir. Anahtar, machine-specific veriden türetilir.
///    Bu sayede dosya başka bir makineye kopyalansa bile çözülemez.
///
/// Platform farkları:
/// - Windows: Machine name + OS description + user domain → key derivation
/// - Linux/Container: Machine name + OS description + /etc/machine-id (varsa) → key derivation
///   Container'larda /etc/machine-id olmayabilir, bu durumda hostname kullanılır.
///   Persistent volume kullanılmıyorsa state her restart'ta sıfırlanır — bu kabul edilebilir
///   çünkü container restart'ı yeni bir "başlangıç" sayılır.
/// </summary>
public sealed class TimeRollbackGuard : ITimeRollbackGuard
{
    private readonly string _statePath;
    private readonly byte[] _encryptionKey;

    // Tolerans: Küçük saat kaymaları (NTP sync vb.) için 2 dakika tolerans
    private static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(2);

    public TimeRollbackGuard(IOptions<LicensingOptions> options)
    {
        _statePath = options.Value.TimeGuardStatePath;
        _encryptionKey = DeriveEncryptionKey();
    }

    public bool IsTimeRolledBack()
    {
        var lastRecordedTime = ReadLastRecordedTime();
        if (lastRecordedTime is null)
            return false; // İlk çalıştırma, state yok

        var now = DateTimeOffset.UtcNow;
        // Mevcut zaman, kayıtlı zamandan tolerans kadar eski ise rollback var
        return now < lastRecordedTime.Value - Tolerance;
    }

    public void RecordCurrentTime()
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var plaintext = Encoding.UTF8.GetBytes(now.ToString("O"));
            var encrypted = Encrypt(plaintext);
            File.WriteAllBytes(_statePath, encrypted);
        }
        catch
        {
            // State yazılamazsa sessizce devam et — bir sonraki check'te tekrar dener
        }
    }

    private DateTimeOffset? ReadLastRecordedTime()
    {
        try
        {
            if (!File.Exists(_statePath))
                return null;

            var encrypted = File.ReadAllBytes(_statePath);
            var plaintext = Decrypt(encrypted);
            var text = Encoding.UTF8.GetString(plaintext);
            return DateTimeOffset.Parse(text);
        }
        catch
        {
            return null; // Dosya bozuksa veya decrypt edilemezse → ilk çalıştırma gibi davran
        }
    }

    private byte[] Encrypt(byte[] plaintext)
    {
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);

        // IV + Ciphertext birleştirilir
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

        // Linux'ta /etc/machine-id varsa ekle
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

        // SHA256 ile 32-byte (256-bit) anahtar türet
        return SHA256.HashData(Encoding.UTF8.GetBytes(material.ToString()));
    }
}
