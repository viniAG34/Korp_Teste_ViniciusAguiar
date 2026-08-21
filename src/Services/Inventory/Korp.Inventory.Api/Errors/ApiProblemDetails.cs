using Microsoft.AspNetCore.Mvc;

namespace Korp.Inventory.Api.Errors;

public static class ApiProblemDetails
{
    public static ProblemDetails Create(
        HttpContext context,
        int status,
        string code,
        string title,
        string detail) => new()
    {
        Type = $"urn:korp:problem:{code.Replace('_', '-')}",
        Title = title,
        Status = status,
        Detail = detail,
        Instance = context.Request.Path,
        Extensions =
        {
            ["code"] = code,
            ["traceId"] = context.TraceIdentifier
        }
    };
}
