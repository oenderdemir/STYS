namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// UTC zamanı Türkiye yerel tarih/saatine çeviren tek, merkezî ve test edilebilir yardımcı.
/// Türkiye 2016'dan beri sabit UTC+3 kullanır (yaz saati uygulaması yoktur), ancak dönüşüm
/// yine de <see cref="TimeZoneInfo"/> üzerinden yapılır (sabit offset varsayımı kodlanmaz).
///
/// Zaman dilimi kimliği işletim sistemine göre değişir: Linux/ICU tabanlı ortamlarda IANA
/// kimliği ("Europe/Istanbul"), klasik Windows zaman dilimi veritabanında Windows kimliği
/// ("Turkey Standard Time") kullanılır. Bu sınıf ikisini de dener; ilk bulduğunu kullanır.
/// </summary>
public static class TurkeyTimeZoneHelper
{
    private static readonly string[] AdaySaatDilimiKimlikleri = ["Europe/Istanbul", "Turkey Standard Time"];

    public static TimeZoneInfo TurkeySaatDilimi { get; } = ResolveTurkeySaatDilimi();

    /// <summary>
    /// UTC bir zamanı Türkiye yerel tarih/saatine çevirir. Girdi <see cref="DateTimeKind.Local"/>
    /// olamaz (hangi sistem saat diliminden geldiği belirsiz olur); yalnızca UTC veya
    /// (UTC olduğu bilinen) Unspecified kabul edilir.
    /// </summary>
    public static DateTime UtcdenTurkiyeYereleCevir(DateTime utcZaman)
    {
        var normalized = utcZaman.Kind switch
        {
            DateTimeKind.Utc => utcZaman,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(utcZaman, DateTimeKind.Utc),
            DateTimeKind.Local => throw new ArgumentException(
                "utcZaman DateTimeKind.Local olamaz; UTC bekleniyor.", nameof(utcZaman)),
            _ => throw new ArgumentOutOfRangeException(nameof(utcZaman))
        };

        return TimeZoneInfo.ConvertTimeFromUtc(normalized, TurkeySaatDilimi);
    }

    private static TimeZoneInfo ResolveTurkeySaatDilimi()
    {
        foreach (var kimlik in AdaySaatDilimiKimlikleri)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(kimlik);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        throw new InvalidOperationException(
            $"Türkiye saat dilimi sistemde bulunamadı. Denenen kimlikler: {string.Join(", ", AdaySaatDilimiKimlikleri)}");
    }
}
