using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace DeviceRental.UnitTests.Architecture;

public sealed class PostgreSqlProviderGuardTests
{
    private const string NpgsqlProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";

    private static readonly HashSet<string> AllowedEntityFrameworkPackages = new(
        [
            "Microsoft.AspNetCore.Identity.EntityFrameworkCore",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.EntityFrameworkCore.Design",
            "Microsoft.EntityFrameworkCore.Relational",
            "Microsoft.EntityFrameworkCore.Tools",
            NpgsqlProvider,
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ExcludedDirectoryNames = new(
        [".git", ".codegraph", ".tools", ".worktrees", "node_modules", "bin", "obj"],
        StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Infrastructure_DeclaresAndLocksNpgsqlEntityFrameworkCoreProvider()
    {
        var infrastructureDirectory = Path.Combine(
            RepositoryPaths.Root,
            "src",
            "DeviceRental.Infrastructure");
        var project = XDocument.Load(Path.Combine(
            infrastructureDirectory,
            "DeviceRental.Infrastructure.csproj"));
        var directPackages = PackageIds(project, "PackageReference");

        Assert.Contains(NpgsqlProvider, directPackages, StringComparer.OrdinalIgnoreCase);

        using var lockDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            infrastructureDirectory,
            "packages.lock.json")));
        var dependencies = lockDocument.RootElement
            .GetProperty("dependencies")
            .GetProperty("net10.0");
        Assert.True(
            dependencies.TryGetProperty(NpgsqlProvider, out var provider),
            $"{NpgsqlProvider} is absent from the Infrastructure lock file.");
        Assert.Equal("Direct", provider.GetProperty("type").GetString());
    }

    [Fact]
    public void PackageDeclarations_AllowOnlyTheApprovedEntityFrameworkCoreProvider()
    {
        var declarationFiles = EnumerateRepositoryFiles()
            .Where(path =>
                path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".props", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".targets", StringComparison.OrdinalIgnoreCase));

        var unapprovedPackages = declarationFiles
            .SelectMany(path =>
            {
                var document = XDocument.Load(path);
                return PackageIds(document, "PackageReference")
                    .Concat(PackageIds(document, "PackageVersion"));
            })
            .Where(package => package.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase))
            .Where(package => !AllowedEntityFrameworkPackages.Contains(package))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(unapprovedPackages);
    }

    [Fact]
    public void Source_DoesNotSelectSQLiteOrInMemoryDatabaseProviders()
    {
        var guardPath = Path.GetFullPath(Path.Combine(
            RepositoryPaths.Root,
            "tests",
            "DeviceRental.UnitTests",
            "Architecture",
            "PostgreSqlProviderGuardTests.cs"));
        var forbiddenUsages = new[] { "UseSqlite", "UseInMemoryDatabase" };

        var violations = EnumerateRepositoryFiles()
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => !string.Equals(Path.GetFullPath(path), guardPath, StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => forbiddenUsages
                .Where(token => File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{Path.GetRelativePath(RepositoryPaths.Root, path)}: {token}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    private static IReadOnlyList<string> PackageIds(XDocument document, string elementName) =>
        document.Descendants()
            .Where(element => element.Name.LocalName == elementName)
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();

    private static IEnumerable<string> EnumerateRepositoryFiles() =>
        Directory.EnumerateFiles(RepositoryPaths.Root, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(RepositoryPaths.Root, path)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(ExcludedDirectoryNames.Contains));
}
