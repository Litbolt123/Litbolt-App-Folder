# Litbolt Rainmeter widgets

Four desktop widgets matching Photo Hub / Photo Widget styling (`#12141A` background, `#E8A04A` gold accent, Segoe UI). Skins only read files under each widget's `@Resources` folder; feeders write `.inc` data on a schedule.

**Do not edit Photo Widget** — these live under `Rainmeter\Skins\Litbolt\` only.

## 1. Turn a widget on or off

**Rainmeter**

1. Right-click the Rainmeter tray icon → **Manage**.
2. Open the **Skins** tab → **Litbolt**.
3. Check or uncheck the `.ini` for **Helios Health**, **Today**, **Litbolt Stats**, or **Market**.

**Photo Hub**

1. **Widget** tab → **On/off** → **Litbolt widgets** checkboxes (same activate/deactivate as Rainmeter).

## 2. Helios Health

- **Needs:** [HWiNFO](https://www.hwinfo.com/) 7.02+ with **Reporting to Gadget** (not shared memory — free edition). Portable copy: `C:\Users\Augus\Desktop Folders\AcerPH18\hwi_854`. Setup: [Rainmeter HWiNFO tips](https://docs.rainmeter.net/tips/hwinfo/) — leave **Shared Memory** off; Sensors → **Configure Sensors** → **HWiNFO Gadget** → enable reporting; check **Report value in Gadget** for CPU package temp, CPU package power, GPU temp, fan RPM.
- **Configure indices:** Edit `Helios Health\@Resources\sensors.inc` — set `IndexCpuTemp`, `IndexCpuPower`, `IndexGpuTemp`, `IndexFanRpm` to each sensor's Gadget **Index** (list with `reg query HKCU\SOFTWARE\HWiNFO64\VSB`). Use `RegHKey=HKEY_LOCAL_MACHINE` if values only appear under HKLM.
- **No Rainmeter HWiNFO plugin** required.
- **ThrottleStop:** Shows a red **ThrottleStop is OFF** banner when `ThrottleStop.exe` is not running; green check when it is.
- **Temps:** &lt; 80°C normal, 80–94°C gold, ≥ 95°C red.

No feeder script (live HWiNFO registry values).

## 3. Today

**Calendar (Google)**

1. Open [Google Calendar](https://calendar.google.com/) → **Settings** → select **augustlittell@gmail.com** (or the calendar you want).
2. Scroll to **Integrate calendar** → copy **Secret address in iCal format** (private URL).
3. Paste into `Today\@Resources\calendar-config.json` → `"ICalUrl": "https://..."`  
   Do not commit this URL to git; keep it only on your PC.

**Countdowns**

Edit `Today\@Resources\countdowns.txt` — one per line:

```text
Event name | 2026-10-12 10:00
```

**Feeder:** `feeders\today-feed.ps1` → writes `Today\@Resources\data.inc`.  
**Schedule:** Task **Litbolt\TodayFeed** every 15 minutes (see §6).

## 4. Litbolt Stats

- Edit `Litbolt Stats\@Resources\stats-config.json` — CurseForge mod slugs; optional `"CurseForgeApiKey"` from [CurseForge Console](https://console.curseforge.com/) (never put the key in a `.ini` skin file).
- **Etsy:** Leave `"Configured": false` until you wire the Etsy API; skin shows **Set up Etsy**.
- **Feeder:** `feeders\stats-feed.ps1` (download counts + delta vs previous day in `stats-history.json`).
- **Schedule:** **Litbolt\StatsFeed** every 3 hours.

## 5. Market

- **Feeder:** `feeders\market-feed-gated.ps1` (calls `market-feed.ps1`) — MU, SNDK, BTC-USD via Yahoo's public chart endpoint; green/red for day change.
- **Schedule:** **Litbolt\MarketFeed** every 5 minutes; script throttles to 30 minutes outside 09:00–16:59 local.

## 6. Scheduled tasks (feeders)

From an **elevated** PowerShell:

```powershell
cd "C:\Users\Augus\Rainmeter\Skins\Litbolt\feeders"
.\register-tasks.ps1
```

Run feeders once manually:

```powershell
.\today-feed.ps1
.\stats-feed.ps1
.\market-feed.ps1
```

Unregister:

```powershell
.\register-tasks.ps1 -Unregister
```

Tasks run hidden PowerShell at **BelowNormal** priority inside each script.

## 7. Install / update from repo

Canonical copy in the Litbolt App Folder repo: `rainmeter-skins\Litbolt\`.  
To refresh Rainmeter's copy:

```powershell
& "C:\Users\Augus\Projects\Litbolt App Folder\scripts\install-litbolt-rainmeter-skins.ps1"
```

## 8. File map

| Widget | Skin ini | Data |
|--------|----------|------|
| Helios Health | `Helios Health\Helios Health.ini` | HWiNFO Gadget registry + `sensors.inc` |
| Today | `Today\Today.ini` | `data.inc` (feeder) |
| Litbolt Stats | `Litbolt Stats\Litbolt Stats.ini` | `data.inc` (feeder) |
| Market | `Market\Market.ini` | `data.inc` (feeder) |

Shared theme: `Litbolt\@Resources\Theme.inc`
