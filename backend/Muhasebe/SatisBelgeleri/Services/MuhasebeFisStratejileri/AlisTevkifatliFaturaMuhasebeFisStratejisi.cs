using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.TevkifatHesapEslemeleri.Services;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services.MuhasebeFisStratejileri;

public sealed class AlisTevkifatliFaturaMuhasebeFisStratejisi : ISatisBelgesiMuhasebeFisStratejisi
{
    private readonly StysAppDbContext _dbContext;
    private readonly ITasinirKodMuhasebeHesapEslemeService _tasinirKodMuhasebeHesapEslemeService;
    private readonly ITevkifatHesapEslemeService _tevkifatHesapEslemeService;

    public AlisTevkifatliFaturaMuhasebeFisStratejisi(
        StysAppDbContext dbContext,
        ITasinirKodMuhasebeHesapEslemeService tasinirKodMuhasebeHesapEslemeService,
        ITevkifatHesapEslemeService tevkifatHesapEslemeService)
    {
        _dbContext = dbContext;
        _tasinirKodMuhasebeHesapEslemeService = tasinirKodMuhasebeHesapEslemeService;
        _tevkifatHesapEslemeService = tevkifatHesapEslemeService;
    }

    public bool Destekler(SatisBelgesi belge)
        => belge.BelgeTipi == SatisBelgesiTipi.AlisFaturasi
           && HasTevkifatliSatir(belge);

    public async Task<IReadOnlyList<MuhasebeFisSatiriTaslak>> SatirlariOlusturAsync(
        SatisBelgesi belge,
        SatisBelgesiMuhasebeFisContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (belge.Satirlar.Count == 0)
            throw new BaseException("Alış belgesinde aktif satır bulunamadı.", 400);

        var aktifSatirlar = belge.Satirlar
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.SiraNo)
            .ToList();

        var tevkifatliSatirlar = aktifSatirlar
            .Where(x => x.KdvUygulamaTipi == KdvUygulamaTipi.Tevkifatli)
            .ToList();

        if (tevkifatliSatirlar.Count == 0)
            throw new BaseException("Alış tevkifatlı belge satırı bulunamadı.", 400);

        if (tevkifatliSatirlar.Any(s => s.TevkifatPay.GetValueOrDefault() <= 0 || s.TevkifatPayda.GetValueOrDefault() <= 0))
            throw new BaseException("Geçersiz tevkifat oranı tespit edildi.", 400);

        if (!context.KdvHesapPlaniId.HasValue)
            throw new BaseException("Alış faturası için 191 İndirilecek KDV hesabı bulunamadı.", 400);

        var satirlar = new List<MuhasebeFisSatiriTaslak>();
        var siraNo = 1;

        foreach (var belgeSatiri in aktifSatirlar)
        {
            if (belgeSatiri.Matrah <= 0)
                continue;

            if (belgeSatiri.SatirTipi == SatisBelgesiSatirTipi.Iade)
                throw new BaseException("İade satırları alış faturalarında desteklenmiyor.", 400);

            if (belgeSatiri.SatirTipi == SatisBelgesiSatirTipi.Urun && !belgeSatiri.TasinirKartId.HasValue)
                throw new BaseException($"Alış faturası stok satırı için taşınır kart seçilmelidir. Satır: {belgeSatiri.SiraNo}", 400);

            var hesap = await ResolveSatirHesabiAsync(belge, belgeSatiri, context, cancellationToken);
            satirlar.Add(new MuhasebeFisSatiriTaslak
            {
                MuhasebeHesapPlaniId = hesap.Id,
                SiraNo = siraNo++,
                Borc = belgeSatiri.Matrah,
                Alacak = 0,
                Aciklama = BuildSatirAciklama(belgeSatiri, belge.BelgeNo),
                TasinirKartId = belgeSatiri.TasinirKartId,
                DepoId = belgeSatiri.DepoId
            });
        }

        satirlar.Add(new MuhasebeFisSatiriTaslak
        {
            MuhasebeHesapPlaniId = context.KdvHesapPlaniId.Value,
            SiraNo = siraNo++,
            Borc = belge.ToplamKdv,
            Alacak = 0,
            Aciklama = $"İndirilecek KDV - {belge.BelgeNo}"
        });

        var gruplanmisTevkifatlar = tevkifatliSatirlar
            .GroupBy(s => new { Pay = s.TevkifatPay!.Value, Payda = s.TevkifatPayda!.Value })
            .OrderBy(g => g.Key.Pay)
            .ThenBy(g => g.Key.Payda)
            .ToList();

