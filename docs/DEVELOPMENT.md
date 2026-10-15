# Build and recovery notes

The original source was deleted. This checkout was reconstructed from the installed beta 0.1.1 managed assemblies and embedded resources, then extended and tested. It is not a verbatim recovery of comments or the original file layout.

## Layout

- `work/FortniteObj/Porter.csproj`: recovered WPF application, exporter code and Blender scripts.
- `work/Installer/Installer.csproj`: recovered installer; its payload is generated during packaging.
- `work/AssetChecks`: live game-asset test harness.
- `work/GameChecks`: compatibility cache fixtures, public-service check and Unity export checks.
- `recovery/unity-runtime`: bundled Python 3.12.10 with UnityPy 1.25.2 and pinned dependencies, including their licenses.
- `work/FormatChecks`: exchange-format conversion checks.
- `recovery/dependencies`: preserved third-party dependencies needed by the recovered projects.
- `recovery/licenses`: preserved third-party notices.
- `recovery/original/launcher.exe`: preserved native launcher. The package script updates its version resource.
- `recovery/package-current.py`: beta 0.4.3 packaging script.

## Build

On Windows x64, install the .NET 10 SDK and Python. Blender is needed for conversion tests. Run from the extracted source root:

```powershell
dotnet build work/FortniteObj/Porter.csproj -c Release
dotnet publish work/FortniteObj/Porter.csproj -c Release -r win-x64 --self-contained true -o work/publish-beta0.4.3/app
python -X utf8 recovery/package-current.py
dotnet publish work/Installer/Installer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o work/installer-beta0.4.3
```

The source backup excludes game data, export caches, generated installers, installer payloads and intermediate build folders. The package script regenerates the installer payload. Keep the dependency DLLs and their notices with this recovered checkout.

## Future releases

Increment patches by 0.0.1, medium updates by 0.1.0 and major updates by 1.0.0. Because constants were inlined in the compiled build, the recovery contains version strings in multiple files: update ProductInfo, UpdateChecker, assembly metadata, XAML labels and packaging scripts together. Preserve historical release notes. Test the actual self-contained installer and animation exports before delivery.

A GitHub Release is separate from updating repository documentation. No binary release is published by these build commands.
