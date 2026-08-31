# Rise Overlay QA Checklist

Date: 2026-08-31  
Spec: `docs/superpowers/specs/2026-08-31-rise-overlay-design.md` §7  
Scope: Monster Hunter Rise compact HUD (briefing + combat + DPS)

Legend: `[ ]` pending human / in-game · `[x]` covered by implementation or docs only (not a substitute for in-game QA)

---

## Spec §7 — in-game acceptance

| # | Check | Status | Notes |
|---|--------|--------|-------|
| 1 | Capturable normal monster: capture threshold line position | `[ ]` | Needs live quest |
| 2 | Capturable: over-threshold banner | `[ ]` | |
| 3 | Capturable: briefing chip (可捕 / threshold) | `[ ]` | Briefing may be empty if quest targets unresolved — see Known gaps |
| 4 | Elder / celestial (e.g. 天彗龙): not capturable | `[ ]` | |
| 5 | Elder / celestial: recommend shows tied top elements | `[ ]` | Static table + `ElementRecommend` |
| 6 | Severable tail: 「断」 marker + sever progress bar | `[ ]` | |
| 7 | Multi-target briefing ×2 | `[ ]` | Blocked until quest target keys available pre-spawn |
| 8 | Multi-target briefing ×4 | `[ ]` | Same as above |
| 9 | Solo DPS + total damage | `[ ]` | |
| 10 | 4-player multiplayer DPS + totals / share | `[ ]` | |
| 11 | Windowed mode: move game window — overlay follows | `[ ]` | **Not met in V1:** `OverlayManager`/`WidgetView` use absolute screen `Position` + topmost + `HideWhenUnfocus`; no client-rect follow / HWND parent. Compact HUD shares this path (not a separate layout). |
| 12 | Long monster name truncated but recommend chips still visible | `[ ]` | |

---

## Shell / overlay (Task 13)

| # | Check | Status | Notes |
|---|--------|--------|-------|
| A | Compact widget registers via `Widget` / `OverlayManager` | `[x]` | Same path as other HunterPie overlays (`WidgetView` topmost screen windows) |
| B | Default position top-left (20,20) | `[x]` | Fresh configs only |
| C | Overlay hide when game unfocused (`HideWhenUnfocus=true`) | `[x]` | Fresh configs; AppData may override |
| D | Toggle visibility hotkey **Ctrl+Alt+O** | `[ ]` | Reuses `OverlayClientConfig.ToggleVisibility` — verify in-game |
| E | Design mode ScrollLock | `[ ]` | |

---

## Rise-only surface (Task 14)

| # | Check | Status | Notes |
|---|--------|--------|-------|
| F | Only Rise process attaches | `[x]` | World/Wilds `CanAttach` returns false |
| G | Legacy Bosses / DamageMeter `Initialize=false` (Rise) | `[x]` | Fresh configs |
| H | Wirebug / class / activities / chat / etc. off | `[x]` | Fresh configs |
| I | Discord RPC default off; Discord sidebar hidden | `[x]` | |

---

## Known gaps (do not treat as pass)

1. **Briefing targets** — Rise `IQuest` has no target monster ids. Briefing shows only when target keys can be resolved from remembered `IGame.Monsters` / static store. Pre-spawn briefing is often empty until a quest-target memory read exists.
2. **AppData config overrides** — Client config under `%AppData%` (HunterPie path) persists prior `Initialize` / hotkey / position values. Fresh defaults apply only after reset or new profile.
3. **In-game QA pending** — All §7 rows above require human play against current Rise patch + matching address maps.
4. **Client-relative window follow** — Spec Task 13 wanted HUD offset locked to the game client while dragging/resizing. Upstream HunterPie does **not** provide that: `OverlayManager` only hosts independent topmost `WidgetView` windows at screen `Position` (plus process memory attach + focus hide). Compact HUD inherits that model — it is not independently positioned, but it also will **not** follow the game window. See `README.RiseOverlay.md` § Overlay attach model.
5. **Native DLL** — Injector expects `libs/HunterPie.Native.dll`. `dotnet publish` does not build C++ Native; build with VS2022 MSBuild (`HunterPie_Native`, x64) then copy into `publish/RiseOverlay/libs/` (and matching `bin/.../libs` for local runs).

---

## Publish smoke (managed)

| # | Check | Status | Notes |
|---|--------|--------|-------|
| P1 | `dotnet build HunterPie/HunterPie.csproj` green | `[x]` | Managed |
| P2 | `dotnet publish … -o publish/RiseOverlay` | `[x]` | Managed publish succeeded 2026-08-31 |
| P3 | Native DLL present under `publish/RiseOverlay/libs/` | `[x]` | Release `HunterPie_Native` x64 built + copied to `libs/` |

When finishing a play session, tick §7 and shell rows in a copy of this file or note results in the PR / release notes.
