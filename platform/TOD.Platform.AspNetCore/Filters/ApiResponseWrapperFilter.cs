using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TOD.Platform.SharedKernel.Responses;

namespace TOD.Platform.AspNetCore.Filters;

public sealed class ApiResponseWrapperFilter : IAsyncResultFilter
{
    public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult objectResult)
        {
            WrapObjectResult(context, objectResult);
            return next();
        }

        if (context.Result is EmptyResult)
        {
            context.Result = new OkObjectResult(ApiResponse.Ok<object?>(null, GetSuccessMessage(200), context.HttpContext.TraceIdentifier));
            return next();
        }

        if (context.Result is StatusCodeResult statusCodeResult)
        {
            var statusCode = statusCodeResult.StatusCode;
            if (statusCode is >= 200 and < 300)
            {
                context.Result = new ObjectResult(ApiResponse.Ok<object?>(null, GetSuccessMessage(statusCode), context.HttpContext.TraceIdentifier))
                {
                    StatusCode = statusCode == 204 ? 200 : statusCode
                };
            }
        }

        return next();
    }

    private static void WrapObjectResult(ResultExecutingContext context, ObjectResult objectResult)
    {
        if (objectResult.Value is IApiResponseEnvelope envelope)
        {
            envelope.TraceId ??= context.HttpContext.TraceIdentifier;
            return;
        }

        var statusCode = objectResult.StatusCode ?? 200;
        if (statusCode is >= 200 and < 300)
        {
            objectResult.Value = ApiResponse.Ok(objectResult.Value, GetSuccessMessage(statusCode), context.HttpContext.TraceIdentifier);
            if (statusCode == 204)
            {
                objectResult.StatusCode = 200;
            }
        }
    }

    private static string GetSuccessMessage(int statusCode)
    {
        return statusCode switch
        {
            200 => "Success",
            201 => "Created",
            202 => "Accepted",
            204 => "Success",
            _ => "Success"
        };
    }
}
