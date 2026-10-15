# Screen-space rolling soil

Open `SoilSurfaceGrading.unity` and enter Play. The example releases dynamic soil
on a slope and moves a thin AGX shovel through it. Its buttons compare ordinary
particle meshes, the full reconstructed surface, and the surface with 5% separate
rocks. Another button switches sphere and mesh input. Physics uses the original
AGX particles in every mode.

The example uses Unity 6000.4, URP 17.4, Forward+, Render Graph, depth priming
disabled, and MSAA disabled. Its `SoilSurfaceExample` component temporarily selects
the included URP asset during Play and restores the previous assets on disable.
This asset is a separate renderer configuration; the project's renderer assets
are not edited. Windows and Linux are the package's supported player targets.
The component defaults are support scale 1.15 and smoothing 0.6. The grading
example uses support scale 1.5 and smoothing 1 for a more connected surface.

## Use in an existing scene

1. Add `TerrainParticleSurfaceFeature` to the camera's Universal Renderer asset.
   Use Forward+, disabled depth priming, and no MSAA. Keep Render Graph enabled.
2. Add `TerrainParticleSurfaceRenderer` to an object with `DeformableTerrain`,
   `MovableTerrain`, or `DeformableTerrainPager`. Its required
   `TerrainParticleSnapshotProvider` is added automatically.
3. Assign a centered particle mesh with all vertices within radius 0.5 and an instanced, opaque lit
   particle material. `JaggedRock.asset` and `Rock.mat` provide examples.
4. Assign a repeatable soil texture, tint, and repeat distance in metres. An
   optional soil material must use `Hidden/AGXUnity/TerrainParticleSurface` and
   have instancing enabled for mesh input.
5. Use `IndividualParticleFraction = 0` for full surface, `0.05f` for mixed
   rendering, or `1` for ordinary individual rocks. The default is zero.

### Matching terrain color

The surface albedo is `soil texture * Soil Tint * native particle color` by
default. White Soil Tint does not remove the native AGX particle color.
Set **Particle Color Contribution = 0** and **Soil Tint = white** to use the
texture's own base color; 1 retains the original particle-color multiplication.
Intermediate values blend the multiplier toward white. Material/color boundary
rejection during reconstruction still uses the native color at every setting.
Individual rocks keep their own ordinary material.

`TerrainPatchRenderer` chooses Unity TerrainLayers; the surface does not inherit
their diffuse remapping, layer blending, normal maps, or smoothness. For a useful
comparison, use the same diffuse texture and neutral TerrainLayer color remapping,
then match smoothness. **Texture Repeat Distance** is metres per repeat, so start
with the terrain layer's Tile Size (X/Z); triplanar sampling on the moving surface
can still differ from the terrain's mapping. Curved normals and lighting prevent
pixel-identical shading even when the base colors match.

**Texture Mapping = Particle** (the default) samples in each particle's local
frame, so the pattern follows its simulated translation and rotation. Bridges
blend samples from their two endpoints, including individual-rock center anchors.
Texture color is filtered with the surface attributes while native colors still
control material-boundary rejection. Changing neighbors and deforming bridges can
crossfade patterns; this is not a persistent UV mesh. **Texture Mapping = World**
retains the stationary world projection for matching static terrain patterns.

All exposed settings can be changed at runtime. Nonfinite and out-of-range
numeric values are rejected. Mesh instance scale is exactly `2 * physicalRadius`.
Support scale is a **neighbor connection distance**, not a size multiplier.
Two surface particles of the same material connect when their center distance is
at most `SupportRadiusScale * (radiusA + radiusB)`. Their physical spheres are
joined by the external tangent envelope (a capsule for equal radii; a tangent
frustum with spherical end caps for unequal radii). This fills gaps between nearby
particles without extending beyond their convex outer envelope. An isolated
particle keeps exactly the same size at every support scale. Individually rendered
rocks contribute **point anchors at their centers** rather than full spheres.
Nearby soil connects to those points, leaving the rocks protruding through the
surface instead of floating above it. A center already inside a soil sphere needs
no extra bridge. Isolated rocks remain separate, and rock-to-rock links do not
create a surface when all particles are individually rendered.
Increasing support may connect across larger
gaps, so keep it near 1–1.5 for dense soil rather than bridging separated piles.

