using AutoMapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.MuhasebeDonemleri.Entities;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Entities;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Repositories;
using STYS.Muhasebe.SatisBelgeleri.Services.MuhasebeFisStratejileri;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// Satış / alış belgesinden muhasebe fişi oluşturma orkestratörü.
/// MuhasebeOnaylandi durumundaki belge için ortak validasyon, transaction,
/// fiş ana kaydı oluşturma ve belgeye MuhasebeFisId yazma akışını yönetir.
/// Belge tipine özel muhasebe satırları strateji sınıflarında üretilir.
/// 
/// Neden BaseRdbmsService'ten türemiyor?
/// Bu servis cross-aggregate işlem yapmaktadır (SatisBelgesi + MuhasebeFis).
/// BaseRdbmsService tek entity tipi üzerinde çalışır. İki farklı entity'yi
/// aynı transaction içinde güncellediğimiz için DbContext ve repository'leri
/// doğrudan kullanmaktayız.
///
/// Neden DbContext üzerinden Add/Update yapılıyor?
/// Repository.AddAsync / Repository.Update kullanılabilirdi, ancak aynı DbContext
/// transaction'ı içinde iki farklı aggregate (SatisBelgesi ve MuhasebeFis)
/// güncellendiği için DbContext doğrudan kullanılmaktadır. Bu, transaction
/// bütünlüğünü garanti altına alır.
/// </summary>
public class SatisBelgesiMuhasebeFisService : ISatisBelgesiMuhasebeFisService
{
    private readonly ISatisBelgesiRepository _satisBelgesiRepository;
    private readonly StysAppDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IMuhasebeDonemService _muhasebeDonemService;
    private readonly IReadOnlyList<ISatisBelgesiMuhasebeFisStratejisi> _stratejiler;
    private readonly ILogger<SatisBelgesiMuhasebeFisService> _logger;

    public SatisBelgesiMuhasebeFisService(
        ISatisBelgesiRepository satisBelgesiRepository,
        StysAppDbContext dbContext,
        IMapper mapper,
        IMuhasebeDonemService muhasebeDonemService,
        IEnumerable<ISatisBelgesiMuhasebeFisStratejisi> stratejiler,
        ILogger<SatisBelgesiMuhasebeFisService> logger)
    {
        _satisBelgesiRepository = satisBelgesiRepository;
        _dbContext = dbContext;
        _mapper = mapper;
        _muhasebeDonemService = muhasebeDonemService;
        _stratejiler = stratejiler.ToList();
        _logger = logger;
    }

