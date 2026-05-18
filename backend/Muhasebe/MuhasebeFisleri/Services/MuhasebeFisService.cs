using AutoMapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeFisleri.Dtos;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.MuhasebeFisleri.Services;

public class MuhasebeFisService
    : BaseRdbmsService<MuhasebeFisDto, MuhasebeFis, int>,
      IMuhasebeFisService
{
    private readonly IMuhasebeFisRepository _repository;
    private readonly StysAppDbContext _dbContext;
    private readonly IMuhasebeDonemService _muhasebeDonemService;

    public MuhasebeFisService(
        IMuhasebeFisRepository repository,
        IMapper mapper,
        StysAppDbContext dbContext,
        IMuhasebeDonemService muhasebeDonemService)
        : base(repository, mapper)
    {
        _repository = repository;
        _dbContext = dbContext;
        _muhasebeDonemService = muhasebeDonemService;
    }

    public async Task<MuhasebeFisDto?> GetByIdWithSatirlarAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdWithSatirlarAsync(id, cancellationToken);
        return Mapper.Map<MuhasebeFisDto?>(entity);
    }

    private async Task<int> YevmiyeNoUretAsync(int tesisId, int maliYil, CancellationToken cancellationToken)
    {
        var dbTransaction = _dbContext.Database.CurrentTransaction;
        if (dbTransaction is null)
        {
            throw new BaseException("Yevmiye no üretimi için aktif transaction bulunamadı.", 500);
        }

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            var sayac = await _dbContext.MuhasebeYevmiyeNoSayaclari
                .FromSqlInterpolated($@"
SELECT *
FROM [muhasebe].[MuhasebeYevmiyeNoSayaclari] WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
WHERE [IsDeleted] = 0 AND [TesisId] = {tesisId} AND [MaliYil] = {maliYil}")
                .FirstOrDefaultAsync(cancellationToken);

            if (sayac is null)
            {
                var created = new MuhasebeYevmiyeNoSayac
                {
                    TesisId = tesisId,
                    MaliYil = maliYil,
                    SonNumara = 1
                };

                await _dbContext.MuhasebeYevmiyeNoSayaclari.AddAsync(created, cancellationToken);
                try
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    return created.SonNumara;
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
                return sayac.SonNumara;
            }
        }

        // Son deneme: lock ile yeniden al ve artır
        var finalSayac = await _dbContext.MuhasebeYevmiyeNoSayaclari
            .FromSqlInterpolated($@"
SELECT *
FROM [muhasebe].[MuhasebeYevmiyeNoSayaclari] WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
WHERE [IsDeleted] = 0 AND [TesisId] = {tesisId} AND [MaliYil] = {maliYil}")
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BaseException("Yevmiye no sayaç kaydı oluşturulamadı.", 409);

        finalSayac.SonNumara += 1;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return finalSayac.SonNumara;
    }

    public async Task<MuhasebeFisDto> OnaylaAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var fis = await _dbContext.MuhasebeFisler
                .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

            if (fis is null)
                throw new BaseException("Fiş bulunamadı.", 404);

            // 1. Sadece Taslak fiş onaylanabilir
            if (fis.Durum != MuhasebeFisDurumlari.Taslak)
                throw new BaseException("Yalnızca taslak durumundaki fişler onaylanabilir.", 400);

            // 2. YevmiyeNo zaten varsa tekrar onaylanamaz
            if (fis.YevmiyeNo.HasValue)
                throw new BaseException("Fiş zaten onaylanmış.", 400);

            // 3. Aktif satırları al ve en az iki satır kontrolü
            var aktifSatirlar = fis.Satirlar.Where(s => !s.IsDeleted).ToList();
            if (aktifSatirlar.Count < 2)
                throw new BaseException("Onaylanacak fiş en az iki satır içermelidir.", 400);

            // 4. Satır bazlı borç/alacak kontrolleri
            foreach (var satir in aktifSatirlar)
            {
                if (satir.Borc < 0 || satir.Alacak < 0)
                    throw new BaseException($"Satır {satir.SiraNo}: borç veya alacak negatif olamaz.", 400);
                if (satir.Borc > 0 && satir.Alacak > 0)
                    throw new BaseException($"Satır {satir.SiraNo}: hem borç hem alacak girilemez.", 400);
                if (satir.Borc == 0 && satir.Alacak == 0)
                    throw new BaseException($"Satır {satir.SiraNo}: borç veya alacak girilmelidir.", 400);
            }

            // 5. Satır toplamlarını hesapla
            var satirToplamBorc = aktifSatirlar.Sum(x => x.Borc);
            var satirToplamAlacak = aktifSatirlar.Sum(x => x.Alacak);

            // 6. Satır toplamları denge kontrolü
            if (satirToplamBorc != satirToplamAlacak)
                throw new BaseException($"Satır toplam borç ({satirToplamBorc:N2}) ile satır toplam alacak ({satirToplamAlacak:N2}) eşit olmalıdır.", 400);

            // 7. Satır toplam borç > 0
            if (satirToplamBorc <= 0)
                throw new BaseException("Toplam borç tutarı sıfırdan büyük olmalıdır.", 400);

            // 8. Fiş başlığı toplamları satır toplamlarıyla uyumlu olmalı
            if (fis.ToplamBorc != satirToplamBorc || fis.ToplamAlacak != satirToplamAlacak)
                throw new BaseException("Fiş toplamları satır toplamları ile uyumlu değildir.", 400);

            // 9. Açık dönem kontrolü
            var donem = await _muhasebeDonemService.GetAktifDonemAsync(fis.TesisId, fis.FisTarihi, cancellationToken);
            if (donem is null)
                throw new BaseException("Fiş tarihi için açık muhasebe dönemi bulunamadı.", 400);
            if (fis.MaliYil != donem.MaliYil || fis.Donem != donem.DonemNo)
                throw new BaseException("Fişin mali yılı/dönemi, açık muhasebe dönemi ile uyumlu değildir.", 400);

            // 10. Satır hesaplarını doğrula
            foreach (var satir in aktifSatirlar)
            {
                var hesap = await _dbContext.MuhasebeHesapPlanlari
                    .FirstOrDefaultAsync(x => x.Id == satir.MuhasebeHesapPlaniId, cancellationToken);

                if (hesap is null)
                    throw new BaseException($"Satır {satir.SiraNo}: seçilen muhasebe hesabı bulunamadı.", 400);
                if (hesap.IsDeleted)
                    throw new BaseException($"Satır {satir.SiraNo}: seçilen muhasebe hesabı silinmiştir.", 400);
                if (!hesap.AktifMi)
                    throw new BaseException($"Satır {satir.SiraNo}: seçilen muhasebe hesabı aktif değildir.", 400);
                if (!hesap.DetayHesapMi)
                    throw new BaseException($"Satır {satir.SiraNo}: ana hesap seçilemez. Detay hesap seçilmelidir.", 400);
                if (!hesap.HareketGorebilirMi)
                    throw new BaseException($"Satır {satir.SiraNo}: hareket görebilir detay hesap seçilmelidir.", 400);
            }

            // 11. Yevmiye no üret
            var yevmiyeNo = await YevmiyeNoUretAsync(fis.TesisId, fis.MaliYil, cancellationToken);

            // 12. Fişi onayla
            fis.YevmiyeNo = yevmiyeNo;
            fis.Durum = MuhasebeFisDurumlari.Onayli;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // Reload
            var reloaded = await _repository.GetByIdWithSatirlarAsync(fis.Id, cancellationToken)
                ?? throw new BaseException("Onaylanan fiş okunamadı.", 500);
            return Mapper.Map<MuhasebeFisDto>(reloaded);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<MuhasebeFisDto> IptalEtAsync(int id, string? aciklama = null, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // 1. Orijinal fişi satırlarıyla birlikte getir
            var orijinalFis = await _dbContext.MuhasebeFisler
                .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

            if (orijinalFis is null)
                throw new BaseException("Fiş bulunamadı.", 404);

            // 2. Daha önce iptal edilmemiş olmalı
            if (orijinalFis.Durum == MuhasebeFisDurumlari.Iptal)
                throw new BaseException("Fiş zaten iptal edilmiş.", 400);

            // 3. Ters kayıt fişi iptal edilemez
            if (orijinalFis.Durum == MuhasebeFisDurumlari.TersKayit)
                throw new BaseException("Ters kayıt fişi iptal edilemez.", 400);

            // 4. Sadece Onayli fiş iptal edilebilir
            if (orijinalFis.Durum != MuhasebeFisDurumlari.Onayli)
                throw new BaseException("Yalnızca onaylı durumdaki fişler iptal edilebilir.", 400);

            // 5. TersKayitFisId doluysa zaten iptal
            if (orijinalFis.TersKayitFisId.HasValue)
                throw new BaseException("Fiş zaten iptal edilmiş.", 400);

            // 6. YevmiyeNo dolu olmalı
            if (!orijinalFis.YevmiyeNo.HasValue)
                throw new BaseException("Fişin yevmiye numarası bulunamadı.", 400);

            // 7. Açık dönem kontrolü
            var donem = await _muhasebeDonemService.GetAktifDonemAsync(orijinalFis.TesisId, orijinalFis.FisTarihi, cancellationToken);
            if (donem is null)
                throw new BaseException("Fiş tarihi için açık muhasebe dönemi bulunamadı.", 400);

            // 8. Aktif satırları al ve kontrol et
            var aktifSatirlar = orijinalFis.Satirlar.Where(s => !s.IsDeleted).ToList();
            if (aktifSatirlar.Count < 2)
                throw new BaseException("İptal edilecek fiş en az iki satır içermelidir.", 400);

            // 9. Ters kayıt fişini oluştur
            var tersFis = new MuhasebeFis
            {
                TesisId = orijinalFis.TesisId,
                MaliYil = orijinalFis.MaliYil,
                Donem = orijinalFis.Donem,
                FisNo = "TERS-" + orijinalFis.FisNo,
                FisTarihi = orijinalFis.FisTarihi,
                FisTipi = MuhasebeFisTipleri.Duzeltme,
                KaynakModul = orijinalFis.KaynakModul,
                KaynakId = orijinalFis.KaynakId,
                Durum = MuhasebeFisDurumlari.TersKayit,
                IptalEdilenFisId = orijinalFis.Id,
                Aciklama = !string.IsNullOrWhiteSpace(aciklama)
                    ? aciklama
                    : $"Fiş iptal ters kaydı: {orijinalFis.FisNo}",
                ToplamBorc = orijinalFis.ToplamAlacak,
                ToplamAlacak = orijinalFis.ToplamBorc,
                Satirlar = aktifSatirlar.Select(s => new MuhasebeFisSatir
                {
                    MuhasebeHesapPlaniId = s.MuhasebeHesapPlaniId,
                    SiraNo = s.SiraNo,
                    Borc = s.Alacak,
                    Alacak = s.Borc,
                    ParaBirimi = s.ParaBirimi,
                    Kur = s.Kur,
                    CariKartId = s.CariKartId,
                    TasinirKartId = s.TasinirKartId,
                    DepoId = s.DepoId,
                    KasaBankaHesapId = s.KasaBankaHesapId,
                    Aciklama = "Ters kayıt: " + (s.Aciklama ?? ""),
                }).ToList(),
            };

            // 10. Ters kayıt borç/alacak dengesi kontrolü
            var tersToplamBorc = tersFis.Satirlar.Sum(x => x.Borc);
            var tersToplamAlacak = tersFis.Satirlar.Sum(x => x.Alacak);

            if (tersToplamBorc != tersToplamAlacak)
                throw new BaseException($"Ters kayıt toplam borç ({tersToplamBorc:N2}) ile toplam alacak ({tersToplamAlacak:N2}) eşit olmalıdır.", 400);

            if (tersToplamBorc <= 0)
                throw new BaseException("Ters kayıt toplam borç tutarı sıfırdan büyük olmalıdır.", 400);

            // 11. Ters kayıt satır hesaplarını doğrula
            foreach (var satir in tersFis.Satirlar)
            {
                var hesap = await _dbContext.MuhasebeHesapPlanlari
                    .FirstOrDefaultAsync(x => x.Id == satir.MuhasebeHesapPlaniId, cancellationToken);

                if (hesap is null)
                    throw new BaseException($"Satır {satir.SiraNo}: seçilen muhasebe hesabı bulunamadı.", 400);
                if (hesap.IsDeleted)
                    throw new BaseException($"Satır {satir.SiraNo}: seçilen muhasebe hesabı silinmiştir.", 400);
                if (!hesap.AktifMi)
                    throw new BaseException($"Satır {satir.SiraNo}: seçilen muhasebe hesabı aktif değildir.", 400);
                if (!hesap.DetayHesapMi)
                    throw new BaseException($"Satır {satir.SiraNo}: ana hesap seçilemez. Detay hesap seçilmelidir.", 400);
                if (!hesap.HareketGorebilirMi)
                    throw new BaseException($"Satır {satir.SiraNo}: hareket görebilir detay hesap seçilmelidir.", 400);
            }

            // 12. Ters kayıt fişine yevmiye no üret
            var yevmiyeNo = await YevmiyeNoUretAsync(tersFis.TesisId, tersFis.MaliYil, cancellationToken);
            tersFis.YevmiyeNo = yevmiyeNo;

            // 13. Ters kayıt fişini kaydet
            await _dbContext.MuhasebeFisler.AddAsync(tersFis, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // 14. Orijinal fişi iptal et ve TersKayitFisId ata
            orijinalFis.Durum = MuhasebeFisDurumlari.Iptal;
            orijinalFis.TersKayitFisId = tersFis.Id;

            await _dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            // Reload
            var reloaded = await _repository.GetByIdWithSatirlarAsync(orijinalFis.Id, cancellationToken)
                ?? throw new BaseException("İptal edilen fiş okunamadı.", 500);
            return Mapper.Map<MuhasebeFisDto>(reloaded);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<List<MuhasebeFisDto>> GetFilteredAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default)
    {
        filter.Normalize();
        var entities = await _repository.GetFilteredAsync(filter, cancellationToken);
        return Mapper.Map<List<MuhasebeFisDto>>(entities);
    }

    public async Task<int> CountFilteredAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default)
    {
        filter.Normalize();
        return await _repository.CountFilteredAsync(filter, cancellationToken);
    }

    public async Task<YevmiyeDefteriDto> GetYevmiyeDefteriAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default)
    {
        filter.Normalize();

        var fisler = await _repository.GetYevmiyeDefteriAsync(filter, cancellationToken);

        var satirlar = new List<YevmiyeDefteriSatirDto>();

        foreach (var fis in fisler)
        {
            foreach (var satir in fis.Satirlar.OrderBy(s => s.SiraNo))
            {
                satirlar.Add(new YevmiyeDefteriSatirDto
                {
                    FisId = fis.Id,
                    FisNo = fis.FisNo,
                    YevmiyeNo = fis.YevmiyeNo,
                    FisTarihi = fis.FisTarihi,
                    FisTipi = fis.FisTipi,
                    Durum = fis.Durum,
                    SiraNo = satir.SiraNo,
                    MuhasebeHesapPlaniId = satir.MuhasebeHesapPlaniId,
                    MuhasebeHesapKodu = satir.MuhasebeHesapPlani?.TamKod,
                    MuhasebeHesapAdi = satir.MuhasebeHesapPlani?.Ad,
                    Borc = satir.Borc,
                    Alacak = satir.Alacak,
                    SatirAciklama = satir.Aciklama,
                    FisAciklama = fis.Aciklama,
                    KaynakModul = fis.KaynakModul,
                    KaynakId = fis.KaynakId,
                });
            }
        }

        return new YevmiyeDefteriDto
        {
            Satirlar = satirlar,
            ToplamBorc = satirlar.Sum(s => s.Borc),
            ToplamAlacak = satirlar.Sum(s => s.Alacak),
        };
    }

    private static bool IsUniqueConflict(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627);
    }

    public async Task<List<MuhasebeFisDto>> GetByKaynakAsync(
        string kaynakModul, int kaynakId, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByKaynakAsync(kaynakModul, kaynakId, cancellationToken);
        return Mapper.Map<List<MuhasebeFisDto>>(entities);
    }

    public override async Task<MuhasebeFisDto> AddAsync(MuhasebeFisDto dto)
    {
        await NormalizeAndValidateCreateAsync(dto, CancellationToken.None);
        var entity = Mapper.Map<MuhasebeFis>(dto);
        entity.ToplamBorc = dto.ToplamBorc;
        entity.ToplamAlacak = dto.ToplamAlacak;
        entity.Durum = MuhasebeFisDurumlari.Taslak;
        entity.FisNo = string.IsNullOrWhiteSpace(dto.FisNo)
            ? $"TASLAK-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
            : dto.FisNo;
        entity.Satirlar = Mapper.Map<List<MuhasebeFisSatir>>(dto.Satirlar);
        await _dbContext.MuhasebeFisler.AddAsync(entity);
        await _dbContext.SaveChangesAsync();
        var created = await _repository.GetByIdWithSatirlarAsync(entity.Id)
            ?? throw new BaseException("Fiş oluşturulamadı.", 500);
        return Mapper.Map<MuhasebeFisDto>(created);
    }

    public override async Task<MuhasebeFisDto> UpdateAsync(MuhasebeFisDto dto)
    {
        var existing = await _dbContext.MuhasebeFisler
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == dto.Id && !x.IsDeleted);

        if (existing is null)
            throw new BaseException("Fiş bulunamadı.", 404);

        if (existing.Durum != MuhasebeFisDurumlari.Taslak)
            throw new BaseException("Yalnızca taslak durumundaki fişler güncellenebilir.", 400);

        // Normalize ve validate
        await NormalizeAndValidateCreateAsync(dto, CancellationToken.None);

        // Header alanlarını güncelle
        existing.TesisId = dto.TesisId;
        existing.MaliYil = dto.MaliYil;
        existing.Donem = dto.Donem;
        existing.FisTarihi = dto.FisTarihi;
        existing.FisTipi = dto.FisTipi;
        existing.Aciklama = dto.Aciklama;
        existing.ToplamBorc = dto.ToplamBorc;
        existing.ToplamAlacak = dto.ToplamAlacak;

        // Eski satırları sil (sadece silinmemiş olanları)
        foreach (var oldSatir in existing.Satirlar.Where(s => !s.IsDeleted))
        {
            _dbContext.Entry(oldSatir).State = EntityState.Deleted;
        }
        existing.Satirlar.Clear();

        foreach (var satirDto in dto.Satirlar)
        {
            var satir = Mapper.Map<MuhasebeFisSatir>(satirDto);
            satir.MuhasebeFisId = existing.Id;
            existing.Satirlar.Add(satir);
        }

        await _dbContext.SaveChangesAsync();

        // Reload complete entity with satırlar
        var reloaded = await _repository.GetByIdWithSatirlarAsync(existing.Id)
            ?? throw new BaseException("Güncellenen fiş okunamadı.", 500);
        return Mapper.Map<MuhasebeFisDto>(reloaded);
    }

    public override async Task DeleteAsync(int id)
    {
        var existing = await _dbContext.MuhasebeFisler
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (existing is null)
            throw new BaseException("Fiş bulunamadı.", 404);

        if (existing.Durum != MuhasebeFisDurumlari.Taslak)
            throw new BaseException("Yalnızca taslak durumundaki fişler silinebilir.", 400);

        // Platform BaseEntity silme davranışı üzerinden fiş ve satırları sil
        foreach (var satir in existing.Satirlar.Where(s => !s.IsDeleted))
        {
            _dbContext.Entry(satir).State = EntityState.Deleted;
        }

        // Platform BaseEntity silme davranışı üzerinden fişi sil
        _dbContext.Entry(existing).State = EntityState.Deleted;
        await _dbContext.SaveChangesAsync();
    }

    private async Task NormalizeAndValidateCreateAsync(MuhasebeFisDto dto, CancellationToken cancellationToken)
    {
        // 1. TesisId > 0
        if (dto.TesisId <= 0)
            throw new BaseException("Geçerli bir tesis seçilmelidir.", 400);

        // 2. MaliYil geçerli (2000-2100)
        if (dto.MaliYil < 2000 || dto.MaliYil > 2100)
            throw new BaseException("Mali yıl 2000-2100 aralığında olmalıdır.", 400);

        // 3. Donem 1-12
        if (dto.Donem < 1 || dto.Donem > 12)
            throw new BaseException("Dönem 1-12 aralığında olmalıdır.", 400);

        // 4. FisTarihi zorunlu
        if (dto.FisTarihi == default)
            throw new BaseException("Fiş tarihi zorunludur.", 400);

        // 4b. Açık muhasebe dönemi kontrolü
        var donem = await _muhasebeDonemService.GetAktifDonemAsync(
            dto.TesisId,
            dto.FisTarihi,
            cancellationToken);

        if (donem is null)
            throw new BaseException("Fiş tarihi için açık muhasebe dönemi bulunamadı.", 400);

        if (dto.MaliYil != donem.MaliYil || dto.Donem != donem.DonemNo)
            throw new BaseException("Fişin mali yılı/dönemi, açık muhasebe dönemi ile uyumlu değildir.", 400);

        // 5. FisTipi desteklenen
        if (string.IsNullOrWhiteSpace(dto.FisTipi))
            throw new BaseException("Fiş tipi boş olamaz.", 400);
        if (!MuhasebeFisTipleri.Hepsi.Contains(dto.FisTipi))
            throw new BaseException($"Desteklenmeyen fiş tipi: {dto.FisTipi}.", 400);

        // 6-7. KaynakModul
        if (string.IsNullOrWhiteSpace(dto.KaynakModul))
            dto.KaynakModul = MuhasebeKaynakModulleri.Manuel;
        if (!MuhasebeKaynakModulleri.Hepsi.Contains(dto.KaynakModul))
            throw new BaseException($"Desteklenmeyen kaynak modül: {dto.KaynakModul}.", 400);

        // 8. Durum create sırasında Taslak olmalı (controller'dan gelmez, service set eder)
        dto.Durum = MuhasebeFisDurumlari.Taslak;

        // 9. En az iki satır olmalı
        if (dto.Satirlar is null || dto.Satirlar.Count < 2)
            throw new BaseException("En az iki fiş satırı gereklidir.", 400);

        decimal toplamBorc = 0;
        decimal toplamAlacak = 0;

        // Satır validasyonları
        for (int i = 0; i < dto.Satirlar.Count; i++)
        {
            var satir = dto.Satirlar[i];

            // 10. MuhasebeHesapPlaniId > 0
            if (satir.MuhasebeHesapPlaniId <= 0)
                throw new BaseException($"{i + 1}. satırda geçerli bir muhasebe hesabı seçilmelidir.", 400);

            // 11. Borc ve Alacak aynı anda > 0 olamaz
            if (satir.Borc > 0 && satir.Alacak > 0)
                throw new BaseException($"{i + 1}. satırda hem borç hem alacak girilemez.", 400);

            // 12. Borc ve Alacak ikisi de 0 olamaz
            if (satir.Borc == 0 && satir.Alacak == 0)
                throw new BaseException($"{i + 1}. satırda borç veya alacak girilmelidir.", 400);

            // 13. Negatif olamaz
            if (satir.Borc < 0 || satir.Alacak < 0)
                throw new BaseException($"{i + 1}. satırda borç veya alacak negatif olamaz.", 400);

            // 15. Sadece DetayHesapMi=true ve HareketGorebilirMi=true hesaplara yazılabilir
            var hesap = await _dbContext.MuhasebeHesapPlanlari
                .FirstOrDefaultAsync(x => x.Id == satir.MuhasebeHesapPlaniId, cancellationToken);

            if (hesap is null)
                throw new BaseException($"{i + 1}. satırda seçilen muhasebe hesabı bulunamadı.", 400);
            if (hesap.IsDeleted)
                throw new BaseException($"{i + 1}. satırda seçilen muhasebe hesabı silinmiştir.", 400);
            if (!hesap.AktifMi)
                throw new BaseException($"{i + 1}. satırda seçilen muhasebe hesabı aktif değildir.", 400);
            if (!hesap.DetayHesapMi)
                throw new BaseException($"{i + 1}. satırda ana hesap seçilemez. Detay hesap seçilmelidir.", 400);
            if (!hesap.HareketGorebilirMi)
                throw new BaseException($"{i + 1}. satırda hareket görebilir detay hesap seçilmelidir.", 400);

            // 16. ParaBirimi boşsa TRY
            if (string.IsNullOrWhiteSpace(satir.ParaBirimi))
                satir.ParaBirimi = "TRY";

            // 17. Kur <= 0 ise 1
            if (satir.Kur <= 0)
                satir.Kur = 1;

            // 18. SiraNo boş/0 ise otomatik sırala (i+1)
            if (satir.SiraNo <= 0)
                satir.SiraNo = i + 1;

            toplamBorc += satir.Borc;
            toplamAlacak += satir.Alacak;
        }

        // 14. Toplam Borc = Toplam Alacak
        if (toplamBorc != toplamAlacak)
            throw new BaseException($"Toplam borç ({toplamBorc:N2}) ile toplam alacak ({toplamAlacak:N2}) eşit olmalıdır.", 400);

        dto.ToplamBorc = toplamBorc;
        dto.ToplamAlacak = toplamAlacak;

    }
}
