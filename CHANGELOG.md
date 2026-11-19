# Changelog

Changes to **oozy's porter studio**, formerly **oozy's Fortnite Porter** and **Fortnite Porter Studio**. Entries distinguish verified packaged changes from current development. The beta sequence intentionally resets after 2.2.0.

## beta 0.13.0 — 19 November 2026

- Reconstruct Sabrina outfits using authored main/trim/secondary sequin gradients, masks and detail textures, with portable baked colour/roughness maps.
- Verified all six current combinations across Sabrina/Babydoll and Tour-Ready pink/Baby Blue/reactivity options.
- Export emote WAV audio through sound notifies, cues and MetaSound references; embed the audio in Blender emote metadata.
- Add Studio audio enablement, volume and timeline synchronization. Songs continue across shorter dance loops; saved projects retain WAV files.
- Verify Midnight Sun’s stereo 48kHz intro and 50.8-second loop in native WebView2 playback and project save/reopen.
- Pause cloth/soft-body simulation during Studio processing and restore its original switches in Blender exports.

[Release notes](docs/releases/beta-0.13.0.md).

## beta 0.12.1 — 19 November 2026

- Apply Dune Racer’s actual alpha mask for painted decal accents, independently of car trim and secondary colour.
- Restrict painted accents to decals with their own PaintColor parameter and a varying alpha mask; unsupported decals retain authored colours and report a warning.
- Tie cached Rocket League previews to the current app version so obsolete grey renders are regenerated.
- Verified six source-driven decal cases: Dune Racer default/Titanium White/changed secondary, Dignitas legacy/2024 and NRG.
- Pixel comparisons verified secondary and painted-accent changes remain inside their respective masks.

[Release notes](docs/releases/beta-0.12.1.md).

## beta 0.12.0 — 19 November 2026

- Separate tyre and rim materials to keep Zomba rubber dark in Studio and exchange formats; retain painted rims.
- Repair older Porter wheel scenes while converting their previews.
- Export labelled 2048px UV template PNGs for Fortnite vehicles and Rocket League cars, enabled by default in vehicle export options.
- Include transparent wire guides, material/UV-tile labels, island numbers, mesh names, an index and painting instructions.
- Preserve original UVs and report surfaces without usable UV maps.

[Release notes](docs/releases/beta-0.12.0.md).

## beta 0.11.0 — 19 November 2026

- Preview Zomba wheel materials on the GPU with independent frame rate and animation speed controls.
- Steer or roll each rigged wheel with a rotation handle or angle slider; save poses in projects and model exports.
- Preserve hidden wheel attachment hierarchy and native rig actions in exchange exports.
- Use source Fortnite surfaces by default, expand material texture aliases, and separate colour/data image colour spaces.
- Add Rocket League paint clear coat and prevent normal placeholders from colouring lenses.
- Fix card rendering for Blender files saved in Pose mode.
- Verified wheel pose export/reopen, timeline seeking and six fresh Fortnite material exports. Custom Unreal shaders remain approximations.

[Release notes](docs/releases/beta-0.11.0.md).

## beta 0.10.0 — 18 November 2026

- Replaced library rows with a virtualized image-card gallery and two-line model names.
- Moved styles, format selection, output, live progress and cancellation into an in-app export overlay.
- Preserved batch selection and exact website ID lookup; removed combined skin + emote mode from Porter.
- Connected real Fortnite/PC-part artwork and native Rocket League thumbnail extraction with a persistent decoded-image cache.
- Added independent move, steer and roll controls at all four named wheel mounts, with mounted tyre/rim/effect assemblies following each corner.
- Fixed team-decal material selection, legacy packed-mask colours and modern full-colour liveries; baked static paint and surface maps for exchange formats.
- Verified Octane Dignitas, Fennec Dignitas 2024, Octane NRG, default blue paint, wheel transforms and GLB paint texture references.

[Release notes](docs/releases/beta-0.10.0.md).

## beta 0.9.0 — 18 November 2026

- Renamed the product to **oozy's porter studio**.
- Merged Porter and Emote Studio into one window and one installer.
- Added two tool tabs at the direct top center; each tool keeps its state while switching.
- Kept a shared titlebar and removed the duplicate editor header.
- Included rigged `.blend` import, multiple emotes, textured timeline preview and BLEND/GLB/FBX/project saving.
- Updated launcher, installer, app metadata and update checks; older Porter installations remain recognized.

[Release notes](docs/releases/beta-0.9.0.md).

## Unreleased

