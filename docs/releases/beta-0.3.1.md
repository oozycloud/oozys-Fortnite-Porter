# beta 0.3.1 — Game tabs and launch compatibility refresh

## Changes

- Added left-side game tabs for Fortnite and PC Building Simulator 2, with separate saved paths and selection reset when switching.
- Added early Unity asset browsing: static meshes, Texture2D images and sprites from an installed PC Building Simulator 2 copy. Images export to PNG; meshes export to Blender and GLB, glTF, FBX, OBJ or STL. The Python runtime and reader are bundled.
- Added a background Fortnite AES/mapping refresh at every normal launch. Scanning reuses a recent refresh, and custom overrides are preserved.
- Validate keys and matching mapping versions before replacing the saved compatibility pair. Offline or invalid service responses keep the last valid pair and show a saved-data status.
- Fixed exchange conversion failures caused by long Windows paths. Conversion uses a short temporary working folder and shortens output filenames where needed.
- Game-specific category shortcuts and export help replace Fortnite-only controls in the Unity tab.

## Validation

- Scanned 3,063 local PC Building Simulator 2 asset files: 26,083 meshes, 17,278 textures and 7,321 sprites, with no read warnings.
- Exported two GPU meshes, a texture and a sprite. Tested all five mesh exchange formats.
- Compatibility checks cover fresh download, shared recent results, offline service, mismatched builds, malformed JSON, invalid keys, invalid mapping data, cancellation and missing cache. A live UEDB download also passed.
- Saved-file validation reopened two Blender meshes and two PNG images, then reimported GLB, glTF, FBX, OBJ and STL with matching face counts.
- A fresh Fortnite scan using the new compatibility cache found 19,710 assets across 20 categories.
- UI checks cover separate game paths and cleared selections, alongside existing dropdown, theme, style, multi-selection and progress checks.

## Limitations

PC Building Simulator 2 support is experimental and exports geometry and images separately. It does not assemble PCs, assign materials, reconstruct rigs or export animations. Stumble Guys is not included.

Existing Fortnite material, reactive-effect and cloth limitations remain, including Marge’s raised-knee dress clipping. This update does not claim to fix every cosmetic. Exchange formats simplify Blender-specific materials and effects.

## Installation

Run `oozys-Fortnite-Porter-Installer (beta 0.3.1).exe`, select the existing installation and choose Update. Keep `app` beside `oozys-Fortnite-Porter (beta 0.3.1).exe`. The installer includes .NET and the Unity reader runtime.
