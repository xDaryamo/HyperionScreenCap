# Possible Next Steps

## Performance

### 6. Fix GenerateMips correctness for FP16 (Medium effort, High impact)

`R16G16B16A16_Float` is not guaranteed to support `D3D11_FORMAT_SUPPORT_MIP_AUTOGEN` on all
hardware (Feature Level 10.x, some FL11.0 implementations). When unsupported, `GenerateMips`
silently does nothing — no HRESULT error — and the subsequent `CopySubresourceRegion` reads
from an uninitialized mip level, sending garbage pixels to Hyperion.

Fix: call `device.CheckFormatSupport(Format.R16G16B16A16_Float)` during `Initialize()`.
If `FormatSupport.MipAutogen` is absent, fall back to a CPU-side box filter downsample or
a single-pass GPU blit via a simple pixel shader.

### 7. Double-buffered staging texture (Medium effort, High impact for GPU pipeline)

`MapSubresource(MapMode.Read, MapFlags.None)` on a staging texture blocks the CPU until all
previously submitted GPU commands finish (Copy + GenerateMips + Copy). This is a full
CPU-GPU pipeline stall every frame.

Fix: allocate two staging textures. While the GPU writes frame N to buffer B, the CPU reads
buffer A (frame N-1). Alternate each frame. Adds one frame of LED latency (~16 ms),
imperceptible for Ambilight use.

### 8. GPU-side FP16→BGRA8 downsample shader (High effort, High impact)

Instead of copying the full-resolution FP16 frame to `_smallerTexture`, generating a mip
chain, and reading one mip level, compile a simple HLSL pixel shader that:
- Samples the full-res FP16 SRV with bilinear filtering at the target resolution
- Applies tone mapping and sRGB encoding on the GPU
- Writes BGRA8 to a small render target

Benefits:
- Eliminates `GenerateMips` correctness concern entirely
- Reduces GPU bandwidth (one bilinear downsample pass vs full mip chain)
- Staging texture is BGRA8 (half the size of FP16), faster `MapSubresource`
- CPU pixel loop becomes a trivial byte copy (same as the SDR path)

Requires: HLSL shader, `ID3D11PixelShader`, `ID3D11RenderTargetView` on a small BGRA8
render target.

## Correctness / Quality

### 9. Use DisplayConfig SDR white level dynamically (Low effort)

`DisplayConfigHelper.GetSdrWhiteLevelNits()` already exists but the manual
`Dx11HdrSdrWhiteNits` setting replaced it. The dynamic query can be used as the initial
default when a task is first created, and re-queried on device recreation.

Current issue: LUID matching between SharpDX `Adapter.Description.Luid` (a `long`) and
`DISPLAYCONFIG_PATH_TARGET_INFO.adapterId` (struct with `.LowPart`/`.HighPart`) needs to be
verified. If the match works, auto-populate `Dx11HdrSdrWhiteNits` from the query on startup
so users don't have to set it manually.

### 10. Restore stashed FP16 / tone mapping work

A `git stash` on the main repo branch (not the worktree) contains earlier iteration work
on the HDR path including the `app.manifest` and tone mapping experiments. Once the current
worktree branch is merged, pop the stash and evaluate whether any parts are worth keeping.

### 11. Per-channel tone mapping vs luminance-preserving

Current implementation tone-maps R, G, B independently. This can shift hue for saturated
HDR colours (a red highlight becomes less saturated as the channel clips). A
luminance-preserving approach:
1. Convert to luminance: `Y = 0.2126R + 0.7152G + 0.0722B`
2. Compute tone-mapped luminance: `Y' = toneMap(Y)`
3. Scale channels: `R' = R * (Y'/Y)`, etc.
This preserves hue and saturation while compressing brightness.

### 12. Adaptive peak luminance

Instead of a fixed `Dx11HdrPeakLuminanceNits` setting, sample the brightest pixel in the
captured frame each iteration and use a rolling average as the tone mapping white point.
This gives scene-adaptive tonemapping that matches how HDR displays behave.
