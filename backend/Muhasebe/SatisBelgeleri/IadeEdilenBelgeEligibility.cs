using System.Linq.Expressions;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;

namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// İade edilen belge uygunluk kriterlerinin TEK, SQL'e çevrilebilir (Expression) kaynağı.
/// Hem SatisBelgesiService.ValidateVeGetIadeEdilenBelgeAsync (tekil, otoriter mutasyon
/// doğrulaması - ayrıntılı/alan-bazlı hata mesajlarıyla) hem TicariBelgeLookupService'in
/// iade-adayları SORGUSU (liste/autocomplete) AYNI bu kriterleri kullanır - kural iki yerde
/// ayrı ayrı YENİDEN YAZILMAZ. Nihai mutasyon doğrulaması yine SatisBelgesiService'te otoriter
/// kalır (bu sınıf yalnızca PAYLAŞILAN, salt kriter/predicate sağlar - iş kuralı KARARINI
/// kendisi VERMEZ).
/// </summary>
public static class IadeEdilenBelgeEligibility
{
    /// <summary>
    /// İade satırlarının kümülatif miktar toplamına DAHİL EDİLEN OTORİTER muhasebe durumları
    /// (bkz. SatisBelgesiService.ValidateIadeSatirlariAsync) - bir belge en az MuhasebeDurumu=Onayda
    /// durumuna ulaşmadan başka bir iade belgesinin kotasını TÜKETMEZ. Reddedildi/IptalEdildi/
    /// soft-delete kapsam dışıdır. TicariBelgeLookupService'in kalan-iade-miktarı hesaplaması da
    /// (kaynak satır lookup) AYNI kümeyi kullanır.
    /// </summary>
    public static readonly IReadOnlySet<TicariBelgeMuhasebeDurumu> IadeKumulatifSayilanMuhasebeDurumlari =
        new HashSet<TicariBelgeMuhasebeDurumu>
        {
            TicariBelgeMuhasebeDurumu.Onayda,
            TicariBelgeMuhasebeDurumu.Onaylandi
        };

    public static SatisBelgesiTipi BeklenenAsilTip(SatisBelgesiTipi iadeTipi) =>
        iadeTipi == SatisBelgesiTipi.SatisIadeFaturasi ? SatisBelgesiTipi.SatisFaturasi : SatisBelgesiTipi.AlisFaturasi;

    /// <summary>Bağlı muhasebe fişinin durumu iade adayı için geçerli mi (Taslak/Onaylı) - bkz. SatisBelgesiService.ValidateMuhasebeFisDurumu.</summary>
    public static bool FisDurumuGecerliMi(string durum) =>
        durum == MuhasebeFisDurumlari.Taslak || durum == MuhasebeFisDurumlari.Onayli;

    /// <summary>
    /// SQL'e çevrilebilir (Expression) uygunluk kriteri - kurum/cari/tip/tarih/durum/alan-doluluk
    /// kontrollerinin TAMAMINI kapsar (bağlı muhasebe fişi kontrolü HARİÇ - o, ayrı bir Any()
    /// alt sorgusu gerektirir, bkz. FisUygunlukIfadesi).
    /// </summary>
    public static Expression<Func<SatisBelgesi, bool>> AlanBazliUygunlukIfadesi(
        SatisBelgesiTipi iadeTipi, int kurumId, int cariKartId, DateTime belgeTarihi, int? selfId)
    {
        var asilTip = BeklenenAsilTip(iadeTipi);
        var satisIadesi = iadeTipi == SatisBelgesiTipi.SatisIadeFaturasi;

        return x =>
            !x.IsDeleted
            && x.KurumId == kurumId
            && x.BelgeTipi == asilTip
            && x.CariKartId == cariKartId
            && x.BelgeTarihi <= belgeTarihi
            && (!selfId.HasValue || x.Id != selfId.Value)
            && (satisIadesi
                ? x.FaturalamaDurumu == TicariBelgeFaturalamaDurumu.Kesildi
                  && x.ResmiFaturaNo != null && x.ResmiFaturaNo != ""
                  && x.FaturaKesimTarihi != null
                : x.MuhasebeDurumu == TicariBelgeMuhasebeDurumu.Onaylandi
                  && x.KarsiTarafFaturaNo != null && x.KarsiTarafFaturaNo != "");
    }

    /// <summary>
    /// Bağlı muhasebe fişinin var/silinmemiş/geçerli-durumda olması kriteri - fişler
    /// tablosuna Any() alt sorgusuyla uygulanır (SQL'e çevrilebilir).
    /// </summary>
    public static Expression<Func<SatisBelgesi, bool>> FisUygunlukIfadesi(IQueryable<MuhasebeFis> fisler) =>
        x => x.MuhasebeFisId.HasValue
             && fisler.Any(f => f.Id == x.MuhasebeFisId!.Value && !f.IsDeleted
                 && (f.Durum == MuhasebeFisDurumlari.Taslak || f.Durum == MuhasebeFisDurumlari.Onayli));
}
