using Duxel.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class FluentCatalogTests
{
    internal static void Run(Action<bool, string> check)
    {
        var exposed = Fluent.ComponentNames.ToArray();
        var required = Fluent.RequiredComponentNames.ToArray();
        var missing = Fluent.MissingComponentNames.ToArray();
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "FluentFactoryInventory.json");
        var expected = JsonSerializer.Deserialize(File.ReadAllText(fixture), FluentFactoryReferenceJson.Default.FluentFactoryReference)
            ?? throw new InvalidDataException("The explicit Fluent API fixture is empty.");
        check(exposed.Distinct(StringComparer.Ordinal).Count() == exposed.Length
            && required.Distinct(StringComparer.Ordinal).Count() == required.Length,
            "Fluent exposed and required inventories contain unique names");
        check(exposed.Length == expected.Exposed.Length && exposed.ToHashSet(StringComparer.Ordinal).SetEquals(expected.Exposed),
            Difference("exposed factories", exposed, expected.Exposed));
        check(required.Length == expected.Required.Length && required.ToHashSet(StringComparer.Ordinal).SetEquals(expected.Required),
            Difference("official requirements", required, expected.Required));
        var expectedMissing = expected.Required.Except(expected.Exposed, StringComparer.Ordinal).ToArray();
        check(missing.Length == expectedMissing.Length && missing.ToHashSet(StringComparer.Ordinal).SetEquals(expectedMissing),
            "Fluent missing inventory is the exact official requirement difference");
    }
    private static string Difference(string label, string[] actual, string[] expected) =>
        $"Fluent {label} match the explicit API contract; missing : {string.Join(", ", expected.Except(actual, StringComparer.Ordinal))} / extra : {string.Join(", ", actual.Except(expected, StringComparer.Ordinal))}";
}

internal sealed record FluentFactoryReference(string[] Exposed, string[] Required);
[JsonSerializable(typeof(FluentFactoryReference))]
internal partial class FluentFactoryReferenceJson : JsonSerializerContext;