    public async Task<SatisBelgesiDto> MuhasebeFisiOlusturAsync(
        int satisBelgesiId,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Validasyonlar (transaction dışında) ──
        if (satisBelgesiId <= 0)
            throw new BaseException("Geçerli bir satış belgesi ID'si gereklidir.", 400);

        // Belgeyi satırlarıyla birlikte transaction dışında al (transaction içinde tekrar okunacak)
        var belgeOnOkuma = await _satisBelgesiRepository.GetByIdAsync(satisBelgesiId);
        if (belgeOnOkuma is null)
            throw new BaseException("Satış belgesi bulunamadı.", 404);

        if (belgeOnOkuma.IsDeleted)
            throw new BaseException("Satış belgesi silinmiş.", 400);

        if (belgeOnOkuma.Durum != SatisBelgesiDurumu.MuhasebeOnaylandi)
            throw new BaseException(
                $"Satış belgesi 'MuhasebeOnaylandı' durumunda değil. Mevcut durum: {belgeOnOkuma.Durum}",
                400);

        if (belgeOnOkuma.MuhasebeFisId.HasValue)
            throw new BaseException("Bu satış belgesi için daha önce muhasebe fişi oluşturulmuş.", 409);

        if (!belgeOnOkuma.TesisId.HasValue)
            throw new BaseException("Satış belgesinde tesis bilgisi bulunamadı.", 400);

        // Desteklenmeyen belge tipleri
        if (belgeOnOkuma.BelgeTipi == SatisBelgesiTipi.Proforma)
            throw new BaseException("Proforma belgeler için muhasebe fişi oluşturulamaz.", 400);

        if (belgeOnOkuma.BelgeTipi == SatisBelgesiTipi.AlisIadeFaturasi)
            throw new BaseException("Alış iade faturaları için muhasebe fişi üretimi henüz desteklenmemektedir.", 400);

        if (belgeOnOkuma.BelgeTipi is SatisBelgesiTipi.IadeFaturasi or SatisBelgesiTipi.SatisIadeFaturasi)
            throw new BaseException("İade faturaları için otomatik muhasebe fişi üretimi henüz desteklenmemektedir.", 400);

        // Toplam kontroller
        if (belgeOnOkuma.ToplamMatrah <= 0)
            throw new BaseException("Satış belgesinde toplam matrah sıfırdan büyük olmalıdır.", 400);

        if (belgeOnOkuma.GenelToplam <= 0)
            throw new BaseException("Satış belgesinde genel toplam sıfırdan büyük olmalıdır.", 400);

        // 0.01m toleransla toplam tutarlılık kontrolü
        var beklenenToplam = belgeOnOkuma.ToplamMatrah + belgeOnOkuma.ToplamKdv;
        if (Math.Abs(belgeOnOkuma.GenelToplam - beklenenToplam) > 0.01m)
            throw new BaseException(
                $"Satış belgesi toplamları tutarsız: Matrah + KDV = {beklenenToplam:N2}, GenelToplam = {belgeOnOkuma.GenelToplam:N2}",
                400);

        // ── 2. Açık dönem kontrolü ──
        var aktifDonemDto = await _muhasebeDonemService.GetAktifDonemAsync(
            belgeOnOkuma.TesisId.Value,
            belgeOnOkuma.BelgeTarihi,
            cancellationToken);

        if (aktifDonemDto is null)
            throw new BaseException("Satış belgesi tarihi için açık muhasebe dönemi bulunamadı.", 400);

        // ── 3. Transaction içinde ana işlem ──
        const int maxRetry = 3;
        for (int attempt = 0; attempt < maxRetry; attempt++)
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // ── 3a. Belgeyi transaction içinde satırlarıyla yeniden oku ──
                var belge = await _dbContext.SatisBelgeleri
                    .Include(x => x.Satirlar.Where(s => !s.IsDeleted).OrderBy(s => s.SiraNo))
                    .FirstOrDefaultAsync(x => x.Id == satisBelgesiId && !x.IsDeleted, cancellationToken);

                if (belge is null)
                    throw new BaseException("Satış belgesi bulunamadı.", 404);

                // Transaction içinde duplicate kontrol (race condition önlemi)
                if (belge.Durum != SatisBelgesiDurumu.MuhasebeOnaylandi)
                    throw new BaseException(
                        $"Satış belgesi 'MuhasebeOnaylandı' durumunda değil. Mevcut durum: {belge.Durum}",
                        400);

                if (belge.MuhasebeFisId.HasValue)
                    throw new BaseException("Bu satış belgesi için daha önce muhasebe fişi oluşturulmuş.", 409);

                // Aynı kaynakla oluşturulmuş aktif fiş var mı?
                var mevcutFis = await _dbContext.MuhasebeFisler
                    .Where(f => !f.IsDeleted
                                && f.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi
                                && f.KaynakId == satisBelgesiId
                                && f.Durum != MuhasebeFisDurumlari.Iptal)
                    .Select(f => new { f.Id, f.FisNo })
                    .FirstOrDefaultAsync(cancellationToken);

                if (mevcutFis is not null)
                {
                    throw new BaseException(
                        $"Bu satış belgesi için zaten bir muhasebe fişi oluşturulmuş. Mevcut fiş: {mevcutFis.FisNo}",
                        409);
                }

                // ── 3b. Satır validasyonları ──
                var aktifSatirlar = belge.Satirlar.ToList();
                if (aktifSatirlar.Count == 0)
                    throw new BaseException("Satış belgesinde aktif satır bulunamadı.", 400);

                // Tevkifat kontrolü
                if (aktifSatirlar.Any(s => s.KdvUygulamaTipi == STYS.Muhasebe.Kdv.Enums.KdvUygulamaTipi.Tevkifatli))
                    throw new BaseException(
                        "Tevkifatlı belgeler için otomatik muhasebe fişi üretimi henüz desteklenmemektedir.",
                        400);

                // Satır toplamları belge toplamlarıyla uyumlu mu?
                var satirToplamMatrah = aktifSatirlar.Sum(s => s.Matrah);
                var satirToplamKdv = aktifSatirlar.Sum(s => s.KdvTutari);
                var satirToplamGenel = aktifSatirlar.Sum(s => s.SatirToplami);

                if (Math.Abs(satirToplamMatrah - belge.ToplamMatrah) > 0.01m)
                    throw new BaseException(
                        $"Satır matrah toplamı ({satirToplamMatrah:N2}) belge toplam matrahı ({belge.ToplamMatrah:N2}) ile uyumlu değil.",
                        400);

                if (Math.Abs(satirToplamKdv - belge.ToplamKdv) > 0.01m)
                    throw new BaseException(
                        $"Satır KDV toplamı ({satirToplamKdv:N2}) belge toplam KDV'si ({belge.ToplamKdv:N2}) ile uyumlu değil.",
                        400);

                if (Math.Abs(satirToplamGenel - belge.GenelToplam) > 0.01m)
                    throw new BaseException(
                        $"Satır genel toplamı ({satirToplamGenel:N2}) belge genel toplamı ({belge.GenelToplam:N2}) ile uyumlu değil.",
                        400);

                // ── 3c. Alış faturası stok giriş hareketleri ──
                if (belge.BelgeTipi == SatisBelgesiTipi.AlisFaturasi)
                {
                    await CreateAlisStokGirisHareketleriAsync(belge, cancellationToken);
                }

                // ── 3c. Donem ve MaliYil belirle ──
                var maliYil = aktifDonemDto.MaliYil;
                var donemNo = aktifDonemDto.DonemNo;

                var strateji = _stratejiler.FirstOrDefault(s => s.Destekler(belge));
                if (strateji is null)
                    throw new BaseException("Bu belge tipi için muhasebe fişi üretimi desteklenmiyor.", 400);

                var fisContext = belgeOnOkuma.BelgeTipi switch
                {
                    SatisBelgesiTipi.AlisFaturasi => await BuildAlisFisContextAsync(
                        belgeOnOkuma.TesisId!.Value,
                        maliYil,
                        donemNo,
                        belgeOnOkuma.BelgeTarihi,
                        belgeOnOkuma.BelgeNo,
                        cancellationToken),
                    _ => await BuildSatisFisContextAsync(
                        belgeOnOkuma.TesisId!.Value,
                        maliYil,
                        donemNo,
                        belgeOnOkuma.BelgeTarihi,
                        belgeOnOkuma.BelgeNo,
                        cancellationToken)
                };

                // ── 3d. Fiş satırlarını strateji ile oluştur ──
                var satirTaslaklari = await strateji.SatirlariOlusturAsync(
                    belge,
                    fisContext,
                    cancellationToken);

                var satirlar = satirTaslaklari
                    .Select(taslak => new MuhasebeFisSatir
                    {
                        MuhasebeHesapPlaniId = taslak.MuhasebeHesapPlaniId,
                        SiraNo = taslak.SiraNo,
                        Borc = taslak.Borc,
                        Alacak = taslak.Alacak,
                        ParaBirimi = "TRY",
                        Kur = 1,
                        CariKartId = taslak.CariKartId,
                        TasinirKartId = taslak.TasinirKartId,
                        DepoId = taslak.DepoId,
                        KasaBankaHesapId = taslak.KasaBankaHesapId,
                        Aciklama = taslak.Aciklama,
                    })
                    .ToList();

                // ── 3e. Borç / alacak denge kontrolü ──
                var toplamBorc = satirlar.Sum(s => s.Borc);
                var toplamAlacak = satirlar.Sum(s => s.Alacak);

                if (Math.Abs(toplamBorc - toplamAlacak) > 0.01m)
                    throw new BaseException(
                        $"Satış belgesi muhasebe fişi borç/alacak dengesi sağlanamadı. " +
                        $"Borç: {toplamBorc:N2}, Alacak: {toplamAlacak:N2}",
                        400);

                // ── 3f. Fiş no üret ──
                var fisNo = await GenerateFisNoAsync(
                    belgeOnOkuma.TesisId.Value,
                    maliYil,
                    MuhasebeFisTipleri.Mahsup,
                    MuhasebeKaynakModulleri.SatisBelgesi,
                    cancellationToken);

                // ── 3g. Muhasebe fişi oluştur ──
                var fis = new MuhasebeFis
                {
                    TesisId = belgeOnOkuma.TesisId.Value,
                    MaliYil = maliYil,
                    Donem = donemNo,
                    FisNo = fisNo,
                    FisTarihi = belgeOnOkuma.BelgeTarihi,
                    FisTipi = MuhasebeFisTipleri.Mahsup,
                    KaynakModul = MuhasebeKaynakModulleri.SatisBelgesi,
                    KaynakId = belgeOnOkuma.Id,
                    Durum = MuhasebeFisDurumlari.Taslak,
                    Aciklama = $"Satış belgesi muhasebe fişi - {belgeOnOkuma.BelgeNo}",
                    ToplamBorc = toplamBorc,
                    ToplamAlacak = toplamAlacak,
                    Satirlar = satirlar,
                };

                // DbContext üzerinden ekle (cross-aggregate transaction)
                await _dbContext.MuhasebeFisler.AddAsync(fis, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);

                // ── 3h. Satış belgesine fiş bağlantısını yaz ──
                belge.MuhasebeFisId = fis.Id;
                belge.MuhasebeFisOlusturmaTarihi = DateTime.UtcNow;

                // DbContext üzerinden güncelle (cross-aggregate transaction — SatisBelgesiRepository.Update kullanılsaydı
                // ayrı bir SaveChanges çağrısı yapması gerekirdi, bu da transaction bütünlüğünü riske atardı)
                await _dbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Satış belgesi {BelgeId} için muhasebe fişi oluşturuldu. Fiş ID: {FisId}, Fiş No: {FisNo}",
                    belge.Id, fis.Id, fisNo);

