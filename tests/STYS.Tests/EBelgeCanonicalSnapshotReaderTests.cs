using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri;
using Xunit;

namespace STYS.Tests;

public class EBelgeCanonicalSnapshotReaderTests
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    [Fact]
    public void GecerliCanonicalV1SnapshotBasariylaTypedModeleOkunur()
    {
        var fixture = CreateFixture();
        var reader = new EBelgeCanonicalSnapshotReader();
        var talep = fixture.Talep with { CanonicalSha256 = fixture.CanonicalSha256.ToLowerInvariant() };

        var snapshot = reader.Oku(talep);

        Assert.Equal(EBelgeCanonicalSnapshotReader.SupportedSnapshotSchemaVersion, snapshot.Metadata.SnapshotSchemaVersion);
        Assert.Equal(fixture.CanonicalSha256, snapshot.CanonicalSha256);
        Assert.Equal(fixture.ExpectedSnapshot.Metadata.BelgeVersiyonu, snapshot.Metadata.BelgeVersiyonu);
    }

    [Fact]
    public void BelgeIdUuidKanalVeBelgeTipiAynenKalinir()
    {
        var fixture = CreateFixture();
        var reader = new EBelgeCanonicalSnapshotReader();

        var snapshot = reader.Oku(fixture.Talep);

        Assert.Equal(fixture.ExpectedSnapshot.Belge.SatisBelgesiId, snapshot.Belge.SatisBelgesiId);
        Assert.Equal(fixture.ExpectedSnapshot.Belge.EBelgeUuid, snapshot.Belge.EBelgeUuid);
        Assert.Equal(fixture.ExpectedSnapshot.Metadata.EBelgeKanali, snapshot.Metadata.EBelgeKanali);
        Assert.Equal(fixture.ExpectedSnapshot.Belge.BelgeTipi, snapshot.Belge.BelgeTipi);
    }

    [Fact]
    public void KurumTesisAliciVeCariKartAlanlariAynenKalinir()
    {
        var fixture = CreateFixture();
        var reader = new EBelgeCanonicalSnapshotReader();

        var snapshot = reader.Oku(fixture.Talep);

        Assert.Equal(fixture.ExpectedSnapshot.Kurum, snapshot.Kurum);
        Assert.Equal(fixture.ExpectedSnapshot.Tesis, snapshot.Tesis);
        Assert.Equal(fixture.ExpectedSnapshot.Alici, snapshot.Alici);
        Assert.Equal(fixture.ExpectedSnapshot.CariKart, snapshot.CariKart);
    }

    [Fact]
    public void ToplamlarDecimalDegerlerVeSatirSirasiAynenKalinir()
    {
        var fixture = CreateFixture();
        var reader = new EBelgeCanonicalSnapshotReader();

        var snapshot = reader.Oku(fixture.Talep);
        var originalSatirlar = snapshot.Satirlar.ToArray();
        var liste = Assert.IsAssignableFrom<IList<EBelgeCanonicalSatirV1>>(snapshot.Satirlar);

        Assert.Equal(fixture.ExpectedSnapshot.ToplamMatrah, snapshot.ToplamMatrah);
        Assert.Equal(fixture.ExpectedSnapshot.ToplamKdv, snapshot.ToplamKdv);
        Assert.Equal(fixture.ExpectedSnapshot.GenelToplam, snapshot.GenelToplam);
        Assert.Equal(fixture.ExpectedSnapshot.Satirlar.Select(x => x.SiraNo), snapshot.Satirlar.Select(x => x.SiraNo));
        Assert.Equal(fixture.ExpectedSnapshot.Satirlar.Select(x => x.Aciklama), snapshot.Satirlar.Select(x => x.Aciklama));
        Assert.Throws<NotSupportedException>(() => liste.Add(originalSatirlar[0]));
        Assert.Throws<NotSupportedException>(() => liste.RemoveAt(0));
        Assert.Equal(originalSatirlar.Length, snapshot.Satirlar.Count);
        Assert.Equal(originalSatirlar, snapshot.Satirlar);
    }

    [Fact]
    public void CanonicalOlmayanAmaHashiYenidenHesaplanmisJsonReddedilir()
    {
        var fixture = CreateFixture();
        var reader = new EBelgeCanonicalSnapshotReader();
        var parsed = JsonNode.Parse(fixture.CanonicalJson)!.AsObject();
        var nonCanonical = new JsonObject
        {
            ["metadata"] = parsed["metadata"]!.DeepClone(),
            ["belge"] = parsed["belge"]!.DeepClone(),
            ["kurum"] = parsed["kurum"]!.DeepClone(),
            ["tesis"] = parsed["tesis"]!.DeepClone(),
            ["alici"] = parsed["alici"]!.DeepClone(),
            ["cariKart"] = parsed["cariKart"]!.DeepClone(),
            ["iade"] = parsed["iade"]!.DeepClone(),
            ["odeme"] = parsed["odeme"]!.DeepClone(),
            ["toplamMatrah"] = parsed["toplamMatrah"]!.DeepClone(),
            ["genelToplam"] = parsed["genelToplam"]!.DeepClone(),
            ["toplamKdv"] = parsed["toplamKdv"]!.DeepClone(),
            ["satirlar"] = parsed["satirlar"]!.DeepClone()
        };
        var nonCanonicalJson = nonCanonical.ToJsonString(SnapshotJsonOptions);
        var nonCanonicalHash = ComputeSha256(nonCanonicalJson);
        var talep = new EBelgeCanonicalSnapshotOkumaTalebi(
            fixture.Talep.SnapshotSchemaVersion,
            nonCanonicalJson,
            nonCanonicalHash);

        var ex = Assert.Throws<EBelgeCanonicalSnapshotException>(() => reader.Oku(talep));

        Assert.Equal(EBelgeCanonicalSnapshotException.HttpStatusCode, ex.ErrorCode);
        Assert.Equal(EBelgeCanonicalSnapshotException.SafeMessage, ex.Message);
    }

    [Fact]
    public void BosCanonicalJsonReddedilir()
    {
        var fixture = CreateFixture();
        var reader = new EBelgeCanonicalSnapshotReader();
        var talep = fixture.Talep with { CanonicalJson = "" };

        var ex = Assert.Throws<EBelgeCanonicalSnapshotException>(() => reader.Oku(talep));

        Assert.Equal(EBelgeCanonicalSnapshotException.SafeMessage, ex.Message);
    }

    [Fact]
    public void GecersizJsonGuvenliSnapshotExceptionUretir()
    {
        var fixture = CreateFixture();
        var reader = new EBelgeCanonicalSnapshotReader();
        var gecersizJson = "{";
        var talep = fixture.Talep with
        {
            CanonicalJson = gecersizJson,
            CanonicalSha256 = ComputeSha256(gecersizJson)
        };

        var ex = Assert.Throws<EBelgeCanonicalSnapshotException>(() => reader.Oku(talep));

        Assert.Equal(EBelgeCanonicalSnapshotException.HttpStatusCode, ex.ErrorCode);
        Assert.Equal(EBelgeCanonicalSnapshotException.SafeErrorCode, ex.HataKodu);
        Assert.Equal(EBelgeCanonicalSnapshotException.SafeMessage, ex.Message);
        Assert.DoesNotContain("{", ex.Message);
        Assert.DoesNotContain("JsonException", ex.Message);
    }

    [Fact]
    public void IcerikleUyuşmayanHashReddedilir()
    {
        var fixture = CreateFixture();
        var reader = new EBelgeCanonicalSnapshotReader();
        var talep = fixture.Talep with { CanonicalSha256 = new string('A', 64) };

        var ex = Assert.Throws<EBelgeCanonicalSnapshotException>(() => reader.Oku(talep));

        Assert.Equal(EBelgeCanonicalSnapshotException.SafeMessage, ex.Message);
    }

    [Fact]
    public void DesteklenmeyenDisSchemaSurumuReddedilir()
    {
        var fixture = CreateFixture();
        var reader = new EBelgeCanonicalSnapshotReader();
        var talep = fixture.Talep with { SnapshotSchemaVersion = "2" };

        var ex = Assert.Throws<EBelgeCanonicalSnapshotException>(() => reader.Oku(talep));

        Assert.Equal(EBelgeCanonicalSnapshotException.SafeMessage, ex.Message);
    }

    [Fact]
    public void MetadataSchemaSurumuDisSurumleUymuyorsaReddedilir()
    {
        var fixture = CreateFixture();
        var reader = new EBelgeCanonicalSnapshotReader();
        var node = JsonNode.Parse(fixture.CanonicalJson)!.AsObject();
        var metadata = node["metadata"]!.AsObject();
        metadata["snapshotSchemaVersion"] = "2";
        var talep = fixture.Talep with
        {
            CanonicalJson = node.ToJsonString(SnapshotJsonOptions),
            CanonicalSha256 = ComputeSha256(node.ToJsonString(SnapshotJsonOptions))
        };

        var ex = Assert.Throws<EBelgeCanonicalSnapshotException>(() => reader.Oku(talep));

        Assert.Equal(EBelgeCanonicalSnapshotException.SafeMessage, ex.Message);
    }

    [Fact]
    public void ZorunluUstSeviyeBolumEksikseReddedilir()
    {
        var fixture = CreateFixture();
        var reader = new EBelgeCanonicalSnapshotReader();
        var node = JsonNode.Parse(fixture.CanonicalJson)!.AsObject();
        node.Remove("odeme");
        var json = node.ToJsonString(SnapshotJsonOptions);
        var talep = fixture.Talep with
        {
            CanonicalJson = json,
            CanonicalSha256 = ComputeSha256(json)
        };

        var ex = Assert.Throws<EBelgeCanonicalSnapshotException>(() => reader.Oku(talep));

        Assert.Equal(EBelgeCanonicalSnapshotException.SafeMessage, ex.Message);
    }

    [Fact]
    public void BilinmeyenJsonAlaniReddedilir()
    {
        var fixture = CreateFixture();
        var reader = new EBelgeCanonicalSnapshotReader();
        var node = JsonNode.Parse(fixture.CanonicalJson)!.AsObject();
        node["fazlaAlan"] = 1;
        var json = node.ToJsonString(SnapshotJsonOptions);
        var talep = fixture.Talep with
        {
            CanonicalJson = json,
            CanonicalSha256 = ComputeSha256(json)
        };

        var ex = Assert.Throws<EBelgeCanonicalSnapshotException>(() => reader.Oku(talep));

        Assert.Equal(EBelgeCanonicalSnapshotException.SafeMessage, ex.Message);
    }

    [Fact]
    public void BilinmeyenStringEnumDegeriReddedilir()
    {
        var fixture = CreateFixture();
        var reader = new EBelgeCanonicalSnapshotReader();
        var json = fixture.CanonicalJson.Replace("\"belgeTipi\":\"satisFaturasi\"", "\"belgeTipi\":\"bilinmeyenTip\"");
        var talep = fixture.Talep with
        {
            CanonicalJson = json,
            CanonicalSha256 = ComputeSha256(json)
        };

        var ex = Assert.Throws<EBelgeCanonicalSnapshotException>(() => reader.Oku(talep));

        Assert.Equal(EBelgeCanonicalSnapshotException.SafeMessage, ex.Message);
    }

    [Fact]
    public void SayisalEnumDegeriReddedilir()
    {
        var fixture = CreateFixture();
        var reader = new EBelgeCanonicalSnapshotReader();
        var json = fixture.CanonicalJson.Replace("\"belgeTipi\":\"satisFaturasi\"", "\"belgeTipi\":2");
        var talep = fixture.Talep with
        {
            CanonicalJson = json,
            CanonicalSha256 = ComputeSha256(json)
        };

        var ex = Assert.Throws<EBelgeCanonicalSnapshotException>(() => reader.Oku(talep));

        Assert.Equal(EBelgeCanonicalSnapshotException.SafeMessage, ex.Message);
    }

    [Theory]
    [MemberData(nameof(GecersizHashCases))]
    public void GecersizHashReddedilir(string hash)
    {
        var fixture = CreateFixture();
        var reader = new EBelgeCanonicalSnapshotReader();
        var talep = fixture.Talep with { CanonicalSha256 = hash };

        var ex = Assert.Throws<EBelgeCanonicalSnapshotException>(() => reader.Oku(talep));

        Assert.Equal(EBelgeCanonicalSnapshotException.SafeMessage, ex.Message);
    }

    public static IEnumerable<object[]> GecersizHashCases()
    {
        yield return new object[] { "" };
        yield return new object[] { new string('A', 63) };
        yield return new object[] { new string('B', 65) };
        yield return new object[] { new string('Z', 64) };
    }

    private static SnapshotFixture CreateFixture()
    {
        var snapshot = new EBelgeCanonicalSnapshotV1
        {
            Metadata = new EBelgeCanonicalSnapshotMetadataV1
            {
                SnapshotSchemaVersion = EBelgeCanonicalSnapshotReader.SupportedSnapshotSchemaVersion,
                BelgeVersiyonu = 1,
                EBelgeKaydiDurumu = EBelgeKaydiDurumu.SnapshotHazir,
                EBelgeKanali = EBelgeKanali.EFatura,
                KararKaynagi = "CariKart",
                KararZamaniUtc = new DateTime(2026, 8, 3, 12, 34, 56, DateTimeKind.Utc)
            },
            Belge = new EBelgeCanonicalBelgeV1
            {
                SatisBelgesiId = 42,
                BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
                BelgeTarihi = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                FaturaKesimTarihi = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
                ResmiFaturaNo = "EBF2026000000001",
                EBelgeUuid = "11111111-2222-3333-4444-555555555555"
            },
            Kurum = new EBelgeCanonicalKurumV1
            {
                KurumId = 7,
                KurumUnvani = "STYS Test Kurumu A.Ş.",
                VergiNo = "1234567890",
                VergiDairesi = null,
                Adres = null,
                Telefon = "02120000000",
                Eposta = "kurum@example.com"
            },
            Tesis = new EBelgeCanonicalTesisV1
            {
                TesisId = 8,
                TesisUnvani = "Merkez Tesis",
                Adres = "Tesis Adresi 1",
                Telefon = "02120000001",
                Eposta = "tesis@example.com"
            },
            Alici = new EBelgeCanonicalAliciV1
            {
                MusteriUnvan = "Alıcı Ticaret Ltd.",
                MusteriAdSoyad = "Alıcı Ad Soyad",
                MusteriVergiNo = "9988776655",
                MusteriTcKimlikNo = null,
                MusteriVergiDairesi = "Vergi Dairesi",
                MusteriAdres = "Alıcı Adres",
                MusteriEposta = "alici@example.com",
                MusteriTelefon = "05550000000",
                KurumsalMi = true
            },
            CariKart = new EBelgeCanonicalCariKartV1
            {
                CariKartId = 9,
                CariKodu = "CR-001",
                EFaturaMukellefiMi = true,
                EArsivKapsamindaMi = false
            },
            Iade = new EBelgeCanonicalIadeV1
            {
                IadeEdilenBelgeId = 21,
                IadeEdilenBelgeNo = "SBF-2026-0001",
                IadeEdilenFaturaNo = "EBF2026000000000",
                IadeEdilenEBelgeUuid = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                IadeEdilenBelgeTarihi = new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc)
            },
            Odeme = new EBelgeCanonicalOdemeV1
            {
                ParaBirimi = null,
                Kur = null,
                OdemeTuru = null,
                VadeTarihi = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc)
            },
            ToplamMatrah = 100.50m,
            ToplamKdv = 18.09m,
            GenelToplam = 118.59m,
            Satirlar =
            [
                new EBelgeCanonicalSatirV1
                {
                    SiraNo = 1,
                    SatirTipi = SatisBelgesiSatirTipi.Urun,
                    Aciklama = "Satır 1",
                    Miktar = 1m,
                    Birim = "Adet",
                    BirimFiyat = 50.25m,
                    IndirimOrani = 0m,
                    IndirimTutari = 0m,
                    Matrah = 50.25m,
                    KdvUygulamaTipi = KdvUygulamaTipi.Kdvli,
                    KdvOrani = 18m,
                    KdvTutari = 9.05m,
                    KdvIstisnaKodu = null,
                    KdvIstisnaAciklamasi = null,
                    TevkifatPay = null,
                    TevkifatPayda = null,
                    TevkifatTutari = 0m,
                    OtvOrani = 0m,
                    OtvTutari = 0m,
                    OivOrani = 0m,
                    OivTutari = 0m,
                    KonaklamaVergisiOrani = 0m,
                    KonaklamaVergisiTutari = 0m,
                    SatirToplami = 59.30m,
                    KaynakSatirId = "SRC-1"
                },
                new EBelgeCanonicalSatirV1
                {
                    SiraNo = 2,
                    SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                    Aciklama = "Satır 2",
                    Miktar = 2m,
                    Birim = "Adet",
                    BirimFiyat = 25.125m,
                    IndirimOrani = 0m,
                    IndirimTutari = 0m,
                    Matrah = 50.25m,
                    KdvUygulamaTipi = KdvUygulamaTipi.Kdvli,
                    KdvOrani = 18m,
                    KdvTutari = 9.04m,
                    KdvIstisnaKodu = null,
                    KdvIstisnaAciklamasi = null,
                    TevkifatPay = null,
                    TevkifatPayda = null,
                    TevkifatTutari = 0m,
                    OtvOrani = 0m,
                    OtvTutari = 0m,
                    OivOrani = 0m,
                    OivTutari = 0m,
                    KonaklamaVergisiOrani = 0m,
                    KonaklamaVergisiTutari = 0m,
                    SatirToplami = 59.29m,
                    KaynakSatirId = "SRC-2"
                }
            ]
        };

        var canonicalJson = JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);
        var canonicalSha256 = ComputeSha256(canonicalJson);

        return new SnapshotFixture(
            snapshot,
            canonicalJson,
            canonicalSha256,
            new EBelgeCanonicalSnapshotOkumaTalebi(
                EBelgeCanonicalSnapshotReader.SupportedSnapshotSchemaVersion,
                canonicalJson,
                canonicalSha256));
    }

    private static string ComputeSha256(string json)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));

    private sealed record SnapshotFixture(
        EBelgeCanonicalSnapshotV1 ExpectedSnapshot,
        string CanonicalJson,
        string CanonicalSha256,
        EBelgeCanonicalSnapshotOkumaTalebi Talep);
}
