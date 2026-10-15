# beta 0.4.2

## Texture fixes for PC Building Simulator 2

The previous build imported base-colour maps but skipped other material textures. Part exports now extract all referenced material images and pack them into the Blender file, including maps whose custom shader inputs cannot yet be reconstructed.

- Added tangent normal maps and detail layers.
- Added metallic/smoothness masks, roughness conversion and ambient occlusion approximation.
- Added authored transparency/cutout handling and emissive colour/maps. Materials with zero authored emission stay non-emissive.
- Preserved additional UV sets and texture transforms.
- Unsupported texture properties are retained as labelled image nodes and reported in export warnings.

Re-export parts to apply these changes. Existing Blender files are not modified by updating the app.

## Validation and remaining limits

GPU, motherboard, RAM and case samples were exported and reopened to check packed images, normal connections, masks and transparency. Custom Unity shaders, runtime effects and component replacements remain incomplete. Detail blending and ambient occlusion are approximations; planar/triplanar mapping falls back to UV0 with a warning. Exchange formats may preserve fewer material features than the packed Blender file.

Mask/detail channels follow [Unity's HDRP documentation](https://docs.unity.cn/Packages/com.unity.render-pipelines.high-definition%4012.1/manual/Mask-Map-and-Detail-Map.html).
