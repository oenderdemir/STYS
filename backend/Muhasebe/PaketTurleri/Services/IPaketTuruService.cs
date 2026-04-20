using STYS.Muhasebe.PaketTurleri.Dtos;
using STYS.Muhasebe.PaketTurleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.PaketTurleri.Services;

public interface IPaketTuruService : IBaseRdbmsService<PaketTuruDto, PaketTuru, int>
{
}
