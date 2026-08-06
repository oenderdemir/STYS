using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using STYS.Muhasebe.SatisBelgeleri.Services;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.8 görev md.3/md.18 - hosted outbox worker'ın GENEL üretim aktivasyon kapısının Enabled
/// bayrağını, yapılandırılabilir timezone/tarih kapısını ve fail-closed config davranışını test
/// eder. TAMAMEN `TimeProvider` ile sabitlenir - `DateTime.Now`/`UtcNow` KULLANILMAZ, DB/sidecar
/// GEREKMEZ (bkz. `EBelgeSigningActivationGateTests` ile AYNI desen).
/// </summary>
public class EBelgeProcessingActivationGateTests
{
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _zaman;
        public FixedTimeProvider(DateTimeOffset zaman) => _zaman = zaman;
        public override DateTimeOffset GetUtcNow() => _zaman;
    }

    private static EBelgeProcessingActivationGate CreateGate(EBelgeProcessingOptions options, DateTimeOffset nowUtc)
        => new(Options.Create(options), new FixedTimeProvider(nowUtc), NullLogger<EBelgeProcessingActivationGate>.Instance);

    private static EBelgeProcessingOptions DefaultOptions(bool enabled, string? notBeforeLocalDate = "2026-09-15", string timeZoneId = "Europe/Istanbul")
        => new() { Enabled = enabled, NotBeforeLocalDate = notBeforeLocalDate, TimeZoneId = timeZoneId };

    [Fact]
    public void EnabledFalseIkenIslemeYapilmaz()
    {
        var gate = CreateGate(
            DefaultOptions(enabled: false),
            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.False(gate.ShouldProcess());
    }

    [Fact]
    public void OnDortEyluldeIslemeYapilmaz()
    {
        // 14 Eylül 2026 23:59 Europe/Istanbul (UTC+3) = 13 Eylül 2026 20:59 UTC - tarih kapısı
        // HENÜZ geçilmemiş olmalı (bkz. görev md.18 test senaryosu 2).
        var istanbul14EylulSonu = new DateTimeOffset(2026, 9, 14, 23, 59, 0, TimeSpan.FromHours(3));
        var gate = CreateGate(DefaultOptions(enabled: true), istanbul14EylulSonu.ToUniversalTime());

        Assert.False(gate.ShouldProcess());
    }

    [Fact]
    public void OnBesEylulYerelGunBaslangicindaVeSonrasindaIslemeYapilabilir()
    {
        // 2026-09-15 00:00:00 Europe/Istanbul (UTC+3) = 2026-09-14T21:00:00Z (bkz. görev md.18
        // test senaryosu 3).
        var istanbul15EylulBaslangic = new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.FromHours(3));
        var gateTamAnda = CreateGate(DefaultOptions(enabled: true), istanbul15EylulBaslangic.ToUniversalTime());

        Assert.True(gateTamAnda.ShouldProcess());

        var birSaatSonra = istanbul15EylulBaslangic.AddHours(1);
        var gateSonra = CreateGate(DefaultOptions(enabled: true), birSaatSonra.ToUniversalTime());

        Assert.True(gateSonra.ShouldProcess());
    }

    [Fact]
    public void ServerUtcVeYerelZamanFarkiKapiyiDegistirmez()
    {
        // Aynı MUTLAK an, iki FARKLI ama eşdeğer UTC temsili ile - kapı kararı AYNI kalmalıdır
        // (bkz. görev md.18 test senaryosu 4 - "server timezone farklı olsa da sonuç değişmez").
        var mutlakAn = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var options = DefaultOptions(enabled: true);

        var gate1 = CreateGate(options, mutlakAn);
        var gate2 = CreateGate(options, mutlakAn.ToOffset(TimeSpan.FromHours(5)));

        Assert.Equal(gate1.ShouldProcess(), gate2.ShouldProcess());
        Assert.True(gate1.ShouldProcess());
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
            DefaultOptions(enabled: true, notBeforeLocalDate: gecersizTarih),
            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.False(gate.ShouldProcess());
    }

    [Theory]
    [InlineData("Gecersiz/TimeZone-Kimligi")]
    [InlineData("")]
    [InlineData("NotARealZone")]
    public void GecersizTimeZoneIdFailClosedOlur(string gecersizTimeZoneId)
    {
        var gate = CreateGate(
            DefaultOptions(enabled: true, timeZoneId: gecersizTimeZoneId),
            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.False(gate.ShouldProcess());
    }

    [Fact]
    public void GelecekTarihliNotBeforeIleHenuzIslemeYapilmaz()
    {
        var gate = CreateGate(
            DefaultOptions(enabled: true, notBeforeLocalDate: "2030-01-01"),
            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.False(gate.ShouldProcess());
    }

    [Fact]
    public void GateKapaliykenTekrarTekrarCagrilabilirVeHerSeferindeYenidenDegerlendirir()
    {
        // Faz 2B.8 görev md.3 - "Gate kapalıyken belirli aralıklarla config durumunu tekrar
        // kontrol edebilmeli": aynı gate örneği, HER çağrıda güncel `TimeProvider` zamanına göre
        // TEKRAR değerlendirilir - önceki sonucu ÖNBELLEKLEMEZ.
        var mutableTimeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        var gate = new EBelgeProcessingActivationGate(
            Options.Create(DefaultOptions(enabled: true)), mutableTimeProvider, NullLogger<EBelgeProcessingActivationGate>.Instance);

        Assert.False(gate.ShouldProcess());

        mutableTimeProvider.SetUtcNow(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.True(gate.ShouldProcess());
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _zaman;
        public MutableTimeProvider(DateTimeOffset zaman) => _zaman = zaman;
        public void SetUtcNow(DateTimeOffset zaman) => _zaman = zaman;
        public override DateTimeOffset GetUtcNow() => _zaman;
    }
}
