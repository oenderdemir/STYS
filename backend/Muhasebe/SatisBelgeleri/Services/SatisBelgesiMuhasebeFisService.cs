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
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Repositories;
using STYS.Muhasebe.SatisBelgeleri.Services.MuhasebeFisStratejileri;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// Satış belgesinden muhasebe fişi oluşturma servisi.
/// MuhasebeOnaylandi durumundaki satış belgesinden 120 / 600 / 391 hesap kurgusuyla
/// muhasebe fişi taslağı oluşturur.
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

        if (belgeOnOkuma.BelgeTipi.IsAlisBelgesi())
            throw new BaseException("Alış faturaları için muhasebe fişi oluşturma henüz desteklenmiyor. Faz 73 kapsamında eklenecektir.", 400);

        if (belgeOnOkuma.BelgeTipi.IsIadeBelgesi())
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
                        "Tevkifatlı satış belgeleri için otomatik muhasebe fişi üretimi henüz desteklenmemektedir.",
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

                // ── 3c. Hesap planından 120, 600 ve 391 hesaplarını bul ──
                var tesisId = belge.TesisId!.Value;
                var hesap120 = await GetHesapPlaniAsync("120", tesisId, cancellationToken);
                var hesap600 = await GetHesapPlaniAsync("600", tesisId, cancellationToken);

                MuhasebeHesapPlani? hesap391 = null;
                if (belge.ToplamKdv > 0)
                {
                    hesap391 = await GetKdvHesabiAsync(belge.TesisId.Value, "391", cancellationToken);
                }

                // ── 3d. Donem ve MaliYil belirle ──
                var maliYil = aktifDonemDto.MaliYil;
                var donemNo = aktifDonemDto.DonemNo;

                var strateji = _stratejiler.FirstOrDefault(s => s.Destekler(belge));
                if (strateji is null)
                    throw new BaseException("Bu belge tipi için muhasebe fişi üretimi desteklenmiyor.", 400);

                var fisContext = new SatisBelgesiMuhasebeFisContext
                {
                    TesisId = tesisId,
                    MaliYil = maliYil,
                    Donem = donemNo,
                    FisTarihi = belge.BelgeTarihi,
                    FisNo = string.Empty,
                    BelgeNo = belge.BelgeNo,
                    CariHesapPlaniId = hesap120.Id,
                    GelirHesapPlaniId = hesap600.Id,
                    KdvHesapPlaniId = hesap391?.Id,
                };

                // ── 3e. Fiş satırlarını strateji ile oluştur ──
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
                        Aciklama = taslak.Aciklama,
                    })
                    .ToList();

                // ── 3f. Borç / alacak denge kontrolü ──
                var toplamBorc = satirlar.Sum(s => s.Borc);
                var toplamAlacak = satirlar.Sum(s => s.Alacak);

                if (Math.Abs(toplamBorc - toplamAlacak) > 0.01m)
                    throw new BaseException(
                        $"Satış belgesi muhasebe fişi borç/alacak dengesi sağlanamadı. " +
                        $"Borç: {toplamBorc:N2}, Alacak: {toplamAlacak:N2}",
                        400);

                // ── 3g. Fiş no üret ──
                var fisNo = await GenerateFisNoAsync(
                    belge.TesisId.Value,
                    maliYil,
                    MuhasebeFisTipleri.Mahsup,
                    MuhasebeKaynakModulleri.SatisBelgesi,
                    cancellationToken);

                // ── 3h. Muhasebe fişi oluştur ──
                var fis = new MuhasebeFis
                {
                    TesisId = belge.TesisId.Value,
                    MaliYil = maliYil,
                    Donem = donemNo,
                    FisNo = fisNo,
                    FisTarihi = belge.BelgeTarihi,
                    FisTipi = MuhasebeFisTipleri.Mahsup,
                    KaynakModul = MuhasebeKaynakModulleri.SatisBelgesi,
                    KaynakId = belge.Id,
                    Durum = MuhasebeFisDurumlari.Taslak,
                    Aciklama = $"Satış belgesi muhasebe fişi - {belge.BelgeNo}",
                    ToplamBorc = toplamBorc,
                    ToplamAlacak = toplamAlacak,
                    Satirlar = satirlar,
                };

                // DbContext üzerinden ekle (cross-aggregate transaction)
                await _dbContext.MuhasebeFisler.AddAsync(fis, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);

                // ── 3i. Satış belgesine fiş bağlantısını yaz ──
                belge.MuhasebeFisId = fis.Id;
                belge.MuhasebeFisOlusturmaTarihi = DateTime.UtcNow;

                // DbContext üzerinden güncelle (cross-aggregate transaction — SatisBelgesiRepository.Update kullanılsaydı
                // ayrı bir SaveChanges çağrısı yapması gerekirdi, bu da transaction bütünlüğünü riske atardı)
                await _dbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Satış belgesi {BelgeId} için muhasebe fişi oluşturuldu. Fiş ID: {FisId}, Fiş No: {FisNo}",
                    belge.Id, fis.Id, fisNo);

                // ── 3j. Güncel DTO dön ──
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
    private async Task<MuhasebeHesapPlani> GetHesapPlaniAsync(
        string anaKod,
        int tesisId,
        CancellationToken cancellationToken)
    {
        // Önce TamKod == anaKod olanları ara (tesis özel öncelikli)
        var hesap = await _dbContext.MuhasebeHesapPlanlari
            .AsNoTracking()
            .Where(x => x.TamKod == anaKod
                        && !x.IsDeleted
                        && x.AktifMi
                        && x.HareketGorebilirMi
                        && x.DetayHesapMi
                        && (x.TesisId == tesisId || x.TesisId == null))
            .OrderByDescending(x => x.TesisId == tesisId)
            .FirstOrDefaultAsync(cancellationToken);

        if (hesap is not null)
            return hesap;

        // TamKod.StartsWith(anaKod + ".") olan en küçük TamKod'u ara
        var prefix = anaKod + ".";
        hesap = await _dbContext.MuhasebeHesapPlanlari
            .AsNoTracking()
            .Where(x => x.TamKod.StartsWith(prefix)
                        && !x.IsDeleted
                        && x.AktifMi
                        && x.HareketGorebilirMi
                        && x.DetayHesapMi
                        && (x.TesisId == tesisId || x.TesisId == null))
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
    /// Satış KDV hesabını (391) bulur.
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
    private async Task<MuhasebeHesapPlani> GetKdvHesabiAsync(
        int tesisId,
        string tamKod,
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
            // Eşlemedeki satış KDV hesabını doğrula: aktif, hareket görebilir, detay hesap
            var eslemeHesap = await _dbContext.MuhasebeHesapPlanlari
                .AsNoTracking()
                .Where(x => x.Id == esleme.SatisKdvHesapId
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

        // ── 3. Son çare: TamKod "391." prefix ile başlayan detay hesap ara ──
        var prefix = tamKod + ".";
        kdvHesap = await _dbContext.MuhasebeHesapPlanlari
            .AsNoTracking()
            .Where(x => x.TamKod.StartsWith(prefix)
                        && !x.IsDeleted
                        && x.AktifMi
                        && x.HareketGorebilirMi
                        && x.DetayHesapMi
                        && (x.TesisId == tesisId || x.TesisId == null))
            .OrderByDescending(x => x.TesisId == tesisId)
            .ThenBy(x => x.TamKod)
            .FirstOrDefaultAsync(cancellationToken);

        if (kdvHesap is not null)
            return kdvHesap;

        throw new BaseException(
            "Satış KDV hesabı (Hesaplanan KDV 391) bulunamadı. " +
            "Lütfen Muhasebe Vergi-Hesap Eşleme sayfasından KDV için satış KDV hesabı eşlemesi tanımlayın, " +
            "veya hesap planında 391 kodlu aktif ve hareket görebilir bir detay hesap oluşturun.",
            400);
    }
}
