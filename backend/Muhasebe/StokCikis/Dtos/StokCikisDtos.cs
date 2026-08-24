using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokTalepleri.Dtos;

namespace STYS.Muhasebe.StokCikis.Dtos;

public class StokCikisIstegi
{
    public int TesisId { get; set; }
    public CreateStokTalepRequest? Talep { get; set; }
    public StokTransferRequest? Transfer { get; set; }
}

public class StokCikisSonuc
{
    public StokTalepDto? Talep { get; set; }
    public IReadOnlyList<StokHareketDto>? TransferHareketleri { get; set; }
}
