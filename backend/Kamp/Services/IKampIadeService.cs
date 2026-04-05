using STYS.Kamp.Dto;

namespace STYS.Kamp.Services;

public interface IKampIadeService
{
    KampIadeKarariDto Hesapla(KampIadeHesaplamaRequestDto request);
}
