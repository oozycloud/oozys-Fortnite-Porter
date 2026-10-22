# beta 0.6.0 — Rocket League asset support

Released 22 October 2026. Medium feature update from beta 0.5.0.

## Added

- Rocket League game tab, automatic Epic/standard Steam install detection, separate saved folder and catalogue, cancellation and background scanning.
- Local package catalogue with cars, decals, wheels, goal explosions, maps, balls, boosts, trails, toppers, antennas, paint finishes, engine audio, banners, avatar borders and anthems.
- Car-specific decal styles use known product compatibility. A conservative group of standard cars also offers legacy universal black-market decals. Unverified new packages are searchable but are not advertised as compatible styles.
- Independent car/decal colour presets and custom hex colours, Cobalt/Crimson and other paint tints where the source shader exposes a paint parameter, wheel selection and paint, and eight common reconstructed finish options.
- On-demand previews use the same selected appearance as export, with cached renders. Texture-only packages use an available source thumbnail. Unreal particle playback is not shown.
- Selected wheels mount to four authored car rig positions when supported. Octane now uses its standard body from Startup for regular decals, not the premium-UV alternate.
- Corrected decal mask alpha and removed inherited tiled detail normals from the base car surface.
- Packed Blender exports and the existing GLB, glTF, FBX, OBJ and STL conversion pipeline for mesh assets. Source resources and reports remain alongside the converted scene.
- Recovered diffuse and normal references, editable decal-mask paint reconstruction and embedded images.
- Maps provide a mesh library with available textures. Effects provide available resources and the original selected package. Resource-only packages do not fabricate a Blender scene.
- Rocket League compatibility keys refresh independently in the background at launch, with validated public data and an offline fallback.

## Publication and source

- Finished beta 0.5.0 publication, including installer, portable build, source and verified SHA-256 checksums.
- Corrected release uploader handling of GitHub's normalized installer asset names, preventing duplicate-upload failures.
- Current app/installer source, recovery/build tools, notices and website source are preserved in GitHub. No local game installation or export cache is included in the source repository.

## Validation

- All 241 installed car entries passed the native read/export check with nonempty meshes and no fatal reader errors. This does not imply every custom shader is reproduced.
- Actual installed-package scan, Octane + Stripes decal, standalone ball, wheel, Dueling Dragons resources and AquaDome mesh library checked.
- UI checks cover three-game folder/category isolation, independent saved car/decal colours and decal selection, preview controls, and existing scan/navigation/ID workflows.
- Blender scene checks verify independent colours and finishes, four wheel mounts, painted wheel materials, embedded textures and a generated preview.
- An invalid decal ID is rejected before export. The real Octane + Stripes workflow produces packed Blender and GLB files.
- Installer validation includes runtime-independent launch, payload hashes, repair, updating an earlier build and user-file preservation.

## Limits

This is initial Rocket League resource support. It does not recreate the Unreal level/particle runtime. Maps are not assembled stadiums; goal explosions do not reproduce their complete animated effect. Paint tints, custom decal shaders and animated materials are approximations. The finish list reconstructs common surface properties; it is not all 151 native finish shaders. Compatibility is based on source shader support, not whether each painted variant is obtainable in the game. Previews are generated when requested, not pre-rendered for every catalogue combination. Some dependency references cannot be resolved by UEViewer and are reported. Car/body packages may include variants; package presence does not prove perfect export fidelity. The website ID catalogue remains Fortnite/PCBS2 only.

The app reads installed files and never modifies the game installation. Bundled UEViewer is the Rocket League fork by ShinyEmii, based on Konstantin Nosov's MIT-licensed UE Viewer. See bundled notices and docs/DEVELOPMENT.md.
