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

    public async Task<MuhasebeDetayHesapSonuc> CreateOrResolveDetayHesapAsync(
        int tesisId,
        string anaMuhasebeHesapKodu,
        string kaynakTipi,
        string kaynakAd,
        CancellationToken cancellationToken = default)
    {
        if (tesisId <= 0)
        {
            throw new BaseException("Tesis secimi zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(anaMuhasebeHesapKodu))
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
                    .FirstOrDefaultAsync(x => x.Kod == anaMuhasebeHesapKodu, cancellationToken);

                if (anaHesap is null)
                {
                    throw new BaseException(BuildAnaHesapBulunamadiMesaji(anaMuhasebeHesapKodu), 400);
                }

                var siraNo = await NextSiraNoAsync(tesisId, anaMuhasebeHesapKodu, kaynakTipi, cancellationToken);
                var kod = $"{anaMuhasebeHesapKodu}.{siraNo}";

                var existing = await _dbContext.MuhasebeHesapPlanlari
                    .FirstOrDefaultAsync(x => !x.IsDeleted && x.TesisId == tesisId && (x.Kod == kod || x.TamKod == kod), cancellationToken);

                if (existing is not null)
                {
                    EnsureNotLinkedToAnotherSource(existing.Id, kaynakTipi);
                    existing.Ad = kaynakAd;
                    existing.AktifMi = true;
                    await tx.CommitAsync(cancellationToken);
                    return new MuhasebeDetayHesapSonuc
                    {
                        MuhasebeHesapPlaniId = existing.Id,
                        Kod = existing.Kod,
                        AnaMuhasebeHesapKodu = anaMuhasebeHesapKodu,
                        SiraNo = siraNo
                    };
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
                return new MuhasebeDetayHesapSonuc
                {
                    MuhasebeHesapPlaniId = detay.Id,
                    Kod = detay.Kod,
                    AnaMuhasebeHesapKodu = anaMuhasebeHesapKodu,
                    SiraNo = siraNo
                };
            }
            catch (DbUpdateConcurrencyException) when (attempt < 5)
            {
                await tx.RollbackAsync(cancellationToken);
            }
            catch (DbUpdateException) when (attempt < 5)
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

    private void EnsureNotLinkedToAnotherSource(int hesapId, string kaynakTipi)
    {
        var hasLink = kaynakTipi switch
        {
            "CariKart" => _dbContext.CariKartlar.Any(x => !x.IsDeleted && x.MuhasebeHesapPlaniId == hesapId),
            "KasaBankaHesap" => _dbContext.KasaBankaHesaplari.Any(x => !x.IsDeleted && x.MuhasebeHesapPlaniId == hesapId),
            "Depo" => _dbContext.Depolar.Any(x => !x.IsDeleted && x.MuhasebeHesapPlaniId == hesapId),
            "TasinirKart" => _dbContext.TasinirKartlar.Any(x => !x.IsDeleted && x.MuhasebeHesapPlaniId == hesapId),
            _ => false
        };

        if (hasLink)
        {
            throw new BaseException("Ayni kodlu muhasebe hesabi baska bir kaynaga bagli.", 400);
        }
    }

    private static string BuildAnaHesapBulunamadiMesaji(string anaMuhasebeHesapKodu)
    {
        return anaMuhasebeHesapKodu switch
        {
            "3.32.320" => "3.32.320 SATICILAR ana hesabı bulunamadı.",
            "1.12.120" => "1.12.120 ALICILAR ana hesabı bulunamadı.",
            "1.10.100" => "1.10.100 KASA ana hesabı bulunamadı.",
            "1.10.102" => "1.10.102 BANKALAR ana hesabı bulunamadı.",
            "1.10.109" => "1.10.109 KREDİ KARTLARI ana hesabı bulunamadı.",
            _ => $"{anaMuhasebeHesapKodu} ana hesabı bulunamadı."
        };
    }
}
