using System.Xml.Linq;

namespace Korp.ArchitectureTests;

public sealed class ProjectReferenceRulesTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string[] SharedContractsReference = ["Korp.Shared.Contracts"];

    public static TheoryData<string, string[]> LayerProjects => new()
    {
        { "Identity", Array.Empty<string>() },
        { "Inventory", SharedContractsReference },
        { "Billing", SharedContractsReference }
    };

    [Theory]
    [MemberData(nameof(LayerProjects))]
    public void ServiceProjectsFollowTheApprovedDependencyDirection(
        string service,
        string[] additionalInfrastructureReferences)
    {
        var serviceDirectory = Path.Combine(RepositoryRoot, "src", "Services", service);

        AssertReferences(
            Path.Combine(serviceDirectory, $"Korp.{service}.Domain", $"Korp.{service}.Domain.csproj"));

        AssertReferences(
            Path.Combine(serviceDirectory, $"Korp.{service}.Application", $"Korp.{service}.Application.csproj"),
            $"Korp.{service}.Domain");

        AssertReferences(
            Path.Combine(serviceDirectory, $"Korp.{service}.Infrastructure", $"Korp.{service}.Infrastructure.csproj"),
            new[] { $"Korp.{service}.Application", $"Korp.{service}.Domain" }
                .Concat(additionalInfrastructureReferences)
                .ToArray());

        AssertReferences(
            Path.Combine(serviceDirectory, $"Korp.{service}.Api", $"Korp.{service}.Api.csproj"),
            $"Korp.{service}.Application",
            $"Korp.{service}.Infrastructure");
    }

    [Fact]
    public void GatewayAndSharedContractsDoNotReferenceServiceProjects()
    {
        AssertReferences(Path.Combine(
            RepositoryRoot,
            "src",
            "Gateway",
            "Korp.Gateway.Api",
            "Korp.Gateway.Api.csproj"));

        AssertReferences(Path.Combine(
            RepositoryRoot,
            "src",
            "Shared",
            "Korp.Shared.Contracts",
            "Korp.Shared.Contracts.csproj"));
    }

    private static void AssertReferences(string projectPath, params string[] expectedProjectNames)
    {
        var document = XDocument.Load(projectPath);
        var actualProjectNames = document
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(path => path is not null)
            .Select(path => Path.GetFileNameWithoutExtension(path!.Replace('\\', '/')))
            .Order(StringComparer.Ordinal)
            .ToArray();

        var expected = expectedProjectNames.Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(expected, actualProjectNames);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Korp.Erp.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Repository root containing Korp.Erp.sln was not found.");
    }
}
