using Microsoft.EntityFrameworkCore;
using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Entegrasyonlar.Pos.Services;
using STYS.Infrastructure.EntityFramework;
using STYS.Iller.Entities;
using STYS.Kurumlar.Entities;
using STYS.Tests.TestSupport;
using STYS.Tesisler.Entities;
using Xunit;

namespace STYS.Tests;

public sealed class PosTerminalServiceTests
{
    [Fact]
    public async Task KaydetAsync_FingerprintAlaniniZorunluTutmaz_veMevcutDegeriKorur()
    {
        var dbName = $"stys-pos-terminal-service-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var db = new StysAppDbContext(options, currentTenantAccessor: new SuperTenantAccessor());
        var (device, existingTerminal) = await SeedAsync(db);

        var service = new PosTerminalService(db, [new FakePavoSaglayici()], new SuperTenantAccessor());

        var saved = await service.KaydetAsync(device.Id, existingTerminal.Id, new PosTerminalKaydetRequest
        {
            PosCihaziId = device.Id,
            TesisId = 0,
            KasaBankaHesapId = null,
            SaglayiciKodu = "PAVO",
            Ad = "Güncellenmiş Terminal",
            TerminalId = existingTerminal.SerialNumber,
            MerchantId = null,
            SerialNumber = existingTerminal.SerialNumber,
            SourceFingerprint = null,
            SourceTerminalReference = null,
            AktifMi = true
        }, CancellationToken.None);

        Assert.Equal("FP-OLD", saved.SourceFingerprint);
        Assert.Equal("REF-OLD", saved.SourceTerminalReference);
        Assert.Equal(device.TesisId, saved.TesisId);

        var reloaded = await db.PosTerminaller.AsNoTracking().SingleAsync(x => x.Id == existingTerminal.Id);
        Assert.Equal("FP-OLD", reloaded.SourceFingerprint);
        Assert.Equal("REF-OLD", reloaded.SourceTerminalReference);
    }

    [Fact]
    public async Task KaydetAsync_SourceFingerprintOlmadanYeniTerminalKaydedilebilir()
    {
        var dbName = $"stys-pos-terminal-service-new-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var db = new StysAppDbContext(options, currentTenantAccessor: new SuperTenantAccessor());
        var (device, _) = await SeedAsync(db);

        var service = new PosTerminalService(db, [new FakePavoSaglayici()], new SuperTenantAccessor());

        var saved = await service.KaydetAsync(device.Id, null, new PosTerminalKaydetRequest
        {
            PosCihaziId = device.Id,
            TesisId = 0,
            KasaBankaHesapId = null,
            SaglayiciKodu = "PAVO",
            Ad = "Yeni Terminal",
            TerminalId = "TERM-NEW",
            MerchantId = "MER-NEW",
            SerialNumber = "TERM-NEW",
            SourceFingerprint = null,
            SourceTerminalReference = null,
            AktifMi = true
        }, CancellationToken.None);

        Assert.Null(saved.SourceFingerprint);
        Assert.Equal("MER-NEW", saved.SourceTerminalReference);
        Assert.Equal(device.TesisId, saved.TesisId);
    }

    private static async Task<(PosCihazi device, PosTerminal terminal)> SeedAsync(StysAppDbContext db)
    {
        var il = new Il { Ad = "Il-1", AktifMi = true };
        var kurum = new Kurum { Kod = "KRM-1", Ad = "Kurum-1", AktifMi = true };
        db.Add(il);
        db.Add(kurum);
        await db.SaveChangesAsync();

        var tesis = new Tesis
        {
            Ad = "Tesis-1",
            KurumId = kurum.Id,
            IlId = il.Id,
            Telefon = "000",
            Adres = "Adres",
            AktifMi = true
        };
        db.Add(tesis);
        await db.SaveChangesAsync();

        var device = new PosCihazi
        {
            KurumId = kurum.Id,
            TesisId = tesis.Id,
            Saglayici = PosSaglayici.Pavo,
            Ad = "Pavo Device",
            SeriNo = "SER-1",
            AktifMi = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };
        db.Add(device);
        await db.SaveChangesAsync();

        var terminal = new PosTerminal
        {
            KurumId = kurum.Id,
            TesisId = tesis.Id,
            PosCihaziId = device.Id,
            KasaBankaHesapId = null,
            SaglayiciKodu = "PAVO",
            CanonicalAcquirerId = "10",
            CanonicalTerminalId = "TERM-1",
            Ad = "Eski Terminal",
            SerialNumber = "TERM-1",
            SourceFingerprint = "FP-OLD",
            SourceTerminalReference = "REF-OLD",
            AktifMi = true,
            EslesmeOnayliMi = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };
        db.Add(terminal);
        await db.SaveChangesAsync();

        return (device, terminal);
    }

    private sealed class FakePavoSaglayici : IPosOdemeSaglayicisi
    {
        public string Kod => "PAVO";
        public string Ad => "PAVO";
        public bool EslesmeDestekliyorMu => true;

        public void TerminalBilgileriniDogrula(PosTerminal terminal)
        {
        }

        public Task<PosEslesmeSonucu> EslesmeBaslatAsync(PosTerminal terminal, CancellationToken cancellationToken) =>
            Task.FromResult(new PosEslesmeSonucu(1, "PAIR-1", "TARGET-1", true));

        public Task<PosEslesmeSonucu> EslesmeKontrolAsync(PosTerminal terminal, CancellationToken cancellationToken) =>
            Task.FromResult(new PosEslesmeSonucu(1, "PAIR-1", "TARGET-1", true));

        public Task<PosOdemeBaslatSonucu> OdemeBaslatAsync(PosTerminal terminal, string islemReferansi, decimal tutar, string paraBirimi, CancellationToken cancellationToken) =>
            Task.FromResult(new PosOdemeBaslatSonucu("PAY-1", "OK", "{}"));

        public Task<PosOdemeSorguSonucu> OdemeDurumuAsync(PosTerminal terminal, string saglayiciIslemId, string islemReferansi, CancellationToken cancellationToken) =>
            Task.FromResult(new PosOdemeSorguSonucu("OK", false, true, "{}", null, null, null, null));
    }
}
