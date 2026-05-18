using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Dtos;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Entities;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Repositories;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.MuhasebeHesapBakiyeleri.Services;

public class MuhasebeHesapBakiyeService
    : BaseRdbmsService<MuhasebeHesapBakiyeDto, MuhasebeHesapBakiye, int>,
      IMuhasebeHesapBakiyeService
{
    private readonly IMuhasebeHesapBakiyeRepository _repository;
    private readonly StysAppDbContext _dbContext;

    public MuhasebeHesapBakiyeService(
        IMuhasebeHesapBakiyeRepository repository,
        IMapper mapper,
        StysAppDbContext dbContext)
        : base(repository, mapper)
    {
        _repository = repository;
        _dbContext = dbContext;
    }

    // ---------------------------------------------------------------
    // Rebuild — performanslı toplu bakiye yeniden hesaplama
    //
    // Bu endpoint manuel yönetim/onarım amaçlıdır.
    // Büyük veri hacminde senkron HTTP endpoint yerine background job
    // tercih edilmelidir.
    // Rebuild işlemi fiş satırlarını projection ile okur, tracking
    // yükünü azaltır. Hesap planı ve üst hesaplar tek seferde çekilir.
    // Bakiye hesapları bellekte dictionary ile gruplanır.
    // Yeni kayıtlar AddRangeAsync ile eklenir.
    // Bu fazda gerçek bulk insert paketi eklenmez.
    // Büyük veri hacminde rebuild işlemi batch/arka plan job olarak
    // tasarlanmalıdır.
    // ---------------------------------------------------------------
    public async Task<MuhasebeHesapBakiyeRebuildResultDto> RebuildAsync(
        MuhasebeHesapBakiyeRebuildRequest request,
        CancellationToken cancellationToken = default)
    {
        var baslamaZamani = DateTime.UtcNow;

        // ── Validasyon ──
        if (request.TesisId <= 0)
            throw new BaseException("Geçerli bir tesis seçilmelidir.", 400);

        if (request.MaliYil < 2000 || request.MaliYil > 2100)
            throw new BaseException("Mali yıl 2000-2100 aralığında olmalıdır.", 400);

        if (request.Donem.HasValue && (request.Donem.Value < 1 || request.Donem.Value > 12))
            throw new BaseException("Dönem 1-12 aralığında olmalıdır.", 400);

        var tesis = await _dbContext.Tesisler
            .FirstOrDefaultAsync(x => x.Id == request.TesisId && !x.IsDeleted, cancellationToken);

        if (tesis is null)
            throw new BaseException("Seçilen tesis bulunamadı.", 400);

        using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            // ── 1. Mevcut bakiye kayıtlarını soft delete ──
            var mevcutBakiyeQuery = _dbContext.MuhasebeHesapBakiyeleri
                .Where(x =>
                    x.TesisId == request.TesisId
                    && x.MaliYil == request.MaliYil
                    && !x.IsDeleted);

            if (request.Donem.HasValue)
                mevcutBakiyeQuery = mevcutBakiyeQuery
                    .Where(x => x.Donem == request.Donem.Value);

            var mevcutBakiyeler = await mevcutBakiyeQuery
                .ToListAsync(cancellationToken);

            var silinenKayitSayisi = mevcutBakiyeler.Count;

            foreach (var bakiye in mevcutBakiyeler)
            {
                bakiye.IsDeleted = true;
                bakiye.DeletedAt = DateTime.UtcNow;
            }

            // Soft delete'i hemen kaydet (unique index çakışmasını önlemek için)
            if (mevcutBakiyeler.Count > 0)
                await _dbContext.SaveChangesAsync(cancellationToken);

            // ── 2. İlgili fişleri projection ile oku ──
            var fisQuery = _dbContext.MuhasebeFisler
                .Where(f =>
                    !f.IsDeleted
                    && f.TesisId == request.TesisId
                    && f.MaliYil == request.MaliYil
                    && (f.Durum == MuhasebeFisDurumlari.Onayli
                        || f.Durum == MuhasebeFisDurumlari.TersKayit));

            if (request.Donem.HasValue)
                fisQuery = fisQuery.Where(f => f.Donem == request.Donem.Value);

            var islenenFisSayisi = await fisQuery.CountAsync(cancellationToken);

            var hareketSatirlari = await fisQuery
                .SelectMany(f => f.Satirlar
                    .Where(s => !s.IsDeleted)
                    .Select(s => new
                    {
                        f.TesisId,
                        f.MaliYil,
                        f.Donem,
                        s.MuhasebeHesapPlaniId,
                        s.Borc,
                        s.Alacak
                    }))
                .ToListAsync(cancellationToken);

            var islenenSatirSayisi = hareketSatirlari.Count;

            if (islenenSatirSayisi == 0)
            {
                await transaction.CommitAsync(cancellationToken);

                return new MuhasebeHesapBakiyeRebuildResultDto
                {
                    TesisId = request.TesisId,
                    MaliYil = request.MaliYil,
                    Donem = request.Donem,
                    IslenenFisSayisi = 0,
                    IslenenSatirSayisi = 0,
                    SilinenBakiyeKaydiSayisi = silinenKayitSayisi,
                    OlusturulanBakiyeKaydiSayisi = 0,
                    BaslamaZamani = baslamaZamani,
                    BitisZamani = DateTime.UtcNow,
                    Mesaj = "İşlenecek onaylı/ters kayıt fişi bulunamadı."
                };
            }

            // ── 3. Hesapları tek sorguda çek ──
            var hesapIds = hareketSatirlari
                .Select(x => x.MuhasebeHesapPlaniId)
                .Distinct()
                .ToList();

            var hesaplar = await _dbContext.MuhasebeHesapPlanlari
                .Where(x => hesapIds.Contains(x.Id) && !x.IsDeleted && x.AktifMi)
                .ToListAsync(cancellationToken);

            // Satırların hesap doğrulaması
            foreach (var satir in hareketSatirlari)
            {
                var hesap = hesaplar.FirstOrDefault(x => x.Id == satir.MuhasebeHesapPlaniId);
                if (hesap is null)
                    throw new BaseException(
                        $"Fiş satırındaki muhasebe hesabı bulunamadı veya aktif değil. HesapId: {satir.MuhasebeHesapPlaniId}",
                        400);
            }

            // ── 4. Üst hesapları çıkar ve çek ──
            var ustKodlar = hesaplar
                .SelectMany(x => GetUstHesapKodlari(x.TamKod))
                .Distinct()
                .ToList();

            Dictionary<string, MuhasebeHesapPlani>? ustHesapLookup = null;

            if (ustKodlar.Count > 0)
            {
                var ustHesapListesi = await _dbContext.MuhasebeHesapPlanlari
                    .Where(x => ustKodlar.Contains(x.TamKod) && !x.IsDeleted && x.AktifMi)
                    .ToListAsync(cancellationToken);

                ustHesapLookup = ustKodlar.ToDictionary(
                    kod => kod,
                    kod =>
                    {
                        var secilen = ustHesapListesi
                            .Where(x => x.TamKod == kod)
                            .OrderByDescending(x => x.TesisId == request.TesisId)
                            .ThenByDescending(x => x.TesisId == null)
                            .FirstOrDefault();

                        if (secilen is null)
                            throw new BaseException(
                                $"Üst muhasebe hesabı bulunamadı: {kod}",
                                400);

                        return secilen;
                    });
            }

            // ── 5. Bellekte aggregate ──
            var aggregate = new Dictionary<BakiyeKey, BakiyeAggregate>();

            foreach (var satir in hareketSatirlari)
            {
                var hesap = hesaplar.First(x => x.Id == satir.MuhasebeHesapPlaniId);

                // A. Gerçek hesap (KonsolideMi = false)
                var key = new BakiyeKey(
                    satir.TesisId,
                    satir.MaliYil,
                    satir.Donem,
                    hesap.Id,
                    KonsolideMi: false);

                if (!aggregate.TryGetValue(key, out var agg))
                {
                    agg = new BakiyeAggregate
                    {
                        TesisId = key.TesisId,
                        MaliYil = key.MaliYil,
                        Donem = key.Donem,
                        MuhasebeHesapPlaniId = key.MuhasebeHesapPlaniId,
                        HesapKodu = hesap.TamKod,
                        HesapAdi = hesap.Ad,
                        KonsolideMi = false
                    };
                    aggregate[key] = agg;
                }

                agg.BorcToplam += satir.Borc;
                agg.AlacakToplam += satir.Alacak;

                // B. Üst hesaplar (KonsolideMi = true)
                var hesapUstKodlari = GetUstHesapKodlari(hesap.TamKod);

                foreach (var ustKod in hesapUstKodlari)
                {
                    if (ustHesapLookup is null || !ustHesapLookup.TryGetValue(ustKod, out var ustHesap))
                        continue;

                    var ustKey = new BakiyeKey(
                        satir.TesisId,
                        satir.MaliYil,
                        satir.Donem,
                        ustHesap.Id,
                        KonsolideMi: true);

                    if (!aggregate.TryGetValue(ustKey, out var ustAgg))
                    {
                        ustAgg = new BakiyeAggregate
                        {
                            TesisId = ustKey.TesisId,
                            MaliYil = ustKey.MaliYil,
                            Donem = ustKey.Donem,
                            MuhasebeHesapPlaniId = ustKey.MuhasebeHesapPlaniId,
                            HesapKodu = ustHesap.TamKod,
                            HesapAdi = ustHesap.Ad,
                            KonsolideMi = true
                        };
                        aggregate[ustKey] = ustAgg;
                    }

                    ustAgg.BorcToplam += satir.Borc;
                    ustAgg.AlacakToplam += satir.Alacak;
                }
            }

            // ── 6. Yeni bakiye kayıtlarını oluştur ──
            var yeniBakiyeler = new List<MuhasebeHesapBakiye>(aggregate.Count);

            foreach (var agg in aggregate.Values)
            {
                var net = agg.BorcToplam - agg.AlacakToplam;

                var yeniBakiye = new MuhasebeHesapBakiye
                {
                    TesisId = agg.TesisId,
                    MaliYil = agg.MaliYil,
                    Donem = agg.Donem,
                    MuhasebeHesapPlaniId = agg.MuhasebeHesapPlaniId,
                    HesapKodu = agg.HesapKodu,
                    HesapAdi = agg.HesapAdi,
                    KonsolideMi = agg.KonsolideMi,
                    BorcToplam = agg.BorcToplam,
                    AlacakToplam = agg.AlacakToplam,
                    SonGuncellemeTarihi = DateTime.UtcNow
                };

                // Hesaplanan alanları set et
                RecalculateBakiye(yeniBakiye);

                yeniBakiyeler.Add(yeniBakiye);
            }

            await _dbContext.MuhasebeHesapBakiyeleri
                .AddRangeAsync(yeniBakiyeler, cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new MuhasebeHesapBakiyeRebuildResultDto
            {
                TesisId = request.TesisId,
                MaliYil = request.MaliYil,
                Donem = request.Donem,
                IslenenFisSayisi = islenenFisSayisi,
                IslenenSatirSayisi = islenenSatirSayisi,
                SilinenBakiyeKaydiSayisi = silinenKayitSayisi,
                OlusturulanBakiyeKaydiSayisi = yeniBakiyeler.Count,
                BaslamaZamani = baslamaZamani,
                BitisZamani = DateTime.UtcNow,
                Mesaj = "Rebuild başarıyla tamamlandı."
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<List<MuhasebeHesapBakiyeDto>> GetFilteredAsync(
        MuhasebeHesapBakiyeFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        filter.Normalize();
        var entities = await _repository.GetFilteredAsync(filter, cancellationToken);
        return Mapper.Map<List<MuhasebeHesapBakiyeDto>>(entities);
    }

    public async Task<int> CountFilteredAsync(
        MuhasebeHesapBakiyeFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        filter.Normalize();
        return await _repository.CountFilteredAsync(filter, cancellationToken);
    }

    public async Task<List<MuhasebeHesapBakiyeDto>> GetByTesisYilDonemAsync(
        int tesisId,
        int maliYil,
        int donem,
        CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByTesisYilDonemAsync(tesisId, maliYil, donem, cancellationToken);
        return Mapper.Map<List<MuhasebeHesapBakiyeDto>>(entities);
    }

    public override async Task<IEnumerable<MuhasebeHesapBakiyeDto>> GetAllAsync(
        Func<IQueryable<MuhasebeHesapBakiye>, IQueryable<MuhasebeHesapBakiye>>? include = null)
    {
        var effectiveInclude = include ?? (q => q
            .Include(x => x.Tesis)
            .Include(x => x.MuhasebeHesapPlani)
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.TesisId)
                .ThenBy(x => x.MaliYil)
                .ThenBy(x => x.Donem)
                .ThenBy(x => x.HesapKodu)
                .ThenBy(x => x.KonsolideMi));

        return await base.GetAllAsync(effectiveInclude);
    }

    public override async Task<MuhasebeHesapBakiyeDto?> GetByIdAsync(
        int id,
        Func<IQueryable<MuhasebeHesapBakiye>, IQueryable<MuhasebeHesapBakiye>>? include = null)
    {
        var effectiveInclude = include ?? (q => q
            .Include(x => x.Tesis)
            .Include(x => x.MuhasebeHesapPlani)
            .Where(x => !x.IsDeleted));

        return await base.GetByIdAsync(id, effectiveInclude);
    }

    public override async Task<MuhasebeHesapBakiyeDto> AddAsync(MuhasebeHesapBakiyeDto dto)
    {
        await ValidateAsync(dto, CancellationToken.None);
        NormalizeAndSetComputedFields(dto);
        return await base.AddAsync(dto);
    }

    public override async Task<MuhasebeHesapBakiyeDto> UpdateAsync(MuhasebeHesapBakiyeDto dto)
    {
        // Mevcut kaydı bul
        var existing = await _dbContext.MuhasebeHesapBakiyeleri
            .FirstOrDefaultAsync(x => x.Id == dto.Id && !x.IsDeleted);

        if (existing is null)
            throw new BaseException("Güncellenecek hesap bakiyesi bulunamadı.", 404);

        await ValidateAsync(dto, CancellationToken.None, existingId: dto.Id);
        NormalizeAndSetComputedFields(dto);
        return await base.UpdateAsync(dto);
    }

    private static void NormalizeAndSetComputedFields(MuhasebeHesapBakiyeDto dto)
    {
        // Bakiye alanları hesapla
        var net = dto.BorcToplam - dto.AlacakToplam;
        dto.NetBakiye = net;
        dto.BorcBakiye = net > 0 ? net : 0;
        dto.AlacakBakiye = net < 0 ? Math.Abs(net) : 0;
        dto.BakiyeTipi = net > 0 ? "Borc" : net < 0 ? "Alacak" : "Sifir";
        dto.Bakiye = Math.Abs(net);

        dto.HesapSeviyesi = CalculateHesapSeviyesi(dto.HesapKodu);
        dto.UstHesapKodu = GetUstHesapKodu(dto.HesapKodu);

        // Son güncelleme tarihi
        dto.SonGuncellemeTarihi = DateTime.UtcNow;
    }

    private static int CalculateHesapSeviyesi(string hesapKodu)
    {
        if (string.IsNullOrWhiteSpace(hesapKodu))
            return 0;

        return hesapKodu
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Length;
    }

    private static string? GetUstHesapKodu(string hesapKodu)
    {
        if (string.IsNullOrWhiteSpace(hesapKodu))
            return null;

        var parts = hesapKodu.Split('.', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length <= 1)
            return null;

        return string.Join('.', parts.Take(parts.Length - 1));
    }

    private async Task ValidateAsync(MuhasebeHesapBakiyeDto dto, CancellationToken cancellationToken, int? existingId = null)
    {
        if (dto.TesisId <= 0)
            throw new BaseException("Tesis seçilmelidir.", 400);

        if (dto.MaliYil < 2000 || dto.MaliYil > 2100)
            throw new BaseException("Mali yıl 2000-2100 aralığında olmalıdır.", 400);

        if (dto.Donem < 1 || dto.Donem > 12)
            throw new BaseException("Dönem 1-12 aralığında olmalıdır.", 400);

        if (dto.MuhasebeHesapPlaniId <= 0)
            throw new BaseException("Muhasebe hesap planı seçilmelidir.", 400);

        if (dto.BorcToplam < 0)
            throw new BaseException("Borç toplamı negatif olamaz.", 400);

        if (dto.AlacakToplam < 0)
            throw new BaseException("Alacak toplamı negatif olamaz.", 400);

        // Tesis kontrolü
        var tesis = await _dbContext.Tesisler
            .FirstOrDefaultAsync(x => x.Id == dto.TesisId && !x.IsDeleted, cancellationToken);

        if (tesis is null)
            throw new BaseException("Seçilen tesis bulunamadı.", 400);

        // Muhasebe hesap planı kontrolü ve HesapKodu/HesapAdi set etme
        var hesap = await _dbContext.MuhasebeHesapPlanlari
            .FirstOrDefaultAsync(x => x.Id == dto.MuhasebeHesapPlaniId, cancellationToken);

        if (hesap is null)
            throw new BaseException("Seçilen muhasebe hesabı bulunamadı.", 400);

        if (hesap.IsDeleted)
            throw new BaseException("Seçilen muhasebe hesabı silinmiştir.", 400);

        if (!hesap.AktifMi)
            throw new BaseException("Seçilen muhasebe hesabı aktif değildir.", 400);

        // HesapKodu ve HesapAdi entity'deki TamKod ve Ad'dan set edilir
        dto.HesapKodu = hesap.TamKod;
        dto.HesapAdi = hesap.Ad;

        // Duplicate aktif kayıt kontrolü
        var duplicateQuery = _dbContext.MuhasebeHesapBakiyeleri
            .Where(x =>
                x.TesisId == dto.TesisId
                && x.MaliYil == dto.MaliYil
                && x.Donem == dto.Donem
                && x.MuhasebeHesapPlaniId == dto.MuhasebeHesapPlaniId
                && x.KonsolideMi == dto.KonsolideMi
                && !x.IsDeleted);

        if (existingId.HasValue)
            duplicateQuery = duplicateQuery.Where(x => x.Id != existingId.Value);

        var duplicateExists = await duplicateQuery.AnyAsync(cancellationToken);
        if (duplicateExists)
            throw new BaseException("Aynı tesis, mali yıl, dönem, hesap ve konsolide bilgisi için aktif kayıt zaten mevcut.", 400);
    }

    private static void RecalculateBakiye(MuhasebeHesapBakiye bakiye)
    {
        var net = bakiye.BorcToplam - bakiye.AlacakToplam;

        bakiye.NetBakiye = net;
        bakiye.BorcBakiye = net > 0 ? net : 0;
        bakiye.AlacakBakiye = net < 0 ? Math.Abs(net) : 0;
        bakiye.BakiyeTipi = net > 0 ? "Borc" : net < 0 ? "Alacak" : "Sifir";

        bakiye.HesapSeviyesi = CalculateHesapSeviyesi(bakiye.HesapKodu);
        bakiye.UstHesapKodu = GetUstHesapKodu(bakiye.HesapKodu);
    }

    // ── Rebuild yardımcı tipleri ve metotları ──

    private sealed record BakiyeKey(
        int TesisId,
        int MaliYil,
        int Donem,
        int MuhasebeHesapPlaniId,
        bool KonsolideMi);

    private sealed class BakiyeAggregate
    {
        public int TesisId { get; set; }
        public int MaliYil { get; set; }
        public int Donem { get; set; }
        public int MuhasebeHesapPlaniId { get; set; }
        public string HesapKodu { get; set; } = string.Empty;
        public string HesapAdi { get; set; } = string.Empty;
        public bool KonsolideMi { get; set; }
        public decimal BorcToplam { get; set; }
        public decimal AlacakToplam { get; set; }
    }

    /// <summary>
    /// Nokta ile ayrılmış tam hesap kodundan üst hesap kodlarını türetir.
    /// Örn: "150.01.001" → ["150", "150.01"]
    /// Örn: "150" → []
    /// </summary>
    private static List<string> GetUstHesapKodlari(string tamKod)
    {
        var result = new List<string>();

        if (string.IsNullOrWhiteSpace(tamKod))
            return result;

        var parts = tamKod.Split('.', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length <= 1)
            return result;

        for (var i = 1; i < parts.Length; i++)
        {
            result.Add(string.Join('.', parts.Take(i)));
        }

        return result;
    }
}
