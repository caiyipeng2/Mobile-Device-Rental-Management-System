namespace DeviceRental.UnitTests.Architecture;

internal static class RepositoryPaths
{
    private const string SolutionFileName = "Mobile-Device-Rental-Management-System.slnx";

    public static string Root { get; } = FindRepositoryRoot();

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, SolutionFileName)))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find {SolutionFileName} above {AppContext.BaseDirectory}.");
    }
}
