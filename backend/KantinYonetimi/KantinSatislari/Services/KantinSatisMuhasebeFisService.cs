using AutoMapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.KantinYonetimi.KantinSatislari.Dtos;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.MuhasebeDonemleri.Dtos;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Services;
using Microsoft.Extensions.Logging;
using TOD.Platform.SharedKernel.Exceptions;
using System.Data;

namespace STYS.KantinYonetimi.KantinSatislari.Services;

public class KantinSatisMuhasebeFisService : IKantinSatisMuhasebeFisService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IKantinSatisService _kantinSatisService;
    private readonly IMuhasebeDonemService _muhasebeDonemService;
    private readonly ITasinirKodMuhasebeHesapEslemeService _tasinirKodMuhasebeHesapEslemeService;
    private readonly ILogger<KantinSatisMuhasebeFisService> _logger;

    public KantinSatisMuhasebeFisService(
        StysAppDbContext dbContext,
        IKantinSatisService kantinSatisService,
        IMuhasebeDonemService muhasebeDonemService,
        ITasinirKodMuhasebeHesapEslemeService tasinirKodMuhasebeHesapEslemeService,
        ILogger<KantinSatisMuhasebeFisService> logger)
    {
        _dbContext = dbContext;
        _kantinSatisService = kantinSatisService;
        _muhasebeDonemService = muhasebeDonemService;
        _tasinirKodMuhasebeHesapEslemeService = tasinirKodMuhasebeHesapEslemeService;
        _logger = logger;
    }

    public async Task<KantinSatisDto> MuhasebeFisiOlusturAsync(int kantinSatisId, CancellationToken cancellationToken = default)
    {
        const int maxRetry = 3;

        for (var attempt = 0; attempt < maxRetry; attempt++)
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                var satis = await _dbContext.KantinSatislar
                    .Include(x => x.Kantin)
                    .Include(x => x.MuhasebeFis)
                    .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
                        .ThenInclude(x => x.TasinirKart)
                    .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
                        .ThenInclude(x => x.StokHareket)
                    .Include(x => x.Odemeler.Where(o => !o.IsDeleted))
                        .ThenInclude(x => x.TahsilatOdemeBelgesi)
                    .Include(x => x.Odemeler.Where(o => !o.IsDeleted))
                        .ThenInclude(x => x.KasaBankaHesap)
                    .FirstOrDefaultAsync(x => x.Id == kantinSatisId && !x.IsDeleted, cancellationToken)
                    ?? throw new BaseException("Kantin satışı bulunamadı.", 404);

                await EnsureFisOlusturulabilirAsync(satis, cancellationToken);
                ValidateKesinlesmisSatis(satis);

                var aktifDonem = await _muhasebeDonemService.GetAktifDonemAsync(satis.TesisId, satis.SatisTarihi, cancellationToken)
                    ?? throw new BaseException("Satış tarihi için açık muhasebe dönemi bulunamadı.", 400);

                var satirlar = satis.Satirlar.OrderBy(x => x.Id).ToList();
                var odemeler = satis.Odemeler.OrderBy(x => x.Id).ToList();

                ValidatePaymentTotals(satis, odemeler);
                ValidateTahsilatlar(odemeler, satis.Kantin?.PerakendeCariKartId);

                var muhasebeSatirlari = new List<MuhasebeFisSatir>();
                var siraNo = 1;

                foreach (var odeme in odemeler.GroupBy(x => x.KasaBankaHesapId))
                {
                    var firstOdeme = odeme.First();
                    var hesapId = await ResolveOdemeMuhasebeHesapIdAsync(satis.TesisId, firstOdeme.KasaBankaHesapId, cancellationToken);
                    muhasebeSatirlari.Add(new MuhasebeFisSatir
                    {
                        MuhasebeHesapPlaniId = hesapId,
                        SiraNo = siraNo++,
                        Borc = ParaTutarYuvarlamaHelper.Yuvarla(odeme.Sum(x => x.Tutar)),
                        Alacak = 0m,
                        ParaBirimi = "TRY",
                        Kur = 1m,
                        KasaBankaHesapId = firstOdeme.KasaBankaHesapId,
                        Aciklama = $"Kantin satış tahsilatı #{satis.Id}"
                    });
                }

                var gelirHesap = await GetRequiredHesapByAnaKodAsync(MuhasebeAnaHesapKodlari.GelirSatis, satis.TesisId, cancellationToken);
                muhasebeSatirlari.Add(new MuhasebeFisSatir
                {
                    MuhasebeHesapPlaniId = gelirHesap.Id,
                    SiraNo = siraNo++,
                    Borc = 0m,
                    Alacak = ParaTutarYuvarlamaHelper.Yuvarla(satirlar.Sum(x => x.Matrah)),
                    ParaBirimi = "TRY",
                    Kur = 1m,
                    Aciklama = $"Kantin satış geliri #{satis.Id}"
                });

                foreach (var oranGrubu in satirlar
                             .Where(x => x.KdvTutari > 0)
                             .GroupBy(x => x.KdvOrani)
                             .OrderBy(x => x.Key))
                {
                    var kdvHesapId = await ResolveKdvHesabiIdForOranAsync(
                        satis.TesisId,
                        oranGrubu.Key,
                        MuhasebeAnaHesapKodlari.KDVHesaplanan,
                        cancellationToken);

                    if (!kdvHesapId.HasValue)
                    {
                        throw new BaseException($"{oranGrubu.Key:0.##}% KDV oranı için satış KDV hesabı bulunamadı.", 400);
                    }

                    muhasebeSatirlari.Add(new MuhasebeFisSatir
                    {
                        MuhasebeHesapPlaniId = kdvHesapId.Value,
                        SiraNo = siraNo++,
                        Borc = 0m,
                        Alacak = ParaTutarYuvarlamaHelper.Yuvarla(oranGrubu.Sum(x => x.KdvTutari)),
                        ParaBirimi = "TRY",
                        Kur = 1m,
                        Aciklama = $"Kantin satış KDV %{oranGrubu.Key:0.##} #{satis.Id}"
                    });
                }

                var maliyetHesap = await GetRequiredHesapByAnaKodAsync(MuhasebeAnaHesapKodlari.SatilanTicariMallarMaliyeti, satis.TesisId, cancellationToken);
                var stokGruplari = new Dictionary<int, decimal>();
                var toplamMaliyet = 0m;

                foreach (var satir in satirlar)
                {
                    var stokHareket = satir.StokHareket;
                    if (stokHareket is null || stokHareket.IsDeleted || !string.Equals(stokHareket.Durum, StokHareketDurumlari.Aktif, StringComparison.Ordinal))
                    {
                        throw new BaseException("Kantin satış satırına bağlı aktif stok hareketi bulunamadı.", 400);
                    }

                    if (!stokHareket.MaliyetTutari.HasValue)
                    {
                        throw new BaseException("Kantin satış stok hareketinde maliyet snapshotı bulunamadı.", 400);
                    }

                    var kart = satir.TasinirKart ?? throw new BaseException("Kantin satış satırındaki taşınır kart okunamadı.", 400);
                    var stokHesapId = await ResolveStokHesapIdAsync(kart, cancellationToken);
                    var maliyetTutari = ParaTutarYuvarlamaHelper.Yuvarla(stokHareket.MaliyetTutari.Value);
                    toplamMaliyet += maliyetTutari;

                    if (stokGruplari.TryGetValue(stokHesapId, out var existing))
                    {
                        stokGruplari[stokHesapId] = ParaTutarYuvarlamaHelper.Yuvarla(existing + maliyetTutari);
                    }
                    else
                    {
                        stokGruplari[stokHesapId] = maliyetTutari;
                    }
                }

                toplamMaliyet = ParaTutarYuvarlamaHelper.Yuvarla(toplamMaliyet);
                if (toplamMaliyet > 0)
                {
                    muhasebeSatirlari.Add(new MuhasebeFisSatir
                    {
                        MuhasebeHesapPlaniId = maliyetHesap.Id,
                        SiraNo = siraNo++,
                        Borc = toplamMaliyet,
                        Alacak = 0m,
                        ParaBirimi = "TRY",
                        Kur = 1m,
                        Aciklama = $"Kantin satış maliyeti #{satis.Id}"
                    });

                    foreach (var stokGrubu in stokGruplari.OrderBy(x => x.Key))
                    {
                        muhasebeSatirlari.Add(new MuhasebeFisSatir
                        {
                            MuhasebeHesapPlaniId = stokGrubu.Key,
                            SiraNo = siraNo++,
                            Borc = 0m,
                            Alacak = stokGrubu.Value,
                            ParaBirimi = "TRY",
                            Kur = 1m,
                            Aciklama = $"Kantin satış stok çıkışı #{satis.Id}"
                        });
                    }
                }

                var toplamBorc = ParaTutarYuvarlamaHelper.Yuvarla(muhasebeSatirlari.Sum(x => x.Borc));
                var toplamAlacak = ParaTutarYuvarlamaHelper.Yuvarla(muhasebeSatirlari.Sum(x => x.Alacak));
                if (Math.Abs(toplamBorc - toplamAlacak) > 0.01m)
                {
                    throw new BaseException($"Kantin satış muhasebe fişi dengesi sağlanamadı. Borç: {toplamBorc:0.00}, Alacak: {toplamAlacak:0.00}", 400);
                }

                var fisNo = await GenerateFisNoAsync(satis.TesisId, aktifDonem.MaliYil, cancellationToken);
                var fis = new MuhasebeFis
                {
                    TesisId = satis.TesisId,
                    MaliYil = aktifDonem.MaliYil,
                    Donem = aktifDonem.DonemNo,
                    FisNo = fisNo,
                    FisTarihi = satis.SatisTarihi,
                    FisTipi = MuhasebeFisTipleri.Mahsup,
                    KaynakModul = MuhasebeKaynakModulleri.KantinSatis,
                    KaynakId = satis.Id,
                    Durum = MuhasebeFisDurumlari.Taslak,
                    Aciklama = $"Kantin satış muhasebe fişi - #{satis.Id}",
                    ToplamBorc = toplamBorc,
                    ToplamAlacak = toplamAlacak,
                    Satirlar = muhasebeSatirlari
                };

                await _dbContext.MuhasebeFisler.AddAsync(fis, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);

                satis.MuhasebeFisId = fis.Id;
                satis.MuhasebeFisOlusturmaTarihi = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return await _kantinSatisService.GetByIdAsync(kantinSatisId, cancellationToken)
                    ?? throw new BaseException("Muhasebe fişi oluşturuldu ancak satış okunamadı.", 500);
            }
            catch (DbUpdateException ex) when (IsUniqueConflict(ex) && attempt < maxRetry - 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();

                var existing = await _dbContext.MuhasebeFisler
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted
                                && x.KaynakModul == MuhasebeKaynakModulleri.KantinSatis
                                && x.KaynakId == kantinSatisId
                                && x.Durum != MuhasebeFisDurumlari.Iptal
                                && x.Durum != MuhasebeFisDurumlari.TersKayit)
                    .AnyAsync(cancellationToken);

                if (existing)
                {
                    throw new BaseException("Bu kantin satışı için daha önce muhasebe fişi oluşturulmuş.", 409);
                }

                _logger.LogWarning("Kantin satış muhasebe fişi oluşturma tekrar deneniyor. Deneme: {Attempt}", attempt + 1);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        throw new BaseException("Fiş numarası üretilemedi. Lütfen tekrar deneyiniz.", 500);
    }

    private async Task EnsureFisOlusturulabilirAsync(Entities.KantinSatis satis, CancellationToken cancellationToken)
    {
        if (!satis.MuhasebeFisId.HasValue)
        {
            return;
        }

        var fis = await _dbContext.MuhasebeFisler
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == satis.MuhasebeFisId.Value, cancellationToken);

        if (fis is null || fis.IsDeleted)
        {
            throw new BaseException("Kantin satışına bağlı muhasebe fişi bulunamadı veya silinmiş.", 400);
        }

        if (!string.Equals(fis.Durum, MuhasebeFisDurumlari.Iptal, StringComparison.Ordinal))
        {
            throw new BaseException("Bu kantin satışı için daha önce muhasebe fişi oluşturulmuş.", 409);
        }
    }

    private static void ValidateKesinlesmisSatis(Entities.KantinSatis satis)
    {
        if (!string.Equals(satis.Durum, Entities.KantinSatisDurumlari.Kesinlesti, StringComparison.Ordinal))
        {
            throw new BaseException("Muhasebe fişi yalnız kesinleşmiş satışlar için oluşturulabilir.", 400);
        }

        if (satis.Satirlar.Count == 0)
        {
            throw new BaseException("Muhasebe fişi için aktif satış satırı bulunmalıdır.", 400);
        }

        if (satis.Odemeler.Count == 0)
        {
            throw new BaseException("Muhasebe fişi için aktif ödeme kaydı bulunmalıdır.", 400);
        }
    }

    private static void ValidatePaymentTotals(Entities.KantinSatis satis, IReadOnlyCollection<Entities.KantinSatisOdeme> odemeler)
    {
        var odemeToplami = ParaTutarYuvarlamaHelper.Yuvarla(odemeler.Sum(x => x.Tutar));
        var toplamTutar = ParaTutarYuvarlamaHelper.Yuvarla(satis.ToplamTutar);
        if (odemeToplami != toplamTutar)
        {
            throw new BaseException("Ödeme toplamı satış toplamına eşit olmalıdır.", 400);
        }
    }

    private static void ValidateTahsilatlar(IReadOnlyCollection<Entities.KantinSatisOdeme> odemeler, int? perakendeCariKartId)
    {
        foreach (var odeme in odemeler)
        {
            if (!odeme.TahsilatOdemeBelgesiId.HasValue)
            {
                throw new BaseException("Tüm ödeme kayıtları için tahsilat belgesi bulunmalıdır.", 400);
            }

            var belge = odeme.TahsilatOdemeBelgesi;
            if (belge is null
                || belge.IsDeleted
                || !string.Equals(belge.Durum, TahsilatOdemeBelgeDurumlari.Aktif, StringComparison.Ordinal)
                || !string.Equals(belge.KaynakModul, MuhasebeKaynakModulleri.KantinSatisOdeme, StringComparison.Ordinal)
                || belge.KaynakId != odeme.Id
                || !string.Equals(belge.BelgeTipi, TahsilatOdemeBelgeTipleri.Tahsilat, StringComparison.Ordinal)
                || belge.CariKartId != perakendeCariKartId
                || !string.Equals(belge.ParaBirimi, "TRY", StringComparison.Ordinal)
                || belge.Tutar != odeme.Tutar
                || !string.Equals(belge.OdemeYontemi, odeme.OdemeYontemi, StringComparison.Ordinal)
                || belge.KasaBankaHesapId != odeme.KasaBankaHesapId)
            {
                throw new BaseException("Mevcut kantin tahsilat belgesi ödeme bilgileriyle uyumsuz.", 400);
            }
        }
    }

    private async Task<int> ResolveOdemeMuhasebeHesapIdAsync(int tesisId, int? kasaBankaHesapId, CancellationToken cancellationToken)
    {
        if (!kasaBankaHesapId.HasValue)
        {
            throw new BaseException("Ödeme hesabı bulunamadı.", 400);
        }

        var hesap = await _dbContext.KasaBankaHesaplari
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == kasaBankaHesapId.Value && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Ödeme hesabı bulunamadı.", 400);

        if (hesap.TesisId != tesisId || !hesap.AktifMi)
        {
            throw new BaseException("Ödeme hesabı aktif ve aynı tesise ait olmalıdır.", 400);
        }

        if (!hesap.MuhasebeHesapPlaniId.HasValue)
        {
            throw new BaseException("Ödeme hesabı için muhasebe hesap planı bağlantısı zorunludur.", 400);
        }

        return (await GetRequiredHesapByIdAsync(hesap.MuhasebeHesapPlaniId.Value, tesisId, cancellationToken)).Id;
    }

    private async Task<int> ResolveStokHesapIdAsync(TasinirKart kart, CancellationToken cancellationToken)
    {
        var tesisId = kart.TesisId ?? throw new BaseException("Taşınır kart tesis bilgisi bulunamadı.", 400);

        if (kart.MuhasebeHesapPlaniId.HasValue)
        {
            var hesap = await TryGetHesapByIdAsync(kart.MuhasebeHesapPlaniId.Value, tesisId, cancellationToken);
            if (hesap is not null)
            {
                return hesap.Id;
            }
        }

        if (kart.TasinirKodId > 0)
        {
            var esleme = await _tasinirKodMuhasebeHesapEslemeService.GetVarsayilanAsync(
                kart.TasinirKodId,
                kart.MalzemeTipi,
                StokHareketTipleri.Giris,
                cancellationToken);

            if (esleme?.MuhasebeHesapPlaniId > 0)
            {
                var hesap = await TryGetHesapByIdAsync(esleme.MuhasebeHesapPlaniId, tesisId, cancellationToken);
                if (hesap is not null)
                {
                    return hesap.Id;
                }
            }
        }

        return (await GetRequiredHesapByAnaKodAsync(MuhasebeAnaHesapKodlari.StokTicariMal, tesisId, cancellationToken)).Id;
    }

    private async Task<MuhasebeHesapPlani?> TryGetHesapByIdAsync(int id, int? tesisId, CancellationToken cancellationToken)
    {
        return await _dbContext.MuhasebeHesapPlanlari
            .AsNoTracking()
            .Where(x => x.Id == id
                        && !x.IsDeleted
                        && x.AktifMi
                        && x.HareketGorebilirMi
                        && x.DetayHesapMi
                        && (x.TesisId == tesisId || x.TesisId == null))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<MuhasebeHesapPlani> GetRequiredHesapByIdAsync(int id, int? tesisId, CancellationToken cancellationToken)
    {
        return await TryGetHesapByIdAsync(id, tesisId, cancellationToken)
            ?? throw new BaseException("Muhasebe hesap planı kaydı bulunamadı veya kullanıma uygun değil.", 400);
    }

    private async Task<MuhasebeHesapPlani?> TryGetHesapByAnaKodAsync(string anaKod, int tesisId, CancellationToken cancellationToken)
    {
        return await _dbContext.MuhasebeHesapPlanlari
            .AsNoTracking()
            .Where(x => !x.IsDeleted
                        && x.AktifMi
                        && x.HareketGorebilirMi
                        && x.DetayHesapMi
                        && (x.TesisId == tesisId || x.TesisId == null)
                        && (x.TamKod == anaKod || x.Kod == anaKod || x.AnaHesapKodu == anaKod || x.TamKod.StartsWith(anaKod + ".")))
            .OrderByDescending(x => x.TesisId == tesisId)
            .ThenBy(x => x.TamKod)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<MuhasebeHesapPlani> GetRequiredHesapByAnaKodAsync(string anaKod, int tesisId, CancellationToken cancellationToken)
    {
        return await TryGetHesapByAnaKodAsync(anaKod, tesisId, cancellationToken)
            ?? throw new BaseException($"{anaKod} hesabı bulunamadı.", 400);
    }

    private async Task<int?> ResolveKdvHesabiIdForOranAsync(int tesisId, decimal oran, string tamKodFallback, CancellationToken cancellationToken)
    {
        var tesisEsleme = await _dbContext.MuhasebeVergiHesapEslemeleri
            .AsNoTracking()
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.AktifMi && x.VergiTipi == "KDV" && x.Oran == oran && x.TesisId == tesisId, cancellationToken);

        var esleme = tesisEsleme ?? await _dbContext.MuhasebeVergiHesapEslemeleri
            .AsNoTracking()
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.AktifMi && x.VergiTipi == "KDV" && x.Oran == oran && x.TesisId == null, cancellationToken);

        if (esleme is not null)
        {
            var hesap = await TryGetHesapByIdAsync(esleme.SatisKdvHesapId, tesisId, cancellationToken);
            if (hesap is not null)
            {
                return hesap.Id;
            }
        }

        return null;
    }

    private async Task<string> GenerateFisNoAsync(int tesisId, int maliYil, CancellationToken cancellationToken)
    {
        var prefix = $"{maliYil}-KNT-";
        var mevcutFisNolar = await _dbContext.MuhasebeFisler
            .Where(x => x.TesisId == tesisId && x.MaliYil == maliYil && !x.IsDeleted && x.FisNo.StartsWith(prefix))
            .Select(x => x.FisNo)
            .ToListAsync(cancellationToken);

        var maxSira = 0;
        foreach (var fisNo in mevcutFisNolar)
        {
            var siraStr = fisNo[prefix.Length..];
            if (int.TryParse(siraStr, out var sira) && sira > maxSira)
            {
                maxSira = sira;
            }
        }

        return $"{prefix}{(maxSira + 1):D6}";
    }

    private static bool IsUniqueConflict(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627);
    }
}
