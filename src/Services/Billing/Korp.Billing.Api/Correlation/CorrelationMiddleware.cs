using Korp.Billing.Api.Errors;

namespace Korp.Billing.Api.Correlation;

public sealed class CorrelationMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemName = "BillingCorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].ToString();
        Guid correlationId;
        if (string.IsNullOrWhiteSpace(supplied)) correlationId = Guid.NewGuid();
        else if (!Guid.TryParseExact(supplied, "D", out correlationId) || correlationId == Guid.Empty)
        {
            await Results.Problem(ApiProblemDetails.Create(context, 400, "invalid_correlation_id",
                "Correlação inválida", "Informe X-Correlation-ID como UUID canônico." )).ExecuteAsync(context);
            return;
        }

        context.Items[ItemName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId.ToString("D");
        await next(context);
    }

    public static Guid Get(HttpContext context) => (Guid)context.Items[ItemName]!;
}
