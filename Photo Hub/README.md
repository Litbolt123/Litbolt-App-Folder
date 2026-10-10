# Photo Hub

Litbolt desktop app for face-sorting photos, managing `_face-groups` people folders, iPhone backup sync, and the Rainmeter Photo Widget.

## Requirements

- Windows 10/11
- [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) (to build)
- Python venv at `C:\Users\Augus\Pictures\_sort-tools\venv` (face pipeline + HEIC convert)
- Rainmeter with the Photo Widget skin (optional)
- **iPhone Photo Sync** built to `C:\Users\Augus\Projects\iPhone Photo Sync\artifacts\iPhone Photo Sync.exe` (or override in code) for the iPhone tab

## Build and shortcuts

```powershell
dotnet build -c Release
```

**Release** syncs to `%LocalAppData%\Programs\PhotoHub\` and refreshes the **Start Menu** shortcut only (no Desktop shortcut).

## How to use (numbered)

1. **iPhone** — Confirm backup folder (`Pictures\iPhone Backup`). **Sync (headless)** runs iPhone Photo Sync with `--photo-hub-cli sync`. Fix any `.partial` stuck files, then **Sort backup in Photo Hub**.
2. **Sort new photos** — Scan the backup (or another folder). Strict matching: det ≥ 0.80, face ≥ 48 px, match ≥ 0.62, margin ≥ 0.08. Matches are **copied** into person folders; logged in `Pictures\People\_fold-log.txt`. `Important` is never scanned. Assign unknown faces by name.
3. **People** — Rename or merge folders (copies only; originals kept). Logged for undo.
4. **Photos** — **Straighten Featured JPGs**, **HEIC → JPG** in a folder (copies only), **Refresh from backup** (`widget-NN.jpg`), **Refresh from People** (`people-NN.jpg`).
5. **Widget** — Featured people/pairs, excludes, pinned `photo:` lines, Rainmeter size/seconds/radius, Photo Widget on/off, illustro toggles. **Save featured list**, then **Update widget** (`Update-FeaturedPhotos.ps1`).
6. **Find** — Search `_face-groups` by person, year, file name; open in Explorer.
7. **Undo** — Pick a recent COPY/RENAME/MERGE line. COPY undo **deletes the destination only if SHA-256 still matches the source**; otherwise it skips and warns.
8. **Log** — Sort progress, widget tools, and errors.

## Litbolt's App Folder

Listed in **Litbolt's App Folder** launcher. Override EXE in **Settings → Apps** if needed.

## Safety

- Originals are not deleted by sort, merge, or HEIC convert.
- Widget refresh replaces only `widget-*` and `people-*` files in `Widget Photos`.
- Heavy jobs run below-normal priority and wait when free RAM is under ~2 GB.
