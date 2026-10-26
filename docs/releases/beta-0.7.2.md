# beta 0.7.2 — local Zomba animation test build

Prepared 26 October 2026. This build is available locally for testing; it is not yet a published GitHub binary release.

- Recover wheel generator settings and effect attachments instead of exporting only the base tyre/rim mesh.
- Reconstruct Zomba's rim faces using the installed game's `Twirl_RGB_T` mask, `Curve_Rainbow` colour ramp, authored sprite size/offsets and `spinner_jnt` attachment transforms. The four-wheel car export has eight source rim-face layers.
- Add a looping Blender material pulse, independent of scene frame rate, with an editable `ZombaPulseCyclesPerSecond` scene property.
- Pack the source textures into the Blender file. Support the wheel on a car and as a separate asset.
- Detect wheel paint support from native product paint settings as well as material parameter names, enabling supported Zomba paint selections.
- Preserve the beta 0.7.1 fixes for wheel corners, deformation-rig bindings, livery texture selection and clean Material Preview defaults.

Checked the repaired NASCAR Camaro + Zomba scene: eight animated face layers across four wheel anchors, packed source textures, valid drivers, equal playback timing at 24/30 FPS, and visible differences between rendered frames 1 and 8.

The pulse material and its default speed are a reconstruction. The original compiled Unreal shader graph and exact clock were not recovered, so appearance is not claimed to be pixel-identical to Rocket League. Blender material animation is preserved in `.blend`; exchange formats do not preserve Blender material drivers. Other wheel effect/shader families are not automatically supported by this targeted Zomba fix.
