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
| 11 | Windowed mode: move game window — overlay follows | `[ ]` | **Partial:** widgets use absolute screen `Position` + `HideWhenUnfocus`; true client-relative attach not implemented |
| 12 | Long monster name truncated but recommend chips still visible | `[ ]` | |

---

## Shell / overlay (Task 13)

| # | Check | Status | Notes |
|---|--------|--------|-------|
| A | Compact widget registers via `Widget` / `OverlayManager` | `[x]` | Same path as other HunterPie overlays |
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
4. **Client-relative window follow** — Spec wants HUD offset locked to game client while dragging/resizing; V1 uses screen-space widget windows + focus hide, not true attach-to-client-rect.
5. **Native DLL** — `HunterPie.Native` is C++/VS; `dotnet publish` does not build it. Copy a prior successful Native x64 output into the publish folder when damage hooks / native features are required (Rise native module usage depends on game/config).

---

## Publish smoke (managed)

| # | Check | Status | Notes |
|---|--------|--------|-------|
| P1 | `dotnet build HunterPie/HunterPie.csproj` green | `[x]` | Managed |
| P2 | `dotnet publish … -o publish/RiseOverlay` | `[x]` | Managed publish succeeded 2026-08-31; Native DLL not in output |
| P3 | Native DLL present next to exe if needed | `[ ]` | Manual copy from VS Native build |

When finishing a play session, tick §7 and shell rows in a copy of this file or note results in the PR / release notes.
