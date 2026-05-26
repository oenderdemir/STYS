using STYS.Muhasebe.SatisBelgeleri.Entities;

namespace STYS.Muhasebe.SatisBelgeleri.Services.MuhasebeFisStratejileri;

public interface ISatisBelgesiMuhasebeFisStratejisi
{
    bool Destekler(SatisBelgesi belge);

    Task<IReadOnlyList<MuhasebeFisSatiriTaslak>> SatirlariOlusturAsync(
        SatisBelgesi belge,
        SatisBelgesiMuhasebeFisContext context,
        CancellationToken cancellationToken);
}
