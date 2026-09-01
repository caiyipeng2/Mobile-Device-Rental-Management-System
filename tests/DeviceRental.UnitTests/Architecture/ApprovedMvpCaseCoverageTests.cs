using Xunit;

namespace DeviceRental.UnitTests.Architecture;

public sealed class ApprovedMvpCaseCoverageTests
{
    [Fact]
    public void MvpRegistry_HasExactlyOneValidRowForEveryApprovedMvpCase() =>
        CoverageContract.AssertMvpCaseCoverage();
}
