using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// `UblImzala` outbox mesajlarını, GERÇEK <see cref="IEBelgeUblImzalamaService"/> aracılığıyla
/// işleyen handler (bkz. Faz 2B.7 görev md.17). <see cref="EBelgeArtefaktOlusturOutboxHandler"/>
/// ile AYNI type-safe dispatch desenini kullanır - AtomikBasarili/AtomikKaliciHata/
/// SahiplikKaybedildi sonuçları, outbox geçişini KENDİ transaction'ında ZATEN tamamlamış
/// sayılır (bkz. EBelgeOutboxHandlerSonucTuru.Atomik*), `EBelgeOutboxMesajIslemeService`
/// İKİNCİ bir DB geçişi YAPMAZ.
/// </summary>
public sealed class EBelgeUblImzalaOutboxHandler : IEBelgeOutboxIsTuruHandler
{
    private readonly IEBelgeUblImzalamaService _imzalamaService;

    public EBelgeUblImzalaOutboxHandler(IEBelgeUblImzalamaService imzalamaService)
    {
        _imzalamaService = imzalamaService ?? throw new ArgumentNullException(nameof(imzalamaService));
    }

    public EBelgeOutboxIsTuru IsTuru => EBelgeOutboxIsTuru.UblImzala;

    public async Task<EBelgeOutboxHandlerSonucu> HandleAsync(
        EBelgeOutboxIslemBaglami baglam,
        CancellationToken cancellationToken = default)
    {
        if (baglam is null)
        {
            throw new BaseException("İşlem bağlamı boş olamaz.", 400);
        }

        if (baglam.IsTuru != EBelgeOutboxIsTuru.UblImzala)
        {
            throw new BaseException("Bu handler yalnız UblImzala iş türünü destekler.", 400);
        }

        // Gerçek lease token'ı (ve bitiş zamanı) aynen aktarılır - loglanmaz.
        var sonuc = await _imzalamaService.ImzalaAsync(
            new EBelgeUblImzalamaTalebi(
                baglam.KurumId,
                baglam.EBelgeKaydiId,
                baglam.OutboxMesajiId,
                baglam.KilitToken,
                baglam.KilitBitisZamaniUtc),
            cancellationToken);

        if (sonuc is null)
        {
            throw new InvalidOperationException("İmzalama servisi null sonuç döndürdü.");
        }

        return sonuc.SonucTuru switch
        {
            EBelgeUblImzalamaSonucuTuru.AtomikBasarili
                => EBelgeOutboxHandlerSonucu.AtomikTamamlandi(),
            EBelgeUblImzalamaSonucuTuru.GeciciHata
                => EBelgeOutboxHandlerSonucu.Basarisiz(EBelgeOutboxHataSinifi.Gecici, sonuc.HataKodu!, sonuc.HataMesaji!),
            EBelgeUblImzalamaSonucuTuru.AtomikKaliciHata
                => EBelgeOutboxHandlerSonucu.AtomikTerminalHata(),
            EBelgeUblImzalamaSonucuTuru.SahiplikKaybedildi
                => EBelgeOutboxHandlerSonucu.SahiplikKaybedildi(),
            EBelgeUblImzalamaSonucuTuru.AtomikPolitikaBloklu
                => EBelgeOutboxHandlerSonucu.AtomikPolitikaBloklu(),
            _ => throw new InvalidOperationException("Bilinmeyen imzalama sonucu.")
        };
    }
}
