using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Kamp.Dto;

namespace STYS.Kamp.Services;

public class KampIadeService : IKampIadeService
{
    private readonly IKampParametreService _parametreService;
    private readonly StysAppDbContext? _dbContext;

    public KampIadeService(IKampParametreService parametreService, StysAppDbContext? dbContext = null)
    {
        _parametreService = parametreService;
        _dbContext = dbContext;
    }

    public KampIadeKarariDto Hesapla(KampIadeHesaplamaRequestDto request)
    {
        if (string.Equals(request.BasvuruDurumu, KampBasvuruDurumlari.TahsisEdilemedi, StringComparison.OrdinalIgnoreCase))
        {
            return new KampIadeKarariDto
            {
                IadeVarMi = request.AvansTutari > 0,
                IadeTutari = request.AvansTutari,
                KesintiTutari = 0,
                Gerekce = KampIadeNedenleri.TahsisYapilamadi
            };
        }

        if (request.MazeretliZorunluAyrilisMi && request.KullanilmayanGunSayisi > 0 && request.ToplamGunSayisi > 0)
        {
            var gunlukTutar = request.DonemToplamTutari / request.ToplamGunSayisi;
            var iadeTutari = decimal.Round(gunlukTutar * request.KullanilmayanGunSayisi, 2, MidpointRounding.AwayFromZero);
            return new KampIadeKarariDto
            {
                IadeVarMi = iadeTutari > 0,
                IadeTutari = iadeTutari,
                KesintiTutari = 0,
                Gerekce = KampIadeNedenleri.ZorunluAyrilis
            };
        }

        if (!request.VazgecmeTarihi.HasValue)
        {
            return new KampIadeKarariDto
            {
                IadeVarMi = false,
                IadeTutari = 0,
                KesintiTutari = request.AvansTutari,
                Gerekce = KampIadeNedenleri.BildirimYok
            };
        }

        var vazgecmeTarihi = request.VazgecmeTarihi.Value.Date;
        var kampBaslangicTarihi = request.KampBaslangicTarihi.Date;

        if (vazgecmeTarihi > kampBaslangicTarihi)
        {
            return new KampIadeKarariDto
            {
                IadeVarMi = false,
                IadeTutari = 0,
                KesintiTutari = request.AvansTutari,
                Gerekce = KampIadeNedenleri.KampBasladi
            };
        }

        var (vazgecmeGunSayisi, gunlukKesintiyUzdesi) = ResolveIadeAyarlari(request.KampDonemiId);
        if (vazgecmeTarihi <= kampBaslangicTarihi.AddDays(-vazgecmeGunSayisi))
        {
            return new KampIadeKarariDto
            {
                IadeVarMi = request.AvansTutari > 0,
                IadeTutari = request.AvansTutari,
                KesintiTutari = 0,
                Gerekce = KampIadeNedenleri.BirHaftaOncesiVazgecme
            };
        }

        var gunSayisi = Math.Max(0, (kampBaslangicTarihi - vazgecmeTarihi).Days);
        var kesintiTutari = decimal.Round(Math.Min(request.AvansTutari, request.AvansTutari * gunlukKesintiyUzdesi * gunSayisi), 2, MidpointRounding.AwayFromZero);
        var iadeTutariGec = decimal.Round(Math.Max(0m, request.AvansTutari - kesintiTutari), 2, MidpointRounding.AwayFromZero);
        return new KampIadeKarariDto
        {
            IadeVarMi = iadeTutariGec > 0,
            IadeTutari = iadeTutariGec,
            KesintiTutari = kesintiTutari,
            Gerekce = KampIadeNedenleri.GecBildirimMazeretsiz
        };
    }

    private (int vazgecmeGunSayisi, decimal gunlukKesintiYuzdesi) ResolveIadeAyarlari(int? kampDonemiId)
    {
        var defaultVazgecmeGunSayisi = _parametreService.GetInt(KampParametreKodlari.VazgecmeIadeGunSayisi, 7);
        var defaultKesinti = _parametreService.GetDecimal(KampParametreKodlari.GecBildirimGunlukKesintiyUzdesi, 0.05m);

        if (!_dbContext?.KampProgramiParametreAyarlari.Any() ?? true)
        {
            return (defaultVazgecmeGunSayisi, defaultKesinti);
        }

        if (!kampDonemiId.HasValue || kampDonemiId.Value <= 0 || _dbContext is null)
        {
            return (defaultVazgecmeGunSayisi, defaultKesinti);
        }

        var kampProgramiId = _dbContext.KampDonemleri
            .AsNoTracking()
            .Where(x => x.Id == kampDonemiId.Value)
            .Select(x => x.KampProgramiId)
            .FirstOrDefault();
        if (kampProgramiId <= 0)
        {
            return (defaultVazgecmeGunSayisi, defaultKesinti);
        }

        var ayar = _dbContext.KampProgramiParametreAyarlari
            .AsNoTracking()
            .Where(x => x.AktifMi && x.KampProgramiId == kampProgramiId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefault();
        if (ayar is null)
        {
            return (defaultVazgecmeGunSayisi, defaultKesinti);
        }

        return (
            ayar.VazgecmeIadeGunSayisi ?? defaultVazgecmeGunSayisi,
            ayar.GecBildirimGunlukKesintiyUzdesi ?? defaultKesinti);
    }
}
