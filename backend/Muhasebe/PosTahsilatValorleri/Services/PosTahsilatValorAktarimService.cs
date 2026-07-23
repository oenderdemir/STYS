using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Services;
using STYS.Muhasebe.PosTahsilatValorleri.Dtos;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.PosTahsilatValorleri.Services;

/// <summary>
/// POS -> Banka valor aktarim orkestratoru. Iki asamali claim/lease deseni kullanir (Adim A:
/// kisa transaction'da satir kilidiyle claim + onceki durumu yakalama; Adim B: ayri transaction'da
/// is kurallari + fis olusturma + fis onaylama). ClaimToken EF concurrency token oldugu icin
/// (bkz. StysAppDbContext.OnModelCreating) her SaveChangesAsync otomatik olarak dogru satiri
/// hedefledigini garanti eder - orphan fis (fis olusup kayit Aktarildi'ye gecmeden kalmasi)
/// yapisal olarak imkansizdir.
///
/// Aktarim, muhasebe etkisini HEMEN dogurdugu icin transfer fisi ayni transaction icinde
/// onaylanir (YevmiyeNo uretilir, hesap bakiyeleri islenir) - Taslak birakilmaz.
///
/// Bu iş bankaya fiziksel para transferi yapmaz; yalnizca STYS icindeki POS alacaginin bagli
/// banka hesabina muhasebe kaydiyla aktarildigini temsil eder.
/// </summary>
public class PosTahsilatValorAktarimService : IPosTahsilatValorAktarimService
{
    private const int StuckDakika = 15;
    private const int AzamiOtomatikDeneme = 5;
    private const int BackoffDakika = 30;

    /// <summary>Mukerrer/yarisma kaynakli hatalar icin ozel hata kodu - bu kod HesabaAktarAsync'in
    /// disaridaki catch blogunda DenemeSayisi'nin ARTIRILMAMASI gerektigini isaret eder (kayit
    /// muhtemelen baska bir islem tarafindan zaten sonuclandirilmis).</summary>
    private const int MukerrerNoPenaltyErrorCode = 499;

    private readonly StysAppDbContext _dbContext;
    private readonly IMuhasebeDonemService _muhasebeDonemService;
    private readonly IMuhasebeFisService _muhasebeFisService;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly ILogger<PosTahsilatValorAktarimService> _logger;

    public PosTahsilatValorAktarimService(
        StysAppDbContext dbContext,
        IMuhasebeDonemService muhasebeDonemService,
        IMuhasebeFisService muhasebeFisService,
        IUserAccessScopeService userAccessScopeService,
        ILogger<PosTahsilatValorAktarimService> logger)
    {
        _dbContext = dbContext;
        _muhasebeDonemService = muhasebeDonemService;
        _muhasebeFisService = muhasebeFisService;
        _userAccessScopeService = userAccessScopeService;
        _logger = logger;
    }

