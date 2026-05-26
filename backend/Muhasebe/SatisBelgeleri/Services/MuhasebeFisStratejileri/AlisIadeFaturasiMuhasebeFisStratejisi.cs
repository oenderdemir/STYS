using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services.MuhasebeFisStratejileri;

public sealed class AlisIadeFaturasiMuhasebeFisStratejisi : ISatisBelgesiMuhasebeFisStratejisi
{
    private readonly StysAppDbContext _dbContext;
    private readonly ITasinirKodMuhasebeHesapEslemeService _tasinirKodMuhasebeHesapEslemeService;

    public AlisIadeFaturasiMuhasebeFisStratejisi(
        StysAppDbContext dbContext,
        ITasinirKodMuhasebeHesapEslemeService tasinirKodMuhasebeHesapEslemeService)
    {
        _dbContext = dbContext;
        _tasinirKodMuhasebeHesapEslemeService = tasinirKodMuhasebeHesapEslemeService;
    }

    public bool Destekler(SatisBelgesi belge)
        => belge.BelgeTipi == SatisBelgesiTipi.AlisIadeFaturasi
           && !HasTevkifatliSatir(belge);

    public async Task<IReadOnlyList<MuhasebeFisSatiriTaslak>> SatirlariOlusturAsync(
        SatisBelgesi belge,
        SatisBelgesiMuhasebeFisContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (belge.Satirlar.Count == 0)
            throw new BaseException("Alış iade belgesinde aktif satır bulunamadı.", 400);

        var aktifSatirlar = belge.Satirlar
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.SiraNo)
            .ToList();

        if (!context.KdvHesapPlaniId.HasValue)
            throw new BaseException("Alış iade faturası için 191 İndirilecek KDV hesabı bulunamadı.", 400);

        var satirlar = new List<MuhasebeFisSatiriTaslak>();
        var siraNo = 1;

        foreach (var belgeSatiri in aktifSatirlar)
        {
            if (belgeSatiri.Matrah <= 0)
                continue;

            if (belgeSatiri.SatirTipi == SatisBelgesiSatirTipi.Iade)
                throw new BaseException("İade satırları alış iade faturalarında desteklenmiyor.", 400);

            if (belgeSatiri.SatirTipi == SatisBelgesiSatirTipi.Urun && !belgeSatiri.TasinirKartId.HasValue)
                throw new BaseException($"Alış iade faturası stok satırı için taşınır kart seçilmelidir. Satır: {belgeSatiri.SiraNo}", 400);

            var hesap = await ResolveSatirHesabiAsync(belge, belgeSatiri, context, cancellationToken);
            satirlar.Add(new MuhasebeFisSatiriTaslak
            {
                MuhasebeHesapPlaniId = hesap.Id,
                SiraNo = siraNo++,
                Borc = 0,
                Alacak = belgeSatiri.Matrah,
                Aciklama = BuildSatirAciklama(belgeSatiri, belge.BelgeNo),
                TasinirKartId = belgeSatiri.TasinirKartId,
                DepoId = belgeSatiri.DepoId
            });
        }

        satirlar.Add(new MuhasebeFisSatiriTaslak
        {
            MuhasebeHesapPlaniId = context.CariHesapPlaniId,
            SiraNo = siraNo++,
            Borc = belge.GenelToplam,
            Alacak = 0,
            Aciklama = $"Satıcı borcu iadesi - {belge.BelgeNo}"
        });

        if (belge.ToplamKdv > 0)
        {
            satirlar.Add(new MuhasebeFisSatiriTaslak
            {
                MuhasebeHesapPlaniId = context.KdvHesapPlaniId.Value,
                SiraNo = siraNo,
                Borc = 0,
                Alacak = belge.ToplamKdv,
                Aciklama = $"İndirilecek KDV iadesi - {belge.BelgeNo}"
            });
        }

        var toplamBorc = satirlar.Sum(x => x.Borc);
        var toplamAlacak = satirlar.Sum(x => x.Alacak);

        if (Math.Abs(toplamBorc - toplamAlacak) > 0.01m)
            throw new BaseException(
                $"Alış iade fiş borç/alacak dengesi sağlanamadı. Borç: {toplamBorc:N2}, Alacak: {toplamAlacak:N2}",
                400);

