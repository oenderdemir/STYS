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
    private const string SnapshotSchemaVersionV2 = "2";
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
                kurum.VergiDairesi,
                kurum.Adres,
                kurum.Telefon ?? string.Empty,
                kurum.Eposta),
            new EBelgeCanonicalTesisSection(
                tesis.Id,
                tesis.Ad,
                tesis.Adres,
                tesis.Telefon,
                tesis.Eposta),
            new EBelgeCanonicalAliciSection(
                belge.MusteriUnvan,
                belge.MusteriAdSoyad,
                belge.MusteriVergiNo,
                belge.MusteriTcKimlikNo,
                belge.MusteriVergiDairesi,
                belge.MusteriAdres,
                belge.MusteriEposta,
                belge.MusteriTelefon,
                belge.KurumsalMi),
            new EBelgeCanonicalCariKartSection(
                cariKart.Id,
                cariKart.CariKodu,
                cariKart.EFaturaMukellefiMi,
                cariKart.EArsivKapsamindaMi),
            new EBelgeCanonicalIadeSection(
                belge.IadeEdilenBelgeId,
                belge.IadeEdilenBelge?.BelgeNo,
                belge.IadeEdilenBelge != null ? belge.IadeEdilenBelge.ResmiFaturaNo ?? belge.IadeEdilenBelge.KarsiTarafFaturaNo : null,
                belge.IadeEdilenBelge?.EBelgeKaydi?.EBelgeUuid ?? belge.IadeEdilenBelge?.EBelgeUuid,
                belge.IadeEdilenBelge?.BelgeTarihi),
            new EBelgeCanonicalOdemeSection(
                belge.ParaBirimi,
                belge.Kur,
                null,
                belge.VadeTarihi),
            belge.ToplamMatrah,
            belge.ToplamKdv,
            belge.GenelToplam,
            belge.Satirlar
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.SiraNo)
                .ThenBy(x => x.Id)
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

    /// <summary>
    /// Faz 2B.4.2: gerçek, typed V2 üretim yolu. Yalnız kesim öncesi UBL kapısından
    /// (IEBelgeUblPreCutValidator) BAŞARIYLA geçmiş belgeler için çağrılmalıdır - bu metot
    /// kendisi yeniden doğrulama YAPMAZ (renderer için yeniden hesaplama yapılmaz ilkesiyle
    /// tutarlı); kapı zaten kanalın EArsiv, belge tipinin SatisFaturasi, tüm satırların
    /// Birim="Adet" olduğunu garanti etmiştir.
    ///
    /// planlananKesimZamaniUtc, FaturaKesAsync içinde TEK bir TimeProvider okumasından
    /// gelen değerin AYNISI olmalıdır - burada ikinci bir zaman okuması YAPILMAZ, yalnız
    /// TurkeyTimeZoneHelper ile (saf, deterministik) TRT'ye çevrilir.
    ///
    /// V2 kayıt tipi (EBelgeCanonicalSnapshotV2), EBelgeCanonicalSnapshotV2Reader'ın okuyabileceği
    /// AYNI public tip ve AYNI JsonSerializerOptions ile serialize edilir - V1'in özel/private
    /// record kopyalama deseni burada TEKRARLANMAZ, bu yüzden üretilen payload'ın okuyucudan
    /// geçeceği garanti edilir (bkz. EBelgeCanonicalSnapshotV1V2ReaderTests ile aynı prensip).
    /// </summary>
    public static EBelgeSnapshot CreateSnapshotV2(
        EBelgeKaydi eBelgeKaydi,
        SatisBelgesi belge,
        Kurum kurum,
        Tesis tesis,
        CariKart cariKart,
        DateTime planlananKesimZamaniUtc)
    {
        var kesimZamaniTrt = TurkeyTimeZoneHelper.UtcdenTurkiyeYereleCevir(planlananKesimZamaniUtc);

        var v2 = new EBelgeCanonicalSnapshotV2
        {
            Metadata = new EBelgeCanonicalSnapshotMetadataV1
            {
                SnapshotSchemaVersion = SnapshotSchemaVersionV2,
                BelgeVersiyonu = BelgeVersiyonu,
                EBelgeKaydiDurumu = eBelgeKaydi.Durum,
                EBelgeKanali = eBelgeKaydi.EBelgeKanali,
                KararKaynagi = "CariKart",
                KararZamaniUtc = planlananKesimZamaniUtc
            },
            Belge = new EBelgeCanonicalBelgeV2
            {
                SatisBelgesiId = belge.Id,
                BelgeTipi = belge.BelgeTipi,
                BelgeTarihi = belge.BelgeTarihi,
                FaturaKesimTarihi = belge.FaturaKesimTarihi,
                ResmiFaturaNo = belge.ResmiFaturaNo,
                EBelgeUuid = eBelgeKaydi.EBelgeUuid,
                // Kesim öncesi kapı, kanal=EArsiv ve belge tipi=SatisFaturasi olduğunu zaten
                // garanti etti (bkz. IEBelgeUblPreCutValidator kural 3-4) - bu iki sabit
                // deterministik üretilir, yeniden hesaplama/karar YAPILMAZ.
                ProfileID = "EARSIVFATURA",
                InvoiceTypeCode = "SATIS",
                FaturaTarihiTrt = DateOnly.FromDateTime(kesimZamaniTrt),
                FaturaSaatiTrt = TimeOnly.FromDateTime(kesimZamaniTrt)
            },
            Kurum = new EBelgeCanonicalKurumV2
            {
                KurumId = kurum.Id,
                KurumUnvani = kurum.Ad,
                VergiNo = kurum.VergiNo,
                VergiDairesi = kurum.VergiDairesi,
                Adres = kurum.Adres,
                Ilce = kurum.Ilce,
                Il = kurum.Il,
                // İlk dalga yalnız Türkiye içi adresleri destekler (bkz. hazırlık raporu §8) -
                // renderer sabiti; ayrı bir Ülke alanı henüz eklenmedi (bkz. görev sonuç raporu).
                UlkeAdi = "Türkiye",
                UlkeKodu = "TR",
                PostaKodu = null,
                SokakAdi = null,
                BinaNo = null,
                Telefon = kurum.Telefon ?? string.Empty,
                Eposta = kurum.Eposta
            },
            Tesis = new EBelgeCanonicalTesisV1
            {
                TesisId = tesis.Id,
                TesisUnvani = tesis.Ad,
                Adres = tesis.Adres,
                Telefon = tesis.Telefon,
                Eposta = tesis.Eposta
            },
            Alici = new EBelgeCanonicalAliciV2
            {
                MusteriUnvan = belge.MusteriUnvan,
                MusteriAdSoyad = belge.MusteriAdSoyad,
                MusteriAd = belge.MusteriAd,
                MusteriSoyad = belge.MusteriSoyad,
                MusteriVergiNo = belge.MusteriVergiNo,
                MusteriTcKimlikNo = belge.MusteriTcKimlikNo,
                MusteriVergiDairesi = belge.MusteriVergiDairesi,
                MusteriAdres = belge.MusteriAdres,
                Ilce = belge.MusteriIlce,
                Il = belge.MusteriIl,
                UlkeAdi = "Türkiye",
                UlkeKodu = "TR",
                PostaKodu = null,
                SokakAdi = null,
                BinaNo = null,
                MusteriEposta = belge.MusteriEposta,
                MusteriTelefon = belge.MusteriTelefon,
                KurumsalMi = belge.KurumsalMi
            },
            CariKart = new EBelgeCanonicalCariKartV1
            {
                CariKartId = cariKart.Id,
                CariKodu = cariKart.CariKodu,
                EFaturaMukellefiMi = cariKart.EFaturaMukellefiMi,
                EArsivKapsamindaMi = cariKart.EArsivKapsamindaMi
            },
            Iade = new EBelgeCanonicalIadeV1
            {
                IadeEdilenBelgeId = belge.IadeEdilenBelgeId,
                IadeEdilenBelgeNo = belge.IadeEdilenBelge?.BelgeNo,
                IadeEdilenFaturaNo = belge.IadeEdilenBelge != null
                    ? belge.IadeEdilenBelge.ResmiFaturaNo ?? belge.IadeEdilenBelge.KarsiTarafFaturaNo
                    : null,
                IadeEdilenEBelgeUuid = belge.IadeEdilenBelge?.EBelgeKaydi?.EBelgeUuid ?? belge.IadeEdilenBelge?.EBelgeUuid,
                IadeEdilenBelgeTarihi = belge.IadeEdilenBelge?.BelgeTarihi
            },
            Odeme = new EBelgeCanonicalOdemeV1
            {
                ParaBirimi = belge.ParaBirimi,
                Kur = belge.Kur,
                OdemeTuru = null,
                VadeTarihi = belge.VadeTarihi
            },
            ToplamMatrah = belge.ToplamMatrah,
            ToplamKdv = belge.ToplamKdv,
            GenelToplam = belge.GenelToplam,
            Satirlar = belge.Satirlar
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.SiraNo)
                .ThenBy(x => x.Id)
                .Select(x => new EBelgeCanonicalSatirV2
                {
                    SiraNo = x.SiraNo,
                    SatirTipi = x.SatirTipi,
                    Aciklama = x.Aciklama,
                    Miktar = x.Miktar,
                    Birim = x.Birim,
                    // Kesim öncesi kapı Birim=="Adet" olduğunu zaten doğruladı (kural 10) -
                    // BirimKodu bu doğrulamadan SONRA, sabit "C62" olarak üretilir.
                    BirimKodu = "C62",
                    BirimFiyat = x.BirimFiyat,
                    IndirimOrani = x.IndirimOrani,
                    IndirimTutari = x.IndirimTutari,
                    Matrah = x.Matrah,
                    KdvUygulamaTipi = x.KdvUygulamaTipi,
                    KdvOrani = x.KdvOrani,
                    KdvTutari = x.KdvTutari,
                    KdvIstisnaKodu = x.KdvIstisnaKodu,
                    KdvIstisnaAciklamasi = x.KdvIstisnaAciklamasi,
                    TevkifatPay = x.TevkifatPay,
                    TevkifatPayda = x.TevkifatPayda,
                    TevkifatTutari = x.TevkifatTutari,
                    OtvOrani = x.OtvOrani,
                    OtvTutari = x.OtvTutari,
                    OivOrani = x.OivOrani,
                    OivTutari = x.OivTutari,
                    KonaklamaVergisiOrani = x.KonaklamaVergisiOrani,
                    KonaklamaVergisiTutari = x.KonaklamaVergisiTutari,
                    SatirToplami = x.SatirToplami,
                    KaynakSatirId = x.KaynakSatirId
                })
                .ToList()
        };

        // Exact UTF-8 byte dizisi TEK sefer üretilir; hash bu diziden, canonicalJson string'i
        // AYNI diziden türetilir (yeniden serialize EDİLMEZ) - bkz. EBelgeCanonicalPayload.
        var utf8Bytes = JsonSerializer.SerializeToUtf8Bytes(v2, EBelgeCanonicalSnapshotV2Reader.CanonicalJsonOptions);
        var payload = EBelgeCanonicalPayload.FromUtf8Bytes(utf8Bytes);

        return new EBelgeSnapshot
        {
            KurumId = belge.KurumId,
            BelgeVersiyonu = BelgeVersiyonu,
            SnapshotSchemaVersion = SnapshotSchemaVersionV2,
            CanonicalJson = payload.ToUtf8String(),
            CanonicalSha256 = payload.Sha256Hex
        };
    }

    private sealed record EBelgeCanonicalSnapshotPayload(
        EBelgeCanonicalSnapshotMetadata Metadata,
        EBelgeCanonicalBelgeSection Belge,
        EBelgeCanonicalKurumSection Kurum,
        EBelgeCanonicalTesisSection Tesis,
        EBelgeCanonicalAliciSection Alici,
        EBelgeCanonicalCariKartSection CariKart,
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

    private sealed record EBelgeCanonicalAliciSection(
        string? MusteriUnvan,
        string? MusteriAdSoyad,
        string? MusteriVergiNo,
        string? MusteriTcKimlikNo,
        string? MusteriVergiDairesi,
        string? MusteriAdres,
        string? MusteriEposta,
        string? MusteriTelefon,
        bool KurumsalMi);

    private sealed record EBelgeCanonicalCariKartSection(
        int CariKartId,
        string CariKodu,
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
