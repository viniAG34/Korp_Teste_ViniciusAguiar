using System.Text.Json;
using Korp.Identity.Api.Errors;
using Korp.Identity.Application.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Korp.Identity.IntegrationTests.Authentication;

public sealed class IdentityExceptionHandlerTests
{
    [Theory]
    [InlineData(true, StatusCodes.Status503ServiceUnavailable, "identity_unavailable")]
    [InlineData(false, StatusCodes.Status500InternalServerError, "unexpected_error")]
    public async Task TstId023ExceptionsReturnSanitizedProblemDetails(
        bool unavailable,
        int expectedStatus,
        string expectedCode)
    {
        const string secretSentinel = "secret-must-not-leak";
        var services = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = new MemoryStream() }
        };
        context.Request.Path = "/api/v1/auth/login";
        var source = new InvalidOperationException(secretSentinel);
        Exception exception = unavailable
            ? new IdentityServiceUnavailableException(source)
            : source;
        var handler = new IdentityExceptionHandler(NullLogger<IdentityExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(
            context,
            exception,
            TestContext.Current.CancellationToken);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(
            context.Response.Body,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(secretSentinel, document.RootElement.GetRawText(), StringComparison.Ordinal);
    }
}
