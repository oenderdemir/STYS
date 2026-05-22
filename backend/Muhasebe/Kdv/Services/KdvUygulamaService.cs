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
        DateTime islemTarihi,
        KdvIslemYonu islemYonu,
        CancellationToken cancellationToken = default)
    {
        var result = new KdvUygulamaResult
        {
            KdvUygulamaTipi = kdvUygulamaTipi,
            KdvOrani = kdvOrani,
            KdvTutari = 0
        };

        // Enum geçerlilik kontrolü (Section 3)
        if (!Enum.IsDefined(typeof(KdvUygulamaTipi), kdvUygulamaTipi))
        {
            throw new BaseException("Geçersiz KDV uygulama tipi.", 400);
        }

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
                // Section 8: KDV'li işlemlerde istisna tanımı seçilemez
                if (kdvIstisnaTanimId.HasValue && kdvIstisnaTanimId.Value > 0)
                {
                    throw new BaseException("KDV'li işlemlerde KDV istisna tanımı seçilemez.", 400);
                }

                if (kdvOrani <= 0)
                {
                    throw new BaseException("KDV'li işlemlerde KDV oranı 0'dan büyük olmalıdır.", 400);
                }

                result.KdvTutari = Math.Round(tutar * kdvOrani / 100m, 2, MidpointRounding.AwayFromZero);
                break;

            case KdvUygulamaTipi.TamIstisna:
            case KdvUygulamaTipi.KismiIstisna:
            case KdvUygulamaTipi.KdvKapsamDisi:
                // Section 9: İstisnalı işlem validasyonu
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

                // Uygulama tipi eşleşmeli
                if ((int)tanim.UygulamaTipi != kdvUygulamaTipi)
                {
                    throw new BaseException("KDV istisna tanımının uygulama tipi ile seçilen uygulama tipi uyuşmuyor.", 400);
                }

                // Section 6: Satış/Alış kullanım kontrolü
                if (islemYonu == KdvIslemYonu.Satis && !tanim.SatisIslemlerindeKullanilirMi)
                {
                    throw new BaseException("Seçilen KDV istisna tanımı satış işlemlerinde kullanılamaz.", 400);
                }

                if (islemYonu == KdvIslemYonu.Alis && !tanim.AlisIslemlerindeKullanilirMi)
                {
                    throw new BaseException("Seçilen KDV istisna tanımı alış işlemlerinde kullanılamaz.", 400);
                }

                // Section 7: Geçerlilik tarihi kontrolü
                var islemDate = islemTarihi.Date;

                if (tanim.GecerlilikBaslangicTarihi.HasValue && islemDate < tanim.GecerlilikBaslangicTarihi.Value.Date)
                {
                    throw new BaseException(
                        $"KDV istisna tanımı ({tanim.Kod}) {tanim.GecerlilikBaslangicTarihi:dd.MM.yyyy} tarihinden önceki işlemlerde kullanılamaz.", 400);
                }

                if (tanim.GecerlilikBitisTarihi.HasValue && islemDate > tanim.GecerlilikBitisTarihi.Value.Date)
                {
                    throw new BaseException(
                        $"KDV istisna tanımı ({tanim.Kod}) {tanim.GecerlilikBitisTarihi:dd.MM.yyyy} tarihinden sonraki işlemlerde kullanılamaz.", 400);
                }

                result.KdvIstisnaTanimId = tanim.Id;
                result.KdvIstisnaKodu = tanim.Kod;
                result.KdvIstisnaAciklamasi = tanim.Ad;
                result.KdvOrani = 0;
                result.KdvTutari = 0;
                break;

            case KdvUygulamaTipi.Tevkifatli:
                // Section 10: Tevkifatlı henüz desteklenmiyor
                throw new BaseException("Tevkifatlı KDV uygulaması henüz desteklenmemektedir.", 400);

            default:
                throw new BaseException("Geçersiz KDV uygulama tipi.", 400);
        }

        return result;
    }
}
