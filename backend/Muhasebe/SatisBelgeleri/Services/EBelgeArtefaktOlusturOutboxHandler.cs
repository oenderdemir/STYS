using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// Claim ile gelen lease bilgisini (KilitToken/KilitBitisZamaniUtc) taşır - artefakt işleme
/// servisinin, sonucu KENDİ atomik transaction'ında kaydetmeden ÖNCE ownership'i YENİDEN
/// doğrulaması için gereklidir (bkz. Faz 2B.6.1 görev md.1-2). Token LOGLANMAZ.
/// </summary>
public sealed record EBelgeArtefaktOlusturmaTalebi(
    int KurumId,
    int EBelgeKaydiId,
    int OutboxMesajiId,
    string KilitToken,
    DateTime KilitBitisZamaniUtc);

/// <summary>
/// Artefakt oluşturma servisinin sonucu - HER değer artık (SahiplikKaybedildi hariç) servisin
/// KENDİ atomik transaction'ında ilgili DB geçişini TAMAMLADIĞINI ifade eder (bkz. Faz 2B.6.1):
/// <see cref="AtomikBasarili"/> ve <see cref="AtomikKaliciHata"/>, outbox'ı KENDİ İÇİNDE
/// Tamamlandi/terminal Hata'ya geçirmiştir - çağıran (handler/IsleAsync) İKİNCİ bir DB geçişi
/// YAPMAMALIDIR. <see cref="GeciciHata"/> İSTİSNADIR - geçici hatalarda EBelgeKaydi
/// değişmediğinden mevcut (ayrı, retry-policy'li) TryFailAsync akışı KORUNUR.
/// </summary>
public enum EBelgeArtefaktOlusturmaSonucuTuru
{
    AtomikBasarili = 1,
    GeciciHata = 2,
    AtomikKaliciHata = 3,
    SahiplikKaybedildi = 4
}

public sealed record EBelgeArtefaktOlusturmaSonucu
{
    public EBelgeArtefaktOlusturmaSonucuTuru SonucTuru { get; }

    public string? HataKodu { get; }

    public string? HataMesaji { get; }

    public bool BasariliMi => SonucTuru == EBelgeArtefaktOlusturmaSonucuTuru.AtomikBasarili;

    private EBelgeArtefaktOlusturmaSonucu(
        EBelgeArtefaktOlusturmaSonucuTuru sonucTuru,
        string? hataKodu,
        string? hataMesaji)
    {
        SonucTuru = sonucTuru;
        HataKodu = hataKodu;
        HataMesaji = hataMesaji;
    }

    public static EBelgeArtefaktOlusturmaSonucu AtomikBasarili()
        => new(EBelgeArtefaktOlusturmaSonucuTuru.AtomikBasarili, null, null);

    public static EBelgeArtefaktOlusturmaSonucu GeciciHata(string hataKodu, string hataMesaji)
    {
        EBelgeOutboxLeaseValidationHelper.ValidateHataAlanlari(hataKodu, hataMesaji);
        return new(EBelgeArtefaktOlusturmaSonucuTuru.GeciciHata, hataKodu, hataMesaji);
    }

    public static EBelgeArtefaktOlusturmaSonucu AtomikKaliciHata(string hataKodu, string hataMesaji)
    {
        EBelgeOutboxLeaseValidationHelper.ValidateHataAlanlari(hataKodu, hataMesaji);
        return new(EBelgeArtefaktOlusturmaSonucuTuru.AtomikKaliciHata, hataKodu, hataMesaji);
    }

    public static EBelgeArtefaktOlusturmaSonucu SahiplikKaybedildi()
        => new(EBelgeArtefaktOlusturmaSonucuTuru.SahiplikKaybedildi, null, null);
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

        // Gerçek lease token'ı (ve bitiş zamanı) aynen aktarılır - loglanmaz.
        var sonuc = await _artefaktOlusturmaService.OlusturAsync(
            new EBelgeArtefaktOlusturmaTalebi(
                baglam.KurumId,
                baglam.EBelgeKaydiId,
                baglam.OutboxMesajiId,
                baglam.KilitToken,
                baglam.KilitBitisZamaniUtc),
            cancellationToken);

        if (sonuc is null)
        {
            throw new InvalidOperationException("Artefakt oluşturma servisi null sonuç döndürdü.");
        }

        return sonuc.SonucTuru switch
        {
            EBelgeArtefaktOlusturmaSonucuTuru.AtomikBasarili
                => EBelgeOutboxHandlerSonucu.AtomikTamamlandi(),
            EBelgeArtefaktOlusturmaSonucuTuru.GeciciHata
                => EBelgeOutboxHandlerSonucu.Basarisiz(EBelgeOutboxHataSinifi.Gecici, sonuc.HataKodu!, sonuc.HataMesaji!),
            EBelgeArtefaktOlusturmaSonucuTuru.AtomikKaliciHata
                => EBelgeOutboxHandlerSonucu.AtomikTerminalHata(),
            EBelgeArtefaktOlusturmaSonucuTuru.SahiplikKaybedildi
                => EBelgeOutboxHandlerSonucu.SahiplikKaybedildi(),
            _ => throw new InvalidOperationException("Bilinmeyen artefakt sonucu.")
        };
    }
}
