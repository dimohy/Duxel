# Linux Gallery startup (2026-11-22)

On WSLg Dozen, the Gallery's first frame spent most of its startup time creating the general SVG stroke pipeline. The first screen needed solid strokes. Its pipeline also compiled the code for following and subdividing dashed curves.

The scene validator records whether each stroke program contains a dash. The renderer uses a solid-stroke shader when no visible stroke program contains a dash. Both shaders share the same solid-stroke code, buffers, and pipeline layout. The general pipeline is created when a visible scene first needs dashes.

| WSLg Dozen on RTX 5070 Ti | Before median | After median | Reduction |
| --- | ---: | ---: | ---: |
| App initialization to first presented frame | 5701.04 ms | 1775.06 ms | 68.9% |
| Initial stroke pipeline creation | 4164.71 ms | 254.78 ms | 93.9% |

The managed Release builds used SDK 11.0.100, identical fonts and window extent, and three independent runs per variant. The Vulkan pipeline cache reported a hit in all six runs. These measurements cover startup. Opening a scene with dashed strokes for the first time still incurs the general pipeline's creation cost.

The Linux Gallery presentation probe exercised the SVG document, features, arc geometry, and both target backgrounds. Windows solid-stroke and dash pixel checks each passed 46 cases. All 136 paired Windows stroke captures matched the baseline byte for byte.

To reproduce the shader variant from the repository root:

```powershell
glslangValidator -V --target-env vulkan1.2 -Os -DDUXEL_SOLID_STROKES=1 -o src/Duxel.Vulkan/Shaders/vector_stroke.solid.comp.spv src/Duxel.Vulkan/Shaders/vector_stroke.comp
spirv-val --target-env vulkan1.2 src/Duxel.Vulkan/Shaders/vector_stroke.solid.comp.spv
```
