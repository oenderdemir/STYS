using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Entities;
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

    public RezervasyonOdemeMuhasebeService(
        StysAppDbContext dbContext,
        ITahsilatOdemeBelgesiService tahsilatOdemeBelgesiService,
        IMuhasebeFisService muhasebeFisService)
    {
        _dbContext = dbContext;
        _tahsilatOdemeBelgesiService = tahsilatOdemeBelgesiService;
        _muhasebeFisService = muhasebeFisService;
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

        var cariKartId = await ResolveCariKartIdAsync(rezervasyon, cariKartIdOverride, cancellationToken);
        if (rezervasyon.CariKartId != cariKartId)
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

            if (fis is not null && fis.Durum == MuhasebeFisDurumlari.Taslak)
            {
                throw new BaseException(
                    "Bu tahsilat icin taslak durumda bir muhasebe fisi var. Odemeyi iptal etmeden once " +
                    "fisi Muhasebe > Fisler ekranindan siliniz ya da onaylayiniz.", 409);
            }

            if (fis is not null)
            {
                // Onaylanmis/kesinlesmis fis — dogrudan silinemez, mevcut ters kayit mekanizmasi kullanilir.
                await _muhasebeFisService.IptalEtAsync(fis.Id, iptalAciklama, cancellationToken);
            }
        }

        // TahsilatOdemeBelgesiService.IptalEtAsync zaten acik donem kontrolu yapar,
        // varsa CariHareket'i CariHareketKapamaService.GeriAlAsync ile ters cevirir ve
        // belge.Durum = Iptal yazar. Ambient transaction algilanip yeniden transaction acmaz.
        await _tahsilatOdemeBelgesiService.IptalEtAsync(belge.Id, cancellationToken);
    }

    /// <summary>
    /// Cari kart cozumleme sirasi (revizyon #5):
    ///   1) Rezervasyon.CariKartId onbellekte varsa kullan
    ///   2) Kullanici acikca bir cari kart secmisse (cariKartIdOverride) onu kullan (dogrulanir)
    ///   3) TCKN/VKN esleşmesi VEYA "guvenli" telefon esleşmesi (telefon + ad-soyad birlikte
    ///      esleşmeli — sadece telefonla eslesme aile bireylerini karistirabileceginden yetersizdir)
    ///      ile ayni tesiste mevcut bir Musteri/KurumsalMusteri cari kart varsa kullan
    ///   4) Tesisin konfigure edilmis "Rezervasyon Misafirleri" varsayilan cari karti varsa kullan
    ///   5) Hicbiri yoksa OTOMATIK CARI KART OLUSTURULMAZ — kullanicidan secim istenir (422)
    /// </summary>
    private async Task<int> ResolveCariKartIdAsync(Rezervasyon rezervasyon, int? cariKartIdOverride, CancellationToken cancellationToken)
    {
        if (rezervasyon.CariKartId.HasValue)
        {
            return rezervasyon.CariKartId.Value;
        }

        if (cariKartIdOverride.HasValue)
        {
            var secilen = await _dbContext.CariKartlar.FirstOrDefaultAsync(
                x => !x.IsDeleted && x.Id == cariKartIdOverride.Value, cancellationToken);

            if (secilen is null || !secilen.AktifMi)
            {
                throw new BaseException("Secilen cari kart bulunamadi veya pasif durumda.", 400);
            }

            if (secilen.TesisId.HasValue && secilen.TesisId.Value != rezervasyon.TesisId)
            {
                throw new BaseException("Secilen cari kart bu rezervasyonun tesisiyle uyumlu degil.", 400);
            }

            return secilen.Id;
        }

        var tcknVeyaTelefonEslesen = await FindEslesenCariKartAsync(rezervasyon, cancellationToken);
        if (tcknVeyaTelefonEslesen.HasValue)
        {
            return tcknVeyaTelefonEslesen.Value;
        }

        var tesis = await _dbContext.Tesisler
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == rezervasyon.TesisId, cancellationToken);

        if (tesis?.RezervasyonMisafirVarsayilanCariKartId is int varsayilanCariKartId)
        {
            var varsayilan = await _dbContext.CariKartlar
                .AsNoTracking()
                .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == varsayilanCariKartId && x.AktifMi, cancellationToken);

            if (varsayilan is not null)
            {
                return varsayilan.Id;
            }
        }

        throw new BaseException(
            "Rezervasyon icin cari kart otomatik belirlenemedi. Lutfen bir cari kart seciniz " +
            "(veya tesis ayarlarindan varsayilan 'Rezervasyon Misafirleri' cari kartini tanimlayiniz).",
            CariKartSecimiGerekliStatusCode);
    }

    private async Task<int?> FindEslesenCariKartAsync(Rezervasyon rezervasyon, CancellationToken cancellationToken)
    {
        var tcknVkn = rezervasyon.TcKimlikNo?.Trim();
        var telefon = rezervasyon.MisafirTelefon?.Trim();
        var adSoyad = rezervasyon.MisafirAdiSoyadi?.Trim();

        var aday = await _dbContext.CariKartlar
            .Where(x => !x.IsDeleted
                        && x.AktifMi
                        && x.TesisId == rezervasyon.TesisId
                        && (x.CariTipi == CariKartTipleri.Musteri || x.CariTipi == CariKartTipleri.KurumsalMusteri)
                        && (
                            (!string.IsNullOrEmpty(tcknVkn) && x.VergiNoTckn == tcknVkn)
                            || (!string.IsNullOrEmpty(telefon) && !string.IsNullOrEmpty(adSoyad)
                                && x.Telefon == telefon && x.UnvanAdSoyad == adSoyad)
                        ))
            // TCKN eslesmesi telefon+ad eslesmesinden daha guvenilir — once onu tercih et.
            .OrderByDescending(x => !string.IsNullOrEmpty(tcknVkn) && x.VergiNoTckn == tcknVkn)
            .FirstOrDefaultAsync(cancellationToken);

        return aday?.Id;
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
