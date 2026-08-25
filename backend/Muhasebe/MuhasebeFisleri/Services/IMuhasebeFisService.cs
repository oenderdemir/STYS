using STYS.Muhasebe.MuhasebeFisleri.Dtos;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.MuhasebeFisleri.Services;

public interface IMuhasebeFisService : IBaseRdbmsService<MuhasebeFisDto, MuhasebeFis, int>
{
    Task<MuhasebeFisDto?> GetByIdWithSatirlarAsync(int id, CancellationToken cancellationToken = default);
    Task<List<MuhasebeFisDto>> GetByKaynakAsync(string kaynakModul, int kaynakId, CancellationToken cancellationToken = default);
    Task<MuhasebeFisDto> OnaylaAsync(int id, CancellationToken cancellationToken = default);
    Task<MuhasebeFisDto> IptalEtAsync(int id, string? aciklama = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// POS valor transfer fislerinin (KaynakModul=PosTahsilatValorTransferi) iptal/ters-kayit
    /// islemine ozel, dar bir metot. KaynakModul kontrolu metot icinde SABITTIR (parametre
    /// olarak alinmaz) - genel IptalEtAsync bu KaynakModul icin 409 doner, yalnizca bu metot
    /// PosTahsilatValorleri modulunun sunucu-ici servis kodundan cagrilir, disari acik bir HTTP
    /// endpoint'i yoktur. Ambient transaction'a katilir (kendi transaction'ini acmaz).
    /// Orijinal fis zaten Iptal ise IptalEdilenFisId iliskisiyle mevcut ters kaydi kilitli
    /// sekilde bulur (idempotent); bulamazsa veri tutarsizligi olarak MuhasebeFisTutarsizlikException
    /// firlatir.
    /// </summary>
    Task<MuhasebeFisIptalSonucDto> PosValorTransferFisiniIptalEtAsync(
        int muhasebeFisId, int beklenenKaynakId, int beklenenTesisId, string aciklama, CancellationToken cancellationToken = default);

    /// <summary>
    /// PosValorTransferFisiniIptalEtAsync ile olusturulmus bir ters kayit fisini "geri alir" -
    /// yani ters kaydin Borc/Alacak'ini TEKRAR ters cevirerek, orijinal transfer fisiyle AYNI net
    /// ekonomik etkiyi yeniden tesis eden YENI bir fis olusturur (orijinal transfer fisi VE ters
    /// kayit fisi asla degistirilmez/silinmez - denetim izi korunur). Yalnizca
    /// Durum=TersKayit olan bir fis uzerinde calisir; ters kaydin KENDISI zaten daha once geri
    /// alinmissa (TersKayitFisId doluysa) IptalEdilenFisId iliskisiyle mevcut geri alma fisini
    /// kilitli sekilde bulup idempotent doner. Ambient transaction'a katilir (kendi transaction'ini
    /// acmaz) - yalnizca PosTahsilatValorleri modulunun sunucu-ici servis kodundan cagrilir, disari
    /// acik bir HTTP endpoint'i yoktur.
    /// </summary>
    Task<MuhasebeFisIptalSonucDto> PosValorTransferFisiniGeriAlAsync(
        int tersKayitFisId, int beklenenKaynakId, int beklenenTesisId, string aciklama, CancellationToken cancellationToken = default);

    /// <summary>
    /// Satış/alış belgesi fişlerinin (KaynakModul=SatisBelgesi) iptal/ters-kayıt işlemine özel,
    /// dar bir metot - PosValorTransferFisiniIptalEtAsync ile AYNI desen. KaynakModul kontrolü
    /// metot içinde SABİTTİR (parametre olarak alınmaz) - genel IptalEtAsync bu KaynakModul için
    /// 409 döner, yalnızca bu metot SatisBelgesiService.IptalEtAsync tarafından çağrılır; dışarı
    /// açık bir HTTP endpoint'i yoktur. Ambient transaction'a katılır (kendi transaction'ını
    /// açmaz). Orijinal fiş zaten Iptal ise IptalEdilenFisId ilişkisiyle mevcut ters kaydı kilitli
    /// şekilde bulur (idempotent); bulamazsa veri tutarsızlığı olarak BaseException(500) fırlatır.
    /// </summary>
    Task<MuhasebeFisIptalSonucDto> SatisBelgesiFisiIptalEtAsync(
        int muhasebeFisId, int beklenenKaynakId, int beklenenTesisId, string aciklama, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kantin satış fişlerinin (KaynakModul=KantinSatis) iptal/ters-kayıt işlemine özel, dar bir
    /// metot — SatisBelgesiFisiIptalEtAsync ile AYNI desen. KaynakModul kontrolü metot içinde
    /// SABİTTİR; genel IptalEtAsync bu KaynakModul için 409 döner. Ambient transaction'a katılır.
    /// Orijinal fiş Onayli ise bir TersKayit üretip orijinali Iptal yapar; zaten Iptal ise mevcut
    /// TersKayit'i bulur (idempotent, ikinci ters kayıt üretilmez).
    /// </summary>
    Task<MuhasebeFisIptalSonucDto> KantinSatisFisiIptalEtAsync(
        int muhasebeFisId, int beklenenKaynakId, int beklenenTesisId, string aciklama, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kantin satış fişlerinin (KaynakModul=KantinSatis) TASLAK hâlinin kontrollü soft-delete'i.
    /// KaynakModul kontrolü SABİTTİR; genel DeleteAsync bu KaynakModul için 400 döner. Ambient
    /// transaction'a katılır; ters kayıt üretilmez.
    /// </summary>
    Task KantinSatisFisiniSilAsync(
        int muhasebeFisId, int beklenenKaynakId, int beklenenTesisId, CancellationToken cancellationToken = default);
    Task<List<MuhasebeFisDto>> GetFilteredAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default);
    Task<int> CountFilteredAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default);
    Task<YevmiyeDefteriDto> GetYevmiyeDefteriAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default);
    Task<byte[]> ExportYevmiyeDefteriExcelAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default);
    Task<MuavinDefterDto> GetMuavinDefterAsync(MuavinDefterFilterDto filter, CancellationToken cancellationToken = default);
    Task<byte[]> ExportMuavinDefterExcelAsync(MuavinDefterFilterDto filter, CancellationToken cancellationToken = default);
    Task<MizanDto> GetMizanAsync(MizanFilterDto filter, CancellationToken cancellationToken = default);
    Task<MizanDto> GetMizanBakiyeAsync(MizanFilterDto filter, CancellationToken cancellationToken = default);
    Task<byte[]> ExportMizanBakiyeExcelAsync(MizanFilterDto filter, CancellationToken cancellationToken = default);
    Task<MizanKarsilastirmaDto> KarsilastirMizanAsync(MizanFilterDto filter, CancellationToken cancellationToken = default);
    Task<TasinirMuhasebeFisiOlusturResultDto> TasinirMuhasebeFisiTaslagiOlusturAsync(
        TasinirMuhasebeFisiOlusturRequest request,
        CancellationToken cancellationToken = default);
}
