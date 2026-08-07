using STYS.Muhasebe.SatisBelgeleri.Enums;

namespace STYS.Muhasebe.SatisBelgeleri.Dtos;

/// <summary>
/// Faz 2B.11 görev md.6/md.32 - eksik satıcı (Kurum) alanları VE genel blokaj nedenleri İÇİN
/// SABİT, güvenli kod sözlüğü. Ham alan değeri/VKN/müşteri bilgisi ASLA taşımaz - yalnız HANGİ
/// alanın/nedenin eksik olduğunu işaret eden type-safe bir SABİT. Frontend bu kodları KENDİ
/// Türkçe etiket haritasına çevirir (bkz. görev md.11/md.32, "frontend kullanıcı dostu
/// açıklamalara çevirebilir").
/// </summary>
public static class EBelgeKurumReadinessKodlari
{
    // ---- Eksik satıcı (Kurum) ana veri alanları ----
    public const string VergiNo = "VERGI_NO";
    public const string VergiDairesi = "VERGI_DAIRESI";
    public const string Adres = "ADRES";
    public const string Ilce = "ILCE";
    public const string Il = "IL";

    // ---- Genel blokaj nedenleri (bkz. görev md.11) - mevcut EBelgeKurumPolitikaEngelliException
    // güvenli kodlarıyla AYNI (görev md.31, "business logic tek yerde yaşamalı" - burada YENİDEN
    // İCAT EDİLMEZ, doğrudan REUSE edilir). ----
    public const string PolitikaYapilandirilmadi = "EBELGE_KURUM_POLICY_NOT_CONFIGURED";
    public const string PolitikaPasif = "EBELGE_KURUM_POLICY_INACTIVE";
    public const string AktivasyonTarihiGelmedi = "EBELGE_KURUM_POLICY_BEFORE_ACTIVATION_DATE";
    public const string YontemDesteklenmiyor = "EBELGE_KURUM_POLICY_METHOD_NOT_IMPLEMENTED";

    // ---- Readiness'e ÖZGÜ, karar servisinde karşılığı olmayan EK nedenler ----
    public const string GlobalIslemKapisiKapali = "EBELGE_GLOBAL_PROCESSING_DISABLED";
    public const string SaticiAnaVerileriEksik = "EBELGE_KURUM_POLICY_SELLER_DATA_INCOMPLETE";
    public const string SigningGateKapali = "EBELGE_SIGNING_GATE_DISABLED";
}

/// <summary>
/// Faz 2B.11 görev md.8 - bir <see cref="EBelgeEntegrasyonYontemi"/>'nin capability planı, DOĞRUDAN
/// <see cref="STYS.Muhasebe.SatisBelgeleri.Services.IEBelgeYontemYetenekSaglayici"/> üzerinden
/// türetilir - frontend'de İKİNCİ bir capability matrix OLUŞTURULMAZ (görev md.8/md.37).
/// </summary>
public sealed record EBelgeYontemReadinessDto
{
    public required EBelgeEntegrasyonYontemi Yontem { get; init; }

    public required bool OperasyonelMi { get; init; }

    public required bool HariciSistemKoduGerekliMi { get; init; }

    public required bool YerelSnapshotOlustur { get; init; }

    public required bool YerelUnsignedUblOlustur { get; init; }

    public required bool YerelImzaOlustur { get; init; }

    public required bool OtomatikGonderimYap { get; init; }
}

/// <summary>
/// Faz 2B.11 görev md.6 - kurumun e-belge işleme HAZIRLIĞININ tam, otoriter anlık görüntüsü.
/// TÜM iş kuralları (yöntem operasyonel mi, hangi yöntemde snapshot/unsigned-UBL/imza gerekir,
/// global kapı açık mı, aktivasyon tarihi geldi mi, satıcı verisi tamam mı) BURADA, backend'de
/// hesaplanır - frontend YALNIZ sonucu görselleştirir (görev md.7). VKN/müşteri bilgisi/XML/
/// connection string/secret/token/sertifika İÇERMEZ (görev md.48).
/// </summary>
public sealed record KurumEBelgeReadinessDto
{
    public required int KurumId { get; init; }

    public required bool PolitikaYapilandirilmisMi { get; init; }

    public required bool PolitikaAktifMi { get; init; }

    public required EBelgeEntegrasyonYontemi EntegrasyonYontemi { get; init; }

    public required bool YontemOperasyonelMi { get; init; }

    public DateTime? KurumAktivasyonYerelTarihi { get; init; }

    /// <summary>Faz 2B.11 görev md.13 - global minimum aktivasyon tarihi (ör. 2026-09-15), yalnız GÖRÜNTÜLEME amaçlı; backend validasyonunun YERİNE GEÇMEZ (bkz. `EBelgeProcessingOptions.NotBeforeLocalDate`).</summary>
    public DateTime? GlobalMinimumAktivasyonYerelTarihi { get; init; }

    public required bool GlobalProcessingAktifMi { get; init; }

    public required string GlobalProcessingDurumu { get; init; }

    public required bool YerelSnapshotGerekliMi { get; init; }

    public required bool YerelUnsignedUblGerekliMi { get; init; }

    public required bool YerelImzaGerekliMi { get; init; }

    public required bool OtomatikGonderimGerekliMi { get; init; }

    public required bool SaticiAnaVerileriHazirMi { get; init; }

    public required IReadOnlyList<string> EksikSaticiAlanlari { get; init; }

    /// <summary>Faz 2B.11 görev md.30 - signing gate yalnız <see cref="YerelImzaGerekliMi"/>=true iken readiness'e KATILIR; GİB Portal/Kullanılmayacak/HariciMuhasebeSistemi gibi local imza GEREKTİRMEYEN yöntemlerde false'tur (Uygulanamaz).</summary>
    public required bool SigningGateUygulanabilirMi { get; init; }

    public required bool SigningSuAnMumkunMu { get; init; }

    public required bool IslemeHazirMi { get; init; }

    public required IReadOnlyList<string> BlokajNedenleri { get; init; }

    public required IReadOnlyList<EBelgeYontemReadinessDto> Yontemler { get; init; }
}
