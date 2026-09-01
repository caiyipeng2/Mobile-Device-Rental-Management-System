using Xunit;

namespace DeviceRental.UnitTests.Architecture;

public sealed class ApprovedRequirementCoverageTests
{
    [Fact]
    public void Traceability_HasExactlyOneValidRowForEveryApprovedRequirement() =>
        CoverageContract.AssertRequirementCoverage();
}
