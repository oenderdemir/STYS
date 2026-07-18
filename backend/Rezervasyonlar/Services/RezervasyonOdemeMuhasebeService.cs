using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.MuhasebeFisleri.Services;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Services;
using STYS.Rezervasyonlar.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Rezervasyonlar.Services;

/// <summary>
/// Rezervasyon odemesinden muhasebe tahsilat belgesi uretimini yoneten cross-aggregate servis.
/// RezervasyonOdeme + CariKart + TahsilatOdemeBelgesi birden fazla aggregate'e dokundugu icin
/// (SatisBelgesiMuhasebeFisService ile ayni gerekce ile) BaseRdbmsService'ten turemez, ortak
/// DbContext uzerinden calisir ve kendi transaction'ini ACMAZ — cagiranin (RezervasyonService)
/// transaction'i icinde kosar.
/// </summary>
public class RezervasyonOdemeMuhasebeService : IRezervasyonOdemeMuhasebeService
{
    /// <summary>Kullanici tarafindan cari kart secimi gerektigini frontend'e ayirt ettirmek icin
    /// kullanilan HTTP durum kodu (plain 400'lerden ayrismasi icin 422 kullanilir).</summary>
    public const int CariKartSecimiGerekliStatusCode = 422;

    private readonly StysAppDbContext _dbContext;
    private readonly ITahsilatOdemeBelgesiService _tahsilatOdemeBelgesiService;
    private readonly IMuhasebeFisService _muhasebeFisService;
    private readonly IRezervasyonCariKartResolver _cariKartResolver;

    public RezervasyonOdemeMuhasebeService(
        StysAppDbContext dbContext,
        ITahsilatOdemeBelgesiService tahsilatOdemeBelgesiService,
        IMuhasebeFisService muhasebeFisService,
        IRezervasyonCariKartResolver cariKartResolver)
    {
        _dbContext = dbContext;
        _tahsilatOdemeBelgesiService = tahsilatOdemeBelgesiService;
        _muhasebeFisService = muhasebeFisService;
        _cariKartResolver = cariKartResolver;
    }

