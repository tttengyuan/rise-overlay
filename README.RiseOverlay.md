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
- Legacy HunterPie monster widget (`BossesWidget` / `MHRMonsterWidgetConfig`) defaults to `Initialize=false` on fresh configs so the compact HUD is not duplicated.
- Client config is persisted under the HunterPie client config path (typically AppData). Existing installs that already saved `BossesWidget.Initialize=true` keep that value until reset.
- Static weakness table: `static/monsters-overlay.json` next to the app binary (from `RiseOverlay.Data/static`).
