# Publish a compact Fluent 2 app with NativeAOT

[한국어](native-aot.ko.md) · [Project README](../README.md)

[duxel_hello.cs](../samples/fba/duxel_hello.cs) displays one Fluent 2 text control in a Vulkan window and uses a system font. It references the Duxel source projects in this repository.

## Measured x64 executable sizes

All rows use .NET SDK **11.0.100** and Release builds. Sizes count the executable alone, in decimal MB rounded to two places (1 MB = 1,000,000 bytes). Each executable presented a frame on the corresponding desktop. The Windows PDB and Linux DBG files are separate.

| Source and publish settings | Windows 11 x64 | Ubuntu 24.04 x64 on WSLg |
| --- | ---: | ---: |
| Published Duxel 0.4.0 · NativeAOT defaults | 12,812,800 bytes | 13,566,208 bytes |
| Published Duxel 0.4.0 · size optimization | 12,038,656 bytes | 13,431,344 bytes |
| Published Duxel 0.4.0 · size optimization and invariant globalization | 11,818,496 bytes | 13,233,648 bytes |
| Repository source · compact build and the runtime settings below | **4,846,592 bytes (4.85 MB)** | **5,283,592 bytes (5.28 MB)** |

The table targets .NET 11. With the same source and publish settings targeting .NET 10, the Linux executable was **5,225,312 bytes (5.23 MB, 4.98 MiB)** and presented its first frame.

The published Windows executable contained about 6.08 MB of embedded resources, including 5.47 MB of Fluent icon fonts and 0.62 MB of Vulkan shaders. NativeAOT already trims code. As long as the app can load a resource from its assembly, code trimming can leave that resource intact. The opt-in compact build omits embedded icon fonts, compresses the Vulkan shaders it still needs, and leaves out the managed file picker fallback.

On Linux, the compact build keeps vector glyph tessellation and uses LibTessDotNet's allocation-only pool. The default pool brings in the .NET expression-tree interpreter to construct tessellation objects. Switching pools saved 853,584 bytes in this sample. The Linux build is still above 5 MB. [LibTessDotNet pool implementation](https://github.com/speps/LibTessDotNet/blob/v1.1.15/LibTessDotNet/Sources/MeshUtils.cs)

## Reproduce the compact build

Clone this repository and run the commands from its root on the target OS. Install the [.NET 11 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/11.0). Windows needs the Visual Studio **Desktop development with C++** workload. On Ubuntu, install Clang and the NativeAOT development libraries and use an [X11/Vulkan desktop](linux-platform.md). [Microsoft NativeAOT prerequisites](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/#prerequisites)

### Windows x64 · PowerShell

~~~powershell
dotnet publish samples/fba/duxel_hello.cs -c Release -r win-x64 `
  -p:PublishAot=true -p:DuxelCompact=true -p:OptimizationPreference=Size `
  -p:InvariantGlobalization=true -p:StackTraceSupport=false -p:EventSourceSupport=false `
  -p:UseSystemResourceKeys=true -p:IlcScanReflection=false `
  "-p:ArtifactsPath=$PWD/out/native-objects" -o out/native
(Get-Item ./out/native/duxel_hello.exe).Length
& ./out/native/duxel_hello.exe
~~~

### Linux x64 · Bash

~~~bash
dotnet publish samples/fba/duxel_hello.cs -c Release -r linux-x64 \
  -p:PublishAot=true -p:DuxelCompact=true -p:OptimizationPreference=Size \
  -p:InvariantGlobalization=true -p:StackTraceSupport=false -p:EventSourceSupport=false \
  -p:UseSystemResourceKeys=true -p:IlcScanReflection=false \
  "-p:ArtifactsPath=$PWD/out/native-objects" -o out/native
wc -c out/native/duxel_hello
./out/native/duxel_hello "$(fc-match -f '%{file}' sans-serif)"
~~~

The window should display **Hello, Duxel**. The executable includes the .NET runtime. The target needs its platform libraries, a Vulkan driver, and the fonts and assets used by the app.

To reproduce the Linux .NET 10 measurement, keep SDK 11 and change the sample's first line to `#:property TargetFramework=net10.0`. Run the Linux commands above with different output and artifact directories.

## Choose settings for your app

1. Start with the app host and features you use. Publish with `-p:PublishAot=true`, then compare `-p:OptimizationPreference=Size` with the same workflow on each target.
2. Enable `DuxelCompact` for an app that needs less embedded content. Set `-p:DuxelEmbedFluentIconFonts=true` to embed the Fluent icon fonts, or place the required font files beside the executable. Compact builds use native file dialogs, so verify the dialog service on your target desktop if the app opens or saves files.
3. Use invariant globalization only after testing formats, sorting, and comparisons in the locales you support. `StackTraceSupport=false`, `EventSourceSupport=false`, and `UseSystemResourceKeys=true` reduce diagnostic data and detailed error messages. Keep the settings your support workflow needs.
4. `IlcScanReflection=false` is a NativeAOT compiler switch used for this .NET 11 measurement. Omit it for apps that rely on reflection metadata unless you have validated every affected path in a native build.
5. Measure the complete files you deploy, including any external fonts and assets. Retain native symbols separately for troubleshooting. When comparing build profiles, use a separate `ArtifactsPath` for each profile.

[Microsoft NativeAOT optimization guide](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/optimizing)