    public async Task<PosTahsilatValorAktarimSonucDto> HesabaAktarAsync(int id, ManuelAktarimGuncellemeDto? guncelleme, CancellationToken cancellationToken = default)
    {
        // Ön-kontrol: side-effect yok. Valör tarihi gelmemiş, kalıcı bir durumdaysa veya
        // kullanıcının bu tesise erişim yetkisi yoksa hiçbir alan değiştirilmeden hata döner.
        var onKontrol = await _dbContext.PosTahsilatValorleri.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Valör kaydı bulunamadı.", 404);

        await EnsureTesisErisimiAsync(onKontrol.TesisId, cancellationToken);

        if (onKontrol.Durum is PosTahsilatValorDurumlari.Aktarildi or PosTahsilatValorDurumlari.Iptal or PosTahsilatValorDurumlari.AktarimFisiIptalEdildi)
        {
            throw new BaseException("Kayıt bu durumda aktarılamaz.", 409);
        }

        if (onKontrol.BeklenenValorTarihi > ValorTarihHesaplamaService.BugunIstanbul())
        {
            throw new BaseException("Valör tarihi henüz gelmedi.", 422);
        }

        var komisyonBilgisiVerildi = guncelleme?.KomisyonTutari.HasValue == true || guncelleme?.NetTutar.HasValue == true;
        if (onKontrol.Durum == PosTahsilatValorDurumlari.MutabakatBekliyor && !komisyonBilgisiVerildi)
        {
            throw new BaseException("Bu kayıt için komisyon tutarı belirsiz; lütfen manuel aktarımda komisyon/net tutarını girin.", 422);
        }

        // Adım A - claim (kısa transaction, satır kilidiyle önceki durumu yakalar).
        var token = Guid.NewGuid();
        (string OncekiDurum, int OncekiDenemeSayisi, string? OncekiHataMesaji, DateTime? OncekiSonDenemeTarihi) onceki;

        await using (var claimTx = await _dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var stuckEsigi = DateTime.UtcNow.AddMinutes(-StuckDakika);
            var backoffEsigi = DateTime.UtcNow.AddMinutes(-BackoffDakika);

            var mevcut = await _dbContext.PosTahsilatValorleri
                .FromSqlInterpolated($@"
SELECT * FROM [muhasebe].[PosTahsilatValorleri] WITH (UPDLOCK, ROWLOCK)
WHERE [Id] = {id} AND [IsDeleted] = 0")
                .FirstOrDefaultAsync(cancellationToken);

            if (mevcut is null)
            {
                await claimTx.RollbackAsync(cancellationToken);
                throw new BaseException("Valör kaydı bulunamadı.", 404);
            }

            var uygun =
                mevcut.Durum == PosTahsilatValorDurumlari.ValorBekliyor
                || (mevcut.Durum == PosTahsilatValorDurumlari.MutabakatBekliyor && komisyonBilgisiVerildi)
                || (mevcut.Durum == PosTahsilatValorDurumlari.Hata && mevcut.DenemeSayisi < AzamiOtomatikDeneme && (mevcut.SonDenemeTarihi is null || mevcut.SonDenemeTarihi < backoffEsigi))
                || (mevcut.Durum == PosTahsilatValorDurumlari.Aktariliyor && mevcut.AktarimBaslamaTarihi is not null && mevcut.AktarimBaslamaTarihi < stuckEsigi);

            if (!uygun)
            {
                await claimTx.RollbackAsync(cancellationToken);
                throw new BaseException("Kayıt şu an aktarılamaz durumda.", 409);
            }

            onceki = (mevcut.Durum, mevcut.DenemeSayisi, mevcut.HataMesaji, mevcut.SonDenemeTarihi);

            mevcut.Durum = PosTahsilatValorDurumlari.Aktariliyor;
            mevcut.AktarimBaslamaTarihi = DateTime.UtcNow;
            mevcut.ClaimToken = token;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await claimTx.CommitAsync(cancellationToken);
        }

        // Adım B - iş (ayrı transaction, satır kilidini yeniden alır).
        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var entity = await _dbContext.PosTahsilatValorleri
                .FromSqlInterpolated($@"
SELECT * FROM [muhasebe].[PosTahsilatValorleri] WITH (UPDLOCK, ROWLOCK)
WHERE [Id] = {id} AND [Durum] = {PosTahsilatValorDurumlari.Aktariliyor} AND [ClaimToken] = {token}")
                .Include(x => x.KrediKartiHesap)
                .Include(x => x.BagliBankaHesap)
                .Include(x => x.TahsilatOdemeBelgesi)
                .FirstOrDefaultAsync(cancellationToken);

            if (entity is null)
            {
                await tx.RollbackAsync(cancellationToken);
                throw new BaseException("İşlem başka bir süreç tarafından devralınmış.", 409);
            }

            await ApplyGuncellemeVeAuditAsync(entity, guncelleme, cancellationToken);
            await ValidateVeFisOlusturAsync(entity, cancellationToken);

            entity.Durum = PosTahsilatValorDurumlari.Aktarildi;
            entity.AktarimTarihi = DateTime.UtcNow;
            entity.HataMesaji = null;
            entity.ClaimToken = null;
            entity.AktarimBaslamaTarihi = null;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return new PosTahsilatValorAktarimSonucDto { Id = id, Basarili = true, MuhasebeFisId = entity.MuhasebeFisId };
        }
        catch (BaseException ex) when (ex.ErrorCode == 422)
        {
            // Kullanıcı hatası: transaction rollback (entity degisiklikleri geri alinir), AMA
            // Adim A'nin claim'i (Durum=Aktariliyor) AYRI, zaten commit edilmis bir transaction'daydi
            // - bu yuzden burada da KosulluGuncelleAsync ile aciklikca onceki duruma DONULMELIDIR,
            // aksi halde kayit Aktariliyor'da "takili" kalir. Hata/DenemeSayisi YAZILMAZ.
            using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await SafeRollbackAsync(tx, cleanupCts.Token);
            await KosulluGuncelleAsync(id, token, onceki.OncekiDurum, onceki.OncekiDenemeSayisi, onceki.OncekiHataMesaji, onceki.OncekiSonDenemeTarihi, cleanupCts.Token);
            throw;
        }
        catch (OperationCanceledException)
        {
            using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await SafeRollbackAsync(tx, cleanupCts.Token);
            var iadeDurumu = onceki.OncekiDurum == PosTahsilatValorDurumlari.Aktariliyor ? PosTahsilatValorDurumlari.Hata : onceki.OncekiDurum;
            var iadeDenemeSayisi = onceki.OncekiDurum == PosTahsilatValorDurumlari.Aktariliyor ? onceki.OncekiDenemeSayisi + 1 : onceki.OncekiDenemeSayisi;
            var iadeHataMesaji = onceki.OncekiDurum == PosTahsilatValorDurumlari.Aktariliyor
                ? "Önceki aktarım denemesi sırasında bağlantı kesildi/uygulama durduruldu; kayıt tekrar denenebilir."
                : onceki.OncekiHataMesaji;

            await KosulluGuncelleAsync(id, token, iadeDurumu, iadeDenemeSayisi, iadeHataMesaji, onceki.OncekiSonDenemeTarihi, cleanupCts.Token);
            throw;
        }
        catch (Exception ex)
        {
            using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await SafeRollbackAsync(tx, cleanupCts.Token);

            var mesaj = ex is BaseException be ? be.Message : "Aktarım sırasında beklenmeyen bir hata oluştu.";
            var yapilandirmaHatasi = ex is BaseException { ErrorCode: 409 };
            var mukerrerNoPenalty = ex is BaseException { ErrorCode: MukerrerNoPenaltyErrorCode };
            var yeniDenemeSayisi = mukerrerNoPenalty
                ? onceki.OncekiDenemeSayisi
                : (yapilandirmaHatasi ? AzamiOtomatikDeneme : onceki.OncekiDenemeSayisi + 1);

            await KosulluGuncelleAsync(id, token, PosTahsilatValorDurumlari.Hata, yeniDenemeSayisi, mesaj, DateTime.UtcNow, cleanupCts.Token);

            _logger.LogWarning(ex, "POS valör aktarımı başarısız: {Id}", id);
            return new PosTahsilatValorAktarimSonucDto { Id = id, Basarili = false, HataMesaji = mesaj };
        }
    }

    /// <summary>
    /// Manuel komisyon/net/hesap override'larini kilitli entity uzerine uygular; GERCEK bir
    /// degisiklik varsa (no-op degilse) her degisen alan icin PosTahsilatValorDegisiklikGecmisi
    /// satiri ekler (ayni transaction, tek SaveChangesAsync ile birlikte kaydedilir) ve zorunlu
    /// aciklamayi dogrular. Uygulanan tutarlar icin Brut>0, 0&lt;=Komisyon&lt;=Brut,
    /// 0&lt;=Net&lt;=Brut ve Brut=Net+Komisyon kurallarini denetler.
    /// </summary>
    private async Task ApplyGuncellemeVeAuditAsync(PosTahsilatValor entity, ManuelAktarimGuncellemeDto? guncelleme, CancellationToken cancellationToken)
    {
        var kullaniciGirdisiVarMi = guncelleme is not null
            && (guncelleme.KomisyonTutari.HasValue || guncelleme.NetTutar.HasValue || guncelleme.KomisyonGiderHesapPlaniIdOverride.HasValue);

        if (guncelleme is not null)
        {
            var eskiKomisyon = entity.KomisyonTutari;
            var eskiNet = entity.NetTutar;
            var eskiKomisyonHesap = entity.KomisyonGiderHesapPlaniId;

            if (guncelleme.KomisyonTutari.HasValue && !guncelleme.NetTutar.HasValue)
            {
                entity.KomisyonTutari = ParaTutarYuvarlamaHelper.Yuvarla(guncelleme.KomisyonTutari.Value);
                entity.NetTutar = ParaTutarYuvarlamaHelper.Yuvarla(entity.BrutTutar - entity.KomisyonTutari);
            }
            else if (guncelleme.NetTutar.HasValue && !guncelleme.KomisyonTutari.HasValue)
            {
                entity.NetTutar = ParaTutarYuvarlamaHelper.Yuvarla(guncelleme.NetTutar.Value);
                entity.KomisyonTutari = ParaTutarYuvarlamaHelper.Yuvarla(entity.BrutTutar - entity.NetTutar);
            }
            else if (guncelleme.KomisyonTutari.HasValue && guncelleme.NetTutar.HasValue)
            {
                entity.KomisyonTutari = ParaTutarYuvarlamaHelper.Yuvarla(guncelleme.KomisyonTutari.Value);
                entity.NetTutar = ParaTutarYuvarlamaHelper.Yuvarla(guncelleme.NetTutar.Value);
            }

            if (guncelleme.KomisyonGiderHesapPlaniIdOverride.HasValue)
            {
                entity.KomisyonGiderHesapPlaniId = guncelleme.KomisyonGiderHesapPlaniIdOverride;
            }

            if (kullaniciGirdisiVarMi)
            {
                ValidateTutarlar(entity, kullaniciGirdisiVarMi: true);
            }

            var degisenAlanlar = new List<(string IslemTipi, object? Eski, object? Yeni)>();
            if (eskiKomisyon != entity.KomisyonTutari)
            {
                degisenAlanlar.Add(("ManuelKomisyonDuzenleme", eskiKomisyon, entity.KomisyonTutari));
            }
            if (eskiNet != entity.NetTutar)
            {
                degisenAlanlar.Add(("ManuelNetDuzenleme", eskiNet, entity.NetTutar));
            }
            if (eskiKomisyonHesap != entity.KomisyonGiderHesapPlaniId)
            {
                degisenAlanlar.Add(("KomisyonHesabiDegisikligi", eskiKomisyonHesap, entity.KomisyonGiderHesapPlaniId));
            }

            if (degisenAlanlar.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(guncelleme.Aciklama))
                {
                    throw new BaseException("Komisyon/net tutarı veya komisyon hesabı değiştiriliyorsa açıklama zorunludur.", 422);
                }

                entity.Aciklama = guncelleme.Aciklama;

                foreach (var (islemTipi, eski, yeni) in degisenAlanlar)
                {
                    await _dbContext.PosTahsilatValorDegisiklikGecmisleri.AddAsync(new PosTahsilatValorDegisiklikGecmisi
                    {
                        PosTahsilatValorId = entity.Id,
                        IslemTipi = islemTipi,
                        Aciklama = guncelleme.Aciklama,
                        OncekiDegerJson = System.Text.Json.JsonSerializer.Serialize(eski),
                        YeniDegerJson = System.Text.Json.JsonSerializer.Serialize(yeni)
                    }, cancellationToken);
                }
            }
            else if (!string.IsNullOrWhiteSpace(guncelleme.Aciklama))
            {
                entity.Aciklama = guncelleme.Aciklama;
            }
        }

        // Snapshot degerleri (override verilmemis kayitlar dahil) her zaman kurallara uymali -
        // bu, bozuk/eski bir snapshot'in fise donusmesini engelleyen son savunma hattidir.
        ValidateTutarlar(entity, kullaniciGirdisiVarMi);
    }

