using System.Text.Json;
using TOD.Platform.Licensing.Abstractions;

namespace Tod.LicenseGenerator;

internal sealed class LicenseGeneratorForm : Form
{
    private readonly TextBox _productCode = new() { Text = "STYS" };
    private readonly TextBox _customerCode = new();
    private readonly TextBox _customerName = new();
    private readonly TextBox _environmentName = new() { Text = "Development" };
    private readonly TextBox _instanceId = new() { Text = "dev-01" };
    private readonly TextBox _deploymentMarker = new();
    private readonly TextBox _modules = new();
    private readonly ComboBox _profile = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DateTimePicker _expiresAt = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm" };
    private readonly TextBox _fingerprint = new() { ReadOnly = true };
    private readonly TextBox _outputPath = new() { ReadOnly = true };
    private readonly TextBox _status = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };

    public LicenseGeneratorForm()
    {
        Text = "TOD License Generator";
        Width = 900;
        Height = 720;
        StartPosition = FormStartPosition.CenterScreen;

        _expiresAt.Value = DateTime.Now.AddYears(1);
        _profile.Items.AddRange([FingerprintProfile.PhysicalServer, FingerprintProfile.Container]);
        _profile.SelectedItem = FingerprintProfile.PhysicalServer;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 13,
            Padding = new Padding(12),
            AutoScroll = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(layout, "Urun Kodu", _productCode, 0);
        AddRow(layout, "Musteri Kodu", _customerCode, 1);
        AddRow(layout, "Musteri Adi", _customerName, 2);
        AddRow(layout, "Ortam Adi", _environmentName, 3);
        AddRow(layout, "Instance Id", _instanceId, 4);
        AddRow(layout, "Fingerprint Profili", _profile, 5);
        AddRow(layout, "Deployment Marker", _deploymentMarker, 6);
        AddRow(layout, "Moduller (virgul)", _modules, 7);
        AddRow(layout, "Bitis Tarihi", _expiresAt, 8);
        AddRow(layout, "Fingerprint", _fingerprint, 9);
        AddRow(layout, "Cikti Dosyasi", _outputPath, 10);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        var btnFingerprint = new Button { Text = "Fingerprint Hesapla", Width = 150, Height = 32 };
        var btnGenerate = new Button { Text = "Lisans Uret", Width = 120, Height = 32 };
        var btnShowPublicKey = new Button { Text = "Public Key Goster", Width = 130, Height = 32 };
        buttonPanel.Controls.Add(btnFingerprint);
        buttonPanel.Controls.Add(btnGenerate);
        buttonPanel.Controls.Add(btnShowPublicKey);
        AddRow(layout, "Islemler", buttonPanel, 11);

        _status.Height = 180;
        AddRow(layout, "Durum", _status, 12);

        Controls.Add(layout);

        btnFingerprint.Click += (_, _) => ComputeFingerprint();
        btnGenerate.Click += async (_, _) => await GenerateLicenseAsync();
        btnShowPublicKey.Click += (_, _) => ShowPublicKey();
    }

    private static void AddRow(TableLayoutPanel layout, string label, Control control, int row)
    {
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(3, 3, 3, 8);
        var lbl = new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(3, 8, 3, 8)
        };

        layout.Controls.Add(lbl, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private void ComputeFingerprint()
    {
        try
        {
            var profile = (FingerprintProfile)(_profile.SelectedItem ?? FingerprintProfile.PhysicalServer);
            _fingerprint.Text = LicenseGeneratorCore.ComputeFingerprintHash(
                profile,
                _environmentName.Text.Trim(),
                _instanceId.Text.Trim(),
                _customerCode.Text.Trim(),
                _deploymentMarker.Text.Trim());
            Log("Fingerprint hesaplandi.");
        }
        catch (Exception ex)
        {
            Log($"Fingerprint hesaplarken hata: {ex.Message}");
        }
    }

    private async Task GenerateLicenseAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_customerCode.Text))
            {
                MessageBox.Show("Musteri kodu zorunludur.", "Uyari", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var profile = (FingerprintProfile)(_profile.SelectedItem ?? FingerprintProfile.PhysicalServer);
            var license = new LicenseDocument
            {
                LicenseId = Guid.NewGuid().ToString("D"),
                ProductCode = _productCode.Text.Trim(),
                CustomerCode = _customerCode.Text.Trim(),
                CustomerName = _customerName.Text.Trim(),
                EnvironmentName = _environmentName.Text.Trim(),
                InstanceId = _instanceId.Text.Trim(),
                IssuedAtUtc = DateTimeOffset.UtcNow,
                ExpiresAtUtc = new DateTimeOffset(_expiresAt.Value).ToUniversalTime(),
                EnabledModules = ParseModules(_modules.Text),
                LicenseVersion = 1
            };

            license.FingerprintHash = LicenseGeneratorCore.ComputeFingerprintHash(
                profile,
                license.EnvironmentName,
                license.InstanceId,
                license.CustomerCode,
                _deploymentMarker.Text.Trim());

            _fingerprint.Text = license.FingerprintHash;

            using var ecdsa = await LicenseGeneratorCore.EnsureKeysExistAsync();
            var payload = LicenseGeneratorCore.BuildCanonicalPayload(license);
            license.Signature = Convert.ToBase64String(ecdsa.SignData(payload, System.Security.Cryptography.HashAlgorithmName.SHA256));

            var defaultName = $"license-{license.ProductCode}-{license.CustomerCode}.json".ToLowerInvariant();
            using var save = new SaveFileDialog
            {
                Filter = "Json files (*.json)|*.json|All files (*.*)|*.*",
                FileName = defaultName,
                Title = "Lisansi Kaydet"
            };

            if (save.ShowDialog(this) != DialogResult.OK)
            {
                Log("Kaydetme iptal edildi.");
                return;
            }

            var json = JsonSerializer.Serialize(license, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(save.FileName, json);

            _outputPath.Text = save.FileName;
            Log($"Lisans olusturuldu: {save.FileName}");
        }
        catch (Exception ex)
        {
            Log($"Lisans olustururken hata: {ex.Message}");
            MessageBox.Show(ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowPublicKey()
    {
        try
        {
            if (!File.Exists(LicenseGeneratorCore.PublicKeyFile))
            {
                MessageBox.Show("Public key bulunamadi. Once lisans uretin.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var publicKeyBase64 = File.ReadAllText(LicenseGeneratorCore.PublicKeyFile).Trim();
            var text = LicenseGeneratorCore.BuildPublicKeyPartsText(publicKeyBase64);

            using var dialog = new Form
            {
                Text = "Public Key Parts",
                Width = 760,
                Height = 420,
                StartPosition = FormStartPosition.CenterParent
            };
            var tb = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10),
                Text = text
            };
            dialog.Controls.Add(tb);
            dialog.ShowDialog(this);
        }
        catch (Exception ex)
        {
            Log($"Public key gosterirken hata: {ex.Message}");
        }
    }

    private static List<string> ParseModules(string text)
        => string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private void Log(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss} - {message}";
        _status.AppendText(line + Environment.NewLine);
    }
}

