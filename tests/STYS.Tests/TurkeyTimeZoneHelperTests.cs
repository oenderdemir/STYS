using STYS.Muhasebe.SatisBelgeleri;
using Xunit;

namespace STYS.Tests;

public class TurkeyTimeZoneHelperTests
{
    [Fact]
    public void SabitUtcArti3OfsetDogruUygulanir()
    {
        var utc = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

        var trt = TurkeyTimeZoneHelper.UtcdenTurkiyeYereleCevir(utc);

        Assert.Equal(new DateTime(2026, 6, 15, 13, 0, 0), trt);
    }

    [Fact]
    public void UtcGunSonuTurkiyeSaatindeBirSonrakiGunuVerir()
    {
        // 13.09.2026 21:30 UTC = 14.09.2026 00:30 TRT (UTC+3) - gün değişimi doğru değerlendirilmeli.
        var utc = new DateTime(2026, 9, 13, 21, 30, 0, DateTimeKind.Utc);

        var trt = TurkeyTimeZoneHelper.UtcdenTurkiyeYereleCevir(utc);

        Assert.Equal(new DateTime(2026, 9, 14, 0, 30, 0), trt);
        Assert.Equal(new DateTime(2026, 9, 14), trt.Date);
    }

    [Fact]
    public void UtcGeceYarisindanOncekiZamanAyniGunuVerir()
    {
        // 13.09.2026 20:59:59 UTC = 13.09.2026 23:59:59 TRT - henüz 14.09.2026'ya geçilmemiş olmalı.
        var utc = new DateTime(2026, 9, 13, 20, 59, 59, DateTimeKind.Utc);

        var trt = TurkeyTimeZoneHelper.UtcdenTurkiyeYereleCevir(utc);

        Assert.Equal(new DateTime(2026, 9, 13), trt.Date);
    }

    [Fact]
    public void UnspecifiedKindUtcOlarakEleAlinir()
    {
        var unspecified = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Unspecified);
        var utc = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

        Assert.Equal(
            TurkeyTimeZoneHelper.UtcdenTurkiyeYereleCevir(utc),
            TurkeyTimeZoneHelper.UtcdenTurkiyeYereleCevir(unspecified));
    }

    [Fact]
    public void LocalKindReddedilir()
    {
        var local = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => TurkeyTimeZoneHelper.UtcdenTurkiyeYereleCevir(local));
    }

    [Fact]
    public void TurkeySaatDilimiSabitUtcArti3TasirVeYazSaatiUygulamazsi()
    {
        var kis = TurkeyTimeZoneHelper.TurkeySaatDilimi.GetUtcOffset(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        var yaz = TurkeyTimeZoneHelper.TurkeySaatDilimi.GetUtcOffset(new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(TimeSpan.FromHours(3), kis);
        Assert.Equal(TimeSpan.FromHours(3), yaz);
    }
}
