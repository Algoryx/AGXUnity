# Validation results

## Collision boundary adhesion (15 October 2026)

Boundary adhesion adds clipped collision-surface footprints and tangent faces
joining them to nearby physical spheres. The input shader clips reconstructed
geometry against those boundary constraints and locks constrained input depth
through smoothing. Native simulation state is unchanged. Supported sources are
AGX boxes, planes, collision meshes (including precomputed pieces), height fields,
deformable terrain and Unity collision terrain. Automatic discovery excludes
disabled collisions and sensors; explicit source selection is available. Other
AGX primitives and complete AGX collision-filter replication are outside this
implementation. Open mesh winding must be consistent; local nearest-surface
sidedness is not a general volumetric classifier for nonmanifold triangle soups.

The implementation caches rigid-local triangle BVHs, nearby 8x8-cell terrain
tiles, collider-local footprints and reusable GPU buffers. Terrain height/hole
notifications invalidate affected caches. Footprints refresh after relative
movement exceeds 1% of particle radius, after geometry/settings changes, or when
an attachment changes. Attachments use a 15%-of-radius release margin. Buried
particle centers do not create new footprints through solid surfaces. The
existing optional Burst assembly batches proximity queries over copied data;
unchanged geometry remains resident in persistent native arrays. A managed
fallback is retained, and no package dependency or plugin DLL was changed.

The edit-mode suite covers GPU layouts, BVH-versus-exhaustive queries, transformed
and updated managed/Burst proximity results, attachment hysteresis and reset,
sensor/collision exclusion, box dimensions, rigid motion, precomputed AGX mesh
handedness/scaling, closed-mesh orientation, and live terrain heights/holes. The
existing particle/surface suite is included (43 tests total), including removing
an explicit boundary without any particle movement.

D3D12 GPU regressions passed actual sphere/ground gap filling, plane/wall corners,
mesh bucket floors/walls and translated/rotated buckets. The adhesion case filled
148 pixels outside the physical sphere while retaining the ground constraint.
All existing hull, texture detail/motion and smoothing checks also passed. All
eight shader passes compiled without shader messages in the final GPU run.

For 1,024 particles attached to a plane, warm cached CPU preparation measured
0.9599 ms; rebuilding footprints during relative movement measured 3.2043 ms;
cached preparation plus GPU uploads measured 0.9351 ms. These are separate warm
runs, so the small timing difference between cached measurements is noise.
They exclude GPU drawing and native particle acquisition.

On an identical frozen native grading snapshot, boundary preparation measured
11.6445 ms managed versus 2.7712 ms with Burst (480 attached particles, 8,724
footprint triangles, 2,335 queries). Both paths attached the same particles. The
real URP integration passed automatic terrain conformance, runtime disable/restore,
multi-camera/terrain rendering, resizing, fallback and disable/re-enable cleanup,
without runtime or Render Graph errors. Later in that run, total pack/upload was
4.9217 ms for 1,614 particles/anchors, including 9,509 boundary patches; the entire
GPU input pass measured 0.3281 ms/draw over 34 samples at 960x640. Geometry, contact
count and hardware affect these costs; cold BVH creation and moving/deforming
boundary updates are not represented by the warm timings above.

Reports and before/after captures are in `SoilBoundaryValidation` and its `Native`
subdirectory in the task's visualization artifacts. Earlier timing tables predate
boundary adhesion.

## Connection hysteresis (15 October 2026)

`Connection Hysteresis` defaults to 0.05. Edges join at the existing support
distance and release only beyond 105% of it. An edge outside the join threshold
can only be retained if it existed in the previous rendered update. Faces inherit
the stabilized edge graph. Physical sphere radii, particle motion and texture
sampling are unchanged. This targets topology changes at the radius threshold;
it does not freeze moving geometry or lighting.

History stores particle IDs and validated cached indices, so packing/reordering
does not attach old connections to unrelated particles. Missing IDs, incompatible
materials, two-rock-anchor pairs and contained pairs are discarded. Empty input,
disabled hysteresis, renderer disable and ordinary/fallback preparation clear the
history. Support/hysteresis setting changes request a reset on the next pack.
Optional Burst retention checks only existing edges; the neighbor grid is not
widened. Persistent storage grows only when required and is released on disable.

