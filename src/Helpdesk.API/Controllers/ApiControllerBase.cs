using Helpdesk.Shared.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Helpdesk.API.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected static ApiSuccessResponse<T> Success<T>(T data) => new(data, DateTime.UtcNow);

    protected static ApiFailureResponse Failure(Error error) =>
        new(false, new ApiErrorDetail(error.Code, error.Message), DateTime.UtcNow);
}

public sealed record ApiSuccessResponse<T>(T Data, DateTime Timestamp);

public sealed record ApiFailureResponse(bool Success, ApiErrorDetail Error, DateTime Timestamp);

public sealed record ApiErrorDetail(string Code, string Message);
