# Emote Studio beta 0.0.1 validation

Tested on Windows x64 with Blender 5.1.1 and the installed WebView2 runtime, 18 November 2026.

- Native WebView2 smoke workflow: real packed rigged model, Scenario and Dancin’ Domino emotes, switching clips, export to BLEND/GLB/FBX, portable project save and reopen.
- Timeline: seek to frame 15, step to frame 16, resume playback; native screenshot confirms textured animation.
- Export round trip: one armature in Blender output, both imported actions retained, selected action active and moving, 19 packed textures. Fresh FBX import recovered animation and 23 image resources.
- Foreign humanoid rig fixture: renamed Mixamo-style pelvis/arm bones with Porter metadata removed; automatic matching and rest-pose retargeting produced moving, finite transforms. Arbitrary skeletons still require individual review.
- Installer: fresh installation into a Unicode path, repair, file hashes against published package, unrelated model preservation, older-version Update detection, empty/root-path rejection, latest typed location wins, newer-version downgrade rejection.
- Installed launcher workflow: completed the full native smoke with external DOTNET_ROOT pointed to a nonexistent path, confirming the bundled .NET runtime is used.
- Independent visual evaluation: PASS after fixing diagnostics overflow and keeping export controls accessible.

No game assets are included in source or release packages. Test fixtures are local and excluded. Testing these humanoid fixtures does not establish compatibility with every custom model or Blender shader.