        return satirlar;
    }

    private async Task<MuhasebeHesapPlani> ResolveSatirHesabiAsync(
        SatisBelgesi belge,
        SatisBelgesiSatiri belgeSatiri,
        SatisBelgesiMuhasebeFisContext context,
        CancellationToken cancellationToken)
    {
        if (belgeSatiri.TasinirKartId.HasValue)
        {
            var kart = await _dbContext.TasinirKartlar
                .Include(x => x.TasinirKod)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == belgeSatiri.TasinirKartId.Value && !x.IsDeleted, cancellationToken)
                ?? throw new BaseException($"Alış iade faturası stok satırı için taşınır kart bulunamadı. Satır: {belgeSatiri.SiraNo}", 400);

            if (kart.TesisId.HasValue && kart.TesisId != belge.TesisId)
                throw new BaseException($"Alış iade faturası stok satırı için seçilen taşınır kart bu tesisle uyumlu değil. Satır: {belgeSatiri.SiraNo}", 400);

            if (kart.MuhasebeHesapPlaniId.HasValue)
            {
                return await ResolveHesapByIdAsync(
                    kart.MuhasebeHesapPlaniId.Value,
                    belge.TesisId!.Value,
                    $"Alış iade faturası stok satırı için 153 Ticari Mallar hesabı bulunamadı. Satır: {belgeSatiri.SiraNo}",
                    cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(kart.AnaMuhasebeHesapKodu))
            {
                return await ResolveHesapByAnaKodAsync(
                    kart.AnaMuhasebeHesapKodu,
                    belge.TesisId!.Value,
                    cancellationToken);
            }

            if (kart.TasinirKodId > 0)
            {
                var esleme = await _tasinirKodMuhasebeHesapEslemeService.GetVarsayilanAsync(
                    kart.TasinirKodId,
                    kart.MalzemeTipi,
                    "Cikis",
                    cancellationToken);

                if (esleme is not null)
                {
                    return await ResolveHesapByIdAsync(
                        esleme.MuhasebeHesapPlaniId,
                        belge.TesisId!.Value,
                        $"Alış iade faturası stok satırı için 153 Ticari Mallar hesabı bulunamadı. Satır: {belgeSatiri.SiraNo}",
                        cancellationToken);
                }
            }

            if (!context.StokHesapPlaniId.HasValue)
                throw new BaseException($"Alış iade faturası stok satırı için 153 Ticari Mallar hesabı bulunamadı. Satır: {belgeSatiri.SiraNo}", 400);

            return await ResolveHesapByIdAsync(
                context.StokHesapPlaniId.Value,
                belge.TesisId!.Value,
                $"Alış iade faturası stok satırı için 153 Ticari Mallar hesabı bulunamadı. Satır: {belgeSatiri.SiraNo}",
                cancellationToken);
        }

        var giderHesapId = context.HizmetGiderHesapPlaniId
            ?? await ResolveHizmetGiderHesabiAsync(belge.TesisId!.Value, cancellationToken);

        return await ResolveHesapByIdAsync(
            giderHesapId,
            belge.TesisId!.Value,
            $"Alış iade faturası hizmet satırı için gider hesabı bulunamadı. Satır: {belgeSatiri.SiraNo}",
            cancellationToken);
    }

    private async Task<int> ResolveHizmetGiderHesabiAsync(int tesisId, CancellationToken cancellationToken)
    {
        try
        {
            return (await ResolveHesapByAnaKodAsync(MuhasebeAnaHesapKodlari.GiderHizmetMaliyet, tesisId, cancellationToken)).Id;
        }
        catch (BaseException)
        {
            try
            {
                return (await ResolveHesapByAnaKodAsync(MuhasebeAnaHesapKodlari.GiderGenelYonetim, tesisId, cancellationToken)).Id;
            }
            catch (BaseException)
            {
                throw new BaseException("Alış iade faturası hizmet satırı için gider hesabı bulunamadı.", 400);
            }
        }
    }

    private async Task<MuhasebeHesapPlani> ResolveHesapByIdAsync(
        int hesapId,
        int tesisId,
        string hataMesaji,
        CancellationToken cancellationToken)
    {
        var hesap = await _dbContext.MuhasebeHesapPlanlari
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == hesapId &&
                !x.IsDeleted &&
                x.AktifMi &&
                x.HareketGorebilirMi &&
                x.DetayHesapMi &&
                (x.TesisId == tesisId || x.TesisId == null), cancellationToken);

        if (hesap is null)
            throw new BaseException(hataMesaji, 400);

        return hesap;
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
            throw new BaseException($"Alış iade faturası için {anaKod} hesabı bulunamadı.", 400);

        return hesap;
    }

    private static string BuildSatirAciklama(SatisBelgesiSatiri satir, string belgeNo)
    {
        var tip = satir.SatirTipi == SatisBelgesiSatirTipi.Urun
            ? "mal"
            : "hizmet";

        if (!string.IsNullOrWhiteSpace(satir.Aciklama))
            return $"Alış iade {tip} bedeli - {belgeNo} / {satir.Aciklama}";

        return $"Alış iade {tip} bedeli - {belgeNo}";
    }

    private static bool HasTevkifatliSatir(SatisBelgesi belge)
        => belge.Satirlar?.Any(s =>
               !s.IsDeleted &&
               s.KdvUygulamaTipi == KdvUygulamaTipi.Tevkifatli) == true;
}
