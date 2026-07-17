using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.TevkifatHesapEslemeleri.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services.MuhasebeFisStratejileri;

public sealed class SatisTevkifatliFaturaMuhasebeFisStratejisi : ISatisBelgesiMuhasebeFisStratejisi
{
    private readonly ITevkifatHesapEslemeService _tevkifatHesapEslemeService;

    public SatisTevkifatliFaturaMuhasebeFisStratejisi(ITevkifatHesapEslemeService tevkifatHesapEslemeService)
    {
        _tevkifatHesapEslemeService = tevkifatHesapEslemeService;
    }

    public bool Destekler(SatisBelgesi belge)
        => belge.BelgeTipi is SatisBelgesiTipi.FaturaTaslagi or SatisBelgesiTipi.SatisFaturasi
           && HasTevkifatliSatir(belge);

    public async Task<IReadOnlyList<MuhasebeFisSatiriTaslak>> SatirlariOlusturAsync(
        SatisBelgesi belge,
        SatisBelgesiMuhasebeFisContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var aktifSatirlar = belge.Satirlar
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.SiraNo)
            .ToList();

        if (aktifSatirlar.Count == 0)
            throw new BaseException("Satış belgesinde aktif satır bulunamadı.", 400);

        var tevkifatliHamSatirlar = aktifSatirlar
            .Where(s => s.KdvUygulamaTipi == KdvUygulamaTipi.Tevkifatli)
            .ToList();

        if (tevkifatliHamSatirlar.Count == 0)
            throw new BaseException("Satış tevkifatlı belge satırı bulunamadı.", 400);

        if (tevkifatliHamSatirlar.Any(s => s.TevkifatPay.GetValueOrDefault() <= 0 || s.TevkifatPayda.GetValueOrDefault() <= 0))
            throw new BaseException("Geçersiz tevkifat oranı tespit edildi.", 400);

        var tevkifatliSatirlar = tevkifatliHamSatirlar;

        if (!context.KdvHesapPlaniId.HasValue)
            throw new BaseException("Satış KDV hesabı bulunamadı.", 400);

        var satirlar = new List<MuhasebeFisSatiriTaslak>
        {
            new()
            {
                MuhasebeHesapPlaniId = context.CariHesapPlaniId,
                SiraNo = 1,
                Borc = belge.GenelToplam,
                Alacak = 0,
                Aciklama = $"Satış alacağı - {belge.BelgeNo}",
                CariKartId = context.CariKartId
            }
        };

        var siraNo = 2;
        var toplamTevkifatTutari = 0m;

        var gruplanmisTevkifatlar = tevkifatliSatirlar
            .GroupBy(s => new { Pay = s.TevkifatPay ?? 0, Payda = s.TevkifatPayda ?? 0 })
            .OrderBy(g => g.Key.Pay)
            .ThenBy(g => g.Key.Payda)
            .ToList();

        foreach (var grup in gruplanmisTevkifatlar)
        {
            var pay = grup.Key.Pay;
            var payda = grup.Key.Payda;

            if (pay <= 0 || payda <= 0 || pay > payda)
                throw new BaseException($"Geçersiz tevkifat oranı: {pay}/{payda}.", 400);

            var esleme = await _tevkifatHesapEslemeService.GetAktifEslemeAsync(
                context.TesisId,
                TevkifatIslemYonleri.Satis,
                pay,
                payda,
                cancellationToken);

            if (esleme is null)
            {
                throw new BaseException(
                    $"Satış tevkifatlı faturalar için {pay}/{payda} oranında aktif tevkifat hesabı bulunamadı.",
                    400);
            }

            var grupTutari = grup.Sum(s => s.TevkifatTutari > 0
                ? s.TevkifatTutari
                : Math.Round((s.KdvTutari * pay) / payda, 2, MidpointRounding.AwayFromZero));

            if (grupTutari <= 0)
                throw new BaseException($"Satış tevkifat tutarı hesaplanamadı. Oran: {pay}/{payda}.", 400);

            toplamTevkifatTutari += grupTutari;

            satirlar.Add(new MuhasebeFisSatiriTaslak
            {
                MuhasebeHesapPlaniId = esleme.MuhasebeHesapPlaniId,
                SiraNo = siraNo++,
                Borc = grupTutari,
                Alacak = 0,
                Aciklama = $"Tevkifat karşılığı {pay}/{payda} - {belge.BelgeNo}"
            });
        }

        if (toplamTevkifatTutari <= 0)
            throw new BaseException("Satış tevkifat tutarı hesaplanamadı.", 400);

        satirlar.Add(new MuhasebeFisSatiriTaslak
        {
            MuhasebeHesapPlaniId = context.GelirHesapPlaniId,
            SiraNo = siraNo++,
            Borc = 0,
            Alacak = belge.ToplamMatrah,
            Aciklama = $"Satış geliri - {belge.BelgeNo}"
        });

        satirlar.Add(new MuhasebeFisSatiriTaslak
        {
            MuhasebeHesapPlaniId = context.KdvHesapPlaniId!.Value,
            SiraNo = siraNo,
            Borc = 0,
            Alacak = belge.ToplamKdv,
            Aciklama = $"Hesaplanan KDV - {belge.BelgeNo}"
        });

        var toplamBorc = satirlar.Sum(x => x.Borc);
        var toplamAlacak = satirlar.Sum(x => x.Alacak);

        if (Math.Abs(toplamBorc - toplamAlacak) > 0.01m)
            throw new BaseException(
                $"Satış tevkifatlı fiş borç/alacak dengesi sağlanamadı. Borç: {toplamBorc:N2}, Alacak: {toplamAlacak:N2}",
                400);

        return satirlar;
    }

    private static bool HasTevkifatliSatir(SatisBelgesi belge)
        => belge.Satirlar?.Any(s =>
               !s.IsDeleted &&
               s.KdvUygulamaTipi == KdvUygulamaTipi.Tevkifatli) == true;
}
