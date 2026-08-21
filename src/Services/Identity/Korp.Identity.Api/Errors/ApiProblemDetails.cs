using Microsoft.AspNetCore.Mvc;

namespace Korp.Identity.Api.Errors;

public static class ApiProblemDetails
{
    public static ProblemDetails Create(
        HttpContext httpContext,
        int status,
        string code,
        string title,
        string detail)
    {
        return new ProblemDetails
        {
            Type = $"urn:korp:problem:{code.Replace('_', '-')}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["code"] = code,
                ["traceId"] = httpContext.TraceIdentifier
            }
        };
    }
}
