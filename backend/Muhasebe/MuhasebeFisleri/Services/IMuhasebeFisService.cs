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