        var toplamTevkifatTutari = 0m;
        foreach (var grup in gruplanmisTevkifatlar)
        {
            var esleme = await _tevkifatHesapEslemeService.GetAktifEslemeAsync(
                context.TesisId,
                TevkifatIslemYonleri.Alis,
                grup.Key.Pay,
                grup.Key.Payda,
                cancellationToken);

            if (esleme is null)
            {
                throw new BaseException(
                    $"Alış tevkifatlı faturalar için {grup.Key.Pay}/{grup.Key.Payda} oranında aktif tevkifat hesabı bulunamadı.",
                    400);
            }

            var grupTutari = grup.Sum(s => s.TevkifatTutari > 0
                ? s.TevkifatTutari
                : Math.Round((s.KdvTutari * grup.Key.Pay) / grup.Key.Payda, 2, MidpointRounding.AwayFromZero));

            if (grupTutari <= 0)
                throw new BaseException($"Alış tevkifat tutarı hesaplanamadı. Oran: {grup.Key.Pay}/{grup.Key.Payda}.", 400);

            toplamTevkifatTutari += grupTutari;

            satirlar.Add(new MuhasebeFisSatiriTaslak
            {
                MuhasebeHesapPlaniId = esleme.MuhasebeHesapPlaniId,
                SiraNo = siraNo++,
                Borc = 0,
                Alacak = grupTutari,
                Aciklama = $"Tevkifat karşılığı {grup.Key.Pay}/{grup.Key.Payda} - {belge.BelgeNo}"
            });
        }

        if (toplamTevkifatTutari <= 0)
            throw new BaseException("Alış tevkifat tutarı hesaplanamadı.", 400);

        satirlar.Add(new MuhasebeFisSatiriTaslak
        {
            MuhasebeHesapPlaniId = context.CariHesapPlaniId,
            SiraNo = siraNo,
            Borc = 0,
            Alacak = belge.GenelToplam,
            Aciklama = $"Tedarikçi borcu - {belge.BelgeNo}"
        });

        var toplamBorc = satirlar.Sum(x => x.Borc);
        var toplamAlacak = satirlar.Sum(x => x.Alacak);

        if (Math.Abs(toplamBorc - toplamAlacak) > 0.01m)
            throw new BaseException(
                $"Alış tevkifatlı fiş borç/alacak dengesi sağlanamadı. Borç: {toplamBorc:N2}, Alacak: {toplamAlacak:N2}",
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
                ?? throw new BaseException($"Alış faturası stok satırı için taşınır kart bulunamadı. Satır: {belgeSatiri.SiraNo}", 400);

            if (kart.TesisId.HasValue && kart.TesisId != belge.TesisId)
                throw new BaseException($"Alış faturası stok satırı için seçilen taşınır kart bu tesisle uyumlu değil. Satır: {belgeSatiri.SiraNo}", 400);

            if (kart.MuhasebeHesapPlaniId.HasValue)
            {
                return await ResolveHesapByIdAsync(
                    kart.MuhasebeHesapPlaniId.Value,
                    belge.TesisId!.Value,
                    $"Alış faturası stok satırı için 153 Ticari Mallar hesabı bulunamadı. Satır: {belgeSatiri.SiraNo}",
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
                    "Giris",
                    cancellationToken);

                if (esleme is not null)
                {
                    return await ResolveHesapByIdAsync(
                        esleme.MuhasebeHesapPlaniId,
                        belge.TesisId!.Value,
                        $"Alış faturası stok satırı için 153 Ticari Mallar hesabı bulunamadı. Satır: {belgeSatiri.SiraNo}",
                        cancellationToken);
                }
            }

            if (!context.StokHesapPlaniId.HasValue)
                throw new BaseException($"Alış faturası stok satırı için 153 Ticari Mallar hesabı bulunamadı. Satır: {belgeSatiri.SiraNo}", 400);

            return await ResolveHesapByIdAsync(
                context.StokHesapPlaniId.Value,
                belge.TesisId!.Value,
                $"Alış faturası stok satırı için 153 Ticari Mallar hesabı bulunamadı. Satır: {belgeSatiri.SiraNo}",
                cancellationToken);
        }

        var giderHesapId = context.HizmetGiderHesapPlaniId
            ?? await ResolveHizmetGiderHesabiAsync(belge.TesisId!.Value, cancellationToken);

        return await ResolveHesapByIdAsync(
            giderHesapId,
            belge.TesisId!.Value,
            $"Alış faturası hizmet satırı için gider hesabı bulunamadı. Satır: {belgeSatiri.SiraNo}",
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
                throw new BaseException("Alış faturası hizmet satırı için gider hesabı bulunamadı.", 400);
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
            throw new BaseException($"Alış faturası için {anaKod} hesabı bulunamadı.", 400);

        return hesap;
    }

    private static string BuildSatirAciklama(SatisBelgesiSatiri satir, string belgeNo)
    {
        var tip = satir.SatirTipi == SatisBelgesiSatirTipi.Urun
            ? "mal"
            : "hizmet";

        if (!string.IsNullOrWhiteSpace(satir.Aciklama))
            return $"Alış faturası {tip} bedeli - {belgeNo} / {satir.Aciklama}";

        return $"Alış faturası {tip} bedeli - {belgeNo}";
    }

    private static bool HasTevkifatliSatir(SatisBelgesi belge)
        => belge.Satirlar?.Any(s =>
               !s.IsDeleted &&
               s.KdvUygulamaTipi == KdvUygulamaTipi.Tevkifatli) == true;
}
