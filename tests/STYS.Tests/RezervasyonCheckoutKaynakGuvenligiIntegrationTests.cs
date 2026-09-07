using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Repositories;
using STYS.Muhasebe.SatisBelgeleri.Services;
using TOD.Platform.SharedKernel.Exceptions;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// RezervasyonCheckout kaynak kaynağı (source provenance) hardening'i: reserved "RezervasyonCheckout"
/// kaynağı yalnız sunucu-taraflı güven bayrağıyla (RezervasyonCheckoutAkisiMi) üretilebilir. Manuel
/// create ve generic kaynaktan-taslak endpoint'leri bu kaynağı SPOOF EDEMEZ; ama gerçek akış
/// (Otel + RezervasyonCheckout + FaturaTaslagi) muhasebe fişi ve aktif cari hareket üretebilir.
/// </summary>
[Trait("Category", "Integration")]
[Collection(SqlServerIntegrationCollection.Name)]
public class RezervasyonCheckoutKaynakGuvenligiIntegrationTests : IAsyncLifetime
{
    private const string TestMarker = "REZCKOUT-771";

    private string _uniqueSuffix = TestMarker;
    private int _kurumId;
    private int _ilId;
    private int _tesisId;
    private int _musteriKartId;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString))
        {
            return;
        }

        _uniqueSuffix = $"{TestMarker}-{Guid.NewGuid():N}"[..24];

        await using var db = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var (kurum, il, tesis) = await SatisBelgesiMuhasebeTestSupport.SeedKurumIlTesisAsync(db, _uniqueSuffix);
        _kurumId = kurum.Id;
        _ilId = il.Id;
        _tesisId = tesis.Id;

        var gelirHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.GelirSatis, "GELIR", _tesisId);
        var kdvHesap = SatisBelgesiMuhasebeTestSupport.BuildAnaKodHesap(_uniqueSuffix, MuhasebeAnaHesapKodlari.KDVHesaplanan, "KDVS", _tesisId);
        var musteriHesap = SatisBelgesiMuhasebeTestSupport.BuildHesap(_uniqueSuffix, "MUS", _tesisId);
        db.MuhasebeHesapPlanlari.AddRange(gelirHesap, kdvHesap, musteriHesap);
        await db.SaveChangesAsync();

        var musteri = SatisBelgesiMuhasebeTestSupport.BuildCariKart(
            _uniqueSuffix, "MUS", CariKartTipleri.Musteri, _tesisId, musteriHesap.Id);
        db.CariKartlar.Add(musteri);
        await db.SaveChangesAsync();
        _musteriKartId = musteri.Id;
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(SatisBelgesiMuhasebeTestSupport.ConnectionString) || _kurumId <= 0)
        {
            return;
        }

        await using var db = SatisBelgesiMuhasebeTestSupport.CreateDbContext();

        // Kaynaktan-taslak akışında BelgeNo otomatik üretildiği için suffix içermez — belgeyi KaynakId
        // üzerinden bulup fiş/cari hareket/belgeyi FK sırasına uygun temizle (CleanupAsync BelgeNo
        // bazlı çalıştığı için bu belgeyi YAKALAYAMAZ).
        var belgeIds = await db.SatisBelgeleri.IgnoreQueryFilters()
            .Where(x => x.KaynakId != null && x.KaynakId.Contains(_uniqueSuffix))
            .Select(x => x.Id)
            .ToListAsync();

        if (belgeIds.Count > 0)
        {
            await db.CariHareketler
                .Where(x => x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi && x.KaynakId != null && belgeIds.Contains(x.KaynakId.Value))
                .ExecuteDeleteAsync();

            var fisIds = await db.MuhasebeFisler.IgnoreQueryFilters()
                .Where(x => x.KaynakId != null && belgeIds.Contains(x.KaynakId.Value))
                .Select(x => x.Id)
                .ToListAsync();

            // SatisBelgeleri, MuhasebeFisler'e Restrict FK (MuhasebeFisId) ile bağlıdır — bu yüzden
            // fişlerden/fiş satırlarından ÖNCE silinmelidir.
            await db.SatisBelgeleri.IgnoreQueryFilters().Where(x => belgeIds.Contains(x.Id)).ExecuteDeleteAsync();

            if (fisIds.Count > 0)
            {
                await db.MuhasebeFisSatirlari.IgnoreQueryFilters().Where(x => fisIds.Contains(x.MuhasebeFisId)).ExecuteDeleteAsync();
                await db.MuhasebeFisler.IgnoreQueryFilters().Where(x => fisIds.Contains(x.Id)).ExecuteDeleteAsync();
            }
        }

        await SatisBelgesiMuhasebeTestSupport.CleanupAsync(db, _uniqueSuffix, _tesisId, _kurumId, _ilId);
    }

    private static SatisBelgesiTaslakOlusturmaService CreateTaslakService(
        ISatisBelgesiService satisService, StysAppDbContext db)
    {
        var mapper = SatisBelgesiMuhasebeTestSupport.CreateMapper();
        return new SatisBelgesiTaslakOlusturmaService(
            satisService,
            new SatisBelgesiRepository(db, mapper),
            new SatisBelgesiMuhasebeTestSupport.FakeUserAccessScopeService(),
            NullLogger<SatisBelgesiTaslakOlusturmaService>.Instance);
    }

    private SatisBelgesiTaslakOlusturRequest BuildTaslakRequest(bool rezervasyonCheckoutAkisiMi) => new()
    {
        KaynakModul = SatisKaynakModulu.Otel,
        KaynakTipi = TicariBelgeIslemYetkisi.RezervasyonCheckoutKaynakTipi,
        KaynakId = $"REZ-{_uniqueSuffix}",
        RezervasyonCheckoutAkisiMi = rezervasyonCheckoutAkisiMi,
        TesisId = _tesisId,
        CariKartId = _musteriKartId,
        BelgeTarihi = new DateTime(2026, 3, 1),
        MusteriAdSoyad = "Test Musteri " + _uniqueSuffix,
        Satirlar =
        [
            new SatisBelgesiTaslakSatirRequest
            {
                SatirTipi = SatisBelgesiSatirTipi.Konaklama,
                Aciklama = "Konaklama geliri",
                Miktar = 1,
                BirimFiyat = 1000m,
                KdvUygulamaTipi = KdvUygulamaTipi.Kdvli,
                KdvOrani = 20m
            }
        ]
    };

    [IntegrationFact]
    public async Task GuvenilirAkis_RezervasyonCheckoutFaturaTaslagi_FisVeCariHareketUretir()
    {
        await using var db = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(db);
        var taslakService = CreateTaslakService(satisService, db);

        // 1. Server-trusted rezervasyon akışı: RezervasyonCheckoutAkisiMi=true ile taslak oluştur.
        var belge = await taslakService.KaynaktanTaslakOlusturAsync(BuildTaslakRequest(rezervasyonCheckoutAkisiMi: true));
        Assert.Equal(SatisBelgesiTipi.FaturaTaslagi, belge.BelgeTipi);

        // 2. Muhasebe onayına gönder + onayla.
        await satisService.MuhasebeOnayinaGonderAsync(belge.Id!.Value);
        await satisService.MuhasebeOnaylaAsync(belge.Id!.Value);

        // 3. Muhasebe fişini oluştur.
        var fisService = SatisBelgesiMuhasebeTestSupport.CreateMuhasebeFisService(db);
        await fisService.MuhasebeFisiOlusturAsync(belge.Id!.Value, CancellationToken.None);

        var guncel = await satisService.GetByIdAsync(belge.Id!.Value);
        Assert.True(guncel.MuhasebeFisId.HasValue);

        // 4. SatisBelgesi kaynaklı AKTİF cari hareket oluşmuş olmalı.
        var hareketler = await db.CariHareketler.AsNoTracking()
            .Where(x => x.KaynakId == belge.Id!.Value
                        && x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi
                        && x.Durum == CariHareketDurumlari.Aktif)
            .ToListAsync();
        Assert.Single(hareketler);
    }

    [IntegrationFact]
    public async Task GenericKaynaktanTaslak_RezervasyonCheckoutSpoofu_Reddedilir()
    {
        await using var db = SatisBelgesiMuhasebeTestSupport.CreateDbContext();
        var satisService = SatisBelgesiMuhasebeTestSupport.CreateSatisBelgesiService(db);
        var taslakService = CreateTaslakService(satisService, db);

        // Server-trusted bayrağı OLMADAN (client'ın [JsonIgnore] nedeniyle set edemediği flag)
        // "Otel + RezervasyonCheckout" gönderimi reddedilmeli.
        var ex = await Assert.ThrowsAsync<BaseException>(
            () => taslakService.KaynaktanTaslakOlusturAsync(BuildTaslakRequest(rezervasyonCheckoutAkisiMi: false)));
        Assert.Equal(400, ex.ErrorCode);
    }
}
