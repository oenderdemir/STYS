using STYS.Kamp.Dto;
using STYS.Kamp.Entities;
using STYS.Tesisler.Entities;

namespace STYS.Kamp.Services;

public interface IKampUcretHesaplamaService
{
    void Hesapla(KampBasvuruRequestDto request, KampDonemi kampDonemi, Tesis tesis, KampBasvuruOnizlemeDto onizleme);
}
