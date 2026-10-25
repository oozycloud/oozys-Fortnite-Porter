# beta 0.7.0 — Rocket League scenes and timelines

Released 25 October 2026. Medium feature update from beta 0.6.0.

## Changed

- Map export follows authored streamed-level references and places linked mesh instances using source translation, rotation, scale and material overrides. Local component archetypes are resolved before placement.
- A native object reader preserves complete object references and exports flat numeric payload filenames, avoiding Windows path-length failures that previously skipped deeply nested templates.
- Effect exports read FXActor attachments. Supported skeletal animations attach to the matching mesh, with source playback rates and independent spawn delays. Dueling Dragons now has two animated, separately positioned dragons with their blue/pink material parameters.
- Supported Cascade sprite emitters bake source bursts/rates, lifetime, velocity, acceleration, size and colour distributions into editable Blender animation. Unsupported modules are listed in the report.
- Animated UberSkin noise parameters create Blender UV panning drivers. Heatwave now animates using its authored texture scales and pan speeds while retaining independent selected car/decal colours.
- Preserved decoded emitter/material properties accompany exports. Resource-only effects do not create an empty model scene.
- Added reproducible source/build instructions for the MIT-licensed native object reader.

## Checked

- AquaDome: persistent level plus six streamed sublevels, 411 placed mesh instances, 282 distinct object origins, six large boost pads at source positions, and 110 packed Blender images.
- Dueling Dragons: two matching animated rigs, source-specific animation selections/rates/delays, changing poses on the Blender timeline, blue/pink source colours, and 66 baked sprite particles.
- Heatwave: authored material parameters recovered, valid frame-rate-aware panning drivers change across frames, with four configured wheel mounts retained.
- Six property/animation regression tests cover nested tagged structures, truncation, float lookup tables, inherited false/zero values, archetype cycles, and animation timing/bone mapping.

## Remaining limits

These scenes are reconstructions, not a complete Unreal runtime. Maps retain geometry and placements; baked lighting, gameplay actors and runtime effects are not recreated. Three AquaDome mesh references point to absent standalone packages and are reported. Instanced-mesh native payloads, custom level types and externally inherited component templates may require further support.

Effect support varies by module. Ribbons, mesh particles, orbit motion, event spawning, collision, sub-UV playback and some velocity/location modules are not fully reconstructed. Infinite emitters bake one cycle and sprite output is capped at 1,000 particles; the report identifies those limits. Sprite materials, camera alignment and custom glowing surfaces are approximations. Sound, camera shake and Unreal post-processing are retained as source definitions rather than simulated.

Animated UberSkin reconstruction supports the recovered noise-pan parameter family; it does not translate every compiled shader graph. Eight common paint finishes remain approximations. The website ID catalogue remains Fortnite/PCBS2 only. See export reports for each asset's actual result.
