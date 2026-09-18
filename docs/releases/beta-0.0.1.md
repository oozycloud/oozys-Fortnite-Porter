# beta 0.0.1 — Combined animation export and beta reset

**Recorded local delivery · 2026-09-17**

## Changes

- Reset the displayed version from 2.2.0 to beta 0.0.1, including launcher and installer filenames.
- Replaced per-key animation insertion with bulk curve creation to address long skin + emote conversions.
- Built skin and animation data before saving the final combined Blender file; failed animation conversion now reports an error instead of exposing a misleading finished skin-only scene.
- Added animation attachment progress and a live Blender conversion log.
- Selected the main dance action for multipart emotes while preserving other clips as actions.
- Updated the standalone importer with the same animation writer.
- Kept scene selections stable during catalogue searches.
- Allowed installer migration from 2.2.0, blocked newer-beta downgrades and included tagged beta prereleases in update checks.

## Validation and limitations

Recorded validation: Art the Clown and Ballerina Cappuccina source-to-Blender conversions, saved actions, packed textures, timeline duration and moving poses; 67 release checks; 10 installer checks; six standalone-emote imports; and an animation-failure test. Fresh downloads for those two skins stalled at the archive index, so the successful conversion tests used the complete source files from the earlier failed exports. The fresh-download fix is Unreleased. Existing cloth/material limitations remain.

## Installation

Use `oozys-Fortnite-Porter-Installer (beta 0.0.1).exe`. The installed root launcher requires its adjacent `app` folder. The .NET runtime is included. The matching GitHub tag is `beta-0.0.1`; creating these notes does not publish that release.

**Historical source:** Preserved “Release beta 0.0.1.md” and packaged beta test report.

[All versions](README.md) · [Current guide](../../README.md)
