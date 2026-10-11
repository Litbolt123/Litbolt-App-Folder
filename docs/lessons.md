# Lessons

What worked and what did not. **Read this before repeating a fix.** After a fix or a failed approach, append a class (not a chat transcript): Symptom · Failed · Worked · Verify · Do not regress.

Vault index: `C:\Users\Augus\OneDrive\Documents\Obsidian Vault\Atlas\Lessons.md`


Also read `GitHub projects\docs\app-development-pipeline.md` section 10.

## WPF People grid scroll jank — sync decode + nested scroll

- **Symptom:** People tab scroll flickers then settles; worse with 70+ folders.
- **Failed:** `WrapPanel` inside outer `ScrollViewer` with `LoadThumb` on refresh for every card on the UI thread.
- **Worked:** People `ListBox` with `ScrollViewer.CanContentScroll=False`, fixed 148×170 cards, `PersonFaceImage` + `ThumbnailCache` async decode. Page outside main tab scroll.
- **Verify:** Scroll People grid with 70+ entries — no layout jump as thumbs load; CPU stays low while scrolling.
- **Do not regress:** `PagePeople` outside `TabScrollHost`; do not block `RefreshPeople` with synchronous decode for all folders.

## People grid virtualization — clipped rows and truncated scroll

- **Symptom:** Top rows clip while scrolling; scrollbar stops after ~24 people with empty space below (70+ folders on disk).
- **Failed:** Custom `VirtualizingWrapPanel` with `ItemHeight=172` while `ListBoxItem` also has `Margin 0,0,8,8` (row pitch mismatch); fragile `IScrollInfo` / generator remove.
- **Worked:** Non-virtualized `WrapPanel` + `VirtualizingPanel.IsVirtualizing=False` (~100 people OK); fixed `Height=170` on items; `PersonFaceImage` async thumbs unchanged.
- **Verify:** Scroll People from first card through **Zion Walsman** (last alphabetically); full scrollbar range, no clipped row tops. **Playtest 2026-10-10:** August confirmed fixed.
- **Do not regress:** Do not re-enable custom virtualizing wrap without matching item height + margin to panel line pitch.

## Face-groups photo count — face_* only folders

- **Symptom:** People tab shows **0 photos** while a `face_*.jpg` thumbnail is visible (e.g. BC).
- **Failed:** Counting only non-`face_` files; many folders only have reference crops from assign/widget tools.
- **Worked:** `CountPhotosInPersonFolder` — prefer full-image count; if zero, count `face_` / `_face_` crops. Thumbnail picks face crop first.
- **Verify:** `BC` under `_face-groups` with only `face_1.jpg`…`face_6.jpg` shows **6 photos**.
- **Do not regress:** `FaceGroupsService.ListPeople`.

## Rainmeter Now Playing — WebNowPlaying vs Spotify desktop

- **Symptom:** WebNowPlayer shows **Active 1 / Spotify** but Title, Artist, Cover, State, and transport are empty; `PlayerId="active"` matches the example skin; Rainmeter.log has **no** WebNowPlaying errors with `Logging=1` / `Debug=1`.
- **Failed:** Assuming `PlayerId` must be numeric from `GetPlayerIds()` — same empty metadata with `"active"` and numeric id; WebNowPlaying **alpha.4** (134 KB DLL) left in place (alpha.5 is ~1 MB backup; swap untested in UI this session).
- **Worked:** **GSMTC** helper in `Skins\Litbolt\@Resources\LitboltMedia\` (not `Now Playing\@Resources` — `#@#` is the Litbolt skin root); `watch-loop` background process writes `nowplaying.ini`; Lua `nowplaying.lua` reads every 1 s with `DynamicVariables`; transport via quoted `HelperExe`; cover `cover.jpg?CoverRev`; Segoe MDL2 transport icons.
- **Verify:** Spotify playing or paused → title/artist/art within 1–2 s; prev/play/next work; song change bumps cover cache.
- **Do not regress:** Do not use `#CURRENTPATH#@Resources` or `@Include` `.inc` for live data; do not `!Refresh` the whole skin on a timer.

## Rainmeter Now Playing — watch killed on skin refresh

- **Symptom:** `nowplaying.ini` goes blank after refreshing the Now Playing skin; transport dies.
- **Failed:** `OnCloseAction` → `stop` on skin unload (runs on refresh before `OnRefreshAction`); `Measure=Run` (invalid on Rainmeter 4.5 — log: *Measure=Run is not valid*).
- **Worked:** Remove skin `OnCloseAction`; watch loop exits when **no `Rainmeter.exe`** process; `watch` no-ops if `watch.pid` process alive; skin uses **`Plugin=RunCommand`**; logon task `Litbolt\MediaWatchBootstrap` runs `ensure-media-watch.ps1`.
- **Verify:** Refresh skin 5× — same `watch.pid`, title stays populated; quit Rainmeter — watch exits; sign-in + Rainmeter → bootstrap starts watch.
- **Do not regress:** No `OnCloseAction` stop on Now Playing; use RunCommand not Measure=Run.

## PowerShell feeder scripts — Unicode em dash on Windows

- **Symptom:** `today-feed.ps1` fails to parse with “Unexpected token” near countdown strings.
- **Failed:** Unicode em dash (`—`) in double-quoted strings when file encoding / console code page disagrees.
- **Worked:** Use ASCII ` - ` in `.ps1` string literals; keep fancy dashes only in `.md` / Rainmeter display `.inc` if UTF-8 is explicit.
- **Verify:** `powershell -File today-feed.ps1` exits 0 from `Skins\Litbolt\feeders`.
- **Do not regress:** Feeder scripts under `rainmeter-skins/Litbolt/feeders`.

## WPF theme StaticResource order in merged dictionary

- **Symptom:** People grid / capture crashes: `Cannot find resource named 'BrushCard'` or `BrushText` in `ListBoxItem` template.
- **Failed:** `StaticResource` brushes defined *after* `PeopleGridListBox` style in `PhotoHubTheme.xaml`.
- **Worked:** Define `SolidColorBrush` / fonts at top of theme dictionary before styles and templates; use `DynamicResource` in item templates if order cannot change.
- **Verify:** `--capture-ui … people` at 1366×768 without `XamlParseException`.
- **Do not regress:** `PhotoHubTheme.xaml` resource order.

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
