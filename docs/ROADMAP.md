# Litbolt App Folder — roadmap

Phases for the hub + embedded apps. ✅ = shipped in current builds.

## Phase 0 — Shell correctness ✅

- Real embed + pop-out for module UI (not launcher-only panes)
- Toolbar state when a tab is detached
- Settings → Apps for custom EXE paths
- WAID-style installer quit

## Phase 1 — Video player (MediaElement) ✅

- LMH-style transport icons and seek bar (click-to-point + drag)
- Volume, speed, repeat, queue, keyboard shortcuts
- System tray + close-to-tray
- **Video-only fullscreen** (not the host window) ✅ v0.2.2

## Phase 2 — Downloader embed ✅ (baseline)

- Health / check / download via localhost API
- Paste URL from clipboard ✅ v0.2.2
- **Format + quality picker** in embed panel ✅ v0.3.0

## Phase 3 — Music embed ✅ (baseline)

- Read playlists from `library.db` with track counts ✅ v0.2.2
- Double-click playlist to play
- Volume + play via IPC to Local Music Hub
- **Now-playing readback** from `now-playing.json` ✅ v0.3.0 (requires LMH rebuild)

## Phase 4 — LibVLC / codec parity ✅ (baseline v0.3.0)

- [x] LibVLCSharp hybrid backend (`HybridVideoHost`) for MKV, WebM, AVI, FLV, TS
- [x] Auto-fallback to LibVLC when MediaElement fails
- [x] Subtitles: auto-detect `.srt`/`.vtt`/`.ass` beside video; **Subtitles…** picker
- [x] MediaElement fallback when LibVLC unavailable
- [x] LibVLC audio track / subtitle track picker UI ✅ v0.3.2-prep
- [ ] Hardware decode tuning (later)

## Phase 5 — Full UI embed (later)

- [ ] Extract Local Music Hub + YouTube Downloader into embeddable `UserControl` libraries
- [ ] Single hub process hosts full app UIs without separate EXEs

## Phase 6 — Hub polish (ongoing)

- [x] Single-instance App Folder + Local Video Hub + `activate.signal` ✅ v0.3.0
- [x] Now-playing readback from Music Hub ✅ v0.3.0
- [x] Downloader format/quality picker ✅ v0.3.0
- [x] Update tray balloon when GitHub release is available ✅ v0.3.0
- [x] Inno installer scripts + `build-installer.ps1` / `publish-installer.ps1` ✅ v0.3.1 (local compile OK)
- [x] `.github/workflows/release-windows.yml` (both Setup EXEs on tag)
- [x] `scripts/mirror-video-hub-release.ps1` (post-tag upload to Local-Video-Hub repo)
- [ ] First GitHub Release upload (tag `v0.3.1` + push)
- [ ] Mirror `LocalVideoHub-Setup-*.exe` to `Litbolt123/Local-Video-Hub` release

---

## Next session — after first release

- Smoke-test updaters in Settings → Updates for both apps
- Phase 4 polish: LibVLC audio/subtitle track picker UI
- Phase 5 spike: Music/Downloader as embeddable `UserControl` libraries
