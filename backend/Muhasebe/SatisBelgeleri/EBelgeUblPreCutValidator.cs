using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Enums;

namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>Bir aktif satırın kesim öncesi UBL kapısı için gerekli, saf (EF'siz) alanları.</summary>
public sealed record EBelgeUblPreCutSatirContext(
    string Birim,
    KdvUygulamaTipi KdvUygulamaTipi,
    string? KdvIstisnaKodu,
    int? TevkifatPay,
    int? TevkifatPayda,
    decimal TevkifatTutari,
    decimal OtvTutari,
    decimal OivTutari,
    decimal KonaklamaVergisiTutari,
    decimal Matrah,
    decimal KdvTutari,
    decimal SatirToplami);

/// <summary>
/// Kesim öncesi UBL hazırlık kapısının tüm girdisi. Saf veri taşır (EF entity referansı yok) -
/// böylece <see cref="IEBelgeUblPreCutValidator"/> veritabanından tamamen bağımsız, izole
/// birim testlerle doğrulanabilir.
/// </summary>
public sealed record EBelgeUblPreCutContext(
    bool FeatureEnabled,
    DateTime BelgeTarihi,
    DateTime PlanlananKesimTarihiTrt,
    EBelgeKanali EBelgeKanali,
    SatisBelgesiTipi BelgeTipi,
    string ParaBirimi,
    decimal Kur,
    bool IadeSenaryosuVarMi,
    bool AliciKurumsalMi,
    string? AliciUnvan,
    string? AliciVergiNo,
    string? AliciAd,
    string? AliciSoyad,
    string? AliciTcKimlikNo,
    string? SaticiIlce,
    string? SaticiIl,
    string? AliciIlce,
    string? AliciIl,
    IReadOnlyList<EBelgeUblPreCutSatirContext> AktifSatirlar,
    decimal ToplamMatrah,
    decimal ToplamKdv,
    decimal GenelToplam);

/// <summary>
/// Kesim öncesi UBL hazırlık kapısı — resmî numara verilmeden, sayaç kilitlenmeden ÖNCE
/// çağrılmalıdır (bkz. SatisBelgesiService.FaturaKesAsync). Yalnız doğrulama yapar; hiçbir
/// entity değiştirmez, hiçbir yan etki üretmez.
/// </summary>
public interface IEBelgeUblPreCutValidator
{
    /// <summary>Kural ihlalinde ilgili tipe özgü BaseException fırlatır; tüm kurallar geçerse sessizce döner.</summary>
    void Validate(EBelgeUblPreCutContext context);
}

/// <summary>
/// Bkz. görev sonuç raporu, "Tam kesim öncesi UBL hazırlık kapısı". Kurallar sırayla (1'den 18'e,
/// artı otoriter alıcı/satıcı kimlik ve adres kontrolleri) uygulanır; ilk ihlalde durur.
/// </summary>
public sealed class EBelgeUblPreCutValidator : IEBelgeUblPreCutValidator
{
    public void Validate(EBelgeUblPreCutContext context)
    {
        // 1. Feature flag açık olmalı.
        if (!context.FeatureEnabled)
        {
            throw new EBelgeUblFeatureDisabledException();
        }

        // 2. Belge tarihi ve planlanan TRT kesim tarihi 14.09.2026 veya sonrası olmalı.
        if (context.BelgeTarihi.Date < EBelgeUblGoLive.Trt || context.PlanlananKesimTarihiTrt.Date < EBelgeUblGoLive.Trt)
        {
            throw new EBelgeInvoiceDateBeforeGoLiveException(
                context.BelgeTarihi.Date, context.PlanlananKesimTarihiTrt.Date, EBelgeUblGoLive.Trt);
        }

        // 3. Kanal yalnız e-Arşiv olmalı (kesin kapsam kararı — e-Fatura bu dalgada desteklenmez).
        if (context.EBelgeKanali != EBelgeKanali.EArsiv)
        {
            throw new EBelgeUblScopeUnsupportedException(
                $"İlk dalgada yalnız e-Arşiv kanalı desteklenir. (Kanal: {context.EBelgeKanali})");
        }

        // 4. Belge tipi yalnız SatisFaturasi olmalı.
        if (context.BelgeTipi != SatisBelgesiTipi.SatisFaturasi)
        {
            throw new EBelgeUblScopeUnsupportedException(
                $"İlk dalgada yalnız SatisFaturasi desteklenir. (BelgeTipi: {context.BelgeTipi})");
        }

        // 5. Para birimi yalnız TRY olmalı.
        if (!string.Equals(context.ParaBirimi, "TRY", StringComparison.Ordinal))
        {
            throw new EBelgeUblScopeUnsupportedException(
                $"İlk dalgada yalnız TRY para birimi desteklenir. (ParaBirimi: {context.ParaBirimi})");
        }

        // 6. Kur yalnız 1 olmalı.
        if (context.Kur != 1m)
        {
            throw new EBelgeUblScopeUnsupportedException(
                $"İlk dalgada yalnız kur 1 desteklenir. (Kur: {context.Kur})");
        }

        // 7 ve 8. ProfileID=EARSIVFATURA / InvoiceTypeCode=SATIS: kanal (kural 3) ve belge tipi
        // (kural 4) zaten doğrulandığı için bu değerler deterministik üretilebilir durumdadır;
        // ayrı bir hata yolu gerekmez (bkz. EBelgeSnapshotFactory.CreateSnapshotV2).

        // Otoriter alıcı/satıcı kimlik ve yapısal adres alanları (bkz. görev md.3) - resmî numara
        // verilmeden önce eksikse reddedilir. Sıralama: satıcı yapısal adres, alıcı kimlik, alıcı
        // yapısal adres.
        if (string.IsNullOrWhiteSpace(context.SaticiIlce) || string.IsNullOrWhiteSpace(context.SaticiIl))
        {
            throw new EBelgeUblAuthoritativeFieldMissingException(
                "Belgenin düzenleyen kurumunda yapısal adres (ilçe/il) bulunmalıdır.");
        }

        if (context.AliciKurumsalMi)
        {
            if (string.IsNullOrWhiteSpace(context.AliciUnvan) || string.IsNullOrWhiteSpace(context.AliciVergiNo))
            {
                throw new EBelgeUblAuthoritativeFieldMissingException(
                    "Kurumsal alıcı için unvan ve VKN bulunmalıdır.");
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(context.AliciAd) ||
                string.IsNullOrWhiteSpace(context.AliciSoyad) ||
                string.IsNullOrWhiteSpace(context.AliciTcKimlikNo))
            {
                throw new EBelgeUblAuthoritativeFieldMissingException(
                    "Gerçek kişi alıcı için ayrı ad, ayrı soyad ve TCKN bulunmalıdır.");
            }
        }

        if (string.IsNullOrWhiteSpace(context.AliciIlce) || string.IsNullOrWhiteSpace(context.AliciIl))
        {
            throw new EBelgeUblAuthoritativeFieldMissingException(
                "Alıcı için yapısal adres (ilçe/il) bulunmalıdır.");
        }

        // 9. En az bir aktif satır olmalı.
        if (context.AktifSatirlar.Count == 0)
        {
            throw new EBelgeUblAuthoritativeFieldMissingException("En az bir aktif satır bulunmalıdır.");
        }

        foreach (var satir in context.AktifSatirlar)
        {
            // 10. Birim yalnız "Adet" olmalı.
            if (!string.Equals(satir.Birim, "Adet", StringComparison.Ordinal))
            {
                throw new EBelgeUblScopeUnsupportedException(
                    $"İlk dalgada yalnız 'Adet' birimi desteklenir. (Birim: {satir.Birim})");
            }

            // 11. KdvUygulamaTipi yalnız Kdvli olmalı.
            if (satir.KdvUygulamaTipi != KdvUygulamaTipi.Kdvli)
            {
                throw new EBelgeUblScopeUnsupportedException(
                    $"İlk dalgada yalnız standart KDV'li satırlar desteklenir. (KdvUygulamaTipi: {satir.KdvUygulamaTipi})");
            }

            // 12. Tevkifat yok.
            if (satir.TevkifatPay.HasValue || satir.TevkifatPayda.HasValue || satir.TevkifatTutari != 0m)
            {
                throw new EBelgeUblScopeUnsupportedException("İlk dalgada tevkifatlı satırlar desteklenmez.");
            }

            // 13. KDV istisnası yok.
            if (!string.IsNullOrWhiteSpace(satir.KdvIstisnaKodu))
            {
                throw new EBelgeUblScopeUnsupportedException("İlk dalgada KDV istisnalı satırlar desteklenmez.");
            }

            // 14. ÖTV yok.
            if (satir.OtvTutari != 0m)
            {
                throw new EBelgeUblScopeUnsupportedException("İlk dalgada ÖTV içeren satırlar desteklenmez.");
            }

            // 15. ÖİV yok.
            if (satir.OivTutari != 0m)
            {
                throw new EBelgeUblScopeUnsupportedException("İlk dalgada ÖİV içeren satırlar desteklenmez.");
            }

            // 16. Konaklama vergisi yok.
            if (satir.KonaklamaVergisiTutari != 0m)
            {
                throw new EBelgeUblScopeUnsupportedException("İlk dalgada konaklama vergisi içeren satırlar desteklenmez.");
            }
        }

        // 17. İade senaryosu yok.
        if (context.IadeSenaryosuVarMi)
        {
            throw new EBelgeUblScopeUnsupportedException("İlk dalgada iade senaryoları desteklenmez.");
        }

        // 18. Belge ve satır toplamları merkezî mali doğrulayıcıya göre tutarlı olmalı.
        var satirKatkilari = context.AktifSatirlar.Select(s =>
            new SatisBelgesiTutarHesaplayici.SatirTutarKatkisi(s.Matrah, s.KdvTutari, s.SatirToplami));

        var uyusmazliklar = SatisBelgesiTutarHesaplayici.DogrulaBelgeToplamlari(
            satirKatkilari, context.ToplamMatrah, context.ToplamKdv, context.GenelToplam);

        if (uyusmazliklar.Count > 0)
        {
            throw new EBelgeUblMonetaryTotalMismatchException(uyusmazliklar);
        }
    }
}
