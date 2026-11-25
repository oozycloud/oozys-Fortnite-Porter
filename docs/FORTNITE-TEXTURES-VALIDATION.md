# Fortnite texture validation — beta 0.13.2

25 November 2026

## Verified

- Sixteen assets extracted and converted with zero missing-map records: Aang, Marge Simpson, Astro Jack, Games Manager, Omega, Renegade Raider, Peely, Black Panther, Sabrina Carpenter, Tour-Ready Sabrina Carpenter, Cuddle Bow, Leviathan Axe, Mako, Deadpool, Iron Man and Spider-Man.
- Blender validation decoded and packed 254 material image-node bindings. Neutral thin-film colour processing corrected Black Panther; 203 other exported map hashes in the original ten-asset comparison stayed identical.
- Unit checks passed linear-colour desaturation, zero-strength pixel preservation, alpha preservation and invalidation of previously missing bulk payloads without discarding successful cache entries.
- A native WPF/WebView2 run imported corrected Black Panther, applied Midnight Sun, loaded the textured preview, saved/reopened a Studio project and exported BLEND/GLB/FBX. Timeline seeking, stepping and emote audio passed. The GLB body/accessory materials reference embedded base-colour images (eight embedded images total).
- All thirteen installer checks passed: fresh install, payload hashes, bundled runtime, launcher version, repair, upgrade, existing user-file/cache preservation and installer location checks.

Local evidence: `recovery/texture-audit-fixed/validation.json`, `recovery/texture-equipment-check/validation.json`, `recovery/texture-studio-confirmed`, and `recovery/texture-package-results.json`. Fixtures contain game assets and are intentionally excluded from the source repository.

## Limits

This sample verifies the supported pipeline; it does not certify the entire Fortnite catalogue. Thin-film colour is baked from the enabled desaturation parameter; view-dependent Unreal shader behaviour remains approximate. Existing exports need to be re-exported and re-imported into Studio. Missing external source files cannot be reconstructed from an untextured model alone.
