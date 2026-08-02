using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using STYS.Kurumlar.Entities;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Tesisler.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri;

internal static class EBelgeSnapshotFactory
{
    private const string SnapshotSchemaVersion = "1";
    private const int BelgeVersiyonu = 1;

    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static EBelgeSnapshot CreateSnapshot(
        EBelgeKaydi eBelgeKaydi,
        SatisBelgesi belge,
        Kurum kurum,
        Tesis tesis,
        CariKart cariKart,
        DateTime kararZamaniUtc)
    {
        var payload = new EBelgeCanonicalSnapshotPayload(
            new EBelgeCanonicalSnapshotMetadata(
                SnapshotSchemaVersion,
                BelgeVersiyonu,
                eBelgeKaydi.Durum,
                eBelgeKaydi.EBelgeKanali,
                "CariKart",
                kararZamaniUtc),
            new EBelgeCanonicalBelgeSection(
                belge.Id,
                belge.BelgeTipi,
                belge.BelgeTarihi,
                belge.FaturaKesimTarihi,
                belge.ResmiFaturaNo,
                eBelgeKaydi.EBelgeUuid),
            new EBelgeCanonicalKurumSection(
                kurum.Id,
                kurum.Ad,
                kurum.VergiNo,
                null,
                null,
                kurum.Telefon ?? string.Empty,
                kurum.Eposta),
            new EBelgeCanonicalTesisSection(
                tesis.Id,
                tesis.Ad,
                tesis.Adres,
                tesis.Telefon,
                tesis.Eposta),
            new EBelgeCanonicalCariSection(
                cariKart.Id,
                cariKart.CariKodu,
                cariKart.UnvanAdSoyad,
                cariKart.VergiNoTckn,
                cariKart.VergiDairesi,
                cariKart.Adres,
                cariKart.Telefon,
                cariKart.Eposta,
                cariKart.Il,
                cariKart.Ilce,
                cariKart.EFaturaMukellefiMi,
                cariKart.EArsivKapsamindaMi),
            new EBelgeCanonicalIadeSection(
                belge.IadeEdilenBelgeId,
                belge.IadeEdilenBelge?.BelgeNo,
                belge.IadeEdilenBelge != null ? belge.IadeEdilenBelge.ResmiFaturaNo ?? belge.IadeEdilenBelge.KarsiTarafFaturaNo : null,
                belge.IadeEdilenBelge?.EBelgeKaydi?.EBelgeUuid ?? belge.IadeEdilenBelge?.EBelgeUuid,
                belge.IadeEdilenBelge?.BelgeTarihi),
            new EBelgeCanonicalOdemeSection(
                null,
                null,
                null,
                null),
            belge.ToplamMatrah,
            belge.ToplamKdv,
            belge.GenelToplam,
            belge.Satirlar
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.SiraNo)
                .Select(x => new EBelgeCanonicalSatirSection(
                    x.SiraNo,
                    x.SatirTipi,
                    x.Aciklama,
                    x.Miktar,
                    x.Birim,
                    x.BirimFiyat,
                    x.IndirimOrani,
                    x.IndirimTutari,
                    x.Matrah,
                    x.KdvUygulamaTipi,
                    x.KdvOrani,
                    x.KdvTutari,
                    x.KdvIstisnaKodu,
                    x.KdvIstisnaAciklamasi,
                    x.TevkifatPay,
                    x.TevkifatPayda,
                    x.TevkifatTutari,
                    x.OtvOrani,
                    x.OtvTutari,
                    x.OivOrani,
                    x.OivTutari,
                    x.KonaklamaVergisiOrani,
                    x.KonaklamaVergisiTutari,
                    x.SatirToplami,
                    x.KaynakSatirId))
                .ToList());

        var canonicalJson = JsonSerializer.Serialize(payload, CanonicalJsonOptions);
        var canonicalSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)));

        return new EBelgeSnapshot
        {
            KurumId = belge.KurumId,
            BelgeVersiyonu = BelgeVersiyonu,
            SnapshotSchemaVersion = SnapshotSchemaVersion,
            CanonicalJson = canonicalJson,
            CanonicalSha256 = canonicalSha256
        };
    }

    private sealed record EBelgeCanonicalSnapshotPayload(
        EBelgeCanonicalSnapshotMetadata Metadata,
        EBelgeCanonicalBelgeSection Belge,
        EBelgeCanonicalKurumSection Kurum,
        EBelgeCanonicalTesisSection Tesis,
        EBelgeCanonicalCariSection Cari,
        EBelgeCanonicalIadeSection? Iade,
        EBelgeCanonicalOdemeSection Odeme,
        decimal ToplamMatrah,
        decimal ToplamKdv,
        decimal GenelToplam,
        IReadOnlyList<EBelgeCanonicalSatirSection> Satirlar);

    private sealed record EBelgeCanonicalSnapshotMetadata(
        string SnapshotSchemaVersion,
        int BelgeVersiyonu,
        EBelgeKaydiDurumu EBelgeKaydiDurumu,
        EBelgeKanali EBelgeKanali,
        string KararKaynagi,
        DateTime KararZamaniUtc);

    private sealed record EBelgeCanonicalBelgeSection(
        int SatisBelgesiId,
        SatisBelgesiTipi BelgeTipi,
        DateTime BelgeTarihi,
        DateTime? FaturaKesimTarihi,
        string? ResmiFaturaNo,
        string EBelgeUuid);

    private sealed record EBelgeCanonicalKurumSection(
        int KurumId,
        string KurumUnvani,
        string? VergiNo,
        string? VergiDairesi,
        string? Adres,
        string Telefon,
        string? Eposta);

    private sealed record EBelgeCanonicalTesisSection(
        int TesisId,
        string TesisUnvani,
        string Adres,
        string Telefon,
        string? Eposta);

    private sealed record EBelgeCanonicalCariSection(
        int CariKartId,
        string CariKodu,
        string UnvanAdSoyad,
        string? VergiNoTckn,
        string? VergiDairesi,
        string? Adres,
        string? Telefon,
        string? Eposta,
        string? Il,
        string? Ilce,
        bool EFaturaMukellefiMi,
        bool EArsivKapsamindaMi);

    private sealed record EBelgeCanonicalIadeSection(
        int? IadeEdilenBelgeId,
        string? IadeEdilenBelgeNo,
        string? IadeEdilenFaturaNo,
        string? IadeEdilenEBelgeUuid,
        DateTime? IadeEdilenBelgeTarihi);

    private sealed record EBelgeCanonicalOdemeSection(
        string? ParaBirimi,
        decimal? Kur,
        string? OdemeTuru,
        DateTime? VadeTarihi);

    private sealed record EBelgeCanonicalSatirSection(
        int SiraNo,
        SatisBelgesiSatirTipi SatirTipi,
        string Aciklama,
        decimal Miktar,
        string Birim,
        decimal BirimFiyat,
        decimal IndirimOrani,
        decimal IndirimTutari,
        decimal Matrah,
        KdvUygulamaTipi KdvUygulamaTipi,
        decimal KdvOrani,
        decimal KdvTutari,
        string? KdvIstisnaKodu,
        string? KdvIstisnaAciklamasi,
        int? TevkifatPay,
        int? TevkifatPayda,
        decimal TevkifatTutari,
        decimal OtvOrani,
        decimal OtvTutari,
        decimal OivOrani,
        decimal OivTutari,
        decimal KonaklamaVergisiOrani,
        decimal KonaklamaVergisiTutari,
        decimal SatirToplami,
        string? KaynakSatirId);
}