    /// <summary>Brut>0, 0&lt;=Komisyon&lt;=Brut, 0&lt;=Net&lt;=Brut, Brut=Net+Komisyon kurallarini
    /// dogrular. kullaniciGirdisiVarMi true ise (canli kullanici girdisi) 422, degilse (snapshot/
    /// yapilandirma kaynakli) 409 ile hata firlatir.</summary>
    private static void ValidateTutarlar(PosTahsilatValor entity, bool kullaniciGirdisiVarMi)
    {
        var hataKodu = kullaniciGirdisiVarMi ? 422 : 409;

        if (entity.BrutTutar <= 0)
        {
            throw new BaseException("Brüt tutar sıfırdan büyük olmalıdır.", hataKodu);
        }

        if (entity.KomisyonTutari < 0 || entity.KomisyonTutari > entity.BrutTutar)
        {
            throw new BaseException("Komisyon tutarı 0 ile brüt tutar arasında olmalıdır.", hataKodu);
        }

        if (entity.NetTutar < 0 || entity.NetTutar > entity.BrutTutar)
        {
            throw new BaseException("Net tutar 0 ile brüt tutar arasında olmalıdır.", hataKodu);
        }

        if (ParaTutarYuvarlamaHelper.Yuvarla(entity.NetTutar + entity.KomisyonTutari) != ParaTutarYuvarlamaHelper.Yuvarla(entity.BrutTutar))
        {
            throw new BaseException("Brüt tutar = Net tutar + Komisyon tutarı olmalıdır.", hataKodu);
        }
    }

