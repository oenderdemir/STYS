using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.Kdv.Enums;

namespace STYS.Muhasebe.SatisBelgeleri.Services.MuhasebeFisStratejileri;

public sealed class SatisFaturasiMuhasebeFisStratejisi : ISatisBelgesiMuhasebeFisStratejisi
{
    public bool Destekler(SatisBelgesi belge)
        => belge.BelgeTipi is SatisBelgesiTipi.FaturaTaslagi or SatisBelgesiTipi.SatisFaturasi
           && !HasTevkifatliSatir(belge);

    public Task<IReadOnlyList<MuhasebeFisSatiriTaslak>> SatirlariOlusturAsync(
        SatisBelgesi belge,
        SatisBelgesiMuhasebeFisContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // ÖTV/ÖİV/konaklama vergisi için ayrı bir hesap planı eşlemesi tanımlı DEĞİL (bkz.
        // SatisBelgesiMuhasebeFisContext) - bu tutarlar, fişin Borç (GenelToplam, artık bu
        // vergileri de içeriyor) = Alacak dengesini korumak için Gelir hesabına eklenir.
        var ekVergiToplami = SatisBelgesiTutarHesaplayici.HesaplaEkVergiToplami(belge.Satirlar.Where(s => !s.IsDeleted));

        var satirlar = new List<MuhasebeFisSatiriTaslak>
        {
            new()
            {
                MuhasebeHesapPlaniId = context.CariHesapPlaniId,
                SiraNo = 1,
                Borc = belge.GenelToplam,
                Alacak = 0,
                Aciklama = $"Satış belgesi alacağı - {belge.BelgeNo}",
                CariKartId = context.CariKartId
            },
            new()
            {
                MuhasebeHesapPlaniId = context.GelirHesapPlaniId,
                SiraNo = 2,
                Borc = 0,
                Alacak = belge.ToplamMatrah + ekVergiToplami,
                Aciklama = ekVergiToplami > 0
                    ? $"Satış geliri (ÖTV/ÖİV/konaklama vergisi dahil) - {belge.BelgeNo}"
                    : $"Satış geliri - {belge.BelgeNo}"
            }
        };

        if (belge.ToplamKdv > 0 && context.KdvHesapPlaniId.HasValue)
        {
            satirlar.Add(new MuhasebeFisSatiriTaslak
            {
                MuhasebeHesapPlaniId = context.KdvHesapPlaniId.Value,
                SiraNo = 3,
                Borc = 0,
                Alacak = belge.ToplamKdv,
                Aciklama = $"Hesaplanan KDV - {belge.BelgeNo}"
            });
        }

        return Task.FromResult<IReadOnlyList<MuhasebeFisSatiriTaslak>>(satirlar);
    }

    private static bool HasTevkifatliSatir(SatisBelgesi belge)
        => belge.Satirlar?.Any(s =>
               !s.IsDeleted &&
               s.KdvUygulamaTipi == KdvUygulamaTipi.Tevkifatli) == true;
}
