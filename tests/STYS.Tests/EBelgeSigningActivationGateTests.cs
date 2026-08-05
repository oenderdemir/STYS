using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using STYS.Muhasebe.SatisBelgeleri.Services;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.7 görev md.18 - aktivasyon kapısının Enabled bayrağını, Europe/Istanbul tarih
/// kapısını ve fail-closed config davranışını test eder. TAMAMEN `TimeProvider` ile
/// sabitlenir - `DateTime.Now`/`UtcNow` KULLANILMAZ, DB/sidecar GEREKMEZ.
/// </summary>
public class EBelgeSigningActivationGateTests
{
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _zaman;
        public FixedTimeProvider(DateTimeOffset zaman) => _zaman = zaman;
        public override DateTimeOffset GetUtcNow() => _zaman;
    }

    private static EBelgeSigningActivationGate CreateGate(EBelgeSigningOptions options, DateTimeOffset nowUtc)
        => new(Options.Create(options), new FixedTimeProvider(nowUtc), NullLogger<EBelgeSigningActivationGate>.Instance);

    [Fact]
    public void EnabledFalseIkenMesajOlusturulmaz()
    {
        var gate = CreateGate(
            new EBelgeSigningOptions { Enabled = false, NotBeforeLocalDate = "2026-09-15" },
            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.False(gate.ShouldCreateSigningMessage());
    }

    [Fact]
    public void OnDortEyluldeMesajOlusturulmaz()
    {
        // 14 Eylül 2026 23:59 Europe/Istanbul (UTC+3) = 13 Eylül 2026 20:59 UTC - tarih kapısı
        // HENÜZ geçilmemiş olmalı (bkz. görev md.25 senaryo 52).
        var istanbul14EylulSonu = new DateTimeOffset(2026, 9, 14, 23, 59, 0, TimeSpan.FromHours(3));
        var gate = CreateGate(
            new EBelgeSigningOptions { Enabled = true, NotBeforeLocalDate = "2026-09-15" },
            istanbul14EylulSonu.ToUniversalTime());

        Assert.False(gate.ShouldCreateSigningMessage());
    }

    [Fact]
    public void OnBesEylulYerelGunBaslangicindaVeSonrasindaMesajOlusturulur()
    {
        // 2026-09-15 00:00:00 Europe/Istanbul (UTC+3) = 2026-09-14T21:00:00Z (bkz. görev md.25 senaryo 53).
        var istanbul15EylulBaslangic = new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.FromHours(3));
        var gateTamAnda = CreateGate(
            new EBelgeSigningOptions { Enabled = true, NotBeforeLocalDate = "2026-09-15" },
            istanbul15EylulBaslangic.ToUniversalTime());

        Assert.True(gateTamAnda.ShouldCreateSigningMessage());

        var birSaatSonra = istanbul15EylulBaslangic.AddHours(1);
        var gateSonra = CreateGate(
            new EBelgeSigningOptions { Enabled = true, NotBeforeLocalDate = "2026-09-15" },
            birSaatSonra.ToUniversalTime());

        Assert.True(gateSonra.ShouldCreateSigningMessage());
    }

    [Fact]
    public void ServerUtcVeYerelZamanFarkiKapiyiDegistirmez()
    {
        // Aynı MUTLAK an, iki FARKLI ama eşdeğer UTC temsili ile - kapı kararı AYNI kalmalıdır
        // (bkz. görev md.25 senaryo 54 - "server UTC/local timezone farkı kapıyı değiştirmez").
        var mutlakAn = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var options = new EBelgeSigningOptions { Enabled = true, NotBeforeLocalDate = "2026-09-15" };

        var gate1 = CreateGate(options, mutlakAn);
        var gate2 = CreateGate(options, mutlakAn.ToOffset(TimeSpan.FromHours(5)));

        Assert.Equal(gate1.ShouldCreateSigningMessage(), gate2.ShouldCreateSigningMessage());
        Assert.True(gate1.ShouldCreateSigningMessage());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("gecersiz-tarih")]
    [InlineData("2026/09/15")]
    [InlineData("15-09-2026")]
    public void GecersizNotBeforeLocalDateFailClosedOlur(string? gecersizTarih)
    {
        var gate = CreateGate(
            new EBelgeSigningOptions { Enabled = true, NotBeforeLocalDate = gecersizTarih },
            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.False(gate.ShouldCreateSigningMessage());
    }

    [Fact]
    public void GelecekTarihliNotBeforeIleHenuzMesajOlusturulmaz()
    {
        var gate = CreateGate(
            new EBelgeSigningOptions { Enabled = true, NotBeforeLocalDate = "2030-01-01" },
            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.False(gate.ShouldCreateSigningMessage());
    }
}
