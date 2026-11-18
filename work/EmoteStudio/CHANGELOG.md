# Changelog

## beta 0.9.0 — 18 November 2026

Merged into oozy's porter studio with a shared window, installer and top-center tool tabs. Existing Studio projects stay compatible.

## beta 0.0.1 — 18 November 2026

First companion release.

- Import rigged `.blend` models and multiple Porter emote `.blend` or animation JSON files.
- Match Porter rigs automatically, with humanoid retargeting and manual bone mapping for other supported rigs.
- Real textured 3D preview with orbit, zoom, timeline scrubbing, frame stepping and playback speed controls.
- Switch between emotes without adding duplicate armatures to the model.
- Save `.blend`, `.glb` and `.fbx`; keep the library in a portable `.oozy-emotes` project.
- Preserve loop timing and closure interpolation from exported emote data.
- Background Blender processing with progress and cancellation; imported scripts disabled.
- Charcoal, grey and gold interface matching the porter’s Studio theme.
- Self-contained Windows build and single-file installer with install/update/repair detection.

Known limits: arbitrary rigs may require manual mapping; game-specific materials, procedural shaders, cloth simulation and emote props are not automatically reconstructed. Blender and WebView2 are required.
