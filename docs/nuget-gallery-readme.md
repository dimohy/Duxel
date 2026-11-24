# Duxel Gallery

Explore Duxel's controls, SVG and Markdown examples, layout tools, and C# samples in a Windows desktop app. The gallery supports English, Korean, Japanese, and Simplified Chinese.

On Windows with the .NET 11 SDK and a Vulkan-capable GPU, save this file as `duxel_gallery.cs`.

```csharp
#:property TargetFramework=net11.0-windows
#:property PublishAot=false
#:property SelfContained=false
#:package Duxel.Gallery@0.4.0

Duxel.Gallery.GalleryApp.Run(args);
```

Run it from PowerShell.

```powershell
dotnet run --file duxel_gallery.cs
```

[Gallery guide](https://github.com/dimohy/Duxel/blob/main/docs/gallery-navigation.md) · [Duxel repository](https://github.com/dimohy/Duxel)