    public async Task TahsilatOlusturAsync(
        Rezervasyon rezervasyon,
        RezervasyonOdeme odeme,
        int? kasaBankaHesapId,
        int? cariKartIdOverride,
        CancellationToken cancellationToken = default)
    {
        // Duplicate koruması: ayni odeme icin ikinci belge uretilmesin.
        // (Migration'daki unique index — IX_RezervasyonOdemeler_TahsilatOdemeBelgesiId — son savunma hattidir.)
        var zatenVar = await _dbContext.TahsilatOdemeBelgeleri.AnyAsync(
            x => !x.IsDeleted
                 && x.KaynakModul == MuhasebeKaynakModulleri.Rezervasyon
                 && x.KaynakId == odeme.Id,
            cancellationToken);
        if (zatenVar)
        {
            throw new BaseException("Bu rezervasyon odemesi icin zaten bir tahsilat belgesi olusturulmus.", 409);
        }

        var cariKartId = await _cariKartResolver.ResolveAsync(rezervasyon, cariKartIdOverride, cancellationToken);

        // Rezervasyonun ana/varsayilan carisi yalnizca ilk kez belirlenirken yazilir. Sonraki
        // odemeler icin cariKartIdOverride farkli bir cari getirebilir (rezervasyonun bir kismini
        // baska bir misafir odemis olabilir) — bu durumda rezervasyonun ana carisi (fatura/gelir
        // belgesi bu alandan cozulur) sessizce degistirilmemelidir.
        if (!rezervasyon.CariKartId.HasValue)
        {
            rezervasyon.CariKartId = cariKartId;
        }

        // Nakit hareketi doguran odeme tiplerinde kasa/banka hesabi zorunlu (OdayaEkle/Mahsup haric).
        if (OdemeYontemleri.NakitHareketiGerektirenler.Contains(odeme.OdemeTipi))
        {
            if (!kasaBankaHesapId.HasValue)
            {
                throw new BaseException(
                    $"'{odeme.OdemeTipi}' odeme tipi icin kasa/banka/POS hesabi secimi zorunludur.", 400);
            }

            await EnsureKasaBankaHesabiUygunAsync(rezervasyon.TesisId, kasaBankaHesapId.Value, odeme.OdemeTipi, cancellationToken);
        }

        // Alacak hesabi tesis konfigurasyonuna gore cari karttan degil MuhasebeAnaHesapKodlari.
        // AlinanSiparisAvanslari'ndan cozulecekse (bkz. TahsilatOdemeBelgesiMuhasebeFisService.
        // ResolveAlacakHesabiAsync), cari kartin kendi MuhasebeHesapPlaniId baglantisina burada
        // ihtiyac yoktur — ValidateOlusturmaAsync bu kontrolu atlar.
        var alacakHesapTipi = await _dbContext.Tesisler
            .AsNoTracking()
            .Where(x => x.Id == rezervasyon.TesisId)
            .Select(x => x.RezervasyonTahsilatAlacakHesapTipi)
            .FirstOrDefaultAsync(cancellationToken) ?? RezervasyonTahsilatAlacakHesapTipleri.Cari;
        var requireCariMuhasebeHesabi = alacakHesapTipi != RezervasyonTahsilatAlacakHesapTipleri.AlinanAvans;

        // TahsilatOdemeBelgesi burada TahsilatOdemeBelgesiService.AddAsync yerine dogrudan
        // DbContext uzerinden ekleniyor (cross-aggregate/ambient transaction gerekcesiyle),
        // ama AddAsync'in yaptigi tum dogrulamalar (cari kart/hesap plani, tesis erisimi, belge/odeme
        // yontemi/durum gecerliligi, acik muhasebe donemi) ValidateOlusturmaAsync ile aynen calistirilir.
        await _tahsilatOdemeBelgesiService.ValidateOlusturmaAsync(
            cariKartId,
            TahsilatOdemeBelgeTipleri.Tahsilat,
            odeme.OdemeTipi,
            TahsilatOdemeBelgeDurumlari.Aktif,
            odeme.OdemeTarihi,
            kapatilacakCariHareketId: null,
            requireCariMuhasebeHesabi,
            cancellationToken);

        // BelgeNo uretimi MAX+1 sorgusuna dayandigindan yaris durumuna acik; ambient transaction
        // icinde savepoint ile guvenli retry uygulanir (unique index BelgeNo uzerinde zaten mevcut).
        const int maxRetry = 3;
        for (var attempt = 0; attempt < maxRetry; attempt++)
        {
            var belgeNo = await GenerateBelgeNoAsync(rezervasyon.TesisId, odeme.OdemeTarihi.Year, cancellationToken);

            var belge = new TahsilatOdemeBelgesi
            {
                BelgeNo = belgeNo,
                BelgeTarihi = odeme.OdemeTarihi,
                BelgeTipi = TahsilatOdemeBelgeTipleri.Tahsilat,
                CariKartId = cariKartId,
                Tutar = odeme.OdemeTutari,
                ParaBirimi = odeme.ParaBirimi,
                OdemeYontemi = odeme.OdemeTipi,
                Aciklama = $"Rezervasyon tahsilati - {rezervasyon.ReferansNo}",
                KaynakModul = MuhasebeKaynakModulleri.Rezervasyon,
                KaynakId = odeme.Id,
                // Bilerek NULL: henuz kapatilacak bir fatura/cari hareket yok — bu saf bir
                // tahsilat/avans kaydidir. Gelir, fatura/konaklama kapanis akisinda olusur.
                KapatilacakCariHareketId = null,
                KasaBankaHesapId = kasaBankaHesapId,
                Durum = TahsilatOdemeBelgeDurumlari.Aktif
            };

            var ambientTransaction = _dbContext.Database.CurrentTransaction;
            var savepointName = $"tahsilat_belge_olustur_{attempt}";
            if (ambientTransaction is not null)
            {
                await ambientTransaction.CreateSavepointAsync(savepointName, cancellationToken);
            }

            try
            {
                await _dbContext.TahsilatOdemeBelgeleri.AddAsync(belge, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);

                odeme.TahsilatOdemeBelgesiId = belge.Id;
                odeme.KasaBankaHesapId = kasaBankaHesapId;
                return;
            }
            catch (DbUpdateException ex) when (IsUniqueConflict(ex) && attempt < maxRetry - 1)
            {
                if (ambientTransaction is not null)
                {
                    await ambientTransaction.RollbackToSavepointAsync(savepointName, cancellationToken);
                }

                _dbContext.Entry(belge).State = EntityState.Detached;
            }
        }

        throw new BaseException("Tahsilat belge numarasi uretilemedi. Lutfen tekrar deneyiniz.", 500);
    }

