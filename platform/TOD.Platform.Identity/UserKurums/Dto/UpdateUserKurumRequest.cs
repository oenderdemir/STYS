namespace TOD.Platform.Identity.UserKurums.Dto;

public class UpdateUserKurumRequest
{
    public bool VarsayilanMi { get; set; }

    public bool AktifMi { get; set; } = true;

    public bool IsKurumAdmin { get; set; }
}
