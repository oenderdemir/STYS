using TOD.Platform.Persistence.Rdbms.Dto;

namespace TOD.Platform.Identity.UserKurums.Dto;

public class UserKurumDto : BaseRdbmsDto<Guid>
{
    public Guid UserId { get; set; }

    public int KurumId { get; set; }

    public bool VarsayilanMi { get; set; }

    public bool AktifMi { get; set; }

    public bool IsKurumAdmin { get; set; }
}
