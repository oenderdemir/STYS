using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services.MuhasebeFisStratejileri;

public sealed class SatisIadeFaturasiMuhasebeFisStratejisi : ISatisBelgesiMuhasebeFisStratejisi
{
    private readonly StysAppDbContext _dbContext;

    public SatisIadeFaturasiMuhasebeFisStratejisi(StysAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public bool Destekler(SatisBelgesi belge)
        => belge.BelgeTipi == SatisBelgesiTipi.SatisIadeFaturasi
           && !HasTevkifatliSatir(belge);

    public async Task<IReadOnlyList<MuhasebeFisSatiriTaslak>> SatirlariOlusturAsync(
        SatisBelgesi belge,
        SatisBelgesiMuhasebeFisContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (belge.Satirlar.Count == 0)
            throw new BaseException("Satış iade belgesinde aktif satır bulunamadı.", 400);

        var iadeHesabi = await ResolveHesapByAnaKodAsync(MuhasebeAnaHesapKodlari.SatisIade, belge.TesisId!.Value, cancellationToken);

        // ÖTV/ÖİV/konaklama vergisi içeren belgeler için bu strateji hiç çağrılmaz —
        // SatisBelgesiMuhasebeFisService, fiş/cari/stok hareketi oluşturulmadan önce bu
        // belgeleri reddeder (bkz. SatisBelgesiMuhasebeFisService.MuhasebeFisiOlusturAsync).
        var satirlar = new List<MuhasebeFisSatiriTaslak>
        {
            new()
            {
                MuhasebeHesapPlaniId = iadeHesabi.Id,
                SiraNo = 1,
                Borc = belge.ToplamMatrah,
                Alacak = 0,
                Aciklama = $"Satış iade bedeli - {belge.BelgeNo}"
            }
        };

        var siraNo = 2;

        if (belge.ToplamKdv > 0)
        {
            foreach (var (oran, tutar) in KdvOranGruplamaHelper.Grupla(belge.Satirlar))
            {
                if (!context.KdvHesaplariByOran.TryGetValue(oran, out var hesapId))
                    throw new BaseException($"%{oran} oranlı Hesaplanan KDV iadesi için 391 hesabı bulunamadı.", 400);

                satirlar.Add(new MuhasebeFisSatiriTaslak
                {
                    MuhasebeHesapPlaniId = hesapId,
                    SiraNo = siraNo++,
                    Borc = tutar,
                    Alacak = 0,
                    Aciklama = $"Hesaplanan KDV iadesi (%{oran}) - {belge.BelgeNo}"
                });
            }
        }

        satirlar.Add(new MuhasebeFisSatiriTaslak
        {
            MuhasebeHesapPlaniId = context.CariHesapPlaniId,
            SiraNo = siraNo,
            Borc = 0,
            Alacak = belge.GenelToplam,
            Aciklama = $"Alıcı borcu iadesi - {belge.BelgeNo}",
            CariKartId = context.CariKartId
        });

        var toplamBorc = satirlar.Sum(x => x.Borc);
        var toplamAlacak = satirlar.Sum(x => x.Alacak);

        if (Math.Abs(toplamBorc - toplamAlacak) > 0.01m)
            throw new BaseException(
                $"Satış iade fiş borç/alacak dengesi sağlanamadı. Borç: {toplamBorc:N2}, Alacak: {toplamAlacak:N2}",
                400);

        return satirlar;
    }

    private async Task<MuhasebeHesapPlani> ResolveHesapByAnaKodAsync(
        string anaKod,
        int tesisId,
        CancellationToken cancellationToken)
    {
        var hesap = await _dbContext.MuhasebeHesapPlanlari
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.AktifMi &&
                x.HareketGorebilirMi &&
                x.DetayHesapMi &&
                (x.TesisId == tesisId || x.TesisId == null) &&
                (x.TamKod == anaKod || x.Kod == anaKod || x.AnaHesapKodu == anaKod || x.TamKod.StartsWith(anaKod + ".")))
            .OrderByDescending(x => x.TesisId == tesisId)
            .ThenBy(x => x.TamKod)
            .FirstOrDefaultAsync(cancellationToken);

        if (hesap is null)
            throw new BaseException($"Satış iade faturası için {anaKod} hesabı bulunamadı.", 400);

        return hesap;
    }

    private static bool HasTevkifatliSatir(SatisBelgesi belge)
        => belge.Satirlar?.Any(s =>
               !s.IsDeleted &&
               s.KdvUygulamaTipi == KdvUygulamaTipi.Tevkifatli) == true;
}
