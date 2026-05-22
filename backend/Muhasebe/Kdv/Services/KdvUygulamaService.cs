using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.Kdv.Repositories;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.Kdv.Services;

public class KdvUygulamaService : IKdvUygulamaService
{
    private readonly IKdvIstisnaTanimRepository _kdvIstisnaTanimRepository;

    public KdvUygulamaService(IKdvIstisnaTanimRepository kdvIstisnaTanimRepository)
    {
        _kdvIstisnaTanimRepository = kdvIstisnaTanimRepository;
    }

    public async Task<KdvUygulamaResult> ValidateAndSnapshotAsync(
        int kdvUygulamaTipi,
        int? kdvIstisnaTanimId,
        decimal kdvOrani,
        decimal tutar,
        CancellationToken cancellationToken = default)
    {
        var result = new KdvUygulamaResult
        {
            KdvUygulamaTipi = kdvUygulamaTipi,
            KdvOrani = kdvOrani,
            KdvTutari = 0
        };

        if (kdvOrani < 0 || kdvOrani > 100)
        {
            throw new BaseException("KDV oranı 0-100 arasında olmalıdır.", 400);
        }

        if (tutar < 0)
        {
            throw new BaseException("Tutar negatif olamaz.", 400);
        }

        var tip = (KdvUygulamaTipi)kdvUygulamaTipi;

        switch (tip)
        {
            case KdvUygulamaTipi.Kdvli:
                // KDV oranı > 0 olmalı
                if (kdvOrani <= 0)
                {
                    throw new BaseException("KDV'li işlemlerde KDV oranı 0'dan büyük olmalıdır.", 400);
                }
                result.KdvTutari = Math.Round(tutar * kdvOrani / 100m, 2, MidpointRounding.AwayFromZero);
                break;

            case KdvUygulamaTipi.TamIstisna:
            case KdvUygulamaTipi.KismiIstisna:
            case KdvUygulamaTipi.KdvKapsamDisi:
                // İstisna tanımı zorunlu
                if (!kdvIstisnaTanimId.HasValue || kdvIstisnaTanimId.Value <= 0)
                {
                    throw new BaseException("İstisna/Kapsam dışı işlemlerde KDV istisna tanımı seçilmesi zorunludur.", 400);
                }

                var tanim = await _kdvIstisnaTanimRepository.GetByIdAsync(kdvIstisnaTanimId.Value);
                if (tanim is null)
                {
                    throw new BaseException("Seçilen KDV istisna tanımı bulunamadı.", 400);
                }

                if (!tanim.AktifMi)
                {
                    throw new BaseException("Seçilen KDV istisna tanımı pasif durumda.", 400);
                }

                // İstisna tanımının UygulamaTipi ile gelen tip uyuşmalı
                if ((int)tanim.UygulamaTipi != kdvUygulamaTipi)
                {
                    throw new BaseException("KDV istisna tanımının uygulama tipi ile seçilen uygulama tipi uyuşmuyor.", 400);
                }

                result.KdvIstisnaTanimId = tanim.Id;
                result.KdvIstisnaKodu = tanim.Kod;
                result.KdvIstisnaAciklamasi = tanim.Ad;
                result.KdvOrani = 0;
                result.KdvTutari = 0;
                break;

            case KdvUygulamaTipi.Tevkifatli:
                // Tevkifatlı: şimdilik basit, ileride detaylandırılacak
                if (kdvOrani <= 0)
                {
                    throw new BaseException("Tevkifatlı işlemlerde KDV oranı 0'dan büyük olmalıdır.", 400);
                }
                result.KdvTutari = Math.Round(tutar * kdvOrani / 100m, 2, MidpointRounding.AwayFromZero);
                // Tevkifat tanımı şimdilik zorunlu değil, ileride eklenecek
                break;

            default:
                throw new BaseException("Geçersiz KDV uygulama tipi.", 400);
        }

        return result;
    }
}