All 32 edit-mode tests passed, including managed/Burst jitter sequences,
release/rejoin, disabled/reset history, reordering, replacement/removal, buffer
growth, material and anchor rejection, and stable tetrahedron faces over 40
alternating threshold crossings. D3D12 geometry, physical hull, texture detail and
texture-motion regressions also passed.

On the 8,192-particle lattice, a 1% expansion reduced fresh connections from
101,086 to 99,918. With 5% hysteresis all 101,086 original connections persisted;
every Burst pair matched the managed result, without duplicates. Neighbor search
plus retention took 2.4665 ms compared with 1.6786 ms without history, an additional
0.7879 ms over 50 warm iterations. This excludes face building and rendering.
Retained edges can also retain more faces, so additional GPU work depends on the
particle layout and hysteresis setting.

Reports: `SoilHysteresisValidation/editmode-complete.xml` and `bridges.txt` in the
task's visualization artifacts.
The real URP grading integration also passed runtime hysteresis changes,
multi-camera reuse, empty inputs, fallback, disable/re-enable and resource cleanup,
with no runtime or Render Graph errors. Its report and captures are in
`SoilHysteresisValidation/Native`.

## Filled local faces (15 October 2026)

Mutual-neighbor triples now contribute the two external tangent triangles of
their physical spheres, alongside the existing sphere and pair envelopes. The
radius rule applies to every edge, and material boundaries are retained. This
fills triangular openings and gives four mutually neighboring, noncoplanar
particles a closed convex shape. It does not construct a global hull over a pile.
Unequal radii and individual-rock center anchors are supported. Faces retain
their planar depth through smoothing; spline-shaped dips are not implemented.

The D3D12 GPU checks passed filled 7x7 interior patches at the expected plane
depth for triangles and tetrahedra, including maximum smoothing. Perspective,
reverse views, near-plane clipping, unequal radii and center anchors passed the
physical hull checks. Nonuniform face textures retained their detail and followed
particle translation/rotation. All seven shader passes compiled without errors.
The edit-mode suite passed 24/24 tests, including tetrahedron topology, unique
face indices, pair-order independence, radius limits and material boundaries.

Face enumeration uses reusable forward adjacency and an optional Burst job in
the existing optional assembly. On the previous 8,192-particle lattice, 101,086
pairs produce 385,503 index triangles. Every Burst triangle matched the managed
fallback in the same order. Warm face enumeration and managed output copy took
4.30 ms with Burst, compared with 15.40 ms for the initial managed implementation.
These are additional CPU costs, not whole-frame timings. Dense local cliques can
produce many faces; the dense benchmark's additional GPU cost was not measured.
The GPU index buffer uses 12 bytes per triangle, rounded up to retained capacity.
Buffers are reused between rendered updates and disposed when disabled.

Results: `SoilFaceValidation/bridges.txt` and `editmode.xml` in the task's
visualization artifacts. Earlier CPU/GPU timing tables predate filled faces.

The real native grading/URP integration also passed, including multi-camera reuse,
fallbacks, resizing, disable/re-enable and face-buffer cleanup, with no runtime or
Render Graph errors. Its later state uploaded 15,253 faces for 1,756 particles and
anchors; total CPU pack/upload was 1.3354 ms. The entire input pass (particles,
pair envelopes and faces) measured 0.3444 ms GPU over 34 samples at 960x640. These
scene measurements do not characterize the much denser synthetic lattice above.
Integration reports and captures are in `SoilFaceValidation/Native`.

## Texture detail under smoothing (15 October 2026)

The geometry filter now copies the center texture color through each pass instead
of averaging neighboring texture samples. Depth, normals and native particle
color still use bilateral smoothing; bridge endpoint texture blending is retained.
This also removes the per-neighbor albedo fetch from the filter loop.

