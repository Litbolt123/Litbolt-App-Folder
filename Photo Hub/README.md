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

## Tabs (what each section does)

### Sort

1. **Sort new photos** — Choose folder (default `Pictures\iPhone Backup`), then **Scan and sort**. Runs `sort_inbox.py`; copies into `_face-groups`; logs to `Pictures\People\_fold-log.txt`.
2. **Name unknown faces** — After a sort, assign each unknown face to a person folder by name.

### People

1. **Browse people** — Wrap grid of `_face-groups` folders (async thumbnails). Click a card to select.
2. **Fix a person** — **Refresh list**, **Rename person…**, **Merge into another…**, **Add full photos from face crops** (selected card only). All folder changes log to `_fold-log.txt`.

### Widget

- **Update widget** (top) — Runs `Widget Photos\Update-FeaturedPhotos.ps1`.
- **Who's featured** — Toggle people and August pairs; **Save featured list** → `featured-people.txt`.
- **Specific photos** — **Pin a photo…** (`photo:` lines), **Hide this photo from the widget**, excluded list, **Show a hidden photo again**.
- **Look** — Sliders for `Photo Widget.ini` Size, Seconds, Radius (and `@Resources\current-photo.inc` when hiding).
- **On/off** — Photo Widget skin visibility; illustro skin checkboxes.

### Photos

1. **Fix photos** — Straighten Featured JPGs; convert HEIC to JPG in a folder (copies only).
2. **Refresh widget pools** — **Refresh from backup** (`widget-NN.jpg`); **Refresh from People** (`people-NN.jpg`) in `Widget Photos` only.

### iPhone

1. **Back up iPhone** — Backup path, status, **Sync now**, **Open full sync app**.
2. **After backup** — **Sort new photos** (jumps to Sort with backup folder).

### Find

Search `_face-groups` by person, August pair, year, or file name; open a match in Explorer.

### Undo

Reverse recent COPY / MERGE / RENAME from `_fold-log.txt` (COPY undo checks SHA-256).

### Log

In-app session log only (not `_fold-log.txt`).

## Litbolt's App Folder

Listed in **Litbolt's App Folder** launcher. Override EXE in **Settings → Apps** if needed.

## Safety

- Originals are not deleted by sort, merge, or HEIC convert.
- Widget refresh replaces only `widget-*` and `people-*` files in `Widget Photos`.
- Heavy jobs run below-normal priority and wait when free RAM is under ~2 GB.
