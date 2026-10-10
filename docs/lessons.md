# Lessons

What worked and what did not. **Read this before repeating a fix.** After a fix or a failed approach, append a class (not a chat transcript): Symptom · Failed · Worked · Verify · Do not regress.

Vault index: `C:\Users\Augus\OneDrive\Documents\Obsidian Vault\Atlas\Lessons.md`


Also read `GitHub projects\docs\app-development-pipeline.md` section 10.

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
