using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.Kdv.Enums;
using TOD.Platform.SharedKernel.Exceptions;

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
                Alacak = belge.ToplamMatrah,
                Aciklama = $"Satış geliri - {belge.BelgeNo}"
            }
        };

        if (belge.ToplamKdv > 0)
        {
            var siraNo = 3;
            foreach (var (oran, tutar) in KdvOranGruplamaHelper.Grupla(belge.Satirlar))
            {
                if (!context.KdvHesaplariByOran.TryGetValue(oran, out var hesapId))
                    throw new BaseException($"%{oran} oranlı Hesaplanan KDV için hesap bulunamadı.", 400);

                satirlar.Add(new MuhasebeFisSatiriTaslak
                {
                    MuhasebeHesapPlaniId = hesapId,
                    SiraNo = siraNo++,
                    Borc = 0,
                    Alacak = tutar,
                    Aciklama = $"Hesaplanan KDV (%{oran}) - {belge.BelgeNo}"
                });
            }
        }

        var konaklamaVergisiTutari = belge.Satirlar
            .Where(x => !x.IsDeleted)
            .Sum(x => x.KonaklamaVergisiTutari);

        if (konaklamaVergisiTutari > 0)
        {
            if (!context.KonaklamaVergisiHesapPlaniId.HasValue)
            {
                throw new BaseException(
                    "Konaklama vergisi için muhasebe hesabı tanımlanmamış. Muhasebe Yönetimi > Konaklama Vergisi Hesabı ekranından hesap eşlemesi yapın.",
                    400);
            }

            satirlar.Add(new MuhasebeFisSatiriTaslak
            {
                MuhasebeHesapPlaniId = context.KonaklamaVergisiHesapPlaniId.Value,
                SiraNo = satirlar.Count + 1,
                Borc = 0,
                Alacak = konaklamaVergisiTutari,
                Aciklama = $"Konaklama vergisi - {belge.BelgeNo}"
            });
        }

        return Task.FromResult<IReadOnlyList<MuhasebeFisSatiriTaslak>>(satirlar);
    }

    private static bool HasTevkifatliSatir(SatisBelgesi belge)
        => belge.Satirlar?.Any(s =>
               !s.IsDeleted &&
               s.KdvUygulamaTipi == KdvUygulamaTipi.Tevkifatli) == true;
}
