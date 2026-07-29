# Publishing a Windows release (Litbolt's App Folder + Local Video Hub)

This monorepo builds **two** Inno installers from one version in `Directory.Build.props`.

## Version source

```xml
<!-- Directory.Build.props -->
<Version>0.3.1</Version>
```

Tag must be **`v` + Version** (e.g. `v0.3.1`).

## Before you tag

1. Bump version in `Directory.Build.props` (or `.\scripts\bump-version.ps1`).
2. Edit **`docs/RELEASE_BODY.md`** with user-facing bullets (CI requires ≥80 chars).
3. Commit and push to `main`.
4. Tag and push:

```powershell
git tag v0.3.1
git push origin v0.3.1
```

## What CI does

Workflow: **`.github/workflows/release-windows.yml`**

On tag push:

1. Publishes self-contained **win-x64** folders (LibVLC natives included — not single-file).
2. Compiles Inno → `LitboltAppFolder-Setup-<version>.exe` and `LocalVideoHub-Setup-<version>.exe`
3. Creates a GitHub Release on **this repo** with both assets.

Manual **workflow_dispatch** uploads artifacts only (no Release).

## Local build

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-build-prerequisites.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -App VideoHub
```

Output: `installer\Output\LitboltAppFolder-Setup-<version>.exe` and `LocalVideoHub-Setup-<version>.exe`

## Updater repos (two GitHub repos)

| App | Updater looks at | Setup asset |
|-----|------------------|-------------|
| Litbolt's App Folder | `Litbolt123/Litbolt-App-Folder` | `LitboltAppFolder-Setup-*.exe` |
| Local Video Hub | `Litbolt123/Local-Video-Hub` | `LocalVideoHub-Setup-*.exe` |

After tagging **this** repo, mirror the Video Hub installer:

```powershell
.\scripts\mirror-video-hub-release.ps1
```

Requires `gh` CLI and access to `Litbolt123/Local-Video-Hub`.

## Dev shortcuts (no installer)

Release `dotnet build` syncs to `%LocalAppData%\Programs\...` and refreshes Start Menu + Desktop shortcuts via `scripts\update-windows-shortcuts.ps1` (robocopy `/MIR` removes stale files).