Sphere input uses the physical collision radius. Mesh input uses the unexpanded,
rotated mesh; its connections use the physical collision spheres. Use sphere
input when the collision envelope should determine the entire soil surface.
Depth smoothing is clamped to the original front/back interval of each sphere or
bridge. Mesh input retains its front depth while smoothing color and normals.
At reduced reconstruction resolution, upsampling still approximates boundaries
within a reconstruction pixel; use full resolution for close contact inspection.

`PostStepForward` only marks the provider dirty. Before a relevant camera renders,
the provider reads the latest completed simulation state; multiple physics steps
coalesce into one readback and one preparation/upload. Additional cameras reuse
that state. Native stepping is synchronous on Unity's main thread, and all native
reads finish before managed rendering jobs start. Code consuming snapshots directly
can call `RefreshIfNeeded()` after stepping. The provider reads position, radius,
rotation, native color, visibility, material UUID when filtering, and the native
**UInt32** ID. There is no identity truncation. The
reference integer hash partitions the snapshot into disjoint sets independent
of movement, array ordering, or removal of other particles. Native flags are
never used to hide the surface subset from other consumers. Pooled proxies are
returned in `finally` blocks. CPU arrays and GPU buffer capacity grow geometrically.
Radius levels in a reusable spatial hash find nearby sphere and center-anchor pairs.
If Burst 1.8 and Collections 2–6 are already installed, an optional assembly runs
the exact neighbor query in parallel Burst jobs. Count/prefix/write passes retain
every qualifying pair without an arbitrary neighbor cap. Double-precision pair
tests and conservative grid bounds keep Mono and Burst consistent at thresholds.
Persistent native buffers
are reused and disposed on renderer disable. Projects without these dependencies,
or with Burst disabled, use the managed implementation; no new dependency is
required by the core AGXUnity assembly. Ordinary/fallback rendering skips bridge
search and soil-buffer upload. No camera means no per-frame acquisition/preparation.

The checked-out native API returns the shared global soil array **by value**.
Providers sharing a native particle system acquire/traverse that array once per
rendered update, dispatching records to their material owners. They filter it by
the terrain's default particle material plus its material patches. This matches
the native `Terrain::hasCreatedParticle` ownership convention. `FilterParticles`
restricts the scope to the default material. If terrains explicitly share a shape
material, that material is drawn once by the first active provider registered;
use distinct particle materials when those terrains need different appearances.
No simulation material is cloned or changed to achieve rendering ownership.
Materials assigned directly through runtime terrain APIs can be listed in the
provider's `AdditionalParticleMaterials` when they have no `TerrainMaterialPatch`.

The terrain's old `DeformableTerrainParticleRenderer` and
`UpsamplingParticleRenderer` are disabled while the surface component owns drawing.
Previously enabled components are restored on disable. Do not manually re-enable
them simultaneously; they draw the whole particle list.

## Rendering order and limits

The feature requests a full depth/normal prepass. At `AfterRenderingPrePasses`
it records physical sphere or mesh input plus tangent bridges into four floating-point targets and a dedicated
depth attachment, applies horizontal/vertical bilateral filters, and adds the
reconstructed depth and normals to the sampled prepass textures. URP 17.4's
Forward+ SSAO runs at `AfterRenderingPrePasses + 1`, so it sees this surface.
The lit composite runs at `AfterRenderingOpaques`, writes the active depth
attachment, and precedes skybox/transparents. Both the sampled camera depth and
active camera depth contain soil at their respective stages.

The shader handles perspective and orthographic projections, ray intersections
at the near plane, GPU projection conventions, reversed depth, and texture
orientation. Reconstruction size follows the camera descriptor, render scale,
resolution setting, and dynamic resolution factors. Texture attributes use
coverage normalization; smoothing retains existing coverage, preserves physical
depth bounds, and rejects samples
behind the scene prepass depth. This avoids merging through foreground shovel
edges. The filter uses 25 samples, projected kernel capped at 24 pixels, continuous
depth weights, and color-boundary rejection. The bounded tangent connections
replace the reference's particle inflation.

