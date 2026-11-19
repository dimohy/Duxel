using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Duxel.Svg.Managed;

internal static class ZoomProfile
{
    internal static void Run(string output)
    {
        Directory.CreateDirectory(output);
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Browser", "Red_Gallardo.svg");
        var scene = SvgManagedScene.Compile(XDocument.Load(path));
        foreach (var scale in new[] { 1f, 1.01f, 2f, 4f })
        {
            var width = (int)MathF.Ceiling(764 * scale);
            var height = (int)MathF.Ceiling(372 * scale);
            var allocated = GC.GetTotalAllocatedBytes(true);
            var started = Stopwatch.GetTimestamp();
            byte[]? pixels = null;
            string? error = null;
            try
            {
                pixels = scene.Render(width, height, Vector4.One);
            }
            catch (InvalidDataException failure) { error = failure.Message; }
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var bytes = GC.GetTotalAllocatedBytes(true) - allocated;
            using var stream = File.Create(Path.Combine(output, $"zoom-{scale.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}.json"));
            using var json = new Utf8JsonWriter(stream, new()
            {
                Indented = true
            });
            json.WriteStartObject();
            json.WriteString("runtime", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
            json.WriteNumber("scale", scale);
            json.WriteNumber("width", width);
            json.WriteNumber("height", height);
            json.WriteNumber("milliseconds", elapsed);
            json.WriteNumber("allocatedBytes", bytes);
            if (pixels is not null)
                json.WriteString("rgbaSha256", Convert.ToHexString(SHA256.HashData(pixels)));
            if (error is not null)
                json.WriteString("error", error);
            json.WriteEndObject();
            json.Flush();
            Console.WriteLine($"Zoom {scale:F2} : {elapsed:F2} ms / {bytes} B / {error ?? "rendered"}");
        }
    }
}
