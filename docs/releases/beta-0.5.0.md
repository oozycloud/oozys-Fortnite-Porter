# beta 0.5.0

The model catalogue now connects the website to the app through exact asset IDs and complete style combinations. This medium update also fixes current Fortnite skeletal geometry, material parameter priority and rigid cosmetic attachments.

## Catalogue and IDs

- Website cards cover Fortnite skins, back blings, pickaxes and gliders, plus PC Building Simulator 2 motherboards, GPUs, CPUs, RAM, cases, coolers and fans.
- Search names or exact IDs, filter by game/category, and copy an ID. Model cards do not download game assets. The porter exports the corresponding asset from the installed game files.
- **Find ID** selects the asset; **Export ID** uses the current output settings. Both switch games when needed and report missing or outdated entries.
- `S` suffixes represent complete combinations of independent style channels, including armour stage and light colour. The registry preserves initial catalogue IDs. Fixed radix 1024 and arbitrary-precision arithmetic avoid losing precision for outfits with many options. Invalid option gaps and overflow are rejected.
- The website uses verified public Fortnite icons, recovered source icons and PC-part model renders. Internal/test/empty definitions that have no source image are explicitly marked **preview unavailable**. Individual style-option previews are labelled; they are not presented as composite renders.

## Export fixes

- Updated managed CUE4Parse libraries and preserved the game-specific skeletal mesh setting across package-reader copies. Current Fortnite assets no longer appear to have zero mesh levels because this setting was lost.
- Included the new parser runtime dependencies in the packaged app.
- Prioritized authored Fortnite material parameters over inherited placeholder textures. The Leviathan Axe normal map now uses its normal texture instead of the specular mask.
- Read character-part attachment sockets for rigid accessories. Ghoul Trooper's glasses remain a separate mesh, sit at the authored head socket and follow head animation.
- Fixed archive-index async IO capturing the WPF synchronization context when invoked by a synchronous archive reader. Cached and streamed archive reads can complete without that UI deadlock.
- Excluded workshop tools, decals, test prefabs and aggregate category containers from the part list. Bridges, connectors and standalone slots are classified as fittings. This also prevents accidentally exporting the entire GPU collection as one model.
- Recomputed non-finite source vertex normals on the affected MAG MAX fan meshes, preserving their geometry and textures.
- Empty PC-part helper meshes are skipped with a warning instead of aborting otherwise valid part geometry.

## Website previews

The entrance provides the textured GameRock RTX 4090, Pink Ghoul Trooper, Leviathan Axe without effects, and ASRock X870E Taichi motherboard. Each GLB embeds its textures. Model switching, weighted mouse motion, reduced motion and responsive camera fitting remain available. The porter download action resolves the latest published beta installer when loaded and clicked.

## Validation

- All 11,021 registered base IDs and 4,190 complete style configurations checked, including invalid gaps, overflow and exact native-asset resolution.
- WPF UI checks include exact short IDs across both games, background scanning/navigation, dark dropdowns, multi-selection, independent style controls, real progress, title-bar controls, update checks and UI-thread archive reads.
- Pink Ghoul Trooper: three meshes and 17 textures; Leviathan Axe: one mesh and five textures. Both export without warnings in the tested scenarios.
- Omega `O6975S2054`: Stage 5 armour and purple light material values exported together.
- Pink Ghoul Trooper with Business Hips: 61 frames over two seconds, 86 matched bones, action attached to the skin rig, and separately retained glasses verified to follow the head across animated frames.
- Marge Simpson: three meshes and 24 textures; Aang: two meshes and 11 textures, including authored toon colour/line maps and outline materials.
- Self-contained installer: 11 checks pass, including a fresh install, payload hash verification, launch with external .NET disabled, repair preserving user files, update removing the previous managed launcher, and installer location/invalid-path controls.
- Browser download resolution, mouse motion, reduced motion and entrance/home navigation checks pass. The expanded desktop/mobile catalogue passed an independent design evaluation.

## Limits

The catalogue snapshot does not guarantee every listed internal or gameplay asset is exportable. Some definitions lack enabled geometry or icons. New IDs and changed styles can require an app update. The exporter reconstructs supported materials and rigs; gameplay reactions, LEGO appearances, custom shader effects and physics are not a complete Unreal or Unity runtime. Source icon previews do not depict every possible custom combination.

Installer, portable application, source backup and SHA-256 checksums are supplied with the release. Keep the launcher and its `app` folder together.