The Unity 6000.4 / D3D12 GPU regression compared unfiltered texture color with both
filter passes at maximum Smoothing (2). All 3,580 covered, nonuniform pixels,
including the bridge, matched within a summed RGB tolerance of 0.00001. Existing
translation, rotation, physical hull, center-anchor and near-plane checks passed,
and all six shader passes compiled without shader errors. Results are recorded in
`SoilSmoothingValidation/bridges.txt` in the task's visualization artifacts.

## CPU optimization (15 October 2026)

Physics-step callbacks now mark pending work without readback, neighbor queries,
matrix generation or uploads. Rendering requests the latest completed state and
shares it between cameras. Providers sharing the native granular system dispatch
one global-array traversal to their material owners. Native objects stay on the
main thread; only copied particle records reach the rendering jobs.

An optional Burst/Collections assembly accelerates exact neighbor queries with
parallel counting, prefix offsets and disjoint output writes. No connections are
dropped or approximated. Persistent native storage is released on disable and
recreated on re-enable. The core assembly retains a managed fallback and does not
add package dependencies. Ordinary/fallback cameras skip bridge construction and
soil-buffer upload. Native acquisition and PackUpload now have separate CPU markers.

An earlier frozen-native-state comparison, before the final threshold precision
hardening, contained 1,634 visible owned particles among 4,662 global entries:

| Identical-input measurement | CPU milliseconds |
| --- | ---: |
| Managed bridge search | 5.4265 |
| Burst bridge search | 0.2996 |
| Pack, exact accelerated bridges and upload | 0.7548 |
| Shared native acquisition/dispatch | 3.2287 |
| Acquisition plus preparation | 3.9835 |

That comparison was **18.11× faster** with exactly the same output pairs.
Particle counts vary between native grading runs; earlier 7,000-particle figures
must not be compared directly with this smaller scene. The remaining dominant
cost is scalar native readback, not the accelerated bridge query.

The **final algorithm** was also measured on an 8,192-particle synthetic dense
soil lattice with mixed radii and 5% individual-rock center anchors. Managed and
Burst searches produced exactly the same **101,086 unique pairs**. After warmup,
managed search averaged **40.1119 ms** and Burst averaged **1.9481 ms**:
**20.59× faster** on identical input. This measures connection search including
native-container copies and job completion, not native acquisition or GPU draws.

The large test exposed Mono/Burst differences in float intermediates at exact
distance thresholds. Both implementations now use explicit double precision for
the physical pair predicate, with conservative, position-scaled float bounds for
grid lookup. Only lookup bounds widen; particle radii and the physical predicate
do not. Exhaustive tangent-lattice tests at world offsets 0, 17 and 120 passed
alongside mixed-material/anchor comparisons, dense output growth/shrink tests,
invalid-grid guards, layout/hash tests, and quaternion checks: **23/23 tests**.

Existing bulk helpers were evaluated rather than assumed faster. At 2,267 native
and visible entries, coordinated scalar / bulk position-radius / full bulk-matrix
readback took 2.4872 / 2.6113 / 2.9847 ms. At 4,662 global and 1,634 visible owned
entries they took 3.2762 / 3.5874 / 4.2732 ms. The final code therefore defaults to
scalar reads. The historical scalar-provider benchmark matched every record and
took 3.2157 ms versus 3.2287 ms for the new coordinator with one provider: there is
no claimed per-particle acquisition speedup. The gain here is avoiding repeated
work between physics steps, cameras, and providers. Native IDs, colors, visibility
and material ownership remain exact; no AGX/plugin DLL was changed.

Integration checks proved that 120 explicit physics steps perform no acquisition
or packing, repeated refresh requests reuse the capture, and two terrain providers
use one native traversal with disjoint ownership. Physical transforms, all render
modes, empty/disable/re-enable behavior, and Render Graph error checks passed.
Artifacts are under `SoilOptimization/Delivery` beside earlier captures. Steady-state
managed allocation totals remain unverified because this runtime's allocation counter
fails its known-allocation probe.

The final Windows development player built with zero errors; its warnings are
recorded in `SoilOptimization/Standalone/build-complete.log`. The D3D12 player
smoke check confirmed visible geometry, supported reconstruction and **Burst Jobs**
as the actual backend, with 2,331 eligible particles (2,212 surface and 119 rocks).
It recorded 135 snapshot captures and 134 pack/upload calls. Its offscreen request
and normal camera reused the same data. Player output, the 8,192-particle report
and build diagnostics are under `SoilOptimization/Standalone`. Temporary AGX
license copies used for testing were removed; the original was unchanged.

