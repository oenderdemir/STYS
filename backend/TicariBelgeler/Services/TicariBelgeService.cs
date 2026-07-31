using AutoMapper;
using STYS.Muhasebe.SatisBelgeleri;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.TicariBelgeler.Dtos;

namespace STYS.TicariBelgeler.Services;

/// <inheritdoc cref="ITicariBelgeService" />
public class TicariBelgeService : ITicariBelgeService
{
    private readonly ISatisBelgesiService _satisBelgesiService;
    private readonly ISatisBelgesiTaslakOlusturmaService _taslakOlusturmaService;
    private readonly IMapper _mapper;

    public TicariBelgeService(
        ISatisBelgesiService satisBelgesiService,
        ISatisBelgesiTaslakOlusturmaService taslakOlusturmaService,
        IMapper mapper)
    {
        _satisBelgesiService = satisBelgesiService;
        _taslakOlusturmaService = taslakOlusturmaService;
        _mapper = mapper;
    }

    public async Task<TicariBelgeDetayDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var belge = await _satisBelgesiService.GetByIdAsync(id, cancellationToken);
        return ToDetayDto(belge);
    }

    public async Task<List<TicariBelgeDto>> FilterAsync(TicariBelgeFilterDto filter, CancellationToken cancellationToken = default)
    {
        var muhasebeFilter = _mapper.Map<SatisBelgesiFilterDto>(filter);
        var belgeler = await _satisBelgesiService.FilterAsync(muhasebeFilter, cancellationToken);

        var sonuc = belgeler.Select(ToDto).ToList();

        // SatisBelgesiFilterDto yalnızca legacy Durum filtresini destekler - TicariDurum/
        // MuhasebeDurumu filtreleri (OTORİTER alanlar) burada, sonuç üzerinde uygulanır.
        if (filter.TicariDurum.HasValue)
        {
            sonuc = sonuc.Where(x => x.TicariDurum == filter.TicariDurum.Value).ToList();
        }

        if (filter.MuhasebeDurumu.HasValue)
        {
            sonuc = sonuc.Where(x => x.MuhasebeDurumu == filter.MuhasebeDurumu.Value).ToList();
        }

        return sonuc;
    }

    public async Task<TicariBelgeDetayDto> KaynaktanTaslakOlusturAsync(
        TicariBelgeTaslakOlusturRequest request, CancellationToken cancellationToken = default)
    {
        var muhasebeRequest = _mapper.Map<SatisBelgesiTaslakOlusturRequest>(request);
        var belge = await _taslakOlusturmaService.KaynaktanTaslakOlusturAsync(muhasebeRequest, cancellationToken);
        return ToDetayDto(belge);
    }

    public async Task<TicariBelgeDetayDto> UpdateAsync(
        int id, TicariBelgeGuncelleRequest request, CancellationToken cancellationToken = default)
    {
        var muhasebeRequest = _mapper.Map<UpdateSatisBelgesiRequest>(request);
        var belge = await _satisBelgesiService.UpdateAsync(id, muhasebeRequest, cancellationToken);
        return ToDetayDto(belge);
    }

    public Task MuhasebeOnayinaGonderAsync(int id, CancellationToken cancellationToken = default)
        => _satisBelgesiService.MuhasebeOnayinaGonderAsync(id, cancellationToken);

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        => _satisBelgesiService.DeleteAsync(id, cancellationToken);

    public Task IptalEtAsync(int id, CancellationToken cancellationToken = default)
        => _satisBelgesiService.IptalEtAsync(id, cancellationToken);

    // ──────────────────────────────────────────────
    //  Private — mapping + işlem yetkileri
    // ──────────────────────────────────────────────

    private TicariBelgeDto ToDto(SatisBelgesiDto belge)
    {
        var dto = _mapper.Map<TicariBelgeDto>(belge);
        UygulaIslemYetkileri(dto);
        return dto;
    }

    private TicariBelgeDetayDto ToDetayDto(SatisBelgesiDto belge)
    {
        var dto = _mapper.Map<TicariBelgeDetayDto>(belge);
        UygulaIslemYetkileri(dto);
        return dto;
    }

    /// <summary>
    /// İşlem yetenekleri ve operasyonel durum açıklaması TEK merkezi kaynaktan
    /// (TicariBelgeIslemYetkisi) türetilir - legacy Durum ASLA kullanılmaz.
    /// </summary>
    private static void UygulaIslemYetkileri(TicariBelgeDto dto)
    {
        dto.GuncellenebilirMi = TicariBelgeIslemYetkisi.GuncellenebilirMi(dto.TicariDurum, dto.MuhasebeDurumu);
        dto.SilinebilirMi = TicariBelgeIslemYetkisi.SilinebilirMi(dto.TicariDurum, dto.MuhasebeDurumu, dto.FaturalamaDurumu);
        dto.MuhasebeOnayinaGonderilebilirMi = TicariBelgeIslemYetkisi.MuhasebeOnayinaGonderilebilirMi(dto.TicariDurum, dto.MuhasebeDurumu);
        dto.IptalEdilebilirMi = TicariBelgeIslemYetkisi.IptalEdilebilirMi(dto.TicariDurum, dto.FaturalamaDurumu);
        dto.OperasyonelDurumAciklamasi = TicariBelgeIslemYetkisi.OperasyonelDurumAciklamasi(dto.TicariDurum, dto.MuhasebeDurumu, dto.FaturalamaDurumu);
    }
}