Local test build beta 0.8.0 adds the website's charcoal studio theme to the app and installer, authored emote loop ranges with a continuous seam, and the interactive Scenario website cover. [Test-build notes](docs/releases/beta-0.8.0.md).

Beta 0.7.3 fixed Games Manager materials and the Zomba shader. [Notes](docs/releases/beta-0.7.3.md).

Remaining fidelity and cosmetic support work is tracked in [Unreleased notes](docs/releases/unreleased.md).

## beta 0.7.1

Fixed duplicated front wheels and missing rear wheels, cloned wheel-rig bindings, exported bone-widget spheres, selected livery diffuse textures, and Blender viewport defaults. [Full notes](docs/releases/beta-0.7.1.md).

## beta 0.7.0

Authored streamed map placement, inherited component settings, skeletal effect attachments and timing, supported sprite timelines, animated UberSkin noise inputs, and native reader source. Support limits remain explicit in export reports. [Full notes](docs/releases/beta-0.7.0.md).

## beta 0.6.0

Rocket League game tab, installed asset catalogue, compatible decal styles, mesh/resource exports and publication fixes. Map placement and Unreal particle simulation remain unsupported. [Full notes](docs/releases/beta-0.6.0.md).

## beta 0.5.0

Website catalogue and exact asset/style IDs, textured model showcases, current Fortnite mesh profile, material parameter priority fixes, and archive-download deadlock correction. [Full notes](docs/releases/beta-0.5.0.md).

## beta 0.4.4

Keep browsing, settings and game navigation available during scans; retain separate catalogues; fix the white game-sidebar background. Preserved builds are published to GitHub Releases. [Full notes](docs/releases/beta-0.4.4.md).

## beta 0.4.3

Fixed mapping format 5 support and validated mapping contents during compatibility refresh. [Full notes](docs/releases/beta-0.4.3.md).

## beta 0.4.2

Fixed skipped PC part material textures, added normal/detail maps, masks and authored transparency, and packed referenced shader images. [Full notes](docs/releases/beta-0.4.2.md).

## beta 0.4.1

Custom title bar and PC part model categories, with enabled prefab hierarchies, packed base-colour textures and colour-variant materials. [Full notes](docs/releases/beta-0.4.1.md).

## beta 0.3.1

Left game tabs, early PC Building Simulator 2 mesh/image exports, automatic Fortnite compatibility refresh at every launch, and long-path exchange fixes. [Full notes](docs/releases/beta-0.3.1.md).

## beta 0.2.1

Weapons, authored building edits, vehicles, five exchange formats and export controls. [Full notes](docs/releases/beta-0.2.1.md).

## beta 0.1.1

Independent style channels, masked customization colours, streaming download fixes, bottom progress, five skyboxes and additional cosmetic categories. [Full notes](docs/releases/beta-0.1.1.md).

## beta 0.0.1

Combined animation export and beta reset. [Full notes](docs/releases/beta-0.0.1.md).

## 2.2.0

oozy's Fortnite Porter branding and updates. [Full notes](docs/releases/2.2.0.md).

## 2.1.0

Packed models and separate-emote import. [Full notes](docs/releases/2.1.0.md).

## 2.0.1

Silent emotes and Astro Jack materials. [Full notes](docs/releases/2.0.1.md).

## 2.0.0

Animation, emoticons and material updates. [Full notes](docs/releases/2.0.0.md).

## 1.7.0

Toon outer layer. [Full notes](docs/releases/1.7.0.md).

## 1.6.0

Orange interface and multiple selection. [Full notes](docs/releases/1.6.0.md).

## 1.5.0

Aang, effects and reactive appearances. [Full notes](docs/releases/1.5.0.md).

## 1.4.0

Toon shading and physics. [Full notes](docs/releases/1.4.0.md).

## 1.3.0

Cartoon shading and environments. [Full notes](docs/releases/1.3.0.md).

## 1.2.1

Interface readability. [Full notes](docs/releases/1.2.1.md).

## 1.2.0

Interface revamp. [Full notes](docs/releases/1.2.0.md).

## 1.1.1

Self-contained installer. [Full notes](docs/releases/1.1.1.md).

## 1.1.0

Cartoon materials and installer. [Full notes](docs/releases/1.1.0.md).

## 1.0.0

Rig, texture and physics groundwork. [Full notes](docs/releases/1.0.0.md).

## Version policy

- Patch fix: increase by **0.0.1**.
- Medium update: increase by **0.1.0**.
- Major update: increase by **1.0.0**.

Add notes for every shipped version. Do not move unfinished work out of Unreleased or claim tests from another package as validation of a new installer.
