using Korp.Billing.Api.Correlation;

namespace Korp.Billing.Api.ProductCatalog;

public sealed class ForwardAuthorizationHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = accessor.HttpContext ?? throw new InvalidOperationException("An HTTP request context is required.");
        var authorization = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization)) throw new InvalidOperationException("Authorization is required.");
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        request.Headers.TryAddWithoutValidation(CorrelationMiddleware.HeaderName, CorrelationMiddleware.Get(context).ToString("D"));
        return base.SendAsync(request, cancellationToken);
    }
}
