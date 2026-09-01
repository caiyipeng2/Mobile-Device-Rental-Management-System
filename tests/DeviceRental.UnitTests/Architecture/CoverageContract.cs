using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace DeviceRental.UnitTests.Architecture;

internal static partial class CoverageContract
{
    private static readonly string[] RequirementHeaders =
        ["RequirementId", "ImplementationTask", "PrimaryNamedTest", "Status"];

    private static readonly string[] MvpHeaders =
        ["MvpCaseId", "ImplementationTask", "PrimaryNamedTest", "Status"];

    private static readonly HashSet<string> AllowedStatuses =
        new(["Planned", "Implemented", "Passing"], StringComparer.Ordinal);

    public static void AssertRequirementCoverage()
    {
        var specification = File.ReadAllText(
            Path.Combine(RepositoryPaths.Root, "docs", "requirements-specification.md"));
        var approvedIds = RequirementIdRegex().Matches(specification)
            .Select(match => match.Value)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(70, approvedIds.Count);

        var rows = ReadCsv(
            Path.Combine(RepositoryPaths.Root, "docs", "traceability.csv"),
            RequirementHeaders);
        Assert.Equal(70, rows.Count);
        AssertUniqueAndValid(rows, "RequirementId");

        var registeredIds = rows
            .Select(row => row["RequirementId"])
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(approvedIds.Order(StringComparer.Ordinal), registeredIds.Order(StringComparer.Ordinal));
    }

    public static void AssertMvpCaseCoverage()
    {
        var testPlan = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "docs", "test-plan.md"));
        var approvedIds = MvpTableRowRegex().Matches(testPlan)
            .Select(match => match.Groups["id"].Value)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(72, approvedIds.Count);

        var rows = ReadCsv(
            Path.Combine(RepositoryPaths.Root, "docs", "mvp-test-cases.csv"),
            MvpHeaders);
        Assert.Equal(72, rows.Count);
        AssertUniqueAndValid(rows, "MvpCaseId");

        var registeredIds = rows
            .Select(row => row["MvpCaseId"])
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(approvedIds.Order(StringComparer.Ordinal), registeredIds.Order(StringComparer.Ordinal));
    }

    private static void AssertUniqueAndValid(
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        string idColumn)
    {
        var ids = rows.Select(row => row[idColumn]).ToArray();
        Assert.All(ids, id => Assert.False(string.IsNullOrWhiteSpace(id)));
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());

        Assert.All(rows, row =>
        {
            Assert.Matches(TaskRegex(), row["ImplementationTask"]);
            Assert.Matches(NamedTestRegex(), row["PrimaryNamedTest"]);
            Assert.Contains(row["Status"], AllowedStatuses);
        });
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> ReadCsv(
        string path,
        IReadOnlyList<string> expectedHeaders)
    {
        Assert.True(File.Exists(path), $"Coverage registry is missing: {path}");
        var records = ParseCsv(File.ReadAllText(path));
        Assert.NotEmpty(records);
        Assert.Equal(expectedHeaders, records[0]);

        return records.Skip(1).Select((values, rowIndex) =>
        {
            Assert.True(
                values.Count == expectedHeaders.Count,
                $"CSV row {rowIndex + 2} has {values.Count} fields; expected {expectedHeaders.Count}.");
            return (IReadOnlyDictionary<string, string>)expectedHeaders
                .Select((header, index) => (header, value: values[index]))
                .ToDictionary(pair => pair.header, pair => pair.value, StringComparer.Ordinal);
        }).ToArray();
    }

    private static IReadOnlyList<IReadOnlyList<string>> ParseCsv(string content)
    {
        var records = new List<IReadOnlyList<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (quoted)
            {
                if (character == '"' && index + 1 < content.Length && content[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else if (character == '"')
                {
                    quoted = false;
                }
                else
                {
                    field.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"' when field.Length == 0:
                    quoted = true;
                    break;
                case ',':
                    record.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    record.Add(field.ToString());
                    field.Clear();
                    if (record.Any(value => value.Length > 0))
                    {
                        records.Add(record.ToArray());
                    }

                    record.Clear();
                    break;
                default:
                    field.Append(character);
                    break;
            }
        }

        Assert.False(quoted, "CSV ends inside a quoted field.");
        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            records.Add(record.ToArray());
        }

        return records;
    }

    [GeneratedRegex(@"\b(?:REQ|NFR)-[A-Z0-9]+-\d{3}\b", RegexOptions.CultureInvariant)]
    private static partial Regex RequirementIdRegex();

    [GeneratedRegex(@"(?m)^\|\s*(?<id>[A-Z][A-Z0-9]+-\d{3})\s*\|\s*M\s*\|", RegexOptions.CultureInvariant)]
    private static partial Regex MvpTableRowRegex();

    [GeneratedRegex(@"^Task (?:[1-9]|1[0-2])$", RegexOptions.CultureInvariant)]
    private static partial Regex TaskRegex();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_.\-/]*$", RegexOptions.CultureInvariant)]
    private static partial Regex NamedTestRegex();
}
