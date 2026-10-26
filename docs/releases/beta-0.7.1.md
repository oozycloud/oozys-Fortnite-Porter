# beta 0.7.1 — Rocket League wheels and livery fixes

Released 26 October 2026. Patch update from beta 0.7.0.

- Choose one actual wheel mount at each corner instead of counting disc geometry helper bones as extra axles. Fixes duplicated front wheels and absent rear wheels on the NASCAR Chevrolet Camaro.
- Preserve source transforms before duplication and bind each cloned wheel mesh to its own copied armature.
- Exclude glTF bone custom-shape spheres from wheel duplication and exported model geometry. Hide editor rigs/helpers and relationship lines without removing the deformation rig.
- Apply a selected decal's explicit diffuse texture to the body material. Fixes the NASCAR x RL livery being omitted in favour of the default body texture. Inherited generic shader defaults do not override animated or mask-based decals.
- Open exported Blender scenes in Material Preview, frame the model, and hide the floor grid, axes and editor extras.

Verified against the user's actual Camaro export: five model meshes (body plus four wheels), four distinct corner mounts, correct wheel-armature references, packed NASCAR livery connected to the body shader, no bone-widget spheres, and clean viewport settings. The repaired scene was rendered to check the visible livery and wheel placement. Animated Heatwave material drivers were also checked after the change.

Other custom shader and particle limitations from beta 0.7.0 remain. This patch does not claim full Unreal material/runtime fidelity.
