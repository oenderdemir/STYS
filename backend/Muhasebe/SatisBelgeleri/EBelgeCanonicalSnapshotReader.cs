using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.ObjectModel;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri;

public sealed record EBelgeCanonicalSnapshotOkumaTalebi(
    string SnapshotSchemaVersion,
    string CanonicalJson,
    string CanonicalSha256);

public sealed class EBelgeCanonicalSnapshotException : BaseException
{
    public const int HttpStatusCode = 422;
    public const string SafeErrorCode = "EBELGE_CANONICAL_SNAPSHOT_INVALID";
    public const string SafeMessage = "Canonical snapshot doğrulanamadı.";

    public string HataKodu { get; } = SafeErrorCode;

    public EBelgeCanonicalSnapshotException()
        : base(SafeMessage, HttpStatusCode)
    {
    }
}

public interface IEBelgeCanonicalSnapshotReader
{
    EBelgeCanonicalSnapshotV1 Oku(EBelgeCanonicalSnapshotOkumaTalebi talep);
}

public sealed class EBelgeCanonicalSnapshotReader : IEBelgeCanonicalSnapshotReader
{
    public const string SupportedSnapshotSchemaVersion = "1";

    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    public EBelgeCanonicalSnapshotV1 Oku(EBelgeCanonicalSnapshotOkumaTalebi talep)
    {
        if (talep is null)
        {
            throw new BaseException("Okuma talebi boş olamaz.", 400);
        }

        ValidateTalep(talep);
        ValidateHash(talep.CanonicalSha256);
        ValidateHashMatchesJson(talep.CanonicalJson, talep.CanonicalSha256);

        EBelgeCanonicalSnapshotV1? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<EBelgeCanonicalSnapshotV1>(talep.CanonicalJson, CanonicalJsonOptions);
        }
        catch (JsonException)
        {
            throw new EBelgeCanonicalSnapshotException();
        }

        if (snapshot is null)
        {
            throw new EBelgeCanonicalSnapshotException();
        }

        ValidateSnapshot(snapshot, talep.SnapshotSchemaVersion);

        var canonicalJson = JsonSerializer.Serialize(snapshot, CanonicalJsonOptions);
        if (!string.Equals(canonicalJson, talep.CanonicalJson, StringComparison.Ordinal))
        {
            throw new EBelgeCanonicalSnapshotException();
        }

        return snapshot with
        {
            CanonicalSha256 = NormalizeHash(talep.CanonicalSha256),
            Satirlar = new ReadOnlyCollection<EBelgeCanonicalSatirV1>(snapshot.Satirlar.ToList())
        };
    }

    private static void ValidateTalep(EBelgeCanonicalSnapshotOkumaTalebi talep)
    {
        if (string.IsNullOrWhiteSpace(talep.SnapshotSchemaVersion))
        {
            throw new EBelgeCanonicalSnapshotException();
        }

        if (!string.Equals(talep.SnapshotSchemaVersion, SupportedSnapshotSchemaVersion, StringComparison.Ordinal))
        {
            throw new EBelgeCanonicalSnapshotException();
        }

        if (string.IsNullOrWhiteSpace(talep.CanonicalJson))
        {
            throw new EBelgeCanonicalSnapshotException();
        }
    }

    private static void ValidateHash(string canonicalSha256)
    {
        if (!EBelgeCanonicalSnapshotHashUtility.IsValidHexHash(canonicalSha256))
        {
            throw new EBelgeCanonicalSnapshotException();
        }
    }

    private static void ValidateHashMatchesJson(string canonicalJson, string canonicalSha256)
    {
        if (!EBelgeCanonicalSnapshotHashUtility.MatchesUtf8(canonicalJson, canonicalSha256))
        {
            throw new EBelgeCanonicalSnapshotException();
        }
    }

    private static void ValidateSnapshot(EBelgeCanonicalSnapshotV1 snapshot, string outerSnapshotSchemaVersion)
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
            !string.Equals(snapshot.Metadata.SnapshotSchemaVersion, outerSnapshotSchemaVersion, StringComparison.Ordinal))
        {
            throw new EBelgeCanonicalSnapshotException();
        }

        if (!string.Equals(snapshot.Metadata.SnapshotSchemaVersion, SupportedSnapshotSchemaVersion, StringComparison.Ordinal) ||
            snapshot.Metadata.BelgeVersiyonu != 1)
        {
            throw new EBelgeCanonicalSnapshotException();
        }

        if (snapshot.Satirlar.Any(x => x is null))
        {
            throw new EBelgeCanonicalSnapshotException();
        }
    }

    private static string NormalizeHash(string canonicalSha256)
        => EBelgeCanonicalSnapshotHashUtility.NormalizeHash(canonicalSha256);
}

