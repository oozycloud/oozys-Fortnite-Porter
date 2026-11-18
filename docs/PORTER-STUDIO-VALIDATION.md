# beta 0.9.0 integration validation

18 November 2026, Windows x64, Blender 5.1.1.

- Built the shared Porter host and editor component without compiler warnings or errors.
- Native integrated workflow imported a rigged textured model and two real emotes, switched animations, scrubbed to frame15, stepped to16, exported BLEND/GLB/FBX and saved/reopened a portable Studio project.
- Verified the same WebView instance survives a round trip through both tool tabs; no second displayed application window is created.
- Native captures at1500×960 and1220×760 show centered tabs and usable preview/export controls. Computed group midpoint610 at window width1220.
- Correctly hides the complete Porter body and status footer in the editor tab, and removes the duplicate editor titlebar.
- Closing the host disposes the browser, closes the hidden component window and leaves no hidden WPF window. Lifecycle check passed.
- Self-contained package UI, Unicode installation, payload hashes, repair preserving an unrelated model, upgrade from the previous launcher name, launcher version metadata and installer location/downgrade checks passed.
- Installed top-tab and lifecycle check run with DOTNET_ROOT pointing at an absent runtime to verify the bundled .NET runtime.
- Independent native visual evaluation: PASS.

This verifies the merger and tested editor workflow, not universal game asset or custom rig compatibility. Existing material/effect limitations remain documented in release notes.
