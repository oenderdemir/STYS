using STYS.Kamp.Dto;

namespace STYS.Kamp.Services;

public class KampPuanlamaService : IKampPuanlamaService
{
    public KampBasvuruOnizlemeDto Puanla(KampBasvuruRequestDto request, KampBasvuruOnizlemeDto onizleme)
    {
        var puan = KampBasvuruKurallari.GetTabanPuan(request.BasvuruSahibiTipi);

        if (request.BasvuruSahibiTipi == KampBasvuruSahibiTipleri.TarimOrmanPersoneli)
        {
            puan += Math.Max(request.HizmetYili, 0);
        }

        if (request.BasvuruSahibiTipi == KampBasvuruSahibiTipleri.TarimOrmanEmeklisi)
        {
            puan += 30;
        }

        puan += request.Katilimcilar.Count * 10;

        if (request.Kamp2023tenFaydalandiMi)
        {
            puan -= 20;
        }

        if (request.Kamp2024tenFaydalandiMi)
        {
            puan -= 20;
        }

        onizleme.Puan = puan;
        onizleme.OncelikSirasi = KampBasvuruKurallari.GetOncelik(request.BasvuruSahibiTipi);
        return onizleme;
    }
}
