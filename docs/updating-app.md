# Updating Litbolt's App Folder / Local Video Hub

Same model as **What Am I Doing** and **Local Music Hub**:

1. App checks [GitHub Releases](https://docs.github.com/en/rest/releases) (paginated list, highest parseable tag — not only `/releases/latest`).
2. If newer than the installed assembly version, show an in-app banner (dismiss with **Later**).
3. **Settings → Updates** can check manually, open the Releases page, or **Download and run installer…** when a `*-Setup-*.exe` asset is attached.
4. No silent in-app upgrade — the user runs the published installer.

## Repositories

| App | GitHub | Installer asset prefix |
|-----|--------|------------------------|
| Litbolt's App Folder | https://github.com/Litbolt123/Litbolt-App-Folder | `LitboltAppFolder-Setup-` |
| Local Video Hub | https://github.com/Litbolt123/Local-Video-Hub | `LocalVideoHub-Setup-` |

Until the first Release with a setup EXE exists, **Check for updates** will report “no published release” (expected).

## Settings keys (local JSON)

- App Folder: `%LocalAppData%\LitboltAppFolder\settings.json` — `autoCheckUpdates`, dismiss version, last check.
- Video Hub: `%LocalAppData%\LocalVideoHub\app-settings.json` — same shape.
