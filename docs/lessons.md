# Lessons

What worked and what did not. **Read this before repeating a fix.** After a fix or a failed approach, append a class (not a chat transcript): Symptom · Failed · Worked · Verify · Do not regress.

Vault index: `C:\Users\Augus\OneDrive\Documents\Obsidian Vault\Atlas\Lessons.md`


Also read `GitHub projects\docs\app-development-pipeline.md` section 10.

## WPF People grid scroll jank — sync decode + nested scroll

- **Symptom:** People tab scroll flickers then settles; worse with 70+ folders.
- **Failed:** `WrapPanel` inside outer `ScrollViewer` with `LoadThumb` on refresh for every card on the UI thread.
- **Worked:** People page uses its own `ListBox` + `VirtualizingWrapPanel` (`PhotoHub.Panels`), `ScrollViewer.CanContentScroll=False`, fixed 148×172 cards, `PersonFaceImage` + `ThumbnailCache` (LRU, `DecodePixelWidth` 160, `DispatcherPriority.Background`). Other tabs stay in `TabScrollHost`.
- **Verify:** Scroll People grid with 70+ entries — no layout jump as thumbs load; CPU stays low while scrolling.
- **Do not regress:** `PagePeople` outside `TabScrollHost`; do not block `RefreshPeople` with synchronous decode for all folders.

## Face-groups photo count — face_* only folders

- **Symptom:** People tab shows **0 photos** while a `face_*.jpg` thumbnail is visible (e.g. BC).
- **Failed:** Counting only non-`face_` files; many folders only have reference crops from assign/widget tools.
- **Worked:** `CountPhotosInPersonFolder` — prefer full-image count; if zero, count `face_` / `_face_` crops. Thumbnail picks face crop first.
- **Verify:** `BC` under `_face-groups` with only `face_1.jpg`…`face_6.jpg` shows **6 photos**.
- **Do not regress:** `FaceGroupsService.ListPeople`.

## PowerShell feeder scripts — Unicode em dash on Windows

- **Symptom:** `today-feed.ps1` fails to parse with “Unexpected token” near countdown strings.
- **Failed:** Unicode em dash (`—`) in double-quoted strings when file encoding / console code page disagrees.
- **Worked:** Use ASCII ` - ` in `.ps1` string literals; keep fancy dashes only in `.md` / Rainmeter display `.inc` if UTF-8 is explicit.
- **Verify:** `powershell -File today-feed.ps1` exits 0 from `Skins\Litbolt\feeders`.
- **Do not regress:** Feeder scripts under `rainmeter-skins/Litbolt/feeders`.

## Photo Hub Widget tab — long single column scroll

- **Symptom:** Featured people list pushes preview/sliders/update to bottom; hard to use on laptop screens.
- **Failed:** Single `StackPanel` inside `TabScrollHost` with one tall column of checkboxes.
- **Worked:** `PageWidget` sibling to `TabScrollHost` (like People): fixed 296px left card, right `ScrollViewer`, bottom sticky apply bar; chip wrap + search/filter; ini/featured batch via **Apply changes to widget**.
- **Verify:** At 1366×768, preview + apply bar visible without scrolling left column; pending count enables pulsing gold Apply.
- **Do not regress:** Widget page must not live only inside `TabScrollHost`.

## HWiNFO free edition — Gadget registry vs shared-memory plugin

- **Symptom:** CPU/GPU readings stop after ~12 hours on free HWiNFO when using the HWiNFO Rainmeter plugin (shared memory).
- **Failed:** Settings → Rainmeter shared memory + `HWiNFO.dll` plugin + hex sensor IDs in the skin.
- **Worked:** HWiNFO **Reporting to Gadget** → Registry `HKCU\SOFTWARE\HWiNFO64\VSB` → `ValueRaw{N}` via Rainmeter `Registry` measure; indices in `Helios Health\@Resources\sensors.inc`. [Rainmeter docs](https://docs.rainmeter.net/tips/hwinfo/).
- **Verify:** `reg query HKCU\SOFTWARE\HWiNFO64\VSB` shows `ValueRaw*` for your indices; skin temps match HWiNFO after 12+ hours.
- **Do not regress:** Helios Health skin — do not require HWiNFO.dll or shared memory.

## Rainmeter empty variable hide — formula syntax

- **Symptom:** Meters for optional events/countdowns show blank rows or bad `Hidden` parse.
- **Failed:** `Hidden=([#Event1#] = ?)` placeholder syntax.
- **Worked:** `Hidden=([#Event1#]="")` with `DynamicVariables=1`.
- **Verify:** Today skin with empty `Event2` hides meter E2 only.
- **Do not regress:** `Today/Today.ini` event/countdown meters.

## Face-groups backfill — numbered face_*.jpg to full photo

- **Symptom:** Person folder has only `face_1.jpg`…`face_N.jpg` (widget/cluster thumbs); no full originals for count or widget pairing.
- **Failed:** Guessing from `groups.csv` alone (many photos per group); expecting `face_<basename>.jpg` naming.
- **Worked:** `FaceCropBackfillService` (People tab **Add full photos from face crops**): SHA1 crop vs `faces_meta.json` crops, `audit\inv.json` for numbered `face_N.jpg`, `face_<basename>.jpg` → People basename, `_fold-log.txt` COPY fallback. COPY + `FoldLogWriter.LogCopy` tag `photo-hub-face-backfill`. One-off script: `Photo Hub/scripts/bc-backfill-full-photos.py`.
- **Verify:** BC folder has six HEICs plus six face crops; fold-log section `BC face crop full-photo backfill`.
- **Do not regress:** Copy-only; log every copy; skip if dest basename already exists.

## Rainmeter Photo Widget.ini — preserve full skin on slider write

- **Symptom:** Moving Size/Seconds/Radius left only three variable lines; Rainmeter reported no meters and deactivated the skin.
- **Failed:** Regex replace on whole file or `text.Replace("[Variables]", …)` that dropped sections; writing on every slider tick.
- **Worked:** Parse `[Variables]` only; replace `Size`/`Seconds`/`Radius` lines; refuse write without `[MeterPhoto]`; restore `Photo Widget.ini.good-2026-10-10` first; debounce 500 ms / mouse release; clamp Size 150–600; one `!Refresh`.
- **Verify:** Slider min/max (150–600) — ini still contains `[MeterPhoto]` and `[MeasurePhoto]`.
- **Do not regress:** `RainmeterService.WriteWidgetSettings` / `EnsureIniHasMeters`.

## Fold-log COPY undo — destination hash must still match source

- **Symptom:** Undoing a COPY removed a file the user had edited or replaced at the destination.
- **Failed:** Deleting destination whenever the log line exists.
- **Worked:** SHA-256 compare `dest` to `src`; delete only on match; log `UNDO-COPY` and warn when skipped.
- **Verify:** Copy a test file via sort; edit dest; Undo → skip + warning. Unedited dest → file removed.
- **Do not regress:** `FoldLogUndoService.UndoCopy` in Photo Hub.

## 2026-08-24 — release tag

**Failed:** Assuming GitHub already has v0.3.1.

**Worked:** Local v0.3.1; first GitHub Release / Video Hub mirror still pending. See Remaining.md.
