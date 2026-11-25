# beta 0.13.2 — Fortnite material and texture recovery

25 November 2026

- Apply authored diffuse desaturation for enabled thin-film materials. Black Panther now uses its silver/neutral trim instead of the gold in the unprocessed source texture. The processed image is portable into Studio and exchange formats.
- Isolate inherited parameter, parent and individual texture-reference failures so a failed optional lookup does not discard an otherwise usable material.
- Recognize additional normalized base-colour and normal-map parameter aliases.
- Invalidate cached missing payloads before an export retry and after mounting streamed archives, while keeping successfully loaded bytes.
- Embed the material manifest in `.blend` files, validate decoded/packed images before saving, and retain `texture-report.json` with packed-only exports.
- Check used embedded images when importing models into Studio and report decoding failures.

Re-export affected assets to receive corrected material pixels. Existing exported files are not modified. Material reconstruction still approximates unsupported Unreal runtime shaders; this release does not claim every Fortnite cosmetic is identical to its in-game rendering.
