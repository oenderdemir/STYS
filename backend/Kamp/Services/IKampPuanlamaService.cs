using STYS.Kamp.Dto;

namespace STYS.Kamp.Services;

public interface IKampPuanlamaService
{
    KampBasvuruOnizlemeDto Puanla(KampBasvuruRequestDto request, KampBasvuruOnizlemeDto onizleme);
}
