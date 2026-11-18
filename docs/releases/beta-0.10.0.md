# oozy's porter studio — beta 0.10.0

18 November 2026. A medium update from beta 0.9.0.

## Browse and export

The library uses image cards rather than rows. Click a card to open an export overlay inside the existing window. Choose styles and file format, then export; progress and cancellation remain visible in the overlay. Close with Back to library, the backdrop or Escape. Ctrl-click several cards and choose Review selection for a batch. Exact ID lookup remains available in its expandable section.

Combined skin + emote mode has been removed from Porter. Use the separate Emote Studio tool to apply exported emotes to a rigged model.

The gallery realizes only nearby card rows. It uses source artwork for Fortnite and registered PC parts, locally extracts Rocket League thumbnail packages and caches decoded images. Where a selected model has no source icon, supported PC-part/Rocket League geometry can be rendered in the background. Assets without artwork or supported geometry display an explicit unavailable state.

## Rocket League

Car exports now include an optional editable wheel rig on the original car armature. Each corner has `wheel_FL_move`, `wheel_FL_steer` and `wheel_FL_roll` controls (FR, RL and RR for the other corners). Select the car's wheel rig and enter Pose Mode. Move with G; steer or roll around the control's local Y axis with R, Y, Y. Tyre, rim, animated face and original mount follow together. The source skeleton remains in a hidden bone collection; a help text is included in the Blender file.

The exporter now chooses the actual decal material rather than a chassis material with more parameters. Legacy team masks use authored colour channels; newer full-colour livery textures are recognized. Static paint colour and surface maps are baked into packed textures, improving Blender/GLB/FBX compatibility.

Tested: Octane with legacy Dignitas and NRG, Fennec with Dignitas 2024, default blue paint, Zomba native thumbnail, four independent wheel positions and rotation, and GLB paint/surface texture references. A 25 cm front-left move left the front-right wheel unchanged; steering and rolling were verified separately.

## Requirements and limits

Windows x64, Blender 4.4+ (tested with 5.1.1), WebView2 Runtime. .NET is included. Four named source mounts are required for automatic wheel rigging; unsupported cars retain their original armature with an export warning. Complex universal/animated Unreal shaders and material drivers are not reproduced identically by every exchange format; Blender retains supported material animation. This update does not claim every game's materials or every team livery has been verified.
