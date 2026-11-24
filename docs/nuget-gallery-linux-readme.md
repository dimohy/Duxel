# Duxel Gallery for Linux

Explore Duxel controls, layout, images, SVG, and Markdown on a Linux desktop. Select an example to try it and inspect its C# code.

With the .NET 11 SDK, X11 or Xwayland, and a working Vulkan driver, save this file as `duxel_gallery_linux.cs`.

```csharp
#:property TargetFramework=net11.0
#:property PublishAot=false
#:property SelfContained=false
#:package Duxel.Gallery.Linux@0.4.0

Duxel.Gallery.GalleryLinuxApp.Run(args);
```

```sh
dotnet run --file duxel_gallery_linux.cs
```

[Linux dependencies and WSLg setup](https://github.com/dimohy/Duxel/blob/main/docs/linux-platform.md) · [Gallery guide](https://github.com/dimohy/Duxel/blob/main/docs/gallery-navigation.md)
