# oozy — modeling entrance

A buildless, dark studio entrance featuring Renegade Raider’s **Black & Gold** style performing **Scenario**. The single textured character is the cover; the earlier four-model picker is removed. The homepage retains the app’s supported model catalogue and latest published porter installer download.

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

## Character and animation

The entrance requests `assets/renegade-raider-scenario.glb`, containing the genuine model, embedded textures, skeleton and Scenario animation. It uses Three.js `AnimationMixer` and `LoopRepeat` at original speed, with no audio. The clip must include a closure key at its exact loop duration, so interpolation reaches the first pose without an extra displayed duplicate frame. The viewer prefers a Scenario-named clip and otherwise selects the first non-intro/outro action; missing animation shows an explicit failure state instead of a stationary character labeled as dancing.

The GLB scene or clip may declare `porterLoopDuration` (seconds), and the scene may declare `porterAnimatedBounds: {min:[x,y,z], max:[x,y,z]}` in glTF extras. Bounds must use the GLB scene’s world coordinates before viewer normalization. Without supplied bounds, the viewer samples the entire action to fit the dancer’s range, including raised hands, instead of fitting only the first pose. Leave the character upright along positive Y and facing positive Z. Centering happens on a parent group so animated root transforms remain intact.

The preview pauses on the homepage and while the tab is hidden. Returning resumes from the same point without advancing through the hidden interval. System reduced-motion settings show a representative still pose and disable pointer tilt. The character faces forward initially. Drag with a mouse or touch to orbit through a full turn; vertical orbit is limited to keep the character upright. Dragging captures the pointer, and release adds a short damped coast. Reduced motion disables this coasting but still permits direct user-controlled rotation. Wheel zoom, touch pinch and compact plus/minus buttons adjust the camera from 70% to 120% of the fitted default; the bounds retain the full dancer in frame. The focusable preview also supports arrow-key rotation, +/- zoom and Home/0 to reset both zoom and orientation. Controls affect only the preview; page scrolling elsewhere and browser Ctrl/+ zoom remain available. Scenario keeps its original speed while orbiting. A failed GLB or unavailable WebGL leaves the begin button and catalogue usable.

Browser materials are simplified exports rather than Fortnite’s complete runtime shaders. This preview does not imply affiliation with Epic Games. Three.js and Manrope licences remain in `assets/vendor/THREE-LICENSE.txt` and `assets/MANROPE-LICENSE.txt`.
