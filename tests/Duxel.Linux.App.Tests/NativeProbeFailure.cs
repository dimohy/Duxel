using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed record NativeProbeFailure(string Kind, string ExceptionType, string Message)
{
    internal static void Capture(string output, Action run)
    {
        try
        {
            run();
        }
        catch (Exception error)
        {
            // Complete the file before rethrowing: an unhandled NativeAOT
            // exception can terminate without unwinding a using declaration.
            using (var file = File.Open(output + ".failure.json", FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                JsonSerializer.Serialize(file, new NativeProbeFailure("managed-client-exception", error.GetType().FullName!, error.Message),
                    NativeProbeFailureJson.Default.NativeProbeFailure);
            throw;
        }
    }

    internal static NativeProbeFailure? Read(string output)
    {
        var path = output + ".failure.json";
        if (!File.Exists(path))
            return null;
        return JsonSerializer.Deserialize(File.ReadAllText(path), NativeProbeFailureJson.Default.NativeProbeFailure)
            ?? throw new InvalidDataException("The owned probe failure record is empty.");
    }
}

internal sealed class NativeProbeClientException(string mode, int exitCode, NativeProbeFailure failure)
    : InvalidOperationException($"Owned probe {mode} exited {exitCode}: {failure.Kind}; {failure.ExceptionType}: {failure.Message}")
{
    internal NativeProbeFailure Failure { get; } = failure;
}

[JsonSerializable(typeof(NativeProbeFailure))]
[JsonSerializable(typeof(NativeFailureContractResult))]
internal partial class NativeProbeFailureJson : JsonSerializerContext;

internal sealed record NativeFailureContractResult(int passed, bool primaryFailurePreserved);
