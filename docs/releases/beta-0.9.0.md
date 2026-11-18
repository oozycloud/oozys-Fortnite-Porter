# oozy's porter studio — beta 0.9.0

18 November 2026. A medium update from Porter beta 0.8.0, combining the companion editor into the main app.

## Shared workspace

One launcher and installer now contain two tools: **Porter** and **Emote Studio**. Their buttons sit at the very top, centered against the full window. Switching tabs preserves loaded assets, editor projects and previews. The window uses one titlebar, product name and version.

The editor imports rigged `.blend` models and multiple exported emotes, offers textured playback, orbit, zoom and timeline controls, and saves BLEND/GLB/FBX or portable Studio projects. Available textures are packed; original files remain separate from the working project.

## Installation

Run `oozys-Porter-Studio-Installer (beta 0.9.0).exe`. Open `oozys-Porter-Studio (beta 0.9.0).exe` afterward. Supporting files stay inside `app`; .NET is included. Blender 4.4+ and WebView2 are required for the editor. Existing Porter install records and settings are retained for compatibility.

## Limits

Custom skeletons may need manual bone mapping. Preview materials use glTF-compatible shading; game-specific effects and procedural Blender materials can differ. This integration does not claim to resolve every pre-existing game-asset fidelity issue.
