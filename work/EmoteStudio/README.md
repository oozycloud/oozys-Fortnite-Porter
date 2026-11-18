# Emote Studio — oozy's porter studio

The animation editor inside oozy’s porter studio. Import a rigged Blender model, add exported emotes, and preview them before saving—without working through Blender’s interface.

## Getting started

1. Run **oozys-Porter-Studio-Installer (beta 0.9.0).exe** and choose an installation folder. The installer offers Repair for an existing copy and Update for an older version.
2. Open **oozys-Porter-Studio (beta 0.9.0).exe** and choose **Emote Studio** at the top.
3. Choose your rigged **.blend** model. Its textures should already be packed or available at the paths stored in the Blender file.
4. Click **Add emotes** and select one or several Porter `.blend` emotes or Porter animation JSON files.
5. Select an emote in the library. Use the timeline to scrub, play, pause, stop, or move one frame at a time. Drag the preview to orbit; scroll to zoom.
6. Save the animated model or save a Studio project to keep the whole emote library.

## File formats

| Format | Contents |
|---|---|
| `.blend` | Model, packed available textures, selected active emote and other imported animation actions |
| `.glb` | Model, compatible materials, embedded textures and selected animation |
| `.fbx` | Model, embedded available textures and selected baked animation; complex Blender shaders may need adjustment in other software |
| `.oozy-emotes` | Portable Studio project with packed base model, working model, cached emote data and selections |

Playback speed changes affect the preview. They do not change saved animation timing. Authored loop metadata is preserved; one-shot clips remain one-shot. A closure key can exist after the last playback frame to keep interpolation smooth.

## Requirements

- Windows x64, minimum window size 1100 × 760.
- Blender 4.4 or newer installed; tested with Blender 5.1.1. Choose **Locate…** if it is not detected.
- Microsoft Edge WebView2 Runtime, normally included with current Windows installations.
- The .NET runtime is included in this build.

Blender runs as an isolated background worker. Imported Blender Python scripts are disabled. Source files are never overwritten by importing or previewing. Save dialogs let you choose the output location.

## Rig compatibility

Porter humanoid rigs are matched automatically. Other humanoid rigs use bone-name matching and rest-pose retargeting; a manual bone map is available when names cannot be matched. Rig proportions, constraints and unusual skeletons vary: check the preview before exporting. Unrigged meshes cannot receive skeletal emotes. This release does not create a skeleton or promise automatic retargeting of every arbitrary rig.

## Troubleshooting

- **No model movement:** select an emote and check the model rig and bone map.
- **Missing texture:** restore the original image files or pack them in the source `.blend` before importing again.
- **Blender not found:** click Locate and select `blender.exe`.
- **Preview materials differ:** the viewer uses glTF-compatible materials; procedural Blender shaders and game-only effects are not reproduced exactly.
- **Interrupted operation:** cancel stops only Studio’s own worker. You can retry; original source files stay intact.

Projects and logs are stored in `%LOCALAPPDATA%\oozycloud\EmoteStudio`. The launcher stays in the installation root; supporting files are inside `app`.

## Source and notices

Source belongs in the companion directories of the [porter repository](https://github.com/oozycloud/oozys-Fortnite-Porter). The build contains Microsoft WebView2 integration, Three.js and Manrope; included notices apply. Blender is a separate dependency. No game models or emotes are included. Use assets you have permission to use.
