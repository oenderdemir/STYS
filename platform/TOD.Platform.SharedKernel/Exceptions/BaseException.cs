namespace TOD.Platform.SharedKernel.Exceptions;

public class BaseException : Exception
{
    public BaseException(string? message, int errorCode = 500)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public int ErrorCode { get; }
}
