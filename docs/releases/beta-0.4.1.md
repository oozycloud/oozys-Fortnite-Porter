# beta 0.4.1

## Changes

- Added a custom title bar with the app name, minimize, maximize/restore and close controls. Drag the bar to move the window; double-click to maximize or restore it.
- PC Building Simulator 2 now lists PC part models: GPUs, motherboards, CPUs, coolers, fans, cases, RAM, storage, power supplies, radiators, water cooling, cables and fittings.
- Part exports retain enabled mesh hierarchies, local transforms, available base-colour textures and material substitutions used by colour variants. Images are packed into Blender files.
- Removed loose images, workshop props and peripherals from the PC parts catalogue. Fortnite categories remain available in Fortnite mode.
- Kept the self-contained installer and separate application support folder.

## Scope and limitations

The installed game catalogue contains 2,102 part entries across 13 categories. This is static model export: Unity scripts are not executed. Custom shaders, normal maps, runtime component replacements and lighting effects are not fully reconstructed. Export warnings identify these limitations. A complete assembled PC is not reconstructed from a saved game.

Re-scan PC Building Simulator 2 after updating to replace the previous raw mesh/image catalogue. Re-export existing models to use the new hierarchy and materials.
