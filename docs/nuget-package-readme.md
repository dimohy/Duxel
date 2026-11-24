# Duxel

Duxel is a C# library for native desktop UI. Build retained widget trees, draw with the immediate-mode API, or combine both in a Vulkan-rendered window. Built-in Fluent 2 themes support light, dark and custom designs.

On Windows, install the platform package. It brings in the shared `Duxel.App` package and its DSL source generator.

```sh
dotnet add package Duxel.Windows.App --version 0.4.0
```

The packages include assemblies for .NET 8, 9, and 10. Windows applications should use a Windows target framework such as `net10.0-windows`. For a single-file application, use the .NET 10 SDK or later and the `#:package Duxel.Windows.App@0.4.0` directive. To explore live controls and copy their C# source, start with [Duxel.Gallery on NuGet](https://www.nuget.org/packages/Duxel.Gallery/0.4.0).

[Quick start and examples](https://github.com/dimohy/Duxel#start-a-windows-app) · [Documentation](https://github.com/dimohy/Duxel/blob/main/docs/README.md) · [Version history](https://github.com/dimohy/Duxel/blob/main/docs/version-history.md)
