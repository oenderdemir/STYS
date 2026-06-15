namespace TOD.Platform.Identity.UserKurums.Dto;

public class AssignUserKurumRequest
{
    public Guid UserId { get; set; }

    public int KurumId { get; set; }

    public bool VarsayilanMi { get; set; }

    public bool AktifMi { get; set; } = true;

    public bool IsKurumAdmin { get; set; }
}
