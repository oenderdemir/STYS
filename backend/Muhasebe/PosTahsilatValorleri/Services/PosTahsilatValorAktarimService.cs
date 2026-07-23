using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
/// is kurallari + fis olusturma). ClaimToken EF concurrency token oldugu icin (bkz.
/// StysAppDbContext.OnModelCreating) her SaveChangesAsync otomatik olarak dogru satiri
/// hedefledigini garanti eder - orphan fis (fis olusup kayit Aktarildi'ye gecmeden kalmasi)
/// yapisal olarak imkansizdir.
///
/// Bu iş bankaya fiziksel para transferi yapmaz; yalnizca STYS icindeki POS alacaginin bagli
/// banka hesabina muhasebe kaydiyla aktarildigini temsil eder.
/// </summary>
public class PosTahsilatValorAktarimService : IPosTahsilatValorAktarimService
{
    private const int StuckDakika = 15;
    private const int AzamiOtomatikDeneme = 5;
    private const int BackoffDakika = 30;

    private readonly StysAppDbContext _dbContext;
    private readonly IMuhasebeDonemService _muhasebeDonemService;
    private readonly IMuhasebeFisService _muhasebeFisService;
    private readonly ILogger<PosTahsilatValorAktarimService> _logger;

    public PosTahsilatValorAktarimService(
        StysAppDbContext dbContext,
        IMuhasebeDonemService muhasebeDonemService,
        IMuhasebeFisService muhasebeFisService,
        ILogger<PosTahsilatValorAktarimService> logger)
    {
        _dbContext = dbContext;
        _muhasebeDonemService = muhasebeDonemService;
        _muhasebeFisService = muhasebeFisService;
        _logger = logger;
    }

    public async Task<PosTahsilatValorAktarimSonucDto> HesabaAktarAsync(int id, ManuelAktarimGuncellemeDto? guncelleme, CancellationToken cancellationToken = default)
    {
        // Ön-kontrol: side-effect yok. Valör tarihi gelmemiş veya kalıcı bir durumdaysa hiçbir
        // alan değiştirilmeden hata döner.
        var onKontrol = await _dbContext.PosTahsilatValorleri.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Valör kaydı bulunamadı.", 404);

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

        if (guncelleme?.KomisyonTutari.HasValue == true && guncelleme.NetTutar.HasValue
            && ParaTutarYuvarlamaHelper.Yuvarla(guncelleme.KomisyonTutari.Value + guncelleme.NetTutar.Value) != ParaTutarYuvarlamaHelper.Yuvarla(onKontrol.BrutTutar))
        {
            throw new BaseException("Brüt tutar = Net tutar + Komisyon tutarı olmalıdır.", 422);
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

            // Manuel override / otomatik tamamlama - kilitli entity üzerinde.
            if (guncelleme is not null)
            {
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

                if (!string.IsNullOrWhiteSpace(guncelleme.Aciklama))
                {
                    entity.Aciklama = guncelleme.Aciklama;
                }
            }

            try
            {
                await ValidateVeFisOlusturAsync(entity, cancellationToken);
            }
            catch (BaseException ex) when (ex.ErrorCode == 422)
            {
                // Kullanıcı hatası: transaction rollback, kayıt önceki haline döner,
                // Hata/DenemeSayisi YAZILMAZ.
                await tx.RollbackAsync(cancellationToken);
                throw;
            }

            entity.Durum = PosTahsilatValorDurumlari.Aktarildi;
            entity.AktarimTarihi = DateTime.UtcNow;
            entity.HataMesaji = null;
            entity.ClaimToken = null;
            entity.AktarimBaslamaTarihi = null;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return new PosTahsilatValorAktarimSonucDto { Id = id, Basarili = true, MuhasebeFisId = entity.MuhasebeFisId };
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
            var yeniDenemeSayisi = yapilandirmaHatasi ? AzamiOtomatikDeneme : onceki.OncekiDenemeSayisi + 1;

            await KosulluGuncelleAsync(id, token, PosTahsilatValorDurumlari.Hata, yeniDenemeSayisi, mesaj, DateTime.UtcNow, cleanupCts.Token);

            _logger.LogWarning(ex, "POS valör aktarımı başarısız: {Id}", id);
            return new PosTahsilatValorAktarimSonucDto { Id = id, Basarili = false, HataMesaji = mesaj };
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

        if (ParaTutarYuvarlamaHelper.Yuvarla(entity.NetTutar + entity.KomisyonTutari) != ParaTutarYuvarlamaHelper.Yuvarla(entity.BrutTutar))
        {
            throw new BaseException("Brüt tutar = Net tutar + Komisyon tutarı olmalıdır.", 422);
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
            throw new BaseException($"Bu valör kaydı için zaten bir muhasebe fişi oluşturulmuş. Mevcut fiş: {mevcutFis.FisNo}", 409);
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
            ToplamBorc = entity.BrutTutar,
            ToplamAlacak = entity.BrutTutar,
            Satirlar = satirlar
        };

        try
        {
            await _dbContext.MuhasebeFisler.AddAsync(fis, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConflict(ex))
        {
            throw new BaseException("Bu valör kaydı için zaten bir muhasebe fişi oluşturulmuş.", 409);
        }

        entity.MuhasebeFisId = fis.Id;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> GenerateFisNoAsync(int tesisId, int maliYil, CancellationToken cancellationToken)
    {
        var prefix = $"{maliYil}-VLR-";
        var mevcutFisNolar = await _dbContext.MuhasebeFisler
            .Where(x => x.TesisId == tesisId && x.MaliYil == maliYil && !x.IsDeleted && x.FisNo.StartsWith(prefix))
            .Select(x => x.FisNo)
            .ToListAsync(cancellationToken);

        var maxSira = 0;
        foreach (var fisNo in mevcutFisNolar)
        {
            var siraStr = fisNo[prefix.Length..];
            if (int.TryParse(siraStr, out var sira) && sira > maxSira)
                maxSira = sira;
        }

        return $"{prefix}{(maxSira + 1):D6}";
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
        var bugun = ValorTarihHesaplamaService.BugunIstanbul();
        var query = _dbContext.PosTahsilatValorleri.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Durum == PosTahsilatValorDurumlari.ValorBekliyor && x.BeklenenValorTarihi <= bugun);

        if (tesisId.HasValue)
        {
            query = query.Where(x => x.TesisId == tesisId.Value);
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
