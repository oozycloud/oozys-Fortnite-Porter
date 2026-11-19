# beta 0.11.0

- Play Zomba wheel materials directly in the Studio preview using GPU shaders.
- Choose Display, 60, 120, 144 or 240 fps without changing animation speed. Actual frame rate depends on the display and GPU.
- Turn and roll individual rigged wheels using a rotation handle or angle slider.
- Save wheel poses in Studio projects and export them to Blender, GLB and FBX.
- Preserve wheel hierarchy and native rig animation when exporting cars.

Zomba material animation remains editable in Blender. Standard GLB and FBX materials cannot reproduce the custom procedural shader.

- Use source material surfaces by default; optional matte mode remains available. Existing settings migrate once and later choices are retained.
- Resolve additional Fortnite diffuse/normal/packed-mask parameter names and isolate colour/data image colour spaces.
- Add a car paint clear coat and prevent normal placeholders from colouring Rocket League glass/lenses.
- Fix card rendering for models saved in Pose mode.

Material checks covered fresh exports of Aang, Marge Simpson, Astro Jack, Games Manager, Omega and Renegade Raider, plus Fennec with Dignitas 2024 and Zomba wheels. Custom Unreal material effects are still approximations; these checks do not establish parity for every game asset.
