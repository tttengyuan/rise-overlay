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

## Docs

Design and implementation planning for this fork live under `docs/superpowers/`.

## Rise compact monster HUD

- New overlay widget: `MHROverlayConfig.RiseCompactMonsterWidget` (default `Initialize=true` for Rise).
- Hosted on the same HunterPie overlay path as other widgets: `RiseCompactMonsterView` : `Widget`, `RiseCompactMonsterViewModel` : `WidgetViewModel`, registered through `OverlayManager` / `WidgetView` (process attach + always-on-top).
- Default position: top-left (`Position` 20,20). Overlay defaults: `IsEnabled=true`, `HideWhenUnfocus=true` (hide when the game loses focus).
- Legacy HunterPie monster widget (`BossesWidget` / `MHRMonsterWidgetConfig`) defaults to `Initialize=false` on fresh configs so the compact HUD is not duplicated.
- Client config is persisted under the HunterPie client config path (typically AppData). Existing installs that already saved `BossesWidget.Initialize=true` keep that value until reset.
- Static weakness table: `static/monsters-overlay.json` next to the app binary (from `RiseOverlay.Data/static`).

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
