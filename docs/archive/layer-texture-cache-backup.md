# Layer Texture Cache Backup

> Archived: 2026-03-14
> Scope: removed experimental `UiLayerCacheBackend.Texture` path and Vulkan layer texture compose/cache implementation

## Purpose

This document preserves the behavior and structure of the removed layer texture cache path so a future implementation can be rebuilt intentionally instead of keeping dormant runtime weight in the main binary.

## Removed public/runtime shape

- Public enum case: `UiLayerCacheBackend.Texture`
- Experimental environment flags:
  - `DUXEL_LAYER_TEXTURE_BACKEND`
  - `DUXEL_LAYER_TEXTURE_COMPOSE`
- Vulkan renderer-specific cache image path in `src/Duxel.Vulkan/VulkanRendererBackend.cs`

## Previous behavior summary

### UI layer cache tagging

Static cached layers built a geometry tag with a backend suffix:

- draw-list backend suffix: `:cbd`
- texture backend suffix: `:cbt`

`UiImmediateContext.Layers.cs` used that suffix to distinguish replay behavior.
When the backend was `Texture`, replay commands carried static geometry tags in `UserData` so the Vulkan renderer could:

1. recognize texture-cache-tagged static layer lists,
2. hash them into a composition signature,
3. render only static cached content once,
4. copy the composed swapchain result into a device-local cache image,
5. reuse that cached image on later frames,
6. draw only dynamic overlays on top.

### Vulkan renderer path

The removed renderer path had these main pieces:

- `LayerTextureCacheResource`
  - cached `Image`
  - `DeviceMemory`
  - `ImageView`
  - `DescriptorSet`
- state flags
  - `_layerTextureCacheInitialized`
  - `_layerTextureComposeSignature`
  - `_layerTextureComposeSignatureValid`
- setup / teardown
  - `CreateLayerTextureCacheScaffold()`
  - `DestroyLayerTextureCacheScaffold()`
- signature computation
  - `TryComputeLayerComposeSignature(UiDrawData, out ulong)`
- command recording branch inside `RecordCommandBuffer(...)`
  - render static content only when cache miss
  - copy swapchain image to cache image
  - reuse cache image on hit
  - compute dynamic dirty coverage and redraw dynamic lists only

## Important design notes for future reimplementation

- The old implementation was **renderer-specific** and depended on Vulkan image copies plus an extra overlay render pass.
- The public `UiLayerCacheBackend.Texture` option rooted renderer-specific code into the general app/runtime surface.
- Future reimplementation should likely be isolated behind a dedicated renderer capability instead of a broadly exposed toggle.
- If reintroduced, measure NativeAOT size impact separately from functional wins.

## Previous source locations

- `src/Duxel.Core/UiTypes.cs`
- `src/Duxel.Core/UiImmediateContext.Layers.cs`
- `src/Duxel.Vulkan/VulkanRendererBackend.cs`
- sample references:
  - `samples/fba/all_features.cs`
  - `samples/fba/idle_layer_validation.cs`
  - `samples/fba/layer_dirty_strategy_bench.cs`
  - `samples/fba/layer_widget_mix_bench_fba.cs`

## Removal rationale

- Feature was experimental and later planned for a cleaner redesign.
- Dormant toggle/configuration paths can still keep code rooted in NativeAOT output.
- Removing the entire path is safer than leaving a disabled-but-linked feature in place.
