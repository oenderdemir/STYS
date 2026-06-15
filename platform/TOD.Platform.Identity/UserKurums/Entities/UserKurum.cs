using TOD.Platform.Identity.Users.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace TOD.Platform.Identity.UserKurums.Entities;

public class UserKurum : BaseEntity<Guid>
{
    public Guid UserId { get; set; }

    public int KurumId { get; set; }

    public bool VarsayilanMi { get; set; }

    public bool AktifMi { get; set; } = true;

    public bool IsKurumAdmin { get; set; }

    public User? User { get; set; }
}
