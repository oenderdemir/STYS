namespace TOD.Platform.SharedKernel.Responses;

public sealed class ApiError
{
    public ApiError(string code, string? field, string detail)
    {
        Code = code;
        Field = field;
        Detail = detail;
    }

    public string Code { get; set; }

    public string? Field { get; set; }

    public string Detail { get; set; }
}
