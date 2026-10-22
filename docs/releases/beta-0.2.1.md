# beta 0.2.1 — Weapons, builds, vehicles and exchange formats

## Changes

- Added Weapons with rarity labels and definition-specific mesh selection.
- Added Builds with individually selectable wood, brick and metal pieces and authored edits. Door exports include the separate door mesh and its offset.
- Added Vehicles from vehicle definitions, including component transforms, inherited mesh components and material overrides.
- Retained and tested Kicks, including the mutable shoe meshes and textures.
- Added GLB, glTF, FBX, OBJ and STL alongside the packed Blender scene. GLB/glTF/FBX support the active animation; OBJ and STL export static geometry.
- Added optional triangulation, animation inclusion and texture limits of 512, 1024, 2048 or 4096 pixels for exchange files. The original Blender file stays unchanged.
- Fixed separate glTF image filenames exceeding Windows path limits and OBJ references to packed image paths.
- Recovered the application, installer and embedded Blender scripts from the installed beta after the original source was deleted. A separate source backup accompanies this build.

## Validation

- Recovered app and installer compile. Native UI checks exercise dark dropdowns, all seven format choices, saved export options, themes, multi-selection, style channels and progress.
- Fifteen fresh exports complete with textures and zero warnings. The initial nine cover: Rare and Legendary Assault Rifle, wood door, brick stair edit, metal cone, Whiplash, Trail Thrasher Dirt Bike, ATK and Squiddy Steppers.
- Six additional build exports cover a wood wall, floor, stairs and edited cone, a brick arch, and a metal window.
- Packaged installation, update from beta 0.1.1, repair, user-file preservation, launcher metadata and runtime-independent UI checks pass.
- Saved Blender scenes reopen with geometry and packed images. Four sample renders were visually inspected.
- All five exchange formats reimport with geometry; textured formats have images and GLB/glTF/FBX preserve rigs.
- A skin with an active emote exports and reimports through GLB, glTF and FBX with skinning, moving poses and the same animation duration.

## Scope and limitations

These are asset exports, not a playable Fortnite simulation. Vehicle driving, physics, runtime paint selection and gameplay effects are not recreated. Some vehicle materials approximate their in-game appearance. Building edits are separate authored meshes, not an interactive editing game.

Custom toon shaders, cloth, procedural materials and effects may simplify in exchange formats. Use the retained Blender file for the richest result. STL contains geometry only, uses millimetres and is not guaranteed watertight. Marge’s previously documented dress clipping and other shader/reactive limitations remain.

## Install or update

Run `oozys-Fortnite-Porter-Installer (beta 0.2.1).exe`. Choose an existing installation to update it after closing the app. The runtime is bundled. Keep `app` beside `oozys-Fortnite-Porter (beta 0.2.1).exe`. Re-export assets to use these changes.