## Particle-relative texture mapping (15 October 2026)

Particle-local triplanar sampling replaces fixed world coordinates by default.
The input pass samples each particle's current inverse transform, and bridges
blend their endpoints' samples. Texture color travels through an additional
reconstruction target and both filters. World projection remains selectable.
Native-color filtering and the Particle Color Contribution control are separate.

The four-target path requires four MRT attachments. Its working set is about
105.7 MB at 960×640 or 26.4 MB at half resolution before transient aliasing, using
half-precision texture color. Full-precision fallback uses 120.4 / 30.1 MB.
Earlier timing and memory tables below predate the texture target.

The D3D12 GPU regression passed nonuniform-texture comparisons after translating
the particles/camera together and rotating them together by 67 degrees. All
3,580 covered pixels, including the smoothed bridge, retained their local texture
within a summed RGB tolerance of 0.015. All shader passes compiled. The native
URP grading run passed both mapping modes, sphere/mesh input, occlusion, camera
and resolution changes, fallback, and buffer cleanup without rendering errors.
Reports are saved under `SoilTextureValidation` beside the earlier artifacts.

At 960×640 in the final larger grading capture, GPU pass averages over 34 samples
were input 1.0665 ms, horizontal/vertical filtering 0.0589/0.0551 ms,
depth/normals 0.0206 ms and composite 0.0227 ms (1.2238 ms total).
CPU packing/search/upload remained substantial: 43.2877 ms for approximately
6,500 surface particles plus center anchors and 45,912 bridges. This adds GPU
texture sampling and storage, rather than changing the CPU neighbor algorithm.

## Physical tangent surface and rock-center anchors

The current renderer replaces particle inflation with local tangent hulls of the
physical spheres. Individual rocks enter the connection graph as zero-radius
points at their centers, while keeping their physical radii for neighbor search.
Only sphere-sphere and sphere-point links produce soil; point-point links do not.
The front/back depth interval constrains both smoothing passes.

Nine Unity Edit-mode tests passed, including GPU record layouts, bridge reach,
containment/material rejection, rock-center anchoring, and comparison of the
multilevel spatial hash against exhaustive mixed-radius pair enumeration.

Direct GPU regressions passed on D3D11 and D3D12 for physical sphere/tangent
boundaries. The D3D12 suite additionally exercises the point-anchor cones. Each
covered pixel is checked against 102 support directions of the physical hull.
The suite covers equal/unequal radii, perspective/orthographic projections,
near-plane intersections, strong smoothing, filled central gaps, and pixel-exact
isolated-particle invariance when support changes from 1 to 3.

The native grading integration passed on D3D12 with both sphere and mesh input,
mixed and full surface modes, multiple cameras/terrains, scene-depth occlusion,
half resolution, resize, MSAA fallback, empty snapshots, and release/recreation
of both particle and bridge buffers. Full-resolution GPU bounds checks use a
2 mm numerical tolerance; reduced-resolution upsampling remains an approximation
within a reconstruction pixel. Shadow proxies do not include the bridges.

Current connections add one instanced draw to the input pass, for six raster
draws per nonempty terrain/camera. Attributes now retain full-precision front/back
bounds: the target working set is about 90.9 MB at 960×640 and 22.7 MB at half
resolution before Render Graph aliasing. Bridge records use 8 bytes each; the
64-byte particle upload also contains the individual-rock center anchors.
Neighbor search is additional CPU work and its cost depends on local density,
radius distribution, support scale, and particle count. See the current captured
CPU/GPU measurements in `SoilBridgeValidation/Delivery/validation.txt`.

On the RTX 4070 Ti SUPER validation host, the final D3D12 multi-terrain exercise
had 6,803 surface particles plus center anchors, 585,241 neighbor candidates and
46,484 bridges. Measured after warmup, excluding simulation and camera submission:

