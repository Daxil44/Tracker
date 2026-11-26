# Tracker

**Live: https://tracker.daxil.workers.dev**

Personal daily trackers in one app, starting with **Water** (protein, sleep and more later).
Built with **Blazor WebAssembly (standalone, .NET 10)**. Frontend only, with no backend, accounts or paid services.
All data stays in the browser's `localStorage`.

## Run locally

Requires the .NET 10 SDK.

```powershell
dotnet run --project src/Tracker --launch-profile http
```

Then open http://localhost:5044.

`dotnet watch --project src/Tracker` gives hot reload while editing.

## Deploy (Cloudflare Workers, free)

Live at **https://tracker.daxil.workers.dev** (a static-assets Worker).
Every push to `main` deploys automatically.

Cloudflare Workers project settings (Workers & Pages → Create → Import a repository):

| Setting | Value |
| --- | --- |
| Project name | `tracker` (must match `name` in [wrangler.jsonc](wrangler.jsonc)) |
| Build command | `bash build.sh` |
| Deploy command | `npx wrangler deploy` |

[build.sh](build.sh) installs the .NET SDK pinned in [global.json](global.json) and runs
`dotnet publish` into `output/`. [wrangler.jsonc](wrangler.jsonc) uploads `output/wwwroot`
as static assets, with single-page-app fallback so routes like `/settings` load `index.html`.
No secrets are needed.

To check a production build locally:

```powershell
dotnet publish src/Tracker/Tracker.csproj -c Release -o output
```

## Features (Water)

- Animated SVG bottle that fills toward your daily goal, with partial-bottle counts
- Quick add (full bottle, half bottle, glass), custom amounts, undo, per-entry delete
- Last-7-days strip, today's log, automatic new day at midnight
- Settings: your name, bottle size, daily goal (in bottles or ml/L), units
- Reminders during office hours: interval, snooze, pause for today, stop at goal,
  Chrome notifications plus an in-app banner (only while the app is open in a tab)
- Light and dark themes, responsive from phone to desktop

## Project structure

```
Tracker/
├─ Tracker.slnx
└─ src/Tracker/
   ├─ Program.cs                 DI setup (no HttpClient, no server)
   ├─ Models/                    UserPreferences, WaterEntry, DailyRecord, HydrationProgress, ReminderState
   ├─ Serialization/             Source-generated System.Text.Json context (trim-safe)
   ├─ Services/
   │  ├─ LocalStorageService     Typed localStorage wrapper (JS interop)
   │  ├─ PreferencesService      Name, goal, bottle size, unit, theme, reminder settings
   │  ├─ WaterTrackingService    Daily records, add/undo/remove, day rollover
   │  ├─ ReminderService         Reminder scheduling + notifications
   │  ├─ ThemeService            Light/dark switching
   │  └─ VolumeFormat            ml/L formatting and parsing
   ├─ Layout/                    MainLayout (loads state), NavRail
   ├─ Pages/                     Dashboard (Water), Settings
   ├─ Components/                WaterBottle, WaterTracker, ProgressStats, TodayLog, WeekStrip,
   │                             GoalSettings, ReminderSettings, ReminderBanner, Icon
   └─ wwwroot/
      ├─ index.html              Pre-paint theme script, <base href>
      ├─ css/app.css             Design tokens (light-dark()), shared UI
      └─ js/tracker.js           The only JS: localStorage, theme attribute, notifications
```

## Storage

| Key | Contents |
| --- | --- |
| `tracker.preferences.v1` | `UserPreferences` JSON (shared across trackers) |
| `tracker.water.history.v1` | `{ version, days: { "yyyy-MM-dd": { goalMl, entries[] } } }` |
| `tracker.water.reminders.v1` | Reminder runtime state (last reminder, snooze, pause) |

Each day snapshots its goal, so changing the goal later doesn't rewrite history.
Unreadable data is copied to `<key>.corrupt` instead of being silently overwritten.

## Notes

- The daily goal is a personal target, not medical advice.
- Data is per browser and per device. Clearing site data removes it.
- Reminders can't fire when the browser is closed or the computer is asleep.
