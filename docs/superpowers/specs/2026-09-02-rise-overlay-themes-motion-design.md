# Rise Overlay — 多主题 HUD + 战斗动效设计

Date: 2026-09-02  
Status: approved (brainstorm)  
Scope: Rise compact overlay themes, quest briefing alignment, combat motion, README

## Goal

让用户在壳窗口自选 HUD 主题并持久化；任务简报 / 战斗 Monster HUD / DPS 共用同一主题；补齐虚弱 / 晕眩 / 破部位 / 推荐弱属性等撞色动效；修复任务简报弱属性行的上下落差；README 标明基于 HunterPie 的 Rise 瘦身 Fork 并列出主题。

## Non-goals (this round)

- 不为任务简报单独设置 ThemeId
- 不提供热键循环切换主题
- 不提供动效分项开关（仅总开关 `EnableCombatMotion`）
- 不重做壳窗口整体视觉（只加主题网格 + 动效开关）
- 不实现与主题无关的新战斗数据字段

## Decisions (locked)

| Item | Choice |
| --- | --- |
| Implementation path | A：壳窗口主题网格 + 配置持久化 |
| Theme surfaces | 任务简报 + 战斗 HUD + DPS + ElementChip 共用 `ThemeId` |
| Default theme | `Classic`（现版金青），避免老用户被突然换皮 |
| Briefing weak row | 「弱」+ 全体弱属性 chips +「推荐」+ 推荐 chips **强制单行**、同高、垂直居中、不换行 |
| Recommend pulse | 推荐弱属性 chip 呼吸描边；受 `EnableCombatMotion` 控制 |
| Qurio parts | 红轨 / 红字 / 红条；感染期间可轻呼吸 |
| Part HP truncation | 战斗部位长数值不得被 272px 右侧截断（数值与进度条排版保证完整可见） |
| Motion master switch | `EnableCombatMotion` 默认开；关闭后保留语义色与静态徽章，去掉脉冲/抖动/爆闪 |

## Themes

Six ResourceDictionary packs (IDs stable in config):

| ThemeId | Display (壳窗口) | Visual direction |
| --- | --- | --- |
| `Classic` | 现版金青 | 当前 CompactTeal / 金边青绿 |
| `Glass` | 玻璃 | 毛玻璃圆角、偏青 |
| `Parchment` | 羊皮纸 | Rise 暖金武具感 |
| `Soft` | 日系浅色 | 浅底柔色 |
| `OledCoral` | OLED 珊瑚 | 纯黑 + 珊瑚红强调 |
| `GlassCoral` | 玻璃珊瑚 | 玻璃结构 + 珊瑚强调（原 X1 混血） |

Cross-theme semantic colors (must not be lost when swapping packs):

- Qurio / 怪异化：红 (`#E11D48` family)
- Breakable：金 / 暖黄
- Severable：橙
- Broken / complete：灰
- Element chips：火/水/雷/冰/龙既有色板可随主题微调，但语义可辨

## Architecture

```
RiseShellWindow
  └─ Theme grid + EnableCombatMotion toggle
       └─ writes RiseCompactMonsterWidgetConfig.ThemeId / EnableCombatMotion
            └─ ClientConfig (AppData) persistence

RiseThemeService (new)
  └─ loads ThemeId → merges corresponding ResourceDictionary
  └─ applies to QuestBriefingView / MonsterHudView / DpsPanelView hosts
  └─ notifies on ThemeId change for live swap

Combat motion (WPF Storyboard / triggers)
  └─ edge-triggered: capture/weaken cross, part break/sever
  └─ state-held: stun active, qurio infected, recommend chip pulse
```

### Config

Extend `RiseCompactMonsterWidgetConfig`:

- `Observable<string> ThemeId` default `"Classic"` — values must be exactly the ThemeId column above
- `Observable<bool> EnableCombatMotion` default `true`

No restart required for theme or motion toggle when feasible; if dictionary swap needs unload/reload, live swap still preferred over restart.

### Quest briefing layout (required fix)

Current risk: 「弱」与「推荐」在视觉上出现上下落差或 chip 行高不一致。

Required layout:

```
[弱] [火][水][雷][冰][龙]  |  [推荐] [水◀pulse]
```

Rules:

- One horizontal flex/grid row; `nowrap`
- All chips fixed height (e.g. 16px), `VerticalAlignment=Center` / `align-items:center`
- Dimmed non-recommended overall elements stay on the same row
- Recommend chip uses pulse when motion enabled

### Combat HUD notes

- Keep ~272px width
- Part rows must show: status text + weak element chips + full HP values (no right clipping on large numbers)
- Qurio infected parts use red rail/text/bar
- Layout may be rearranged for beauty as long as information parity is preserved

## Combat motion matrix

| Trigger | Presentation | Duration |
| --- | --- | --- |
| Cross weaken / capturable threshold (edge) | Gold-red contrasting border pulse + optional banner sweep; HP bar brief flash | ~1.6s then static capturable styling |
| Stun active | Bright yellow emphasis; very light card shake | While stun lasts |
| Part break / sever (edge) | Orange burst on that row + short “破坏” cue → grey complete | ~1.2s |
| Qurio part | Red rail/text/bar + light breath | While infected |
| Recommended weak chip (briefing + parts) | Outline breath pulse | Continuous while shown; gated by motion switch |

## Shell UX

In `RiseShellWindow`, add section **HUD 主题**:

- 2-column (or wrap) grid of 6 theme swatches
- Selected theme highlighted; click applies immediately and saves
- Checkbox **战斗动效** bound to `EnableCombatMotion`
- Short hint: 「主题与动效能记住，下次打开自动恢复」

## README updates (same delivery)

Update `README.RiseOverlay.md` (and root README pointer if needed) to include:

1. Attribution: based on [HunterPie](https://github.com/HunterPie/HunterPie); this repo is a thin Rise-focused fork extracting/keeping the Rise compact overlay path rather than the full multi-game client surface
2. Current features: quest briefing, combat HUD (HP/parts/ailments), DPS, capture/qurio rules, GitHub check-for-updates
3. UI themes list + how to switch in the shell window
4. Keep license pointer; do not paste large upstream docs

## Testing

- Switch each ThemeId in shell → briefing, combat HUD, DPS all update; restart app → theme restored
- Briefing: five element chips + recommend chip stay on one baseline; no vertical stagger
- Motion off: no pulse/shake/burst; semantic colors remain
- Motion on: stun / capture-cross / part-break / qurio / recommend pulse behave per matrix
- High part HP values (5-digit) remain fully visible
- Qurio monster: badge + infected parts red across all themes

## Out of scope follow-ups

- Per-motion toggles
- Theme hotkey cycling
- Redesigning shell chrome beyond theme/motion controls
