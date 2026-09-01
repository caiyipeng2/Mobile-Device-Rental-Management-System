using System.Xml.Linq;

namespace DeviceRental.UnitTests.Architecture;

internal static class ProjectReferences
{
    public static IReadOnlyList<string> For(string projectName)
    {
        var projectPath = FindProject(projectName);
        var projectDirectory = Path.GetDirectoryName(projectPath)
            ?? throw new InvalidOperationException($"Project path has no directory: {projectPath}");
        var document = XDocument.Load(projectPath, LoadOptions.SetLineInfo);

        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFullPath(include!, projectDirectory))
            .Select(path => Path.GetFileNameWithoutExtension(path)
                ?? throw new InvalidOperationException($"Project reference has no file name: {path}"))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string FindProject(string projectName)
    {
        var candidates = new[] { "src", "tests" }
            .Select(folder => Path.Combine(RepositoryPaths.Root, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.csproj", SearchOption.AllDirectories))
            .Where(path => string.Equals(
                Path.GetFileNameWithoutExtension(path),
                projectName,
                StringComparison.Ordinal))
            .ToArray();

        return candidates.Length switch
        {
            1 => candidates[0],
            0 => throw new InvalidOperationException($"Project not found: {projectName}"),
            _ => throw new InvalidOperationException($"Multiple projects found: {projectName}"),
        };
    }
}
