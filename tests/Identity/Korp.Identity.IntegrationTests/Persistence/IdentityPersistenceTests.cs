using Korp.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Korp.Identity.IntegrationTests.Persistence;

public sealed class IdentityPersistenceTests : IAsyncLifetime
{
    private readonly string _connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_CONNECTION")
        ?? "Host=localhost;Port=5432;Database=identity_db;Username=identity;Password=identity_test_password";

    public async ValueTask InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE user_roles, user_claims, user_logins, user_tokens, role_claims, users, roles CASCADE", TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task TstData001MigrationsCreateCompleteIdentitySchema()
    {
        await using var context = CreateContext();
        var tableCount = await context.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public' AND table_name IN ('users','roles','user_roles','user_claims','user_logins','user_tokens','role_claims')").SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(7, tableCount);
    }

    [Fact]
    public async Task TstData003AdministratorSeedIsIdempotentAndPreservesPassword()
    {
        await using var provider = CreateServiceProvider();
        await using var firstScope = provider.CreateAsyncScope();
        var firstInitializer = firstScope.ServiceProvider.GetRequiredService<IdentityDatabaseInitializer>();
        var options = new IdentitySeedOptions("admin@korp.local", "Strong-Test-Password-2026!");
        await firstInitializer.InitializeAsync(options, TestContext.Current.CancellationToken);
        var firstUserManager = firstScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var firstUser = await firstUserManager.FindByEmailAsync(options.Email);
        var originalHash = Assert.IsType<string>(firstUser?.PasswordHash);
        await firstScope.DisposeAsync();

        await using var secondScope = provider.CreateAsyncScope();
        var secondInitializer = secondScope.ServiceProvider.GetRequiredService<IdentityDatabaseInitializer>();
        await secondInitializer.InitializeAsync(options with { Password = "Different-Password-2026-Must-Not-Apply!" }, TestContext.Current.CancellationToken);
        var context = secondScope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        Assert.Equal(1, await context.Users.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.Roles.CountAsync(role => role.Name == IdentityDatabaseInitializer.AdministratorRole, TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.UserRoles.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(originalHash, await context.Users.Select(user => user.PasswordHash).SingleAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("invalid-email", "Strong-Test-Password-2026!")]
    [InlineData("admin@korp.local", "too-short")]
    [InlineData("admin@korp.local", "NO-LOWERCASE-2026!")]
    [InlineData("admin@korp.local", "no-uppercase-2026!")]
    [InlineData("admin@korp.local", "NoNumbersHere!")]
    [InlineData("admin@korp.local", "NoSpecialCharacter2026")]
    public async Task TstId005InvalidSeedConfigurationChangesNoIdentityState(string email, string password)
    {
        await using var provider = CreateServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IdentityDatabaseInitializer>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.InitializeAsync(
            new IdentitySeedOptions(email, password),
            TestContext.Current.CancellationToken));
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        Assert.Equal(0, await context.Users.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await context.Roles.CountAsync(TestContext.Current.CancellationToken));
    }

    private IdentityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new IdentityDbContext(options);
    }

    private ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(_connectionString).UseSnakeCaseNamingConvention());
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<IdentityDbContext>();
        services.AddScoped<IdentityDatabaseInitializer>();
        return services.BuildServiceProvider(validateScopes: true);
    }
}
