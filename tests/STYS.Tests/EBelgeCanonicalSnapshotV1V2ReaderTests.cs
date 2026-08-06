using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Faz 2B.4.1: typed V1/V2 reader sözleşmesinin derlenebilir ve doğru çalıştığını doğrular.
/// V1 ve V2 için TEK bir object/dynamic dispatcher yoktur - bkz.
/// IEBelgeCanonicalSnapshotV1Reader/IEBelgeCanonicalSnapshotV2Reader (EBelgeCanonicalSnapshotV2.cs).
/// </summary>
[Trait("Domain", "EBelge")]
[Trait("TestLevel", "Contract")]
public class EBelgeCanonicalSnapshotV1V2ReaderTests
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    [Fact]
    public void V1ReaderV1PayloadiOkuyabilir()
    {
        var (payload, hash, expected) = CreateV1Fixture();
        var reader = new EBelgeCanonicalSnapshotV1Reader();

        var snapshot = reader.Read(payload, hash);

        Assert.Equal(expected.Belge.SatisBelgesiId, snapshot.Belge.SatisBelgesiId);
        Assert.Equal(expected.Belge.EBelgeUuid, snapshot.Belge.EBelgeUuid);
        Assert.Equal("1", snapshot.Metadata.SnapshotSchemaVersion);
        Assert.Equal(hash.ToUpperInvariant(), snapshot.CanonicalSha256);
    }

    [Fact]
    public void V2ReaderV2PayloadiOkuyabilir()
    {
        var (payload, hash, expected) = CreateV2Fixture();
        var reader = new EBelgeCanonicalSnapshotV2Reader();

        var snapshot = reader.Read(payload, hash);

        Assert.Equal(expected.Belge.SatisBelgesiId, snapshot.Belge.SatisBelgesiId);
        Assert.Equal("EARSIVFATURA", snapshot.Belge.ProfileID);
        Assert.Equal("SATIS", snapshot.Belge.InvoiceTypeCode);
        Assert.Equal(new DateOnly(2026, 9, 15), snapshot.Belge.FaturaTarihiTrt);
        Assert.Equal(new TimeOnly(14, 30, 0), snapshot.Belge.FaturaSaatiTrt);
        Assert.Equal("C62", snapshot.Satirlar[0].BirimKodu);
        Assert.Equal("2", snapshot.Metadata.SnapshotSchemaVersion);
        Assert.Equal(hash.ToUpperInvariant(), snapshot.CanonicalSha256);
    }

    [Fact]
    public void V1PayloadiV2ReaderaSessizceDonusturulmezVeReddedilir()
    {
        var (v1Payload, v1Hash, _) = CreateV1Fixture();
        var v2Reader = new EBelgeCanonicalSnapshotV2Reader();

        var ex = Assert.Throws<EBelgeCanonicalSnapshotException>(() => v2Reader.Read(v1Payload, v1Hash));

        Assert.Equal(EBelgeCanonicalSnapshotException.SafeMessage, ex.Message);
        Assert.Equal(EBelgeCanonicalSnapshotException.HttpStatusCode, ex.ErrorCode);
    }

    [Fact]
    public void V2PayloadiV1ReaderaSessizceDonusturulmezVeReddedilir()
    {
        var (v2Payload, v2Hash, _) = CreateV2Fixture();
        var v1Reader = new EBelgeCanonicalSnapshotV1Reader();

        // V2 JSON'da V1'in UnmappedMemberHandling=Disallow şeması altında bilinmeyen alanlar
        // (profileId, invoiceTypeCode, birimKodu, ...) vardır - V1 reader bunu reddetmelidir.
        Assert.Throws<EBelgeCanonicalSnapshotException>(() => v1Reader.Read(v2Payload, v2Hash));
    }

    [Fact]
    public void V2ReaderGecersizHashiReddeder()
    {
        var (payload, _, _) = CreateV2Fixture();
        var reader = new EBelgeCanonicalSnapshotV2Reader();

        Assert.Throws<EBelgeCanonicalSnapshotException>(() => reader.Read(payload, new string('A', 64)));
    }

    [Fact]
    public void V2ReaderYanlisSurumluMetadataliPayloadiReddeder()
    {
        var (_, _, expected) = CreateV2Fixture();
        var yanlisSurumlu = expected with
        {
            Metadata = expected.Metadata with { SnapshotSchemaVersion = "3" }
        };
        var json = JsonSerializer.Serialize(yanlisSurumlu, V2JsonOptions());
        var payload = Encoding.UTF8.GetBytes(json);
        var hash = ComputeSha256Hex(payload);
        var reader = new EBelgeCanonicalSnapshotV2Reader();

        Assert.Throws<EBelgeCanonicalSnapshotException>(() => reader.Read(payload, hash));
    }

    private static (byte[] Payload, string Hash, EBelgeCanonicalSnapshotV1 Expected) CreateV1Fixture()
    {
        var snapshot = new EBelgeCanonicalSnapshotV1
        {
            Metadata = new EBelgeCanonicalSnapshotMetadataV1
            {
                SnapshotSchemaVersion = "1",
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
            Iade = new EBelgeCanonicalIadeV1(),
            Odeme = new EBelgeCanonicalOdemeV1
            {
                ParaBirimi = "TRY",
                Kur = 1m,
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
                    BirimFiyat = 100.50m,
                    IndirimOrani = 0m,
                    IndirimTutari = 0m,
                    Matrah = 100.50m,
                    KdvUygulamaTipi = KdvUygulamaTipi.Kdvli,
                    KdvOrani = 18m,
                    KdvTutari = 18.09m,
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
                    SatirToplami = 118.59m,
                    KaynakSatirId = "SRC-1"
                }
            ]
        };

        var json = JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);
        var payload = Encoding.UTF8.GetBytes(json);
        var hash = ComputeSha256Hex(payload);

        return (payload, hash, snapshot);
    }

    private static (byte[] Payload, string Hash, EBelgeCanonicalSnapshotV2 Expected) CreateV2Fixture()
    {
        var snapshot = new EBelgeCanonicalSnapshotV2
        {
            Metadata = new EBelgeCanonicalSnapshotMetadataV1
            {
                SnapshotSchemaVersion = "2",
                BelgeVersiyonu = 1,
                EBelgeKaydiDurumu = EBelgeKaydiDurumu.SnapshotHazir,
                EBelgeKanali = EBelgeKanali.EArsiv,
                KararKaynagi = "CariKart",
                KararZamaniUtc = new DateTime(2026, 9, 15, 11, 30, 0, DateTimeKind.Utc)
            },
            Belge = new EBelgeCanonicalBelgeV2
            {
                SatisBelgesiId = 99,
                BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
                BelgeTarihi = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc),
                FaturaKesimTarihi = new DateTime(2026, 9, 15, 11, 30, 0, DateTimeKind.Utc),
                ResmiFaturaNo = "EBF2026000000099",
                EBelgeUuid = "99999999-8888-7777-6666-555555555555",
                ProfileID = "EARSIVFATURA",
                InvoiceTypeCode = "SATIS",
                FaturaTarihiTrt = new DateOnly(2026, 9, 15),
                FaturaSaatiTrt = new TimeOnly(14, 30, 0)
            },
            Kurum = new EBelgeCanonicalKurumV2
            {
                KurumId = 7,
                KurumUnvani = "STYS Test Kurumu A.Ş.",
                VergiNo = "1234567890",
                VergiDairesi = "Kadıköy",
                Adres = "Kurum Adresi 1",
                Ilce = "Kadıköy",
                Il = "İstanbul",
                UlkeAdi = "Türkiye",
                UlkeKodu = "TR",
                PostaKodu = "34700",
                SokakAdi = "Bağdat Caddesi",
                BinaNo = "1",
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
            Alici = new EBelgeCanonicalAliciV2
            {
                MusteriUnvan = null,
                MusteriAdSoyad = "Ayşe Yılmaz",
                MusteriAd = "Ayşe",
                MusteriSoyad = "Yılmaz",
                MusteriVergiNo = null,
                MusteriTcKimlikNo = "11111111110",
                MusteriVergiDairesi = null,
                MusteriAdres = "Alıcı Adres",
                Ilce = "Beşiktaş",
                Il = "İstanbul",
                UlkeAdi = "Türkiye",
                UlkeKodu = "TR",
                PostaKodu = "34100",
                SokakAdi = "Barbaros Bulvarı",
                BinaNo = "10",
                MusteriEposta = "alici@example.com",
                MusteriTelefon = "05550000000",
                KurumsalMi = false
            },
            CariKart = new EBelgeCanonicalCariKartV1
            {
                CariKartId = 9,
                CariKodu = "CR-001",
                EFaturaMukellefiMi = false,
                EArsivKapsamindaMi = true
            },
            Iade = new EBelgeCanonicalIadeV1(),
            Odeme = new EBelgeCanonicalOdemeV1
            {
                ParaBirimi = "TRY",
                Kur = 1m,
                OdemeTuru = null,
                VadeTarihi = null
            },
            ToplamMatrah = 100.00m,
            ToplamKdv = 18.00m,
            GenelToplam = 118.00m,
            Satirlar =
            [
                new EBelgeCanonicalSatirV2
                {
                    SiraNo = 1,
                    SatirTipi = SatisBelgesiSatirTipi.Urun,
                    Aciklama = "Satır 1",
                    Miktar = 1m,
                    Birim = "Adet",
                    BirimKodu = "C62",
                    BirimFiyat = 100.00m,
                    IndirimOrani = 0m,
                    IndirimTutari = 0m,
                    Matrah = 100.00m,
                    KdvUygulamaTipi = KdvUygulamaTipi.Kdvli,
                    KdvOrani = 18m,
                    KdvTutari = 18.00m,
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
                    SatirToplami = 118.00m,
                    KaynakSatirId = "SRC-1"
                }
            ]
        };

        var json = JsonSerializer.Serialize(snapshot, V2JsonOptions());
        var payload = Encoding.UTF8.GetBytes(json);
        var hash = ComputeSha256Hex(payload);

        return (payload, hash, snapshot);
    }

    private static JsonSerializerOptions V2JsonOptions() => SnapshotJsonOptions;

    private static string ComputeSha256Hex(byte[] payload)
        => Convert.ToHexString(SHA256.HashData(payload));
}
