using Xunit;

namespace DeviceRental.UnitTests.Architecture;

public sealed class ProjectReferenceTests
{
    [Fact]
    public void Domain_HasNoProjectReferences() =>
        Assert.Empty(ProjectReferences.For("DeviceRental.Domain"));

    [Fact]
    public void Projects_FollowTheApprovedReferenceGraph()
    {
        var expected = new Dictionary<string, string[]>
        {
            ["DeviceRental.Domain"] = [],
            ["DeviceRental.Application"] = ["DeviceRental.Domain"],
            ["DeviceRental.Infrastructure"] = ["DeviceRental.Application"],
            ["DeviceRental.Web"] = ["DeviceRental.Application", "DeviceRental.Infrastructure"],
            ["DeviceRental.Worker"] = ["DeviceRental.Application", "DeviceRental.Infrastructure"],
            ["DeviceRental.AdminCli"] = ["DeviceRental.Application", "DeviceRental.Infrastructure"],
            ["DeviceRental.UnitTests"] = ["DeviceRental.Application", "DeviceRental.Domain"],
            ["DeviceRental.Testing"] = ["DeviceRental.Application", "DeviceRental.Infrastructure"],
            ["DeviceRental.IntegrationTests"] = ["DeviceRental.Infrastructure", "DeviceRental.Testing"],
            ["DeviceRental.WebTests"] = ["DeviceRental.Testing", "DeviceRental.Web"],
            ["DeviceRental.E2ETests"] = ["DeviceRental.Testing", "DeviceRental.Web"],
        };

        foreach (var (project, references) in expected)
        {
            Assert.Equal(references, ProjectReferences.For(project).Order(StringComparer.Ordinal));
        }
    }

    [Fact]
    public void CompositionRoots_DoNotReferenceEachOther()
    {
        Assert.DoesNotContain("DeviceRental.Worker", ProjectReferences.For("DeviceRental.Web"));
        Assert.DoesNotContain("DeviceRental.AdminCli", ProjectReferences.For("DeviceRental.Worker"));
        Assert.DoesNotContain("DeviceRental.Web", ProjectReferences.For("DeviceRental.AdminCli"));
    }
}
