# beta 0.11.0 validation

Native WPF + WebView2 checks loaded a Fennec/Dignitas 2024/Zomba scene with thirteen meshes, twenty-eight packed textures and eight GPU-animated wheel faces. Timeline seeking and stepping passed. The preview reported approximately 145–153 fps with Display selected on the test machine. This is measured hardware output, not a guaranteed frame rate.

A front-left steering pose of 30 degrees survived project save/reopen. A separate Blender process loaded the exported file and verified 30.0000008 degrees on its pose-offset bone, thirteen meshes and twenty-eight packed images. The offset hierarchy retains original rig action channels.

Fresh source-material exports: Aang (11 textures), Marge Simpson, Astro Jack, Games Manager, Omega and Renegade Raider. All referenced image files existed; all exports reported zero missing-texture warnings. Packed Blender files were rebuilt and rendered. This verifies file integrity and the tested material paths, not exact reconstruction of all custom Unreal shaders.

Rocket League Fennec/Dignitas 2024/Zomba was rebuilt and rendered with clear coat and lens-placeholder handling. Source images and custom Blender material animation remain packed. Procedural material animation is not a standard GLB/FBX feature.
