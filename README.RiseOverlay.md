# Rise Overlay

Thin fork / overlay work based on **[HunterPie](https://github.com/HunterPie/HunterPie)**.

## Attribution & license

This repository is based on HunterPie. HunterPie's original license terms apply to upstream code; see the root `LICENSE` file in this tree (and HunterPie's repository).

Upstream project: https://github.com/HunterPie/HunterPie

## Purpose

Rise Overlay aims to be a **Monster Hunter Rise-only** compact HUD overlay focused on:

- Monster HP
- Parts
- Ailments
- DPS

It is intentionally narrower in scope than full HunterPie (multi-game client / full feature set).

**Supported game:** Monster Hunter Rise only. World / Wilds process attach is disabled by default; World / Wilds widget configs keep `Initialize=false`. Discord Rich Presence and the Discord sidebar entry are off/hidden by default. Unused Rise widgets (wirebug, class meters, activities, chat, etc.) default to `Initialize=false` so the compact HUD is the primary surface. Persisted AppData configs may still enable older widgets until reset.

## Docs

Design and implementation planning for this fork live under `docs/superpowers/`.

## Rise compact monster HUD

- New overlay widget: `MHROverlayConfig.RiseCompactMonsterWidget` (default `Initialize=true` for Rise).
- Hosted on the same HunterPie overlay path as other widgets: `RiseCompactMonsterView` : `Widget`, `RiseCompactMonsterViewModel` : `WidgetViewModel`, registered through `OverlayManager` → `WidgetView`.
- Default position: top-left (`Position` 20,20). Overlay defaults: `IsEnabled=true`, `HideWhenUnfocus=true` (hide when the game loses focus).
- Legacy HunterPie monster widget (`BossesWidget` / `MHRMonsterWidgetConfig`) defaults to `Initialize=false` on fresh configs so the compact HUD is not duplicated.
- Client config is persisted under the HunterPie client config path (typically AppData). Existing installs that already saved `BossesWidget.Initialize=true` keep that value until reset.
- Static weakness table: `static/monsters-overlay.json` next to the app binary (from `RiseOverlay.Data/static`).

### Overlay attach model (Task 13)

HunterPie does **not** parent overlay HWNDs to the game client. Verified path:

| Layer | What it does |
| --- | --- |
| Process attach | `WindowsProcessWatcher` + Rise `IProcessAttachStrategy` open the game **process** for memory reads (not window ownership). |
| `OverlayManager` | Creates one topmost transparent `WidgetView` WPF window per widget and tracks focus/HUD visibility. |
| `WidgetView` | Screen-space `Left`/`Top` bound to widget `Position`; `Topmost` + periodic `SetWindowPos(HWND_TOPMOST)`. No `SetParent` / client-rect follow. |

Compact HUD uses that same path — it does **not** use independent custom screen logic, but it also does **not** auto-follow the game window when you drag/resize the client. Moving the game leaves HUD at its last screen coordinates; `HideWhenUnfocus` only hides when the game loses focus. True client-relative attach remains a known gap (see QA checklist §7 #11).

## Overlay toggle hotkey

Rise Overlay reuses HunterPie’s global overlay visibility hotkey (no separate `RiseOverlay.Toggle`):

| Action | Default keybinding | Config |
| --- | --- | --- |
| Show / hide overlay | **Ctrl+Alt+O** | `OverlayClientConfig.ToggleVisibility` |
| Design mode (drag widgets) | ScrollLock | `OverlayClientConfig.ToggleDesignMode` |

Requires app restart after changing hotkeys. Persisted AppData configs may still have older values until reset.

## Quest briefing ↔ combat scene switch

One host widget (`RiseCompactMonsterView`) swaps **QuestBriefingView** and **CombatStackView**.

### Event sources

- `IGame.OnQuestStart` / `OnQuestEnd`
- `IGame.OnMonsterSpawn` / `OnMonsterDespawn`
- `IPlayer.OnStageUpdate` (hunting-zone heuristic)

### Visibility heuristics

Rise `IQuest` exposes id/type/status/timer but **not** quest target monster ids (unlike Wilds’ target-key path). Gathering-hub vs map is approximated as:

| Scene | When |
| --- | --- |
| **Briefing** | Quest active, no alive large monsters yet (pre-combat this quest), and at least one target name/id key can be resolved via `MonsterStaticStore` + `MonsterHudMapper.ToBriefingTarget` |
| **Combat** | Quest active and any large monster with `Health > 0` is present |
| **Idle (hidden)** | Quest ended / idle, or quest active but neither briefing targets nor combat monsters |

Target keys are remembered from `IGame.Monsters` (name + id) when known. Pre-spawn briefing therefore only appears if keys are already available (e.g. monsters still tracked) or after a future quest-target memory read is added. Empty briefing panels are not shown.

## Build & publish (V1 folder distribution)

V1 ships as a **folder** from `dotnet publish` (no installer required). Native and managed builds are hybrid.

### Prerequisites

- .NET SDK matching `HunterPie` (`net10.0-windows`)
- Visual Studio 2022 with C++ desktop workload (for `HunterPie.Native` x64)
- Monster Hunter Rise address maps under the app `Address` folder (same as upstream HunterPie)

### Hybrid build

1. **Native (x64)** — open/build `HunterPie.Native/HunterPie.Native.vcxproj` with VS2022 MSBuild (Debug or Release | x64). `dotnet build` / `dotnet publish` do **not** compile this C++ project.
2. **Managed** — from repo root:

```powershell
dotnet build HunterPie/HunterPie.csproj -c Release
```

3. **Publish folder (V1):**

```powershell
dotnet publish HunterPie/HunterPie.csproj -c Release -o F:\rise-overlay\publish\RiseOverlay
```

4. **Native DLL** — injector loads `libs/HunterPie.Native.dll` (see `IPCInjectorInitializer`). After a VS Native x64 build, copy into both run and publish trees:

```powershell
# Debug run output (typical MSBuild destination)
# HunterPie\bin\Debug\net10.0-windows7.0\libs\HunterPie.Native.dll

New-Item -ItemType Directory -Force -Path publish\RiseOverlay\libs | Out-Null
Copy-Item -Force HunterPie\bin\Debug\net10.0-windows7.0\libs\HunterPie.Native.dll publish\RiseOverlay\libs\
# Prefer Release Native when shipping a Release publish folder, if that build succeeded.
```

If Native failed to build, managed overlay may still start; features that need the native module will not work until the DLL is under `libs\`.

5. Confirm `publish\RiseOverlay\static\monsters-overlay.json` exists (copied from `RiseOverlay.Data`).

### Run

Launch `HunterPie.exe` from the publish folder, start Monster Hunter Rise, then use **Ctrl+Alt+O** to toggle overlay visibility.

### QA

Manual acceptance checklist (spec §7 + known gaps):  
`docs/superpowers/specs/2026-08-31-rise-overlay-qa-checklist.md`