The lit composite uses URP's PBR lighting, clustered additional lights, shadow
reception, ambient SH, fog, and particle-relative or world-space triplanar texture coordinates. It
outputs linear HDR color. Unity handles display conversion. Individual rocks
use their own ordinary material and cast/receive shadows. Surface particles are
submitted as ordinary mesh **shadow-only** instances at physical size. These are
approximate shadow proxies: the camera-dependent smoothed surface has no separate
light-space mesh. The bridges do not have separate shadow proxies, so their
shadows remain approximate.

Supported reconstruction is intentionally restricted to URP 17.4 Forward+,
single-eye, unstacked cameras, no MSAA, and disabled depth priming. Deferred,
ordinary Forward, XR, camera stacks, reflections, Built-in, HDRP, unsupported
formats, and missing features use ordinary particle rendering with one warning
per component. These configurations do not silently change pipeline settings.
Scene View follows the same feature and snapshot in Play. Reflection/preview
behavior follows AGX camera filtering and the ordinary fallback.

Depth/normals and SSAO integration are implemented. Particle motion vectors,
temporal reconstruction history, baked GI on the moving surface, APV, decals,
rendering-layer outputs, deferred G-buffers, and XR reconstruction are not provided.
An effect requiring those outputs needs additional integration. The screen-space
surface is opaque and view-dependent, not an exportable triangle mesh.

## Validation and profiling

`Tests/Editor/SoilParticleSurfaceTests.cs` checks native hash vectors, strict
thresholds, endpoint fractions, selection stability, GPU layouts, and bridge
neighbor/material/containment rules. `SoilBridgeValidation.Run` checks the actual
GPU sphere/bridge/filter output against 102 physical hull support directions per
covered pixel, including unequal radii and near-plane intersections. It requires
a graphics device but no AGX license, and exits the isolated editor when done.
The editor `SoilSurfaceValidation.Run` entry point exercises native grading,
captures mode comparisons, checks partition endpoints, toggles settings and
projection, measures CPU acquisition/packing, and checks buffer cleanup.
Run it in an **isolated Unity project with a graphics device and valid AGX license**;
it changes that project's scene and exits the editor when done.

Set `AGX_SOIL_VALIDATION_OUTPUT` to a results directory and launch Unity with
`-batchmode -projectPath <isolated-project> -executeMethod
AGXUnityEditor.Rendering.SoilSurfaceValidation.Run -logFile <log>`.
Do not use `-nographics`. Shader files live under `Resources` for player inclusion.

CPU profiler markers are `AGX Soil.Snapshot` and `AGX Soil.PackUpload`. GPU pass
markers identify Input, FilterHorizontal, FilterVertical, DepthNormals, and
Composite. Snapshot acquisition and PackUpload are separate, non-nested CPU
markers. The remaining readback cost is native ownership/visibility/color/ID and
pose queries. Existing native bulk helpers were benchmarked and were slower on
the tested dense and sparse cases, so scalar readback remains the default.
Measure GPU durations in Unity's GPU Profiler on the target platform;
CPU submission time is not GPU time. Depth, native color and normals use full
precision to retain physical front/back bounds. The additional texture color uses
half precision where supported. Three reconstruction sets require roughly
168 bytes/pixel plus 4 bytes/pixel input depth, before transient aliasing
(196 bytes/pixel including input depth with full-precision texture-color fallback).
A particle GPU record uses 64 bytes; each bridge index pair uses 8 bytes.
Bridges add one instanced procedural draw to the input pass when nonempty.
The ordinary/shadow mesh draws use batches of at most 511 transforms.

Algorithm reference: the approved `agxOSG/implementation/SoilParticleSurface`
checkout and its `data/shaders/soil` shaders. API conventions were checked against
the installed AGX assemblies and URP 17.4 source. See
[AGX Unity terrain rendering](https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#rendering-the-particles)
and [Unity Render Graph passes](https://docs.unity.com/en-us/engine/6000.0/manual/render-pipelines/universal-render-pipeline/customizing-urp/render-graph/write-render-pass).
