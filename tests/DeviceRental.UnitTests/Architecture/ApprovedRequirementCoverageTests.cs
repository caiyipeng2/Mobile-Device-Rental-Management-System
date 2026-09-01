using Xunit;

namespace DeviceRental.UnitTests.Architecture;

public sealed class ApprovedRequirementCoverageTests
{
    [Fact]
    public void RequirementDefinitionExtraction_OnlyReadsAuthoritativeBulletsAndPreservesDuplicates()
    {
        const string markdown = """
            Prose reference: `REQ-AUTH-001`.
            - `REQ-AUTH-001` First authoritative definition.
            | REQ-DEV-001 | Traceability reference |
              - `REQ-DEV-001` Nested list item is not an authoritative definition.
            - `NFR-SEC-001` Second authoritative definition.
            - `REQ-AUTH-001` Duplicate authoritative definition.
            """;

        Assert.Equal(
            ["REQ-AUTH-001", "NFR-SEC-001", "REQ-AUTH-001"],
            CoverageContract.ExtractAuthoritativeRequirementIds(markdown));
    }

    [Fact]
    public void Traceability_HasExactlyOneValidRowForEveryApprovedRequirement() =>
        CoverageContract.AssertRequirementCoverage();
}