                // ── 3i. Güncel DTO dön ──
                // Satırlarıyla birlikte yeniden oku (include navigation)
                var guncelBelge = await _satisBelgesiRepository.GetByIdAsync(belge.Id);
                if (guncelBelge is null)
                    throw new BaseException("Fiş oluşturuldu ancak güncel belge okunamadı.", 500);

                // Satırları da manuel yükle (repository GetByIdAsync include yapmıyor olabilir)
                await _dbContext.Entry(guncelBelge)
                    .Collection(x => x.Satirlar)
                    .Query()
                    .Where(s => !s.IsDeleted)
                    .OrderBy(s => s.SiraNo)
                    .LoadAsync(cancellationToken);

                var result = _mapper.Map<SatisBelgesiDto>(guncelBelge);
                return result;
            }
            catch (DbUpdateException ex) when (IsUniqueConflict(ex) && attempt < maxRetry - 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();

                // Kaynak duplicate mi yoksa FisNo çakışması mı ayırt et
                var kaynakDuplicateMi = await _dbContext.MuhasebeFisler
                    .AsNoTracking()
                    .Where(f => !f.IsDeleted
                                && f.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi
                                && f.KaynakId == satisBelgesiId
                                && f.Durum != MuhasebeFisDurumlari.Iptal)
                    .AnyAsync(cancellationToken);

                if (kaynakDuplicateMi)
                {
                    throw new BaseException(
                        "Bu satış belgesi için daha önce muhasebe fişi oluşturulmuş. " +
                        "Aynı belgeden yeni bir fiş oluşturmak için önce mevcut fişi iptal ediniz.",
                        409);
                }

                // FisNo çakışması → tekrar dene
                _logger.LogWarning("Fiş no çakışması, yeniden deneniyor (deneme {Attempt})", attempt + 1);
                continue;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        throw new BaseException("Fiş numarası üretilemedi. Lütfen tekrar deneyiniz.", 500);
    }

    // ════════════════════════════════════════════════════════════════
    // PRIVATE HELPER'LAR
    //
    // Gerekçe: MuhasebeFisService içindeki GenerateFisNoAsync, GetKdvHesabiAsync
    // ve IsUniqueConflict metotları private olduğu için, satış belgesi kaynaklı
    // fiş üretimi için aynı pattern bu servis içinde kontrollü şekilde
    // uygulanmıştır. GetHesapPlaniAsync ise satış belgesi fiş üretimine özel
    // bir helper'dır (mevcut MuhasebeFisService'te birebir karşılığı yoktur).
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Fiş numarası üretir. Pattern: {MaliYil}-{FisTipiKodu}-{6 haneli sıra}
    /// MuhasebeFisService.GenerateFisNoAsync ile aynı pattern.
    /// </summary>
    private async Task<string> GenerateFisNoAsync(
        int tesisId,
        int maliYil,
        string fisTipi,
        string? kaynakModul,
        CancellationToken cancellationToken)
    {
        var fisTipiKodu = GetFisTipiKodu(fisTipi, kaynakModul);
        var prefix = $"{maliYil}-{fisTipiKodu}-";

        var mevcutFisNolar = await _dbContext.MuhasebeFisler
            .Where(x => x.TesisId == tesisId
                        && x.MaliYil == maliYil
                        && !x.IsDeleted
                        && x.FisNo.StartsWith(prefix))
            .Select(x => x.FisNo)
            .ToListAsync(cancellationToken);

        int maxSira = 0;
        foreach (var fisNo in mevcutFisNolar)
        {
            var siraStr = fisNo.Substring(prefix.Length);
            if (int.TryParse(siraStr, out var sira) && sira > maxSira)
                maxSira = sira;
        }

        return $"{prefix}{(maxSira + 1):D6}";
    }

    /// <summary>
    /// Fiş tipi kodunu döner. MuhasebeFisService.GetFisTipiKodu ile aynı pattern.
    /// </summary>
    private static string GetFisTipiKodu(string fisTipi, string? kaynakModul)
    {
        if (kaynakModul == MuhasebeKaynakModulleri.TasinirHareket)
            return "TSN";

        if (kaynakModul == MuhasebeKaynakModulleri.SatisBelgesi)
            return "STB";

        return fisTipi switch
        {
            MuhasebeFisTipleri.Mahsup => "MHS",
            MuhasebeFisTipleri.Tahsil => "THS",
            MuhasebeFisTipleri.Tediye => "TDY",
            MuhasebeFisTipleri.Acilis => "ACL",
            MuhasebeFisTipleri.Kapanis => "KPN",
            MuhasebeFisTipleri.Duzeltme => "DZT",
            _ => "MHS"
        };
    }

    /// <summary>
    /// Unique constraint violation (SQL Server 2601/2627) kontrolü.
    /// MuhasebeFisService.IsUniqueConflict ile aynı pattern.
    /// </summary>
    private static bool IsUniqueConflict(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sqlEx &&
               (sqlEx.Number == 2601 || sqlEx.Number == 2627);
    }

    /// <summary>
    /// Hesap planından belirtilen ana koda sahip hesabı bulur.
    /// Önce tesis özel (TesisId eşleşen), sonra global (TesisId=null) hesaplar.
    /// TamKod == anaKod öncelikli, yoksa TamKod.StartsWith(anaKod + ".") olan en küçük TamKod.
    /// Hesap aktif, hareket görebilir ve detay hesap olmalıdır.
    /// </summary>
    private async Task<SatisBelgesiMuhasebeFisContext> BuildSatisFisContextAsync(
        int tesisId,
        int maliYil,
        int donemNo,
        DateTime fisTarihi,
        string belgeNo,
        CancellationToken cancellationToken)
    {
        var cari = await GetHesapPlaniAsync(MuhasebeAnaHesapKodlari.CariMusteri, tesisId, cancellationToken);
        var gelir = await GetHesapPlaniAsync(MuhasebeAnaHesapKodlari.GelirSatis, tesisId, cancellationToken);
        var kdv = await GetKdvHesabiAsync(tesisId, MuhasebeAnaHesapKodlari.KDVHesaplanan, true, cancellationToken);

        return new SatisBelgesiMuhasebeFisContext
        {
            TesisId = tesisId,
            MaliYil = maliYil,
            Donem = donemNo,
            FisTarihi = fisTarihi,
            FisNo = string.Empty,
            BelgeNo = belgeNo,
            CariHesapPlaniId = cari.Id,
            GelirHesapPlaniId = gelir.Id,
            KdvHesapPlaniId = kdv.Id
        };
    }

    private async Task<SatisBelgesiMuhasebeFisContext> BuildAlisFisContextAsync(
        int tesisId,
        int maliYil,
        int donemNo,
        DateTime fisTarihi,
        string belgeNo,
        CancellationToken cancellationToken)
    {
        var cari = await GetHesapPlaniAsync(MuhasebeAnaHesapKodlari.CariTedarikci, tesisId, cancellationToken);
        var stok = await GetHesapPlaniAsync(MuhasebeAnaHesapKodlari.StokTicariMal, tesisId, cancellationToken);
        var hizmet = await GetHesapPlaniFallbackAsync(
            tesisId,
            cancellationToken,
            MuhasebeAnaHesapKodlari.GiderHizmetMaliyet,
            MuhasebeAnaHesapKodlari.GiderGenelYonetim);
        var kdv = await GetKdvHesabiAsync(tesisId, MuhasebeAnaHesapKodlari.KDVIndirilecek, false, cancellationToken);

        return new SatisBelgesiMuhasebeFisContext
        {
            TesisId = tesisId,
            MaliYil = maliYil,
            Donem = donemNo,
            FisTarihi = fisTarihi,
            FisNo = string.Empty,
            BelgeNo = belgeNo,
            CariHesapPlaniId = cari.Id,
            GelirHesapPlaniId = 0,
            KdvHesapPlaniId = kdv.Id,
            StokHesapPlaniId = stok.Id,
            HizmetGiderHesapPlaniId = hizmet.Id
        };
    }

    private async Task CreateAlisStokGirisHareketleriAsync(
        SatisBelgesi belge,
        CancellationToken cancellationToken)
    {
        var stokSatirlari = belge.Satirlar
            .Where(x => !x.IsDeleted && x.TasinirKartId.HasValue && x.Miktar > 0)
            .OrderBy(x => x.SiraNo)
            .ToList();

        if (stokSatirlari.Count == 0)
        {
            return;
        }

        var mevcutHareketVarMi = await _dbContext.StokHareketleri
            .AsNoTracking()
            .AnyAsync(x =>
                !x.IsDeleted &&
                x.Durum == StokHareketDurumlari.Aktif &&
                x.KaynakModul == MuhasebeKaynakModulleri.SatisBelgesi &&
                x.KaynakId == belge.Id &&
                x.HareketTipi == StokHareketTipleri.Giris,
                cancellationToken);

        if (mevcutHareketVarMi)
        {
            throw new BaseException("Bu alış faturası için stok giriş hareketleri daha önce oluşturulmuş.", 409);
        }

        var hareketTarihi = belge.BelgeTarihi;
        var hareketler = new List<StokHareket>(stokSatirlari.Count);

        foreach (var satir in stokSatirlari)
        {
            if (!satir.DepoId.HasValue)
            {
                throw new BaseException($"Alış faturası stok satırı için depo seçilmelidir. Satır: {satir.SiraNo}", 400);
            }

            var depo = await _dbContext.Depolar
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == satir.DepoId.Value &&
                    !x.IsDeleted &&
                    x.AktifMi &&
                    x.TesisId == belge.TesisId,
                    cancellationToken);

            if (depo is null)
            {
                throw new BaseException($"Alış faturası stok satırı için seçilen depo bu tesisle uyumlu değil. Satır: {satir.SiraNo}", 400);
            }

            var tasinirKart = await _dbContext.TasinirKartlar
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == satir.TasinirKartId!.Value &&
                    !x.IsDeleted &&
                    x.AktifMi &&
                    x.TesisId == belge.TesisId,
                    cancellationToken);

            if (tasinirKart is null)
            {
                throw new BaseException($"Alış faturası stok satırı için seçilen taşınır kart bu tesisle uyumlu değil. Satır: {satir.SiraNo}", 400);
            }

            hareketler.Add(new StokHareket
            {
                DepoId = depo.Id,
                TasinirKartId = tasinirKart.Id,
                HareketTarihi = hareketTarihi,
                HareketTipi = StokHareketTipleri.Giris,
                Miktar = satir.Miktar,
                BirimFiyat = satir.BirimFiyat,
                Tutar = satir.Matrah,
                BelgeNo = belge.BelgeNo,
                BelgeTarihi = belge.BelgeTarihi,
                Aciklama = $"Alış faturası stok girişi - {belge.BelgeNo}",
                KaynakModul = MuhasebeKaynakModulleri.SatisBelgesi,
                KaynakId = belge.Id,
                Durum = StokHareketDurumlari.Aktif,
                KdvUygulamaTipi = (int)satir.KdvUygulamaTipi,
                KdvIstisnaTanimId = satir.KdvIstisnaTanimId,
                KdvIstisnaKodu = satir.KdvIstisnaKodu,
                KdvIstisnaAciklamasi = satir.KdvIstisnaAciklamasi,
                KdvOrani = satir.KdvOrani,
                KdvTutari = satir.KdvTutari
            });
        }

        await _dbContext.StokHareketleri.AddRangeAsync(hareketler, cancellationToken);
    }

    private async Task<MuhasebeHesapPlani> GetHesapPlaniAsync(
        string anaKod,
        int tesisId,
        CancellationToken cancellationToken)
    {
        var hesap = await _dbContext.MuhasebeHesapPlanlari
            .AsNoTracking()
            .Where(x => !x.IsDeleted
                        && x.AktifMi
                        && x.HareketGorebilirMi
                        && x.DetayHesapMi
                        && (x.TesisId == tesisId || x.TesisId == null)
                        && (x.TamKod == anaKod
                            || x.Kod == anaKod
                            || x.AnaHesapKodu == anaKod
                            || x.TamKod.StartsWith(anaKod + ".")))
            .OrderByDescending(x => x.TesisId == tesisId)
            .ThenBy(x => x.TamKod)
            .FirstOrDefaultAsync(cancellationToken);

        if (hesap is null)
            throw new BaseException(
                $"Hesap planında {anaKod} hesabı bulunamadı veya aktif/hareket görebilir/detay hesap değil. " +
                $"Lütfen {anaKod} kodlu detay hesabı hesap planında tanımlayın.",
                400);

        return hesap;
    }

    /// <summary>
    /// KDV hesabını (191/391) bulur.
    ///
    /// Arama sırası:
    /// 1. MuhasebeVergiHesapEslemeleri tablosunda VergiTipi = "KDV" olan ve
    ///    tesis özel (TesisId eşleşen) veya global (TesisId=null) aktif eşleme ara.
    ///    Eşlemede satış KDV hesabı (SatisKdvHesap) kullanılır.
    /// 2. Eşleme bulunamazsa fallback: MuhasebeHesapPlanlari üzerinden TamKod == "391"
    ///    veya TamKod "391." prefix'i ile başlayan hesap ara.
    ///
    /// Her iki yöntemde de hesap aktif, hareket görebilir ve detay hesap olmalıdır.
    /// Tesis özel sonuç global sonuca göre önceliklidir.
    ///
    /// MuhasebeFisService.GetKdvHesabiAsync private olduğu için aynı pattern
    /// (VergiHesapEsleme tablosu ile zenginleştirilmiş) burada uygulanmıştır.
    /// </summary>
    private async Task<MuhasebeHesapPlani> GetHesapPlaniFallbackAsync(
        int tesisId,
        CancellationToken cancellationToken,
        params string[] anaKodlar)
    {
        BaseException? lastError = null;
        foreach (var anaKod in anaKodlar)
        {
            try
            {
                return await GetHesapPlaniAsync(anaKod, tesisId, cancellationToken);
            }
            catch (BaseException ex)
            {
                lastError = ex;
            }
        }

        throw lastError ?? new BaseException("Hesap planı bulunamadı.", 400);
    }

    private async Task<MuhasebeHesapPlani> GetKdvHesabiAsync(
        int tesisId,
        string tamKod,
        bool satisMi,
        CancellationToken cancellationToken)
    {
        // ── 1. Önce VergiHesapEsleme tablosunda satış KDV eşlemesi ara ──
        var esleme = await _dbContext.MuhasebeVergiHesapEslemeleri
            .AsNoTracking()
            .Where(e => e.VergiTipi == "KDV"
                        && !e.IsDeleted
                        && e.AktifMi
                        && (e.TesisId == tesisId || e.TesisId == null))
            .OrderByDescending(e => e.TesisId == tesisId) // tesis özel öncelikli
            .FirstOrDefaultAsync(cancellationToken);

        if (esleme is not null)
        {
            var hesapId = satisMi ? esleme.SatisKdvHesapId : esleme.AlisKdvHesapId;

            // Eşlemedeki KDV hesabını doğrula: aktif, hareket görebilir, detay hesap
            var eslemeHesap = await _dbContext.MuhasebeHesapPlanlari
                .AsNoTracking()
                .Where(x => x.Id == hesapId
                            && !x.IsDeleted
                            && x.AktifMi
                            && x.HareketGorebilirMi
                            && x.DetayHesapMi)
                .FirstOrDefaultAsync(cancellationToken);

            if (eslemeHesap is not null)
                return eslemeHesap;
        }

        // ── 2. Fallback: HesapPlanı üzerinden TamKod ile ara ──
        var kdvHesap = await _dbContext.MuhasebeHesapPlanlari
            .AsNoTracking()
            .Where(x => x.TamKod == tamKod
                        && !x.IsDeleted
                        && x.AktifMi
                        && x.HareketGorebilirMi
                        && x.DetayHesapMi
                        && (x.TesisId == tesisId || x.TesisId == null))
            .OrderByDescending(x => x.TesisId == tesisId)
            .FirstOrDefaultAsync(cancellationToken);

        if (kdvHesap is not null)
            return kdvHesap;

        // ── 3. Son çare: TamKod/Kod prefix ile başlayan detay hesap ara ──
        kdvHesap = await _dbContext.MuhasebeHesapPlanlari
            .AsNoTracking()
            .Where(x => !x.IsDeleted
                        && x.AktifMi
                        && x.HareketGorebilirMi
                        && x.DetayHesapMi
                        && (x.TesisId == tesisId || x.TesisId == null)
                        && (x.TamKod == tamKod
                            || x.Kod == tamKod
                            || x.AnaHesapKodu == tamKod
                            || x.TamKod.StartsWith(tamKod + ".")))
            .OrderByDescending(x => x.TesisId == tesisId)
            .ThenBy(x => x.TamKod)
            .FirstOrDefaultAsync(cancellationToken);

        if (kdvHesap is not null)
            return kdvHesap;

        throw new BaseException(
            satisMi
                ? "Satış KDV hesabı (Hesaplanan KDV 391) bulunamadı. Lütfen Muhasebe Vergi-Hesap Eşleme sayfasından KDV için satış KDV hesabı eşlemesi tanımlayın, veya hesap planında 391 kodlu aktif ve hareket görebilir bir detay hesap oluşturun."
                : "Alış KDV hesabı (İndirilecek KDV 191) bulunamadı. Lütfen Muhasebe Vergi-Hesap Eşleme sayfasından KDV için alış KDV hesabı eşlemesi tanımlayın, veya hesap planında 191 kodlu aktif ve hareket görebilir bir detay hesap oluşturun.",
            400);
    }
}
