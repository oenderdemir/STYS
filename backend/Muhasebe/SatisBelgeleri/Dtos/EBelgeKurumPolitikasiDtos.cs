using STYS.Muhasebe.SatisBelgeleri.Enums;

namespace STYS.Muhasebe.SatisBelgeleri.Dtos;

/// <summary>Faz 2B.10 görev md.17 - politika güncelleme isteği. `RowVersion` boş string ise "henüz politika satırı yok" varsayılır (ilk oluşturma).</summary>
public sealed record KurumEBelgePolitikasiGuncellemeDto
{
    public required EBelgeEntegrasyonYontemi EntegrasyonYontemi { get; init; }

    public required bool AktifMi { get; init; }

    public DateTime? AktivasyonYerelTarihi { get; init; }

    public string? HariciSistemKodu { get; init; }

    public required string DegisiklikNedeni { get; init; }

    public required string RowVersion { get; init; }
}

/// <summary>
/// Faz 2B.10 görev md.17 - API response DTO'su. Entity DOĞRUDAN döndürülmez; VKN veya başka
/// kurum kimlik detayı BURAYA gereksiz yere EKLENMEZ (bkz. görev md.4/md.17).
/// </summary>
public sealed record KurumEBelgePolitikasiDto
{
    public required int KurumId { get; init; }

    public required EBelgeEntegrasyonYontemi EntegrasyonYontemi { get; init; }

    public required bool AktifMi { get; init; }

    public DateTime? AktivasyonYerelTarihi { get; init; }

    public string? HariciSistemKodu { get; init; }

    public required int PolitikaSurumu { get; init; }

    public required string RowVersion { get; init; }
}

/// <summary>Faz 2B.10 görev md.16/md.17 - politika değişiklik geçmişi (audit) response DTO'su.</summary>
public sealed record KurumEBelgePolitikaRevizyonuDto
{
    public required int EskiSurum { get; init; }

    public required int YeniSurum { get; init; }

    public required EBelgeEntegrasyonYontemi EskiYontem { get; init; }

    public required EBelgeEntegrasyonYontemi YeniYontem { get; init; }

    public required bool EskiAktifMi { get; init; }

    public required bool YeniAktifMi { get; init; }

    public required DateTime DegisiklikZamaniUtc { get; init; }

    public required string DegisiklikNedeni { get; init; }

    public string? DegistirenKullanici { get; init; }
}
