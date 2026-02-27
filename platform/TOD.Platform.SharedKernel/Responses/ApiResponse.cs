namespace TOD.Platform.SharedKernel.Responses;

public interface IApiResponseEnvelope
{
    string? TraceId { get; set; }
}

public sealed class ApiResponse<T> : IApiResponseEnvelope
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public T? Data { get; set; }

    public List<ApiError> Errors { get; set; } = [];

    public string? TraceId { get; set; }

    public static ApiResponse<T> Ok(T? data, string message = "Success", string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data,
            Errors = [],
            TraceId = traceId
        };
    }

    public static ApiResponse<T> Fail(string message, IEnumerable<ApiError>? errors = null, string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Data = default,
            Errors = errors?.ToList() ?? [],
            TraceId = traceId
        };
    }
}

public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T? data, string message = "Success", string? traceId = null)
    {
        return ApiResponse<T>.Ok(data, message, traceId);
    }

    public static ApiResponse<object?> Fail(string message, IEnumerable<ApiError>? errors = null, string? traceId = null)
    {
        return ApiResponse<object?>.Fail(message, errors, traceId);
    }
}
