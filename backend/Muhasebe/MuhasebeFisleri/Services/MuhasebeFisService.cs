using AutoMapper;
using ClosedXML.Excel;
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
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Entities;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Services;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;

namespace STYS.Muhasebe.MuhasebeFisleri.Services;

public class MuhasebeFisService
    : BaseRdbmsService<MuhasebeFisDto, MuhasebeFis, int>,
      IMuhasebeFisService
{
    private readonly IMuhasebeFisRepository _repository;
    private readonly StysAppDbContext _dbContext;
    private readonly IMuhasebeDonemService _muhasebeDonemService;
    private readonly IMuhasebeHesapBakiyeGuncellemeService _muhasebeHesapBakiyeGuncellemeService;

    public MuhasebeFisService(
        IMuhasebeFisRepository repository,
        IMapper mapper,
        StysAppDbContext dbContext,
        IMuhasebeDonemService muhasebeDonemService,
        IMuhasebeHesapBakiyeGuncellemeService muhasebeHesapBakiyeGuncellemeService)
        : base(repository, mapper)
    {
        _repository = repository;
        _dbContext = dbContext;
        _muhasebeDonemService = muhasebeDonemService;
        _muhasebeHesapBakiyeGuncellemeService = muhasebeHesapBakiyeGuncellemeService;
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
            await ValidateOpenPeriodAsync(fis.TesisId, fis.FisTarihi, fis.MaliYil, fis.Donem, cancellationToken);

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

            // 13. Hesap bakiyelerini güncelle
            await _muhasebeHesapBakiyeGuncellemeService.FisBakiyeleriniIsleAsync(
                fis,
                cancellationToken);

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
            await ValidateOpenPeriodAsync(orijinalFis.TesisId, orijinalFis.FisTarihi, orijinalFis.MaliYil, orijinalFis.Donem, cancellationToken);

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

            // 15. Ters kayıt fişi bakiyelerini güncelle
            await _muhasebeHesapBakiyeGuncellemeService.FisBakiyeleriniIsleAsync(
                tersFis,
                cancellationToken);

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

    public async Task<byte[]> ExportYevmiyeDefteriExcelAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default)
    {
        // 1. Mevcut GetYevmiyeDefteriAsync mantığını kullan — pagination uygulanmıyor, tüm veri gelir
        var yevmiyeDefteri = await GetYevmiyeDefteriAsync(filter, cancellationToken);
        var satirlar = yevmiyeDefteri.Satirlar;

        // 2. Excel oluştur
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Yevmiye Defteri");

        var now = DateTime.Now;
        var baslangicStr = filter.BaslangicTarihi.HasValue ? filter.BaslangicTarihi.Value.ToString("dd.MM.yyyy") : "-";
        var bitisStr = filter.BitisTarihi.HasValue ? filter.BitisTarihi.Value.ToString("dd.MM.yyyy") : "-";
        var fisTipiStr = !string.IsNullOrWhiteSpace(filter.FisTipi) ? filter.FisTipi : "Tümü";
        var durumStr = !string.IsNullOrWhiteSpace(filter.Durum) ? filter.Durum : "Onaylı / Ters Kayıt";

        // Üst bilgi satırları
        ws.Cell(1, 1).Value = "Yevmiye Defteri Raporu";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value = "Export Tarihi:";
        ws.Cell(2, 2).Value = now.ToString("dd.MM.yyyy HH:mm");
        ws.Cell(3, 1).Value = "Tesis Id:";
        ws.Cell(3, 2).Value = filter.TesisId;
        ws.Cell(4, 1).Value = "Mali Yıl:";
        ws.Cell(4, 2).Value = filter.MaliYil;
        ws.Cell(5, 1).Value = "Tarih Aralığı:";
        ws.Cell(5, 2).Value = $"{baslangicStr} / {bitisStr}";
        ws.Cell(6, 1).Value = "Fiş Tipi:";
        ws.Cell(6, 2).Value = fisTipiStr;
        ws.Cell(7, 1).Value = "Durum:";
        ws.Cell(7, 2).Value = durumStr;

        // Sütun başlıkları (row 9)
        var headers = new[] { "Fiş No", "Fiş Tarihi", "Fiş Tipi", "Durum", "Hesap Kodu", "Hesap Adı", "Satır Açıklama", "Borç", "Alacak", "Fiş Açıklama", "Belge No", "Kaynak Modül", "Kaynak Id" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(9, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
        }
        ws.SheetView.FreezeRows(9);

        // Veri satırları (row 10'dan başlayarak)
        int row = 10;
        foreach (var satir in satirlar)
        {
            ws.Cell(row, 1).Value = satir.FisNo;
            ws.Cell(row, 2).Value = satir.FisTarihi;
            ws.Cell(row, 2).Style.DateFormat.Format = "dd.MM.yyyy";
            ws.Cell(row, 3).Value = satir.FisTipi;
            ws.Cell(row, 4).Value = satir.Durum;
            ws.Cell(row, 5).Value = satir.MuhasebeHesapKodu ?? "";
            ws.Cell(row, 6).Value = satir.MuhasebeHesapAdi ?? "";
            ws.Cell(row, 7).Value = satir.SatirAciklama ?? "";
            ws.Cell(row, 8).Value = satir.Borc;
            ws.Cell(row, 9).Value = satir.Alacak;
            ws.Cell(row, 10).Value = satir.FisAciklama ?? "";
            ws.Cell(row, 11).Value = "-";
            ws.Cell(row, 12).Value = satir.KaynakModul;
            ws.Cell(row, 13).Value = satir.KaynakId;
            row++;
        }

        // Sayı formatı (Borç, Alacak sütunları: 8 ve 9)
        ws.Column(8).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(9).Style.NumberFormat.Format = "#,##0.00";

        // Toplam satırı
        ws.Cell(row, 1).Value = "TOPLAM";
        ws.Cell(row, 8).Value = yevmiyeDefteri.ToplamBorc;
        ws.Cell(row, 9).Value = yevmiyeDefteri.ToplamAlacak;
        // Borç/Alacak eşitlik kontrolü
        if (yevmiyeDefteri.ToplamBorc != yevmiyeDefteri.ToplamAlacak)
        {
            ws.Cell(row, 10).Value = "UYARI: Borç-Alacak eşit değil!";
            ws.Cell(row, 10).Style.Font.FontColor = XLColor.Red;
        }
        for (int i = 1; i <= 13; i++)
            ws.Cell(row, i).Style.Font.Bold = true;

        // Otomatik sütun genişliği ve filtre
        ws.Columns().AdjustToContents();
        ws.RangeUsed()?.SetAutoFilter();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<MuavinDefterDto> GetMuavinDefterAsync(MuavinDefterFilterDto filter, CancellationToken cancellationToken = default)
    {
        // 1. Tüm veriyi hesapla (pagination uygulamadan)
        var (tumSatirlar, hesap, toplamBorc, toplamAlacak, netBakiye) = await BuildMuavinDefterCoreAsync(filter, cancellationToken);

        // 2. Sayfalama (sadece istenen sayfadaki satırlar)
        var pagedSatirlar = tumSatirlar
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        // 3. DTO'yu oluştur
        return new MuavinDefterDto
        {
            TesisId = filter.TesisId,
            MuhasebeHesapPlaniId = filter.MuhasebeHesapPlaniId,
            MuhasebeHesapKodu = hesap.TamKod,
            MuhasebeHesapAdi = hesap.Ad,
            ToplamBorc = toplamBorc,
            ToplamAlacak = toplamAlacak,
            Bakiye = Math.Abs(netBakiye),
            BakiyeTipi = netBakiye > 0 ? "Borc" : netBakiye < 0 ? "Alacak" : "Sifir",
            Satirlar = pagedSatirlar,
        };
    }

    public async Task<byte[]> ExportMuavinDefterExcelAsync(MuavinDefterFilterDto filter, CancellationToken cancellationToken = default)
    {
        // 1. Tüm veriyi hesapla (pagination uygulamadan)
        var (tumSatirlar, hesap, toplamBorc, toplamAlacak, netBakiye) = await BuildMuavinDefterCoreAsync(filter, cancellationToken);

        // 2. Excel oluştur
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Muavin Defter");

        var now = DateTime.Now;
        var baslangicStr = filter.BaslangicTarihi.HasValue ? filter.BaslangicTarihi.Value.ToString("dd.MM.yyyy") : "-";
        var bitisStr = filter.BitisTarihi.HasValue ? filter.BitisTarihi.Value.ToString("dd.MM.yyyy") : "-";
        var donemStr = filter.Donem.HasValue ? filter.Donem.Value.ToString() : "Tümü";
        var altHesaplarDahilStr = filter.AltHesaplariDahilEt ? "Evet" : "Hayır";

        // Üst bilgi satırları
        ws.Cell(1, 1).Value = "Muavin Defter Raporu";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value = "Export Tarihi:";
        ws.Cell(2, 2).Value = now.ToString("dd.MM.yyyy HH:mm");
        ws.Cell(3, 1).Value = "Tesis Id:";
        ws.Cell(3, 2).Value = filter.TesisId;
        ws.Cell(4, 1).Value = "Mali Yıl:";
        ws.Cell(4, 2).Value = filter.MaliYil?.ToString() ?? "-";
        ws.Cell(5, 1).Value = "Dönem:";
        ws.Cell(5, 2).Value = donemStr;
        ws.Cell(6, 1).Value = "Tarih Aralığı:";
        ws.Cell(6, 2).Value = $"{baslangicStr} / {bitisStr}";
        ws.Cell(7, 1).Value = "Hesap Kodu:";
        ws.Cell(7, 2).Value = hesap.TamKod ?? "-";
        ws.Cell(8, 1).Value = "Alt Hesaplar Dahil:";
        ws.Cell(8, 2).Value = altHesaplarDahilStr;

        // Sütun başlıkları (row 10)
        var headers = new[] { "Tarih", "Yevmiye No", "Fiş No", "Fiş Tipi", "Hesap Kodu", "Hesap Adı", "Açıklama", "Borç", "Alacak", "Bakiye", "Bakiye Tipi" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(10, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
        }
        ws.SheetView.FreezeRows(10);

        // Veri satırları (row 11'den başlayarak)
        int row = 11;
        foreach (var satir in tumSatirlar)
        {
            ws.Cell(row, 1).Value = satir.FisTarihi;
            ws.Cell(row, 1).Style.DateFormat.Format = "dd.MM.yyyy";
            ws.Cell(row, 2).Value = satir.YevmiyeNo;
            ws.Cell(row, 3).Value = satir.FisNo;
            ws.Cell(row, 4).Value = satir.FisTipi;
            ws.Cell(row, 5).Value = satir.MuhasebeHesapKodu ?? "";
            ws.Cell(row, 6).Value = satir.MuhasebeHesapAdi ?? "";
            ws.Cell(row, 7).Value = satir.SatirAciklama ?? "";
            ws.Cell(row, 8).Value = satir.Borc;
            ws.Cell(row, 9).Value = satir.Alacak;
            ws.Cell(row, 10).Value = satir.Bakiye;
            ws.Cell(row, 11).Value = satir.BakiyeTipi;
            row++;
        }

        // Sayı formatı (Borç, Alacak, Bakiye sütunları: 8, 9, 10)
        ws.Column(8).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(9).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(10).Style.NumberFormat.Format = "#,##0.00";

        // Toplam satırı
        var sonBakiye = netBakiye;
        var sonBakiyeTipi = netBakiye > 0 ? "Borc" : netBakiye < 0 ? "Alacak" : "Sifir";
        ws.Cell(row, 1).Value = "TOPLAM";
        ws.Cell(row, 8).Value = toplamBorc;
        ws.Cell(row, 9).Value = toplamAlacak;
        ws.Cell(row, 10).Value = Math.Abs(sonBakiye);
        ws.Cell(row, 11).Value = sonBakiyeTipi;
        for (int i = 1; i <= 11; i++)
            ws.Cell(row, i).Style.Font.Bold = true;

        // Otomatik sütun genişliği ve filtre
        ws.Columns().AdjustToContents();
        ws.RangeUsed()?.SetAutoFilter();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private async Task<(List<MuavinDefterSatirDto> Satirlar, MuhasebeHesapPlani Hesap, decimal ToplamBorc, decimal ToplamAlacak, decimal NetBakiye)>
        BuildMuavinDefterCoreAsync(MuavinDefterFilterDto filter, CancellationToken cancellationToken)
    {
        // 1. Normalize
        filter.Normalize();

        // 2. Validasyon
        if (filter.TesisId <= 0)
            throw new BaseException("Geçerli bir tesis seçilmelidir.", 400);

        if (filter.MuhasebeHesapPlaniId <= 0)
            throw new BaseException("Geçerli bir muhasebe hesabı seçilmelidir.", 400);

        if (filter.BaslangicTarihi.HasValue && filter.BitisTarihi.HasValue && filter.BaslangicTarihi.Value > filter.BitisTarihi.Value)
            throw new BaseException("Başlangıç tarihi bitiş tarihinden büyük olamaz.", 400);

        // 3. Seçilen hesabı bul
        var hesap = await _dbContext.MuhasebeHesapPlanlari
            .FirstOrDefaultAsync(x => x.Id == filter.MuhasebeHesapPlaniId && !x.IsDeleted && x.AktifMi, cancellationToken);

        if (hesap is null)
            throw new BaseException("Seçilen muhasebe hesabı bulunamadı.", 404);

        var hesapKoduPrefix = hesap.TamKod!;

        // 4. Repository'den fişleri çek
        var fisler = await _repository.GetMuavinDefterAsync(filter, cancellationToken);

        // 5. Satırları flatten ve filtrele
        var tumSatirlar = new List<MuavinDefterSatirDto>();

        foreach (var fis in fisler)
        {
            foreach (var satir in fis.Satirlar.OrderBy(s => s.SiraNo))
            {
                // Hesap filtresi
                if (!filter.AltHesaplariDahilEt)
                {
                    if (satir.MuhasebeHesapPlaniId != filter.MuhasebeHesapPlaniId)
                        continue;
                }
                else
                {
                    if (satir.MuhasebeHesapPlani?.TamKod is null ||
                        !satir.MuhasebeHesapPlani.TamKod.StartsWith(hesapKoduPrefix))
                        continue;
                }

                tumSatirlar.Add(new MuavinDefterSatirDto
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

        // 6. Sırala: FisTarihi, YevmiyeNo, FisId, SiraNo
        tumSatirlar = tumSatirlar
            .OrderBy(s => s.FisTarihi)
            .ThenBy(s => s.YevmiyeNo)
            .ThenBy(s => s.FisId)
            .ThenBy(s => s.SiraNo)
            .ToList();

        // 7. Yürüyen bakiye hesapla (tüm satırlar üzerinden)
        decimal bakiye = 0;
        foreach (var satir in tumSatirlar)
        {
            bakiye += satir.Borc - satir.Alacak;
            satir.Bakiye = Math.Abs(bakiye);
            satir.BakiyeTipi = bakiye > 0 ? "Borc" : bakiye < 0 ? "Alacak" : "Sifir";
        }

        // 8. Toplamlar (tüm filtrelenmiş satırlar üzerinden)
        var toplamBorc = tumSatirlar.Sum(s => s.Borc);
        var toplamAlacak = tumSatirlar.Sum(s => s.Alacak);
        var netBakiye = toplamBorc - toplamAlacak;

        return (tumSatirlar, hesap, toplamBorc, toplamAlacak, netBakiye);
    }

    public async Task<MizanDto> GetMizanAsync(MizanFilterDto filter, CancellationToken cancellationToken = default)
    {
        // 1. Normalize
        filter.Normalize();

        // 2. Validasyon
        if (filter.TesisId <= 0)
            throw new BaseException("Geçerli bir tesis seçilmelidir.", 400);

        if (filter.BaslangicTarihi.HasValue && filter.BitisTarihi.HasValue && filter.BaslangicTarihi.Value > filter.BitisTarihi.Value)
            throw new BaseException("Başlangıç tarihi bitiş tarihinden büyük olamaz.", 400);

        if (filter.MaliYil.HasValue && (filter.MaliYil.Value < 2000 || filter.MaliYil.Value > 2100))
            throw new BaseException("Mali yıl 2000-2100 aralığında olmalıdır.", 400);

        if (filter.Donem.HasValue && (filter.Donem.Value < 1 || filter.Donem.Value > 12))
            throw new BaseException("Dönem numarası 1-12 aralığında olmalıdır.", 400);

        // 3. Repository'den fişleri çek
        var fisler = await _repository.GetMizanFisleriAsync(filter, cancellationToken);

        // 4. Satırları flatten et, sadece geçerli hesap satırları
        var satirGruplari = new Dictionary<(int Id, string Kod, string Ad, bool Detay, bool HareketGor), (decimal Borc, decimal Alacak)>();

        foreach (var fis in fisler)
        {
            foreach (var satir in fis.Satirlar)
            {
                if (satir.IsDeleted) continue;
                var hesap = satir.MuhasebeHesapPlani;
                if (hesap is null || hesap.IsDeleted || !hesap.AktifMi) continue;

                var hesapKodu = hesap.TamKod ?? string.Empty;

                // 5. Hesap kodu aralığı filtresi
                if (filter.HesapKoduBaslangic is not null &&
                    string.Compare(hesapKodu, filter.HesapKoduBaslangic, StringComparison.Ordinal) < 0)
                    continue;

                if (filter.HesapKoduBitis is not null &&
                    string.Compare(hesapKodu, filter.HesapKoduBitis, StringComparison.Ordinal) > 0)
                    continue;

                var key = (hesap.Id, hesapKodu, hesap.Ad ?? string.Empty, hesap.DetayHesapMi, hesap.HareketGorebilirMi);

                if (satirGruplari.TryGetValue(key, out var totals))
                {
                    satirGruplari[key] = (totals.Borc + satir.Borc, totals.Alacak + satir.Alacak);
                }
                else
                {
                    satirGruplari[key] = (satir.Borc, satir.Alacak);
                }
            }
        }

        // 6. Mizan satırlarını oluştur
        var mizanSatirlar = new List<MizanSatirDto>();

        foreach (var kvp in satirGruplari)
        {
            var (id, kod, ad, detay, hareketGor) = kvp.Key;
            var (toplamBorc, toplamAlacak) = kvp.Value;

            // SadeceHareketGorenHesaplar filtresi
            if (filter.SadeceHareketGorenHesaplar && toplamBorc == 0 && toplamAlacak == 0)
                continue;

            var net = toplamBorc - toplamAlacak;

            mizanSatirlar.Add(new MizanSatirDto
            {
                MuhasebeHesapPlaniId = id,
                HesapKodu = kod,
                HesapAdi = ad,
                DetayHesapMi = detay,
                HareketGorebilirMi = hareketGor,
                ToplamBorc = toplamBorc,
                ToplamAlacak = toplamAlacak,
                BorcBakiye = net > 0 ? net : 0,
                AlacakBakiye = net < 0 ? Math.Abs(net) : 0,
                Bakiye = Math.Abs(net),
                BakiyeTipi = net > 0 ? "Borc" : net < 0 ? "Alacak" : "Sifir",
            });
        }

        // 7. Seviye ata (segment sayısı)
        foreach (var satir in mizanSatirlar)
        {
            satir.Seviye = satir.HesapKodu.Split('.').Length;
        }

        // 8. Genel toplamları konsolidasyon ÖNCESİ hesapla
        // (böylece sadece gerçek hareket satırları toplanır; üst hesap kendi
        //  hareketi varsa ve sonradan KonsolideSatirMi=true yapılırsa düşmez)
        var genelToplamBorc = mizanSatirlar.Sum(s => s.ToplamBorc);
        var genelToplamAlacak = mizanSatirlar.Sum(s => s.ToplamAlacak);
        var genelBorcBakiye = mizanSatirlar.Sum(s => s.BorcBakiye);
        var genelAlacakBakiye = mizanSatirlar.Sum(s => s.AlacakBakiye);

        // 9. AltHesaplariDahilEt konsolidasyonu
        if (filter.AltHesaplariDahilEt && mizanSatirlar.Count > 0)
        {
            // Gerçek hareket gören hesaplardan üst hesap kodlarını topla
            var ancestorKodlari = new HashSet<string>();
            foreach (var satir in mizanSatirlar)
            {
                foreach (var ustKod in GetUstHesapKodlari(satir.HesapKodu))
                {
                    ancestorKodlari.Add(ustKod);
                }
            }

            if (ancestorKodlari.Count > 0)
            {
                // Üst hesapları veritabanından çek
                var ancestorHesaplar = await _dbContext.MuhasebeHesapPlanlari
                    .AsNoTracking()
                    .Where(h => ancestorKodlari.Contains(h.TamKod) && h.AktifMi && !h.IsDeleted)
                    .ToDictionaryAsync(h => h.TamKod, h => h, cancellationToken);

                // Her üst hesap için alt hesaplardan toplamları konsolide et
                // (her zaman gerçek hareket satırlarından topla, konsolide satırları atla)
                foreach (var ancestorKod in ancestorKodlari)
                {
                    if (!ancestorHesaplar.TryGetValue(ancestorKod, out var ancestorHesap))
                        continue;

                    decimal consolidatedBorc = 0;
                    decimal consolidatedAlacak = 0;
                    var prefix = ancestorKod + ".";

                    foreach (var satir in mizanSatirlar)
                    {
                        if (satir.KonsolideSatirMi)
                            continue; // sadece gerçek hareket satırlarını topla
                        if (satir.HesapKodu.StartsWith(prefix, StringComparison.Ordinal))
                        {
                            consolidatedBorc += satir.ToplamBorc;
                            consolidatedAlacak += satir.ToplamAlacak;
                        }
                    }

                    if (filter.SadeceHareketGorenHesaplar && consolidatedBorc == 0 && consolidatedAlacak == 0)
                        continue;

                    // Üst hesap zaten mizanSatirlar'da varsa (kendi doğrudan hareketi olabilir),
                    // konsolide toplamları mevcut satıra ekle; yoksa yeni satır oluştur.
                    var existingAncestorSatir = mizanSatirlar
                        .FirstOrDefault(x => x.HesapKodu == ancestorKod && !x.KonsolideSatirMi);

                    if (existingAncestorSatir is not null)
                    {
                        existingAncestorSatir.ToplamBorc += consolidatedBorc;
                        existingAncestorSatir.ToplamAlacak += consolidatedAlacak;
                        existingAncestorSatir.KonsolideSatirMi = true;
                        RecalculateMizanSatirBakiye(existingAncestorSatir);
                    }
                    else
                    {
                        var net = consolidatedBorc - consolidatedAlacak;

                        mizanSatirlar.Add(new MizanSatirDto
                        {
                            MuhasebeHesapPlaniId = ancestorHesap.Id,
                            HesapKodu = ancestorKod,
                            HesapAdi = ancestorHesap.Ad ?? string.Empty,
                            DetayHesapMi = ancestorHesap.DetayHesapMi,
                            HareketGorebilirMi = ancestorHesap.HareketGorebilirMi,
                            ToplamBorc = consolidatedBorc,
                            ToplamAlacak = consolidatedAlacak,
                            BorcBakiye = net > 0 ? net : 0,
                            AlacakBakiye = net < 0 ? Math.Abs(net) : 0,
                            Bakiye = Math.Abs(net),
                            BakiyeTipi = net > 0 ? "Borc" : net < 0 ? "Alacak" : "Sifir",
                            KonsolideSatirMi = true,
                            Seviye = ancestorKod.Split('.').Length,
                        });
                    }
                }
            }
        }

        // 10. Sırala: HesapKodu ascending
        mizanSatirlar = mizanSatirlar
            .OrderBy(s => s.HesapKodu, StringComparer.Ordinal)
            .ToList();

        // 11. Sayfalama (hesap satırı bazlı)
        var pagedSatirlar = mizanSatirlar
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        // 12. DTO'yu döndür (genel toplamlar konsolidasyon öncesinden)
        return new MizanDto
        {
            TesisId = filter.TesisId,
            GenelToplamBorc = genelToplamBorc,
            GenelToplamAlacak = genelToplamAlacak,
            GenelBorcBakiye = genelBorcBakiye,
            GenelAlacakBakiye = genelAlacakBakiye,
            Satirlar = pagedSatirlar,
        };
    }

    /// <summary>
    /// Mizanı <see cref="MuhasebeHesapBakiye"/> özet tablosundan okur.
    /// Genel toplamlar ve hesap kodu bazlı aggregation DB tarafında yapılır;
    /// sadece istenen sayfa kadar kayıt belleğe alınır.
    /// </summary>
    public async Task<MizanDto> GetMizanBakiyeAsync(MizanFilterDto filter, CancellationToken cancellationToken = default)
    {
        // 1. Normalize
        filter.Normalize();

        // 2. Validasyon
        if (filter.TesisId <= 0)
            throw new BaseException("Geçerli bir tesis seçilmelidir.", 400);

        if (filter.BaslangicTarihi.HasValue || filter.BitisTarihi.HasValue)
            throw new BaseException("Bakiye tablosundan mizan için tarih aralığı yerine MaliYil/Donem filtresi kullanılmalıdır.", 400);

        if (!filter.MaliYil.HasValue)
            throw new BaseException("Bakiye tablosundan mizan için mali yıl seçilmelidir.", 400);

        if (filter.MaliYil.Value < 2000 || filter.MaliYil.Value > 2100)
            throw new BaseException("Mali yıl 2000-2100 aralığında olmalıdır.", 400);

        if (filter.Donem.HasValue && (filter.Donem.Value < 1 || filter.Donem.Value > 12))
            throw new BaseException("Dönem numarası 1-12 aralığında olmalıdır.", 400);

        // 3. Temel filtre sorgusu (AsNoTracking; Include kullanılmaz — aggregate DB tarafında yapılır)
        var query = _dbContext.MuhasebeHesapBakiyeleri
            .AsNoTracking()
            .Where(x => !x.IsDeleted
                && x.TesisId == filter.TesisId
                && x.MaliYil == filter.MaliYil.Value);

        if (filter.Donem.HasValue)
            query = query.Where(x => x.Donem == filter.Donem.Value);

        if (!filter.AltHesaplariDahilEt)
            query = query.Where(x => !x.KonsolideMi);

        if (filter.SadeceHareketGorenHesaplar)
            query = query.Where(x => x.BorcToplam != 0 || x.AlacakToplam != 0);

        if (!string.IsNullOrWhiteSpace(filter.HesapKoduBaslangic))
        {
            var baslangic = filter.HesapKoduBaslangic.Trim();
            query = query.Where(x => string.Compare(x.HesapKodu, baslangic) >= 0);
        }

        if (!string.IsNullOrWhiteSpace(filter.HesapKoduBitis))
        {
            var bitis = filter.HesapKoduBitis.Trim();
            query = query.Where(x => string.Compare(x.HesapKodu, bitis) <= 0);
        }

        // 4. Genel toplamları DB tarafında hesapla (sadece KonsolideMi=false gerçek kayıtlar)
        var genelQuery = query.Where(x => !x.KonsolideMi);

        var genelToplam = await genelQuery
            .GroupBy(x => 1)
            .Select(g => new
            {
                GenelToplamBorc = g.Sum(x => x.BorcToplam),
                GenelToplamAlacak = g.Sum(x => x.AlacakToplam),
                GenelBorcBakiye = g.Sum(x => x.BorcBakiye),
                GenelAlacakBakiye = g.Sum(x => x.AlacakBakiye)
            })
            .FirstOrDefaultAsync(cancellationToken);

        // 5. Display satırlarını DB tarafında HesapKodu bazında grupla
        var aggregateQuery = query
            .GroupBy(x => x.HesapKodu)
            .Select(g => new
            {
                HesapKodu = g.Key,
                ToplamBorc = g.Sum(x => x.BorcToplam),
                ToplamAlacak = g.Sum(x => x.AlacakToplam),
                NetBakiye = g.Sum(x => x.NetBakiye),
                BorcBakiye = g.Sum(x => x.BorcBakiye),
                AlacakBakiye = g.Sum(x => x.AlacakBakiye),
                KonsolideSatirMi = g.Any(x => x.KonsolideMi),
                Seviye = g.Min(x => x.HesapSeviyesi),
                IlkHesapPlaniId = g.Min(x => x.MuhasebeHesapPlaniId),
                HesapAdi = g.Min(x => x.HesapAdi)
            });

        // 6. Sıralama ve sayfalama DB tarafında
        var aggregatePage = await aggregateQuery
            .OrderBy(x => x.HesapKodu)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        // 7. Boş sonuç
        if (aggregatePage.Count == 0)
        {
            return new MizanDto
            {
                TesisId = filter.TesisId,
                GenelToplamBorc = genelToplam?.GenelToplamBorc ?? 0,
                GenelToplamAlacak = genelToplam?.GenelToplamAlacak ?? 0,
                GenelBorcBakiye = genelToplam?.GenelBorcBakiye ?? 0,
                GenelAlacakBakiye = genelToplam?.GenelAlacakBakiye ?? 0,
                Satirlar = []
            };
        }

        // 8. Sayfadaki hesap planı ID'leri için ayrı sorgu
        var hesapPlaniIds = aggregatePage
            .Select(x => x.IlkHesapPlaniId)
            .Distinct()
            .ToList();

        var hesapPlanlari = await _dbContext.MuhasebeHesapPlanlari
            .Where(x => hesapPlaniIds.Contains(x.Id))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var hesapPlaniLookup = hesapPlanlari.ToDictionary(x => x.Id);

        // 9. DTO'ları oluştur
        var satirlar = aggregatePage.Select(agg =>
        {
            var net = agg.NetBakiye;
            hesapPlaniLookup.TryGetValue(agg.IlkHesapPlaniId, out var hp);

            return new MizanSatirDto
            {
                MuhasebeHesapPlaniId = hp?.Id ?? agg.IlkHesapPlaniId,
                HesapKodu = agg.HesapKodu,
                HesapAdi = hp?.Ad ?? agg.HesapAdi ?? string.Empty,
                DetayHesapMi = hp?.DetayHesapMi ?? false,
                HareketGorebilirMi = hp?.HareketGorebilirMi ?? true,
                ToplamBorc = agg.ToplamBorc,
                ToplamAlacak = agg.ToplamAlacak,
                BorcBakiye = net > 0 ? net : 0,
                AlacakBakiye = net < 0 ? Math.Abs(net) : 0,
                Bakiye = Math.Abs(net),
                BakiyeTipi = net > 0 ? "Borc" : net < 0 ? "Alacak" : "Sifir",
                KonsolideSatirMi = agg.KonsolideSatirMi,
                Seviye = agg.Seviye,
            };
        }).ToList();

        // 10. DTO'yu döndür
        return new MizanDto
        {
            TesisId = filter.TesisId,
            GenelToplamBorc = genelToplam?.GenelToplamBorc ?? 0,
            GenelToplamAlacak = genelToplam?.GenelToplamAlacak ?? 0,
            GenelBorcBakiye = genelToplam?.GenelBorcBakiye ?? 0,
            GenelAlacakBakiye = genelToplam?.GenelAlacakBakiye ?? 0,
            Satirlar = satirlar,
        };
    }

    public async Task<byte[]> ExportMizanBakiyeExcelAsync(MizanFilterDto filter, CancellationToken cancellationToken = default)
    {
        // 1. Normalize + validasyon (GetMizanBakiyeAsync ile aynı)
        filter.Normalize();

        if (filter.TesisId <= 0)
            throw new BaseException("Geçerli bir tesis seçilmelidir.", 400);

        if (filter.BaslangicTarihi.HasValue || filter.BitisTarihi.HasValue)
            throw new BaseException("Bakiye tablosundan mizan için tarih aralığı yerine MaliYil/Donem filtresi kullanılmalıdır.", 400);

        if (!filter.MaliYil.HasValue)
            throw new BaseException("Bakiye tablosundan mizan için mali yıl seçilmelidir.", 400);

        if (filter.MaliYil.Value < 2000 || filter.MaliYil.Value > 2100)
            throw new BaseException("Mali yıl 2000-2100 aralığında olmalıdır.", 400);

        if (filter.Donem.HasValue && (filter.Donem.Value < 1 || filter.Donem.Value > 12))
            throw new BaseException("Dönem numarası 1-12 aralığında olmalıdır.", 400);

        // 2. Temel filtre sorgusu (GetMizanBakiyeAsync ile aynı)
        var query = _dbContext.MuhasebeHesapBakiyeleri
            .AsNoTracking()
            .Where(x => !x.IsDeleted
                && x.TesisId == filter.TesisId
                && x.MaliYil == filter.MaliYil.Value);

        if (filter.Donem.HasValue)
            query = query.Where(x => x.Donem == filter.Donem.Value);

        if (!filter.AltHesaplariDahilEt)
            query = query.Where(x => !x.KonsolideMi);

        if (filter.SadeceHareketGorenHesaplar)
            query = query.Where(x => x.BorcToplam != 0 || x.AlacakToplam != 0);

        if (!string.IsNullOrWhiteSpace(filter.HesapKoduBaslangic))
        {
            var baslangic = filter.HesapKoduBaslangic.Trim();
            query = query.Where(x => string.Compare(x.HesapKodu, baslangic) >= 0);
        }

        if (!string.IsNullOrWhiteSpace(filter.HesapKoduBitis))
        {
            var bitis = filter.HesapKoduBitis.Trim();
            query = query.Where(x => string.Compare(x.HesapKodu, bitis) <= 0);
        }

        // 3. Genel toplamlar (sadece KonsolideMi=false)
        var genelQuery = query.Where(x => !x.KonsolideMi);

        var genelToplam = await genelQuery
            .GroupBy(x => 1)
            .Select(g => new
            {
                GenelToplamBorc = g.Sum(x => x.BorcToplam),
                GenelToplamAlacak = g.Sum(x => x.AlacakToplam),
                GenelBorcBakiye = g.Sum(x => x.BorcBakiye),
                GenelAlacakBakiye = g.Sum(x => x.AlacakBakiye)
            })
            .FirstOrDefaultAsync(cancellationToken);

        // 4. Tüm satırları sayfalamadan al
        var allRows = await query
            .GroupBy(x => x.HesapKodu)
            .Select(g => new
            {
                HesapKodu = g.Key,
                ToplamBorc = g.Sum(x => x.BorcToplam),
                ToplamAlacak = g.Sum(x => x.AlacakToplam),
                NetBakiye = g.Sum(x => x.NetBakiye),
                BorcBakiye = g.Sum(x => x.BorcBakiye),
                AlacakBakiye = g.Sum(x => x.AlacakBakiye),
                KonsolideSatirMi = g.Any(x => x.KonsolideMi),
                Seviye = g.Min(x => x.HesapSeviyesi),
                IlkHesapPlaniId = g.Min(x => x.MuhasebeHesapPlaniId),
                HesapAdi = g.Min(x => x.HesapAdi)
            })
            .OrderBy(x => x.HesapKodu)
            .ToListAsync(cancellationToken);

        // 5. Hesap planı lookup
        var hesapPlaniIds = allRows
            .Select(x => x.IlkHesapPlaniId)
            .Distinct()
            .ToList();

        var hesapPlanlari = await _dbContext.MuhasebeHesapPlanlari
            .Where(x => hesapPlaniIds.Contains(x.Id))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var hesapPlaniLookup = hesapPlanlari.ToDictionary(x => x.Id);

        // 6. Excel oluştur
        // İleride çok büyük veri için streaming export değerlendirilebilir.
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Hızlı Mizan");

        // Başlık ve üst bilgi satırları
        var now = DateTime.Now;
        var baslangicKodu = !string.IsNullOrWhiteSpace(filter.HesapKoduBaslangic) ? filter.HesapKoduBaslangic.Trim() : null;
        var bitisKodu = !string.IsNullOrWhiteSpace(filter.HesapKoduBitis) ? filter.HesapKoduBitis.Trim() : null;

        ws.Cell(1, 1).Value = "Hızlı Mizan Raporu";
        ws.Cell(1, 1).Style.Font.Bold = true;

        ws.Cell(2, 1).Value = "Export Tarihi:";
        ws.Cell(2, 2).Value = now.ToString("dd.MM.yyyy HH:mm");

        ws.Cell(3, 1).Value = "Tesis Id:";
        ws.Cell(3, 2).Value = filter.TesisId;

        ws.Cell(4, 1).Value = "Mali Yıl:";
        ws.Cell(4, 2).Value = filter.MaliYil;

        ws.Cell(5, 1).Value = "Dönem:";
        ws.Cell(5, 2).Value = filter.Donem.HasValue ? filter.Donem.Value.ToString() : "Tümü";

        ws.Cell(6, 1).Value = "Hesap Kodu Aralığı:";
        ws.Cell(6, 2).Value = $"{(baslangicKodu ?? "-")} / {(bitisKodu ?? "-")}";

        // Kolon başlıkları (satır 8)
        var headers = new[] { "Hesap Kodu", "Hesap Adı", "Seviye", "Borç Toplamı", "Alacak Toplamı", "Borç Bakiye", "Alacak Bakiye", "Net Bakiye", "Bakiye Tipi" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(8, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
        }

        // Freeze pane: başlık satırlarını dondur
        ws.SheetView.FreezeRows(8);

        // Veri satırları (satır 9'dan başlar)
        int row = 9;

        foreach (var agg in allRows)
        {
            var net = agg.NetBakiye;
            hesapPlaniLookup.TryGetValue(agg.IlkHesapPlaniId, out var hp);
            var ad = hp?.Ad ?? agg.HesapAdi ?? string.Empty;

            ws.Cell(row, 1).Value = agg.HesapKodu;
            ws.Cell(row, 2).Value = ad;
            ws.Cell(row, 3).Value = agg.Seviye;
            ws.Cell(row, 4).Value = agg.ToplamBorc;
            ws.Cell(row, 5).Value = agg.ToplamAlacak;
            ws.Cell(row, 6).Value = net > 0 ? net : 0;
            ws.Cell(row, 7).Value = net < 0 ? Math.Abs(net) : 0;
            ws.Cell(row, 8).Value = Math.Abs(net);
            ws.Cell(row, 9).Value = net > 0 ? "Borç" : net < 0 ? "Alacak" : "Sıfır";

            row++;
        }

        // Sayısal sütunlar için format
        var formatCols = new[] { 4, 5, 6, 7, 8 };
        foreach (var col in formatCols)
        {
            ws.Column(col).Style.NumberFormat.Format = "#,##0.00";
        }

        // Toplam satırı (genelToplam değerlerini kullan; AltHesaplariDahilEt=true olduğunda konsolide satırlar toplamı şişirmez)
        var gtBorc = genelToplam?.GenelToplamBorc ?? 0;
        var gtAlacak = genelToplam?.GenelToplamAlacak ?? 0;
        var gtBorcBakiye = genelToplam?.GenelBorcBakiye ?? 0;
        var gtAlacakBakiye = genelToplam?.GenelAlacakBakiye ?? 0;
        var gtNetBakiye = Math.Abs(gtBorcBakiye - gtAlacakBakiye);

        ws.Cell(row, 1).Value = "TOPLAM";
        ws.Cell(row, 4).Value = gtBorc;
        ws.Cell(row, 5).Value = gtAlacak;
        ws.Cell(row, 6).Value = gtBorcBakiye;
        ws.Cell(row, 7).Value = gtAlacakBakiye;
        ws.Cell(row, 8).Value = gtNetBakiye;

        for (int i = 1; i <= 9; i++)
        {
            ws.Cell(row, i).Style.Font.Bold = true;
        }

        // Otomatik sütun genişliği
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// MizanSatirDto bakiyelerini ToplamBorc ve ToplamAlacak'a göre yeniden hesaplar.
    /// </summary>
    private static void RecalculateMizanSatirBakiye(MizanSatirDto satir)
    {
        var net = satir.ToplamBorc - satir.ToplamAlacak;
        satir.BorcBakiye = net > 0 ? net : 0;
        satir.AlacakBakiye = net < 0 ? Math.Abs(net) : 0;
        satir.Bakiye = Math.Abs(net);
        satir.BakiyeTipi = net > 0 ? "Borc" : net < 0 ? "Alacak" : "Sifir";
    }

    /// <summary>
    /// Nokta ile ayrılmış tam hesap kodundan üst hesap kodlarını türetir.
    /// Örn: "150.01.001" → ["150", "150.01"]
    /// Örn: "150" → []
    /// </summary>
    private static List<string> GetUstHesapKodlari(string tamKod)
    {
        var result = new List<string>();
        var segments = tamKod.Split('.');

        for (int i = 0; i < segments.Length - 1; i++)
        {
            result.Add(string.Join(".", segments.Take(i + 1)));
        }

        return result;
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
        entity.Satirlar = Mapper.Map<List<MuhasebeFisSatir>>(dto.Satirlar);

        // Manuel FisNo girilmişse trim + duplicate kontrolü
        if (!string.IsNullOrWhiteSpace(dto.FisNo))
        {
            var manuelFisNo = dto.FisNo.Trim();
            var duplicate = await _dbContext.MuhasebeFisler
                .AnyAsync(x => x.TesisId == dto.TesisId
                    && x.FisNo == manuelFisNo
                    && !x.IsDeleted);
            if (duplicate)
                throw new BaseException(
                    $"Aynı tesis içinde bu fiş numarası zaten kullanılıyor: {manuelFisNo}", 400);

            entity.FisNo = manuelFisNo;
        }

        // Otomatik fiş no üretimi (retry ile)
        const int maxRetry = 3;
        for (int attempt = 0; attempt < maxRetry; attempt++)
        {
            if (string.IsNullOrWhiteSpace(dto.FisNo))
            {
                entity.FisNo = await GenerateFisNoAsync(
                    entity.TesisId,
                    entity.MaliYil,
                    entity.FisTipi,
                    entity.KaynakModul,
                    CancellationToken.None);
            }

            try
            {
                await _dbContext.MuhasebeFisler.AddAsync(entity);
                await _dbContext.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException ex) when (IsUniqueConflict(ex))
            {
                // Manuel fiş no ise retry yapma, direkt fırlat
                if (!string.IsNullOrWhiteSpace(dto.FisNo) || attempt == maxRetry - 1)
                    throw;

                // Otomatik numara çakıştıysa tekrar dene
                _dbContext.Entry(entity).State = EntityState.Detached;
                entity.Id = 0;
            }
        }

        var created = await _repository.GetByIdWithSatirlarAsync(entity.Id)
            ?? throw new BaseException("Fiş oluşturulamadı.", 500);
        return Mapper.Map<MuhasebeFisDto>(created);
    }

    public override async Task<MuhasebeFisDto> UpdateAsync(MuhasebeFisDto dto)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();

        try
        {
            // 1. Mevcut fişi satırlarıyla getir
            var existing = await _dbContext.MuhasebeFisler
                .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
                .FirstOrDefaultAsync(x => x.Id == dto.Id && !x.IsDeleted);

            if (existing is null)
                throw new BaseException("Fiş bulunamadı.", 404);

            // 2. Sadece Taslak fişler güncellenebilir
            if (existing.Durum != MuhasebeFisDurumlari.Taslak)
                throw new BaseException("Yalnızca taslak durumundaki fişler güncellenebilir.", 400);

            // 3. Normalize ve validate (dönem, satır sayısı, hesap planı, borç/alacak dengesi)
            //    NormalizeAndValidateCreateAsync ayrıca dto.ToplamBorc ve dto.ToplamAlacak'ı
            //    satırlardan yeniden hesaplar
            await NormalizeAndValidateCreateAsync(dto, CancellationToken.None);

            // 4. Toplamları backend'de satırlardan hesapla (güvenlik: frontend toplamlarına güvenme)
            var toplamBorc = dto.Satirlar.Sum(x => x.Borc);
            var toplamAlacak = dto.Satirlar.Sum(x => x.Alacak);
            if (toplamBorc != toplamAlacak)
                throw new BaseException($"Toplam borç ({toplamBorc:N2}) ile toplam alacak ({toplamAlacak:N2}) eşit olmalıdır.", 400);

            // 5. Başlık alanlarını güncelle
            existing.TesisId = dto.TesisId;
            existing.MaliYil = dto.MaliYil;
            existing.Donem = dto.Donem;
            existing.FisTarihi = dto.FisTarihi;
            existing.FisTipi = dto.FisTipi;
            existing.Aciklama = dto.Aciklama;
            existing.ToplamBorc = toplamBorc;
            existing.ToplamAlacak = toplamAlacak;

            // 6. Eski satırları soft-delete (RemoveRange → ApplyAuditInfo soft-delete'e çevirir, DeletedBy set edilir)
            // NOT: existing.Satirlar.Clear() kullanılmaz — cascade davranışı tetiklenmemeli
            var aktifEskiSatirlar = existing.Satirlar.Where(s => !s.IsDeleted).ToList();
            _dbContext.MuhasebeFisSatirlari.RemoveRange(aktifEskiSatirlar);

            // 7. Yeni satırları oluştur — linked alanları koru, DbSet üzerinden ekle
            var yeniSatirlar = new List<MuhasebeFisSatir>();
            foreach (var satirDto in dto.Satirlar)
            {
                var satir = Mapper.Map<MuhasebeFisSatir>(satirDto);
                satir.MuhasebeFisId = existing.Id;

                // Linked alanları AutoMapper sonrası açıkça koru
                satir.ParaBirimi = string.IsNullOrWhiteSpace(satirDto.ParaBirimi) ? "TRY" : satirDto.ParaBirimi;
                satir.Kur = satirDto.Kur <= 0 ? 1 : satirDto.Kur;
                satir.CariKartId = satirDto.CariKartId;
                satir.TasinirKartId = satirDto.TasinirKartId;
                satir.DepoId = satirDto.DepoId;
                satir.KasaBankaHesapId = satirDto.KasaBankaHesapId;

                yeniSatirlar.Add(satir);
            }

            await _dbContext.MuhasebeFisSatirlari.AddRangeAsync(yeniSatirlar);

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            // 8. Reload ve dön
            var reloaded = await _repository.GetByIdWithSatirlarAsync(existing.Id)
                ?? throw new BaseException("Güncellenen fiş okunamadı.", 500);
            return Mapper.Map<MuhasebeFisDto>(reloaded);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public override async Task DeleteAsync(int id)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();

        try
        {
            // 1. Fişi satırlarıyla çek
            var fis = await _dbContext.MuhasebeFisler
                .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

            if (fis is null)
                throw new BaseException("Fiş bulunamadı.", 404);

            // 2. Sadece Taslak fiş silinebilir
            if (fis.Durum != MuhasebeFisDurumlari.Taslak)
                throw new BaseException("Sadece taslak fişler silinebilir. Onaylı fişler iptal/ters kayıt ile kapatılmalıdır.", 400);

            // 3. YevmiyeNo almış fiş silinemez (ek güvenlik)
            if (fis.YevmiyeNo.HasValue)
                throw new BaseException("Yevmiye numarası almış fiş silinemez.", 400);

            // 3b. Açık dönem kontrolü
            await ValidateOpenPeriodAsync(fis.TesisId, fis.FisTarihi, fis.MaliYil, fis.Donem, CancellationToken.None);

            // 4. Aktif satırları soft-delete (RemoveRange → ApplyAuditInfo: EntityState.Deleted → Modified + IsDeleted/DeletedAt/DeletedBy)
            var aktifSatirlar = fis.Satirlar.Where(s => !s.IsDeleted).ToList();
            if (aktifSatirlar.Count > 0)
                _dbContext.MuhasebeFisSatirlari.RemoveRange(aktifSatirlar);

            // 5. Fişi soft-delete (Remove → ApplyAuditInfo)
            _dbContext.MuhasebeFisler.Remove(fis);

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static string GetFisTipiKodu(string fisTipi, string? kaynakModul)
    {
        if (kaynakModul == MuhasebeKaynakModulleri.TasinirHareket)
            return "TSN";

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
    /// Fişin dönemine ait açık muhasebe dönemi olup olmadığını kontrol eder.
    /// Tüm fiş yazma işlemlerinde (create/update/delete/onayla/iptal) ortak kullanılır.
    /// </summary>
    private async Task ValidateOpenPeriodAsync(
        int tesisId,
        DateTime fisTarihi,
        int maliYil,
        int donemNo,
        CancellationToken cancellationToken)
    {
        var donem = await _muhasebeDonemService.GetAktifDonemAsync(
            tesisId,
            fisTarihi,
            cancellationToken);

        if (donem is null)
            throw new BaseException("Fiş tarihi için açık muhasebe dönemi bulunamadı.", 400);

        if (maliYil != donem.MaliYil || donemNo != donem.DonemNo)
            throw new BaseException("Fişin mali yılı/dönemi, açık muhasebe dönemi ile uyumlu değildir.", 400);
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
        await ValidateOpenPeriodAsync(dto.TesisId, dto.FisTarihi, dto.MaliYil, dto.Donem, cancellationToken);

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

    /// <summary>
    /// Bu endpoint doğrulama/denetim amaçlıdır. Büyük veri hacminde tüm mizan satırlarını
    /// karşılaştırdığı için operasyonel rapor ekranlarında kullanılmamalıdır.
    /// </summary>
    public async Task<MizanKarsilastirmaDto> KarsilastirMizanAsync(
        MizanFilterDto filter, CancellationToken cancellationToken = default)
    {
        // 1. Normalize
        filter.Normalize();

        // 2. Validasyon
        if (filter.TesisId <= 0)
            throw new BaseException("Geçerli bir tesis seçilmelidir.", 400);

        if (filter.BaslangicTarihi.HasValue || filter.BitisTarihi.HasValue)
            throw new BaseException(
                "Bakiye tablosu ile mizan karşılaştırması için tarih aralığı yerine MaliYil/Donem filtresi kullanılmalıdır.", 400);

        if (!filter.MaliYil.HasValue)
            throw new BaseException(
                "Bakiye tablosu ile mizan karşılaştırması için mali yıl seçilmelidir.", 400);

        // 3. Sayfalama devre dışı — tüm satırlar karşılaştırılacak
        var compareFilter = new MizanFilterDto
        {
            TesisId = filter.TesisId,
            MaliYil = filter.MaliYil,
            Donem = filter.Donem,
            HesapKoduBaslangic = filter.HesapKoduBaslangic,
            HesapKoduBitis = filter.HesapKoduBitis,
            SadeceHareketGorenHesaplar = filter.SadeceHareketGorenHesaplar,
            AltHesaplariDahilEt = filter.AltHesaplariDahilEt,
            Page = 1,
            PageSize = 100000
        };

        // 4. Eski mizan (MuhasebeFisSatir tabanlı)
        var eskiMizan = await GetMizanAsync(compareFilter, cancellationToken);

        // 5. Hızlı mizan (MuhasebeHesapBakiye tabanlı)
        var hizliMizan = await GetMizanBakiyeAsync(compareFilter, cancellationToken);

        // 6. Satırları HesapKodu bazında dictionary yap
        var eskiDict = eskiMizan.Satirlar
            .GroupBy(x => x.HesapKodu)
            .ToDictionary(g => g.Key, g => AggregateMizanSatiri(g.ToList()));

        var hizliDict = hizliMizan.Satirlar
            .GroupBy(x => x.HesapKodu)
            .ToDictionary(g => g.Key, g => AggregateMizanSatiri(g.ToList()));

        // 7. Tüm hesap kodlarını birleştir
        var tumHesapKodlari = eskiDict.Keys
            .Union(hizliDict.Keys)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        // 8. Tolerans
        const decimal tolerance = 0.01m;

        // 9. Her hesap kodu için fark kontrolü
        var farklar = new List<MizanKarsilastirmaSatirDto>();

        foreach (var hesapKodu in tumHesapKodlari)
        {
            var eskiVar = eskiDict.TryGetValue(hesapKodu, out var eskiSatir);
            var hizliVar = hizliDict.TryGetValue(hesapKodu, out var hizliSatir);

            var karsilastirmaSatir = new MizanKarsilastirmaSatirDto
            {
                HesapKodu = hesapKodu,
                HesapAdi = (hizliSatir ?? eskiSatir)!.HesapAdi,
                EskiMizandaVarMi = eskiVar,
                HizliMizandaVarMi = hizliVar
            };

            if (eskiVar)
            {
                karsilastirmaSatir.EskiToplamBorc = eskiSatir!.ToplamBorc;
                karsilastirmaSatir.EskiToplamAlacak = eskiSatir.ToplamAlacak;
                karsilastirmaSatir.EskiBorcBakiye = eskiSatir.BorcBakiye;
                karsilastirmaSatir.EskiAlacakBakiye = eskiSatir.AlacakBakiye;
            }

            if (hizliVar)
            {
                karsilastirmaSatir.HizliToplamBorc = hizliSatir!.ToplamBorc;
                karsilastirmaSatir.HizliToplamAlacak = hizliSatir.ToplamAlacak;
                karsilastirmaSatir.HizliBorcBakiye = hizliSatir.BorcBakiye;
                karsilastirmaSatir.HizliAlacakBakiye = hizliSatir.AlacakBakiye;
            }

            if (eskiVar && hizliVar)
            {
                karsilastirmaSatir.ToplamBorcFark = hizliSatir!.ToplamBorc - eskiSatir!.ToplamBorc;
                karsilastirmaSatir.ToplamAlacakFark = hizliSatir.ToplamAlacak - eskiSatir.ToplamAlacak;
                karsilastirmaSatir.BorcBakiyeFark = hizliSatir.BorcBakiye - eskiSatir.BorcBakiye;
                karsilastirmaSatir.AlacakBakiyeFark = hizliSatir.AlacakBakiye - eskiSatir.AlacakBakiye;
            }

            // FarkTipi belirleme
            if (!eskiVar && hizliVar)
            {
                karsilastirmaSatir.FarkTipi = "SadeceHizliMizandaVar";
            }
            else if (eskiVar && !hizliVar)
            {
                karsilastirmaSatir.FarkTipi = "SadeceEskiMizandaVar";
            }
            else if (eskiVar && hizliVar)
            {
                var hasTutarFark =
                    Math.Abs(karsilastirmaSatir.ToplamBorcFark) > tolerance ||
                    Math.Abs(karsilastirmaSatir.ToplamAlacakFark) > tolerance ||
                    Math.Abs(karsilastirmaSatir.BorcBakiyeFark) > tolerance ||
                    Math.Abs(karsilastirmaSatir.AlacakBakiyeFark) > tolerance;

                var hasAdFark = !string.Equals(
                    eskiSatir!.HesapAdi, hizliSatir!.HesapAdi, StringComparison.Ordinal);

                if (hasTutarFark)
                    karsilastirmaSatir.FarkTipi = "TutarFarki";
                else if (hasAdFark)
                    karsilastirmaSatir.FarkTipi = "HesapAdiFarki";
            }

            // Sadece fark varsa listeye ekle
            if (!string.IsNullOrEmpty(karsilastirmaSatir.FarkTipi))
                farklar.Add(karsilastirmaSatir);
        }

        // 10. Genel toplam farklarını hesapla
        var genelBorçFark = hizliMizan.GenelToplamBorc - eskiMizan.GenelToplamBorc;
        var genelAlacakFark = hizliMizan.GenelToplamAlacak - eskiMizan.GenelToplamAlacak;
        var genelBorçBakiyeFark = hizliMizan.GenelBorcBakiye - eskiMizan.GenelBorcBakiye;
        var genelAlacakBakiyeFark = hizliMizan.GenelAlacakBakiye - eskiMizan.GenelAlacakBakiye;

        var eslesiyorMu = farklar.Count == 0
            && Math.Abs(genelBorçFark) <= tolerance
            && Math.Abs(genelAlacakFark) <= tolerance
            && Math.Abs(genelBorçBakiyeFark) <= tolerance
            && Math.Abs(genelAlacakBakiyeFark) <= tolerance;

        return new MizanKarsilastirmaDto
        {
            TesisId = filter.TesisId,
            MaliYil = filter.MaliYil,
            Donem = filter.Donem,

            EskiGenelToplamBorc = eskiMizan.GenelToplamBorc,
            HizliGenelToplamBorc = hizliMizan.GenelToplamBorc,
            GenelToplamBorcFark = genelBorçFark,

            EskiGenelToplamAlacak = eskiMizan.GenelToplamAlacak,
            HizliGenelToplamAlacak = hizliMizan.GenelToplamAlacak,
            GenelToplamAlacakFark = genelAlacakFark,

            EskiGenelBorcBakiye = eskiMizan.GenelBorcBakiye,
            HizliGenelBorcBakiye = hizliMizan.GenelBorcBakiye,
            GenelBorcBakiyeFark = genelBorçBakiyeFark,

            EskiGenelAlacakBakiye = eskiMizan.GenelAlacakBakiye,
            HizliGenelAlacakBakiye = hizliMizan.GenelAlacakBakiye,
            GenelAlacakBakiyeFark = genelAlacakBakiyeFark,

            EskiSatirSayisi = eskiMizan.Satirlar.Count,
            HizliSatirSayisi = hizliMizan.Satirlar.Count,
            FarkliSatirSayisi = farklar.Count,

            EslesiyorMu = eslesiyorMu,
            Farklar = farklar
        };
    }

    /// <summary>
    /// Aynı hesap kodu birden fazla satırda gelirse birleştir.
    /// </summary>
    private static MizanSatirDto AggregateMizanSatiri(List<MizanSatirDto> satirlar)
    {
        var ilk = satirlar.First();
        var toplamBorc = satirlar.Sum(x => x.ToplamBorc);
        var toplamAlacak = satirlar.Sum(x => x.ToplamAlacak);
        var net = toplamBorc - toplamAlacak;

        return new MizanSatirDto
        {
            MuhasebeHesapPlaniId = ilk.MuhasebeHesapPlaniId,
            HesapKodu = ilk.HesapKodu,
            HesapAdi = ilk.HesapAdi,
            DetayHesapMi = ilk.DetayHesapMi,
            HareketGorebilirMi = ilk.HareketGorebilirMi,
            ToplamBorc = toplamBorc,
            ToplamAlacak = toplamAlacak,
            BorcBakiye = net > 0 ? net : 0,
            AlacakBakiye = net < 0 ? Math.Abs(net) : 0,
            Bakiye = Math.Abs(net),
            BakiyeTipi = net > 0 ? "Borc" : net < 0 ? "Alacak" : "Sifir",
            KonsolideSatirMi = satirlar.Any(x => x.KonsolideSatirMi),
            Seviye = ilk.Seviye
        };
    }

    public async Task<TasinirMuhasebeFisiOlusturResultDto> TasinirMuhasebeFisiTaslagiOlusturAsync(
        TasinirMuhasebeFisiOlusturRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Validasyonlar
        if (request.TesisId <= 0)
            throw new BaseException("Geçerli bir tesis seçilmelidir.", 400);

        if (request.MaliYil < 2000 || request.MaliYil > 2100)
            throw new BaseException("Mali yıl 2000-2100 aralığında olmalıdır.", 400);

        if (request.Donem.HasValue && (request.Donem.Value < 1 || request.Donem.Value > 12))
            throw new BaseException("Dönem numarası 1-12 aralığında olmalıdır.", 400);

        if (request.FisTarihi == default)
            throw new BaseException("Fiş tarihi zorunludur.", 400);

        var tasinirKodu = request.TasinirKodu?.Trim();
        if (string.IsNullOrWhiteSpace(tasinirKodu))
            throw new BaseException("Taşınır kodu zorunludur.", 400);

        if (request.Tutar <= 0)
            throw new BaseException("Tutar sıfırdan büyük olmalıdır.", 400);

        var alacakHesapKodu = string.IsNullOrWhiteSpace(request.AlacakHesapKodu)
            ? "320"
            : request.AlacakHesapKodu.Trim();

        // 1b. KDV validasyonları
        string? kdvHesapKodu = null;
        if (request.KdvOrani.HasValue)
        {
            if (request.KdvOrani.Value <= 0 || request.KdvOrani.Value > 100)
                throw new BaseException("KDV oranı 0 ile 100 arasında olmalıdır.", 400);

            if (string.IsNullOrWhiteSpace(request.KdvHesapKodu))
                throw new BaseException("KDV hesap kodu zorunludur.", 400);

            kdvHesapKodu = request.KdvHesapKodu.Trim();
        }

        // 2. Donem çözümle
        int donem;
        if (request.Donem.HasValue)
        {
            donem = request.Donem.Value;
        }
        else
        {
            var aktifDonem = await _muhasebeDonemService.GetAktifDonemAsync(
                request.TesisId, request.FisTarihi, cancellationToken)
                ?? throw new BaseException("Fiş tarihi için açık muhasebe dönemi bulunamadı.", 400);

            if (request.MaliYil != aktifDonem.MaliYil)
                throw new BaseException("Fişin mali yılı, açık muhasebe dönemi ile uyumlu değildir.", 400);

            donem = aktifDonem.DonemNo;
        }

        // 3. Taşınır kodunu bul
        var tasinirKodEntity = await _dbContext.TasinirKodlar
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Kod == tasinirKodu && !x.IsDeleted && x.AktifMi, cancellationToken)
            ?? throw new BaseException(
                $"Taşınır kodu bulunamadı veya aktif değil. Kod: {tasinirKodu}", 400);

        // 4. Taşınır kodu muhasebe hesap eşlemesini bul
        // NOT: TasinirKodMuhasebeHesapEsleme entity'sinde TesisId bulunmadığından tesis/global önceliği uygulanmadı.
        var esleme = await _dbContext.TasinirKodMuhasebeHesapEslemeleri
            .Include(x => x.MuhasebeHesapPlani)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TasinirKodId == tasinirKodEntity.Id
                && !x.IsDeleted
                && x.AktifMi, cancellationToken)
            ?? throw new BaseException(
                $"Taşınır kodu için muhasebe hesap eşlemesi bulunamadı. Taşınır kodu: {tasinirKodu}", 400);

        var borcHesap = esleme.MuhasebeHesapPlani;
        if (borcHesap is null)
            throw new BaseException(
                $"Taşınır kodu için muhasebe hesap eşlemesi bulunamadı. Taşınır kodu: {tasinirKodu}", 400);

        // 5. Borç hesabı kontrolü: hareket görebilir, detay hesap olmalı ve tesis uyumu
        var borcHesapKontrol = await _dbContext.MuhasebeHesapPlanlari
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == borcHesap.Id
                && !x.IsDeleted
                && x.AktifMi
                && x.HareketGorebilirMi
                && x.DetayHesapMi
                && (x.TesisId == request.TesisId || x.TesisId == null), cancellationToken)
            ?? throw new BaseException(
                $"Borç hesabı hesap planında bulunamadı veya hareket görebilir değil. Hesap kodu: {borcHesap.TamKod}", 400);

        // 6. Alacak hesabı kontrolü: hareket görebilir, detay hesap olmalı ve tesis uyumu
        // Aynı TamKod farklı tesislerde varsa önce tesis özel, sonra global hesap seçilir.
        var alacakHesap = await _dbContext.MuhasebeHesapPlanlari
            .AsNoTracking()
            .Where(x => x.TamKod == alacakHesapKodu
                && !x.IsDeleted
                && x.AktifMi
                && x.HareketGorebilirMi
                && x.DetayHesapMi
                && (x.TesisId == request.TesisId || x.TesisId == null))
            .OrderByDescending(x => x.TesisId == request.TesisId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BaseException(
                $"Alacak hesabı hesap planında bulunamadı veya hareket görebilir değil. Hesap kodu: {alacakHesapKodu}", 400);

        // 6b. KDV hesabı kontrolü
        MuhasebeHesapPlani? kdvHesap = null;
        if (kdvHesapKodu is not null)
        {
            kdvHesap = await _dbContext.MuhasebeHesapPlanlari
                .AsNoTracking()
                .Where(x => x.TamKod == kdvHesapKodu
                    && !x.IsDeleted
                    && x.AktifMi
                    && x.HareketGorebilirMi
                    && x.DetayHesapMi
                    && (x.TesisId == request.TesisId || x.TesisId == null))
                .OrderByDescending(x => x.TesisId == request.TesisId)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new BaseException(
                    $"KDV hesabı hesap planında bulunamadı veya hareket görebilir değil. Hesap kodu: {kdvHesapKodu}", 400);
        }

        // 7. KDV hesaplama
        decimal matrah, kdvTutari, genelToplam;
        if (request.KdvOrani.HasValue)
        {
            if (request.KdvDahilMi)
            {
                // KDV dahil: Tutar = genelToplam
                genelToplam = request.Tutar;
                matrah = Math.Round(genelToplam / (1 + request.KdvOrani.Value / 100), 2);
                kdvTutari = genelToplam - matrah;
            }
            else
            {
                // KDV hariç: Tutar = matrah
                matrah = request.Tutar;
                kdvTutari = Math.Round(matrah * request.KdvOrani.Value / 100, 2);
                genelToplam = matrah + kdvTutari;
            }
        }
        else
        {
            matrah = request.Tutar;
            kdvTutari = 0;
            genelToplam = request.Tutar;
        }

        // 8. Kaynak modül ve ID'yi request'ten çıkar
        var kaynakModul = !string.IsNullOrWhiteSpace(request.ReferansTipi)
            ? request.ReferansTipi
            : MuhasebeKaynakModulleri.TasinirHareket;

        int? kaynakId = null;
        if (!string.IsNullOrWhiteSpace(request.ReferansId) && int.TryParse(request.ReferansId, out var parsedId))
        {
            kaynakId = parsedId;
        }

        // 9. Aynı kaynaktan daha önce oluşturulmuş aktif (İptal edilmemiş) fiş var mı kontrol et
        if (kaynakId.HasValue)
        {
            var mevcutFis = await _dbContext.MuhasebeFisler
                .AsNoTracking()
                .Where(f => f.TesisId == request.TesisId
                            && f.KaynakModul == kaynakModul
                            && f.KaynakId == kaynakId.Value
                            && f.Durum != MuhasebeFisDurumlari.Iptal)
                .Select(f => new { f.Id, f.FisNo, f.Durum })
                .FirstOrDefaultAsync(cancellationToken);

            if (mevcutFis is not null)
            {
                throw new BaseException(
                    $"Bu kaynak işlem için zaten bir muhasebe fişi taslağı oluşturulmuş. " +
                    $"Mevcut fiş: {mevcutFis.FisNo} (Durum: {mevcutFis.Durum}). " +
                    $"Aynı kaynaktan yeni bir fiş oluşturmak için önce mevcut fişi iptal ediniz.",
                    409);
            }
        }

        // 10. Açıklama oluştur
        var aciklama = !string.IsNullOrWhiteSpace(request.Aciklama)
            ? request.Aciklama
            : $"Taşınır kodu {tasinirKodu} için otomatik muhasebe fişi taslağı";

        if (!string.IsNullOrWhiteSpace(request.ReferansTipi) || !string.IsNullOrWhiteSpace(request.ReferansId))
        {
            aciklama += $" | Referans: {request.ReferansTipi}/{request.ReferansId}";
        }

        if (!string.IsNullOrWhiteSpace(request.BelgeNo))
        {
            aciklama += $" | Belge No: {request.BelgeNo}";
        }

        if (request.KdvOrani.HasValue)
        {
            aciklama += $" | KDV Oranı: %{request.KdvOrani.Value} | KDV Dahil: {(request.KdvDahilMi ? "Evet" : "Hayır")}";
        }

        // 11. Fiş ve satırları transaction içinde oluştur (aynı anda iki işlem aynı TSN
        //     numarasını üretirse yeni fiş no ile tekrar dene)
        const int maxRetry = 3;
        for (int attempt = 0; attempt < maxRetry; attempt++)
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // Transaction içinde tekrar duplicate kontrolü (race condition önlemi)
                if (kaynakId.HasValue)
                {
                    var transactionIciMevcutFis = await _dbContext.MuhasebeFisler
                        .Where(f => f.TesisId == request.TesisId
                                    && f.KaynakModul == kaynakModul
                                    && f.KaynakId == kaynakId.Value
                                    && f.Durum != MuhasebeFisDurumlari.Iptal)
                        .Select(f => new { f.Id })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (transactionIciMevcutFis is not null)
                    {
                        throw new BaseException(
                            $"Bu kaynak işlem için zaten bir muhasebe fişi oluşturulmuş. " +
                            $"Lütfen mevcut fişi kontrol ediniz.",
                            409);
                    }
                }

                var satirlar = new List<MuhasebeFisSatir>
                {
                    new()
                    {
                        MuhasebeHesapPlaniId = borcHesapKontrol.Id,
                        SiraNo = 1,
                        Borc = matrah,
                        Alacak = 0,
                        ParaBirimi = "TRY",
                        Kur = 1,
                        Aciklama = $"Taşınır kodu {tasinirKodu} borç kaydı",
                    },
                };

                int siraNo = 2;

                if (kdvHesap is not null && kdvTutari > 0)
                {
                    satirlar.Add(new MuhasebeFisSatir
                    {
                        MuhasebeHesapPlaniId = kdvHesap.Id,
                        SiraNo = siraNo++,
                        Borc = kdvTutari,
                        Alacak = 0,
                        ParaBirimi = "TRY",
                        Kur = 1,
                        Aciklama = $"Taşınır kodu {tasinirKodu} KDV kaydı (%{request.KdvOrani!.Value})",
                    });
                }

                satirlar.Add(new MuhasebeFisSatir
                {
                    MuhasebeHesapPlaniId = alacakHesap.Id,
                    SiraNo = siraNo,
                    Borc = 0,
                    Alacak = genelToplam,
                    ParaBirimi = "TRY",
                    Kur = 1,
                    Aciklama = $"Taşınır kodu {tasinirKodu} alacak kaydı",
                });

                var toplamBorc = matrah + kdvTutari;
                var toplamAlacak = genelToplam;

                var fisNo = await GenerateFisNoAsync(
                    request.TesisId,
                    request.MaliYil,
                    MuhasebeFisTipleri.Mahsup,
                    kaynakModul,
                    cancellationToken);

                var fis = new MuhasebeFis
                {
                    TesisId = request.TesisId,
                    MaliYil = request.MaliYil,
                    Donem = donem,
                    FisNo = fisNo,
                    FisTarihi = request.FisTarihi,
                    FisTipi = MuhasebeFisTipleri.Mahsup,
                    KaynakModul = kaynakModul,
                    KaynakId = kaynakId,
                    Durum = MuhasebeFisDurumlari.Taslak,
                    Aciklama = aciklama,
                    ToplamBorc = toplamBorc,
                    ToplamAlacak = toplamAlacak,
                    Satirlar = satirlar,
                };

                await _dbContext.MuhasebeFisler.AddAsync(fis, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new TasinirMuhasebeFisiOlusturResultDto
                {
                    MuhasebeFisId = fis.Id,
                    FisNo = fis.FisNo,
                    Durum = fis.Durum,
                    BorcHesapKodu = borcHesapKontrol.TamKod ?? string.Empty,
                    BorcHesapAdi = borcHesapKontrol.Ad,
                    AlacakHesapKodu = alacakHesap.TamKod ?? string.Empty,
                    AlacakHesapAdi = alacakHesap.Ad,
                    ToplamBorc = toplamBorc,
                    ToplamAlacak = toplamAlacak,
                    Matrah = matrah,
                    KdvTutari = kdvTutari,
                    GenelToplam = genelToplam,
                    KdvHesapKodu = kdvHesap?.TamKod,
                    KdvHesapAdi = kdvHesap?.Ad,
                    Mesaj = "Taşınır muhasebe fişi taslağı oluşturuldu.",
                };
            }
            catch (DbUpdateException ex) when (IsUniqueConflict(ex) && attempt < maxRetry - 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();

                // Unique conflict'in FisNo mu yoksa kaynak duplicate mi olduğunu ayırt et
                if (kaynakId.HasValue)
                {
                    var kaynakDuplicateMi = await _dbContext.MuhasebeFisler
                        .AsNoTracking()
                        .Where(f => f.TesisId == request.TesisId
                                    && f.KaynakModul == kaynakModul
                                    && f.KaynakId == kaynakId.Value
                                    && f.Durum != MuhasebeFisDurumlari.Iptal)
                        .AnyAsync(cancellationToken);

                    if (kaynakDuplicateMi)
                    {
                        throw new BaseException(
                            $"Bu kaynak işlem için zaten bir muhasebe fişi oluşturulmuş. " +
                            $"Aynı kaynaktan yeni bir fiş oluşturmak için önce mevcut fişi iptal ediniz.",
                            409);
                    }
                }

                // FisNo çakışması → tekrar dene
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
}