| Current measurement | Observed |
| --- | ---: |
| CPU bridge search alone | 42.8557 ms/update |
| CPU packing, bridge search and GPU upload | 44.1418 ms/update |
| Native snapshot plus packing/search/upload | 54.7168 ms/update |
| GPU input, including bridges | 0.4716 ms |
| GPU horizontal / vertical filter | 0.0341 / 0.0306 ms |
| GPU depth/normals / composite | 0.0086 / 0.0132 ms |
| Total GPU reconstruction, 34 samples at 960×640 | 0.5581 ms |

The CPU search is substantial in this larger case. These measurements establish
the cost rather than a real-time performance guarantee; support distance and
dense neighborhoods increase it. The managed allocation counter still fails its
known-allocation probe, so steady-state allocations remain unverified.

Current test XML, GPU-bound reports, grading captures and logs are saved in
`C:/Users/Joakim/.codex/visualizations/2026/10/14/01a13a47-574d-7073-a99d-10271af040fa/SoilBridgeValidation`.
No native/plugin DLL was changed. A standalone player build was validated for the
earlier implementation; this revision was validated in the Unity editor.

## Historical baseline: original inflated-support renderer

The remaining results below describe the earlier implementation, before bounded
tangent connections and center anchors. Its performance and memory figures do
not describe the current renderer.

Validated on 14 October 2026 with Unity 6000.4.0f1, URP 17.4.0,
Windows Direct3D 11 (reversed Z), and an NVIDIA GeForce RTX 4070 Ti SUPER.
The tests ran in an isolated temporary Unity project using this repository and
the original project's installed packages and local AGX license.

## Implemented files

- `AGXUnity/Rendering/TerrainParticleSnapshotProvider.cs`: synchronized acquisition,
  material ownership, native visibility, handedness conversion, pooled proxies.
- `AGXUnity/Rendering/SoilParticleSelection.cs`: reference UInt32 hash.
- `AGXUnity/Rendering/TerrainParticleSurfaceRenderer.cs`: controls, partition,
  shared upload, individual meshes, shadow proxies, ordinary fallback, lifecycle.
- `AGXUnity/Rendering/URP/TerrainParticleSurfaceFeature.cs`: Render Graph passes,
  sampled depth/normal updates for SSAO, opaque composite and capability checks.
- `Resources/Shaders/URP/TerrainParticleSurface.shader`: sphere/mesh input,
  coverage-aware filtering, material boundaries, depth reconstruction, triplanar
  texturing, URP PBR lighting and shadow reception.
- `Editor/Rendering/URP`: example builder and isolated validation/build harness.
- `Tests/Editor/SoilParticleSurfaceTests.cs`: hash and GPU record layout tests.
- `Samples/SoilSurface/SoilSurfaceGrading.unity`: native grading example with
  connected soil, normalized jagged mesh, textured terrain and 5% individual rocks.

## Checks

Five Unity Edit-mode tests passed: three reference hash vectors, strict selection
thresholds/endpoints/distribution and surviving-ID stability, and the 64-byte
CPU/HLSL layout. The 5% statistical test selected 5,134 of 100,000 IDs.

The Play-mode harness created approximately 2,400 eligible particles by 2.7 seconds
of native simulation. The following integration checks passed:

- Native X handedness, radius retention, unit-diameter rock scale.
- Sphere input and actual rotated/scaled triangle mesh input.
- Fractions 0, 0.05 and 1; complete disjoint partitions.
- Foreground shovel occlusion and a transparent pane among/behind the soil.
- Perspective, orthographic and near-plane intersection captures.
- Resize from 960×640 to 640×480, and half-resolution reconstruction.
- Runtime mode, fraction, smoothing and resolution changes; invalid numeric values.
- Two cameras sharing one snapshot without native reacquisition.
- Two native terrains with disjoint material-scoped particle ownership.
- Ordinary-particle fallback with MSAA enabled.
- Empty snapshots, GPU buffer release and disable/re-enable resource recreation.
- No surface runtime, Render Graph or shader compilation errors in the completed run.

