## 2026-10-10 — Photo Hub (Litbolt app set)

- **User:** Build Photo Hub WPF (.NET 8) for face sort / people / Rainmeter widget; place under `Photo Hub/`; match Litbolt App Folder styling, shortcuts, launcher pattern.
- **Done:** New `Photo Hub/PhotoHub.csproj` (Litbolt.Shell tray/single-instance, dark UI like Local Video Hub, Release → `%LocalAppData%\Programs\PhotoHub\` + Start Menu shortcut via shared `scripts/update-windows-shortcuts.ps1`). Python orchestrator `Pictures\_sort-tools\photo_hub\sort_inbox.py` reuses `audit2.detect` + strict thresholds (0.80/48/0.62/0.08). Wired into solution + App Folder nav (`PhotoHubModule`, settings EXE override). README in `Photo Hub/README.md`.

## 2026-10-10 — Photo Hub 0.2 (widget refresh, iPhone, undo)

- **User:** Both widget refresh buttons (backup + People); full widget tab; iPhone tab; find + undo COPY with hash match; commit per step; no Desktop shortcut.
- **Done:** `WidgetRefreshService`, Photos tab, expanded Widget tab (excludes, illustro, sliders). iPhone tab + `--photo-hub-cli sync` in iPhone Photo Sync. Find tab + Undo tab (`FoldLogUndoService` SHA-256 gate on COPY). v0.2.0 README numbered flow.

## 2026-07-28 — Distribution installers + Phase 4 track pickers

- **User:** Confirm Start Menu/shortcuts updated; continue roadmap phases.
- **Shortcuts:** Yes — v0.3.1 at `%LocalAppData%\Programs\...`; Release build re-synced shortcuts (Start Menu + Desktop) via `update-windows-shortcuts.ps1`.
- **Distribution:** Fixed Inno compile (`Litbolt's` apostrophe in Pascal `[Code]` strings). Both installers built locally:
  - `installer/Output/LitboltAppFolder-Setup-0.3.1.exe`
  - `installer/Output/LocalVideoHub-Setup-0.3.1.exe`
- **Release next:** Tag `v0.3.1` + push → CI uploads both Setup EXEs; then `.\scripts\mirror-video-hub-release.ps1` for Local-Video-Hub updater repo.
- **Phase 4:** LibVLC **Audio** / **Subs** dropdowns in video player (when LibVLC active); refresh on media open + after loading external subtitle file.
- **Bugfix:** Phone portrait MP4/MOV now play via LibVLC first (honors rotation metadata); MediaElement is fallback only. Subtitles button auto-switches to LibVLC when needed.
- **Rotate:** Manual **Rotate** button cycles 0°→90°→180°→270° (keyboard **R**); saved per file in `settings.json` → `fileRotations`. Auto-reads QuickTime/MOV rotation metadata on first open (e.g. portrait phone clips).
- **Icons:** Regenerated `app.ico` for both apps — black rounded edges, no white corner artifacts (`scripts/compose-app-icon.ps1`).

## 2026-07-27 — Quick polish v0.3.1 + tomorrow plan

- **User:** No fps/resolution UI if we decode at file resolution; quick polish tonight; roadmap tomorrow.
- **Playback:** Yes — native decode resolution; UI uses `Stretch.Uniform` (aspect-correct letterbox, no forced upscale metadata). No resolution/fps display planned.
- **Polish:** Tray menu nullability fix; pop-out windows get `app.ico`; high-quality bitmap scaling on video stage.
- **Tomorrow:** `docs/ROADMAP.md` → **Next session** table: Inno + CI + first GitHub Release for App Folder / Video Hub.

## 2026-07-27 — Roadmap v0.3.0 (Phase 4 + Phase 6 polish)

- **User:** Continue roadmap for apps.
- **Phase 6 shipped:**
  - Single-instance + `activate.signal` for App Folder and Local Video Hub (`Litbolt.Shell.SingleInstanceService`, `ActivateSignalWatcher`).
  - Update tray balloon once per session when a newer GitHub release is found.
  - Downloader embed: format (MP3/M4A/…/MP4) + quality picker; passes to `/download` API.
  - Music embed: **Now playing** panel reads `%LocalAppData%\LocalMusicHub\now-playing.json` (LMH writes on track/state change).
- **Phase 4 shipped (baseline):**
  - `HybridVideoHost` — LibVLCSharp for MKV/WebM/AVI/FLV/TS; MediaElement for MP4/MOV; auto-fallback on `MediaFailed`.
  - Subtitles: auto sidecar + **Subtitles…** button; LibVLC `:sub-file` option.
  - Packages: LibVLCSharp 3.9, VideoLAN.LibVLC.Windows 3.0.21; exe TFM → `net8.0-windows` (avoids WinApp SDK PriGen on .NET 10 SDK).
- **LMH:** `NowPlayingStateService.cs` — writes `now-playing.json` on playback events (rebuilt/synced).
- **Version:** 0.3.0; Release synced to `%LocalAppData%\Programs\LitboltAppFolder\` and `LocalVideoHub\`.

## 2026-07-27 — Pipeline doc: lessons from mistakes

- **User:** Add what was learned from mistakes to the app development pipeline doc.
- **Done:** Expanded `GitHub projects/docs/app-development-pipeline.md` section 10 — symptom/cause/fix tables for platform, WPF, seek sliders, LMH audio, updates, hub embed, performance, inter-app, and agent process mistakes.

## 2026-07-27 — App development pipeline doc (shared)

- **User:** Pipeline doc for agents covering all Litbolt apps (WAID, LMH, YT, Harbor, App Folder, Video Hub, PSE).
- **Delivered:** `GitHub projects/docs/app-development-pipeline.md` — ecosystem map, agent workflow, integration contracts, tiered platform requirements, copy-from map, prompt snippets. Linked from platform checklist + master prompt.

## 2026-07-27 — Video-only fullscreen + roadmap v0.2.2

- **User:** Fullscreen should target the video, not Litbolt's App Folder shell; continue phase roadmap.
- **Fullscreen:** `VideoFullscreenWindow` reparents `MediaElement` into a borderless maximized window (hub stays normal). Esc or double-click video exits.
- **Phase 3 polish:** Music playlists show track counts; double-click to play; running status. Downloader **Paste URL** button.
- **Docs:** `docs/ROADMAP.md` — Phases 0–3 done; Phase 4 LibVLC next.

## 2026-07-27 — LMH-style transport + click-to-seek + tray icons

- **User:** Click on seek bar snaps back (drag only worked); want LMH button UI/playback; tray needs real app icons.
- **Seek fix (LMH pattern):** `PreviewMouseLeftButtonUp` with `handledEventsToo: true` (required when `IsMoveToPointEnabled`), `LostMouseCapture`, `ValueChanged` during scrub, deferred commit via `Dispatcher.BeginInvoke`, thumb `DragStarted`/`DragCompleted`.
- **Transport UI:** `VideoTransportUi` — LMH line icons, accent play/pause button, prev/next/stop/repeat, elapsed | slider | duration layout.
- **Tray icons:** `TrayIconAssets` loads `app.ico` from beside the EXE first (Release copies it), then pack URI fallback.

## 2026-07-27 — Video seek slider fix

- **User:** App crashes / cannot scrub when moving the video position slider (App Folder + standalone LVP).
- **Cause:** Seek only handled `PreviewMouseLeftButtonUp` on the slider track; thumb drags use `Thumb.DragCompleted` instead, so seeks never applied cleanly and `MediaElement.Position` was set unsafely during playback.
- **Fix:** `IsMoveToPointEnabled`, `Thumb.DragStarted`/`DragCompleted`, `ScrubbingEnabled`, pause → seek → resume via `SeekTo()`, clamp/guard duration math.

## 2026-07-27 — System tray (v0.2.1)

- **User:** System tray support for Litbolt's App Folder and Local Video Hub.
- **Shipped:** Tray icon on startup (app.ico); double-click or **Open** restores window; **Quit** fully exits. Closing the window minimizes to tray when enabled (default on). Settings → **System tray**: “Close to system tray” and “Start with Windows” (launches with `--minimized`). Shared `AppTrayIcon` + `AutoStartService` in `Litbolt.Shell.Tray`. Installer download still fully quits (not tray).

## 2026-07-27 — Embedded apps + full video player (v0.2.0)

- **User:** Video player feature-complete like VLC/WMP; all apps embed in App Folder with pop-in/pop-out; fix bugs.
- **Shipped (App Folder v0.2.0):**
  - **Local Video Hub (embedded):** play/pause toggle, prev/next, auto-advance, time labels, volume/mute, speed, repeat (off/all/one), fullscreen, keyboard shortcuts, open file, drag-drop folders/files, recursive scan, filter, recent files, queue, MediaFailed skip. Settings persisted in `%LocalAppData%\LocalVideoHub\settings.json`.
  - **Music (embedded panel):** reads playlists from `%LocalAppData%\LocalMusicHub\library.db`; play playlist + volume via IPC (`playlist-request.json`, `volume-request.json`); open full Local Music Hub.
  - **Downloader (embedded panel):** talks to YouTube Downloader localhost API (`/health`, `/check`, `/download`) using token/port from `%LocalAppData%\YouTubeToMp3\settings.json`.
  - **Shell:** real UI pop-out for all modules (not launcher-only); toolbar state fixed when detached; Settings → Apps for custom EXE paths; installer download quits app (WAID-style).
- **Not in this release:** LibVLC (MKV/subtitles/codec parity); full Music Hub / Downloader UI inside shell (would need refactor to UserControl libraries).
- **Build:** Release synced to `%LocalAppData%\Programs\LitboltAppFolder\` and `LocalVideoHub\`.

## 2026-07-21 — App icons (v0.1.3)

- **User:** Icons for the new apps.
- **Shipped:** Litbolt's App Folder (folder + colored app tabs) and Local Video Hub (film frame + play) as `app.ico`; wired into EXE + Desktop/Start Menu shortcuts. Source PNGs under each project `Assets/`.

## 2026-07-21 — WAID-style update flow (v0.1.2)

- **Shipped:** Shared `GitHubUpdateClient` (paginated max tag, Setup asset, download-to-temp). App Folder + Local Video Hub: startup check (~12s), update banner + Later, Settings → Updates.
- **Repos:** `Litbolt123/Litbolt-App-Folder`, `Litbolt123/Local-Video-Hub` (no releases yet — check reports empty until first Setup upload).
- **Docs:** `docs/updating-app.md`.

## 2026-07-20 — Rename video app → Local Video Hub (v0.1.1)

- **User:** Video player product name should be **Local Video Hub** (not Litbolt Video).
- **Change:** `LocalVideoHub.exe` / `LocalVideoHub.Core`; nav + shortcuts updated; legacy LitboltVideo path still resolved as fallback.

## 2026-07-20 — Update flow deferred to tomorrow

- **User:** Tomorrow — add the same update flow as **WAID** (What Am I Doing) to Litbolt's App Folder. Not tonight.

## 2026-07-20 — Project born (v0.1.0)

- **Name:** Litbolt's App Folder.
- **Shipped:** Home + Music + Video + Downloader nav; Pop tab out / Redock; Open standalone; Video library player (MediaElement) + LitboltVideo.exe.
- **Music/Downloader:** external launcher modules to installed EXEs.
- **Shortcuts:** Desktop + Start Menu after Release build.

## 2026-07-20 — Project born

