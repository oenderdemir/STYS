using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace STYS.Agent.Client.Authentication;

public sealed class FileAgentCredentialStore : IAgentCredentialStore
{
    private readonly string _storePath;
    private readonly ILogger<FileAgentCredentialStore> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public FileAgentCredentialStore(ILogger<FileAgentCredentialStore> logger)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var directory = Path.Combine(appData, "STYS", "Agent");
        Directory.CreateDirectory(directory);
        _storePath = Path.Combine(directory, "credential.dat");
        _logger = logger;
    }

    public async Task<AgentLocalCredential?> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_storePath))
                return null;

            var encrypted = await File.ReadAllBytesAsync(_storePath, cancellationToken);
            var plainBytes = Unprotect(encrypted);
            if (plainBytes is null)
                return null;

            var json = Encoding.UTF8.GetString(plainBytes);
            return JsonSerializer.Deserialize<AgentLocalCredential>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Credential read failed.");
            return null;
        }
    }

    public async Task SaveAsync(AgentLocalCredential credential, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(credential, JsonOptions);
        var plainBytes = Encoding.UTF8.GetBytes(json);
        var encrypted = Protect(plainBytes);

        await File.WriteAllBytesAsync(_storePath, encrypted, cancellationToken);
        _logger.LogInformation("Credential saved securely.");
    }

    public Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(_storePath))
            File.Delete(_storePath);

        return Task.CompletedTask;
    }

    private static byte[] Protect(byte[] data)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

        return data;
    }

    private static byte[]? Unprotect(byte[] data)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);

            return data;
        }
        catch
        {
            return null;
        }
    }
}
