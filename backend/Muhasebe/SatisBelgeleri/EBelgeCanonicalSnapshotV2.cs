using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.ObjectModel;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Enums;

namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// Canonical snapshot V2. V1'e ek olarak, renderer için gerekli fakat V1'de bulunmayan alanları
/// taşır (bkz. docs/e-belge-ubl-pdf-eposta-renderer-hazirlik-raporu.md §6): ProfileID,
/// InvoiceTypeCode, kesim anında çözülmüş Türkiye yerel tarih/saat, satıcı/alıcı yapısal adres
/// ve gerçek kişi alıcılar için ayrı Ad/Soyad, satır düzeyinde BirimKodu.
///
/// V1 record'ları (Metadata/Tesis/CariKart/Iade/Odeme) DEĞİŞMEDEN yeniden kullanılır - V2'nin
/// eklediği alanlar yalnız Belge, Kurum, Alici ve Satır bölümlerindedir. Bu tip, henüz hiçbir
/// üretim kod yolu (EBelgeSnapshotFactory) tarafından doldurulmuyor; bu faz yalnız typed
/// reader sözleşmesini ve şemasını hazırlar (bkz. görev sonuç raporu, "Faz 2B.4.2 için önerilen
/// sonraki adım").
/// </summary>
public sealed record class EBelgeCanonicalSnapshotV2
{
    public required EBelgeCanonicalSnapshotMetadataV1 Metadata { get; init; }

    public required EBelgeCanonicalBelgeV2 Belge { get; init; }

    public required EBelgeCanonicalKurumV2 Kurum { get; init; }

    public required EBelgeCanonicalTesisV1 Tesis { get; init; }

    public required EBelgeCanonicalAliciV2 Alici { get; init; }

    public required EBelgeCanonicalCariKartV1 CariKart { get; init; }

    public required EBelgeCanonicalIadeV1 Iade { get; init; }

    public required EBelgeCanonicalOdemeV1 Odeme { get; init; }

    public decimal ToplamMatrah { get; init; }

    public decimal ToplamKdv { get; init; }

    public decimal GenelToplam { get; init; }

    public required IReadOnlyList<EBelgeCanonicalSatirV2> Satirlar { get; init; }

    [JsonIgnore]
    public string CanonicalSha256 { get; init; } = string.Empty;
}

public sealed record class EBelgeCanonicalBelgeV2
{
    public int SatisBelgesiId { get; init; }

    public SatisBelgesiTipi BelgeTipi { get; init; }

    public DateTime BelgeTarihi { get; init; }

    public DateTime? FaturaKesimTarihi { get; init; }

    public string? ResmiFaturaNo { get; init; }

    public string EBelgeUuid { get; init; } = string.Empty;

    /// <summary>UBL cbc:ProfileID (örn. "EARSIVFATURA"). Kaynağı hâlâ açık bir ürün kararı gerektirir.</summary>
    public required string ProfileID { get; init; }

    /// <summary>UBL cbc:InvoiceTypeCode (dar kapsamda "SATIS").</summary>
    public required string InvoiceTypeCode { get; init; }

    /// <summary>Kesim anında TEK bir TimeProvider okumasından çözülmüş Türkiye yerel tarihi.</summary>
    public required DateOnly FaturaTarihiTrt { get; init; }

    /// <summary>Kesim anında TEK bir TimeProvider okumasından çözülmüş Türkiye yerel saati.</summary>
    public required TimeOnly FaturaSaatiTrt { get; init; }
}

public sealed record class EBelgeCanonicalKurumV2
{
    public int KurumId { get; init; }

    public string KurumUnvani { get; init; } = string.Empty;

    public string? VergiNo { get; init; }

    public string? VergiDairesi { get; init; }

    /// <summary>Serbest metin adres; UI/geriye dönük uyumluluk için korunur, UBL PostalAddress bu alandan üretilmez.</summary>
    public string? Adres { get; init; }

    public string? Ilce { get; init; }

    public string? Il { get; init; }

    public string? UlkeAdi { get; init; }

    public string? UlkeKodu { get; init; }

    public string? PostaKodu { get; init; }

    public string? SokakAdi { get; init; }

    public string? BinaNo { get; init; }

    public string Telefon { get; init; } = string.Empty;

    public string? Eposta { get; init; }
}

public sealed record class EBelgeCanonicalAliciV2
{
    public string? MusteriUnvan { get; init; }

    public string? MusteriAdSoyad { get; init; }

    /// <summary>Gerçek kişi alıcılarda cac:Person/FirstName kaynağı; tahmini bölme YASAK.</summary>
    public string? MusteriAd { get; init; }

