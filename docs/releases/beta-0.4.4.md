# beta 0.4.4

Scans now run without disabling browsing, settings or game navigation. You can search a previously loaded catalogue, change preferences and switch games while files are being read. Each game retains its own catalogue, and a scan finishing in the background updates the correct game.

The active scan uses the settings captured when it started. New scans and exports remain unavailable until it finishes or is cancelled. Fortnite style lookups wait while its archives are being rebuilt. A failed or cancelled rescan clears its stale catalogue so it cannot export through a disposed archive reader.

The game selector now uses an explicit dark background and control template, preventing the Windows theme from showing a white panel behind it.

Validation covers navigation and settings during a controlled background scan, progress retention across game changes, result routing, catalogue retention, cancellation and conflicting-operation controls. The existing title bar and UI checks remain in place.

Preserved previous builds and this build are available through GitHub Releases. See [available builds](AVAILABLE-BUILDS.md) for which historic binaries survived the source-folder deletion.