    private static bool IsUniqueConflict(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627);
    }

    public async Task TahsilatIptalEtAsync(
        RezervasyonOdeme odeme,
        string? iptalAciklama,
        CancellationToken cancellationToken = default)
    {
        if (!odeme.TahsilatOdemeBelgesiId.HasValue)
        {
            // Muhasebeye hic islenmemis odeme (eski kayit) — RezervasyonOdeme iptali yeterli.
            return;
        }

        var belge = await _dbContext.TahsilatOdemeBelgeleri
            .FirstOrDefaultAsync(x => x.Id == odeme.TahsilatOdemeBelgesiId.Value, cancellationToken);

        if (belge is null || belge.Durum == TahsilatOdemeBelgeDurumlari.Iptal)
        {
            return;
        }

        if (belge.MuhasebeFisId.HasValue)
        {
            var fis = await _dbContext.MuhasebeFisler
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == belge.MuhasebeFisId.Value, cancellationToken);

            // Fis Taslak veya Onayli ise (aktif muhasebe etkisi var demektir) odeme iptali
            // reddedilir. Rezervasyon odeme iptali (RezervasyonYonetimi.Manage) hicbir kosulda
            // otomatik muhasebe fisi iptali/ters kayit uretmez — bu, MuhasebeFisYonetimi.Manage
            // yetkisi gerektiren, muhasebenin bilincli olarak yapmasi gereken bir islemdir.
            if (fis is not null
                && (fis.Durum == MuhasebeFisDurumlari.Taslak || fis.Durum == MuhasebeFisDurumlari.Onayli))
            {
                throw new BaseException(
                    "Bu odeme icin muhasebe fisi olusturulmus. Once muhasebe fisi muhasebe ekranindan " +
                    "iptal edilmelidir.", 409);
            }

            // Durum Iptal veya TersKayit ise muhasebe etkisi zaten kapatilmis kabul edilir;
            // burada yeniden IptalEtAsync cagrilmaz (zaten iptal fis uzerinde islem yapilamaz).
        }

        // TahsilatOdemeBelgesiService.IptalEtAsync zaten acik donem kontrolu yapar,
        // varsa CariHareket'i CariHareketKapamaService.GeriAlAsync ile ters cevirir ve
        // belge.Durum = Iptal yazar. Ambient transaction algilanip yeniden transaction acmaz.
        await _tahsilatOdemeBelgesiService.IptalEtAsync(belge.Id, cancellationToken);
    }

    private async Task EnsureKasaBankaHesabiUygunAsync(int tesisId, int kasaBankaHesapId, string odemeTipi, CancellationToken cancellationToken)
    {
        var hesap = await _dbContext.KasaBankaHesaplari
            .AsNoTracking()
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == kasaBankaHesapId, cancellationToken);

        if (hesap is null || !hesap.AktifMi)
        {
            throw new BaseException("Secilen kasa/banka/POS hesabi bulunamadi veya pasif durumda.", 400);
        }

        if (hesap.TesisId.HasValue && hesap.TesisId.Value != tesisId)
        {
            throw new BaseException("Secilen kasa/banka/POS hesabi bu rezervasyonun tesisiyle uyumlu degil.", 400);
        }

        if (!OdemeYontemleri.UygunKasaBankaHesapTipleri.TryGetValue(odemeTipi, out var uygunTipler)
            || !uygunTipler.Contains(hesap.Tip))
        {
            throw new BaseException(
                $"'{odemeTipi}' odeme tipi icin '{hesap.Tip}' turunde bir hesap secilemez.", 400);
        }
    }

    private async Task<string> GenerateBelgeNoAsync(int tesisId, int yil, CancellationToken cancellationToken)
    {
        var prefix = $"{yil}-REZ-{tesisId}-";

        var mevcutBelgeNolar = await _dbContext.TahsilatOdemeBelgeleri
            .Where(x => x.BelgeNo.StartsWith(prefix))
            .Select(x => x.BelgeNo)
            .ToListAsync(cancellationToken);

        var maxSira = 0;
        foreach (var belgeNo in mevcutBelgeNolar)
        {
            var siraStr = belgeNo[prefix.Length..];
            if (int.TryParse(siraStr, out var sira) && sira > maxSira)
            {
                maxSira = sira;
            }
        }

        return $"{prefix}{(maxSira + 1):D6}";
    }
}
