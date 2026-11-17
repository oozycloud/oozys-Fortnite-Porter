# beta 0.7.3 — local test build

- Fortnite: resolve texture aliases at the nearest material instance before inherited/parser defaults. Preserve selected style texture overrides. Fixes Games Manager receiving its fabric detail map instead of its separate body and face-mask textures.
- Fortnite: recognise SRM masks and extract roughness from green, metalness from blue. Retain legacy SMR packing. Prevents cloth becoming polished metal.
- Rocket League: recognise named tyre/rim texture families such as Rim_Lightning_01 and Tire_Lighting_01. Use separate diffuse and normal maps, authored vertex-red tyre selection, rubber roughness, and rim-only paint.
- Zomba: replace the guessed three-channel pulse with the recovered SM5 pixel-shader equations and uniform expressions. Restore counter-rotation (+1/-2 radians per second), LUT panning (-.2/-.5 UV units per second), separate R/B and G/A samples, source alpha falloff and painted/unpainted colour handling.

Installer QA uses a local HTTP image fixture instead of depending on the live cosmetic CDN, which currently responds with HTTP 403 on this machine. This tests asynchronous image decoding, not live CDN availability.

This is a local test installer, not a new public GitHub binary release. Blender material drivers retain timing at different frame rates. GLB/FBX do not preserve these Blender drivers. Unreal scene fog, exposure and renderer-specific lighting are not reproduced; this is not a guarantee of pixel-identical output or universal support for every game shader.
