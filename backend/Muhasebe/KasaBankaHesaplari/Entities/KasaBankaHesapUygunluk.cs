using System.Linq.Expressions;

namespace STYS.Muhasebe.KasaBankaHesaplari.Entities;

public static class KasaBankaHesapUygunluk
{
    // Selection/write policy only: historical queries must retain legacy accounts.
    public static readonly Expression<Func<KasaBankaHesap, bool>> YeniIslemIcinGecerli = x =>
        !x.IsDeleted && x.AktifMi
        && x.MuhasebeHesapPlani != null
        && !x.MuhasebeHesapPlani.IsDeleted && x.MuhasebeHesapPlani.AktifMi
        && x.MuhasebeHesapPlani.HareketGorebilirMi && x.MuhasebeHesapPlani.DetayHesapMi;

    public static IQueryable<KasaBankaHesap> YeniIslemIcin(this IQueryable<KasaBankaHesap> query)
        => query.Where(YeniIslemIcinGecerli);
}
