# beta 0.1.1 — Customization, progress and export fixes

Medium update from beta 0.0.1 (+0.1.0).

## Changes

- Stream large archive indexes with byte progress and an idle timeout instead of buffering the entire response under a fixed 30-second timeout. Validate the index before replacing its cache file, and avoid repeating the same failed request for every material in one export.
- Replace the failing image host for known BR thumbnails with the public Fortnite-API icon endpoint.
- Add independent style-channel tabs and combine selected geometry, masks, patterns and colours. Read authored colour names where available.
- Export the customization pattern mask and reconstruct Tricksy’s base, pattern and accent colours in Blender.
- Apply explicitly selected light colours through their authored light-strip masks, including Omega’s purple option.
- Display real stage progress for downloads, scanning, meshes and animation attachment, with explicit completion and cancellation states. Ignore delayed callbacks from finished operations.
- Remove Terrain and its generated prefab baseplate. Add Daylight, Sunrise, Sunset, Overcast and Night skyboxes.
- Add kicks through their mutable skeletal-mesh/material inputs; exclude the empty attachment placeholder.
- Add spray and loading-screen image exports as PNGs and packed Blender planes.
- Remove unselected hidden style geometry from Blender’s object list while keeping visible accessories separate.
- Clarify that a source PNG icon does not depict a custom combination of style channels.
- Add the README, version history, individual version notes and a release-documentation workflow.

## Validation

- Fresh Art the Clown and Ballerina Cappuccina exports completed without export warnings after the archive-index fix.
- Rendered Tricksy with the Cuddle Team Leader mask, camo pattern, teal base, white pattern and blue accents; rendered Omega with full armour and purple strips.
- Exported and rendered all five procedural sky presets.
- Passed native UI checks for independent style choices, live thumbnail decoding, download/animation progress, completion, cancellation, stale callback rejection, themes and dark dropdowns.
- Exported Squiddy Steppers, X Mark and Born of Fire without export warnings; visually inspected the shoe mesh and materials.
- Completed the 13-case asset export regression run without export warnings. All 13 also passed saved-scene checks for packed images, silent emotes, finite poses, stable mesh bounds and applicable animation/material behaviour. These checks do not detect every visual defect.
- Re-ran the existing 67 installer/update checks successfully. These do not replace testing the next packaged installer.

- Final packaged build: all 67 installer/updater checks and 10 complete installer checks passed, including update from beta 0.0.1, repair, user-file preservation and launch with an unavailable external runtime.
- Rebuilt and reopened a combined Art the Clown emote export; verified the active main dance, moving rig and packed images. Both delivered application copies match the payload hashes.
- Wrap, pet/carrier, toy and bus conversions completed. The tested Bonesy pet reports an unavailable material slot.

## Known limitations

- Marge’s dress can clip during raised-knee animations. Experimental cloth changes that worsened it were excluded.
- Some Unreal shaders, particles and reactive gameplay behaviours remain approximate. Universal cosmetic support is not claimed; contrails and standalone music packs are not implemented.
- Wraps use a generated material-preview sphere. Pet, toy and battle-bus exports are experimental; the bus flame can fall back to a constant material.
- A combined-style PNG can use the base catalogue icon rather than render the chosen combination.

## Install or update

Run `oozys-Fortnite-Porter-Installer (beta 0.1.1).exe`. The runtime is included. Choose your existing installation to update it; close the app first. Settings and unrelated exports are preserved. The complete application folder is also supplied: keep its launcher and `app` folder together. Re-export assets to apply these fixes.
