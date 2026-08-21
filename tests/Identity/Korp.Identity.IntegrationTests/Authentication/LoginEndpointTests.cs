using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korp.Identity.Api.Features.Auth.Contracts;
using Korp.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Korp.Identity.IntegrationTests.Authentication;

public sealed class LoginEndpointTests : IAsyncLifetime
{
    private const string AdministratorEmail = "admin@korp.local";
    private const string AdministratorPassword = "Strong-Test-Password-2026!";
    private static readonly string SigningKey = Convert.ToBase64String(
        Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
    private readonly string _connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_CONNECTION")
        ?? "Host=localhost;Port=5432;Database=identity_db;Username=identity;Password=identity_test_password";
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = CreateFactory(_connectionString);
        _client = _factory.CreateClient();
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE user_roles, user_claims, user_logins, user_tokens, role_claims, users, roles CASCADE",
            TestContext.Current.CancellationToken);
        var initializer = scope.ServiceProvider.GetRequiredService<IdentityDatabaseInitializer>();
        await initializer.InitializeAsync(
            new IdentitySeedOptions(AdministratorEmail, AdministratorPassword),
            TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync();
    }

    [Fact]
    public async Task TstId007ValidCredentialsReturnUsableAccessToken()
    {
        var response = await PostLoginAsync(AdministratorEmail, AdministratorPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal("Bearer", body.TokenType);
        Assert.Equal(900, body.ExpiresInSeconds);
        Assert.Contains("Admin", body.User.Roles);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    }

    [Fact]
    public async Task TstId008UnknownUserAndWrongPasswordArePubliclyIndistinguishable()
    {
        var unknown = await PostLoginAsync("unknown@korp.local", "Wrong-Password-2026!");
        var wrong = await PostLoginAsync(AdministratorEmail, "Wrong-Password-2026!");
        var unknownBody = await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var wrongBody = await wrong.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(unknown.StatusCode, wrong.StatusCode);
        Assert.Equal(NormalizeProblem(unknownBody), NormalizeProblem(wrongBody));
        Assert.DoesNotContain(AdministratorEmail, wrongBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WWW-Authenticate", wrong.Headers.Select(header => header.Key));
    }

    [Fact]
    public async Task TstId010FiveFailuresLockAndExpiredLockoutAllowsLogin()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failure = await PostLoginAsync(AdministratorEmail, "Wrong-Password-2026!");
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
        }

        var blocked = await PostLoginAsync(AdministratorEmail, AdministratorPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, blocked.StatusCode);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var user = await context.Users.SingleAsync(TestContext.Current.CancellationToken);
            user.LockoutEnd = DateTimeOffset.UtcNow.AddSeconds(-1);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var recovered = await PostLoginAsync(AdministratorEmail, AdministratorPassword);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
    }

    [Fact]
    public async Task TstId011SuccessfulLoginClearsFailureCounter()
    {
        await PostLoginAsync(AdministratorEmail, "Wrong-Password-2026!");
        var success = await PostLoginAsync(AdministratorEmail, AdministratorPassword);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Equal(0, await context.Users.Select(user => user.AccessFailedCount).SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TstId019UnavailableDatabaseReturnsSanitizedServiceUnavailableProblem()
    {
        const string unavailableConnection =
            "Host=127.0.0.1;Port=1;Database=identity_db;Username=identity;Password=not-logged;Timeout=1;Pooling=false";
        await using var factory = CreateFactory(unavailableConnection);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(AdministratorEmail, AdministratorPassword),
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("identity_unavailable", body, StringComparison.Ordinal);
        Assert.DoesNotContain(unavailableConnection, body, StringComparison.Ordinal);
        Assert.DoesNotContain("not-logged", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TstId021OpenApiContainsOnlyAnonymousLoginFunction()
    {
        var response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        var document = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/v1/auth/login", document, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh", document, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/users", document, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("invalid", "password")]
    [InlineData("admin@example.com", "")]
    public async Task InvalidContractReturnsValidationProblem(string email, string password)
    {
        var response = await PostLoginAsync(email, password);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("validation_failed", body, StringComparison.Ordinal);
    }

    private Task<HttpResponseMessage> PostLoginAsync(string email, string password) =>
        _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, password),
            TestContext.Current.CancellationToken);

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:IdentityDatabase", connectionString);
            builder.UseSetting("Jwt:SigningKey", SigningKey);
            builder.UseSetting("Jwt:Issuer", "korp-identity");
            builder.UseSetting("Jwt:Audience", "korp-erp-api");
        });

    private static string NormalizeProblem(string json)
    {
        using var document = JsonDocument.Parse(json);
        return string.Join('|',
            document.RootElement.GetProperty("status").GetInt32(),
            document.RootElement.GetProperty("code").GetString(),
            document.RootElement.GetProperty("detail").GetString());
    }
}