    /// <summary>Gerçek kişi alıcılarda cac:Person/FamilyName kaynağı; tahmini bölme YASAK.</summary>
    public string? MusteriSoyad { get; init; }

    public string? MusteriVergiNo { get; init; }

    public string? MusteriTcKimlikNo { get; init; }

    public string? MusteriVergiDairesi { get; init; }

    public string? MusteriAdres { get; init; }

    public string? Ilce { get; init; }

    public string? Il { get; init; }

    public string? UlkeAdi { get; init; }

    public string? UlkeKodu { get; init; }

    public string? PostaKodu { get; init; }

    public string? SokakAdi { get; init; }

    public string? BinaNo { get; init; }

    public string? MusteriEposta { get; init; }

    public string? MusteriTelefon { get; init; }

    public bool KurumsalMi { get; init; }
}

public sealed record class EBelgeCanonicalSatirV2
{
    public int SiraNo { get; init; }

    public SatisBelgesiSatirTipi SatirTipi { get; init; }

    public string Aciklama { get; init; } = string.Empty;

    public decimal Miktar { get; init; }

    public string Birim { get; init; } = string.Empty;

    /// <summary>UN/ECE unitCode (dar kapsamda yalnız "C62" - "Adet"). Serbest metin Birim'den ASLA türetilmez.</summary>
    public required string BirimKodu { get; init; }

    public decimal BirimFiyat { get; init; }

    public decimal IndirimOrani { get; init; }

    public decimal IndirimTutari { get; init; }

    public decimal Matrah { get; init; }

    public KdvUygulamaTipi KdvUygulamaTipi { get; init; }

    public decimal KdvOrani { get; init; }

    public decimal KdvTutari { get; init; }

    public string? KdvIstisnaKodu { get; init; }

    public string? KdvIstisnaAciklamasi { get; init; }

    public int? TevkifatPay { get; init; }

    public int? TevkifatPayda { get; init; }

    public decimal TevkifatTutari { get; init; }

    public decimal OtvOrani { get; init; }

    public decimal OtvTutari { get; init; }

    public decimal OivOrani { get; init; }

    public decimal OivTutari { get; init; }

    public decimal KonaklamaVergisiOrani { get; init; }

    public decimal KonaklamaVergisiTutari { get; init; }

    public decimal SatirToplami { get; init; }

    public string? KaynakSatirId { get; init; }
}

/// <summary>
/// V1 canonical snapshot'ı ham UTF-8 byte payload'ından okur. Mevcut
/// <see cref="IEBelgeCanonicalSnapshotReader"/>/<see cref="EBelgeCanonicalSnapshotReader"/> AYNEN
/// korunmuştur (JSON/hash doğrulaması değişmedi); bu arayüz yalnız byte-tabanlı bir giriş noktası
/// ekler. object/dynamic kullanılmaz; V1 ve V2 için tek bir dispatcher YOKTUR - iki bağımsız
/// typed reader vardır (bkz. görev sonuç raporu).
/// </summary>
public interface IEBelgeCanonicalSnapshotV1Reader
{
    EBelgeCanonicalSnapshotV1 Read(ReadOnlyMemory<byte> payload, string payloadHash);
}

/// <summary>V2 canonical snapshot'ı ham UTF-8 byte payload'ından okur. V1 payload'ı sessizce V2'ye dönüştürülmez (bkz. testler).</summary>
public interface IEBelgeCanonicalSnapshotV2Reader
{
    EBelgeCanonicalSnapshotV2 Read(ReadOnlyMemory<byte> payload, string payloadHash);
}

/// <summary>
/// <see cref="IEBelgeCanonicalSnapshotV1Reader"/> implementasyonu - mevcut, hiç değiştirilmemiş
/// <see cref="EBelgeCanonicalSnapshotReader"/>'ı (composition ile) sarar; V1 hash/şema doğrulama
/// mantığı TEK yerde (EBelgeCanonicalSnapshotReader) kalır, burada TEKRARLANMAZ.
/// </summary>
public sealed class EBelgeCanonicalSnapshotV1Reader : IEBelgeCanonicalSnapshotV1Reader
{
    private readonly EBelgeCanonicalSnapshotReader _innerReader = new();

    public EBelgeCanonicalSnapshotV1 Read(ReadOnlyMemory<byte> payload, string payloadHash)
    {
        var canonicalJson = Encoding.UTF8.GetString(payload.Span);
        var talep = new EBelgeCanonicalSnapshotOkumaTalebi(
            EBelgeCanonicalSnapshotReader.SupportedSnapshotSchemaVersion,
            canonicalJson,
            payloadHash);

        return _innerReader.Oku(talep);
    }
}

