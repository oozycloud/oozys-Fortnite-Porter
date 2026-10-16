# oozy — modeling entrance

A buildless, dark studio entrance with selectable textured previews: Palit GameRock RTX 4090, Pink Ghoul Trooper, Leviathan Axe without effects, and an ASRock X870E Taichi motherboard. The graphics card preview is temporary; it is not a GameRock 5090. The homepage contains the app's supported model catalogue and a porter installer download.

## Browse and export

The catalogue includes Fortnite skins, back blings, pickaxes and gliders, plus PC Building Simulator 2 motherboards, GPUs, CPUs, RAM, cases, coolers and fans. Filter by game/category or search by name or exact asset ID. It displays 48 entries per page and lazy-loads thumbnail images. PC-part thumbnails are renders of the actual exported models, stored at `assets/parts/<ID>.webp`. Fortnite thumbnails use source icons, including locally extracted icons where available. Some test, empty and NPC definitions have no source icon; these remain searchable with their exact ID and a clear **preview unavailable** label. Failed thumbnail requests receive the same label instead of an invented image.

Copy a model's ID into **Website asset ID** in porter beta 0.5.0 or later. Choose **Find ID** to select styles, or **Export ID** to export with the current/default style. Model cards do not download model files. Clipboard access falls back to selected text for manual copying if the browser blocks it.

`assets/catalog.json` is a snapshot generated from the app catalogue. Refresh this file when the game's catalogue changes. Rows have `{id,name,game,category,image,styleChannels?:[{key,name,options:[{key,name,image?}]}]}`. Base IDs come from the stable registry, such as `G167`; sorting and filtering never change them.

Open **customize styles** to choose each supplied style channel. Changing a channel or choosing **use these styles** creates a complete combination ID, such as `G167S4`; **use default** restores the base ID. Channel order and option order come directly from the stable registry. The suffix uses fixed radix 1024 with `BigInt`: add each selected zero-based option index times `1024 ** channelIndex`, then add one. Appending options therefore preserves existing IDs. The catalogue declares `styleRadix:1024`; each channel supports up to 1024 options. Combinations are never enumerated. An exact combination-ID search decodes and selects its channels, rejecting indices outside current option counts or a nonzero remainder beyond the last channel. An option image is labeled **option preview · not a combined render**; otherwise the base image is labeled **asset preview**. The browser does not fabricate composite previews.

Style suffixes accept up to 101 decimal digits, matching the app. Invalid indices and extra digits are rejected. Long IDs remain selectable and copyable in a constrained input rather than widening the cards.

Images may use public HTTPS URLs or local `assets/` paths. An asset with `imageUnavailable:true` displays the explicit missing-preview state. Metadata is downloaded only after entering the homepage. Older supplied `styles:[{id,name,image}]` lists remain supported when a row has no style channels.

## Preview locally

Serve this directory over HTTP; opening `index.html` directly as a file will block JavaScript modules and the model in most browsers.

```powershell
cd website
python -m http.server 8080
```

Open http://localhost:8080. No Node build or installation is required. Supplied 3D previews, fonts, catalogue metadata, PC-part renders and Three.js are local assets. GitHub access resolves the installer download; some Fortnite catalogue thumbnails load from their image host.

## GitHub Pages

Deploy the contents of this directory as the Pages artifact (using GitHub Actions), or copy the contents into the repository's `docs/` directory and select that folder in **Settings → Pages**. All asset paths are relative and support the `/oozys-Fortnite-Porter/` project path. Keep the `assets/` directory beside `index.html`.

The download button checks published GitHub releases at load and again when clicked. It selects the highest numeric **beta** version with an installer EXE; legacy 2.x releases do not outrank beta releases. No download happens automatically. API failures show a GitHub Releases link instead of presenting a stale download as current.

## Model and texture

The viewer expects each preview GLB to contain its model and embedded textures in one file: `assets/gamerock-4090.glb`, `assets/pink-ghoul-trooper.glb`, `assets/leviathan-axe.glb`, and `assets/asrock-taichi.glb`. A missing export shows a usable error state; a configured selector alone does not mean its GLB has been supplied. GLB is the format to use for future replacements. Preserve filenames or update `assets/viewer.js`. Preserve material distinctions when exporting; use a centered model facing positive Z with its upright axis along positive Y. The camera fits each model automatically.

Only the selected model is requested. The viewer retains at most two completed models and releases geometry, material and texture resources when evicting an older preview. Switching hides the previous preview immediately so labels cannot appear on the wrong model; overlapping requests cannot replace a newer selection. All four buttons support Tab/Enter/Space, with optional left/right arrows and Home/End shortcuts. Each selection resets the weighted pose.

The temporary preview comes from PC Building Simulator 2's `GPU_PALIT_RTX_4090_GameRock_OC` asset and is labeled Palit GameRock RTX 4090. Its web export contains 15 meshes, 13 materials and 8 embedded images, with simplified materials for browser rendering. The preview does not imply affiliation with Palit or PC Building Simulator 2. Third-party library and font licenses are in `assets/vendor/THREE-LICENSE.txt` and `assets/MANROPE-LICENSE.txt`.

The motherboard preview is the ASRock X870E Taichi exported from PC Building Simulator 2 (95 meshes and 9 source textures). The additional Fortnite previews are Pink Ghoul Trooper and Leviathan Axe; the axe preview excludes effects. Browser materials are simplified exports rather than the games' runtime shaders. These previews do not imply affiliation with ASRock or Epic Games.

Motion respects reduced-motion settings and pauses when hidden. The entrance and download still work if WebGL or the preview asset is unavailable.