public sealed record class EBelgeCanonicalSnapshotV1
{
    public required EBelgeCanonicalSnapshotMetadataV1 Metadata { get; init; }

    public required EBelgeCanonicalBelgeV1 Belge { get; init; }

    public required EBelgeCanonicalKurumV1 Kurum { get; init; }

    public required EBelgeCanonicalTesisV1 Tesis { get; init; }

    public required EBelgeCanonicalAliciV1 Alici { get; init; }

    public required EBelgeCanonicalCariKartV1 CariKart { get; init; }

    public required EBelgeCanonicalIadeV1 Iade { get; init; }

    public required EBelgeCanonicalOdemeV1 Odeme { get; init; }

    public decimal ToplamMatrah { get; init; }

    public decimal ToplamKdv { get; init; }

    public decimal GenelToplam { get; init; }

    public required IReadOnlyList<EBelgeCanonicalSatirV1> Satirlar { get; init; }

    [JsonIgnore]
    public string CanonicalSha256 { get; init; } = string.Empty;
}

public sealed record class EBelgeCanonicalSnapshotMetadataV1
{
    public string SnapshotSchemaVersion { get; init; } = string.Empty;

    public int BelgeVersiyonu { get; init; }

    public EBelgeKaydiDurumu EBelgeKaydiDurumu { get; init; }

    public EBelgeKanali EBelgeKanali { get; init; }

    public string KararKaynagi { get; init; } = string.Empty;

    public DateTime KararZamaniUtc { get; init; }
}

public sealed record class EBelgeCanonicalBelgeV1
{
    public int SatisBelgesiId { get; init; }

    public SatisBelgesiTipi BelgeTipi { get; init; }

    public DateTime BelgeTarihi { get; init; }

    public DateTime? FaturaKesimTarihi { get; init; }

    public string? ResmiFaturaNo { get; init; }

    public string EBelgeUuid { get; init; } = string.Empty;
}

public sealed record class EBelgeCanonicalKurumV1
{
    public int KurumId { get; init; }

    public string KurumUnvani { get; init; } = string.Empty;

    public string? VergiNo { get; init; }

    public string? VergiDairesi { get; init; }

    public string? Adres { get; init; }

    public string Telefon { get; init; } = string.Empty;

    public string? Eposta { get; init; }
}

public sealed record class EBelgeCanonicalTesisV1
{
    public int TesisId { get; init; }

    public string TesisUnvani { get; init; } = string.Empty;

    public string Adres { get; init; } = string.Empty;

    public string Telefon { get; init; } = string.Empty;

    public string? Eposta { get; init; }
}

public sealed record class EBelgeCanonicalAliciV1
{
    public string? MusteriUnvan { get; init; }

    public string? MusteriAdSoyad { get; init; }

    public string? MusteriVergiNo { get; init; }

    public string? MusteriTcKimlikNo { get; init; }

    public string? MusteriVergiDairesi { get; init; }

    public string? MusteriAdres { get; init; }

    public string? MusteriEposta { get; init; }

    public string? MusteriTelefon { get; init; }

    public bool KurumsalMi { get; init; }
}

public sealed record class EBelgeCanonicalCariKartV1
{
    public int CariKartId { get; init; }

    public string CariKodu { get; init; } = string.Empty;

    public bool EFaturaMukellefiMi { get; init; }

    public bool EArsivKapsamindaMi { get; init; }
}

public sealed record class EBelgeCanonicalIadeV1
{
    public int? IadeEdilenBelgeId { get; init; }

    public string? IadeEdilenBelgeNo { get; init; }

    public string? IadeEdilenFaturaNo { get; init; }

    public string? IadeEdilenEBelgeUuid { get; init; }

    public DateTime? IadeEdilenBelgeTarihi { get; init; }
}

public sealed record class EBelgeCanonicalOdemeV1
{
    public string? ParaBirimi { get; init; }

    public decimal? Kur { get; init; }

    public string? OdemeTuru { get; init; }

    public DateTime? VadeTarihi { get; init; }
}

public sealed record class EBelgeCanonicalSatirV1
{
    public int SiraNo { get; init; }

    public SatisBelgesiSatirTipi SatirTipi { get; init; }

    public string Aciklama { get; init; } = string.Empty;

    public decimal Miktar { get; init; }

    public string Birim { get; init; } = string.Empty;

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
