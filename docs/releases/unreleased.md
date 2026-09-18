Unreleased

**Development work — not included in the beta 0.0.1 installer.**

## Implemented in source

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

## Evidence so far

- Fresh Art the Clown and Ballerina Cappuccina exports completed without export warnings after the archive-index fix.
- Rendered Tricksy with the Cuddle Team Leader mask, camo pattern, teal base, white pattern and blue accents; rendered Omega with full armour and purple strips.
- Exported and rendered all five procedural sky presets.
- Passed native UI checks for independent style choices, live thumbnail decoding, download/animation progress, completion, cancellation, stale callback rejection, themes and dark dropdowns.
- Exported Squiddy Steppers, X Mark and Born of Fire without export warnings; visually inspected the shoe mesh and materials.
- Completed the 13-case asset export regression run without export warnings. All 13 also passed saved-scene checks for packed images, silent emotes, finite poses, stable mesh bounds and applicable animation/material behaviour. These checks do not detect every visual defect.
- Re-ran the existing 67 installer/update checks successfully. These do not replace testing the next packaged installer.

## Still required before distribution

- Resolve or explicitly scope remaining visual problems, including Marge’s dress clipping during Business Hips. A looser-pin experiment produced bunching and was excluded.
- Complete the cosmetic-type audit. Wraps, contrails, music packs, pets/carriers, toys, buses and other categories still need export-path review; this is not universal cosmetic support.
- Finish broader thumbnail fallbacks, style-channel edge cases, cleanup validation and remaining animation/material regression checks.
- Assign the next version under the project’s versioning convention, rebuild the complete package and test its installer and update migration.

The Desktop installer and delivered application remain **beta 0.0.1**. No new release is published by this document.
