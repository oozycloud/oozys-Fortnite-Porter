# beta 0.13.1 — automatic skin customization previews

19 November 2026

## Changes

- Skin export overlays automatically render the current complete customization selection, including independent style channels, colours and accessories.
- Changes are debounced so quick selections do not queue obsolete renders. Completed appearances are cached; stale results cannot replace the current selection.
- Preview rendering runs in the background and shows its progress. The preview uses the same model/material reconstruction as export.

## Using the preview

Scan Fortnite, open a skin card and change its customization options. The preview updates after you stop changing options. A new combination needs time to extract and render in Blender; previously rendered combinations load from the cache. Blender must be installed. A rendered preview is a still image, not a simulation of in-game reactive triggers.

This remains a reconstruction of the supported source data. Unsupported Unreal shader and gameplay behaviour is not reproduced by the preview.
