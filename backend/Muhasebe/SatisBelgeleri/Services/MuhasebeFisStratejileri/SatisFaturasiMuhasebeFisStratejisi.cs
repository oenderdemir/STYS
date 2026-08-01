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

        // ÖTV/ÖİV/konaklama vergisi içeren belgeler için bu strateji hiç çağrılmaz —
        // SatisBelgesiMuhasebeFisService, fiş/cari/stok hareketi oluşturulmadan önce bu
        // belgeleri reddeder (bkz. SatisBelgesiMuhasebeFisService.MuhasebeFisiOlusturAsync).
        // Bu yüzden burada Gelir hesabına yalnızca ToplamMatrah yazılır.
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

        return Task.FromResult<IReadOnlyList<MuhasebeFisSatiriTaslak>>(satirlar);
    }

    private static bool HasTevkifatliSatir(SatisBelgesi belge)
        => belge.Satirlar?.Any(s =>
               !s.IsDeleted &&
               s.KdvUygulamaTipi == KdvUygulamaTipi.Tevkifatli) == true;
}
