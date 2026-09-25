# Development and releases

This public repository currently contains distribution documentation; the application source tree described below is maintained separately. The build commands require that full source checkout.

## Source layout

| Path | Purpose |
| --- | --- |
| `work/FortniteObj` | .NET 10 WPF app, asset conversion and embedded Blender scripts |
| `work/Shared/ProductInfo.cs` | Product identity, displayed version, tag, launcher name and update compatibility |
| `work/Launcher` | Native Windows launcher; MSVC and Windows SDK build |
| `work/Installer` | WPF installer and embedded payload |
| `work/BackendCheck` | Tests against locally installed game assets |
| `work/ReleaseChecks` | Installer transaction and update-checker tests |
| `docs/releases` | Individual release notes and the Unreleased entry |

Commands below run from the source checkout root on Windows. Build requirements are the .NET 10 SDK, MSVC/Windows SDK for the launcher, and Blender for conversion tests. The installed app bundles its runtime and does not require the SDK.

```powershell
dotnet build work/FortniteObj -c Release
dotnet build work/BackendCheck -c Release
dotnet run --project work/ReleaseChecks -c Release
```

Native UI checks can be run with `work/FortniteObj/bin/Release/net10.0-windows/Porter.exe --ui-check <output-folder>`. Asset tests require a configured local Fortnite installation; they may download missing streamed data. Source changes do not update the Desktop installation automatically.

## Release procedure

1. Finish the intended changes and review remaining limitations. Record specific tested assets and failures.
2. Choose the next version: +0.0.1 for patches, +0.1.0 for medium updates, +1.0.0 for major updates. Preserve the beta prefix during beta development.
3. Update shared metadata, application/installer project versions, launcher resources and packaging filenames together.
4. Create `docs/releases/<tag>.md` with changes, validation, limitations and installation/migration notes. Add it to `docs/releases/README.md` and `CHANGELOG.md`; update the main README's build status and filenames. Leave unfinished work under Unreleased.
5. Build the app self-contained for `win-x64` under `app/`, compile the native launcher and bundle the Blender tools. Include README.md, CHANGELOG.md and the complete docs tree in `app/` before creating the installation manifest or payload hashes.
6. Generate the installer's `payload.zip` from the launcher-plus-app layout, then publish the self-contained installer. Historical `package-beta.py` and `finalize-beta.py` are version-specific scripts, not safe general release commands; adapt their version and validation data before using them.
7. Test a clean installation, existing-install update, same-version repair, rollback/locked-file failure, settings preservation, future-version rejection, runtime-independent launch and the actual packaged UI. Verify the delivered files against the payload hashes.
8. Deliver the installer and complete application folder. Keep the root folder tidy; documentation and support files belong under `app/`.
9. When publishing is authorized, create the matching GitHub Release and attach the installer. Beta tags use `beta-<number>` and may be prereleases. Uploading a repository file alone does not trigger the update checker.

## Release-note template

```markdown
# beta X.Y.Z — Brief change summary

## Changes

- Describe the user-visible change and the problem it addresses.

## Validation

- List the tests actually run against this build and the assets checked.

## Known limitations

- Record remaining issues that affect the documented features.

## Install or update

State the installer filename, supported migration and any required re-export.
```

Public release notes should not include private machine paths, speculative fixes, fabricated test counts or promises of universal asset compatibility. Retain bundled third-party notices with distribution files.