/// <summary>
/// <see cref="IEBelgeCanonicalSnapshotV2Reader"/> implementasyonu. V1 okuyucusuyla AYNI
/// belirleyici kurallara uyar: hash formatı + tam UTF-8 byte üzerinden hash eşleşmesi,
/// UnmappedMemberHandling.Disallow ile katı şema, zorunlu alt bölümlerin null olmaması,
/// canonical round-trip (yeniden serialize edilmiş JSON == girdi) doğrulaması. Bu iki
/// okuyucunun paylaştığı TEK şey hash yardımcılarıdır (EBelgeCanonicalSnapshotHashUtility) -
/// şema/tip doğrulaması V1 ve V2 arasında kasıtlı olarak AYRIDIR (farklı record tipleri).
/// </summary>
public sealed class EBelgeCanonicalSnapshotV2Reader : IEBelgeCanonicalSnapshotV2Reader
{
    public const string SupportedSnapshotSchemaVersion = "2";

    /// <summary>
    /// EBelgeSnapshotFactory.CreateSnapshotV2 tarafından da kullanılır - üretici ve okuyucu AYNI
    /// seçenekleri paylaşmazsa, üreticinin ürettiği payload okuyucunun kendi canonical round-trip
    /// denetiminde reddedilir. Bu yüzden internal olarak paylaşılır, ikinci bir kopya oluşturulmaz.
    /// </summary>
    internal static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    public EBelgeCanonicalSnapshotV2 Read(ReadOnlyMemory<byte> payload, string payloadHash)
    {
        if (string.IsNullOrWhiteSpace(payloadHash) || !EBelgeCanonicalSnapshotHashUtility.IsValidHexHash(payloadHash))
        {
            throw new EBelgeCanonicalSnapshotException();
        }

        if (!EBelgeCanonicalSnapshotHashUtility.MatchesUtf8Bytes(payload.Span, payloadHash))
        {
            throw new EBelgeCanonicalSnapshotException();
        }

        var canonicalJson = Encoding.UTF8.GetString(payload.Span);
        if (string.IsNullOrWhiteSpace(canonicalJson))
        {
            throw new EBelgeCanonicalSnapshotException();
        }

        EBelgeCanonicalSnapshotV2? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<EBelgeCanonicalSnapshotV2>(canonicalJson, CanonicalJsonOptions);
        }
        catch (JsonException)
        {
            // V1 payload'ı burada da yakalanır: V1 JSON'da BirimKodu/ProfileID/InvoiceTypeCode/
            // FaturaTarihiTrt/FaturaSaatiTrt gibi V2'ye özgü zorunlu alanlar bulunmadığından
            // deserialize başarısız olur - V1 payload'ı V2'ye SESSİZCE dönüştürülmez.
            throw new EBelgeCanonicalSnapshotException();
        }

        if (snapshot is null)
        {
            throw new EBelgeCanonicalSnapshotException();
        }

        ValidateSnapshot(snapshot);

        var yenidenSerialize = JsonSerializer.Serialize(snapshot, CanonicalJsonOptions);
        if (!string.Equals(yenidenSerialize, canonicalJson, StringComparison.Ordinal))
        {
            throw new EBelgeCanonicalSnapshotException();
        }

        return snapshot with
        {
            CanonicalSha256 = EBelgeCanonicalSnapshotHashUtility.NormalizeHash(payloadHash),
            Satirlar = new ReadOnlyCollection<EBelgeCanonicalSatirV2>(snapshot.Satirlar.ToList())
        };
    }

    private static void ValidateSnapshot(EBelgeCanonicalSnapshotV2 snapshot)
    {
        if (snapshot.Metadata is null ||
            snapshot.Belge is null ||
            snapshot.Kurum is null ||
            snapshot.Tesis is null ||
            snapshot.Alici is null ||
            snapshot.CariKart is null ||
            snapshot.Iade is null ||
            snapshot.Odeme is null ||
            snapshot.Satirlar is null)
        {
            throw new EBelgeCanonicalSnapshotException();
        }

        if (string.IsNullOrWhiteSpace(snapshot.Metadata.SnapshotSchemaVersion) ||
            !string.Equals(snapshot.Metadata.SnapshotSchemaVersion, SupportedSnapshotSchemaVersion, StringComparison.Ordinal))
        {
            throw new EBelgeCanonicalSnapshotException();
        }

        if (snapshot.Metadata.BelgeVersiyonu != 1)
        {
            throw new EBelgeCanonicalSnapshotException();
        }

        if (snapshot.Satirlar.Any(x => x is null))
        {
            throw new EBelgeCanonicalSnapshotException();
        }
    }
}
