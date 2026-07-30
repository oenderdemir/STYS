using Microsoft.EntityFrameworkCore;
using STYS.Muhasebe.Kdv.Entities;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// Part C — a1f25d6'nın ürettiği "KDV istisna tanımı doğrulaması belgenin satış/alış yönüne
/// göre yapılır" davranışını GERÇEK SQL Server üzerinde, gerçek SatisBelgesiService public
/// akışıyla (CreateAsync ve MuhasebeOnayinaGonderAsync) doğrulayan regresyon testleri.
///
/// İstisna tanımları GERÇEK DbContext'ten okunur (mock ile doğrudan döndürülmez); her test
/// kendi izole KdvIstisnaTanim satırlarını oluşturur ve testten sonra temizler.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class SatisBelgesiKdvIstisnaYonuIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "KDVYON-355";

    private string _uniqueSuffix = TestMarker;
    private int _satisTanimId;
    private int _alisTanimId;
    private int _ikiYonluTanimId;
    private string _satisTanimEtiket = string.Empty;
    private string _alisTanimEtiket = string.Empty;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return;
        }

        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();

        var satisTanim = new KdvIstisnaTanim
        {
            Kod = $"SATIS-{_uniqueSuffix}",
            Ad = "Yalnizca satista kullanilabilen istisna",
            UygulamaTipi = KdvUygulamaTipi.TamIstisna,
            SatisIslemlerindeKullanilirMi = true,
            AlisIslemlerindeKullanilirMi = false,
            AktifMi = true
        };
        var alisTanim = new KdvIstisnaTanim
        {
            Kod = $"ALIS-{_uniqueSuffix}",
            Ad = "Yalnizca aliste kullanilabilen istisna",
            UygulamaTipi = KdvUygulamaTipi.TamIstisna,
            SatisIslemlerindeKullanilirMi = false,
            AlisIslemlerindeKullanilirMi = true,
            AktifMi = true
        };
        var ikiYonluTanim = new KdvIstisnaTanim
        {
            Kod = $"IKIYON-{_uniqueSuffix}",
            Ad = "Hem satis hem aliste kullanilabilen istisna",
            UygulamaTipi = KdvUygulamaTipi.TamIstisna,
            SatisIslemlerindeKullanilirMi = true,
            AlisIslemlerindeKullanilirMi = true,
            AktifMi = true
        };
        dbContext.KdvIstisnaTanimlari.AddRange(satisTanim, alisTanim, ikiYonluTanim);
        await dbContext.SaveChangesAsync();

        _satisTanimId = satisTanim.Id;
        _alisTanimId = alisTanim.Id;
        _ikiYonluTanimId = ikiYonluTanim.Id;
        _satisTanimEtiket = $"{satisTanim.Kod} — {satisTanim.Ad}";
        _alisTanimEtiket = $"{alisTanim.Kod} — {alisTanim.Ad}";
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return;
        }

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(dbContext, _uniqueSuffix);
    }

    public static IEnumerable<object[]> YonMatrisi()
    {
        // (belgeTipi, kullanilacakTanim, kabulEdilmeliMi)
        yield return [SatisBelgesiTipi.SatisFaturasi, "Satis", true];
        yield return [SatisBelgesiTipi.AlisFaturasi, "Satis", false];
        yield return [SatisBelgesiTipi.AlisFaturasi, "Alis", true];
        yield return [SatisBelgesiTipi.SatisFaturasi, "Alis", false];
        yield return [SatisBelgesiTipi.SatisFaturasi, "Iki", true];
        yield return [SatisBelgesiTipi.AlisFaturasi, "Iki", true];
        yield return [SatisBelgesiTipi.SatisIadeFaturasi, "Satis", true];
        yield return [SatisBelgesiTipi.AlisIadeFaturasi, "Alis", true];
        yield return [SatisBelgesiTipi.Proforma, "Satis", true];
    }

    [IntegrationTheory]
    [MemberData(nameof(YonMatrisi))]
    public async Task KdvIstisnaYonKontrolu_GercekServisAkisiylaDogruSekildeKabulVeyaRedEdilir(
        SatisBelgesiTipi belgeTipi, string kullanilacakTanim, bool kabulEdilmeliMi)
    {
        var tanimId = kullanilacakTanim switch
        {
            "Satis" => _satisTanimId,
            "Alis" => _alisTanimId,
            "Iki" => _ikiYonluTanimId,
            _ => throw new InvalidOperationException($"Bilinmeyen tanim: {kullanilacakTanim}")
        };

        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var request = new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = belgeTipi,
            BelgeTarihi = new DateTime(2026, 1, 15),
            MusteriAdSoyad = "Test Musteri " + _uniqueSuffix,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1,
                    Aciklama = "Istisnali satir",
                    Miktar = 1,
                    BirimFiyat = 500m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.TamIstisna,
                    KdvIstisnaTanimId = tanimId
                }
            ]
        };

        if (kabulEdilmeliMi)
        {
            var created = await service.CreateAsync(request, CancellationToken.None);
            var satirDb = await dbContext.SatisBelgesiSatirlari.AsNoTracking()
                .FirstAsync(x => x.SatisBelgesiId == created.Id);
            Assert.Equal(tanimId, satirDb.KdvIstisnaTanimId);
        }
        else
        {
            var ex = await Assert.ThrowsAsync<BaseException>(() => service.CreateAsync(request, CancellationToken.None));

            if (kullanilacakTanim == "Satis")
            {
                // satis-only tanim, alis belgesinde reddedilmeli -> "alış" yönü hatası
                Assert.Contains("KDV istisna tanımı alış işlemlerinde kullanılamaz", ex.Message);
            }
            else
            {
                // alis-only tanim, satis belgesinde reddedilmeli -> "satış" yönü hatası
                Assert.Contains("KDV istisna tanımı satış işlemlerinde kullanılamaz", ex.Message);
            }
        }
    }

    [IntegrationFact]
    public async Task OnayaGonderilirkenYenidenDogrulama_TanimSonradanPasifYapilirsa_OnayaGonderilemez()
    {
        await using var dbContext = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var service = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(dbContext);

        var request = new CreateSatisBelgesiRequest
        {
            BelgeNo = $"BLG-{_uniqueSuffix}-{Guid.NewGuid():N}"[..40],
            BelgeTipi = SatisBelgesiTipi.SatisFaturasi,
            BelgeTarihi = new DateTime(2026, 1, 15),
            MusteriAdSoyad = "Test Musteri " + _uniqueSuffix,
            Satirlar =
            [
                new CreateSatisBelgesiSatiriRequest
                {
                    SiraNo = 1, Aciklama = "Istisnali satir", Miktar = 1, BirimFiyat = 500m,
                    KdvUygulamaTipi = (int)KdvUygulamaTipi.TamIstisna, KdvIstisnaTanimId = _satisTanimId
                }
            ]
        };

        // 1. Gecerli tanimla belge/satir olustur (basarili olmali).
        var created = await service.CreateAsync(request, CancellationToken.None);

        // 2. Belge onaya gonderilmeden ONCE tanimi pasif hale getir.
        var tanimDb = await dbContext.KdvIstisnaTanimlari.FirstAsync(x => x.Id == _satisTanimId);
        tanimDb.AktifMi = false;
        await dbContext.SaveChangesAsync();

        // 3. Onaya gonderme, ARADA DEGISEN durumu yeniden dogrulamali ve reddetmelidir - yalnizca
        // satir eklenirken degil, sonraki yeniden dogrulamada da ayni kontrol calisir.
        var ex = await Assert.ThrowsAsync<BaseException>(
            () => service.MuhasebeOnayinaGonderAsync(created.Id!.Value, CancellationToken.None));
        Assert.Contains("pasif durumda", ex.Message);

        var belgeDb = await dbContext.SatisBelgeleri.AsNoTracking().FirstAsync(x => x.Id == created.Id);
        Assert.Equal(STYS.Muhasebe.SatisBelgeleri.Enums.SatisBelgesiDurumu.Taslak, belgeDb.Durum);
    }
}
