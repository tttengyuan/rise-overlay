# Rise Overlay

基于 **[HunterPie](https://github.com/HunterPie/HunterPie)** 的 Monster Hunter Rise 专用紧凑叠层（「Rise血条」）。

## 归属与许可

本仓库是 HunterPie 的**瘦身 Fork**：在原版架构上主要提取 / 强化 **Rise 紧凑战斗 HUD** 相关模块（任务简报、怪物血条 / 部位 / 异常、DPS），默认关闭多游戏客户端、Discord、以及 Rise 侧大量非必要控件。

上游项目：https://github.com/HunterPie/HunterPie  

HunterPie 原版许可证条款适用于上游代码；详见仓库根目录 `LICENSE`。

## 做什么

Rise-only 叠层，聚焦：

- 任务简报（弱属性 / 推荐属性单行对齐）
- 怪物 HP、部位、异常状态
- 队伍 DPS
- 捕获线 / 怪异化标注
- 壳窗口「检查更新」（GitHub Releases）

**仅支持 Monster Hunter Rise。** World / Wilds 进程附加默认关闭；未使用的 Rise 控件默认 `Initialize=false`。

## UI 主题

壳窗口 **HUD 主题** 可选，写入配置后下次启动自动恢复（简报 + 战斗 HUD + DPS 共用同一主题）：

| ThemeId | 名称 |
| --- | --- |
| `Classic` | 现版金青（默认） |
| `Glass` | 玻璃 |
| `Parchment` | 羊皮纸 |
| `Soft` | 日系浅色 |
| `OledCoral` | OLED 珊瑚 |
| `GlassCoral` | 玻璃珊瑚 |

另有 **战斗动效** 总开关：虚弱 / 可捕越过阈值、晕眩、部位破坏、推荐弱属性脉冲、怪异化红轨。关闭后保留语义色，去掉脉冲类动画。

设计说明：`docs/superpowers/specs/2026-09-02-rise-overlay-themes-motion-design.md`

## Docs

更多设计与实现规划见 `docs/superpowers/`。

## Rise compact monster HUD

- New overlay widget: `MHROverlayConfig.RiseCompactMonsterWidget` (default `Initialize=true` for Rise).
- Hosted on the same HunterPie overlay path as other widgets: `RiseCompactMonsterView` : `Widget`, `RiseCompactMonsterViewModel` : `WidgetViewModel`, registered through `OverlayManager` → `WidgetView`.
- Default position: top-left (`Position` 20,20). Overlay defaults: `IsEnabled=true`, `HideWhenUnfocus=true` (hide when the game loses focus).
- Legacy HunterPie monster widget (`BossesWidget` / `MHRMonsterWidgetConfig`) defaults to `Initialize=false` on fresh configs so the compact HUD is not duplicated.
- Client config is persisted under the HunterPie client config path (typically next to the binary / ClientPath `config.json`).
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

Requires app restart after changing hotkeys. Persisted configs may still have older values until reset.


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

3. **Publish folder (V1):** Prefer the sync script (copies Languages + binaries; `dotnet publish` alone often **omits** `Languages\*.xml` and the client will start with no window):

```powershell
dotnet build HunterPie/HunterPie.csproj -c Release
powershell -ExecutionPolicy Bypass -File Scripts\sync-publish.ps1
```

Or after a raw `dotnet publish`, always copy languages:

```powershell
dotnet publish HunterPie/HunterPie.csproj -c Release -o F:\rise-overlay\publish\RiseOverlay
mkdir F:\rise-overlay\publish\RiseOverlay\Languages -Force
copy F:\rise-overlay\Localization\localization\*.xml F:\rise-overlay\publish\RiseOverlay\Languages\
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

## Updates (GitHub Releases)

Repo: https://github.com/tttengyuan/rise-overlay

**You (publisher):**

```powershell
powershell -ExecutionPolicy Bypass -File Scripts\release.ps1
# or pin version:
powershell -ExecutionPolicy Bypass -File Scripts\release.ps1 -Version 1.0.1
```

**Friends:** open the shell window → **检查更新** (needs network access to GitHub; VPN may help in CN).  
First install still needs one manual download from Releases.