    private async Task ValidateVeFisOlusturAsync(PosTahsilatValor entity, CancellationToken cancellationToken)
    {
        if (entity.TahsilatOdemeBelgesi is null || entity.TahsilatOdemeBelgesi.Durum != TahsilatOdemeBelgeDurumlari.Aktif)
        {
            throw new BaseException("Kaynak tahsilat belgesi aktif değil.", 409);
        }

        if (entity.KrediKartiHesap is null || !entity.KrediKartiHesap.AktifMi)
        {
            throw new BaseException("Kredi kartı/POS hesabı aktif değil.", 409);
        }

        if (entity.BagliBankaHesap is null || !entity.BagliBankaHesap.AktifMi)
        {
            throw new BaseException("Bağlı banka hesabı tanımlı/aktif değil; hesap tanımını kontrol edin.", 409);
        }

        if (entity.KrediKartiHesap.TesisId != entity.BagliBankaHesap.TesisId || entity.KrediKartiHesap.TesisId != entity.TesisId)
        {
            throw new BaseException("Hesaplar aynı tesise ait olmalıdır.", 409);
        }

        if (entity.BagliBankaHesap.ParaBirimi != entity.ParaBirimi)
        {
            throw new BaseException("Bağlı banka hesabı ile para birimi uyumlu değil.", 409);
        }

        if (!entity.KrediKartiHesap.MuhasebeHesapPlaniId.HasValue || !entity.BagliBankaHesap.MuhasebeHesapPlaniId.HasValue)
        {
            throw new BaseException("Hesapların muhasebe hesap planı bağlantısı eksik.", 409);
        }

        var krediKartiPlan = await _dbContext.MuhasebeHesapPlanlari
            .FirstOrDefaultAsync(x => x.Id == entity.KrediKartiHesap.MuhasebeHesapPlaniId!.Value, cancellationToken);

        if (krediKartiPlan is null || !krediKartiPlan.TamKod.StartsWith(MuhasebeAnaHesapKodlari.FinansalKrediKarti))
        {
            throw new BaseException("Seçilen kredi kartı/POS hesabı 109 - Kredi Kartı/POS Alacakları hesap koduna bağlı değil; hesap tanımını kontrol edin.", 409);
        }

        if (entity.KomisyonTutari > 0 && !entity.KomisyonGiderHesapPlaniId.HasValue)
        {
            throw new BaseException("Komisyon tutarı girilmişse komisyon gider hesabı zorunludur.", 409);
        }

        var aktifDonem = await _muhasebeDonemService.GetAktifDonemAsync(entity.TesisId, DateTime.UtcNow, cancellationToken)
            ?? throw new BaseException("Aktarım tarihi için açık muhasebe dönemi bulunamadı.", 409);

        var mevcutFis = await _dbContext.MuhasebeFisler
            .Where(f => !f.IsDeleted
                        && f.KaynakModul == MuhasebeKaynakModulleri.PosTahsilatValorTransferi
                        && f.KaynakId == entity.Id
                        && (f.Durum == MuhasebeFisDurumlari.Taslak || f.Durum == MuhasebeFisDurumlari.Onayli))
            .Select(f => new { f.Id, f.FisNo })
            .FirstOrDefaultAsync(cancellationToken);

        if (mevcutFis is not null)
        {
            throw new BaseException($"Bu valör kaydı için zaten bir muhasebe fişi oluşturulmuş. Mevcut fiş: {mevcutFis.FisNo}", MukerrerNoPenaltyErrorCode);
        }

        var satirlar = new List<MuhasebeFisSatir>
        {
            new()
            {
                MuhasebeHesapPlaniId = entity.BagliBankaHesap.MuhasebeHesapPlaniId!.Value,
                SiraNo = 1,
                Borc = entity.NetTutar,
                Alacak = 0m,
                ParaBirimi = entity.ParaBirimi,
                Kur = 1,
                KasaBankaHesapId = entity.BagliBankaHesapId,
                Aciklama = $"POS valör aktarımı - {entity.KrediKartiHesap.Ad} -> {entity.BagliBankaHesap.Ad}"
            }
        };

        var siraNo = 2;
        if (entity.KomisyonTutari > 0)
        {
            satirlar.Add(new MuhasebeFisSatir
            {
                MuhasebeHesapPlaniId = entity.KomisyonGiderHesapPlaniId!.Value,
                SiraNo = siraNo++,
                Borc = entity.KomisyonTutari,
                Alacak = 0m,
                ParaBirimi = entity.ParaBirimi,
                Kur = 1,
                Aciklama = "POS/Banka komisyon gideri"
            });
        }

        satirlar.Add(new MuhasebeFisSatir
        {
            MuhasebeHesapPlaniId = krediKartiPlan.Id,
            SiraNo = siraNo,
            Borc = 0m,
            Alacak = entity.BrutTutar,
            ParaBirimi = entity.ParaBirimi,
            Kur = 1,
            KasaBankaHesapId = entity.KrediKartiHesapId,
            Aciklama = $"POS valör aktarımı - kaynak belge: {entity.TahsilatOdemeBelgesi.BelgeNo}"
        });

        // Fis insertinden hemen once son savunma: negatif satir tutari kesinlikle kabul edilmez,
        // gercek satir toplamlarinda ToplamBorc=ToplamAlacak olmalidir.
        if (satirlar.Any(s => s.Borc < 0 || s.Alacak < 0))
        {
            throw new BaseException("Fiş satırlarında negatif tutar olamaz.", 500);
        }

        var toplamBorc = satirlar.Sum(s => s.Borc);
        var toplamAlacak = satirlar.Sum(s => s.Alacak);
        if (ParaTutarYuvarlamaHelper.Yuvarla(toplamBorc) != ParaTutarYuvarlamaHelper.Yuvarla(toplamAlacak))
        {
            throw new BaseException("Fiş satır toplamları (Borç/Alacak) eşit değil; fiş oluşturulamadı.", 500);
        }

        var fisNo = await GenerateFisNoAsync(entity.TesisId, aktifDonem.MaliYil, cancellationToken);

        var fis = new MuhasebeFis
        {
            TesisId = entity.TesisId,
            MaliYil = aktifDonem.MaliYil,
            Donem = aktifDonem.DonemNo,
            FisNo = fisNo,
            FisTarihi = DateTime.UtcNow.Date,
            FisTipi = MuhasebeFisTipleri.Mahsup,
            KaynakModul = MuhasebeKaynakModulleri.PosTahsilatValorTransferi,
            KaynakId = entity.Id,
            Durum = MuhasebeFisDurumlari.Taslak,
            Aciklama = $"POS→Banka valör aktarımı - {entity.KrediKartiHesap.Ad} → {entity.BagliBankaHesap.Ad} - Kaynak belge: {entity.TahsilatOdemeBelgesi.BelgeNo}",
            ToplamBorc = toplamBorc,
            ToplamAlacak = toplamAlacak,
            Satirlar = satirlar
        };

        try
        {
            await _dbContext.MuhasebeFisler.AddAsync(fis, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            var tip = ClassifyFisConflict(ex);
            if (tip is FisConflictType.KaynakMukerrer or FisConflictType.FisNoCakismasi)
            {
                // Bu valor kaydi icin fis zaten baska bir eszamanli islem tarafindan olusturulmus
                // olabilir - bu durum "hata" degil, "mukerrer" olarak siniflandirilir; DenemeSayisi
                // TUKETILMEZ (bkz. HesabaAktarAsync'in disaridaki catch blogu).
                throw new BaseException("Bu valör kaydı için zaten bir muhasebe fişi oluşturulmuş veya eşzamanlı bir işlem tespit edildi.", MukerrerNoPenaltyErrorCode);
            }

            throw;
        }

        entity.MuhasebeFisId = fis.Id;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Is kurali: aktarimin muhasebe etkisi HEMEN dogar - fis Taslak birakilmaz, ayni
        // transaction icinde onaylanir (YevmiyeNo uretilir, hesap bakiyeleri islenir).
        await _muhasebeFisService.OnaylaAsync(fis.Id, cancellationToken);
    }

    private enum FisConflictType { KaynakMukerrer, FisNoCakismasi, Diger }

    private static FisConflictType ClassifyFisConflict(DbUpdateException ex)
    {
        if (!IsUniqueConflict(ex))
        {
            return FisConflictType.Diger;
        }

        var mesaj = ex.InnerException?.Message ?? string.Empty;
        if (mesaj.Contains("IX_MuhasebeFisler_TesisId_KaynakModul_KaynakId", StringComparison.OrdinalIgnoreCase)
            || mesaj.Contains("IX_MuhasebeFisler_KaynakModul_KaynakId", StringComparison.OrdinalIgnoreCase))
        {
            return FisConflictType.KaynakMukerrer;
        }

        if (mesaj.Contains("IX_MuhasebeFisler_TesisId_FisNo", StringComparison.OrdinalIgnoreCase))
        {
            return FisConflictType.FisNoCakismasi;
        }

        return FisConflictType.Diger;
    }

    /// <summary>
    /// Tesis+MaliYil bazli, eszamanliliga guvenli fis no sayaci. MuhasebeFisService.
    /// YevmiyeNoUretAsync ile ayni WITH (UPDLOCK, ROWLOCK, HOLDLOCK) + retry deseni - eski
    /// Max(FisNo)+1 yaklasimi yarisa acikti.
    /// </summary>
    private async Task<string> GenerateFisNoAsync(int tesisId, int maliYil, CancellationToken cancellationToken)
    {
        var prefix = $"{maliYil}-VLR-";

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var sayac = await _dbContext.PosValorFisNoSayaclari
                .FromSqlInterpolated($@"
SELECT * FROM [muhasebe].[PosValorFisNoSayaclari] WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
WHERE [IsDeleted] = 0 AND [TesisId] = {tesisId} AND [MaliYil] = {maliYil}")
                .FirstOrDefaultAsync(cancellationToken);

            if (sayac is null)
            {
                var created = new PosValorFisNoSayac { TesisId = tesisId, MaliYil = maliYil, SonNumara = 1 };
                await _dbContext.PosValorFisNoSayaclari.AddAsync(created, cancellationToken);
                try
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    return $"{prefix}{created.SonNumara:D6}";
                }
                catch (DbUpdateException ex) when (attempt < 3 && IsUniqueConflict(ex))
                {
                    _dbContext.Entry(created).State = EntityState.Detached;
                    continue;
                }
            }
            else
            {
                sayac.SonNumara += 1;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return $"{prefix}{sayac.SonNumara:D6}";
            }
        }

        throw new BaseException("Fiş numarası üretilemedi. Lütfen tekrar deneyiniz.", 500);
    }

