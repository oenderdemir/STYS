using System.Diagnostics.Metrics;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.8 görev md.13/md.18 - `EBelgeOutboxWorkerMetrics`'in GERÇEK `System.Diagnostics.Metrics`
/// ölçümlerini (isim, tip, tag anahtarları) doğru ürettiğini `MeterListener` ile doğrudan
/// gözlemler - yalnız düşük cardinality tag'lerin (iş türü, sonuç türü) kullanıldığını, Outbox
/// ID/Kurum ID/EBelgeKaydi ID/hata mesajı/hash/token gibi YÜKSEK cardinality kimliklerin HİÇ tag
/// olarak EKLENMEDİĞİNİ kanıtlar.
/// </summary>
public class EBelgeOutboxWorkerMetricsTests : IDisposable
{
    private readonly EBelgeOutboxWorkerMetrics _metrics = new();
    private readonly List<(string InstrumentName, object? Value, KeyValuePair<string, object?>[] Tags)> _olcumler = [];
    private readonly MeterListener _listener;

    public EBelgeOutboxWorkerMetricsTests()
    {
        _listener = new MeterListener();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == EBelgeOutboxWorkerMetrics.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            _olcumler.Add((instrument.Name, measurement, tags.ToArray())));
        _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
            _olcumler.Add((instrument.Name, measurement, tags.ToArray())));
        _listener.Start();
    }

    public void Dispose()
    {
        _listener.Dispose();
        _metrics.Dispose();
    }

    [Fact]
    public void ClaimedSayaciDusukCardinalityIsTuruTagiIleArtar()
    {
        _metrics.RecordClaimed(EBelgeOutboxIsTuru.ArtefaktOlustur);

        var olcum = Assert.Single(_olcumler, o => o.InstrumentName == "stys_ebelge_outbox_claimed_total");
        Assert.Equal(1L, olcum.Value);
        var tag = Assert.Single(olcum.Tags);
        Assert.Equal("is_turu", tag.Key);
        Assert.Equal("ArtefaktOlustur", tag.Value);
    }

    [Theory]
    [InlineData(EBelgeOutboxIslemeSonucuTuru.Tamamlandi, "stys_ebelge_outbox_completed_total")]
    [InlineData(EBelgeOutboxIslemeSonucuTuru.RetryPlanlandi, "stys_ebelge_outbox_retry_scheduled_total")]
    [InlineData(EBelgeOutboxIslemeSonucuTuru.TerminalHata, "stys_ebelge_outbox_terminal_error_total")]
    [InlineData(EBelgeOutboxIslemeSonucuTuru.SahiplikKaybedildi, "stys_ebelge_outbox_lease_lost_total")]
    public void SonucTuruDogruSayaciArtirirVeSureHistogramiKaydeder(EBelgeOutboxIslemeSonucuTuru sonucTuru, string beklenenSayacAdi)
    {
        _metrics.RecordResult(EBelgeOutboxIsTuru.UblImzala, sonucTuru, TimeSpan.FromMilliseconds(250));

        var sayacOlcumu = Assert.Single(_olcumler, o => o.InstrumentName == beklenenSayacAdi);
        Assert.Equal(1L, sayacOlcumu.Value);

        var sureOlcumu = Assert.Single(_olcumler, o => o.InstrumentName == "stys_ebelge_outbox_processing_duration_ms");
        Assert.Equal(250d, sureOlcumu.Value);

        // Tüm tag'ler yalnız DÜŞÜK cardinality (iş türü/sonuç türü) - Outbox/Kurum/EBelgeKaydi ID,
        // hata mesajı, hash, token GİBİ yüksek cardinality/PII alanlar HİÇBİR ölçümde tag OLARAK
        // görünmemeli.
        foreach (var olcum in _olcumler)
        {
            foreach (var tag in olcum.Tags)
            {
                Assert.True(
                    tag.Key is "is_turu" or "sonuc_turu",
                    $"Beklenmeyen/yüksek cardinality olabilecek tag anahtarı: {tag.Key}");
            }
        }
    }

    [Fact]
    public void PollErrorSayaciTagsizArtar()
    {
        _metrics.RecordPollError();

        var olcum = Assert.Single(_olcumler, o => o.InstrumentName == "stys_ebelge_outbox_poll_errors_total");
        Assert.Equal(1L, olcum.Value);
        Assert.Empty(olcum.Tags);
    }

    [Fact]
    public void InflightArtarVeAzalir()
    {
        _metrics.IncrementInflight();
        _metrics.IncrementInflight();
        _metrics.DecrementInflight();

        var inflightOlcumleri = _olcumler.Where(o => o.InstrumentName == "stys_ebelge_outbox_inflight").ToList();
        Assert.Equal(3, inflightOlcumleri.Count);
        Assert.Equal(1L, inflightOlcumleri[0].Value);
        Assert.Equal(1L, inflightOlcumleri[1].Value);
        Assert.Equal(-1L, inflightOlcumleri[2].Value);
    }
}
