using STYS.Kamp.Dto;

namespace STYS.Kamp.Services;

public class KampIadeService : IKampIadeService
{
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

        if (vazgecmeTarihi <= kampBaslangicTarihi.AddDays(-7))
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
        var kesintiTutari = decimal.Round(Math.Min(request.AvansTutari, request.AvansTutari * 0.05m * gunSayisi), 2, MidpointRounding.AwayFromZero);
        var iadeTutariGec = decimal.Round(Math.Max(0m, request.AvansTutari - kesintiTutari), 2, MidpointRounding.AwayFromZero);
        return new KampIadeKarariDto
        {
            IadeVarMi = iadeTutariGec > 0,
            IadeTutari = iadeTutariGec,
            KesintiTutari = kesintiTutari,
            Gerekce = KampIadeNedenleri.GecBildirimMazeretsiz
        };
    }
}
