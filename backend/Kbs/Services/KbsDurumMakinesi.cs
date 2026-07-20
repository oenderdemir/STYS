using STYS.Kbs.Constants;

namespace STYS.Kbs.Services;

public static class KbsDurumMakinesi
{
    public static bool ManuelRetryYapilabilir(string durum) => durum == KbsBildirimDurumlari.MudahaleGerekli;
    public static bool MutabakatYapilabilir(string durum) => durum == KbsBildirimDurumlari.SonucuBelirsiz;
    public static bool EgmDogrulanabilir(string durum) => durum == KbsBildirimDurumlari.YuklemeOnayiBekliyor;
    public static bool WorkerGonderebilir(string durum) => durum is KbsBildirimDurumlari.Hazir or KbsBildirimDurumlari.TekrarBekliyor;
}
