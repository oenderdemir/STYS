using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.DonemKapanis.Dtos;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.DonemKapanis.Services;

public class DonemKapanisKontrolService : IDonemKapanisKontrolService
{
    private readonly StysAppDbContext _db;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public DonemKapanisKontrolService(StysAppDbContext db, IUserAccessScopeService userAccessScopeService)
    {
        _db = db;
        _userAccessScopeService = userAccessScopeService;
    }

    public async Task<DonemKapanisKontrolDto> KontrolEtAsync(
        DonemKapanisKontrolFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessTesisAsync(filter.TesisId, cancellationToken);

        var result = new DonemKapanisKontrolDto
        {
            TesisId = filter.TesisId,
            MaliYil = filter.MaliYil,
            DonemNo = filter.DonemNo,
            KapatilabilirMi = true
        };

        // 1. Dönem var mı?
        var donem = await _db.MuhasebeDonemler
            .FirstOrDefaultAsync(d =>
                d.TesisId == filter.TesisId
                && d.MaliYil == filter.MaliYil
                && d.DonemNo == filter.DonemNo,
                cancellationToken);

        if (donem == null)
        {
            result.DonemVarMi = false;
            result.KapatilabilirMi = false;
            result.Maddeler.Add(new DonemKapanisKontrolMaddeDto
            {
                Kod = "DONEM_YOK",
                Baslik = "Dönem bulunamadı",
                Mesaj = "Seçilen tesis, mali yıl ve dönem için muhasebe dönemi bulunamadı.",
                Severity = "error",
                BasariliMi = false,
                BloklayiciMi = true
            });
            return result;
        }

        result.DonemId = donem.Id;
        result.DonemVarMi = true;
        result.Maddeler.Add(new DonemKapanisKontrolMaddeDto
        {
            Kod = "DONEM_VAR",
            Baslik = "Dönem mevcut",
            Mesaj = $"Tesis #{filter.TesisId}, {filter.MaliYil} / Dönem {filter.DonemNo} bulundu.",
            Severity = "success",
            BasariliMi = true,
            BloklayiciMi = false
        });

        // 2. Dönem kapalı mı?
        if (donem.KapaliMi)
        {
            result.DonemKapaliMi = true;
            result.KapatilabilirMi = false;
            result.Maddeler.Add(new DonemKapanisKontrolMaddeDto
            {
                Kod = "DONEM_ZATEN_KAPALI",
                Baslik = "Dönem zaten kapalı",
                Mesaj = "Bu dönem daha önce kapatılmıştır.",
                Severity = "warn",
                BasariliMi = false,
                BloklayiciMi = true
            });
            return result;
        }

        result.DonemKapaliMi = false;

        // Fişleri bu dönem için sorgula
        var fisQuery = _db.MuhasebeFisler
            .Where(f => f.TesisId == filter.TesisId
                        && f.MaliYil == filter.MaliYil
                        && f.Donem == filter.DonemNo);

        var tumFisler = await fisQuery.ToListAsync(cancellationToken);

        // Sayımlar
        result.TaslakFisSayisi = tumFisler.Count(f => f.Durum == MuhasebeFisDurumlari.Taslak);
        result.OnayliFisSayisi = tumFisler.Count(f => f.Durum == MuhasebeFisDurumlari.Onayli);
        result.IptalFisSayisi = tumFisler.Count(f => f.Durum == MuhasebeFisDurumlari.Iptal);
        result.TersKayitFisSayisi = tumFisler.Count(f => f.Durum == MuhasebeFisDurumlari.TersKayit);

        // 3. Taslak fiş kontrolü
        if (result.TaslakFisSayisi > 0)
        {
            result.KapatilabilirMi = false;
            result.Maddeler.Add(new DonemKapanisKontrolMaddeDto
            {
                Kod = "TASLAK_FIS_VAR",
                Baslik = "Taslak fişler var",
                Mesaj = $"Bu dönemde {result.TaslakFisSayisi} adet onaylanmamış taslak fiş bulunmaktadır.",
                Severity = "error",
                BasariliMi = false,
                BloklayiciMi = true,
                Route = "muhasebe/fisler"
            });
        }
        else
        {
            result.Maddeler.Add(new DonemKapanisKontrolMaddeDto
            {
                Kod = "TASLAK_FIS_YOK",
                Baslik = "Taslak fiş yok",
                Mesaj = "Bu dönemde onaylanmamış taslak fiş bulunmamaktadır.",
                Severity = "success",
                BasariliMi = true,
                BloklayiciMi = false
            });
        }

        // 4. Dengesiz taslak kontrolü
        var dengesizTaslakFisler = tumFisler
            .Where(f => f.Durum == MuhasebeFisDurumlari.Taslak
                        && Math.Abs(f.ToplamBorc - f.ToplamAlacak) > 0.009m)
            .ToList();
        result.DengesizTaslakFisSayisi = dengesizTaslakFisler.Count;

        if (dengesizTaslakFisler.Count > 0)
        {
            foreach (var fis in dengesizTaslakFisler)
            {
                result.ProblemliFisler.Add(new DonemKapanisKontrolFisOzetDto
                {
                    Id = fis.Id,
                    FisNo = fis.FisNo,
                    YevmiyeNo = fis.YevmiyeNo,
                    FisTarihi = fis.FisTarihi,
                    FisTipi = fis.FisTipi,
                    Durum = fis.Durum,
                    ToplamBorc = fis.ToplamBorc,
                    ToplamAlacak = fis.ToplamAlacak,
                    ProblemTipi = "Dengesiz Taslak"
                });
            }

            result.Maddeler.Add(new DonemKapanisKontrolMaddeDto
            {
                Kod = "DENGESIZ_TASLAK",
                Baslik = "Dengesiz taslak fişler",
                Mesaj = $"{dengesizTaslakFisler.Count} adet dengesiz taslak fiş bulunmaktadır.",
                Severity = "warn",
                BasariliMi = false,
                BloklayiciMi = false,
                Route = "muhasebe/fisler"
            });
        }

        // 5+6+7. Onaylı + TersKayıt fiş kontrolleri
        var onayliVeTersKayitFisler = tumFisler
            .Where(f => f.Durum == MuhasebeFisDurumlari.Onayli
                        || f.Durum == MuhasebeFisDurumlari.TersKayit)
            .ToList();

        // 5. Dengesiz onaylı fiş kontrolü
        var dengesizOnayliFisler = onayliVeTersKayitFisler
            .Where(f => Math.Abs(f.ToplamBorc - f.ToplamAlacak) > 0.009m)
            .ToList();
        result.DengesizOnayliFisSayisi = dengesizOnayliFisler.Count;

        if (dengesizOnayliFisler.Count > 0)
        {
            result.KapatilabilirMi = false;
            foreach (var fis in dengesizOnayliFisler)
            {
                result.ProblemliFisler.Add(new DonemKapanisKontrolFisOzetDto
                {
                    Id = fis.Id,
                    FisNo = fis.FisNo,
                    YevmiyeNo = fis.YevmiyeNo,
                    FisTarihi = fis.FisTarihi,
                    FisTipi = fis.FisTipi,
                    Durum = fis.Durum,
                    ToplamBorc = fis.ToplamBorc,
                    ToplamAlacak = fis.ToplamAlacak,
                    ProblemTipi = "Dengesiz Onaylı/Ters Kayıt Fiş"
                });
            }

            result.Maddeler.Add(new DonemKapanisKontrolMaddeDto
            {
                Kod = "DENGESIZ_ONAYLI_FIS",
                Baslik = "Dengesiz onaylı fiş",
                Mesaj = "Onaylı/Ters kayıt fişlerde borç-alacak dengesi bozuk kayıtlar var.",
                Severity = "error",
                BasariliMi = false,
                BloklayiciMi = true,
                Route = "muhasebe/fisler"
            });
        }
        else
        {
            result.Maddeler.Add(new DonemKapanisKontrolMaddeDto
            {
                Kod = "ONAYLI_FISLER_DENGELI",
                Baslik = "Onaylı fişler dengeli",
                Mesaj = "Onaylı/Ters kayıt fişlerde borç-alacak dengesi doğru.",
                Severity = "success",
                BasariliMi = true,
                BloklayiciMi = false
            });
        }

        // 6. Yevmiye no kontrolü
        var yevmiyeNoEksikFisler = onayliVeTersKayitFisler
            .Where(f => f.YevmiyeNo == null)
            .ToList();
        result.YevmiyeNoEksikOnayliFisSayisi = yevmiyeNoEksikFisler.Count;

        if (yevmiyeNoEksikFisler.Count > 0)
        {
            result.KapatilabilirMi = false;
            foreach (var fis in yevmiyeNoEksikFisler)
            {
                result.ProblemliFisler.Add(new DonemKapanisKontrolFisOzetDto
                {
                    Id = fis.Id,
                    FisNo = fis.FisNo,
                    YevmiyeNo = fis.YevmiyeNo,
                    FisTarihi = fis.FisTarihi,
                    FisTipi = fis.FisTipi,
                    Durum = fis.Durum,
                    ToplamBorc = fis.ToplamBorc,
                    ToplamAlacak = fis.ToplamAlacak,
                    ProblemTipi = "Yevmiye No Eksik"
                });
            }

            result.Maddeler.Add(new DonemKapanisKontrolMaddeDto
            {
                Kod = "YEVMIYE_NO_EKSIK",
                Baslik = "Yevmiye no eksik",
                Mesaj = $"Onaylı/Ters kayıt fişlerde {yevmiyeNoEksikFisler.Count} adet yevmiye numarası eksik kayıt var.",
                Severity = "error",
                BasariliMi = false,
                BloklayiciMi = true,
                Route = "muhasebe/fisler"
            });
        }
        else
        {
            result.Maddeler.Add(new DonemKapanisKontrolMaddeDto
            {
                Kod = "YEVMIYE_NO_TAM",
                Baslik = "Yevmiye numaraları tam",
                Mesaj = "Tüm onaylı/Ters kayıt fişlerin yevmiye numarası mevcut.",
                Severity = "success",
                BasariliMi = true,
                BloklayiciMi = false
            });
        }

        // 7. Toplam kontrol (Onaylı + TersKayıt)
        result.ToplamBorc = onayliVeTersKayitFisler.Sum(f => f.ToplamBorc);
        result.ToplamAlacak = onayliVeTersKayitFisler.Sum(f => f.ToplamAlacak);
        result.Fark = result.ToplamBorc - result.ToplamAlacak;

        if (Math.Abs(result.Fark) > 0.009m)
        {
            result.KapatilabilirMi = false;
            result.Maddeler.Add(new DonemKapanisKontrolMaddeDto
            {
                Kod = "DONEM_TOPLAM_DENGESIZ",
                Baslik = "Dönem toplamı dengesiz",
                Mesaj = $"Dönem toplam borç ({result.ToplamBorc:N2}) ile toplam alacak ({result.ToplamAlacak:N2}) arasında {result.Fark:N2} fark var.",
                Severity = "error",
                BasariliMi = false,
                BloklayiciMi = true
            });
        }
        else if (onayliVeTersKayitFisler.Count > 0)
        {
            result.Maddeler.Add(new DonemKapanisKontrolMaddeDto
            {
                Kod = "DONEM_TOPLAMI_DENGELI",
                Baslik = "Dönem toplamı dengeli",
                Mesaj = $"Toplam borç: {result.ToplamBorc:N2} / Toplam alacak: {result.ToplamAlacak:N2} — Denkleşmiş.",
                Severity = "success",
                BasariliMi = true,
                BloklayiciMi = false
            });
        }
        else
        {
            result.Maddeler.Add(new DonemKapanisKontrolMaddeDto
            {
                Kod = "DONEM_TOPLAMI_DENGELI",
                Baslik = "Dönem toplamı",
                Mesaj = "Bu dönemde onaylı/Ters kayıt fiş bulunmamaktadır.",
                Severity = "info",
                BasariliMi = true,
                BloklayiciMi = false
            });
        }

        return result;
    }

    private async Task EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken)
    {
        if (tesisId <= 0)
        {
            throw new BaseException("Tesis secimi zorunludur.", 400);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && !scope.TesisIds.Contains(tesisId))
        {
            throw new BaseException("Seçilen tesis için yetkiniz bulunmuyor.", 403);
        }

        var exists = await _db.Tesisler.AnyAsync(x => x.Id == tesisId && x.AktifMi, cancellationToken);
        if (!exists)
        {
            throw new BaseException("Seçilen tesis bulunamadı.", 400);
        }
    }
}
