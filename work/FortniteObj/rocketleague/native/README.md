# Rocket League object reader

`porter-rl-dump.exe` is a statically linked Windows x64 derivative of UEViewer's MIT-licensed `Tools/PackageExtract`, at ShinyEmii/UEViewer commit `1b6977ec1c8414acec4cbc183b50d88823adc212`. The existing `LICENSE.txt` notice applies.

The files here replace the upstream tool entry point and build configuration. Changes preserve archetype/outer references, complete object tables and prefix class filters, and flatten object payload filenames to avoid Windows path limits. A guarded Windows header include makes CoreWin32 compile with GCC. No game assets are embedded in this reader.

Run `./build.ps1` with Git, CMake and MinGW-w64 GCC available. It fetches the pinned upstream source into a new temporary folder and copies the resulting executable beside this directory. The build uses static C/C++ runtime libraries; a separate MinGW runtime installation is not required to run the reader.

The Python reader validates tagged property bounds and resolves local archetypes. Level exports traverse authored streaming references. Unsupported engine systems remain listed in the export report; this is not an Unreal runtime.
