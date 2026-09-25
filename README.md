<p align="center"><img src="docs/assets/porter.png" width="96" height="96" alt="oozy’s Fortnite Porter icon"></p>

# oozy's Fortnite Porter

Bring Fortnite characters, animations and props into Blender, with packed textures and editable rigs.

**Windows x64 · Blender workflow · Beta**

· [Release history](CHANGELOG.md) · [Report a problem](https://github.com/oozycloud/oozys-Fortnite-Porter/issues)

> **Current build: beta 0.1.1.** Installer and complete application files are packaged locally. GitHub availability depends on publication; see [release notes](docs/releases/beta-0.1.1.md) for changes and known limitations.

## What it does

- Exports skins, pickaxes, back blings, gliders, emotes, emoticons, sidekicks and prefabs from a local Fortnite installation.
- Creates a `.blend` with the model, rig, materials and texture images packed together.
- Combines a skin and emote, or applies a separately exported emote to an existing skin using the Blender tools.
- Supports multiple selections, authored cosmetic styles, animation resampling and selected animated material effects.
- Provides an orange-and-black interface, dark dropdowns, hover help, animated backgrounds and reduced motion.
- Installs through one self-contained EXE, with supporting application files kept in an `app` folder.

This is a Blender reconstruction of Fortnite assets. Support varies by asset; the exporter does not reproduce the entire Unreal material, physics or gameplay runtime.

## Requirements

| Requirement | Details |
| --- | --- |
| Operating system | Windows x64 |
| Game files | A local Fortnite installation |
| Blender | Required for `.blend` conversion; tested with Blender **5.1.1**. The emote importer requires Blender **4.4 or newer**. |
| Internet | Used for compatibility data, catalogue images, streamed asset data and optional update checks |
| .NET | Included in the installer; no separate runtime installation is required |
| Storage | Space for the app, downloaded asset cache and exported files; usage depends on your selections |

## Install or update

1. Download the installer from [GitHub Releases](https://github.com/oozycloud/oozys-Fortnite-Porter/releases).
2. Run `oozys-Fortnite-Porter-Installer (beta 0.1.1).exe`.
3. Choose an installation folder. If the installer detects an existing installation there, it offers **Update**.
4. Close the running porter before updating, then install.
5. Open `oozys-Fortnite-Porter (beta 0.1.1).exe` from that folder.

The installed layout is:

```text
oozys-Fortnite-Porter (beta 0.1.1).exe
app/
    Porter.exe
    Porter.dll
    README.md
    ...runtime, libraries and Blender tools
```

Keep the launcher and `app` folder together. If you use the full application download instead of the installer, extract the whole folder before opening the launcher. Running an EXE from inside an archive or copying only `app/Porter.exe` leaves required files behind.

Settings and cache are stored under `%LOCALAPPDATA%\FortniteObj`. Updates preserve settings and unrelated files in the selected installation folder. Re-export assets to apply exporter fixes: updating the app does not rewrite existing Blender projects.

## Your first export

1. Open **Settings**, choose your Fortnite installation and choose an output folder.
2. Scan the installation, then search by name or use the category filter.
3. Select an asset and choose its style. Use **Ctrl-click** or **Shift-click** to select several assets.
4. Choose **Blender file (packed)** for a portable Blender project, then export.
5. Wait for the operation to finish and review any warnings before opening the resulting `.blend`.

For **Skin + emote**, use the separate skin and emote selection lists. Multiple selections export the selected combinations, so large selections can create many scenes. Leave a list empty when you do not want that asset type included, and use the individual-asset mode when exporting it on its own.

### File formats

| Output | Use it for |
| --- | --- |
| Packed Blender `.blend` | A Blender project containing the model, textures, rig and available animation |
| Blender + OBJ source files | Keeping geometry, material files, textures and conversion data for inspection or another workflow |
| Preview PNG | The cosmetic's source icon; this is not a rendered portrait of every possible custom combination |

An OBJ does not store an armature or animation. Keep its `.mtl` and textures beside it. The packed `.blend` is the recommended format for animated characters.

### Styles and colours

The style panel provides separate tabs for independent customization channels, such as Omega's armour stage and light colour, or Tricksy's mask, pattern, base colour, pattern colour and accent colour. Those choices are combined during export.

Not every option shown in Fortnite maps to an equivalent Blender feature. For example, gameplay reactions and alternate LEGO forms require more than a colour or mesh swap. Combined-style PNG previews currently use the base source icon and report that limitation.

### Emote speed and traversal

- Leave **Animation FPS** blank to use the authored rate, or enter a value from **1 to 240**, including fractional rates. Resampling preserves clip duration and playback speed.
- **Traversal** and **Static traversal** appear only for traversal emotes. They select the authored moving or stationary clip. Gameplay-driven travel may still require a path or root movement in Blender.
- Emote exports are silent. Emote audio was removed in version 2.0.1.
- Combined exports build the final Blender file after the animation has been attached. A file appearing during an unfinished or failed export is not proof that the scene is complete.

## Add an emote to a skin you already exported

1. In the porter's **Settings**, select **Install Blender emote tools**. Restart Blender if it was open.
2. Open the skin's `.blend` and select its mesh or armature.
3. In Blender's 3D View, press **N** and open the **Porter** tab.
4. Choose **Apply exported emote**, then select the separately exported emote `.blend`.
5. Choose an imported action if needed and press **Space** to play.

The importer attaches animation to the existing rig. Appending another armature and pressing **Ctrl+P** does not retarget the animation. If the skin has a baked cloth simulation, clear the old bake and simulate again from frame 1 after changing its action.

For manual installation, use the included `app/PorterEmoteTools.zip` through Blender's **Preferences → Add-ons → Install from Disk**, then enable the add-on.

## Materials, physics and environments

Use **Material Preview** or **Rendered** mode to inspect textures. Play the timeline to see supported animated surfaces. The **Source** material preset retains supported surface properties; **Matte** deliberately reduces reflections.

Toon materials use authored colour maps and an outer outline where supported. Garment physics use inferred pins and body collisions. Play sequentially from frame 1, adjust **Physics Pins** if necessary and bake before rendering. The garment's **Enable Cloth** property can disable simulation while editing.

Terrain has been removed. Skyboxes now offers **Daylight, Sunrise, Sunset, Overcast and Night**, with procedural Blender materials and no external image dependencies. Kicks, sprays and loading screens are included. Experimental exports also cover wraps, pets/carriers, toys and battle buses. See [release notes](docs/releases/beta-0.1.1.md) for tested examples and outstanding work.

## Update checks

**Check for updates** reads releases from [oozycloud/oozys-Fortnite-Porter](https://github.com/oozycloud/oozys-Fortnite-Porter). Enable **Check automatically** for a check at startup. A newer release opens its GitHub page; downloading and installing it remain manual.

Beta release tags use `beta-0.0.1`, `beta-0.0.2`, and so on, and may be marked as prereleases. Uploading an EXE to the repository without creating a GitHub Release does not advertise an update.

## Troubleshooting

| Problem | What to check |
| --- | --- |
| Installer or app does not open | Extract the full download, check that the root launcher sits beside `app`, and reinstall the complete package if files are missing. |
| A .NET prompt appears | Use the supplied root launcher and complete self-contained package. An isolated internal EXE or incomplete installation may not find the bundled runtime. |
| Export takes a long time | Check the current stage and session log. New high-resolution assets may need downloading. This build streams archive indexes and reports downloaded bytes. |
| The model is white or has incorrect materials | Use Material Preview/Rendered mode, review texture warnings and re-export with the latest tested build. Do not assume an export is visually correct just because it finished. |
| Emote does not move the skin | Use Skin + emote export or the Porter importer above; confirm that an action is selected and that you are inside its frame range. |
| Cloth clips or bunches | Play from frame 1, clear stale caches and review pins/collisions. Marge's dress still clips in raised-knee Business Hips poses. |
| Catalogue images are blank | Check the connection. This build uses a replacement thumbnail source for known BR items; missing images can still occur for other assets. |
| No update is found | Check GitHub Releases. The update checker cannot offer a build that has not been published there. |

For a bug report, include the app version, Blender version, exact asset and style choices, export format, what happened, and the relevant session/export log. Add a screenshot when the problem is visual. Export folders may contain `blender.log`, `export-report.json` and `INCOMPLETE.txt`; keep these when reporting a failed export.

## Current limitations

- Marge's dress can clip or bunch during raised-knee animation. A recent looser-pin experiment made the result worse and was not adopted.
- Cloth, some animated shaders, particles and reactive appearances are approximations. Gameplay-triggered behaviour is not universally supported.
- The full cosmetic-type audit is unfinished. Contrails, music packs and other omitted types are not all exportable in the current app.
- A successful automated check covers the tested asset and scenario, not every cosmetic in Fortnite.

## Development and release history

See [development and release instructions](docs/DEVELOPMENT.md), the [changelog](CHANGELOG.md), and [notes for each version](docs/releases/README.md).

Version increments follow the project's convention: fixes add **0.0.1**, medium updates add **0.1.0**, and major updates add **1.0.0**. The beta reset deliberately follows the older 2.2.0 development build; it is not an accidental downgrade. New releases must include their own notes and validation results.
