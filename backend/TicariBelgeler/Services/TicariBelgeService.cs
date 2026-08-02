using AutoMapper;
using STYS.AccessScope;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.TicariBelgeler.Dtos;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.TicariBelgeler.Services;

/// <inheritdoc cref="ITicariBelgeService" />
public class TicariBelgeService : ITicariBelgeService
{
    private readonly ISatisBelgesiService _satisBelgesiService;
    private readonly ISatisBelgesiTaslakOlusturmaService _taslakOlusturmaService;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly IMapper _mapper;

    public TicariBelgeService(
        ISatisBelgesiService satisBelgesiService,
        ISatisBelgesiTaslakOlusturmaService taslakOlusturmaService,
        IUserAccessScopeService userAccessScopeService,
        IMapper mapper)
    {
        _satisBelgesiService = satisBelgesiService;
        _taslakOlusturmaService = taslakOlusturmaService;
        _userAccessScopeService = userAccessScopeService;
        _mapper = mapper;
    }

    public async Task<TicariBelgeDetayDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var belge = await _satisBelgesiService.GetByIdAsync(id, cancellationToken);
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        EnsureTesisErisimKapsaminda(scope, belge.TesisId);
        return ToDetayDto(belge);
    }

    public async Task<List<TicariBelgeDto>> FilterAsync(TicariBelgeFilterDto filter, CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);

        // İstemci açıkça erişemediği bir TesisId isterse 403 - kapsam sessizce genişletilmez/daraltılmaz.
        if (scope.IsScoped && filter.TesisId.HasValue && !scope.TesisIds.Contains(filter.TesisId.Value))
        {
            throw new BaseException("Bu tesis için yetkiniz bulunmuyor.", 403);
        }

        var muhasebeFilter = _mapper.Map<SatisBelgesiFilterDto>(filter);

        // DAHİLİ erişim kapsamı - istemcinin JSON üzerinden belirleyebileceği bir alan DEĞİLDİR
        // (bkz. SatisBelgesiFilterDto.ErisimKapsamiTesisIdleri, [JsonIgnore]). Scope.TesisIds boşsa
        // (scoped ama hiçbir tesise erişimi yoksa) sonuç KESİNLİKLE boş döner - "filtre yok" OLARAK
        // yorumlanmaz; boş bir koleksiyon YİNE DE atanır (null bırakılmaz).
        if (scope.IsScoped)
        {
            muhasebeFilter.ErisimKapsamiTesisIdleri = scope.TesisIds;
        }

        // Tüm filtreler (kapsam dahil) SatisBelgesiService.FilterAsync içinde SQL sorgusunda
        // uygulanır - burada sonuç üzerinde İKİNCİ bir bellek içi süzme YAPILMAZ.
        var belgeler = await _satisBelgesiService.FilterAsync(muhasebeFilter, cancellationToken);
        return belgeler.Select(ToDto).ToList();
    }

    public async Task<TicariBelgeDetayDto> KaynaktanTaslakOlusturAsync(
        TicariBelgeTaslakOlusturRequest request, CancellationToken cancellationToken = default)
    {
        // Taslak oluşturma sırasında tesis yazma kontrolü BİLİNÇLİ OLARAK burada TEKRARLANMAZ -
        // mevcut ISatisBelgesiTaslakOlusturmaService/ISatisBelgesiService.CreateAsync akışındaki
        // kontrol KORUNUR (bkz. görev A.3).
        var muhasebeRequest = _mapper.Map<SatisBelgesiTaslakOlusturRequest>(request);
        var belge = await _taslakOlusturmaService.KaynaktanTaslakOlusturAsync(muhasebeRequest, cancellationToken);
        return ToDetayDto(belge);
    }

    public async Task<TicariBelgeDetayDto> UpdateAsync(
        int id, TicariBelgeGuncelleRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureBelgeErisimKapsamindaAsync(id, cancellationToken);
        var muhasebeRequest = _mapper.Map<UpdateSatisBelgesiRequest>(request);
        var belge = await _satisBelgesiService.UpdateAsync(id, muhasebeRequest, cancellationToken);
        return ToDetayDto(belge);
    }

    public async Task MuhasebeOnayinaGonderAsync(int id, CancellationToken cancellationToken = default)
    {
        await EnsureBelgeErisimKapsamindaAsync(id, cancellationToken);
        await _satisBelgesiService.MuhasebeOnayinaGonderAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await EnsureBelgeErisimKapsamindaAsync(id, cancellationToken);
        await _satisBelgesiService.DeleteAsync(id, cancellationToken);
    }

    public async Task IptalEtAsync(int id, CancellationToken cancellationToken = default)
    {
        await EnsureBelgeErisimKapsamindaAsync(id, cancellationToken);

        // Mali etkisi doğmuş (bağlı bir muhasebe fişi olan VE/VEYA muhasebece onaylanmış) bir
        // belge operasyon ekranından ASLA iptal edilemez (bkz. görev 2) - aksi halde fiş ters
        // kayda alınırken kaynak belgenin cari/stok hareketleri ve durumları güncellenmeden
        // kalabiliyordu. Bu belgeler yalnızca Muhasebe Satış/Alış Belgeleri ekranından
        // (MuhasebeSatisBelgeleriYonetimi.Manage) iptal edilebilir.
        var belge = await _satisBelgesiService.GetByIdAsync(id, cancellationToken);
        if (TicariBelgeIslemYetkisi.MaliEtkisiOlusmusMu(belge.MuhasebeDurumu, belge.MuhasebeFisId))
        {
            throw new BaseException(
                "Bu belge muhasebece onaylanmış ve/veya bağlı bir muhasebe fişine sahip; operasyon ekranından iptal edilemez. " +
                "İlgili işlem yalnızca Muhasebe Satış/Alış Belgeleri ekranından yapılabilir.",
                403);
        }

        await _satisBelgesiService.IptalEtAsync(id, cancellationToken);
    }

    // ──────────────────────────────────────────────
    //  Private — tesis erişim kapsamı (bkz. görev A)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Mutasyonlardan ÖNCE, façade seviyesinde de belge kapsamı doğrulaması yapar (alt muhasebe
    /// servisindeki mevcut güvenlik kontrolleri KALDIRILMAZ, bu KATMANLI/ek bir savunmadır).
    /// </summary>
    private async Task EnsureBelgeErisimKapsamindaAsync(int id, CancellationToken cancellationToken)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsScoped)
        {
            return;
        }

        var belge = await _satisBelgesiService.GetByIdAsync(id, cancellationToken);
        EnsureTesisErisimKapsaminda(scope, belge.TesisId);
    }

    private static void EnsureTesisErisimKapsaminda(DomainAccessScope scope, int? tesisId)
    {
        if (scope.IsScoped && (!tesisId.HasValue || !scope.TesisIds.Contains(tesisId.Value)))
        {
            throw new BaseException("Bu belge için yetkiniz bulunmuyor.", 403);
        }
    }

    // ──────────────────────────────────────────────
    //  Private — mapping + işlem yetkileri
    // ──────────────────────────────────────────────

    private TicariBelgeDto ToDto(SatisBelgesiDto belge)
    {
        var dto = _mapper.Map<TicariBelgeDto>(belge);
        UygulaIslemYetkileri(dto, belge.MuhasebeFisId);
        return dto;
    }

    private TicariBelgeDetayDto ToDetayDto(SatisBelgesiDto belge)
    {
        var dto = _mapper.Map<TicariBelgeDetayDto>(belge);
        UygulaIslemYetkileri(dto, belge.MuhasebeFisId);
        return dto;
    }

    /// <summary>
    /// İşlem yetenekleri ve operasyonel durum açıklaması TEK merkezi kaynaktan
    /// (TicariBelgeIslemYetkisi) türetilir - legacy Durum ASLA kullanılmaz. Fail-closed (bkz.
    /// görev C): üç otoriter alan SatisBelgesiDurumProjection.ProjeLegacyDurum tarafından GEÇERLİ
    /// bulunmadan (tanımsız/çelişkili kombinasyon) hiçbir işlem yeteneği hesaplanmaz - açık
    /// InvalidOperationException fırlatılır, yanıltıcı bir varsayılana (ör. tümü false) DÜŞÜLMEZ.
    ///
    /// <paramref name="muhasebeFisId"/>, kaynak SatisBelgesiDto'dan (TicariBelgeDto'nun KENDİSİ
    /// bu alanı BİLİNÇLİ OLARAK dışarı vermez, bkz. TicariBelgeDto XML doc'u) YALNIZCA
    /// OperasyonelIptalEdilebilirMi hesaplaması için ödünç alınır - dto'ya YAZILMAZ.
    /// </summary>
    private static void UygulaIslemYetkileri(TicariBelgeDto dto, int? muhasebeFisId)
    {
        SatisBelgesiDurumProjection.ProjeLegacyDurum(dto.TicariDurum, dto.MuhasebeDurumu, dto.FaturalamaDurumu);

        dto.GuncellenebilirMi = TicariBelgeIslemYetkisi.GuncellenebilirMi(dto.TicariDurum, dto.MuhasebeDurumu);
        dto.SilinebilirMi = TicariBelgeIslemYetkisi.SilinebilirMi(dto.TicariDurum, dto.MuhasebeDurumu, dto.FaturalamaDurumu);
        dto.MuhasebeOnayinaGonderilebilirMi = TicariBelgeIslemYetkisi.MuhasebeOnayinaGonderilebilirMi(dto.TicariDurum, dto.MuhasebeDurumu);
        dto.IptalEdilebilirMi = TicariBelgeIslemYetkisi.OperasyonelIptalEdilebilirMi(dto.TicariDurum, dto.MuhasebeDurumu, dto.FaturalamaDurumu, muhasebeFisId);
        dto.OperasyonelDurumAciklamasi = TicariBelgeIslemYetkisi.OperasyonelDurumAciklamasi(dto.TicariDurum, dto.MuhasebeDurumu, dto.FaturalamaDurumu);
    }
}
