using System.Reflection;
using KampServiceNs = STYS.Kamp.Services;
using RestoranServiceNs = STYS.RestoranYonetimi.Services;
using RezervasyonServiceNs = STYS.Rezervasyonlar.Services;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.Rezervasyonlar.Dto;
using STYS.TicariBelgeler.Controllers;
using TOD.Platform.AspNetCore.Authorization;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Reflection tabanlı, hızlı (DB gerektirmeyen) testler - TicariBelge operasyon sınırının
/// yapısal kurallarını doğrular: lookup endpointleri yalnızca TicariBelgeYonetimi.View kullanır
/// (TesisYonetimi/CariKartYonetimi/MuhasebeKdvIstisnaTanimlariYonetimi İSTEMEZ), operasyon
/// modülü servisleri ISatisBelgesiService/ISatisBelgesiTaslakOlusturmaService'i DOĞRUDAN inject
/// ETMEZ (bkz. görev A/D önceki turlarda tamamlanan ayrım), ve Rezervasyon gelir özeti DTO'sunda
/// MuhasebeFisId bulunmaz (bkz. Round 1).
/// </summary>
public class TicariBelgeOperasyonSinirYansimasiTests
{
    private static readonly string[] LookupEndpointMetotlari =
    [
        nameof(TicariBelgelerController.GetTesisLookup),
        nameof(TicariBelgelerController.GetCariKartLookup),
        nameof(TicariBelgelerController.GetKdvIstisnaLookup),
        nameof(TicariBelgelerController.GetIadeAdaylariLookup),
        nameof(TicariBelgelerController.GetKaynakSatirLookup)
    ];

    private static readonly FieldInfo PermissionsField = typeof(PermissionAttribute)
        .GetField("_permissions", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("PermissionAttribute._permissions alani bulunamadi.");

    [Theory]
    [MemberData(nameof(LookupEndpointAdlari))]
    public void LookupEndpointleri_YalnizcaTicariBelgeYonetimiViewYetkisiKullanir(string metotAdi)
    {
        var metot = typeof(TicariBelgelerController).GetMethod(metotAdi, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Metot bulunamadi: {metotAdi}");

        var permissionAttrs = metot.GetCustomAttributes<PermissionAttribute>().ToList();
        Assert.Single(permissionAttrs);

        var permissions = (string[])PermissionsField.GetValue(permissionAttrs[0])!;
        Assert.Equal([StructurePermissions.TicariBelgeYonetimi.View], permissions);

        // Diger operasyon-disi permission sabitleri (TesisYonetimi/CariKartYonetimi/Kdv) burada
        // ASLA gorunmemelidir - bkz. gorev A.
        Assert.DoesNotContain(permissions, p => p.StartsWith("TesisYonetimi", StringComparison.Ordinal));
        Assert.DoesNotContain(permissions, p => p.StartsWith("CariKartYonetimi", StringComparison.Ordinal));
        Assert.DoesNotContain(permissions, p => p.Contains("KdvIstisna", StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<object[]> LookupEndpointAdlari() => LookupEndpointMetotlari.Select(x => new object[] { x });

    [Fact]
    public void RezervasyonGelirOzetiDto_MuhasebeFisIdIcermez()
    {
        var tip = typeof(RezervasyonGelirOzetiDto);
        Assert.Null(tip.GetProperty("MuhasebeFisId"));
        Assert.NotNull(tip.GetProperty("MuhasebelestirildiMi"));
    }

    [Fact]
    public void KampSatisBelgesiService_ISatisBelgesiServiceVeyaTaslakServisiniDogrudanInjectEtmez()
    {
        AssertConstructorDoesNotDependOn(typeof(KampServiceNs.KampSatisBelgesiService));
    }

    [Fact]
    public void RestoranSatisBelgesiService_ISatisBelgesiServiceVeyaTaslakServisiniDogrudanInjectEtmez()
    {
        AssertConstructorDoesNotDependOn(typeof(RestoranServiceNs.RestoranSatisBelgesiService));
    }

    [Fact]
    public void RezervasyonSatisBelgesiService_ISatisBelgesiServiceVeyaTaslakServisiniDogrudanInjectEtmez()
    {
        AssertConstructorDoesNotDependOn(typeof(RezervasyonServiceNs.RezervasyonSatisBelgesiService));
    }

    private static void AssertConstructorDoesNotDependOn(Type serviceType)
    {
        var yasakliTipler = new[] { typeof(ISatisBelgesiService), typeof(ISatisBelgesiTaslakOlusturmaService) };
        var ctor = serviceType.GetConstructors().Single();
        var parametreTipleri = ctor.GetParameters().Select(p => p.ParameterType).ToList();

        foreach (var yasakli in yasakliTipler)
        {
            Assert.DoesNotContain(yasakli, parametreTipleri);
        }
    }
}
