# oozy — modeling entrance

A buildless, dark studio entrance with the textured Palit GameRock RTX 4090 preview. The homepage is intentionally blank apart from navigation and the porter download. The preview is temporary; it is not a GameRock 5090.

## Preview locally

Serve this directory over HTTP; opening `index.html` directly as a file will block JavaScript modules and the model in most browsers.

```powershell
cd website
python -m http.server 8080
```

Open http://localhost:8080. No Node build or installation is required. The 3D preview, fonts and Three.js are local assets. GitHub access is needed only to resolve the installer download.

## GitHub Pages

Deploy the contents of this directory as the Pages artifact (using GitHub Actions), or copy the contents into the repository's `docs/` directory and select that folder in **Settings → Pages**. All asset paths are relative and support the `/oozys-Fortnite-Porter/` project path. Keep the `assets/` directory beside `index.html`.

The download button checks published GitHub releases at load and again when clicked. It selects the highest numeric **beta** version with an installer EXE; legacy 2.x releases do not outrank beta releases. No download happens automatically. API failures show a GitHub Releases link instead of presenting a stale download as current.

## Model and texture

`assets/gamerock-4090.glb` contains the model and embedded textures in one file. GLB is the format to use for future replacements. Preserve its filename or update `assets/viewer.js`. Preserve material distinctions when exporting; use a centered model with its fan face towards positive Z and its long edge along X for this camera.

The temporary preview comes from PC Building Simulator 2's `GPU_PALIT_RTX_4090_GameRock_OC` asset and is labeled Palit GameRock RTX 4090. Its web export contains 15 meshes, 13 materials and 8 embedded images, with simplified materials for browser rendering. The preview does not imply affiliation with Palit or PC Building Simulator 2. Third-party library and font licenses are in `assets/vendor/THREE-LICENSE.txt` and `assets/MANROPE-LICENSE.txt`.

Motion respects reduced-motion settings and pauses when hidden. The entrance and download still work if WebGL or the preview asset is unavailable.
