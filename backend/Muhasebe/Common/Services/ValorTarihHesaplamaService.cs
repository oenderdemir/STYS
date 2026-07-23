using STYS.Muhasebe.Common.Constants;

namespace STYS.Muhasebe.Common.Services;

public interface IValorTarihHesaplamaService
{
    DateOnly HesaplaValorTarihi(DateOnly odemeTarihi, int gunSayisi, string gunTuru);

    ValorDurumBilgisiDto DegerlendirDurum(DateOnly beklenenValorTarihi, string mevcutDurum);
}

public sealed class ValorDurumBilgisiDto
{
    public int ValoreKalanGun { get; set; }
    public bool ValorGectiMi { get; set; }
    public bool BugunValorGunuMu { get; set; }
    public bool AktarilabilirMi { get; set; }
}

/// <summary>
/// Valor tarihi hesaplamasi ve durum degerlendirmesi icin tek merkezi servis. Angular tarafinda
/// tekrar hesaplama yapilmaz, tum sonuc bu servisten gelir.
/// </summary>
public class ValorTarihHesaplamaService : IValorTarihHesaplamaService
{
    private readonly IResmiTatilGunuProvider _resmiTatilGunuProvider;
    private static readonly TimeZoneInfo IstanbulTimeZone = ResolveIstanbulTimeZone();

    public ValorTarihHesaplamaService(IResmiTatilGunuProvider resmiTatilGunuProvider)
    {
        _resmiTatilGunuProvider = resmiTatilGunuProvider;
    }

    public DateOnly HesaplaValorTarihi(DateOnly odemeTarihi, int gunSayisi, string gunTuru)
    {
        if (gunTuru == ValorGunTurleri.IsGunu)
        {
            var tarih = odemeTarihi;
            var kalan = gunSayisi;
            while (kalan > 0)
            {
                tarih = tarih.AddDays(1);
                if (!IsHaftaSonuVeyaTatil(tarih))
                {
                    kalan--;
                }
            }
            return tarih;
        }

        return odemeTarihi.AddDays(gunSayisi);
    }

    public ValorDurumBilgisiDto DegerlendirDurum(DateOnly beklenenValorTarihi, string mevcutDurum)
    {
        var bugun = BugunIstanbul();
        var kalanGun = beklenenValorTarihi.DayNumber - bugun.DayNumber;

        return new ValorDurumBilgisiDto
        {
            ValoreKalanGun = kalanGun,
            ValorGectiMi = kalanGun < 0,
            BugunValorGunuMu = kalanGun == 0,
            AktarilabilirMi = kalanGun <= 0 && IsClaimableDurum(mevcutDurum)
        };
    }

    private static bool IsClaimableDurum(string durum)
    {
        return durum is "ValorBekliyor" or "MutabakatBekliyor" or "Hata";
    }

    private bool IsHaftaSonuVeyaTatil(DateOnly tarih)
    {
        var gun = tarih.DayOfWeek;
        if (gun == DayOfWeek.Saturday || gun == DayOfWeek.Sunday)
        {
            return true;
        }

        return _resmiTatilGunuProvider.ResmiTatilMi(tarih);
    }

    public static DateOnly BugunIstanbul()
    {
        var simdi = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstanbulTimeZone);
        return DateOnly.FromDateTime(simdi);
    }

    private static TimeZoneInfo ResolveIstanbulTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        }
    }
}
