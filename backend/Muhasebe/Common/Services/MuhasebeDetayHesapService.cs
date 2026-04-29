using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.Common.Services;

public class MuhasebeDetayHesapService : IMuhasebeDetayHesapService
{
    private readonly StysAppDbContext _dbContext;

    public MuhasebeDetayHesapService(StysAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(int HesapPlaniId, string Kod, int SiraNo)> CreateAsync(
        int tesisId,
        string anaHesapKodu,
        string kaynakAd,
        string kaynakTipi,
        CancellationToken cancellationToken = default)
    {
        if (tesisId <= 0)
        {
            throw new BaseException("Tesis secimi zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(anaHesapKodu))
        {
            throw new BaseException($"{kaynakTipi} icin ana muhasebe hesap kodu tanimli degil.", 400);
        }

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var anaHesap = await _dbContext.MuhasebeHesapPlanlari
                    .Where(x => !x.IsDeleted && x.AktifMi && x.TesisId == null)
                    .FirstOrDefaultAsync(x => x.Kod == anaHesapKodu || x.TamKod == anaHesapKodu, cancellationToken);

                if (anaHesap is null)
                {
                    throw new BaseException($"{anaHesapKodu} ana hesabı bulunamadı.", 400);
                }

                var siraNo = await NextSiraNoAsync(tesisId, anaHesapKodu, kaynakTipi, cancellationToken);
                var kod = $"{anaHesapKodu}.{siraNo}";

                var existing = await _dbContext.MuhasebeHesapPlanlari
                    .FirstOrDefaultAsync(x => !x.IsDeleted && x.TesisId == tesisId && (x.Kod == kod || x.TamKod == kod), cancellationToken);

                if (existing is not null)
                {
                    existing.Ad = kaynakAd;
                    existing.AktifMi = true;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);
                    return (existing.Id, existing.Kod, siraNo);
                }

                var detay = new MuhasebeHesapPlani
                {
                    TesisId = tesisId,
                    Kod = kod,
                    TamKod = kod,
                    Ad = kaynakAd,
                    UstHesapId = anaHesap.Id,
                    SeviyeNo = anaHesap.SeviyeNo + 1,
                    AktifMi = true,
                    Aciklama = $"{kaynakTipi} otomatik detay hesabi"
                };

                await _dbContext.MuhasebeHesapPlanlari.AddAsync(detay, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return (detay.Id, detay.Kod, siraNo);
            }
            catch (DbUpdateConcurrencyException) when (attempt < 5)
            {
                await tx.RollbackAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (attempt < 5 && IsRetryableSqlConflict(ex))
            {
                await tx.RollbackAsync(cancellationToken);
            }
        }

        throw new BaseException("Muhasebe detay hesabi olusturulurken eszamanli islem catismasi olustu. Tekrar deneyiniz.", 409);
    }

    private async Task<int> NextSiraNoAsync(int tesisId, string anaHesapKodu, string kaynakTipi, CancellationToken cancellationToken)
    {
        var sayac = await _dbContext.Set<MuhasebeHesapKoduSayac>()
            .FirstOrDefaultAsync(x => x.TesisId == tesisId && x.AnaHesapKodu == anaHesapKodu, cancellationToken);

        if (sayac is null)
        {
            sayac = new MuhasebeHesapKoduSayac
            {
                TesisId = tesisId,
                AnaHesapKodu = anaHesapKodu,
                SonSiraNo = 0,
                Aciklama = $"{kaynakTipi} otomatik kod sayaci"
            };
            await _dbContext.Set<MuhasebeHesapKoduSayac>().AddAsync(sayac, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        sayac.SonSiraNo += 1;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return sayac.SonSiraNo;
    }

    private static bool IsRetryableSqlConflict(DbUpdateException ex)
    {
        var sqlEx = ex.InnerException as SqlException;
        if (sqlEx is null)
        {
            return false;
        }

        return sqlEx.Number is 2601 or 2627;
    }
}

