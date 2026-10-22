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
- `recovery/package-current.py`: beta 0.6.0 packaging script.

## Build

On Windows x64, install the .NET 10 SDK and Python. Blender is needed for conversion tests. Run from the extracted source root:

```powershell
dotnet build work/FortniteObj/Porter.csproj -c Release
dotnet publish work/FortniteObj/Porter.csproj -c Release -r win-x64 --self-contained true -o work/publish-beta0.6.0/app
python -X utf8 recovery/package-current.py
dotnet publish work/Installer/Installer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o work/installer-beta0.6.0
```

The source backup excludes game data, export caches, generated installers, installer payloads and intermediate build folders. The package script regenerates the installer payload. Keep the dependency DLLs and their notices with this recovered checkout.

## Future releases

Increment patches by 0.0.1, medium updates by 0.1.0 and major updates by 1.0.0. Because constants were inlined in the compiled build, the recovery contains version strings in multiple files: update ProductInfo, UpdateChecker, assembly metadata, XAML labels and packaging scripts together. Preserve historical release notes. Test the actual self-contained installer and animation exports before delivery.

A GitHub Release is separate from updating repository documentation. No binary release is published by these build commands.

## Catalogue IDs and parser provenance

The app embeds `work/FortniteObj/website-id-registry.json`. The website uses the matching `website/assets/catalog.json`. Refresh metadata with `recovery/build-website-catalog.py` after producing the local game catalogue and style-channel metadata; preserve the existing ID registry so catalogue sorting never reassigns IDs. Each style ordinal uses radix 1024 per independent channel and arbitrary-precision integers. The app rejects invalid option gaps and overflow.

Managed CUE4Parse dependencies were rebuilt from revision `605672df7a993a90094bad92d30c388d87ae6c1a`; see `recovery/licenses/PROVENANCE.txt`. The source archive includes the small version-option cloning patch under `recovery/parser-patches`. Runtime libraries, including System.Numerics.Tensors, are explicit project references. The native ACL decoder is the preserved version documented in provenance.

Run `dotnet run --project work/AssetChecks -c Release -- --website-id-checks` for the full ID/style contract and parser-option regression check. `Porter.exe --ui-check <output-folder>` tests the WPF interaction and archive-reader UI context. Live export checks require local game files and Blender.

## Rocket League reader

`work/FortniteObj/rocketleague_assets.py` builds the local catalogue and drives the bundled UEViewer reader. `rocketleague_blend.py` imports extracted glTF meshes, reconstructs supported material references and packs images. `RocketLeagueSession.cs` handles cancellation, formats and export reports.

Reader source: https://github.com/ShinyEmii/UEViewer, revision `1b6977ec1c8414acec4cbc183b50d88823adc212`. Binary: the project's `EncryptedTFC` release (downloaded 22 October 2026). MIT notice is included in `work/FortniteObj/rocketleague/LICENSE.txt`. Public product/map metadata and compatibility keys: https://github.com/ShinyEmii/Toga-Files. The bundled metadata removes key columns; the reader's key list refreshes from that public source. These resources do not include game models or textures.

`dotnet run --project work/AssetChecks -c Release -- <output> --rocket-check` checks a real scan, invalid decal rejection and Octane/Stripes packed Blender + GLB. Blender and the local game must be installed. Run `recovery/test-package.ps1` with PowerShell 7 after packaging.