Image checks assert that output contains geometry and are supplemented by manual
inspection of comparison captures. They are not pixel-exact tests of every lighting
or occlusion case. Main and additional lighting, main shadows and separate rock
appearance were exercised in the grading scene. Surface shadow proxies remain an
approximation and can differ from the enlarged reconstructed silhouette.

Windows development player builds succeeded with zero errors. The build log also
contains existing project warnings, predominantly from optional OpenPLX test
resources. The surface shader's earlier tangent warning was corrected; all five
surface passes were included in the player shader binary.

The final standalone smoke test also passed its image-content check: reconstruction
was supported, the captured image contained visible geometry, and 1,928 eligible
particles were partitioned into 1,831 surface particles and 97 individual rocks.
The local player artifact requires the user's normal AGX license configuration;
temporary license copies used by validation were removed after testing.

## Measurements

CPU acquisition and upload were measured after warmup without simulation time or
camera rendering. These are small-scene measurements, not a large-particle stress
test or a guarantee for another machine.

| Measurement | Observed |
| --- | --- |
| Packing/upload, mixed rendering after multi-terrain exercise | 0.34–0.39 ms/update |
| Snapshot plus packing/upload | 3.71–4.61 ms/update |
| Acquisition estimate (difference of separate averages) | 3.37–4.25 ms/update |
| Particle GPU buffer capacity | 4,096 × 64 bytes = 256 KiB |
| Typical valid surface upload in the later capture | Approximately 100 KiB/update |
| Reconstruction targets at 960×640, before aliasing | Approximately 61.4 MB/terrain |
| Reconstruction targets at half resolution | Approximately 15.4 MB/terrain |

Earlier GPU recordings at support scale 1.15 produced these per-pass timings,
averaged over 34 samples at 960×640:

| Pass | GPU milliseconds |
| --- | ---: |
| Mesh input | 0.0558 |
| Horizontal filter | 0.0490 |
| Vertical filter | 0.0449 |
| Depth/normals | 0.0178 |
| Lit composite | 0.0258 |
| Total reconstruction | 0.1933 |

The final jagged-mesh example uses support scale 1.5 and smoothing 1 to close gaps
between the prototype's smaller inscribed surfaces. GPU timestamps were unavailable
in subsequent batch recordings; the earlier timing table must not be treated as a
measurement of that larger preset. A target-platform GPU Profiler capture remains
necessary for reliable final-preset timings and complete frame draw-call counts.

Reconstruction issues five raster draws per nonempty terrain/camera. Ordinary rock
and shadow-proxy submissions use at most 511 matrices per batch. Actual shadow-pass
draws vary with lights, cascades, faces and culling and were not counted with a
Frame Debugger capture. Managed arrays reserve roughly 256 bytes per capacity slot
across the provider record array, packed array and two transform arrays.

Unity's `GC.GetAllocatedBytesForCurrentThread()` returned zero even for a known
4 KiB allocation in this runtime. Steady-state managed allocation totals are
therefore **unverified**, not zero. Scalar acquisition remains the dominant CPU
cost. A native bulk API is a possible later optimization; no AGX/plugin DLL was
changed for this implementation.

## Support and remaining limits

Verified reconstruction support is Windows/D3D11, Unity 6000.4, URP 17.4 Forward+,
single-eye unstacked cameras, disabled MSAA and disabled depth priming. Linux,
other graphics APIs and dynamic-resolution changes were not exercised. Their
projection/resource code exists but they are not claimed as verified platforms.
Deferred, ordinary Forward, XR, stacks, Built-in and HDRP use ordinary particles.

Native particle ownership is represented by material identity. Explicitly shared
materials form one render group assigned to the first active provider; use distinct
particle materials for independent terrain appearances. Direct runtime material
assignments can be registered with `AdditionalParticleMaterials`.

There are no particle motion vectors, temporal history, APV/decals/rendering-layer
outputs, deferred G-buffers or XR reconstruction. The surface is view-dependent,
opaque and cannot be exported as a triangle mesh. See `README.md` for setup and
the configured example controls.

Comparison PNGs, logs, test XML, player output and measurements are saved locally in
`C:/Users/Joakim/.codex/visualizations/2026/10/14/01a13a47-574d-7073-a99d-10271af040fa/SoilSurfaceValidation`.
