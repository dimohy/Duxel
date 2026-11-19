using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

if (args.Length != 1) throw new ArgumentException("Expected the validation result JSON path.");
var checks = new List<string>();
void Check(bool condition, string name)
{
    if (!condition)
        throw new InvalidOperationException(name);
    checks.Add(name);
}
ControlKeyboardTests.Run(Check);
ProgressWidgetTests.Run(Check);
ProgressMotionTests.Run(Check);
AccessibilityPublicationTests.Run(Check);
ProgressAccessibilityTests.Run(Check);
SelectionPolicyTests.Run(Check);
AccessibilityTextTests.Run(Check);
AccessibilityTextEditTests.Run(Check);
var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, JsonSerializer.Serialize(new ControlValidationResult(
    RuntimeInformation.FrameworkDescription, checks.Count, checks.ToArray(),
    ProgressMotionTests.ImmediateConstructionBytes, ProgressMotionTests.AnimatedConstructionBytes), ControlValidationJson.Default.ControlValidationResult));
Console.WriteLine($"Passed {checks.Count} control checks.");

internal sealed record ControlValidationResult(string Framework, int Passed, string[] Checks, long ImmediateProgressConstructionBytes, long AnimatedProgressConstructionBytes);
[JsonSerializable(typeof(ControlValidationResult))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class ControlValidationJson : JsonSerializerContext;
