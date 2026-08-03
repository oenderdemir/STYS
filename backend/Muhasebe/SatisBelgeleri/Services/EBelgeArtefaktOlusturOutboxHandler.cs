using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

public sealed record EBelgeArtefaktOlusturmaTalebi(int KurumId, int EBelgeKaydiId);

public enum EBelgeArtefaktOlusturmaSonucuTuru
{
    Basarili = 1,
    GeciciHata = 2,
    KaliciHata = 3
}

public sealed record EBelgeArtefaktOlusturmaSonucu
{
    public EBelgeArtefaktOlusturmaSonucuTuru SonucTuru { get; }

    public string? HataKodu { get; }

    public string? HataMesaji { get; }

    public bool BasariliMi => SonucTuru == EBelgeArtefaktOlusturmaSonucuTuru.Basarili;

    private EBelgeArtefaktOlusturmaSonucu(
        EBelgeArtefaktOlusturmaSonucuTuru sonucTuru,
        string? hataKodu,
        string? hataMesaji)
    {
        SonucTuru = sonucTuru;
        HataKodu = hataKodu;
        HataMesaji = hataMesaji;
    }

    public static EBelgeArtefaktOlusturmaSonucu Basarili()
        => new(EBelgeArtefaktOlusturmaSonucuTuru.Basarili, null, null);

    public static EBelgeArtefaktOlusturmaSonucu GeciciHata(string hataKodu, string hataMesaji)
    {
        EBelgeOutboxLeaseValidationHelper.ValidateHataAlanlari(hataKodu, hataMesaji);
        return new(EBelgeArtefaktOlusturmaSonucuTuru.GeciciHata, hataKodu, hataMesaji);
    }

    public static EBelgeArtefaktOlusturmaSonucu KaliciHata(string hataKodu, string hataMesaji)
    {
        EBelgeOutboxLeaseValidationHelper.ValidateHataAlanlari(hataKodu, hataMesaji);
        return new(EBelgeArtefaktOlusturmaSonucuTuru.KaliciHata, hataKodu, hataMesaji);
    }
}

public interface IEBelgeArtefaktOlusturmaService
{
    Task<EBelgeArtefaktOlusturmaSonucu?> OlusturAsync(
        EBelgeArtefaktOlusturmaTalebi talep,
        CancellationToken cancellationToken = default);
}

public sealed class EBelgeArtefaktOlusturOutboxHandler : IEBelgeOutboxIsTuruHandler
{
    private readonly IEBelgeArtefaktOlusturmaService _artefaktOlusturmaService;

    public EBelgeArtefaktOlusturOutboxHandler(IEBelgeArtefaktOlusturmaService artefaktOlusturmaService)
    {
        _artefaktOlusturmaService = artefaktOlusturmaService ?? throw new ArgumentNullException(nameof(artefaktOlusturmaService));
    }

    public EBelgeOutboxIsTuru IsTuru => EBelgeOutboxIsTuru.ArtefaktOlustur;

    public async Task<EBelgeOutboxHandlerSonucu> HandleAsync(
        EBelgeOutboxIslemBaglami baglam,
        CancellationToken cancellationToken = default)
    {
        if (baglam is null)
        {
            throw new BaseException("İşlem bağlamı boş olamaz.", 400);
        }

        if (baglam.IsTuru != EBelgeOutboxIsTuru.ArtefaktOlustur)
        {
            throw new BaseException("Bu handler yalnız ArtefaktOlustur iş türünü destekler.", 400);
        }

        var sonuc = await _artefaktOlusturmaService.OlusturAsync(
            new EBelgeArtefaktOlusturmaTalebi(baglam.KurumId, baglam.EBelgeKaydiId),
            cancellationToken);

        if (sonuc is null)
        {
            throw new InvalidOperationException("Artefakt oluşturma servisi null sonuç döndürdü.");
        }

        return sonuc.SonucTuru switch
        {
            EBelgeArtefaktOlusturmaSonucuTuru.Basarili
                => EBelgeOutboxHandlerSonucu.Basarili(),
            EBelgeArtefaktOlusturmaSonucuTuru.GeciciHata
                => EBelgeOutboxHandlerSonucu.Basarisiz(EBelgeOutboxHataSinifi.Gecici, sonuc.HataKodu!, sonuc.HataMesaji!),
            EBelgeArtefaktOlusturmaSonucuTuru.KaliciHata
                => EBelgeOutboxHandlerSonucu.Basarisiz(EBelgeOutboxHataSinifi.Kalici, sonuc.HataKodu!, sonuc.HataMesaji!),
            _ => throw new InvalidOperationException("Bilinmeyen artefakt sonucu.")
        };
    }
}
