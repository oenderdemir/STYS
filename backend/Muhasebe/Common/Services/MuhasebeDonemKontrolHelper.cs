using STYS.Muhasebe.MuhasebeDonemleri.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.Common.Services;

public static class MuhasebeDonemKontrolHelper
{
    public static async Task EnsureOpenPeriodAsync(
        IMuhasebeDonemService muhasebeDonemService,
        int? tesisId,
        DateTime tarih,
        CancellationToken cancellationToken = default)
    {
        if (!tesisId.HasValue || tesisId.Value <= 0)
        {
            throw new BaseException("İşlem tarihi kapalı muhasebe dönemindedir.", 400);
        }

        var aktifDonem = await muhasebeDonemService.GetAktifDonemAsync(tesisId.Value, tarih, cancellationToken);
        if (aktifDonem is null)
        {
            throw new BaseException("İşlem tarihi kapalı muhasebe dönemindedir.", 400);
        }
    }
}