    private async Task KosulluGuncelleAsync(int id, Guid token, string durum, int denemeSayisi, string? hataMesaji, DateTime? sonDenemeTarihi, CancellationToken cancellationToken)
    {
        var etkilenen = await _dbContext.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE [muhasebe].[PosTahsilatValorleri]
SET [Durum] = {durum}, [DenemeSayisi] = {denemeSayisi}, [HataMesaji] = {hataMesaji},
    [SonDenemeTarihi] = {sonDenemeTarihi}, [ClaimToken] = NULL, [AktarimBaslamaTarihi] = NULL
WHERE [Id] = {id} AND [Durum] = {PosTahsilatValorDurumlari.Aktariliyor} AND [ClaimToken] = {token}",
            cancellationToken);

        if (etkilenen == 0)
        {
            _logger.LogInformation("POS valör kaydı {Id} zaten başka bir süreç tarafından sonuçlandırılmış, üzerine yazılmadı.", id);
        }
    }

    private async Task EnsureTesisErisimiAsync(int tesisId, CancellationToken cancellationToken)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && !scope.TesisIds.Contains(tesisId))
        {
            throw new BaseException("Bu kayıt için yetkiniz bulunmuyor.", 403);
        }
    }

    private static async Task SafeRollbackAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, CancellationToken cancellationToken)
    {
        try
        {
            await tx.RollbackAsync(cancellationToken);
        }
        catch
        {
            // Zaten rollback/commit olmus olabilir - yut.
        }
    }

    private static bool IsUniqueConflict(DbUpdateException ex)
    {
        return ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627);
    }

    public async Task<PosTahsilatValorToplamAktarimSonucDto> SeciliHesaplaraAktarAsync(List<int> valorIdler, CancellationToken cancellationToken = default)
    {
        var sonuc = new PosTahsilatValorToplamAktarimSonucDto();
        foreach (var id in valorIdler)
        {
            await AktarBirTaneVeTopla(id, null, sonuc, cancellationToken);
        }
        return sonuc;
    }

    public async Task<PosTahsilatValorToplamAktarimSonucDto> ValoruGelenleriHesabaAktarAsync(int? tesisId, CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && tesisId.HasValue && !scope.TesisIds.Contains(tesisId.Value))
        {
            throw new BaseException("Seçilen tesis için yetkiniz bulunmuyor.", 403);
        }

        var bugun = ValorTarihHesaplamaService.BugunIstanbul();
        var query = _dbContext.PosTahsilatValorleri.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Durum == PosTahsilatValorDurumlari.ValorBekliyor && x.BeklenenValorTarihi <= bugun);

        if (tesisId.HasValue)
        {
            query = query.Where(x => x.TesisId == tesisId.Value);
        }
        else if (scope.IsScoped)
        {
            query = query.Where(x => scope.TesisIds.Contains(x.TesisId));
        }

        var idler = await query.Select(x => x.Id).ToListAsync(cancellationToken);

        var sonuc = new PosTahsilatValorToplamAktarimSonucDto();
        foreach (var id in idler)
        {
            await AktarBirTaneVeTopla(id, null, sonuc, cancellationToken);
        }
        return sonuc;
    }

    private async Task AktarBirTaneVeTopla(int id, ManuelAktarimGuncellemeDto? guncelleme, PosTahsilatValorToplamAktarimSonucDto sonuc, CancellationToken cancellationToken)
    {
        try
        {
            var tek = await HesabaAktarAsync(id, guncelleme, cancellationToken);
            if (tek.Basarili)
            {
                sonuc.Basarili.Add(tek);
            }
            else
            {
                sonuc.Hatali.Add(tek);
            }
        }
        catch (BaseException ex)
        {
            sonuc.Hatali.Add(new PosTahsilatValorAktarimSonucDto { Id = id, Basarili = false, HataMesaji = ex.Message });
        }
    }

    public async Task<PosTahsilatValorAktarimSonucDto> YenidenDeneAsync(int id, CancellationToken cancellationToken = default)
    {
        return await HesabaAktarAsync(id, null, cancellationToken);
    }

    public async Task<PosTahsilatValorAktarimSonucDto> DuzeltmeTersKayitAsync(int id, string aciklama, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(aciklama))
        {
            throw new BaseException("Düzeltme/ters kayıt için açıklama zorunludur.", 422);
        }

        var onKontrol = await _dbContext.PosTahsilatValorleri.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Valör kaydı bulunamadı.", 404);

        await EnsureTesisErisimiAsync(onKontrol.TesisId, cancellationToken);

        var token = Guid.NewGuid();

        await using (var claimTx = await _dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var stuckEsigi = DateTime.UtcNow.AddMinutes(-StuckDakika);

            var mevcut = await _dbContext.PosTahsilatValorleri
                .FromSqlInterpolated($@"
SELECT * FROM [muhasebe].[PosTahsilatValorleri] WITH (UPDLOCK, ROWLOCK)
WHERE [Id] = {id} AND [IsDeleted] = 0")
                .FirstOrDefaultAsync(cancellationToken);

            if (mevcut is null)
            {
                await claimTx.RollbackAsync(cancellationToken);
                throw new BaseException("Valör kaydı bulunamadı.", 404);
            }

            var uygun = mevcut.Durum == PosTahsilatValorDurumlari.Aktarildi
                || (mevcut.Durum == PosTahsilatValorDurumlari.TersKayitOlusturuluyor && mevcut.AktarimBaslamaTarihi is not null && mevcut.AktarimBaslamaTarihi < stuckEsigi);

            if (!uygun)
            {
                await claimTx.RollbackAsync(cancellationToken);
                throw new BaseException("Düzeltme/ters kayıt işlemi sürüyor, birkaç dakika sonra tekrar deneyin.", 409);
            }

            mevcut.Durum = PosTahsilatValorDurumlari.TersKayitOlusturuluyor;
            mevcut.AktarimBaslamaTarihi = DateTime.UtcNow;
            mevcut.ClaimToken = token;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await claimTx.CommitAsync(cancellationToken);
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var entity = await _dbContext.PosTahsilatValorleri
                .FromSqlInterpolated($@"
SELECT * FROM [muhasebe].[PosTahsilatValorleri] WITH (UPDLOCK, ROWLOCK)
WHERE [Id] = {id} AND [Durum] = {PosTahsilatValorDurumlari.TersKayitOlusturuluyor} AND [ClaimToken] = {token}")
                .FirstOrDefaultAsync(cancellationToken);

            if (entity is null || !entity.MuhasebeFisId.HasValue)
            {
                await tx.RollbackAsync(cancellationToken);
                throw new BaseException("İşlem başka bir süreç tarafından devralınmış.", 409);
            }

            var sonuc = await _muhasebeFisService.PosValorTransferFisiniIptalEtAsync(
                entity.MuhasebeFisId.Value, entity.Id, entity.TesisId, aciklama, cancellationToken);

            entity.TersKayitMuhasebeFisId = sonuc.TersKayitFisId;
            entity.Durum = PosTahsilatValorDurumlari.AktarimFisiIptalEdildi;
            entity.ClaimToken = null;
            entity.AktarimBaslamaTarihi = null;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return new PosTahsilatValorAktarimSonucDto { Id = id, Basarili = true, MuhasebeFisId = sonuc.TersKayitFisId };
        }
        catch (Exception ex)
        {
            using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await SafeRollbackAsync(tx, cleanupCts.Token);

            // duzeltme-ters-kayit her zaman Aktarildi'den baslar; kurtarma dali disinda oncekiDurum
            // sabittir. Henuz hicbir muhasebe etkisi olusmamis olabilecegi icin Aktarildi'ye donmek
            // guvenlidir (Hata'ya dusurulmez - bu bir POS aktarim denemesi degil, admin duzeltmesidir).
            await _dbContext.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE [muhasebe].[PosTahsilatValorleri]
SET [Durum] = {PosTahsilatValorDurumlari.Aktarildi}, [ClaimToken] = NULL, [AktarimBaslamaTarihi] = NULL
WHERE [Id] = {id} AND [Durum] = {PosTahsilatValorDurumlari.TersKayitOlusturuluyor} AND [ClaimToken] = {token}",
                cleanupCts.Token);

            _logger.LogWarning(ex, "POS valör düzeltme/ters kayıt başarısız: {Id}", id);
            throw;
        }
    }
}
