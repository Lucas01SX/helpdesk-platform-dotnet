using Helpdesk.Shared.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.API.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected string CorrelationId =>
        HttpContext.Items["CorrelationId"] as string ?? HttpContext.TraceIdentifier;

    protected ApiSuccessResponse<T> Success<T>(T data) =>
        new(data, CorrelationId, DateTime.UtcNow);

    protected ApiFailureResponse Failure(Error error) =>
        new(false, new ApiErrorDetail(error.Code, error.Message), CorrelationId, DateTime.UtcNow);
}

public sealed record ApiSuccessResponse<T>(T Data, string CorrelationId, DateTime Timestamp);

public sealed record ApiFailureResponse(bool Success, ApiErrorDetail Error, string CorrelationId, DateTime Timestamp);

public sealed record ApiErrorDetail(string Code, string Message);
